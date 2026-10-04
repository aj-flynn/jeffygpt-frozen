import { describe, expect, it } from 'vitest';
import { canSendMessage } from './chatComposer';

describe('chatComposer', () => {
  it('allows send only when input is non-empty and not sending', () => {
    expect(canSendMessage('hello', false)).toBe(true);
    expect(canSendMessage('  ', false)).toBe(false);
    expect(canSendMessage('hello', true)).toBe(false);
  });
});
