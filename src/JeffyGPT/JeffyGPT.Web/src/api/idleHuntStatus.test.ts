import { describe, expect, it } from 'vitest';
import { IDLE_HUNT_STATUSES, pickIdleHuntStatus } from './idleHuntStatus';

describe('pickIdleHuntStatus', () => {
  it('picks from the idle status list using the injected random source', () => {
    expect(pickIdleHuntStatus(() => 0)).toBe(IDLE_HUNT_STATUSES[0]);
    expect(pickIdleHuntStatus(() => 0.999)).toBe(IDLE_HUNT_STATUSES[IDLE_HUNT_STATUSES.length - 1]);
  });

  it('clamps a mocked random of 1 to the last status', () => {
    expect(pickIdleHuntStatus(() => 1)).toBe(IDLE_HUNT_STATUSES[IDLE_HUNT_STATUSES.length - 1]);
  });
});
