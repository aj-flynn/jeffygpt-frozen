import { afterEach, describe, expect, it, vi } from 'vitest';
import { ChatHttpError, conversationIdHeader, streamChat } from './chatClient';
import type { ChatStreamEvent } from './chatTypes';

function ndjsonResponse(chunks: string[], conversationId = 'conv-1'): Response {
  const encoder = new TextEncoder();
  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const chunk of chunks) {
        controller.enqueue(encoder.encode(chunk));
      }
      controller.close();
    },
  });

  return new Response(stream, {
    status: 200,
    headers: { [conversationIdHeader]: conversationId },
  });
}

describe('streamChat', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('posts the new text, reads ndjson, and returns the conversation id', async () => {
    const signal = new AbortController().signal;
    const events: ChatStreamEvent[] = [];
    const fetchMock = vi.fn(async () =>
      ndjsonResponse([
        '{"type":"assistant_delta","content":"Hi"}\n\n',
        '{"type":"done"}\n{"type":"assistant_delta","content":"late"}',
      ]),
    );
    vi.stubGlobal('fetch', fetchMock);

    const conversationId = await streamChat(
      { content: 'hello', conversationId: 'conv-1' },
      {
        signal,
        onEvent: (event) => events.push(event),
      },
    );

    expect(fetchMock).toHaveBeenCalledWith('/api/chat/stream', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/x-ndjson',
      },
      body: JSON.stringify({ content: 'hello', conversationId: 'conv-1' }),
      signal,
    });
    expect(conversationId).toBe('conv-1');
    expect(events).toEqual([{ type: 'assistant_delta', content: 'Hi' }, { type: 'done' }]);
  });

  it('throws the server error message with the status', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(JSON.stringify({ error: 'Jeffy is busy.' }), { status: 503 })),
    );

    await expect(streamChat({ content: 'hello' }, { onEvent: () => {} })).rejects.toEqual(
      expect.objectContaining({ message: 'Jeffy is busy.', status: 503 }),
    );
  });

  it('throws the status when the error body is not JSON', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('nope', { status: 502 })),
    );

    const error = await streamChat({ content: 'hello' }, { onEvent: () => {} }).catch(
      (err: unknown) => err,
    );

    expect(error).toBeInstanceOf(ChatHttpError);
    expect((error as ChatHttpError).message).toBe('Chat stream failed: 502');
    expect((error as ChatHttpError).status).toBe(502);
  });

  it('rejects a response that has no conversation id', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('{"type":"done"}\n', { status: 200 })),
    );

    await expect(streamChat({ content: 'hello' }, { onEvent: () => {} })).rejects.toThrow(
      /conversation id/i,
    );
  });

  it('rejects a response that has no body', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          ({
            ok: true,
            status: 200,
            body: null,
            headers: new Headers({ [conversationIdHeader]: 'conv-1' }),
          }) as Response,
      ),
    );

    await expect(streamChat({ content: 'hello' }, { onEvent: () => {} })).rejects.toThrow(
      /did not include a body/i,
    );
  });

  it('rejects a stream that ends without done or error', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ndjsonResponse(['{"type":"assistant_delta","content":"Hi"}\n'])),
    );

    await expect(streamChat({ content: 'hello' }, { onEvent: () => {} })).rejects.toThrow(
      /ended unexpectedly/i,
    );
  });
});
