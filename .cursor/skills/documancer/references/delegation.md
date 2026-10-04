# Selective delegation in Cursor

The four optional Markdown subagents are packaged in `subagents/`. Install them in the destination project's `.cursor/agents/` with `node scripts/install-agents.mjs <project-root>` or use `install.mjs <project-root> --agents` from the extracted ZIP. The skill and renderer work without them.

Cursor puts every file in `.cursor/agents/` on the Task tool for Agent sessions in that project, including sessions that never load this skill. The descriptions therefore limit each role to a brief assigned by the Documancer parent. They intentionally omit "use proactively."

| Agent | Use |
| --- | --- |
| `documancer-map` | Gather verified implementation facts for one bounded subsystem. |
| `documancer-analyze` | Resolve difficult cross-module behavior, conflicting contracts, or UAT journeys. |
| `documancer-write` | Draft one JSON document from a verified evidence packet and agreed outline. |
| `documancer-review` | Check one substantial draft against its evidence for unsupported claims and acceptance errors. |

The profiles set `model: inherit` and leave `is_background` unset. Each run uses the parent model and returns before the parent continues. There is no cost or reasoning-effort guarantee. A project owner may set a supported model id, including Cursor's `model[effort=…]` form, on an installed copy after checking that account's model list. Leave Codex model ids out of these files.

Launch a role with the Task tool. The subagent type is `documancer-map`, `documancer-analyze`, `documancer-write`, or `documancer-review`. A user can also ask for that role with `/documancer-map` and the matching names. Subagents start from a clean context: they do not see this skill or the parent conversation. Put the brief in the Task prompt: request, project rules, assigned boundary, source identity, relevant evidence paths, and expected output. Include the mode-reference and data-contract paths when the role must read them.

Use the smallest useful workflow. A small correction stays with the primary agent. For an unknown feature, map only the relevant subsystem. Do not also run Cursor's built-in Explore subagent on that same scope. `documancer-map` returns a citation packet; Explore returns a general search summary. Resolve material ambiguity before writing. For several requested documents, discover facts once and give each writer a shared evidence packet and an exclusive JSON output path. Add one bounded review when consequential claims or acceptance criteria need an independent check. Keep deterministic rendering, validation, and file copying on the parent. The parent checks the final HTML with Cursor browser tools.

Launch every Documancer task whose inputs are ready, in one turn. Hold a task until the packet, outline, or draft it requires exists. Read-only tasks with separate boundaries go together: one `documancer-map` or `documancer-analyze` per requested subsystem, never one per file. Writers go together only when each has its own JSON path, one writer per document. A review starts after its draft exists; reviews of different drafts can run together. Integrate the results before widening the scope. Do not add agents to speed up work inside a single boundary. Subagents must not delegate again. Since Cursor 2.5 a subagent can launch one nested level, and `readonly: true` does not remove the Task tool. The profile text forbids that call; the parent still owns the boundary and must not treat the forbid as a sandbox. Discovery, analysis, and review set `readonly: true`, which blocks edits and state-changing shell commands. The writer is not path-sandboxed and may edit only its assigned JSON path. The primary agent owns scope, evidence integration, final JSON and HTML, and verification. If the Task tool or an installed profile is unavailable, do the bounded work in the primary agent and report that limitation. Do not claim an unavailable profile ran.

Discovery packets state verified facts with file, line, or symbol references, actual UI labels and setup, meaningful failures, and narrow unresolved questions. Static inspection is not proof that the application was run. Writers use the verified packet and mode reference; they do not rediscover the repository. The primary agent renders the intended set once.

Source: [Cursor subagents](https://cursor.com/docs/subagents) and [Cursor skills](https://cursor.com/docs/skills).
