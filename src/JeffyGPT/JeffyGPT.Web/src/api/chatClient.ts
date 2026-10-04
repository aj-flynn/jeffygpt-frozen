import type { ChatRequest, ChatResponse, ChatStreamEvent } from './chatTypes';
import { isTerminalStreamEventType, parseChatStreamLine } from './chatStreamParse';

export async function postChat(request: ChatRequest): Promise<ChatResponse> {
  const response = await fetch('/api/chat', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const body = (await response.json().catch(() => null)) as { error?: string } | null;
    throw new Error(body?.error ?? `Chat request failed: ${response.status}`);
  }

  return response.json() as Promise<ChatResponse>;
}

export interface StreamChatOptions {
  signal?: AbortSignal;
  onEvent: (event: ChatStreamEvent) => void;
}

export async function streamChat(request: ChatRequest, options: StreamChatOptions): Promise<void> {
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
    const body = (await response.json().catch(() => null)) as { error?: string } | null;
    throw new Error(body?.error ?? `Chat stream failed: ${response.status}`);
  }

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
