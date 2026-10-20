import { describe, expect, it } from 'vitest';
import { lastUserMessageId, type ChatMessageWithId } from './chatHistory';

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
