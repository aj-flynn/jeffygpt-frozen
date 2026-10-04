import {
  markHuntEpisodesComplete,
  recordHuntTool,
  removeRunningTool,
  type HuntEpisode,
  type RunningTool,
} from './chatActivity';
import {
  applyStreamEvent,
  createStreamHistoryState,
  lastUserMessageId,
  type ChatMessageWithId,
  type StreamHistoryState,
} from './chatHistory';
import type { ChatStreamEvent } from './chatTypes';

export type AssistantDraft = { status: 'idle' } | { status: 'streaming'; text: string };

export const idleAssistantDraft: AssistantDraft = { status: 'idle' };

export interface ChatStreamReducerState {
  history: StreamHistoryState;
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
    history: createStreamHistoryState(messages),
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

/** Clears the live bubble and running tools when the stream ends without a completion event. */
export function endStreamWithoutCompletion(state: ChatStreamReducerState): ChatStreamReducerState {
  return {
    ...state,
    assistantDraft: idleAssistantDraft,
    runningTools: [],
    huntEpisodes: markHuntEpisodesComplete(state.huntEpisodes),
  };
}

function withHuntComplete(
  state: ChatStreamReducerState,
  history: StreamHistoryState,
): ChatStreamReducerState {
  return {
    ...state,
    history,
    assistantDraft: idleAssistantDraft,
    runningTools: [],
    huntEpisodes: markHuntEpisodesComplete(state.huntEpisodes),
  };
}

function appendAssistantDraft(draft: AssistantDraft, content: string): AssistantDraft {
  const text = draft.status === 'streaming' ? draft.text : '';
  return { status: 'streaming', text: text + content };
}

/**
 * Single entry point for stream event handling: updates API history and transient UI state.
 * Pure and testable without React.
 */
export function reduceChatStreamEvent(
  state: ChatStreamReducerState,
  event: ChatStreamEvent,
): ChatStreamReducerState {
  if (state.streamFailed) {
    return state;
  }

  const history = applyStreamEvent(state.history, event);

  switch (event.type) {
    case 'assistant_delta':
      return {
        ...state,
        history,
        assistantDraft: appendAssistantDraft(state.assistantDraft, event.content),
      };

    case 'tool_call_started': {
      const tool = {
        id: event.toolCall.id,
        toolName: event.toolCall.name,
      };

      return {
        ...state,
        history,
        assistantDraft: idleAssistantDraft,
        huntEpisodes: recordHuntTool(state.huntEpisodes, state.huntUserMessageId, tool),
        runningTools: [...state.runningTools, tool],
      };
    }

    case 'tool_call_finished':
      return {
        ...state,
        history,
        runningTools: removeRunningTool(state.runningTools, event.toolResult.toolCallId),
      };

    case 'assistant_tool_round':
      return {
        ...state,
        history,
        assistantDraft: idleAssistantDraft,
      };

    case 'assistant_message':
      return withHuntComplete(state, history);

    case 'error':
      return {
        ...withHuntComplete(state, history),
        streamFailed: true,
        errorContent: event.content,
      };

    case 'done':
      return withHuntComplete(state, history);

    default:
      return { ...state, history };
  }
}

/** Messages to show in the transcript after a stream event reduction. */
export function committedMessagesFromReducerState(
  state: ChatStreamReducerState,
): ChatMessageWithId[] {
  return state.history.committedMessages;
}
