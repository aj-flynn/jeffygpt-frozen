import { useEffect, useRef, useState } from 'react';
import { streamChat } from '../api/chatClient';
import { canSendMessage } from '../api/chatComposer';
import { isAbortError, shouldClearAbortController, toUserFacingError } from '../api/chatErrors';
import type { HuntEpisode, RunningTool } from '../api/chatActivity';
import {
  committedMessagesFromReducerState,
  createChatStreamReducerState,
  endStreamWithoutCompletion,
  reduceChatStreamEvent,
  startAssistantDraft,
  type AssistantDraft,
} from '../api/chatStreamState';
import { toOutboundChatRequest, type ChatMessageWithId } from '../api/chatHistory';
import type { ChatStreamEvent } from '../api/chatTypes';

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
 * Owns the chat transcript and the streaming state machine: sending a message,
 * applying stream events, tool activity, errors, and cancellation.
 */
export function useChatStream(): UseChatStreamResult {
  const [messages, setMessages] = useState<ChatMessageWithId[]>([]);
  const [assistantDraft, setAssistantDraft] = useState<AssistantDraft>({ status: 'idle' });
  const [runningTools, setRunningTools] = useState<RunningTool[]>([]);
  const [huntEpisodes, setHuntEpisodes] = useState<HuntEpisode[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  const abortControllerRef = useRef<AbortController | null>(null);
  const reducerRef = useRef(createChatStreamReducerState([]));

  useEffect(() => {
    return () => {
      abortControllerRef.current?.abort();
    };
  }, []);

  function applyReducerState() {
    const state = reducerRef.current;
    setMessages(committedMessagesFromReducerState(state));
    setAssistantDraft(state.assistantDraft);
    setRunningTools(state.runningTools);
    setHuntEpisodes(state.huntEpisodes);
    if (state.errorContent !== null) {
      setError(
        toUserFacingError(
          new Error(state.errorContent),
          'The chat stream failed. Please try again.',
        ),
      );
    }
  }

  function handleStreamEvent(streamEvent: ChatStreamEvent) {
    reducerRef.current = reduceChatStreamEvent(reducerRef.current, streamEvent);
    applyReducerState();
  }

  function cancel() {
    abortControllerRef.current?.abort();
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
    const requestMessages = [...messages, userMessage];
    const abortController = new AbortController();
    abortControllerRef.current = abortController;
    reducerRef.current = startAssistantDraft(
      createChatStreamReducerState(requestMessages, reducerRef.current.huntEpisodes),
    );

    setError(null);
    applyReducerState();
    setSending(true);

    try {
      await streamChat(toOutboundChatRequest(requestMessages), {
        signal: abortController.signal,
        onEvent: handleStreamEvent,
      });
    } catch (err) {
      reducerRef.current = endStreamWithoutCompletion(reducerRef.current);
      applyReducerState();

      if (isAbortError(err)) {
        setError(null);
        return;
      }

      console.error(err);
      setError(toUserFacingError(err, 'Failed to send message'));
    } finally {
      if (shouldClearAbortController(abortControllerRef.current, abortController)) {
        abortControllerRef.current = null;
      }

      setSending(false);
    }
  }

  return {
    messages,
    assistantDraft,
    runningTools,
    huntEpisodes,
    error,
    sending,
    send,
    cancel,
  };
}
