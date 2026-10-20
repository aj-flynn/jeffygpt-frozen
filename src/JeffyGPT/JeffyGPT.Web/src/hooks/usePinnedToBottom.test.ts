import { describe, expect, it } from 'vitest';
import { isFollowingContentEnd, scrollTopForContentEnd } from './usePinnedToBottom';

describe('isFollowingContentEnd', () => {
  it('follows when the reply ends at the bottom of the viewport', () => {
    expect(isFollowingContentEnd(400, 800, 810)).toBe(true);
  });

  it('does not follow a tool list that extends past the reply', () => {
    expect(isFollowingContentEnd(900, 800, 700)).toBe(false);
  });

  it('follows when the reply is shorter than the viewport and the scroll is at the top', () => {
    expect(isFollowingContentEnd(0, 800, 420)).toBe(true);
  });

  it('does not follow after the reader scrolls the reply away from the bottom', () => {
    expect(isFollowingContentEnd(200, 800, 420)).toBe(false);
  });
});

describe('scrollTopForContentEnd', () => {
  it('scrolls down until the reply sits at the bottom of the viewport', () => {
    expect(scrollTopForContentEnd(100, 500, 860)).toBe(460);
  });

  it('scrolls back up when a long tool list had pushed the view past the reply', () => {
    expect(scrollTopForContentEnd(900, 800, 640)).toBe(740);
  });

  it('does not scroll above the start of the transcript', () => {
    expect(scrollTopForContentEnd(20, 800, 200)).toBe(0);
  });
});
