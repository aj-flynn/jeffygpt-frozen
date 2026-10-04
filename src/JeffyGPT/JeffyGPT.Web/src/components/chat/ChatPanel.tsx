import { shouldShowEmptyState } from '../../api/chatDisplay';
import { useChatStream } from '../../hooks/useChatStream';
import { DenChrome } from '../layout/DenChrome';
import { ChatComposer } from './ChatComposer';
import { ChatTranscript } from './ChatTranscript';
import { EmptyChatPanel } from './EmptyChatPanel';
import './ChatPanel.css';

export function ChatPanel() {
  const { messages, assistantDraft, runningTools, huntEpisodes, error, sending, send, cancel } =
    useChatStream();

  const showEmptyState = shouldShowEmptyState(messages.length, assistantDraft);

  return (
    <div className="chat-layout">
      <DenChrome sending={sending} />

      <section className="chat-panel">
        <div className="chat-panel__transcript">
          {showEmptyState ? (
            <EmptyChatPanel />
          ) : (
            <ChatTranscript
              messages={messages}
              assistantDraft={assistantDraft}
              huntEpisodes={huntEpisodes}
              runningTools={runningTools}
            />
          )}
        </div>

        {error && <p className="chat-panel__error">{error}</p>}
      </section>

      <ChatComposer
        sending={sending}
        onSend={(text) => {
          void send(text);
        }}
        onCancel={cancel}
      />
    </div>
  );
}
