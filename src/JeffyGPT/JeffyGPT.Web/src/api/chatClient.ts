import type { ChatResponse, ChatStreamEvent, ChatTurnRequest } from './chatTypes';
import { isTerminalStreamEventType, parseChatStreamLine } from './chatStreamParse';

export const conversationIdHeader = 'X-Conversation-Id';

export class ChatHttpError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = 'ChatHttpError';
    this.status = status;
  }
}

export function isChatHttpError(err: unknown): err is ChatHttpError {
  return err instanceof ChatHttpError;
}

export async function postChat(request: ChatTurnRequest): Promise<ChatResponse> {
  const response = await fetch('/api/chat', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw await readHttpError(response, 'Chat request failed');
  }

  return response.json() as Promise<ChatResponse>;
}

export interface StreamChatOptions {
  signal?: AbortSignal;
  onEvent: (event: ChatStreamEvent) => void;
  onConversationId?: (conversationId: string) => void;
}

export async function streamChat(request: ChatTurnRequest, options: StreamChatOptions): Promise<string> {
  const response = await fetch('/api/chat/stream', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/x-ndjson',
    },
    body: JSON.stringify(request),
    signal: options.signal,
  });

  if (!response.ok) {
    throw await readHttpError(response, 'Chat stream failed');
  }

  const conversationId = response.headers.get(conversationIdHeader);
  if (!conversationId) {
    throw new Error('Chat stream response did not include a conversation id.');
  }

  options.onConversationId?.(conversationId);

  if (!response.body) {
    throw new Error('Chat stream response did not include a body.');
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  let sawTerminalEvent = false;

  const handleLine = (line: string) => {
    if (sawTerminalEvent) {
      return;
    }

    const event = parseChatStreamLine(line);
    options.onEvent(event);

    if (isTerminalStreamEventType(event.type)) {
      sawTerminalEvent = true;
    }
  };

  while (true) {
    const { done, value } = await reader.read();
    if (done) {
      break;
    }

    buffer += decoder.decode(value, { stream: true });
    buffer = consumeNdjsonLines(buffer, handleLine);
  }

  const trailingLine = buffer.trim();
  if (trailingLine.length > 0) {
    handleLine(trailingLine);
  }

  assertSawTerminalEvent(sawTerminalEvent);
  return conversationId;
}

async function readHttpError(response: Response, fallbackPrefix: string): Promise<ChatHttpError> {
  const body = (await response.json().catch(() => null)) as { error?: string } | null;
  const message = body?.error ?? `${fallbackPrefix}: ${response.status}`;
  return new ChatHttpError(response.status, message);
}

function consumeNdjsonLines(buffer: string, onLine: (line: string) => void): string {
  let remaining = buffer;

  while (true) {
    const newlineIndex = remaining.indexOf('\n');
    if (newlineIndex === -1) {
      return remaining;
    }

    const line = remaining.slice(0, newlineIndex).trim();
    remaining = remaining.slice(newlineIndex + 1);

    if (line.length > 0) {
      onLine(line);
    }
  }
}

function assertSawTerminalEvent(sawTerminalEvent: boolean): void {
  if (!sawTerminalEvent) {
    throw new Error('Chat stream ended unexpectedly before a done or error event.');
  }
}
