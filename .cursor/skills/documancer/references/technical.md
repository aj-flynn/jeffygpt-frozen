# Technical mode

Use `mode: "technical"`. Explain what an engineer needs to understand, integrate, operate or maintain. Start with Overview; select subsequent sections to match the system rather than forcing an API reference.

A useful order is architecture and boundaries, local setup, behavior/contracts, configuration, operations, recovery and source/version information. Include concrete dependencies, commands, types, defaults and failure behavior only when verified. Explain important tradeoffs and state transitions; distinguish recommended practices from enforced behavior. Link primary project sources through `sources` and record the relevant commit/release in `identity.source`.

Use code blocks for copyable commands and examples, tables for fields or configuration, and details for secondary troubleshooting. Examples that are not executable must be labeled illustrative. Never embed secret values. A document about an API may contain authentication, request/response and error reference; other technical documents need not.

See `../examples/technical.json` for the renderer's supported structure. Do not inherit its fictional system facts into real documentation.
