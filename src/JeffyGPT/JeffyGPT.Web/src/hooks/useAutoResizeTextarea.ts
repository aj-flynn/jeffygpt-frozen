import { useEffect, useRef } from 'react';

/**
 * Grows a textarea with its content between `minLines` and `maxLines`,
 * collapsing back to `minLines` when the value is blank.
 */
export function useAutoResizeTextarea(value: string, maxLines: number, minLines = 1) {
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    const textarea = textareaRef.current;
    if (!textarea) {
      return;
    }

    const style = getComputedStyle(textarea);
    const lineHeight = Number.parseFloat(style.lineHeight) || 24;
    const verticalPadding =
      Number.parseFloat(style.paddingTop) + Number.parseFloat(style.paddingBottom);
    const verticalBorder =
      Number.parseFloat(style.borderTopWidth) + Number.parseFloat(style.borderBottomWidth);
    const minHeight = lineHeight * minLines + verticalPadding + verticalBorder;
    const maxHeight = lineHeight * maxLines + verticalPadding + verticalBorder;

    if (!value.trim()) {
      textarea.style.height = `${minHeight}px`;
      return;
    }

    textarea.style.height = 'auto';
    textarea.style.height = `${Math.min(Math.max(textarea.scrollHeight, minHeight), maxHeight)}px`;
  }, [value, maxLines, minLines]);

  return textareaRef;
}
