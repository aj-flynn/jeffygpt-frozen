# Testing

JeffyGPT uses xUnit for backend automated tests and Vitest for frontend predicate unit tests.

## Test projects

| Project | Path | References |
|---------|------|------------|
| `JeffyGPT.Api.Tests` | `tests/JeffyGPT/JeffyGPT.Api.Tests/` | Api, Application.Tests (shared doubles) |
| `JeffyGPT.Application.Tests` | `tests/JeffyGPT/JeffyGPT.Application.Tests/` | Application, Infrastructure |
| `JeffyMCP.Tests` | `tests/JeffyMCP/JeffyMCP.Tests/` | JeffyMCP |

## Running tests

From repository root:

```bash
dotnet test
# or
dotnet test JeffyGPT.slnx
```

### Frontend (Vitest)

From `src/JeffyGPT/JeffyGPT.Web`: `npm test`

### Frontend lint and format

From `src/JeffyGPT/JeffyGPT.Web`:

```bash
npm run lint
npm run format:check
```

`npm run format` writes Prettier changes. C# uses `dotnet format JeffyGPT.slnx` / `dotnet format JeffyGPT.slnx --verify-no-changes`. See [Development](./development.md#formatting).

## CRAP score

A method's Change Risk Anti-Patterns score is `complexity² × (1 − line coverage)³ + complexity`. Line coverage is 0 to 1. Cyclomatic complexity counts independent paths. The check fails when any scored method is 30 or higher. An untested method reaches 30 at complexity 5. A fully tested method reaches 30 only when its complexity is 30. Untested methods of complexity 1–4 stay under 30.

The check skips compiler-generated methods (names containing `<`), test projects, and the Aspire host. It scores `JeffyGPT.Api`, `JeffyGPT.Application`, `JeffyGPT.Infrastructure`, `JeffyGPT.Contracts`, `JeffyMCP`, and `JeffyGPT.Web` `src/**/*.{ts,tsx}` except `*.test.ts`.

.NET coverage comes from Coverlet Cobertura (`coverlet.runsettings`). Api tests and Application tests write separate reports, so [scripts/crap/CheckCobertura.cs](../../scripts/crap/CheckCobertura.cs) merges them by file and method signature before scoring. From the repository root:

```bash
dotnet test JeffyGPT.slnx --collect:"XPlat Code Coverage" --results-directory artifacts/coverage --settings coverlet.runsettings
dotnet run scripts/crap/CheckCobertura.cs -- --self-test
dotnet run scripts/crap/CheckCobertura.cs -- artifacts/coverage
```

TypeScript, from `src/JeffyGPT/JeffyGPT.Web`:

```bash
npm run crap
```

`npm run crap` runs Vitest with the v8 coverage provider, then [scripts/crap-score.mjs](../../src/JeffyGPT/JeffyGPT.Web/scripts/crap-score.mjs). A source file missing from the coverage report counts as 0% coverage.

[`.github/workflows/crap.yml`](../../.github/workflows/crap.yml) runs both checks on pull requests and on manual dispatch. A failing job is a red check on the pull request. It is not a required status check, so it does not block merging. A failed test run fails the job before scoring.

## Api integration tests

`JeffyGPTWebApplicationFactory` forces `JeffyGPT:Assistant:Provider=Fake`, disables real MCP servers (`JeffyGPT:Mcp:Servers:0:Enabled=false`), and registers `TestMcpToolBootstrapper` so CI does not spawn JeffyMCP.

## Application unit tests

**Path:** `tests/JeffyGPT/JeffyGPT.Application.Tests/`

```
Chat/
├── ChatOrchestratorTests.cs
├── ChatRequestValidatorTests.cs
├── ChatStreamEventSerializationTests.cs
└── JeffyPersonalityTests.cs
Infrastructure/
├── InfrastructureDependencyInjectionTests.cs
├── McpConnectionManagerTests.cs
├── McpJsonHelpersTests.cs
├── OpenAiCompatibleAssistantClientTests.cs
└── OpenAiCompatibleAssistantClientMappingTests.cs
Tools/
├── ToolExecutorTests.cs
├── ToolArgumentValidatorTests.cs
├── DefaultToolExecutionPolicyTests.cs
├── McpServerPolicyOptionsTests.cs
├── McpToolBootstrapperTests.cs
├── McpToolCatalogTests.cs
└── McpToolNameResolverTests.cs
TestDoubles/
├── StubAssistantClient.cs
├── EchoQueryToolHandler.cs
├── ConfigurableSecurityToolHandler.cs
├── GetCurrentTimeToolHandler.cs   # test double (not production handler)
├── FakeMcpConnectionManager.cs
└── TestMcpToolBootstrapper.cs
```

Test names use `Method_Scenario_Expected` (for example `SendAsync_EmptyMessages_ThrowsArgumentException`). Prefer that over type-name-only titles that bury the assertion.

### Notable MCP / tool tests

| Area | Asserts |
|------|---------|
| `McpToolCatalogTests` | Catalog reads from `McpToolHandlerRegistry` |
| `McpToolNameResolverTests` | Unprefixed names with one server; `{id}__{name}` with many; duplicate detection |
| `McpServerPolicyOptionsTests` | `AllowAllTools` vs required `AllowedTools`; invalid empty list |
| `McpToolBootstrapperTests` | Allow-all vs approved-list registration; invalid policy fails startup |
| `ToolExecutorTests` | Dispatch, validation errors, mutating/confirmation policy, MCP proxied dispatch via `FakeMcpConnectionManager` |
| `DefaultToolExecutionPolicyTests` | Mutating and confirmation-only |
| `Infrastructure/InfrastructureDependencyInjectionTests` | Fake provider + `TestMcpToolBootstrapper` registers catalog tools |
| `Chat/ChatOrchestratorTests` | Tool loops via `TestMcpToolBootstrapper` / stubs; system prompt prepended |
| `Chat/JeffyPersonalityTests` | Server-owned Jeffy system prompt is prepended to client history |
| `Chat/ChatStreamEventSerializationTests` | Exact NDJSON wire shape per `ChatStreamEvent` derived record (type discriminator + payload), plus polymorphic round-trip |

