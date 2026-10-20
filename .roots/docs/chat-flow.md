# Chat Flow

End-to-end description of a chat request from the browser through the backend and back.

## Web UI flow (streaming)

The React UI uses `POST /api/chat/stream`. The sync `POST /api/chat` endpoint remains available for non-streaming clients.

### Sequence diagram

```
User                ChatPanel          API              Orchestrator       AssistantClient    ToolExecutor
  |                    |                |                    |                    |                |
  |-- type + send ---->|                |                    |                    |                |
  |                    |-- POST /api/chat/stream --------->|                    |                |
  |                    |                |-- StartTurn ------>|                    |                |
  |                    |                |                    |-- CompleteStreamingAsync --------->|
  |                    |                |                    |<-- content deltas -|                |
  |                    |                |                    |-- assistant_delta events           |
  |                    |<-- NDJSON -----|                    |                    |                |
  |<-- live bubble -----|                |                    |                    |                |
  |                    |                |                    |<-- tool calls -----|                |
  |                    |                |                    |-- tool_call_started              |
  |                    |                |                    |-- ExecuteAsync (policy → validate → handler) -->|
  |                    |                |                    |<-- ToolResultDto -------------------|
  |                    |                |                    |-- tool_call_finished             |
  |                    |                |                    |-- CompleteStreamingAsync (again)->|
  |                    |                |                    |<-- final content deltas ----------|
  |                    |<-- NDJSON -----|                    |                    |                |
  |                    |                |                    |-- assistant_message + done       |
  |<-- final message ---|                |                    |                    |                |
```

## Frontend flow

### 1. User sends a message

`ChatComposer` clears the input and delegates to `useChatStream.send` (`src/hooks/useChatStream.ts`), which:

- Trims input; ignores empty or duplicate sends while `sending`
- Builds `ChatMessageDto` with `role: "user"`
- Appends to local `messages` state (optimistic UI)
- Clears prior errors and transient streaming state
- Aborts any in-flight stream from a previous send

### 2. API request

`streamChat({ content, conversationId? })` sends:

```http
POST /api/chat/stream
Content-Type: application/json
Accept: application/x-ndjson

{
  "content": "what time is it"
}
```

The client sends new user text. The server keeps model history in memory and returns a conversation id in the `X-Conversation-Id` response header. Later requests include that id as `conversationId`.

### 3. Stream handling

The response is NDJSON — one event per line:

| Event | UI effect |
|-------|-----------|
| `assistant_delta` | Append text to live assistant bubble |
| `assistant_tool_round` | Hold nonempty commentary from the latest tool round until the reply completes |
| `tool_call_started` | Insert **Hunt** row if needed; show running tool in the dashed box; hide live bubble |
| `tool_call_finished` | Remove that tool from the Hunt box; discard the result payload after handling the event |
| `assistant_message` | Append pending commentary and the final reply; mark Hunt **Hunted**; clear running tools |
| `error` | Show error; discard pending commentary; mark Hunt **Hunted**; clear running tools |
| `done` | Ensure transient state is cleared; Hunt episodes that ran stay **Hunted** |

The client keeps display messages (`id`, `role`, `content`) and tool activity in one stream state object. Tool-result JSON and tool-call arguments stay out of display history. Users see **Upstairs** / **Jeffy** messages plus Hunt/Hunted rows. Running tools appear inside the Hunt box and leave when they finish; after the request, the **Hunted** label remains. Hover or focus **Hunted** to see the tools that ran. The streaming bubble is transient during the request.

An interrupted turn removes its optimistic user row and pending commentary. A 404 clears the local conversation and id so the next send starts a new conversation.

### 4. UI updates

- Auto-scrolls message list if user was already at the bottom (within 24px)
- A **Hunt** row appears in the transcript when the first tool starts; the dashed box lists running tools and hides when none are running
- After the request, that row’s label becomes **Hunted** and the box is gone. Hover or focus **Hunted** to see the tools that were called
- Message textarea receives focus on page load and when each response completes
- Chat lives in a centered den (`min(900px, 100%)`); composer is in-den with a **Hunt** button
- Idle chrome status rerolls after each completed request
- No composer activity popup after completion

