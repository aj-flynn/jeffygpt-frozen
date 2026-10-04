# JeffyGPT

A self-hosted LLM tool-calling application.

JeffyGPT is a chat application with read-only document access via configured filesystem tools. It does not expose document-write or ingestion APIs.

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) 10.0 or later
- [Node.js](https://nodejs.org/) 20 or later (includes npm)

## Run with Aspire (recommended)

From the repository root:

```bash
dotnet run --project src/JeffyAspire/JeffyAspire.AppHost --launch-profile http
```

Starts JeffyMCP (HTTP), the API, and the Vite frontend. F5 Aspire debug configs open the dashboard in your browser (anonymous in Development). The chat UI is **http://localhost:5173**.

## Run the backend (without Aspire)

From the repository root:

```bash
dotnet run --project src/JeffyGPT/JeffyGPT.Api
```

The API listens on `http://localhost:5008` by default and spawns JeffyMCP via stdio.

Health check:

```bash
curl http://localhost:5008/health
```

Expected response:

```json
{ "status": "ok" }
```

## Run the frontend (without Aspire)

From the repository root:

```bash
cd src/JeffyGPT/JeffyGPT.Web
npm install
npm run dev
```

Open `http://localhost:5173`. The Vite dev server proxies `/api` and `/health` to the backend. The chat UI streams responses via `POST /api/chat/stream`.

## Build and test

From the repository root:

```bash
dotnet build JeffyGPT.slnx
dotnet test
```

## Project structure

```
JeffyGPT/
├── src/
│   ├── JeffyAspire/
│   │   ├── JeffyAspire.AppHost/         # Aspire orchestration
│   │   └── JeffyAspire.ServiceDefaults/ # OTel, /alive, service discovery
│   ├── JeffyGPT/
│   │   ├── JeffyGPT.Api/                # ASP.NET Core Web API
│   │   ├── JeffyGPT.Application/
│   │   ├── JeffyGPT.Contracts/
│   │   ├── JeffyGPT.Infrastructure/
│   │   └── JeffyGPT.Web/                # React + Vite (npm project, not a .csproj)
│   └── JeffyMCP/                        # Standalone MCP tool server
├── tests/
│   ├── JeffyGPT/
│   │   ├── JeffyGPT.Api.Tests/
│   │   └── JeffyGPT.Application.Tests/
│   └── JeffyMCP/
│       └── JeffyMCP.Tests/
├── prototypes/                          # Exploratory UI looks; not shipped
├── .roots/docs/                         # Agent and developer markdown
├── .userdocs/
└── JeffyGPT.slnx
```

`JeffyGPT.Web` is listed in `JeffyGPT.slnx` as a solution folder (`package.json`) so it shows in IDEs. It is npm-owned and is not a `.csproj`.

User guide: open [`.userdocs/user-guide.html`](.userdocs/user-guide.html) in a browser. Technical reference: [`.userdocs/technical.html`](.userdocs/technical.html).

Detailed documentation: [`.roots/docs/README.md`](.roots/docs/README.md)

## Project references

- **JeffyGPT.Api** → Application, Contracts, Infrastructure
- **JeffyGPT.Application** → Contracts
- **JeffyGPT.Infrastructure** → Application, Contracts
