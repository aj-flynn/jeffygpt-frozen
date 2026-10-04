export const MAX_USER_FACING_ERROR_LENGTH = 280;

export function isAbortError(err: unknown): boolean {
  return err instanceof Error && err.name === 'AbortError';
}

export function toUserFacingError(err: unknown, fallback: string): string {
  if (!(err instanceof Error) || !err.message.trim()) {
    return fallback;
  }

  const message = err.message.trim();

  if (/^Chat (request|stream) failed:\s*\d+/i.test(message)) {
    return 'The chat service returned an error. Please try again.';
  }

  if (/invalid JSON|unexpected|maximum allowed length|Unsupported chat stream/i.test(message)) {
    return 'The chat stream was interrupted. Please try again.';
  }

  if (message.length > MAX_USER_FACING_ERROR_LENGTH) {
    return fallback;
  }

  return message;
}

export function shouldClearAbortController(
  currentController: AbortController | null,
  completedController: AbortController,
): boolean {
  return currentController === completedController;
}
