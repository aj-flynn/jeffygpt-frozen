import type { ChatMessageDto, ChatStreamEvent, ToolCallDto, ToolResultDto } from './chatTypes';

export const MAX_STREAM_LINE_LENGTH = 1_000_000;

const TERMINAL_EVENT_TYPES = new Set(['done', 'error']);

export function isTerminalStreamEventType(type: string): boolean {
  return TERMINAL_EVENT_TYPES.has(type);
}

export function parseChatStreamLine(line: string): ChatStreamEvent {
  if (line.length > MAX_STREAM_LINE_LENGTH) {
    throw new Error('Chat stream line exceeded the maximum allowed length.');
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(line);
  } catch {
    throw new Error('Chat stream contained invalid JSON.');
  }

  if (!parsed || typeof parsed !== 'object') {
    throw new Error('Chat stream event must be a JSON object.');
  }

  const event = parsed as Record<string, unknown>;
  const type = event.type;
  if (typeof type !== 'string') {
    throw new Error('Chat stream event is missing a type.');
  }

  switch (type) {
    case 'assistant_delta':
      if (typeof event.content !== 'string') {
        throw new Error('assistant_delta event requires string content.');
      }
      return { type, content: event.content };

    case 'tool_call_started': {
      const toolCall = parseToolCall(event.toolCall);
      if (!toolCall) {
        throw new Error('tool_call_started event requires a toolCall payload.');
      }
      return { type, toolCall };
    }

    case 'tool_call_finished': {
      const toolResult = parseToolResult(event.toolResult);
      if (!toolResult) {
        throw new Error('tool_call_finished event requires a toolResult payload.');
      }
      return { type, toolResult };
    }

    case 'assistant_message':
    case 'assistant_tool_round': {
      const message = parseMessage(event.message);
      if (!message) {
        throw new Error(`${type} event requires a message payload.`);
      }
      return { type, message };
    }

    case 'error':
      if (typeof event.content !== 'string') {
        throw new Error('error event requires string content.');
      }
      return { type, content: event.content };

    case 'done':
      return { type };

    default:
      throw new Error(`Unsupported chat stream event type: ${type}`);
  }
}

function parseToolCall(value: unknown): ToolCallDto | null {
  if (!value || typeof value !== 'object') {
    return null;
  }

  const toolCall = value as Record<string, unknown>;
  if (
    typeof toolCall.id !== 'string' ||
    typeof toolCall.name !== 'string' ||
    typeof toolCall.argumentsJson !== 'string'
  ) {
    return null;
  }

  return {
    id: toolCall.id,
    name: toolCall.name,
    argumentsJson: toolCall.argumentsJson,
  };
}

function parseToolResult(value: unknown): ToolResultDto | null {
  if (!value || typeof value !== 'object') {
    return null;
  }

  const toolResult = value as Record<string, unknown>;
  if (
    typeof toolResult.toolCallId !== 'string' ||
    typeof toolResult.contentJson !== 'string' ||
    typeof toolResult.isError !== 'boolean'
  ) {
    return null;
  }

  return {
    toolCallId: toolResult.toolCallId,
    contentJson: toolResult.contentJson,
    isError: toolResult.isError,
  };
}

function parseMessage(value: unknown): ChatMessageDto | null {
  if (!value || typeof value !== 'object') {
    return null;
  }

  const message = value as Record<string, unknown>;
  if (typeof message.role !== 'string' || typeof message.content !== 'string') {
    return null;
  }

  return {
    role: message.role,
    content: message.content,
    toolCalls: Array.isArray(message.toolCalls)
      ? message.toolCalls
          .map((item) => parseToolCall(item))
          .filter((item): item is ToolCallDto => item !== null)
      : undefined,
    toolCallId: typeof message.toolCallId === 'string' ? message.toolCallId : undefined,
  };
}
