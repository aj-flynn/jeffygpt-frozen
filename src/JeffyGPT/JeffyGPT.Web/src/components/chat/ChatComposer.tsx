import { useCallback, useEffect, useRef, useState } from 'react';
import {
  canSendMessage,
  isComposerSubmitShortcut,
  MAX_COMPOSER_INPUT_LENGTH,
} from '../../api/chatComposer';
import { useAutoResizeTextarea } from '../../hooks/useAutoResizeTextarea';
import './ChatComposer.css';

const MAX_INPUT_LINES = 8;

export interface ChatComposerProps {
  sending: boolean;
  onSend: (text: string) => void;
  onCancel: () => void;
}

export function ChatComposer({ sending, onSend, onCancel }: ChatComposerProps) {
  const [input, setInput] = useState('');
  const textareaRef = useAutoResizeTextarea(input, MAX_INPUT_LINES, 2);
  const wasSendingRef = useRef(false);

  const focusInput = useCallback(() => {
    textareaRef.current?.focus();
  }, [textareaRef]);

  useEffect(() => {
    focusInput();
  }, [focusInput]);

  useEffect(() => {
    if (wasSendingRef.current && !sending) {
      focusInput();
    }
    wasSendingRef.current = sending;
  }, [sending, focusInput]);

  function handleSend(event: React.FormEvent) {
    event.preventDefault();

    if (!canSendMessage(input, sending)) {
      return;
    }

    const trimmed = input.trim();
    setInput('');
    onSend(trimmed);
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (isComposerSubmitShortcut(event)) {
      event.preventDefault();
      event.currentTarget.form?.requestSubmit();
    }
  }

  return (
    <footer className="chat-panel__composer-bar">
      <label className="chat-panel__section-label" htmlFor="chat-input">
        Your message
      </label>
      <form className="chat-panel__composer" onSubmit={handleSend}>
        <textarea
          id="chat-input"
          ref={textareaRef}
          className="chat-panel__input"
          value={input}
          placeholder="Scratch at the dirt…"
          rows={2}
          maxLength={MAX_COMPOSER_INPUT_LENGTH}
          onChange={(event) => setInput(event.target.value)}
          onKeyDown={handleKeyDown}
          disabled={sending}
        />
        {sending ? (
          <button
            className="chat-panel__send chat-panel__send--cancel"
            type="button"
            onClick={onCancel}
          >
            Cancel
          </button>
        ) : (
          <button
            className="chat-panel__send"
            type="submit"
            disabled={!canSendMessage(input, sending)}
          >
            Hunt
          </button>
        )}
      </form>
    </footer>
  );
}
