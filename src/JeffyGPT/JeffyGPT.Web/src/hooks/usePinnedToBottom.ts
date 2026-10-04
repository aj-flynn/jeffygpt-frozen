import { useCallback, useRef } from 'react';

const SCROLL_BOTTOM_THRESHOLD_PX = 24;

export function isScrolledToBottom(element: HTMLElement): boolean {
  return (
    element.scrollHeight - element.scrollTop - element.clientHeight <= SCROLL_BOTTOM_THRESHOLD_PX
  );
}

/**
 * Tracks whether the user is pinned to the bottom of a scrollable element.
 * Attach `elementRef` and `handleScroll` to the element, and call
 * `scrollToBottomIfPinned` after content changes to keep it pinned.
 */
export function usePinnedToBottom() {
  const elementRef = useRef<HTMLDivElement>(null);
  const isPinnedRef = useRef(true);

  const handleScroll = useCallback(() => {
    const element = elementRef.current;
    if (!element) {
      return;
    }

    isPinnedRef.current = isScrolledToBottom(element);
  }, []);

  const scrollToBottomIfPinned = useCallback(() => {
    const element = elementRef.current;
    if (!element || !isPinnedRef.current) {
      return;
    }

    element.scrollTop = element.scrollHeight;
  }, []);

  return { elementRef, handleScroll, scrollToBottomIfPinned };
}
