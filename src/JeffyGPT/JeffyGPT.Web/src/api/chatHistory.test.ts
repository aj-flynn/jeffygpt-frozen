import { describe, expect, it } from 'vitest';
import {
  isToolOnlyAssistantRound,
  isToolRoleMessage,
  isTranscriptVisibleMessage,
  lastUserMessageId,
  toOutboundRequestMessages,
  type ChatMessageWithId,
} from './chatHistory';
import type { ChatMessageDto } from './chatTypes';

describe('isTranscriptVisibleMessage', () => {
  it('hides tool role messages', () => {
    const message: ChatMessageDto = { role: 'tool', content: '{"ok":true}' };

    expect(isToolRoleMessage(message)).toBe(true);
    expect(isTranscriptVisibleMessage(message)).toBe(false);
  });

  it('hides assistant tool-only rounds without visible content', () => {
    const message: ChatMessageDto = {
      role: 'assistant',
      content: '',
      toolCalls: [{ id: 'call_1', name: 'get-current-time', argumentsJson: '{}' }],
    };

    expect(isToolOnlyAssistantRound(message)).toBe(true);
    expect(isTranscriptVisibleMessage(message)).toBe(false);
  });

  it('shows assistant messages with content even when tool calls are present', () => {
    const message: ChatMessageDto = {
      role: 'assistant',
      content: 'Checking the time.',
      toolCalls: [{ id: 'call_1', name: 'get-current-time', argumentsJson: '{}' }],
    };

    expect(isToolOnlyAssistantRound(message)).toBe(false);
    expect(isTranscriptVisibleMessage(message)).toBe(true);
  });

  it('shows user messages', () => {
    const message: ChatMessageDto = { role: 'user', content: 'hello' };

    expect(isTranscriptVisibleMessage(message)).toBe(true);
  });
});

describe('lastUserMessageId', () => {
  it('returns the most recent user message id', () => {
    const messages: ChatMessageWithId[] = [
      { id: 'u1', role: 'user', content: 'first' },
      { id: 'a1', role: 'assistant', content: 'ok' },
      { id: 'u2', role: 'user', content: 'second' },
    ];

    expect(lastUserMessageId(messages)).toBe('u2');
    expect(lastUserMessageId([])).toBeNull();
  });
});

describe('toOutboundRequestMessages', () => {
  it('keeps user/assistant content and strips tool rounds and toolCalls', () => {
    const messages: ChatMessageDto[] = [
      { role: 'user', content: 'hello' },
      {
        role: 'assistant',
        content: '',
        toolCalls: [{ id: 'call_1', name: 'get-current-time', argumentsJson: '{}' }],
      },
      { role: 'tool', content: '{"utc":"2026-01-01T00:00:00Z"}', toolCallId: 'call_1' },
      { role: 'assistant', content: 'It is midnight UTC.', toolCalls: [] },
      { role: 'system', content: 'ignore me' },
    ];

    expect(toOutboundRequestMessages(messages)).toEqual([
      { role: 'user', content: 'hello' },
      { role: 'assistant', content: 'It is midnight UTC.' },
    ]);
  });
});
