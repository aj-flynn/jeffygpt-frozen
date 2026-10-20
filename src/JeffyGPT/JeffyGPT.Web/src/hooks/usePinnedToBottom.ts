import { useCallback, useRef } from 'react';

const SCROLL_BOTTOM_THRESHOLD_PX = 24;

export function isScrolledToBottom(element: HTMLElement): boolean {
  return (
    element.scrollHeight - element.scrollTop - element.clientHeight <= SCROLL_BOTTOM_THRESHOLD_PX
  );
}

/**
 * The viewport is following the reply when that content ends at the bottom edge,
 * or when there is not enough content to scroll.
 * Extra height below the reply, such as a tool list, does not count.
 */
export function isFollowingContentEnd(
  scrollTop: number,
  viewportBottom: number,
  contentEnd: number,
): boolean {
  const pastEnd = contentEnd - viewportBottom;
  if (Math.abs(pastEnd) <= SCROLL_BOTTOM_THRESHOLD_PX) {
    return true;
  }

  return scrollTop <= SCROLL_BOTTOM_THRESHOLD_PX && pastEnd < 0;
}

/** Scroll offset that places the reply at the bottom of the viewport. */
export function scrollTopForContentEnd(
  scrollTop: number,
  viewportBottom: number,
  contentEnd: number,
): number {
  return Math.max(0, scrollTop + contentEnd - viewportBottom);
}

function contentViewportBottom(element: HTMLElement): number {
  const paddingBottom = Number.parseFloat(getComputedStyle(element).paddingBottom) || 0;
  return element.getBoundingClientRect().bottom - paddingBottom;
}

/**
 * Tracks whether the user is pinned to the bottom of a scrollable element.
 * `endRef` marks the in-flow reply. Scrolling follows that edge, not a tool list
 * hanging below it. Attach `elementRef` and `handleScroll` to the scroller, and
 * call `scrollToBottomIfPinned` after content changes.
 */
export function usePinnedToBottom() {
  const elementRef = useRef<HTMLDivElement>(null);
  const endRef = useRef<HTMLDivElement>(null);
  const isPinnedRef = useRef(true);

  const handleScroll = useCallback(() => {
    const element = elementRef.current;
    if (!element) {
      return;
    }

    const end = endRef.current;
    isPinnedRef.current = end
      ? isFollowingContentEnd(
          element.scrollTop,
          contentViewportBottom(element),
          end.getBoundingClientRect().bottom,
        )
      : isScrolledToBottom(element);
  }, []);

  const scrollToBottomIfPinned = useCallback(() => {
    const element = elementRef.current;
    if (!element || !isPinnedRef.current) {
      return;
    }

    const end = endRef.current;
    if (!end) {
      element.scrollTop = element.scrollHeight;
      return;
    }

    element.scrollTop = scrollTopForContentEnd(
      element.scrollTop,
      contentViewportBottom(element),
      end.getBoundingClientRect().bottom,
    );
  }, []);

  return { elementRef, endRef, handleScroll, scrollToBottomIfPinned };
}
