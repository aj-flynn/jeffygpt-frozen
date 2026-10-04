# Infrastructure Layer

The Infrastructure project contains concrete implementations of Application abstractions.

**Path:** `src/JeffyGPT/JeffyGPT.Infrastructure/`

## Configuration

Assistant client selection is configured under `JeffyGPT:Assistant`:

| Setting | Default | Description |
|---------|---------|-------------|
| `Provider` | `Fake` | `Fake` or `OpenAiCompatible` |
| `OpenAiCompatible:BaseUrl` | `https://api.openai.com/v1` | OpenAI-compatible API base URL |
| `OpenAiCompatible:ApiKey` | `""` | Optional inline API key (prefer environment variable) |
| `OpenAiCompatible:ApiKeyEnvironmentVariable` | `OPENAI_API_KEY` | Env var read when `ApiKey` is empty |
| `OpenAiCompatible:Model` | `gpt-5.4-nano` | Model name sent to the provider |
| `OpenAiCompatible:ReasoningEffort` | *(omit)* | Optional chat/completions `reasoning_effort`. For GPT-5.6 + tools on chat/completions, set `none` (OpenAI otherwise returns 400) |
| `OpenAiCompatible:RequireApiKey` | `true` | When `false`, no bearer token is sent (for local vLLM without auth) |
| `OpenAiCompatible:TimeoutSeconds` | `120` | HTTP client timeout |

`AssistantOptions.Provider` defaults to `Fake` in code. `appsettings.Development.json` includes the `OpenAiCompatible` settings block for optional manual testing — see [Development Guide](./development.md).

The same `OpenAiCompatibleAssistantClient` can target a self-hosted vLLM OpenAI-compatible endpoint by changing `BaseUrl` and `Model`.

### MCP client configuration

JeffyGPT connects to one or more MCP servers at startup. Configure under `JeffyGPT:Mcp`:

| Setting | Description |
|---------|-------------|
| `Servers[]` | List of MCP server connections |
| `Servers[].Id` | Stable server id (used for multi-server tool prefixes) |
| `Servers[].Enabled` | When `false`, server is skipped (no tools from it) |
| `Servers[].Required` | When `true`, startup fails if connection fails; when `false`, log and continue without that server’s tools |
| `Servers[].Transport` | `Stdio` or `Http` |
| `Servers[].Command` / `Arguments` | Stdio transport: process to spawn |
| `Servers[].WorkingDirectory` | Stdio: child process cwd (JeffyMCP project folder so `appsettings.json` resolves) |
| `Servers[].Endpoint` | HTTP transport: MCP server base URL (standalone JeffyMCP `--http` → `http://localhost:3001`; Aspire injects a dynamic URL) |
| `Servers[].Policy.AllowAllTools` | When `true`, every discovered tool on that server is registered and executable with no further per-tool checks. Default `false`. |
| `Servers[].Policy.AllowedTools` | Original MCP tool names that may run. Required when `AllowAllTools` is `false` (startup throws if the list is empty). Ignored when `AllowAllTools` is `true`. |

Example (`appsettings.json`):

```json
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
```

HTTP example (run JeffyMCP with `dotnet run --project src/JeffyMCP/JeffyMCP.csproj --launch-profile http`):

```json
{
  "Id": "jeffymcp",
  "Transport": "Http",
  "Endpoint": "http://127.0.0.1:3001",
  "Policy": { "AllowAllTools": true }
}
```

JeffyGPT’s MCP client uses Streamable HTTP when `Transport` is `Http`. Outside Aspire, committed config uses **stdio**. Under Aspire, AppHost overrides `Servers[0]` to HTTP and injects the endpoint so demos can show Api → JeffyMCP traces (MCP stays non-external / loopback; still unauthenticated). Keep `jeffymcp` as the first server entry when using that override.

`McpOptions` is bound in `AddInfrastructure()` via `services.Configure<McpOptions>(...)`.

### JeffyMCP server configuration