## JeffyMCP tests

**Path:** `tests/JeffyMCP/JeffyMCP.Tests/`

| Area | Coverage |
|------|----------|
| `FileSystem/FileSystemSandboxTests` | Path traversal, aliases, extensions, size, directories vs file |
| `FileSystem/FileSystemPathRulesTests` | Absolute-path helpers |
| `Tools/*ToolsTests` | `get-current-time`, `list-document-roots`, `read-file`, `list-files` behavior |

Tool implementation tests live here — not under JeffyGPT.Application.Tests.

### OpenAiCompatibleAssistantClientTests

Uses a test `HttpMessageHandler` (no live LLM server).

| Test | Asserts |
|------|---------|
| `CompleteAsync_SendsOpenAiCompatibleChatCompletionRequest` | POST `/chat/completions`, model, messages, tools, `tool_choice: auto`, bearer auth, content mapping |
| `CompleteAsync_WhenReasoningEffortConfigured_SendsReasoningEffort` | Configured `ReasoningEffort` is sent as `reasoning_effort` |
| `CompleteAsync_HttpError_ThrowsAssistantClientExceptionWithStatus` | HTTP 401 → `AssistantClientException` with status; no response body in message |
| `CompleteAsync_MapsReturnedToolCalls` | Tool call id, name, and arguments mapping |
| `CompleteAsync_SendsAssistantToolCallsAndToolResultsInFollowUpRequest` | Multi-turn request maps assistant tool calls and tool result messages |
| `CompleteStreamingAsync_SendsStreamTrueAndMapsContentDeltas` | Streaming request sets `stream: true` and maps content deltas |
| `CompleteStreamingAsync_AssemblesStreamingToolCalls` | Streaming SSE assembles tool call id, name, and arguments |

### OpenAiCompatibleAssistantClientMappingTests

**File:** `tests/JeffyGPT/JeffyGPT.Application.Tests/Infrastructure/OpenAiCompatibleAssistantClientMappingTests.cs`

| Test | Asserts |
|------|---------|
| `ShouldOmitContentForToolCalls_*` | OpenAI outbound content omitted only for tool-call rounds with empty content |
| `MapOutboundContent_*` | Content preserved when non-empty |
| `TryParseSseDataLine_*` | SSE line classification (`Empty`, `NonData`, `Data`, `Done`) |
| `TryGetFirstChoice_*` | First-choice extraction from response lists |

## Frontend unit tests (Vitest)

**Path:** `src/JeffyGPT/JeffyGPT.Web/src/`

| File | Asserts |
|------|---------|
| `api/chatHistory.test.ts` | Transcript visibility predicates |
| `api/chatStreamParse.test.ts` | One NDJSON line becomes a typed event, or is rejected |
| `api/chatClient.test.ts` | `streamChat` NDJSON reads, with `fetch` stubbed |
| `api/chatStreamState.test.ts` | Stream reducer: deltas, Hunt episodes, running tools leave on finish, Hunted after commit, error latch |
| `hooks/useChatStream.test.ts` | Sending a message, surfacing errors, and cancelling an in-flight send |
| `api/chatActivity.test.ts` | Hunt episode helpers and tool-name labels |
| `api/idleHuntStatus.test.ts` | Idle chrome status picker |
| `api/chatComposer.test.ts` | Send eligibility |
| `api/chatErrors.test.ts` | Abort detection and user-facing error text |
| `api/chatDisplay.test.ts` | Empty-state gate and Upstairs/Jeffy labels |
| `theme/theme.test.ts` | `isTheme`, `getOppositeTheme`, `getStoredTheme` |

Run with `npm test` from `JeffyGPT.Web`.

## Test count summary

| Project | Tests (approx.) |
|---------|-----------------|
| JeffyGPT.Api.Tests | 7 |
| JeffyGPT.Application.Tests | ~90 |
| JeffyMCP.Tests | ~39 |

## Coverage gaps

Not currently tested:

- Live JeffyMCP stdio subprocess from Api.Tests (by design: `TestMcpToolBootstrapper` + `Enabled=false`)
- React component rendering and layout (predicate logic is unit-tested in Vitest)
- `ChatRequestValidator` role allowlist / size limits (`Chat/ChatRequestValidatorTests.cs`)
- JeffyMCP HTTP loopback bind rules and streamed sandboxed reads
- Frontend: outbound history filtering, composer error mapping (`npm test`)
- `FakeAssistantClient` directly
- Live OpenAI or vLLM integration (handler-based unit tests only; no live LLM in CI)
- CORS behavior
- Authentication (not implemented)
- Multi-message conversation history edge cases (frontend now stores full API history in-session; not covered by automated tests)

## Testing guidelines (from project rules)

- Do not depend on a live LLM in normal automated tests
- Mock or fake the LLM client behind `IAssistantClient`
- Prefer integration tests for API endpoints
- Use test names that describe behavior (`Method_Scenario_Expected`)
