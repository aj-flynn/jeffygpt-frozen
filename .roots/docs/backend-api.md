# Backend API

The API project is the HTTP entry point for JeffyGPT chat and read functionality.

**Path:** `src/JeffyGPT/JeffyGPT.Api/`

> **JeffyGPT is a chat application with read-only document access via configured filesystem tools. It does not expose document-write or ingestion APIs.**

## Program.cs

`Program.cs` is intentionally thin:

1. Configure CORS (pinned to Vite origins `http://localhost:5173` and `http://127.0.0.1:5173`) and the `chat` rate limiter (60 req/min)
2. Call `AddApplication()` and `AddInfrastructure()`
3. Build the host, then **await `IMcpToolBootstrapper.InitializeAsync()`** (connect MCP servers / discover tools)
4. Log the active assistant provider at startup (no API keys)
5. `UseCors()` + `UseRateLimiter()`, then map endpoint groups
6. Run the host

```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
await app.Services.GetRequiredService<IMcpToolBootstrapper>().InitializeAsync();

app.UseCors();
app.MapHealthEndpoints();
app.MapChatEndpoints();
```

A `public partial class Program` declaration exists at the bottom for integration test hosting via `WebApplicationFactory<Program>`.

If a required MCP server fails to connect, the process throws at step 3 and does not listen. See [JeffyMCP](./jeffymcp.md) and [Development](./development.md#mcp-and-tool-configuration).

## Endpoints

Endpoints live in `src/JeffyGPT/JeffyGPT.Api/Endpoints/` as static extension classes. This keeps route definitions out of `Program.cs`.

### Health — `HealthEndpoints.cs`

| Method | Route | Response |
|--------|-------|----------|
| `GET` | `/health` | `{ "status": "ok" }` |

Returns a `HealthResponse` record from Contracts. Used for liveness checks and frontend connectivity verification.

### Chat — `ChatEndpoints.cs`

| Method | Route | Request | Success | Error |
|--------|-------|---------|---------|-------|
| `POST` | `/api/chat` | `ChatTurnRequest` JSON body | `200` + `ChatResponse` | `400` invalid text; `404` unknown conversation; `409` busy conversation; `502` assistant failure |

Handler signature:

```csharp
private static async Task<IResult> HandleChat(
    ChatTurnRequest request,
    IChatOrchestrator orchestrator,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken)
```

- Delegates all logic to `IChatOrchestrator.SendAsync`
- Catches `ArgumentException` from validation and returns `400 Bad Request`
- Maps unknown conversation ids to 404 and concurrent turns on a conversation to 409
- Catches `AssistantClientException` from the OpenAI-compatible client and returns `502 Bad Gateway` with a safe `{ "error": "..." }` message (no upstream response body)
- Does not contain echo, tool, or LLM logic

### Chat stream — `ChatEndpoints.cs`

| Method | Route | Request | Success | Error |
|--------|-------|---------|---------|-------|
| `POST` | `/api/chat/stream` | `ChatTurnRequest` JSON body | `200` + NDJSON stream | `400` invalid text, `404` unknown conversation, or `409` busy conversation before opening; `error` event for failures during streaming |

Response content type: `application/x-ndjson` — one JSON object per line, flushed after each event.

Both routes accept `{ "content": "hello", "conversationId": "optional-existing-id" }`. Omit or leave `conversationId` blank to start a conversation. Sync JSON includes `conversationId`; the streaming response returns it in the `X-Conversation-Id` header. Clients send that id with the next user message. The server stores the transcript until the API restarts.

| Event `type` | Payload |
|--------------|---------|
| `assistant_delta` | `{ "content": "..." }` |
| `assistant_tool_round` | `{ "message": ChatMessageDto }` (assistant with `toolCalls`) |
| `tool_call_started` | `{ "toolCall": ToolCallDto }` |
| `tool_call_finished` | `{ "toolResult": ToolResultDto }` |
| `assistant_message` | `{ "message": ChatMessageDto }` |
| `error` | `{ "content": "..." }` |
| `done` | `{}` |

The handler calls `IChatOrchestrator.StartTurn` before opening the stream, then enumerates `ChatTurn.Events`. If the assistant provider throws `AssistantClientException` during streaming, the handler writes one `error` event and stops. Client disconnect cancels in-flight LLM and tool work via `CancellationToken`.

## CORS

Configured in `Program.cs`:

- **All environments:** `http://localhost:5173` and `http://127.0.0.1:5173` only (not every loopback port — avoids local browser abuse until auth ships)
- **Headers / methods:** any
- **Exposed response header:** `X-Conversation-Id`

Production CORS for non-local origins is not yet configured.

## Rate limiting

`AddRateLimiter` registers a fixed-window policy named `chat` (60 requests per minute). Both `/api/chat` and `/api/chat/stream` call `.RequireRateLimiting("chat")`. Excess requests receive `429`.

## Configuration files

### launchSettings.json

**Path:** `src/JeffyGPT/JeffyGPT.Api/Properties/launchSettings.json`

| Profile | URL |
|---------|-----|
| `http` | `http://localhost:5008` |
| `https` | `https://localhost:7268` and `http://localhost:5008` |

### appsettings.json

Base ASP.NET logging plus **`JeffyGPT:Mcp`** so the API can spawn/connect to JeffyMCP (and other MCP servers):

```json
"JeffyGPT": {
  "Mcp": {
    "Servers": [
      {
        "Id": "jeffymcp",
        "Enabled": true,
        "Required": true,
        "Transport": "Stdio",
        "Command": "dotnet",
        "Arguments": ["../../JeffyMCP/bin/Debug/net10.0/JeffyMCP.dll"],
        "WorkingDirectory": "../../JeffyMCP",
        "Policy": { "AllowAllTools": true }
      }
    ]
  }
}
```

Filesystem sandbox roots live in **JeffyMCP** (`JeffyMCP:Tools`) — not in the API. See [JeffyMCP](./jeffymcp.md) and [Infrastructure](./backend-infrastructure.md#mcp-client-configuration). Assistant provider settings live in `appsettings.Development.json`.

### appsettings.Development.json

Local assistant configuration under `JeffyGPT:Assistant` (see [Infrastructure](./backend-infrastructure.md)). Set `Provider` to `Fake` for offline dev or `OpenAiCompatible` for OpenAI/vLLM testing.

## Debugging

VS Code / Cursor launch configurations in `.vscode/launch.json`:

- **JeffyGPT.Api (Fake)** — offline `FakeAssistantClient` (default dev)
- **JeffyGPT.Api (OpenAI Compatible)** — sets `JeffyGPT__Assistant__Provider=OpenAiCompatible`

Both pre-build the API project, set `ASPNETCORE_URLS=http://localhost:5008`, and use `ASPNETCORE_ENVIRONMENT=Development`.

See [Development Guide](./development.md) for full debug workflow.

## JeffyGPT.Api.http

**Path:** `src/JeffyGPT/JeffyGPT.Api/JeffyGPT.Api.http`

REST Client / VS Code `.http` samples for local manual testing against `http://localhost:5008`:

- `GET /health`
- `POST /api/chat` with a minimal `ChatTurnRequest` body
- `POST /api/chat/stream` with `Accept: application/x-ndjson` for NDJSON streaming
