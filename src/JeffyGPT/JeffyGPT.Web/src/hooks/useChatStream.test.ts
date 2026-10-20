import { act, createElement } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ChatHttpError, streamChat, type StreamChatOptions } from '../api/chatClient';
import { useChatStream, type UseChatStreamResult } from './useChatStream';

globalThis.IS_REACT_ACT_ENVIRONMENT = true;

vi.mock('../api/chatClient', async () => {
  const actual = await vi.importActual<typeof import('../api/chatClient')>('../api/chatClient');
  return {
    ...actual,
    streamChat: vi.fn(async () => 'conv-test'),
  };
});

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
    mockedStreamChat.mockResolvedValue('conv-test');
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

  it('keeps the user message when the assistant message arrives', async () => {
    mockedStreamChat.mockImplementation(async (_request, options: StreamChatOptions) => {
      options.onEvent({ type: 'assistant_delta', content: 'Hi' });
      options.onEvent({
        type: 'assistant_message',
        message: { role: 'assistant', content: 'Hi' },
      });
      options.onEvent({ type: 'done' });
      return 'conv-test';
    });

    await act(async () => {
      await probe.current.send('hello');
    });

    expect(mockedStreamChat).toHaveBeenCalledWith({ content: 'hello' }, expect.any(Object));
    expect(probe.current.messages).toEqual([
      expect.objectContaining({ role: 'user', content: 'hello' }),
      expect.objectContaining({ role: 'assistant', content: 'Hi' }),
    ]);
    expect(probe.current.sending).toBe(false);
    expect(probe.current.error).toBeNull();

    await act(async () => {
      await probe.current.send('again');
    });

    expect(mockedStreamChat).toHaveBeenLastCalledWith(
      { content: 'again', conversationId: 'conv-test' },
      expect.any(Object),
    );
  });

  it('remembers the conversation id when the body fails after the header', async () => {
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    mockedStreamChat.mockImplementation(async (_request, options: StreamChatOptions) => {
      options.onConversationId?.('conv-kept');
      throw new Error('Chat stream ended unexpectedly before a done or error event.');
    });

    await act(async () => {
      await probe.current.send('hello');
    });

    mockedStreamChat.mockResolvedValue('conv-kept');
    await act(async () => {
      await probe.current.send('again');
    });

    expect(mockedStreamChat).toHaveBeenLastCalledWith(
      { content: 'again', conversationId: 'conv-kept' },
      expect.any(Object),
    );
    errorSpy.mockRestore();
  });

  it('drops the user message when the stream ends without an assistant message', async () => {
    mockedStreamChat.mockImplementation(async (_request, options: StreamChatOptions) => {
      options.onEvent({ type: 'assistant_delta', content: 'Hi' });
      options.onEvent({ type: 'done' });
      return 'conv-test';
    });

    await act(async () => {
      await probe.current.send('hello');
    });

    expect(probe.current.messages).toEqual([]);
    expect(probe.current.error).toBeNull();
  });

  it('renders live tool activity and keeps only display messages after the turn', async () => {
    let options!: StreamChatOptions;
    let complete!: (conversationId: string) => void;
    mockedStreamChat.mockImplementation((_request, streamOptions) => {
      options = streamOptions;
      return new Promise((resolve) => {
        complete = resolve;
      });
    });

    let sending!: Promise<void>;
    act(() => {
      sending = probe.current.send('what time is it');
    });
    expect(probe.current.assistantDraft).toEqual({ status: 'streaming', text: '' });

    act(() => {
      options.onEvent({
        type: 'tool_call_started',
        toolCall: { id: 'clock', name: 'get-current-time', argumentsJson: '{}' },
      });
    });
    expect(probe.current.runningTools).toEqual([{ id: 'clock', toolName: 'get-current-time' }]);
    expect(probe.current.huntEpisodes[0]?.status).toBe('hunting');

    await act(async () => {
      options.onEvent({
        type: 'tool_call_finished',
        toolResult: { toolCallId: 'clock', contentJson: '{"local":"midnight"}', isError: false },
      });
      options.onEvent({ type: 'assistant_delta', content: 'It is midnight.' });
      options.onEvent({
        type: 'assistant_message',
        message: { role: 'assistant', content: 'It is midnight.' },
      });
      options.onEvent({ type: 'done' });
      complete('conv-test');
      await sending;
    });

    expect(probe.current.messages.map(({ role, content }) => ({ role, content }))).toEqual([
      { role: 'user', content: 'what time is it' },
      { role: 'assistant', content: 'It is midnight.' },
    ]);
    expect(probe.current.huntEpisodes[0]?.status).toBe('hunted');
    expect(probe.current.runningTools).toEqual([]);
    expect(probe.current.assistantDraft).toEqual({ status: 'idle' });
  });

  it('shows streamed errors and clears them on the next send', async () => {
    mockedStreamChat.mockImplementation(async (_request, options) => {
      options.onEvent({ type: 'error', content: 'Unable to reach Jeffy.' });
      return 'conv-test';
    });

    await act(async () => {
      await probe.current.send('hello');
    });
    expect(probe.current.error).toBe('Unable to reach Jeffy.');
    expect(probe.current.messages).toEqual([]);

    mockedStreamChat.mockImplementation(async (_request, options) => {
      options.onEvent({
        type: 'assistant_message',
        message: { role: 'assistant', content: 'Hi.' },
      });
      options.onEvent({ type: 'done' });
      return 'conv-test';
    });
    await act(async () => {
      await probe.current.send('again');
    });

    expect(probe.current.error).toBeNull();
    expect(probe.current.messages.map((message) => message.content)).toEqual(['again', 'Hi.']);
  });

  it('shows an error when the stream fails', async () => {
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    mockedStreamChat.mockRejectedValue(new Error('Unable to reach Jeffy.'));

    await act(async () => {
      await probe.current.send('hello');
    });

    expect(probe.current.error).toBe('Unable to reach Jeffy.');
    expect(probe.current.messages).toEqual([]);
    expect(probe.current.sending).toBe(false);
    errorSpy.mockRestore();
  });

  it('clears the den when the conversation is unknown', async () => {
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    mockedStreamChat.mockRejectedValue(new ChatHttpError(404, 'Conversation not found.'));

    await act(async () => {
      await probe.current.send('hello');
    });

    expect(probe.current.messages).toEqual([]);
    expect(probe.current.error).toBe('Conversation not found.');
    errorSpy.mockRestore();
  });

  it('clears the error when the send is aborted', async () => {
    mockedStreamChat.mockImplementation(async (_request, options) => {
      options.onEvent({ type: 'error', content: 'Unable to reach Jeffy.' });
      throw new DOMException('Aborted', 'AbortError');
    });

    await act(async () => {
      await probe.current.send('hello');
    });

    expect(probe.current.error).toBeNull();
    expect(probe.current.messages).toEqual([]);
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

    mockedStreamChat.mockImplementation((_request, options: StreamChatOptions) => {
      const signal = options.signal;
      if (!signal) {
        throw new Error('send should pass an abort signal.');
      }

      signals.push(signal);
      return (signals.length === 1 ? firstGate : secondGate).then(() => 'conv-test');
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
