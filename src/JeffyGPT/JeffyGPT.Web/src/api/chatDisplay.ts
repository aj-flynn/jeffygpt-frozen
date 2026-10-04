import type { AssistantDraft } from './chatStreamState';

export function shouldShowEmptyState(messageCount: number, draft: AssistantDraft): boolean {
  return messageCount === 0 && draft.status === 'idle';
}

/** Transcript rows are user or assistant. Any other role is shown as Jeffy. */
export function toBubbleRole(role: string): 'user' | 'assistant' {
  return role === 'user' ? 'user' : 'assistant';
}

export function displayRoleLabel(role: 'user' | 'assistant'): string {
  return role === 'user' ? 'Upstairs' : 'Jeffy';
}
