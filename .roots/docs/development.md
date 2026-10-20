# Development Guide

How to set up, run, build, test, and debug JeffyGPT locally.

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | Backend build and run |
| [Node.js](https://nodejs.org/) | 20+ | Frontend (includes npm) |

Optional for debugging:

- **C# Dev Kit** extension in Cursor/VS Code (recommended in `.vscode/extensions.json`)

Verify installations:

```bash
dotnet --version
node --version
npm --version
```

## Clone and first-time setup

```bash
git clone <repository-url>
cd JeffyGPT

# Backend — restore happens on first build
dotnet build JeffyGPT.slnx

# Frontend
cd src/JeffyGPT/JeffyGPT.Web
npm install
```

## Running locally

### Preferred: Aspire AppHost

Orchestrates JeffyMCP (HTTP), JeffyGPT.Api, and the Vite frontend with the Aspire dashboard and OpenTelemetry. `JeffyAspire.AppHost` is the solution **default startup** project (`DefaultStartup="true"` in `JeffyGPT.slnx`), so Visual Studio / C# Dev Kit F5 or Run targets AppHost (Api + UI + JeffyMCP) rather than Api alone.

```bash
dotnet run --project src/JeffyAspire/JeffyAspire.AppHost --launch-profile http
```

The AppHost project references `Aspire.Hosting.AppHost` (same version as `Aspire.AppHost.Sdk`) plus `ProjectReference`s to Api and JeffyMCP. Those references generate `Projects.*` metadata types used by `AddProject<Projects....>` in `Hosting/JeffyAspireResources.cs` — restore/build the AppHost if the IDE reports `Projects` as missing. `AppHost.cs` is the composition script (`AddJeffyMcp` / `AddJeffyApi` / `AddJeffyWeb`); launch-profile and `.run-assistant-provider` resolution lives in `Hosting/AssistantRunSettings.cs`. In Development, AppHost sets `ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true` (also in launch profiles) so the dashboard skips browser-token login. Visual Studio F5 applies AppHost `Properties/launchSettings.json` (including `DOTNET_ENVIRONMENT=Development`). Cursor/VS Code JeffyAspire configs launch the AppHost DLL via `coreclr` and set that same profile `env` explicitly — `launchSettingsProfile` alone does not apply generic-host variables the way Visual Studio does. They open **http://localhost:15073** via `serverReadyAction` when AppHost logs `Now listening on:`.

The default / recommended local profile is `http` (`ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` for the HTTP dashboard). The `https` profile is optional when you have a trusted HTTPS dashboard setup. Use `http-openai` (or the VS Code **JeffyAspire (OpenAI Compatible)** launch) for OpenAI mode.

Under Aspire:

- JeffyMCP runs with `--http` (Streamable HTTP; no auth — loopback / non-external only) so the Aspire dashboard can show Api → JeffyMCP request traces
- The AppHost overrides `JeffyGPT:Mcp:Servers:0` to `Transport=Http` and injects the MCP endpoint URL
- **Development CORS:** Vite origins only (`http://localhost:5173`, `http://127.0.0.1:5173`)
- Chat endpoints are rate-limited (`chat` policy, 60/min)
- `OPENAI_API_KEY` is forwarded via an Aspire secret parameter (masked in the dashboard) when set in the AppHost environment or `Parameters:openai-api-key`
- Traces/metrics/logs from Api and JeffyMCP export to the dashboard when `OTEL_EXPORTER_OTLP_ENDPOINT` is set by Aspire

Committed API `appsettings.json` remains **stdio** so non-Aspire workflows keep working.

### Backend API (without Aspire)

From repository root:

```bash
dotnet run --project src/JeffyGPT/JeffyGPT.Api
```

Default URL: **http://localhost:5008**. The API spawns JeffyMCP via **stdio** from `JeffyGPT:Mcp`.

Verify:

```bash
curl http://localhost:5008/health
# {"status":"ok"}
```

### Frontend (without Aspire)

In a separate terminal:

```bash
cd src/JeffyGPT/JeffyGPT.Web
npm run dev
```

Default URL: **http://localhost:5173**

Vite proxies `/api` and `/health` to `http://localhost:5008` (or Aspire-injected `API_HTTP` / `services__api__http__0` when launched from the AppHost).

### Typical workflow (without Aspire)

1. Start the API (`dotnet run --project src/JeffyGPT/JeffyGPT.Api`)
2. Start the frontend (`npm run dev` in `JeffyGPT.Web`)
3. Open http://localhost:5173
4. Send chat messages; with `Provider: Fake`, try `"what time is it"` to see the Hunt box, tools appear and leave, then a **Hunted** label and a streamed time response

**Note:** Only one API instance should bind to port 5008 when not using Aspire. Stop any existing `dotnet run` before starting a debug session (F5).

## Building

### Backend

```bash
dotnet build JeffyGPT.slnx
```

Or from VS Code: **Ctrl+Shift+B** (default build task).

### Frontend

```bash
cd src/JeffyGPT/JeffyGPT.Web
npm run build
```

Output: `src/JeffyGPT/JeffyGPT.Web/dist/`

### Preview production frontend build

```bash
cd src/JeffyGPT/JeffyGPT.Web
npm run preview
```

## UI look prototypes

Standalone chat-shell mockups live in `prototypes/jeffygpt-looks/`. They are not part of the Vite or Aspire build. From the repository root:

```bash
python -m http.server 8765
```

Then visit http://localhost:8765/prototypes/jeffygpt-looks/. The gallery is look 07 (Crawlspace Mouser) plus five small variants of that den. Left and Right arrows move through the six pages. See [Branding & Themes](./branding-and-themes.md#exploratory-look-prototypes).

## Testing

```bash
dotnet test
```

Frontend unit tests (Vitest):

```bash
cd src/JeffyGPT/JeffyGPT.Web
npm test
```

CRAP score (complexity and coverage, threshold 30) runs in GitHub Actions on pull requests. The check is not required, so a red result does not block merging. Local commands and the formula are in [Testing](./testing.md#crap-score).

Frontend lint:

```bash
cd src/JeffyGPT/JeffyGPT.Web
npm run lint
```

## Formatting

Shared style lives in the repo-root `.editorconfig` (indent, charset, trailing whitespace, C# `dotnet_sort_system_directives_first`, TypeScript 2-space). Shared MSBuild defaults (`TargetFramework`, `Nullable`, `ImplicitUsings`) live in `Directory.Build.props` so individual `.csproj` files do not repeat them.

C# uses the SDK `dotnet format` command against the solution. Frontend files and `.userdocs/user-guide.json` use Prettier (`npm run format` / `npm run format:check` in `JeffyGPT.Web`). The generated `user-guide.html` is excluded.

```bash
dotnet format JeffyGPT.slnx
dotnet format JeffyGPT.slnx --verify-no-changes

cd src/JeffyGPT/JeffyGPT.Web
npm run format
npm run format:check
```

`format` writes changes; `format:check` and `dotnet format --verify-no-changes` report drift without writing. Existing files are not required to match until a later format pass.


## Debugging

### VS Code / Cursor (Aspire — preferred)

1. Install **C# Dev Kit** and the **Aspire** extension (`microsoft-aspire.aspire-vscode`)
2. Set breakpoints in Api / JeffyMCP / Application code as needed
3. Open **Run and Debug** (Ctrl+Shift+D)
4. Select **JeffyAspire (Fake)** or **JeffyAspire (OpenAI Compatible)** and press **F5**

F5 **JeffyAspire (Fake)** or **JeffyAspire (OpenAI Compatible)** launches `JeffyAspire.AppHost` via `coreclr` with the matching launch-profile environment copied into `launch.json` (`DOTNET_ENVIRONMENT=Development`, dashboard URLs, unsecured anonymous). Cursor opens the dashboard with `serverReadyAction` when AppHost prints `Now listening on: http://localhost:15073` (launchSettings `launchBrowser` alone is not reliable in Cursor). Development disables dashboard login (`ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS`). Vite may also open **http://localhost:5173**.

Aspire AppHost is a generic host: `builder.Environment.IsDevelopment()` follows **`DOTNET_ENVIRONMENT`**, not Debug vs Release. Visual Studio injects that from `launchSettings.json`; a Cursor `coreclr` DLL launch does not unless `env` is set. Without it the host stays Production and the Development `if` in `AppHost.cs` is skipped.

| Launch config | What it does |
|---------------|--------------|
| **JeffyAspire (Fake)** | AppHost `http` profile; Fake assistant via `.run-assistant-provider` marker |
| **JeffyAspire (OpenAI Compatible)** | AppHost `http-openai` profile; OpenAI via marker / `Provider=OpenAiCompatible` |

For **OpenAI Compatible**, set `OPENAI_API_KEY` in your user/system environment (or AppHost / Api user-secrets under `Parameters:openai-api-key`) before F5. The AppHost forwards the key into the Api resource as an Aspire **secret** parameter when present (masked in the dashboard). A missing key no longer blocks Api/web startup; the first chat request fails with `AssistantClientException` (sync `502` or stream `error` event) until the key is set.

Look for `[JeffyAspire] Api launch profile 'http-openai', assistant provider 'OpenAiCompatible'.` in the AppHost console when OpenAI mode starts. The Aspire dashboard should open at **http://localhost:15073**; the chat UI is **http://localhost:5173**.

### VS Code / Cursor (API only, without Aspire)

| Launch config | Assistant provider | API key required |
|---------------|-------------------|------------------|
| **JeffyGPT.Api (Fake, without Aspire)** | `FakeAssistantClient` | No |
| **JeffyGPT.Api (OpenAI Compatible, without Aspire)** | `OpenAiCompatibleAssistantClient` | Yes (unless `RequireApiKey: false`) |

These use `http://localhost:5008` and stdio MCP. Pre-launch task: `build-api`. Cursor `coreclr` launches set `DOTNET_ENVIRONMENT` and `ASPNETCORE_ENVIRONMENT` to `Development` explicitly (same as the JeffyAspire configs). The OpenAI config also sets `DOTNET_LAUNCH_PROFILE=http-openai` and `JeffyGPT__Assistant__Provider`.

### Trigger breakpoints

Send a chat message from the UI (the UI uses `POST /api/chat/stream`), or exercise the sync endpoint:

```bash
curl.exe -X POST http://localhost:5008/api/chat ^
  -H "Content-Type: application/json" ^
  -d "{\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}]}"
```

To inspect the streaming endpoint directly:

```bash
curl.exe -N -X POST http://localhost:5008/api/chat/stream ^
  -H "Content-Type: application/json" ^
  -H "Accept: application/x-ndjson" ^
  -d "{\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}]}"
```

The `-N` flag disables curl output buffering so NDJSON lines appear as they are flushed.

### Debug API tests

Launch config **JeffyGPT.Api.Tests** runs tests under the debugger. Set breakpoints in test or application code first.

## Project configuration reference

| File | Purpose |
|------|---------|
| `JeffyGPT.slnx` | .NET solution (JeffyGPT backend + JeffyMCP + JeffyAspire + test projects). `JeffyGPT.Web` is a solution folder (`package.json`), not a `.csproj`. AppHost is `DefaultStartup` |
| `Directory.Build.props` | Shared `TargetFramework` (`net10.0`), `Nullable`, and `ImplicitUsings` for every `.csproj` |
| `.editorconfig` | Shared editor/format defaults for C#, TypeScript, HTML, and CSS |
| `src/JeffyGPT/JeffyGPT.Web/.prettierrc.json` | Prettier options for the Vite app and `.userdocs` |
| `src/JeffyAspire/JeffyAspire.AppHost/` | Aspire orchestration (MCP HTTP + Api + Vite); composition in `AppHost.cs`, helpers in `Hosting/` |
| `src/JeffyAspire/JeffyAspire.ServiceDefaults/` | Shared OpenTelemetry, health `/alive`, service discovery |
| `.gitignore` | Root ignore rules for .NET build output, frontend `node_modules`/`dist`, env files, and local secrets |
| `src/JeffyGPT/JeffyGPT.Api/appsettings.json` | Base logging plus `JeffyGPT:Mcp` (spawns JeffyMCP via stdio by default) |
| `src/JeffyGPT/JeffyGPT.Api/appsettings.Development.json` | Local `JeffyGPT:Assistant` provider config (`Fake` or `OpenAiCompatible`) |
| `src/JeffyGPT/JeffyGPT.Api/Properties/launchSettings.json` | JeffyGPT API launch URLs and `openai-compatible` profile |
| `src/JeffyGPT/JeffyGPT.Web/vite.config.ts` | Dev server proxy to :5008 |
| `.vscode/launch.json` | Debug configurations |
| `.vscode/tasks.json` | Build and test tasks |
| `.vscode/settings.json` | `dotnet.defaultSolution: JeffyGPT.slnx` |

## Testing with OpenAI (manual)

By default, `appsettings.Development.json` uses `Provider: Fake` so the app works without an API key. The `OpenAiCompatible` block stays in the file as a ready-to-use template.

### Switch provider without editing files (recommended)

**PowerShell (session only)**

```powershell
$env:JeffyGPT__Assistant__Provider = "OpenAiCompatible"
$env:OPENAI_API_KEY = "your-api-key-here"
dotnet run --project src/JeffyGPT/JeffyGPT.Api
```

**bash**

```bash
export JeffyGPT__Assistant__Provider=OpenAiCompatible
export OPENAI_API_KEY="your-api-key-here"
dotnet run --project src/JeffyGPT/JeffyGPT.Api
```

Alternatively, use the `openai-compatible` launch profile in `launchSettings.json` (sets the provider env var; key still from env or user secrets).

### User secrets (optional)

The API project has `UserSecretsId` configured. In Development, `dotnet user-secrets` overrides `appsettings`:

```bash
dotnet user-secrets set "JeffyGPT:Assistant:Provider" "OpenAiCompatible" --project src/JeffyGPT/JeffyGPT.Api
dotnet user-secrets set "JeffyGPT:Assistant:OpenAiCompatible:ApiKey" "sk-..." --project src/JeffyGPT/JeffyGPT.Api
```

Never commit real keys.

### Edit appsettings.Development.json

Set `"Provider": "OpenAiCompatible"` and configure the `OpenAiCompatible` block:

```json
"JeffyGPT": {
  "Assistant": {
    "Provider": "OpenAiCompatible",
    "OpenAiCompatible": {
      "BaseUrl": "https://api.openai.com/v1",
      "ApiKey": "",
      "ApiKeyEnvironmentVariable": "OPENAI_API_KEY",
      "Model": "gpt-5.4-nano",
      "ReasoningEffort": "none",
      "RequireApiKey": true,
      "TimeoutSeconds": 120
    }
  }
}
```

Leave `ApiKey` empty when using the environment variable.

For **GPT-5.6** models (`gpt-5.6-luna`, `gpt-5.6-terra`, `gpt-5.6-sol`), set `ReasoningEffort` to `none` when using JeffyGPT’s chat/completions + tools path. OpenAI rejects function tools with default reasoning on `/v1/chat/completions` (400). Omit `ReasoningEffort` for providers that do not accept the field.

### MCP and tool configuration

JeffyGPT connects to JeffyMCP at startup (**stdio by default** outside Aspire). Under Aspire, the AppHost forces HTTP and injects the endpoint. Filesystem sandbox paths live in `src/JeffyMCP/appsettings.json` under `JeffyMCP:Tools`.

Run JeffyMCP standalone:

```bash
dotnet run --project src/JeffyMCP/JeffyMCP.csproj
dotnet run --project src/JeffyMCP/JeffyMCP.csproj -- --http
# or launch profile "http" → http://localhost:3001
```

Each MCP server is gated by `Policy.AllowAllTools` or a required `Policy.AllowedTools` list. The committed API `appsettings.json` sets `AllowAllTools: true` for `jeffymcp`.

```json
"Mcp": {
  "Servers": [
    {
      "Id": "jeffymcp",
      "Transport": "Stdio",
      "Command": "dotnet",
      "Arguments": ["../../JeffyMCP/bin/Debug/net10.0/JeffyMCP.dll"],
      "WorkingDirectory": "../../JeffyMCP",
      "Policy": { "AllowAllTools": true }
    }
  ]
}
```

See [Infrastructure](./backend-infrastructure.md) for MCP client and JeffyMCP server details.

**Document discovery workflow:** When `jeffymcp` allows those tools, the LLM can call `list-document-roots`, then `read-file` starting at the configured `entryPoint` (e.g. `docs` / `README.md`).

### Startup confirmation

After restart, the API logs the active provider at Information level. For `OpenAiCompatible`, it logs `BaseUrl`, `Model`, and `RequireApiKey` (never the API key).

### Manual test checklist

1. **Fake (default):** echo works; `"what time is it"` triggers fake time tool flow
2. **Fake + filesystem demo:** with `jeffymcp` `Policy.AllowAllTools: true`, send `"read docs overview"` → fake provider calls `read-file` with `rootAlias: "docs"` and `relativePath: "overview.md"` (JeffyMCP resolves via `JeffyMCP:Tools` `docs.Path` `../../.roots/docs` when `WorkingDirectory` is the JeffyMCP project folder).
3. **OpenAI:** provider + key set → startup log shows `OpenAiCompatible` → ask *"What time is it?"* → model may call `get-current-time` → streamed answer
4. **Follow-up:** second message after tool use sends full history (see frontend docs)
5. **Bad key:** UI shows an error message (sync `502` or stream `error` event), not an opaque `500`

### Troubleshooting

| Symptom | Likely cause |
|---------|----------------|
| API fails at startup with API key message | `Provider: OpenAiCompatible` but no key and `RequireApiKey: true` |
| `Required MCP server 'jeffymcp' failed to connect` | JeffyMCP DLL missing/wrong path, bad `WorkingDirectory`, or stdio handshake failed — run `dotnet build src/JeffyGPT/JeffyGPT.Api` then check `JeffyGPT:Mcp` |
| MCP entry point was not found | Build JeffyMCP; for Release change Arguments from `bin/Debug/...` |
| `502` / stream `error` with `401` | Invalid or missing API key |
| `502` / connection errors | Wrong `BaseUrl`, LLM server not running, or firewall |
| Model errors in message | Wrong `Model` name for your provider |
| `400` with GPT-5.6 + tools | Set `OpenAiCompatible:ReasoningEffort` to `none` (chat/completions rejects tools with default reasoning) |
| Filesystem tools missing from the catalog | Set `Servers[].Policy.AllowAllTools` to `true`, or list the tool in `Policy.AllowedTools` |
| `MCP server '…' policy is invalid` | Enabled server has `AllowAllTools: false` and an empty `AllowedTools` list |

## Testing with vLLM (manual)

Use the same `OpenAiCompatible` provider with a local OpenAI-compatible endpoint:

```json
"OpenAiCompatible": {
  "BaseUrl": "http://localhost:8000/v1",
  "Model": "your-served-model",
  "RequireApiKey": false,
  "TimeoutSeconds": 120
}
```

Set `JeffyGPT__Assistant__Provider=OpenAiCompatible` (env var or appsettings). No bearer token is sent when `RequireApiKey` is `false`.

Start vLLM (or your server) before the API. Confirm tool-calling support on your model — JeffyGPT executes tools via MCP (`JeffyMCP` by default).

## Solution vs frontend

`JeffyGPT.Web` is listed in `JeffyGPT.slnx` as a solution folder pointing at `package.json`. It is managed independently via npm. `dotnet build` does not build the React app.

## Agent instructions

Repository coding values are documented in [AGENTS.md](../../AGENTS.md).