## Sync flow (`POST /api/chat`)

Non-streaming clients receive a single `ChatResponse` JSON body after the full tool loop completes. See backend docs for the sync request/response shape.

## Backend flow

### 1. Endpoint (`ChatEndpoints`)

| Route | Handler | Response |
|-------|---------|----------|
| `POST /api/chat` | `HandleChat` | `ChatResponse` JSON |
| `POST /api/chat/stream` | `HandleChatStream` | NDJSON stream of `ChatStreamEvent` |

Both delegate to `IChatOrchestrator`. Invalid user text returns HTTP 400, unknown conversation ids return 404, and busy conversations return 409. For streaming requests these checks run before opening the NDJSON response.

### 2. Turn setup (`ChatOrchestrator.StartTurn`)

- Require nonblank user text, trimmed to at most 32,000 characters
- Create a conversation for a blank id, or find an existing conversation by id
- Require an idle conversation whose saved content plus the new text fits the 128,000-character limit
- Mark the conversation busy, copy its saved transcript, and append the user message

`StartTurn` returns a `ChatTurn` with the id and events. Enumerating `ChatTurn.Events` runs the assistant/tool loop. A final reply commits the working transcript; an exception or cancellation before that reply discards it. The iterator releases the busy flag when it finishes or is disposed. Conversations are lost when the API restarts.

### 3. Personality (`JeffyPersonality`)

`ChatOrchestrator` prepends a server-owned `system` message from `JeffyPersonality.SystemPrompt` (Jeffy GPT crawlspace character: helpful first, cheerful deadpan, conspiracy enthusiast; uses tools in character and answers wait-until-night questions from the clock). Clients cannot send `system`. The prompt is not returned to the frontend.

### 4. Tool catalog (`McpToolCatalog.GetTools`)

