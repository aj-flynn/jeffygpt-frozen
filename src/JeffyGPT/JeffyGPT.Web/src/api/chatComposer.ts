import type { KeyboardEvent } from 'react';

export const MAX_COMPOSER_INPUT_LENGTH = 32_000;

export function canSendMessage(input: string, sending: boolean): boolean {
  const trimmed = input.trim();
  return Boolean(trimmed) && !sending && trimmed.length <= MAX_COMPOSER_INPUT_LENGTH;
}

export function isComposerSubmitShortcut(event: KeyboardEvent<HTMLTextAreaElement>): boolean {
  return event.key === 'Enter' && !event.shiftKey;
}
