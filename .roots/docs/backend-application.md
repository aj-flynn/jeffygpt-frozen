# Application Layer

The Application project contains business orchestration and abstractions. It has no knowledge of HTTP, vLLM SDKs, or databases.

**Path:** `src/JeffyGPT/JeffyGPT.Application/`

## Interfaces

All interfaces live in `src/JeffyGPT/JeffyGPT.Application/Chat/`.

### IChatOrchestrator

Entry point for chat operations from the API layer.

```csharp
Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken cancellationToken = default);

IAsyncEnumerable<ChatStreamEvent> StreamAsync(
    ChatRequest request,
    CancellationToken cancellationToken = default);
```

Implemented by `ChatOrchestrator`. `SendAsync` returns a single JSON response; `StreamAsync` yields NDJSON events for the web UI.

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
ToolExecutionPolicyResult Evaluate(IToolHandler handler, ToolCallDto toolCall);
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

1. **Validate** — `ChatRequestValidator.Validate(request)`
2. **Load tools** — `_toolCatalog.GetTools()`
3. **Personality** — `JeffyPersonality.WithSystemPrompt` inserts a server-owned `system` message (`JeffyPersonality.SystemPrompt`) ahead of client history. Clients cannot send `system`.
4. **Tool loop** (up to 40 rounds per request):
   - Call `_assistantClient.CompleteStreamingAsync(conversation, tools)`
   - If no tool calls → finish with the assistant message
   - If 40 tool rounds have already run → finish with the limit message and do not execute another round
   - Otherwise append the assistant message, execute each tool call via `_toolExecutor.ExecuteAsync`, append the `role: "tool"` results, and repeat
5. **Return** — `ChatResponse` with the final assistant message plus accumulated tool calls and tool results (for sync clients)

If the iteration limit is reached while the assistant still requests tools, both paths return a **safe assistant message** explaining that the loop stopped after the configured maximum (40 tool rounds). Accumulated tool calls and results from completed rounds are still included in the sync `ChatResponse`. Each request starts a new count.

`SendAsync` does not run a second loop. It collects the final assistant message, tool calls, and tool results from `StreamAsync`.

### StreamAsync

`StreamAsync` is the tool loop. It yields `ChatStreamEvent` derived records (`AssistantDeltaEvent`, `AssistantToolRoundEvent`, `ToolCallStartedEvent`, `ToolCallFinishedEvent`, `AssistantMessageEvent`, `StreamErrorEvent`, `StreamDoneEvent` — see [Contracts](./backend-contracts.md)):

1. Stream LLM content via `CompleteStreamingAsync` → yield `assistant_delta` for each token chunk
2. When the LLM round completes with tool calls → yield `assistant_tool_round` (assistant message with `toolCalls`), then `tool_call_started`, execute tools, yield `tool_call_finished`
3. When the LLM round completes without tool calls → yield `assistant_message`, then `done`

If the iteration limit is reached while the assistant still requests tools, `StreamAsync` yields `assistant_message` with the same safe limit explanation, then `done` (no `error` event). Tool events from completed rounds are still emitted before the final message.

## ChatRequestValidator

**File:** `src/JeffyGPT/JeffyGPT.Application/Chat/ChatRequestValidator.cs`

Public static class. Throws `ArgumentException` on invalid requests.

| Rule | Error message |
|------|---------------|
| `Messages` is null or empty | `"Messages cannot be null or empty."` |
| More than 50 messages | `"At most 50 messages are allowed."` |
| Role other than `user` / `assistant` | `"Only 'user' and 'assistant' roles are allowed in client requests."` |
| Client `toolCallId` or `toolCalls` | Rejected — tool rounds are rebuilt server-side within a turn |
| Per-message content > 32,000 chars | `"Each message content must be at most 32000 characters."` |
| Total content > 128,000 chars | `"Total message content must be at most 128000 characters."` |
| No message with `role: "user"` | `"A user message is required."` |

Role comparison is case-insensitive. `system` and `tool` roles are not accepted from clients. The orchestrator prepends Jeffy’s system prompt itself.

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

The contracts use simple string roles. **Client requests** may only include `user` and `assistant` (content only). Tool rounds use `assistant`+`toolCalls` and `tool` internally during orchestration.

| Role | Client request | Usage |
|------|----------------|-------|
| `user` | Allowed | User messages (at least one required) |
| `assistant` | Allowed (content only) | Prior model replies in history |
| `system` | Rejected from clients | Injected by `JeffyPersonality` on every assistant call |
| `tool` | Rejected | Server-built tool results within a turn only |
