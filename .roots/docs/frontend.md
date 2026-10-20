# Frontend

The frontend is a React single-page application built with Vite and TypeScript.

**Path:** `src/JeffyGPT/JeffyGPT.Web/`

## Naming glossary

Use the same casing rules as [Architecture](./architecture.md#naming-glossary). In this tree that means:

| Kind | Form | Example |
|------|------|---------|
| UI copy | `JeffyGPT` | `index.html` title, wordmark |
| React components | React PascalCase | `JeffyGptLogo` (do not rename to `JeffyGPTLogo`) |
| CSS / storage keys | short prefix | `--rust`, `jeffygpt-theme` |

C# identifiers stay `JeffyMcp` / `OpenAi` (acronyms as words). Do not add a fourth spelling.

Theme tokens live in `src/index.css`. Use `--text-primary`, `--text-heading`, and `--surface` only — do not reintroduce Vite leftover aliases (`--text`, `--text-h`, `--bg`) or global `h1`/`h2`/`code` scaffold type.

## Entry point

### main.tsx

Mounts the app with:

```
StrictMode → ThemeProvider → App
```

Imports global styles from `index.css`.

### App.tsx

Named export (`export function App`) so the entry tree matches other modules. Renders:

```
AppShell
  └── ChatPanel
```

## Component tree

```
AppShell (soil page + centered den)
└── ChatPanel (den: chrome + feed + composer; state from useChatStream)
    ├── DenChrome (wordmark, 2 tonight, idle status, ThemeToggle)
    ├── EmptyChatPanel (quiet empty feed when no messages)
    ├── ChatTranscript (when messages exist; list, Hunt/Hunted rows, streaming cursor)
    ├── Error message (on API failure)
    └── ChatComposer (in-den Hunt / Cancel)
        ├── Visually hidden label: "Your message"
        ├── Textarea (auto-grow, 1–8 lines)
        └── Hunt button
```

## Components

### Layout

| File | Purpose |
|------|---------|
| `components/layout/AppShell.tsx` | Full-height soil page with a centered den frame for chat |
| `components/layout/AppShell.css` | Page background, grain overlay, den column |
| `components/layout/DenChrome.tsx` | Den header: wordmark, tally chip, idle hunt status, theme toggle |
| `components/layout/DenChrome.css` | Chrome bar, tally, pulsing status |

### Chat

| File | Purpose |
|------|---------|
| `components/chat/ChatPanel.tsx` | Layout assembler: chrome, empty vs transcript, error; state from `useChatStream` |
| `components/chat/ChatPanel.css` | Den grid and error styles |
| `components/chat/ChatTranscript.tsx` | Message list, Hunt/Hunted rows, live streaming bubble |
| `components/chat/ChatTranscript.css` | Den bubbles, hunt box, streaming cursor |
| `components/chat/ChatComposer.tsx` | Composer form, Hunt/cancel, input focus |
| `components/chat/ChatComposer.css` | In-den composer, textarea, Hunt button |
| `components/chat/MarkdownContent.tsx` | Renders message text as GitHub-flavored markdown (user and assistant) |
| `components/chat/MarkdownContent.css` | Markdown typography, code blocks, links, tables (theme tokens) |
| `components/chat/EmptyChatPanel.tsx` | Quiet empty feed when conversation is empty |
| `components/chat/EmptyChatPanel.css` | Empty feed layout |

### Branding

| File | Purpose |
|------|---------|
| `components/brand/JeffyGptLogo.tsx` | Reusable logo — imports official SVG, optional wordmark and icon |
| `components/brand/JeffyGptLogo.css` | Plex Sans wordmark; GPT uses nest/rust |

**Props:** `size`, `showWordmark`, `showIcon` (default `true`), `className`

### Theme

| File | Purpose |
|------|---------|
| `components/theme/ThemeToggle.tsx` | Moon/sun button in `DenChrome` to toggle parchment/soil |
| `components/theme/ThemeToggle.css` | Toggle button styles |
| `theme/ThemeProvider.tsx` | React context: `theme`, `setTheme`, `toggleTheme` |
| `theme/theme.ts` | localStorage key, resolve/apply/persist helpers |

## Hooks

Reusable behavior lives in `src/hooks/`:

| File | Purpose |
|------|---------|
| `hooks/useChatStream.ts` | Holds one stream state object, applies events with functional state updates, and tracks request errors, sending status, cancellation, and the conversation id |
| `hooks/useAutoResizeTextarea.ts` | Grows a textarea with its content between a min line count and a max line count |
| `hooks/usePinnedToBottom.ts` | Tracks whether a scrollable element is pinned to its bottom edge; `scrollToBottomIfPinned` re-pins after content changes |

## API client layer

API calls live in `src/api/` — not inside components.

| File | Purpose |
|------|---------|
| `api/chatTypes.ts` | TypeScript interfaces matching backend DTOs and stream events |
| `api/chatHistory.ts` | Display message type (`id`, `role`, `content`) and last-user-message lookup |
| `api/chatStreamState.ts` | Pure stream reducer (`reduceChatStreamEvent`) — visible messages, assistant draft, and tool activity; tested in `chatStreamState.test.ts` |
| `api/chatActivity.ts` | Hunt episode helpers. `runningTools` are in flight; `calledTools` are the finished list |
| `api/chatDisplay.ts` | Empty-state check and Upstairs/Jeffy role labels |
| `api/idleHuntStatus.ts` | Idle chrome status list and `pickIdleHuntStatus` |
| `api/chatComposer.ts` | Send eligibility and the Enter-to-send shortcut |
| `api/chatErrors.ts` | Abort detection, user-facing error text, and abort-controller identity |
| `api/chatClient.ts` | `postChat(request)` → `POST /api/chat`; `streamChat(request, options)` → `POST /api/chat/stream` (used by `useChatStream`) |

### chatClient error handling

On non-OK responses, reads `{ error?: string }` from the body and throws `Error` with that message (or a status-based fallback). Applies to both `postChat` and `streamChat` before the response body is read.

`streamChat` parses NDJSON lines via `chatStreamParse.ts` (typed validation, line-length cap), invokes `onEvent` for each valid `ChatStreamEvent`, ignores events after a terminal `done`/`error`, and throws if the connection ends without a terminal event. Mid-stream `error` events are surfaced through the callback; transport/parse failures throw.

## ChatPanel behavior

### Sending messages

1. User submits via Hunt button or Enter (Shift+Enter inserts newline)
2. User message appended to local state immediately (with a stable `id` for React keys)
3. `streamChat` sends `{ content, conversationId? }` with an `AbortSignal`. The server owns the model transcript; the browser remembers the id returned in `X-Conversation-Id`
4. During the stream:
   - `assistant_delta` events append to a live assistant draft (`{ status: 'streaming', text }`). An idle draft (`{ status: 'idle' }`) means no streaming bubble
   - `assistant_tool_round` holds nonempty commentary from the latest tool round until the final reply arrives; tool-call arguments and result JSON are not kept in display history
   - First `tool_call_started` inserts a **Hunt** row after that request's user message; running tools appear in the dashed box and leave on `tool_call_finished`
   - `error` latches failure (further events ignored); `done` / `assistant_message` / abort mark the episode **Hunted** and clear running tools
5. On `assistant_message`, the reducer appends any pending commentary and the final reply to display messages. An interrupted turn discards pending commentary and removes its optimistic user row
6. Transient streaming state is cleared; a **Hunted** label remains if any tools ran. Hover or focus **Hunted** to see the tools that were called. Streams that end without `done`/`error` fail closed
7. The transcript renders the display messages directly, plus Hunt/Hunted rows. Roles display as **Upstairs** / **Jeffy**

If the API returns 404 for an unknown conversation, the hook clears the local conversation and id so the next send starts a new one.

In-flight streams are cancelled via **Cancel**, sending a new message, or unmount. Abort clears transient UI without treating abort as a hard error.

### Message rendering (`MarkdownContent`)

User and assistant message bodies (including the live streaming bubble) are rendered through `MarkdownContent` using **GitHub-flavored markdown** (`react-markdown` + `remark-gfm`):

- Supports headings, lists, blockquotes, tables, inline/fenced code, links, and emphasis
- Markdown updates live during streaming as tokens arrive
- Raw HTML in message source is **not** rendered (`rehype-raw` is not used); content is treated as untrusted
- External links open in a new tab with `rel="noopener noreferrer"` (`target`/`rel` applied after prop spread)
- Images are not fetched (placeholder text only)

### Textarea

- Default height matches two prototype rows; auto-grows with content up to **8 lines**, then scrolls internally
- Resets to default height after send

### Input focus

- The message textarea receives focus when the chat panel mounts (page load)
- Focus returns to the textarea when `sending` returns to `false` after a request completes (success, error, or abort)

### Layout width

- The den is centered at **min(900px, 100%)** with 1rem page padding
- Message bubbles cap at **min(500px, 94%)** of the feed, aligned like look 07 Take
- Composer spans the den width (textarea + Hunt button)

### Composer docking

- Composer sits in the den grid (not `position: fixed` on the viewport)
- Hunt button is the send action; **Cancel** replaces it while a request is in flight

### Auto-scroll

- Tracks whether the message list is pinned to the bottom (within 24px threshold)
- On new messages, streaming content, or activity updates, scrolls to bottom **only if** already at bottom
- Scrolling up to read history prevents forced scroll on new messages

## Vite configuration

**File:** `vite.config.ts`

```typescript
server: {
  proxy: {
    '/health': 'http://localhost:5008',
    '/api': 'http://localhost:5008',
  },
}
```

During `npm run dev`, API calls use relative URLs (`/api/chat/stream` for the UI, `/api/chat` also available) and Vite proxies them to the backend.

## Build output

`npm run build` runs `tsc -b && vite build` and outputs to `dist/`.

## Linting and formatting

`npm run lint` runs **oxlint** with React hooks rules (`.oxlintrc.json`).

`npm run format` / `npm run format:check` run **Prettier** (`.prettierrc.json`) on `JeffyGPT.Web` and `.userdocs`. The generated `.userdocs/user-guide.html` is ignored so Prettier does not rewrite the Documancer export. Indent and charset also come from the repo-root `.editorconfig` (TypeScript 2-space).

## Exploratory look prototypes

HTML chat-shell sketches live in `prototypes/jeffygpt-looks/`. They are not imported by the React app. The shipped UI matches look 07 Take. Branding notes: [Branding & Themes](./branding-and-themes.md#exploratory-look-prototypes).