After startup bootstrap, the catalog exposes tools discovered from configured MCP servers (JeffyMCP by default). Schemas come from MCP `tools/list` (C# MCP SDK reflection over tool parameters — plain strings for `rootAlias` / `relativePath`, not JSON Schema enums). Invalid aliases are rejected at runtime by JeffyMCP’s `FileSystemSandbox`, not by JeffyGPT’s JSON Schema validator. When multiple MCP servers are enabled, tool names are prefixed `{serverId}__{toolName}`.

### 5. Assistant completion (`IAssistantClient`)

Behavior depends on `JeffyGPT:Assistant:Provider`:

#### Fake provider (`FakeAssistantClient`)

1. If the last message is `role: "tool"`, formats a follow-up (parses `local` for time tool; generic `"Tool result: ..."` otherwise).
2. Otherwise inspects the **last** user message:
   - `"read docs overview"` → `read-file` tool call (`docs` / `overview.md`)
   - `"time"` (case-insensitive) → `get-current-time` tool call
   - else → `"Echo: {content}"`
3. Streaming: yields content in small chunks for natural-language responses; tool-call rounds emit completion only (no deltas).

#### OpenAI-compatible provider (`OpenAiCompatibleAssistantClient`)

- POSTs to `{BaseUrl}/chat/completions` with messages, tool definitions, and `tool_choice: auto`
- Streaming: adds `stream: true`, parses upstream SSE, yields content deltas and assembled tool calls
- Maps model response content and any `tool_calls` to `AssistantCompletion`

Both implementations return `AssistantCompletion` — they do **not** execute tools.

### 6. Tool execution (`ToolExecutor.ExecuteAsync`)

For each tool call in the completion:

1. Look up handler by name (unknown tools return error immediately)
2. Evaluate `IToolExecutionPolicy` (mutating / confirmation)
3. Validate arguments against the handler JSON schema
4. Execute the handler — for MCP tools, `McpProxiedToolHandler` calls MCP `tools/call` on the owning server. Unapproved tools never reach this step (they are not registered).

Examples:

- `get-current-time` → local timestamp JSON via JeffyMCP
- Unapproved MCP tools are not registered, so a model call for them returns `Unknown tool`
- In-process mutating/confirmation handlers → `{ "error": "Tool execution is not allowed by policy: ..." }`, `isError: true`
- Unknown tool → error JSON, `isError: true`

### 7. Tool loop and response assembly (`ChatOrchestrator`)

When the assistant returns tool calls:

1. Append the assistant message (including tool calls) to the working conversation
2. Execute each tool via `IToolExecutor`
3. Append `role: "tool"` messages with structured JSON results
4. Call the assistant again with the expanded conversation
5. Repeat until the assistant returns a message with no tool calls, or the limit of 40 tool rounds is reached

**Sync response** contains:

- **`conversationId`** — the id to send with the next user message
- **`assistantMessage`** — the final assistant reply after tool results have been fed back
- **`toolCalls`** / **`toolResults`** — accumulated from all tool rounds

**Stream response** emits events as each step occurs; the final `assistant_message` event carries the canonical reply.

## Example scenarios

### Simple echo (Fake provider)

**Request:**

```json
{ "content": "hello there" }
```

**Stream events (abbreviated):**

```
{"type":"assistant_delta","content":"Echo: "}
{"type":"assistant_delta","content":"hello "}
{"type":"assistant_delta","content":"there"}
{"type":"assistant_message","message":{"role":"assistant","content":"Echo: hello there"}}
{"type":"done"}
```

### Time tool trigger (Fake provider)

Any user message containing `"time"` (case-insensitive) triggers the fake tool path. The stream includes `tool_call_started` and `tool_call_finished` events, then content deltas for the final `"The current local time is {local}."` message.

### Document discovery (OpenAI-compatible provider)

Prerequisite: `jeffymcp` must allow the filesystem tools (`Policy.AllowAllTools: true` in the committed API `appsettings.json`, or list them in `Policy.AllowedTools`).

Typical multi-turn flow when the model needs project documentation:

1. Model calls `list-document-roots` with `{}`.
2. Tool result includes configured roots, for example `{ "roots": [{ "alias": "docs", "description": "JeffyGPT project documentation root", "entryPoint": "README.md" }] }`.
3. Model calls `list-files` with `rootAlias: "docs"` and `relativePath: ""` (or a subdirectory path) to discover readable files under that root.
4. Model calls `read-file` with `rootAlias: "docs"` and `relativePath: "README.md"` (or another path from the listing).
5. Orchestrator feeds tool results back until the model produces a final natural-language answer.

Configured `EntryPoint` is metadata only — the fake provider demo below still hardcodes `overview.md` for the `"read docs overview"` trigger.

### read-file demo (Fake provider)

Prerequisite: `jeffymcp` must allow the filesystem tools (`Policy.AllowAllTools: true` in the committed API `appsettings.json`, or list them in `Policy.AllowedTools`).

**Request:**

```json
{ "content": "read docs overview" }
```

**Flow:**

1. `FakeAssistantClient` returns empty assistant content and a `read-file` tool call (`rootAlias: "docs"`, `relativePath: "overview.md"`).
2. `ToolExecutor` validates arguments and dispatches to `McpProxiedToolHandler`.
3. The proxy calls MCP `tools/call` on JeffyMCP.
4. JeffyMCP’s `FileSystemSandbox` resolves `docs` → `../../.roots/docs` (content root = JeffyMCP project folder) and returns file content JSON (no absolute paths).
5. Fake provider follow-up returns `Tool result: {content}` (generic tool-message handling).

### Validation error

**Request:**

```json
{ "content": " " }
```

**Response:** `400 Bad Request`

```json
{ "error": "A user message is required." }
```

## Multi-step tool use

The orchestrator supports multiple tool rounds in a single request (for example, when the model calls a tool, receives the result, and then produces a natural-language answer). The streaming UI shows progress as each round completes; the sync endpoint returns one final JSON response.

### Iteration limit

The tool loop allows up to **40 tool execution rounds** per request. A later request starts again at zero. If the assistant still requests tools after the fortieth round completes, the orchestrator stops and returns a safe assistant message:

> The tool loop stopped after the maximum of 40 tool rounds.

**Sync (`POST /api/chat`):** The `ChatResponse` includes this message as `assistantMessage`, plus any `toolCalls` and `toolResults` accumulated from rounds that completed before the limit.

**Stream (`POST /api/chat/stream`):** After the last completed tool round events, the stream yields `assistant_message` with the limit text, then `done`.