Filesystem sandbox settings for built-in tools live in **JeffyMCP** under `JeffyMCP:Tools` (`src/JeffyMCP/appsettings.json`):

| Setting | Default | Description |
|---------|---------|-------------|
| `FileSystem:Roots` | `docs` root | Root alias → `{ Path, Description?, EntryPoint? }` |
| `FileSystem:MaxReadBytes` | `1048576` | Maximum file size for read tools |
| `FileSystem:AllowedExtensions` | `.md`, `.txt`, `.json`, `.cs`, `.ts`, `.tsx`, `.css` | Allowed extensions |

Paths are relative to the JeffyMCP content root when the server runs.

## DependencyInjection

**File:** `src/JeffyGPT/JeffyGPT.Infrastructure/DependencyInjection.cs`

```csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
```

Registration by `JeffyGPT:Assistant:Provider`:

| Provider | `IAssistantClient` implementation | Registration |
|----------|-----------------------------------|--------------|
| `Fake` | `FakeAssistantClient` | Singleton |
| `OpenAiCompatible` | `OpenAiCompatibleAssistantClient` | Typed `HttpClient` via `IHttpClientFactory` |

For `OpenAiCompatible`, register through the interface so the configured client is used:

```csharp
services.AddHttpClient<IAssistantClient, OpenAiCompatibleAssistantClient>((_, httpClient) =>
    OpenAiCompatibleAssistantClient.ConfigureHttpClient(
        httpClient,
        assistantOptions.OpenAiCompatible,
        apiKey));
```

Do **not** register `OpenAiCompatibleAssistantClient` separately as a singleton — that bypasses `HttpClient` configuration (`BaseAddress`, auth headers).

Unknown provider values throw at startup. When `RequireApiKey` is `true` (default), `OpenAiCompatible` requires an API key from config or the configured environment variable (resolved by `OpenAiApiKeyResolver` at startup). When `RequireApiKey` is `false`, registration succeeds without a key and `ConfigureHttpClient` omits the `Authorization` header.

HTTP failures from the upstream LLM API are surfaced as `AssistantClientException` with a safe message (status code only, no response body).

