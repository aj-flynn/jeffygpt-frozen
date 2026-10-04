/**
 * Committed history keeps tool-round messages so a turn stays complete.
 * Those messages are hidden from the transcript and are not resent.
 */
import type { ChatMessageDto, ChatRequest, ChatStreamEvent } from './chatTypes';

export function isToolRoleMessage(message: ChatMessageDto): boolean {
  return message.role === 'tool';
}

export function isToolOnlyAssistantRound(message: ChatMessageDto): boolean {
  return (
    message.role === 'assistant' && (message.toolCalls?.length ?? 0) > 0 && !message.content?.trim()
  );
}

export function isTranscriptVisibleMessage(message: ChatMessageDto): boolean {
  if (isToolRoleMessage(message)) {
    return false;
  }

  if (isToolOnlyAssistantRound(message)) {
    return false;
  }

  return true;
}

/** Roles the API accepts on client chat requests (tool/system rounds stay server-side). */
export function isOutboundClientRole(role: string): boolean {
  return role === 'user' || role === 'assistant';
}

/**
 * Build the message list for POST /api/chat(/stream): user/assistant content only.
 * Drops tool transcripts and strips toolCalls/toolCallId so history cannot inject tool rounds.
 */
export function toOutboundRequestMessages(messages: ChatMessageDto[]): ChatMessageDto[] {
  return messages
    .filter(
      (message) =>
        isOutboundClientRole(message.role) &&
        !isToolOnlyAssistantRound(message) &&
        !isToolRoleMessage(message),
    )
    .map((message) => ({
      role: message.role,
      content: message.content ?? '',
    }));
}

export interface ChatMessageWithId extends ChatMessageDto {
  id: string;
}

/** Strips the client-only id so a transcript message can be sent to the API. */
export function toApiMessage(message: ChatMessageWithId): ChatMessageDto {
  const { id: _id, ...apiMessage } = message;
  return apiMessage;
}

export function toOutboundChatRequest(messages: ChatMessageWithId[]): ChatRequest {
  return { messages: toOutboundRequestMessages(messages.map(toApiMessage)) };
}

export interface StreamHistoryState {
  committedMessages: ChatMessageWithId[];
  pendingAssistantToolRound: ChatMessageDto | null;
  pendingToolMessages: ChatMessageDto[];
}

export function lastUserMessageId(messages: ChatMessageWithId[]): string | null {
  for (let index = messages.length - 1; index >= 0; index -= 1) {
    const message = messages[index];
    if (message?.role === 'user') {
      return message.id;
    }
  }

  return null;
}

export function createStreamHistoryState(messages: ChatMessageWithId[]): StreamHistoryState {
  return {
    committedMessages: messages,
    pendingAssistantToolRound: null,
    pendingToolMessages: [],
  };
}

function withMessageId(message: ChatMessageDto): ChatMessageWithId {
  return {
    ...message,
    id: crypto.randomUUID(),
  };
}

function commitPendingToolRound(state: StreamHistoryState): ChatMessageWithId[] {
  const toCommit: ChatMessageDto[] = [];

  if (state.pendingAssistantToolRound) {
    toCommit.push(state.pendingAssistantToolRound);
  }

  toCommit.push(...state.pendingToolMessages);

  return toCommit.map(withMessageId);
}

function sanitizeCommittedMessage(message: ChatMessageDto): ChatMessageDto | null {
  if (!isOutboundClientRole(message.role) && message.role !== 'tool') {
    return null;
  }

  if (message.role === 'assistant' || message.role === 'user') {
    return {
      role: message.role,
      content: message.content ?? '',
      toolCalls: message.toolCalls,
      toolCallId: message.toolCallId,
    };
  }

  return {
    role: 'tool',
    content: message.content ?? '',
    toolCallId: message.toolCallId,
  };
}

export function applyStreamEvent(
  state: StreamHistoryState,
  event: ChatStreamEvent,
): StreamHistoryState {
  switch (event.type) {
    case 'assistant_tool_round': {
      const message = sanitizeCommittedMessage(event.message);
      if (!message) {
        return state;
      }

      return {
        ...state,
        pendingAssistantToolRound: message,
        pendingToolMessages: [],
      };
    }

    case 'tool_call_finished':
      return {
        ...state,
        pendingToolMessages: [
          ...state.pendingToolMessages,
          {
            role: 'tool',
            content: event.toolResult.contentJson,
            toolCallId: event.toolResult.toolCallId,
          },
        ],
      };

    case 'assistant_message': {
      const message = sanitizeCommittedMessage(event.message);
      if (!message) {
        return {
          ...state,
          pendingAssistantToolRound: null,
          pendingToolMessages: [],
        };
      }

      const committedRound = commitPendingToolRound(state);
      return {
        committedMessages: [...state.committedMessages, ...committedRound, withMessageId(message)],
        pendingAssistantToolRound: null,
        pendingToolMessages: [],
      };
    }

    case 'error':
      return {
        ...state,
        pendingAssistantToolRound: null,
        pendingToolMessages: [],
      };

    default:
      return state;
  }
}
