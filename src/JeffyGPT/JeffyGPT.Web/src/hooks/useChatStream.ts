import { useEffect, useRef, useState } from 'react';
import { isChatHttpError, streamChat } from '../api/chatClient';
import { canSendMessage } from '../api/chatComposer';
import { isAbortError, shouldClearAbortController, toUserFacingError } from '../api/chatErrors';
import type { HuntEpisode, RunningTool } from '../api/chatActivity';
import {
  createChatStreamReducerState,
  dropUncommittedUserMessage,
  reduceChatStreamEvent,
  startAssistantDraft,
  type AssistantDraft,
} from '../api/chatStreamState';
import type { ChatMessageWithId } from '../api/chatHistory';

export interface UseChatStreamResult {
  messages: ChatMessageWithId[];
  assistantDraft: AssistantDraft;
  runningTools: RunningTool[];
  huntEpisodes: HuntEpisode[];
  error: string | null;
  sending: boolean;
  send: (text: string) => Promise<void>;
  cancel: () => void;
}

/**
 * Paints the stream and remembers the conversation id.
 * The server keeps the transcript the model sees.
 */
export function useChatStream(): UseChatStreamResult {
  const [state, setState] = useState(() => createChatStreamReducerState([]));
  const [requestError, setRequestError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  const abortControllerRef = useRef<AbortController | null>(null);
  const conversationIdRef = useRef<string | null>(null);

  useEffect(() => {
    return () => {
      abortControllerRef.current?.abort();
    };
  }, []);

  function cancel() {
    abortControllerRef.current?.abort();
  }

  function dropUserMessage(userMessageId: string) {
    setState((current) => dropUncommittedUserMessage(current, userMessageId));
  }

  async function send(text: string): Promise<void> {
    const trimmed = text.trim();
    if (!canSendMessage(trimmed, sending)) {
      return;
    }

    abortControllerRef.current?.abort();

    const userMessage: ChatMessageWithId = {
      id: crypto.randomUUID(),
      role: 'user',
      content: trimmed,
    };
    const abortController = new AbortController();
    abortControllerRef.current = abortController;
    setState((current) =>
      startAssistantDraft(
        createChatStreamReducerState([...current.messages, userMessage], current.huntEpisodes),
      ),
    );

    setRequestError(null);
    setSending(true);

    let committed = false;

    try {
      const conversationId = await streamChat(
        {
          content: trimmed,
          ...(conversationIdRef.current ? { conversationId: conversationIdRef.current } : {}),
        },
        {
          signal: abortController.signal,
          onConversationId: (id) => {
            conversationIdRef.current = id;
          },
          onEvent: (event) => {
            setState((current) => reduceChatStreamEvent(current, event));
            if (event.type === 'assistant_message') {
              committed = true;
            }
          },
        },
      );
      conversationIdRef.current = conversationId;

      if (!committed) {
        dropUserMessage(userMessage.id);
      }
    } catch (err) {
      if (!committed) {
        dropUserMessage(userMessage.id);
      }

      if (isChatHttpError(err) && err.status === 404) {
        conversationIdRef.current = null;
        setState(createChatStreamReducerState([]));
      }

      if (isAbortError(err)) {
        setRequestError(null);
        setState((current) => ({ ...current, errorContent: null }));
        return;
      }

      console.error(err);
      setRequestError(toUserFacingError(err, 'Failed to send message'));
    } finally {
      if (shouldClearAbortController(abortControllerRef.current, abortController)) {
        abortControllerRef.current = null;
      }

      setSending(false);
    }
  }

  const error =
    requestError ??
    (state.errorContent === null
      ? null
      : toUserFacingError(
          new Error(state.errorContent),
          'The chat stream failed. Please try again.',
        ));

  return {
    messages: state.messages,
    assistantDraft: state.assistantDraft,
    runningTools: state.runningTools,
    huntEpisodes: state.huntEpisodes,
    error,
    sending,
    send,
    cancel,
  };
}
