> The Cursor skill commands below are historical demo instructions. Current Codex work uses the single consolidated collection in `~/.codex/skills`; the old engineering wrapper is retired.

# Conference Demo — search-files

Breakout runbook for demonstrating [Matt Pocock skills](https://github.com/mattpocock/skills) while adding a JeffyMCP `search-files` tool.

This runbook stays in markdown. The old presenter page under `.userdocs` was removed when that folder became the Documancer user guide. `search-files` is not a tool in the current server.

## Flow (summary)

1. Pre-install skills: `npx skills@latest add mattpocock/skills` (select your coding agent and `setup-matt-pocock-skills`)
2. Once per repo: `/setup-matt-pocock-skills` (prefer local-files tracker for on-stage reliability)
3. Live: `/grill-with-docs` → `/to-spec` → `/tdd` (or `/implement`) for `search-files`
4. Restart stack; prove the tool in JeffyGPT chat against the `docs` root

## Related

- [JeffyMCP](./jeffymcp.md)
- [Using JeffyGPT](../../.userdocs/user-guide.html#tools)
- [Development](./development.md)
