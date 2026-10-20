# Application Layer

The Application project contains business orchestration and abstractions. It has no knowledge of HTTP, vLLM SDKs, or databases.

**Path:** `src/JeffyGPT/JeffyGPT.Application/`

## Interfaces

All interfaces live in `src/JeffyGPT/JeffyGPT.Application/Chat/`.

### IChatOrchestrator

Entry point for chat operations from the API layer.

```csharp
ChatTurn StartTurn(string? conversationId, string? content);

Task<ChatResponse> SendAsync(
    string? conversationId,
    string? content,
    CancellationToken cancellationToken = default);
```

Implemented by `ChatOrchestrator`. `StartTurn` returns a conversation id and an `IAsyncEnumerable<ChatStreamEvent>` in `ChatTurn.Events`. Reading those events runs the turn. `SendAsync` collects the same events into one response.

The server stores conversations in memory. A blank id creates one; later requests send that id and only the new user text. The store lasts until the API restarts.

### IAssistantClient

Seam for LLM completion. Accepts conversation history and available tool definitions; returns an assistant message and any requested tool calls.

Implementations: `FakeAssistantClient` (offline echo), `OpenAiCompatibleAssistantClient` (HTTP to OpenAI-compatible APIs).

```csharp
Task<AssistantCompletion> CompleteAsync(
    IReadOnlyList<ChatMessageDto> messages,
    IReadOnlyList<ToolDefinitionDto> tools,
    CancellationToken cancellationToken = default);

IAsyncEnumerable<AssistantStreamPart> CompleteStreamingAsync(
    IReadOnlyList<ChatMessageDto> messages,
    IReadOnlyList<ToolDefinitionDto> tools,
    CancellationToken cancellationToken = default);
```

Returns `AssistantCompletion` (Contracts) for non-streaming calls. Streaming calls yield `AssistantContentDelta` parts during token generation and `AssistantCompletionFinished` once per LLM round.

### IToolCatalog

Returns the list of tools available to the assistant for the current request.

```csharp
IReadOnlyList<ToolDefinitionDto> GetTools();
```

### IToolExecutor

Executes a single tool call and returns a structured result.

```csharp
Task<ToolResultDto> ExecuteAsync(ToolCallDto toolCall, CancellationToken cancellationToken = default);
```

### IToolHandler

Defines a single backend tool: its catalog metadata, security descriptor, and execution logic.

```csharp
string Name { get; }
ToolDefinitionDto Definition { get; }
ToolSecurityDescriptor Security { get; }
Task<ToolResultDto> ExecuteAsync(ToolCallDto toolCall, CancellationToken cancellationToken = default);
```

Implementations live in `Infrastructure/Mcp/` (proxied MCP tools) and `JeffyGPT.Application/Chat/Tools/` (security types). Tool **implementations** live in `src/JeffyMCP/`.

### Tool security types

**Folder:** `src/JeffyGPT/JeffyGPT.Application/Chat/Tools/`

Each handler exposes a required `ToolSecurityDescriptor` describing what the tool can do and what data it may expose. Security metadata is backend-only — it is not sent to the LLM or frontend.

| Type | Purpose |
|------|---------|
| `ToolAccessKind` | `ReadOnly` or `Mutating` |
| `ToolDataExposure` | `None`, `RuntimeMetadata`, `LocalFileSystem`, `ExternalService`, `UserPrivateData` |
| `ToolSecurityDescriptor` | `AccessKind`, `DataExposure`, `RequiresConfirmation`, optional `SafetyNotes` |
| `ToolExecutionPolicyResult` | `IsAllowed`, optional `ErrorMessage` |
| `IToolExecutionPolicy` | Evaluates whether a tool call may run (mutating / confirmation) |

Filesystem path sandbox types live in **JeffyMCP** (`JeffyMCP.FileSystem`), not in Application.

### IToolExecutionPolicy

Evaluates a handler's security descriptor before execution.

```csharp
ToolExecutionPolicyResult Evaluate(IToolHandler handler);
```

Implemented by `DefaultToolExecutionPolicy` in Infrastructure. Denies **mutating** tools and tools that **require confirmation**. There is no confirmation prompt UI yet — `RequiresConfirmation: true` means **deny**. MCP tools allowed by per-server `Policy.AllowAllTools` or `Policy.AllowedTools` are registered as executable and are not re-checked here. Unapproved tools are not registered.

### IToolArgumentValidator

