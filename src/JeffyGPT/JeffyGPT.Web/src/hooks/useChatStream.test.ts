import { act, createElement } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { streamChat, type StreamChatOptions } from '../api/chatClient';
import type { ChatRequest } from '../api/chatTypes';
import { useChatStream, type UseChatStreamResult } from './useChatStream';

globalThis.IS_REACT_ACT_ENVIRONMENT = true;

vi.mock('../api/chatClient', () => ({
  streamChat: vi.fn(),
}));

const mockedStreamChat = vi.mocked(streamChat);

/** A hook has to render before you can call it. */
function mountChatStream() {
  const host = document.createElement('div');
  document.body.appendChild(host);
  const root: Root = createRoot(host);
  let current: UseChatStreamResult | null = null;

  function Probe() {
    current = useChatStream();
    return null;
  }

  act(() => {
    root.render(createElement(Probe));
  });

  return {
    get current() {
      if (!current) {
        throw new Error('Chat stream probe did not render.');
      }

      return current;
    },
    unmount() {
      act(() => {
        root.unmount();
      });
      host.remove();
    },
  };
}

describe('useChatStream', () => {
  let probe: ReturnType<typeof mountChatStream>;

  beforeEach(() => {
    mockedStreamChat.mockReset();
    probe = mountChatStream();
  });

  afterEach(() => {
    probe.unmount();
  });

  it('does not send whitespace', async () => {
    await act(async () => {
      await probe.current.send('   ');
    });

    expect(mockedStreamChat).not.toHaveBeenCalled();
  });

  it('records the user message when the stream finishes', async () => {
    mockedStreamChat.mockImplementation(
      async (_request: ChatRequest, options: StreamChatOptions) => {
        options.onEvent({ type: 'assistant_delta', content: 'Hi' });
        options.onEvent({ type: 'done' });
      },
    );

    await act(async () => {
      await probe.current.send('hello');
    });

    expect(probe.current.messages).toEqual([
      expect.objectContaining({ role: 'user', content: 'hello' }),
    ]);
    expect(probe.current.sending).toBe(false);
    expect(probe.current.error).toBeNull();
  });

  it('shows an error when the stream fails', async () => {
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    mockedStreamChat.mockRejectedValue(new Error('Unable to reach Jeffy.'));

    await act(async () => {
      await probe.current.send('hello');
    });

    expect(probe.current.error).toBe('Unable to reach Jeffy.');
    expect(probe.current.sending).toBe(false);
    errorSpy.mockRestore();
  });

  it('clears the error when the send is aborted', async () => {
    mockedStreamChat.mockRejectedValue(new DOMException('Aborted', 'AbortError'));

    await act(async () => {
      await probe.current.send('hello');
    });

    expect(probe.current.error).toBeNull();
    expect(probe.current.sending).toBe(false);
  });

  it('aborts the in-flight message when a new one is sent', async () => {
    const signals: AbortSignal[] = [];
    let releaseFirst: () => void = () => {};
    const firstGate = new Promise<void>((resolve) => {
      releaseFirst = resolve;
    });
    let releaseSecond: () => void = () => {};
    const secondGate = new Promise<void>((resolve) => {
      releaseSecond = resolve;
    });

    mockedStreamChat.mockImplementation((_request: ChatRequest, options: StreamChatOptions) => {
      const signal = options.signal;
      if (!signal) {
        throw new Error('send should pass an abort signal.');
      }

      signals.push(signal);
      return signals.length === 1 ? firstGate : secondGate;
    });

    let firstSend!: Promise<void>;
    let secondSend!: Promise<void>;
    await act(async () => {
      const send = probe.current.send;
      firstSend = send('first');
      secondSend = send('second');
    });

    expect(signals).toHaveLength(2);
    expect(signals[0]?.aborted).toBe(true);
    expect(signals[1]?.aborted).toBe(false);

    await act(async () => {
      releaseFirst();
      await firstSend;
    });

    act(() => {
      probe.current.cancel();
    });
    expect(signals[1]?.aborted).toBe(true);

    await act(async () => {
      releaseSecond();
      await secondSend;
    });
  });
});
