# Contracts (DTOs)

The Contracts project defines shared request/response types used by the API, Application, and Infrastructure layers. Types are **client-neutral** — they do not contain frontend-specific fields.

**Path:** `src/JeffyGPT/JeffyGPT.Contracts/`

All types are **immutable records** with positional constructor parameters. ASP.NET Core serializes them to JSON with **camelCase** property names by default.

## Chat DTOs

**Namespace:** `JeffyGPT.Contracts.Chat`  
**Path:** `src/JeffyGPT/JeffyGPT.Contracts/Chat/`

### ChatMessageDto

```csharp
public sealed record ChatMessageDto(
    string Role,
    string Content,
    IReadOnlyList<ToolCallDto>? ToolCalls = null,
    string? ToolCallId = null);
```

A single message in the conversation. Optional fields support replaying tool rounds to the LLM:

| Property | Description |
|----------|-------------|
| `role` | Server-built message role: `user`, `assistant`, `system`, or `tool` |
| `content` | Text content or tool result JSON |
| `toolCalls` | Present on assistant messages that invoked tools |
| `toolCallId` | Present on `tool` role messages; links to the originating call |

**JSON examples:**

```json
{ "role": "user", "content": "hello" }
```

```json
{
  "role": "assistant",
  "content": "",
  "toolCalls": [
    { "id": "call_abc", "name": "get-current-time", "argumentsJson": "{}" }
  ]
}
```

```json
{
  "role": "tool",
  "content": "{\"local\":\"2026-06-27T08:00:00.0000000-04:00\"}",
  "toolCallId": "call_abc"
}
```

```json
{
  "role": "assistant",
  "content": "",
  "toolCalls": [
    { "id": "call_roots", "name": "list-document-roots", "argumentsJson": "{}" }
  ]
}
```

```json
{
  "role": "tool",
  "content": "{\"roots\":[{\"alias\":\"docs\",\"description\":\"JeffyGPT project documentation root\",\"entryPoint\":\"README.md\"}]}",
  "toolCallId": "call_roots"
}
```

```json
{
  "role": "assistant",
  "content": "",
  "toolCalls": [
    {
      "id": "call_read",
      "name": "read-file",
      "argumentsJson": "{\"rootAlias\":\"docs\",\"relativePath\":\"README.md\"}"
    }
  ]
}
```

```json
{
  "role": "tool",
  "content": "{\"rootAlias\":\"docs\",\"relativePath\":\"README.md\",\"content\":\"...\",\"bytesRead\":1234}",
  "toolCallId": "call_read"
}
```

```json
{
  "role": "assistant",
  "content": "",
  "toolCalls": [
    {
      "id": "call_read",
      "name": "read-file",
      "argumentsJson": "{\"rootAlias\":\"docs\",\"relativePath\":\"overview.md\"}"
    }
  ]
}
```

```json
{
  "role": "tool",
  "content": "{\"rootAlias\":\"docs\",\"relativePath\":\"overview.md\",\"content\":\"...\",\"bytesRead\":1234}",
  "toolCallId": "call_read"
}
```

### ChatTurnRequest

```csharp
public sealed record ChatTurnRequest(string? ConversationId, string? Content);
```

Request body for `POST /api/chat` and `POST /api/chat/stream`. Contains only new user text and an optional conversation id. Omit or leave the id blank to start a conversation. The server stores the transcript in memory. Streaming responses return the id in `X-Conversation-Id`; sync responses include `conversationId` in the JSON body.

### ChatResponse

```csharp
public sealed record ChatResponse(
    string ConversationId,
    ChatMessageDto AssistantMessage,
    IReadOnlyList<ToolCallDto> ToolCalls,
    IReadOnlyList<ToolResultDto> ToolResults);
```

Response from `POST /api/chat`.

| Property | Description |
|----------|-------------|
| `conversationId` | Id to send with the next user message |
| `assistantMessage` | The assistant's text reply |
| `toolCalls` | Tool invocations requested by the assistant (may be empty) |
| `toolResults` | Results from executing those tool calls (may be empty) |

### ChatStreamEvent

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AssistantDeltaEvent), "assistant_delta")]
// ... one JsonDerivedType per event ...
public abstract record ChatStreamEvent;

