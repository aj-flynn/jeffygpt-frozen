# Architecture

JeffyGPT follows a layered architecture common in .NET applications, with a clear seam for swapping assistant and tool implementations.

## Layer diagram

```
┌─────────────────────────────────────────────────────────┐
│  JeffyGPT.Web (React)                                   │
│  Browser UI — calls /api/chat/stream, /health          │
└──────────────────────────┬──────────────────────────────┘
                           │ HTTP (JSON / NDJSON)
┌──────────────────────────▼──────────────────────────────┐
│  JeffyGPT.Api                                           │
│  Endpoints, CORS, DI, MCP bootstrap                     │
└──────────────────────────┬──────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────┐
│  JeffyGPT.Application                                    │
│  ChatOrchestrator, validation, abstractions             │
└────────────┬─────────────────────────────┬──────────────┘
             │                             │
┌────────────▼────────────┐   ┌────────────▼──────────────┐
│  JeffyGPT.Contracts     │   │  JeffyGPT.Infrastructure  │
│  DTOs (no logic)        │   │  Chat/ (LLM clients),      │
│                         │   │  Mcp/ (MCP client),        │
│                         │   │  Tools/ (executor, policy) │
└─────────────────────────┘   └────────────┬──────────────┘
                                           │ MCP stdio/HTTP
                              ┌────────────▼──────────────┐
                              │  JeffyMCP (MCP server)     │
                              │  Tools + FileSystem sandbox│
                              └────────────────────────────┘
```

## Projects and responsibilities

### JeffyAspire.AppHost / ServiceDefaults

**Paths:** `src/JeffyAspire/JeffyAspire.AppHost/`, `src/JeffyAspire/JeffyAspire.ServiceDefaults/`

Local orchestration and shared observability:

- AppHost composition is in `AppHost.cs`; resource wiring and assistant run-settings live under `Hosting/`
- Development disables Aspire dashboard browser-token auth (`ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS`); Visual Studio applies AppHost `launchSettings.json`; Cursor/VS Code JeffyAspire configs copy that profile `env` onto the `coreclr` launch (including `DOTNET_ENVIRONMENT`) and open the dashboard via `serverReadyAction`
- AppHost runs JeffyMCP (`--http`), JeffyGPT.Api (MCP endpoint injected), and JeffyGPT.Web (`AddViteApp`)
- ServiceDefaults wires OpenTelemetry (OTLP when configured), service discovery, and `/alive` (Api keeps its custom JSON `/health`)

### JeffyGPT.Api

**Path:** `src/JeffyGPT/JeffyGPT.Api/`

HTTP host. Responsibilities:

- Map minimal API endpoints (`/health`, `/api/chat`, `/api/chat/stream`) plus Aspire `/alive` in Development
- Configure CORS (Vite origins only) and chat rate limiting
- Register Application and Infrastructure services via DI extension methods
- Bootstrap MCP at startup (stdio by default; Aspire injects HTTP transport for telemetry demos)
- Translate validation exceptions to HTTP 400 responses

Does **not** contain fake LLM or tool logic. Does not expose document-write or ingestion APIs.

### JeffyGPT.Application

**Path:** `src/JeffyGPT/JeffyGPT.Application/`

Business orchestration. Responsibilities:

- Define interfaces for chat, assistant, tools
- `ChatOrchestrator` — validates new user text, keeps the in-memory transcript, and coordinates the assistant/tool loop

Does **not** reference HTTP, databases, or specific LLM SDKs.

### JeffyGPT.Contracts

**Path:** `src/JeffyGPT/JeffyGPT.Contracts/`

Shared data transfer objects. Responsibilities:

- Request/response records for chat and health endpoints (including `ChatStreamEvent` for NDJSON streaming)
- Types used across Api, Application, and Infrastructure

No dependencies on other JeffyGPT projects. No business logic.

### JeffyMCP

**Path:** `src/JeffyMCP/`

Standalone MCP server (stdio + HTTP) hosting built-in tools. Configured in JeffyGPT via `JeffyGPT:Mcp:Servers` and usable by other MCP clients independently.

### JeffyGPT.Infrastructure

**Path:** `src/JeffyGPT/JeffyGPT.Infrastructure/`

External integrations and development fakes. Responsibilities:

