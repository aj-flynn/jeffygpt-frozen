import { describe, expect, it } from 'vitest';
import { isAbortError, shouldClearAbortController, toUserFacingError } from './chatErrors';

describe('chatErrors', () => {
  it('detects abort errors', () => {
    expect(isAbortError(new DOMException('Aborted', 'AbortError'))).toBe(true);
    expect(isAbortError(new Error('network'))).toBe(false);
  });

  it('maps unknown errors to fallback text', () => {
    expect(toUserFacingError(new Error('boom'), 'fallback')).toBe('boom');
    expect(toUserFacingError('nope', 'fallback')).toBe('fallback');
    expect(toUserFacingError(new Error('Chat stream failed: 500'), 'fallback')).toBe(
      'The chat service returned an error. Please try again.',
    );
  });

  it('clears abort controller only when it matches the completed request', () => {
    const controller = new AbortController();

    expect(shouldClearAbortController(controller, controller)).toBe(true);
    expect(shouldClearAbortController(new AbortController(), controller)).toBe(false);
    expect(shouldClearAbortController(null, controller)).toBe(false);
  });
});
