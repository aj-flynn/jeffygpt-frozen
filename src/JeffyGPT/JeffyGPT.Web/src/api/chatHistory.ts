/** Display messages only. The server owns model history, including tool results. */
export interface ChatMessageWithId {
  id: string;
  role: 'user' | 'assistant';
  content: string;
}

export function lastUserMessageId(messages: ChatMessageWithId[]): string | null {
  for (let index = messages.length - 1; index >= 0; index -= 1) {
    const message = messages[index];
    if (message?.role === 'user') {
      return message.id;
    }
  }

  return null;
}
