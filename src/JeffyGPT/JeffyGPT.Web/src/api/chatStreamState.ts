import {
  markHuntEpisodesComplete,
  recordHuntTool,
  removeRunningTool,
  type HuntEpisode,
  type RunningTool,
} from './chatActivity';
import { lastUserMessageId, type ChatMessageWithId } from './chatHistory';
import type { ChatMessageDto, ChatStreamEvent } from './chatTypes';

export type AssistantDraft = { status: 'idle' } | { status: 'streaming'; text: string };

export const idleAssistantDraft: AssistantDraft = { status: 'idle' };

export interface ChatStreamReducerState {
  messages: ChatMessageWithId[];
  pendingAssistantCommentary: ChatMessageWithId | null;
  assistantDraft: AssistantDraft;
  runningTools: RunningTool[];
  huntEpisodes: HuntEpisode[];
  huntUserMessageId: string | null;
  errorContent: string | null;
  streamFailed: boolean;
}

export function createChatStreamReducerState(
  messages: ChatMessageWithId[],
  huntEpisodes: HuntEpisode[] = [],
): ChatStreamReducerState {
  return {
    messages,
    pendingAssistantCommentary: null,
    assistantDraft: idleAssistantDraft,
    runningTools: [],
    huntEpisodes: markHuntEpisodesComplete(huntEpisodes),
    huntUserMessageId: lastUserMessageId(messages),
    errorContent: null,
    streamFailed: false,
  };
}

/** Shows an empty assistant bubble before the first token arrives. */
export function startAssistantDraft(state: ChatStreamReducerState): ChatStreamReducerState {
  return {
    ...state,
    assistantDraft: { status: 'streaming', text: '' },
    runningTools: [],
  };
}

/** Drops the optimistic user row when the server did not commit that turn. */
export function dropUncommittedUserMessage(
  state: ChatStreamReducerState,
  userMessageId: string,
): ChatStreamReducerState {
  const ended = endStream(state);
  const messages = ended.messages.filter((message) => message.id !== userMessageId);

  return {
    ...ended,
    messages,
    huntUserMessageId: lastUserMessageId(messages),
  };
}

/** Clears transient activity when a stream finishes or stops. */
export function endStream(state: ChatStreamReducerState): ChatStreamReducerState {
  return {
    ...state,
    pendingAssistantCommentary: null,
    assistantDraft: idleAssistantDraft,
    runningTools: [],
    huntEpisodes: markHuntEpisodesComplete(state.huntEpisodes),
  };
}

function toDisplayMessage(message: ChatMessageDto, id: string): ChatMessageWithId | null {
  if (message.role !== 'user' && message.role !== 'assistant') {
    return null;
  }

  return { id, role: message.role, content: message.content };
}

function appendAssistantDraft(draft: AssistantDraft, content: string): AssistantDraft {
  const text = draft.status === 'streaming' ? draft.text : '';
  return { status: 'streaming', text: text + content };
}

/**
 * Updates display messages and tool activity from server events.
 * Pure and testable without React.
 */
export function reduceChatStreamEvent(
  state: ChatStreamReducerState,
  event: ChatStreamEvent,
): ChatStreamReducerState {
  if (state.streamFailed) {
    return state;
  }

  switch (event.type) {
    case 'assistant_delta':
      return {
        ...state,
        assistantDraft: appendAssistantDraft(state.assistantDraft, event.content),
      };

    case 'tool_call_started': {
      const tool = {
        id: event.toolCall.id,
        toolName: event.toolCall.name,
      };

      return {
        ...state,
        assistantDraft: idleAssistantDraft,
        huntEpisodes: recordHuntTool(state.huntEpisodes, state.huntUserMessageId, tool),
        runningTools: [...state.runningTools, tool],
      };
    }

    case 'tool_call_finished':
      return {
        ...state,
        runningTools: removeRunningTool(state.runningTools, event.toolResult.toolCallId),
      };

    case 'assistant_tool_round':
      return {
        ...state,
        pendingAssistantCommentary: event.message.content.trim()
          ? toDisplayMessage(event.message, `commentary-${state.huntUserMessageId}`)
          : null,
        assistantDraft: idleAssistantDraft,
      };

    case 'assistant_message': {
      const message = toDisplayMessage(event.message, `reply-${state.huntUserMessageId}`);
      const messages = [...state.messages];
      if (message) {
        if (state.pendingAssistantCommentary) {
          messages.push(state.pendingAssistantCommentary);
        }
        messages.push(message);
      }

      return { ...endStream(state), messages };
    }

    case 'error':
      return {
        ...endStream(state),
        streamFailed: true,
        errorContent: event.content,
      };

    case 'done':
      return endStream(state);

    default:
      return state;
  }
}
