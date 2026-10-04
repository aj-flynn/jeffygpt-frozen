# Overview

JeffyGPT is a self-hosted LLM tool-calling application. It provides a browser-based chat interface backed by an ASP.NET Core API that orchestrates language-model completions and controlled tool execution via **MCP** (Model Context Protocol).

JeffyGPT is a chat application with read-only document access via tools hosted in **JeffyMCP**. It does not expose document-write or ingestion APIs.

## Product goals

- Chat with a local or hosted language model through a web UI
- Keep tool definitions and execution on MCP servers (not in the browser or LLM directly)
- Return structured tool results via the sync API; the web UI shows Hunt / Hunted tool activity in the transcript during streaming
- Remain client-neutral so future mobile or other clients can share the same API
- Expose configured document roots as read-only MCP tools (`list-document-roots`, `list-files`, `read-file`) gated by JeffyGPT client policy + JeffyMCP sandbox

## Current capabilities

| Area | Status |
|------|--------|
| Chat UI | Working — den shell, GFM markdown, Hunt composer, live streaming bubble, Hunt/Hunted tool rows, Jeffy system prompt |
| `POST /api/chat` | Working — sync JSON response (fake echo assistant or OpenAI-compatible provider) |
| `POST /api/chat/stream` | Working — NDJSON stream for web UI |
| Tool calling (fake) | Working — `get-current-time` when user message contains `"time"`; `read-file` when message contains `"read docs overview"` (requires those tools to be allowed by `jeffymcp` policy) |
| MCP client | Working — multi-server config (`JeffyGPT:Mcp`); stdio + HTTP transports |
| JeffyMCP server | Working — standalone MCP server (stdio/HTTP) with four built-in tools |
| Aspire AppHost | Working — `JeffyAspire.AppHost` orchestrates MCP HTTP + Api + Vite (HTTP MCP for demo traces; stdio outside Aspire) |
| Tool execution policy | Working — `DefaultToolExecutionPolicy` for mutating/confirmation; MCP tools gated per server by `Policy.AllowAllTools` or `Policy.AllowedTools` |
| OpenAI-compatible LLM | Working — `OpenAiCompatibleAssistantClient` via `JeffyGPT:Assistant:Provider` |
| Health check | Working — `GET /health` |
| Light/dark themes | Working — parchment den (light) and soil den (dark) |
| Authentication | Not implemented — deferred until non-loopback deployment; local hardening uses per-server MCP tool policy (`AllowAllTools` / `AllowedTools`), chat role/size validation, and JeffyMCP loopback HTTP binds |
| MCP HTTP client auth headers | Not implemented — deferred with API/MCP auth |
| Database / persistence | Not implemented |

## Technology stack

### Backend

- **.NET 10** — ASP.NET Core minimal APIs
- **Model Context Protocol** — official C# SDK (`ModelContextProtocol`) for JeffyMCP + JeffyGPT MCP client
- **xUnit** — automated tests
- **Layered projects** — Api, Application, Contracts, Infrastructure + JeffyMCP
- **.NET Aspire 13** — AppHost orchestration + ServiceDefaults OpenTelemetry

### Frontend

- **React 19** + **TypeScript**
- **Vite 8** — dev server and production build
- **react-markdown** + **remark-gfm** — message markdown rendering
- **CSS custom properties** — theme tokens (no CSS framework)

## Repository layout

```
JeffyGPT/
├── src/
│   ├── JeffyAspire/
│   │   ├── JeffyAspire.AppHost/         # Aspire orchestration
│   │   └── JeffyAspire.ServiceDefaults/ # OTel, /alive, service discovery
│   ├── JeffyGPT/
│   │   ├── JeffyGPT.Api/                # HTTP API host (+ MCP bootstrap)
│   │   ├── JeffyGPT.Application/        # Business orchestration
│   │   ├── JeffyGPT.Contracts/          # Shared DTOs
│   │   ├── JeffyGPT.Infrastructure/     # LLM clients + MCP client
│   │   └── JeffyGPT.Web/                # React frontend (npm project)
│   └── JeffyMCP/                        # Standalone MCP tool server
├── tests/
│   ├── JeffyGPT/
│   │   ├── JeffyGPT.Api.Tests/
│   │   └── JeffyGPT.Application.Tests/
│   └── JeffyMCP/
│       └── JeffyMCP.Tests/
├── prototypes/                          # Exploratory UI looks; not shipped
├── JeffyGPT.slnx
├── .roots/docs/
└── README.md
```

## Design principles

- Tool calls are backend-owned; the LLM requests tools but does not execute them
- Tool **implementations** live in MCP servers (JeffyMCP by default); JeffyGPT proxies via `McpProxiedToolHandler`
- `DefaultToolExecutionPolicy` blocks in-process handlers that declare mutating or confirmation. Allowed MCP tools skip that check.
- MCP tools are gated per server by `Policy.AllowAllTools` or `Policy.AllowedTools`. JeffyMCP filesystem tools are also sandboxed in `FileSystemSandbox`.
- See [JeffyMCP](./jeffymcp.md) and [Architecture](./architecture.md)