Validates tool call arguments against a compiled JSON Schema before handler execution.

```csharp
ToolArgumentValidationResult Validate(JsonSchema schema, string toolName, string argumentsJson);
```

Returns `IsValid: false` with a safe error message when arguments are not valid JSON or do not match the schema. Implemented by `JsonSchemaToolArgumentValidator` in Infrastructure.

## ChatOrchestrator

**File:** `src/JeffyGPT/JeffyGPT.Application/Chat/ChatOrchestrator.cs`

Personality lives in `JeffyPersonality.cs` as a C# raw string (`SystemPrompt`). The orchestrator prepends it; it is never accepted from the client.

Orchestration steps:

1. **Start the turn** — `StartTurn` validates user text, finds or creates the conversation, checks that it is idle and within the stored character limit, then copies the saved messages and appends the new user text
2. **Load tools** — `_toolCatalog.GetTools()`
3. **Personality** — `JeffyPersonality.WithSystemPrompt` inserts a server-owned `system` message (`JeffyPersonality.SystemPrompt`) ahead of the working transcript
4. **Tool loop** (up to 40 rounds per request):
   - Call `_assistantClient.CompleteStreamingAsync(conversation, tools)`
   - If no tool calls → finish with the assistant message
   - If 40 tool rounds have already run → finish with the limit message and do not execute another round
   - Otherwise append the assistant message, execute each tool call via `_toolExecutor.ExecuteAsync`, append the `role: "tool"` results, and repeat
5. **Return** — `ChatResponse` with the final assistant message plus accumulated tool calls and tool results (for sync clients)

If the iteration limit is reached while the assistant still requests tools, both paths return a **safe assistant message** explaining that the loop stopped after the configured maximum (40 tool rounds). Accumulated tool calls and results from completed rounds are still included in the sync `ChatResponse`. Each request starts a new count.

`SendAsync` collects the final assistant message, tool calls, and tool results from `ChatTurn.Events`.

### Turn events and transcript storage

The private `ReadEvents` iterator runs the tool loop. It yields `ChatStreamEvent` derived records (`AssistantDeltaEvent`, `AssistantToolRoundEvent`, `ToolCallStartedEvent`, `ToolCallFinishedEvent`, `AssistantMessageEvent`, `StreamErrorEvent`, `StreamDoneEvent` — see [Contracts](./backend-contracts.md)):

1. Stream LLM content via `CompleteStreamingAsync` → yield `assistant_delta` for each token chunk
2. When the LLM round completes with tool calls → yield `assistant_tool_round` (assistant message with `toolCalls`), then `tool_call_started`, execute tools, yield `tool_call_finished`
3. When the LLM round completes without tool calls → yield `assistant_message`, then `done`

If the iteration limit is reached while the assistant still requests tools, the iterator yields `assistant_message` with the same safe limit explanation, then `done`. Tool events from completed rounds are still emitted before the final message.

After a final reply, the iterator saves the working transcript. An exception or cancellation before the final reply discards that copy. Its `finally` block releases the conversation's busy flag. Callers must consume or dispose the event iterator to release an active turn.

## Turn validation

Validation lives in `ChatOrchestrator.RequireUserText` and `TakeDraft`.

| Rule | Error message |
|------|---------------|
| Blank user text | `"A user message is required."` |
| Trimmed text > 32,000 chars | `"Each message content must be at most 32000 characters."` |
| Saved content plus new text > 128,000 chars | `"The conversation has reached its 128000 character limit."` |
| Unknown or malformed conversation id | `ConversationNotFoundException` → HTTP 404 |
| Conversation already running a turn | `ConversationBusyException` → HTTP 409 |

The request carries only `conversationId` and `content`; message roles and tool results are built on the server. When saving a completed draft over the character limit, `WithinStoredLimit` walks backward and retains messages that fit.

## DependencyInjection

**File:** `src/JeffyGPT/JeffyGPT.Application/DependencyInjection.cs`

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddSingleton<IChatOrchestrator, ChatOrchestrator>();
    return services;
}
```

Requires `Microsoft.Extensions.DependencyInjection.Abstractions`.

## Message roles

The server builds messages with string roles. Clients send user text through `ChatTurnRequest`, not message arrays.

| Role | Usage |
|------|-------|
| `user` | Validated user text appended at the start of a turn |
| `assistant` | Model replies and tool-call messages |
| `system` | Injected by `JeffyPersonality` on every assistant call |
| `tool` | Server-built tool results |