- **Mcp/** — `McpConnectionManager`, `McpProxiedToolHandler`, `McpToolBootstrapper`, `McpToolCatalog`
- **Tools/** — `ToolExecutor`, `DefaultToolExecutionPolicy`, `JsonSchemaToolArgumentValidator`

References Application (for interfaces) and Contracts (for DTOs).

### JeffyGPT.Web

**Path:** `src/JeffyGPT/JeffyGPT.Web/`

React single-page application. Responsibilities:

- Chat UI, branding, theme switching
- API client layer (`src/api/`) — `ChatPanel` uses `streamChat` only; `postChat` remains for sync/non-UI clients
- Full-width transcript with **2rem** side gutters; composer stays a centered **40rem** column
- Proxies API calls to the backend during development (Vite)

Standalone npm project. `JeffyGPT.slnx` lists it as a solution folder (`package.json`) so it appears in IDEs; `dotnet build` does not compile it.

## Naming glossary

Product, namespaces, and identifiers use different casing on purpose. Do not invent a fourth spelling.

| Kind | Form | Example |
|------|------|---------|
| UI copy | `JeffyGPT`, `JeffyMCP` | page title, wordmark |
| C# namespaces and project names | `JeffyGPT`, `JeffyMCP` | `namespace JeffyGPT.Api.Endpoints` |
| C# identifiers | PascalCase acronyms as words | `JeffyMcpHttpBindRules`, `OpenAiCompatibleAssistantClient`, `AddJeffyMcp` |
| TS/React components | React PascalCase | `JeffyGptLogo` (do not rename to `JeffyGPTLogo`) |
| CSS / storage keys | short prefix | `--rust`, `jeffygpt-theme` |

Do not introduce `JeffyMCPHttp…` identifiers. Keep the existing `JeffyMcp` / `JeffyGpt` / `jgpt-` forms.

## Shared MSBuild properties

Repo-root `Directory.Build.props` sets `TargetFramework` (`net10.0`), `Nullable`, and `ImplicitUsings` for every `.csproj`. Project files only declare what they add (SDK, references, `UserSecretsId`, `IsPackable`, and so on).

## Project references

```
JeffyGPT.Contracts          (no project references)

JeffyGPT.Application    →   Contracts
JeffyGPT.Infrastructure →   Application, Contracts
JeffyGPT.Api            →   Application, Contracts, Infrastructure, ServiceDefaults
                         +   JeffyMCP (build order only; ReferenceOutputAssembly=false)

JeffyMCP                →   ServiceDefaults
JeffyAspire.AppHost     →   Api, JeffyMCP

JeffyGPT.Api.Tests      →   Api, Application.Tests
JeffyGPT.Application.Tests → Application, Infrastructure
JeffyMCP.Tests          →   JeffyMCP
```

## Dependency injection

Registration is split across extension methods:

| Extension | Project | Registrations |
|-----------|---------|---------------|
| `AddApplication()` | Application | `IChatOrchestrator` → `ChatOrchestrator` |
| `AddInfrastructure()` | Infrastructure | `IAssistantClient` (provider-selected); calls `AddMcpTools()` for MCP client + tool executor |

`Program.cs` calls `IMcpToolBootstrapper.InitializeAsync()` before the host runs or resolves the tool executor. The registry accepts one initialization, including an empty list. Reading it before initialization or replacing it afterward throws a clear error.

`IChatOrchestrator`, `IToolCatalog`, and `IToolExecutor` are registered as **singletons**. `IAssistantClient` depends on `JeffyGPT:Assistant:Provider`:

| Provider | Registration |
|----------|--------------|
| `Fake` | Singleton `FakeAssistantClient` |
| `OpenAiCompatible` | Typed `HttpClient` via `AddHttpClient<IAssistantClient, OpenAiCompatibleAssistantClient>` |

## Extension points (future work)

| Interface | Current implementations | Future work |
|-----------|-------------------------|-------------|
| `IAssistantClient` | `FakeAssistantClient`, `OpenAiCompatibleAssistantClient` | Additional providers |
| `IToolCatalog` | `McpToolCatalog` (MCP-discovered tools) | Additional MCP servers via config |
| `IToolExecutor` | `ToolExecutor` | Additional policy providers |

Swap assistant implementations in `Infrastructure/DependencyInjection.cs`. Add MCP servers in `JeffyGPT:Mcp:Servers`. Add tools to JeffyMCP (or any standards-compliant MCP server).
