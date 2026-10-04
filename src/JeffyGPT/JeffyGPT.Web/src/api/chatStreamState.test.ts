import { describe, expect, it } from 'vitest';
import { isTranscriptVisibleMessage } from './chatHistory';
import {
  committedMessagesFromReducerState,
  createChatStreamReducerState,
  endStreamWithoutCompletion,
  reduceChatStreamEvent,
} from './chatStreamState';
import type { ChatMessageWithId } from './chatHistory';
import type { HuntEpisode } from './chatActivity';

const userMessage: ChatMessageWithId = {
  id: 'msg_1',
  role: 'user',
  content: 'hello',
};

describe('reduceChatStreamEvent', () => {
  it('appends assistant deltas to streaming content', () => {
    let state = createChatStreamReducerState([userMessage]);
    state = { ...state, assistantDraft: { status: 'streaming', text: '' } };

    state = reduceChatStreamEvent(state, { type: 'assistant_delta', content: 'Hi ' });
    state = reduceChatStreamEvent(state, { type: 'assistant_delta', content: 'there' });

    expect(state.assistantDraft).toEqual({ status: 'streaming', text: 'Hi there' });
    expect(state.huntEpisodes).toEqual([]);
  });

  it('starts a Hunt episode and tracks running tools on tool start', () => {
    let state = createChatStreamReducerState([userMessage]);
    state = { ...state, assistantDraft: { status: 'streaming', text: 'partial' } };

    state = reduceChatStreamEvent(state, {
      type: 'tool_call_started',
      toolCall: { id: 'call_1', name: 'get-current-time', argumentsJson: '{}' },
    });

    expect(state.assistantDraft).toEqual({ status: 'idle' });
    expect(state.runningTools).toEqual([{ id: 'call_1', toolName: 'get-current-time' }]);
    expect(state.huntEpisodes).toEqual([
      {
        id: 'hunt-msg_1',
        userMessageId: 'msg_1',
        status: 'hunting',
        calledTools: [{ id: 'call_1', toolName: 'get-current-time' }],
      },
    ]);
  });

  it('removes a finished tool from the hunt box and leaves the Hunt label', () => {
    let state = createChatStreamReducerState([userMessage]);
    state = reduceChatStreamEvent(state, {
      type: 'tool_call_started',
      toolCall: { id: 'call_1', name: 'get-current-time', argumentsJson: '{}' },
    });
    state = reduceChatStreamEvent(state, {
      type: 'tool_call_started',
      toolCall: { id: 'call_2', name: 'read-file', argumentsJson: '{}' },
    });
    state = reduceChatStreamEvent(state, {
      type: 'tool_call_finished',
      toolResult: { toolCallId: 'call_1', contentJson: '{}', isError: false },
    });

    expect(state.runningTools).toEqual([{ id: 'call_2', toolName: 'read-file' }]);
    expect(state.huntEpisodes[0]?.status).toBe('hunting');
    expect(state.huntEpisodes[0]?.calledTools).toEqual([
      { id: 'call_1', toolName: 'get-current-time' },
      { id: 'call_2', toolName: 'read-file' },
    ]);
  });

  it('commits the assistant message and leaves a Hunted marker after tools finish', () => {
    let state = createChatStreamReducerState([userMessage]);

    state = reduceChatStreamEvent(state, {
      type: 'tool_call_started',
      toolCall: { id: 'call_1', name: 'get-current-time', argumentsJson: '{}' },
    });
    state = reduceChatStreamEvent(state, {
      type: 'assistant_tool_round',
      message: {
        role: 'assistant',
        content: '',
        toolCalls: [{ id: 'call_1', name: 'get-current-time', argumentsJson: '{}' }],
      },
    });
    state = reduceChatStreamEvent(state, {
      type: 'tool_call_finished',
      toolResult: {
        toolCallId: 'call_1',
        contentJson: '{"utc":"2026-01-01T00:00:00Z"}',
        isError: false,
      },
    });
    state = reduceChatStreamEvent(state, {
      type: 'assistant_message',
      message: { role: 'assistant', content: 'It is midnight UTC.' },
    });
    state = reduceChatStreamEvent(state, { type: 'done' });

    expect(state.assistantDraft).toEqual({ status: 'idle' });
    expect(state.runningTools).toEqual([]);
    expect(state.huntEpisodes).toEqual([
      {
        id: 'hunt-msg_1',
        userMessageId: 'msg_1',
        status: 'hunted',
        calledTools: [{ id: 'call_1', toolName: 'get-current-time' }],
      },
    ]);
    const visibleContent = committedMessagesFromReducerState(state)
      .filter(isTranscriptVisibleMessage)
      .map((message) => message.content);
    expect(visibleContent).toEqual(['hello', 'It is midnight UTC.']);
  });

  it('does not create a hunt episode when the assistant replies without tools', () => {
    let state = createChatStreamReducerState([userMessage]);
    state = reduceChatStreamEvent(state, {
      type: 'assistant_message',
      message: { role: 'assistant', content: 'Hi.' },
    });
    state = reduceChatStreamEvent(state, { type: 'done' });

    expect(state.huntEpisodes).toEqual([]);
  });

  it('keeps prior Hunted rows when a later send starts a new reducer state', () => {
    const prior: HuntEpisode[] = [
      {
        id: 'hunt-msg_1',
        userMessageId: 'msg_1',
        status: 'hunting',
        calledTools: [{ id: 'call_1', toolName: 'get-current-time' }],
      },
    ];
    const nextUser: ChatMessageWithId = { id: 'msg_2', role: 'user', content: 'again' };

    const state = createChatStreamReducerState([userMessage, nextUser], prior);

    expect(state.huntEpisodes).toEqual([
      {
        id: 'hunt-msg_1',
        userMessageId: 'msg_1',
        status: 'hunted',
        calledTools: [{ id: 'call_1', toolName: 'get-current-time' }],
      },
    ]);
    expect(state.huntUserMessageId).toBe('msg_2');
  });

  it('marks an in-flight Hunt as Hunted on stream error', () => {
    let state = createChatStreamReducerState([userMessage]);
    state = reduceChatStreamEvent(state, {
      type: 'tool_call_started',
      toolCall: { id: 'call_1', name: 'get-current-time', argumentsJson: '{}' },
    });
    state = reduceChatStreamEvent(state, { type: 'error', content: 'boom' });

    expect(state.streamFailed).toBe(true);
    expect(state.errorContent).toBe('boom');
    expect(state.assistantDraft).toEqual({ status: 'idle' });
    expect(state.runningTools).toEqual([]);
    expect(state.huntEpisodes[0]?.status).toBe('hunted');
    expect(state.huntEpisodes[0]?.calledTools).toEqual([
      { id: 'call_1', toolName: 'get-current-time' },
    ]);
  });

  it('latches streamFailed and ignores later events after error', () => {
    let state = createChatStreamReducerState([userMessage]);
    state = { ...state, assistantDraft: { status: 'streaming', text: 'partial' } };

    state = reduceChatStreamEvent(state, { type: 'error', content: 'boom' });
    const afterError = reduceChatStreamEvent(state, {
      type: 'assistant_delta',
      content: 'ignored',
    });

    expect(afterError).toEqual(state);
    expect(state.streamFailed).toBe(true);
    expect(state.errorContent).toBe('boom');
    expect(state.assistantDraft).toEqual({ status: 'idle' });
  });

  it('clears the streaming draft when a tool round starts', () => {
    let state = createChatStreamReducerState([userMessage]);
    state = { ...state, assistantDraft: { status: 'streaming', text: 'partial' } };

    state = reduceChatStreamEvent(state, {
      type: 'assistant_tool_round',
      message: {
        role: 'assistant',
        content: '',
        toolCalls: [{ id: 'call_1', name: 'get-current-time', argumentsJson: '{}' }],
      },
    });

    expect(state.assistantDraft).toEqual({ status: 'idle' });
  });

  it('ends an in-flight hunt when the stream stops without a completion', () => {
    let state = createChatStreamReducerState([userMessage]);
    state = reduceChatStreamEvent(state, {
      type: 'tool_call_started',
      toolCall: { id: 'call_1', name: 'get-current-time', argumentsJson: '{}' },
    });

    state = endStreamWithoutCompletion(state);

    expect(state.assistantDraft).toEqual({ status: 'idle' });
    expect(state.runningTools).toEqual([]);
    expect(state.huntEpisodes[0]?.status).toBe('hunted');
  });
});
