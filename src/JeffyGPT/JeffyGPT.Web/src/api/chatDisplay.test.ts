import { describe, expect, it } from 'vitest';
import { displayRoleLabel, shouldShowEmptyState } from './chatDisplay';

describe('chatDisplay', () => {
  it('shows empty state only when there are no messages and the draft is idle', () => {
    expect(shouldShowEmptyState(0, { status: 'idle' })).toBe(true);
    expect(shouldShowEmptyState(0, { status: 'streaming', text: '' })).toBe(false);
    expect(shouldShowEmptyState(1, { status: 'idle' })).toBe(false);
  });

  it('maps transcript roles to den labels', () => {
    expect(displayRoleLabel('user')).toBe('Upstairs');
    expect(displayRoleLabel('assistant')).toBe('Jeffy');
  });
});