Also calls `AddMcpTools()` — see [MCP client](#mcp-client-layer) below.

## JeffyMCP server

**Path:** `src/JeffyMCP/`

Standalone [Model Context Protocol](https://modelcontextprotocol.io/) server exposing JeffyGPT's built-in tools. Uses the official C# SDK (`ModelContextProtocol`, `ModelContextProtocol.AspNetCore`).

**Primary doc:** [jeffymcp.md](./jeffymcp.md) (transports, sandbox config, Cursor/HTTP clients, adding tools).

| Transport | How to run |
|-----------|------------|
| **stdio** (default) | Prefer launching the built DLL (see JeffyGPT config); or `dotnet run --project src/JeffyMCP/JeffyMCP.csproj` |
| **HTTP** | `dotnet run --project src/JeffyMCP/JeffyMCP.csproj -- --http` → `http://localhost:3001` (or Aspire AppHost) |

Register in other MCP clients (Cursor / Claude Desktop) — prefer the **DLL + cwd** form so restore/build noise does not corrupt stdio JSON-RPC:

```json
{
  "mcpServers": {
    "JeffyMCP": {
      "command": "dotnet",
      "args": ["src/JeffyMCP/bin/Debug/net10.0/JeffyMCP.dll"],
      "cwd": "src/JeffyMCP"
    }
  }
}
```

(Paths relative to the repository root; build JeffyMCP first.)

### Registered tools (JeffyMCP)

| Name | Description |
|------|-------------|
| `get-current-time` | Returns current local time |
| `list-document-roots` | Lists configured document roots |
| `read-file` | Reads a sandboxed text file |
| `list-files` | Lists readable files in a sandbox directory |

Tool implementations live under `src/JeffyMCP/Tools/`. Filesystem sandbox: `src/JeffyMCP/FileSystem/FileSystemSandbox.cs`.

## MCP client layer

**Folder:** `src/JeffyGPT/JeffyGPT.Infrastructure/Mcp/`

JeffyGPT is an MCP **client**. At startup, `McpToolBootstrapper` connects to configured servers, discovers tools, and registers `McpProxiedToolHandler` instances.

```
Mcp/
├── McpConnectionManager.cs      # Connect, list tools, call tools
├── McpProxiedToolHandler.cs     # IToolHandler adapter → MCP tools/call
├── McpToolBootstrapper.cs       # Startup discovery; AllowAllTools / AllowedTools filter
├── McpToolHandlerRegistry.cs    # Populated handler list
├── McpToolCatalog.cs            # IToolCatalog over registry
├── McpToolNameResolver.cs       # Multi-server name prefixes
└── McpDependencyInjection.cs    # AddMcpTools()
```

### McpDependencyInjection

**File:** `src/JeffyGPT/JeffyGPT.Infrastructure/Mcp/McpDependencyInjection.cs`

```csharp
public static IServiceCollection AddMcpTools(this IServiceCollection services)
```

Registers:

- `McpConnectionManager`, `IMcpConnectionManager`
- `McpToolHandlerRegistry`, `IMcpToolBootstrapper`
- `IToolArgumentValidator` → `JsonSchemaToolArgumentValidator`
- `IToolExecutionPolicy` → `DefaultToolExecutionPolicy`
- `IToolExecutor` → `ToolExecutor`
- `IToolCatalog` → `McpToolCatalog`

Called from `Infrastructure/DependencyInjection.cs` after the assistant provider is selected.

`Program.cs` calls `IMcpToolBootstrapper.InitializeAsync()` before accepting traffic.

### Multi-server tool naming

When **one** MCP server is enabled, tools keep their original names (`read-file`). When **multiple** servers are enabled, names are prefixed: `{serverId}__{toolName}` (for example `jeffymcp__read-file`).

## Shared tool infrastructure

**Folder:** `src/JeffyGPT/JeffyGPT.Infrastructure/Tools/`

```
Tools/
├── DefaultToolExecutionPolicy.cs
├── ToolExecutor.cs
└── JsonSchemaToolArgumentValidator.cs
```

## DefaultToolExecutionPolicy

**File:** `src/JeffyGPT/JeffyGPT.Infrastructure/Tools/DefaultToolExecutionPolicy.cs`  
**Implements:** `IToolExecutionPolicy`

Evaluates each handler's `ToolSecurityDescriptor` before execution:

| Condition | Result |
|-----------|--------|
| `AccessKind == Mutating` | Deny — `"mutating tools are not permitted"` |
| `RequiresConfirmation == true` | Deny — `"tool requires user confirmation"` |
| Otherwise | Allow |

MCP tools that pass `AllowAllTools` or `AllowedTools` are registered as executable and are not classified as mutating. Unapproved tools are not registered. Path sandboxing for JeffyMCP filesystem tools stays in `FileSystemSandbox`.

## FakeAssistantClient

**File:** `src/JeffyGPT/JeffyGPT.Infrastructure/Chat/FakeAssistantClient.cs`  
**Implements:** `IAssistantClient`

Simulates an LLM response without calling any external service. Used for local development, tests, and fallback.

### Behavior

The fake client inspects the conversation in this order:

1. **Tool follow-up** — If the last message has `role: "tool"` and its JSON content includes a `local` property, returns `"The current local time is {local}."` (or `"Tool result: {content}"` for other tool payloads).
2. **User message** — Otherwise finds the **last** message with `role: "user"` (case-insensitive):
   - If the content contains `"read docs overview"` (case-insensitive), returns an assistant message with **empty content** and one tool call (`read-file` with `rootAlias: "docs"`, `relativePath: "overview.md"`).
   - Else if the content contains `"time"` (case-insensitive), returns an assistant message with **empty content** and one tool call (`get-current-time`).
   - Otherwise returns `"Echo: {last user message content}"`.

| Tool | `id` | `name` | `argumentsJson` |
|------|------|--------|-----------------|
| Time | `call_get-current-time` | `get-current-time` | `{}` |
| Docs demo | `call_read-file` | `read-file` | `{"rootAlias":"docs","relativePath":"overview.md"}` |

The `tools` parameter from the catalog is accepted but not used by the fake client.

### Examples

| User message | Assistant content | Tool calls |
|--------------|-------------------|------------|
| `hello` | `Echo: hello` | none |
| `what time is it` | (empty content) | `get-current-time` |
| `read docs overview` | (empty content) | `read-file` (requires the tool to be allowed by that server's `Policy`) |

### CompleteStreamingAsync

Yields content in 6-character chunks for natural-language responses, then `AssistantCompletionFinished`. Tool-call rounds emit only the completion (no content deltas), matching non-stream behavior.

## OpenAiCompatibleAssistantClient

**File:** `src/JeffyGPT/JeffyGPT.Infrastructure/Chat/OpenAiCompatibleAssistantClient.cs`  
**Implements:** `IAssistantClient`

Calls an OpenAI-compatible `POST {BaseUrl}/chat/completions` endpoint using `HttpClientFactory`. Provider wire-format DTOs (`ChatCompletionRequest`, `ChatCompletionStreamChunk`, etc.) live in `Chat/OpenAiWireModels.cs` (internal) and never leave Infrastructure. Both completion paths build their request through a shared `BuildRequest(messages, tools, stream)` helper.

### Request mapping

- `ChatMessageDto` → `messages[].role`, `messages[].content`
- `ToolDefinitionDto` → `tools[]` with `type: "function"` and `function.name`, `function.description`, `function.parameters` (parsed from `ParametersJsonSchema`)
- Sends `model`, `tool_choice: "auto"`
- When `ReasoningEffort` is configured, sends `reasoning_effort` (needed for GPT-5.6 function tools on chat/completions)
- Streaming requests also send `stream: true`

### Response mapping

- `choices[0].message.content` → `AssistantMessage` with `role: "assistant"` (empty string if null)
- `choices[0].message.tool_calls[]` → `ToolCallDto` (`id`, `function.name`, `function.arguments`)

### CompleteStreamingAsync

- Sends `stream: true`; a private `ReadChunksAsync` iterator parses upstream SSE data lines into chunks (stopping at `[DONE]`, skipping malformed lines)
- Yields `AssistantContentDelta` from `choices[0].delta.content`
- Accumulates `tool_calls` deltas by index, then yields `AssistantCompletionFinished` with the assembled completion

API keys are resolved from `OpenAiCompatible:ApiKey` first, then the configured environment variable (`OpenAiApiKeyResolver`). Keys are not logged. The API key is read once at startup when `OpenAiCompatible` is selected — restart the API after changing environment variables.

## ToolExecutor

**File:** `src/JeffyGPT/JeffyGPT.Infrastructure/Tools/ToolExecutor.cs`  
**Implements:** `IToolExecutor`

Dispatches tool calls to `McpProxiedToolHandler` and other `IToolHandler` instances from `McpToolHandlerRegistry` by name (case-insensitive). At construction, compiles each handler's `ParametersJsonSchema` via JsonSchema.Net.

Execution order for a known tool:

1. Evaluate `IToolExecutionPolicy` against the handler's `Security` descriptor
2. Validate `toolCall.ArgumentsJson` through `IToolArgumentValidator`
3. Call the handler's `ExecuteAsync` (MCP handlers delegate to `tools/call` on the configured server)

Unknown tools skip policy and validation. Policy blocks and invalid arguments return safe error results without throwing.

## JsonSchemaToolArgumentValidator

**File:** `src/JeffyGPT/JeffyGPT.Infrastructure/Tools/JsonSchemaToolArgumentValidator.cs`  
**Implements:** `IToolArgumentValidator`

Uses JsonSchema.Net to evaluate arguments against the handler's compiled schema. Used by `ToolExecutor` before every handler invocation.

## Future work

- Hot-reload MCP server list without API restart
- OAuth for remote HTTP MCP servers
- MCP resources and prompts (tools only today)