public sealed record AssistantDeltaEvent(string Content) : ChatStreamEvent;
public sealed record AssistantToolRoundEvent(ChatMessageDto Message) : ChatStreamEvent;
public sealed record ToolCallStartedEvent(ToolCallDto ToolCall) : ChatStreamEvent;
public sealed record ToolCallFinishedEvent(ToolResultDto ToolResult) : ChatStreamEvent;
public sealed record AssistantMessageEvent(ChatMessageDto Message) : ChatStreamEvent;
public sealed record StreamErrorEvent(string Content) : ChatStreamEvent;
public sealed record StreamDoneEvent : ChatStreamEvent;
```

One line in the NDJSON response from `POST /api/chat/stream`. The abstract base carries a `"type"` JSON discriminator; each derived record carries exactly the payload for its event, so invalid combinations are unrepresentable. The wire shape is unchanged: `{"type":"...", ...payload}`.

| `type` | Record | Payload field |
|--------|--------|---------------|
| `assistant_delta` | `AssistantDeltaEvent` | `content` |
| `assistant_tool_round` | `AssistantToolRoundEvent` | `message` (assistant message with `toolCalls`, before tools run) |
| `tool_call_started` | `ToolCallStartedEvent` | `toolCall` |
| `tool_call_finished` | `ToolCallFinishedEvent` | `toolResult` |
| `assistant_message` | `AssistantMessageEvent` | `message` |
| `error` | `StreamErrorEvent` | `content` |
| `done` | `StreamDoneEvent` | none |

The exact serialized shape per event is pinned by `ChatStreamEventSerializationTests` in `JeffyGPT.Application.Tests`. Serializer options are shared via `ChatStreamJson.WireOptions` in Contracts (used by the API endpoint and tests).

### AssistantCompletion

```csharp
public sealed record AssistantCompletion(
    ChatMessageDto AssistantMessage,
    IReadOnlyList<ToolCallDto> ToolCalls);
```

Internal result from `IAssistantClient.CompleteAsync`. Not returned directly to HTTP clients — the orchestrator enriches it with tool results before building `ChatResponse`.

### ToolCallDto

```csharp
public sealed record ToolCallDto(string Id, string Name, string ArgumentsJson);
```

| Property | Description |
|----------|-------------|
| `id` | Unique identifier linking call to result |
| `name` | Tool name (e.g. `get-current-time`) |
| `argumentsJson` | JSON string of arguments |

### ToolResultDto

```csharp
public sealed record ToolResultDto(string ToolCallId, string ContentJson, bool IsError);
```

| Property | Description |
|----------|-------------|
| `toolCallId` | Matches `ToolCallDto.Id` |
| `contentJson` | JSON string result payload |
| `isError` | `true` if execution failed |

### ToolDefinitionDto

```csharp
public sealed record ToolDefinitionDto(
    string Name,
    string Description,
    string ParametersJsonSchema);
```

Describes a tool available to the assistant. `ParametersJsonSchema` is a JSON Schema string passed to the LLM client.

## Health DTOs

**Namespace:** `JeffyGPT.Contracts.Health`  
**Path:** `src/JeffyGPT/JeffyGPT.Contracts/Health/`

### HealthResponse

```csharp
public sealed record HealthResponse(string Status);
```

Returned by `GET /health`. Current value: `"ok"`.

## Frontend type mirror

TypeScript interfaces in `src/JeffyGPT/JeffyGPT.Web/src/api/chatTypes.ts` mirror the chat DTOs using camelCase property names to match JSON serialization.

| C# record property | TypeScript property |
|--------------------|---------------------|
| `Role` | `role` |
| `Content` | `content` |
| `ToolCalls` | `toolCalls` |
| `ToolCallId` | `toolCallId` |
| `AssistantMessage` | `assistantMessage` |
| `ToolCalls` | `toolCalls` |
| `ToolResults` | `toolResults` |
| `ArgumentsJson` | `argumentsJson` |
| `ToolCallId` | `toolCallId` |
| `ContentJson` | `contentJson` |
| `IsError` | `isError` |

Stream event types in TypeScript use a discriminated union (`ChatStreamEvent`) with a `type` field matching the C# JSON discriminator. Both sides are discriminated unions now: each TypeScript interface mirrors one C# derived record (`content`, `toolCall`, `toolResult`, `message`).

Keep C# and TypeScript shapes in sync when adding or changing DTOs.
