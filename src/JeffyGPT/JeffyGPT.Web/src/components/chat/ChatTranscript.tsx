import { Fragment, useEffect, useId, useRef, type CSSProperties } from 'react';
import {
  huntEpisodeForUserMessage,
  huntEpisodeLabel,
  huntedAriaLabel,
  shouldShowHuntBody,
  shouldShowHuntedToolList,
  type HuntEpisode,
  type RunningTool,
} from '../../api/chatActivity';
import { displayRoleLabel, toBubbleRole } from '../../api/chatDisplay';
import type { ChatMessageWithId } from '../../api/chatHistory';
import type { AssistantDraft } from '../../api/chatStreamState';
import { usePinnedToBottom } from '../../hooks/usePinnedToBottom';
import { MarkdownContent } from './MarkdownContent';
import './ChatTranscript.css';

export interface ChatTranscriptProps {
  messages: ChatMessageWithId[];
  assistantDraft: AssistantDraft;
  huntEpisodes: HuntEpisode[];
  runningTools: RunningTool[];
}

function HuntingMarker({ runningTools }: { runningTools: RunningTool[] }) {
  const showLiveBody = shouldShowHuntBody('hunting', runningTools.length);

  return (
    <article className="chat-panel__message chat-panel__message--hunt">
      <span className="chat-panel__role">{huntEpisodeLabel('hunting')}</span>
      {showLiveBody && (
        <ul className="chat-panel__hunt-body" aria-live="polite">
          {runningTools.map((tool) => (
            <li key={tool.id}>{tool.toolName}</li>
          ))}
        </ul>
      )}
    </article>
  );
}

function huntAnchorName(reactId: string): string {
  return `--hunt${reactId.replaceAll(':', '')}`;
}

function HuntedMarker({ calledTools }: { calledTools: RunningTool[] }) {
  const showToolList = shouldShowHuntedToolList('hunted', calledTools.length);
  const label = huntEpisodeLabel('hunted');
  const anchorName = huntAnchorName(useId());
  const labelRef = useRef<HTMLSpanElement>(null);

  useEffect(() => {
    labelRef.current?.style.setProperty('anchor-name', anchorName);
  }, [anchorName]);

  return (
    <article className="chat-panel__message chat-panel__message--hunt">
      {showToolList ? (
        <span
          ref={labelRef}
          className="chat-panel__hunt-label"
          style={{ '--hunt-anchor': anchorName } as CSSProperties}
        >
          <span
            className="chat-panel__role chat-panel__hunt-trigger"
            tabIndex={0}
            aria-label={huntedAriaLabel(calledTools)}
          >
            {label}
          </span>
          <ul className="chat-panel__hunt-tooltip" role="tooltip">
            {calledTools.map((tool) => (
              <li key={tool.id}>{tool.toolName}</li>
            ))}
          </ul>
        </span>
      ) : (
        <span className="chat-panel__role">{label}</span>
      )}
    </article>
  );
}

export function ChatTranscript({
  messages,
  assistantDraft,
  huntEpisodes,
  runningTools,
}: ChatTranscriptProps) {
  const {
    elementRef: messagesRef,
    endRef,
    handleScroll: handleMessagesScroll,
    scrollToBottomIfPinned,
  } = usePinnedToBottom();

  useEffect(() => {
    scrollToBottomIfPinned();
  }, [messages, assistantDraft, huntEpisodes, runningTools, scrollToBottomIfPinned]);

  const streamingText = assistantDraft.status === 'streaming' ? assistantDraft.text : null;

  return (
    <div
      ref={messagesRef}
      className="chat-panel__messages"
      aria-live="polite"
      role="log"
      onScroll={handleMessagesScroll}
    >
      <div ref={endRef} className="chat-panel__thread">
        {messages.map((message) => {
          const displayRole = toBubbleRole(message.role);
          const hunt = huntEpisodeForUserMessage(huntEpisodes, message.id);

          return (
            <Fragment key={message.id}>
              <article className={`chat-panel__message chat-panel__message--${displayRole}`}>
                <span className="chat-panel__role">{displayRoleLabel(displayRole)}</span>
                <MarkdownContent className="chat-panel__content" content={message.content} />
              </article>
              {hunt?.status === 'hunting' ? (
                <HuntingMarker runningTools={runningTools} />
              ) : hunt ? (
                <HuntedMarker calledTools={hunt.calledTools} />
              ) : null}
            </Fragment>
          );
        })}
        {streamingText !== null && (
          <article className="chat-panel__message chat-panel__message--assistant chat-panel__message--streaming">
            <span className="chat-panel__role">{displayRoleLabel('assistant')}</span>
            <MarkdownContent
              className="chat-panel__content"
              content={streamingText}
              trailing={<span className="chat-panel__cursor" aria-hidden="true" />}
            />
          </article>
        )}
      </div>
    </div>
  );
}
