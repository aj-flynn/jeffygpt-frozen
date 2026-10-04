import { afterEach, describe, expect, it, vi } from 'vitest';
import { streamChat } from './chatClient';
import type { ChatRequest, ChatStreamEvent } from './chatTypes';

const request: ChatRequest = {
  messages: [{ role: 'user', content: 'hello' }],
};

function ndjsonResponse(chunks: string[]): Response {
  const encoder = new TextEncoder();
  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const chunk of chunks) {
        controller.enqueue(encoder.encode(chunk));
      }
      controller.close();
    },
  });

  return new Response(stream, { status: 200 });
}

describe('streamChat', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('reads ndjson, skips a blank line, and ignores anything after done', async () => {
    const signal = new AbortController().signal;
    const events: ChatStreamEvent[] = [];
    const fetchMock = vi.fn(async () =>
      ndjsonResponse([
        '{"type":"assistant_delta","content":"Hi"}\n\n',
        '{"type":"done"}\n{"type":"assistant_delta","content":"late"}',
      ]),
    );
    vi.stubGlobal('fetch', fetchMock);

    await streamChat(request, {
      signal,
      onEvent: (event) => events.push(event),
    });

    expect(fetchMock).toHaveBeenCalledWith('/api/chat/stream', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/x-ndjson',
      },
      body: JSON.stringify(request),
      signal,
    });
    expect(events).toEqual([{ type: 'assistant_delta', content: 'Hi' }, { type: 'done' }]);
  });

  it('throws the server error message', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(JSON.stringify({ error: 'Jeffy is busy.' }), { status: 503 })),
    );

    await expect(streamChat(request, { onEvent: () => {} })).rejects.toThrow('Jeffy is busy.');
  });

  it('throws the status when the error body is not JSON', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('nope', { status: 502 })),
    );

    await expect(streamChat(request, { onEvent: () => {} })).rejects.toThrow(
      'Chat stream failed: 502',
    );
  });

  it('rejects a response that has no body', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({ ok: true, status: 200, body: null }) as Response),
    );

    await expect(streamChat(request, { onEvent: () => {} })).rejects.toThrow(
      /did not include a body/i,
    );
  });

  it('rejects a stream that ends without done or error', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ndjsonResponse(['{"type":"assistant_delta","content":"Hi"}\n'])),
    );

    await expect(streamChat(request, { onEvent: () => {} })).rejects.toThrow(/ended unexpectedly/i);
  });
});
