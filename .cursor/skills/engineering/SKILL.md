---
name: engineering
description: Persistent engineering session for these skills. Keeps the design tree on disk and re-reads the active child skill every turn. Use as a Custom Mode, or invoke /engineering, when grilling, triage, wayfinder, diagnosing bugs, or another multi-turn skill must survive later turns.
disable-model-invocation: true
icon: code
color: orange
---

# Engineering

This is the Cursor session mode for these skills. Custom Mode keeps this file in context on every turn. Typing `/engineering` once does not, so the session file and the always-apply rule re-enter this skill on later turns while `status` is `active`.

Harness differences from Claude Code are in [HARNESS.md](HARNESS.md). Follow that file when you dispatch a Task subagent, read another skill, or change phase.

## Every turn

Before you answer:

1. Read `.scratch/engineering/session.md`. If it is missing, or `status` is not `active`, this invocation is entry: create it as specified below. Do not reopen a `closed` session unless the user invoked engineering or named a skill again.
2. Re-read the `SKILL.md` for every skill listed in `active_skills`, including user-invoked skills. The human already started those by entering this session. Find each folder under `.cursor/skills/` whose frontmatter `name` matches.
3. Do the work for this turn.
4. Write the session file again before the reply. For a grill, Settled and Frontier are the design tree. Conversation memory does not override them.

## Session file

```markdown
---
status: active
phase: grilling
active_skills:
  - grilling
  - domain-modeling
confirmed_understanding: false
---

## Settled

-

## Frontier

-

## Notes

```

- `status` is `active` or `closed`.
- `phase` is `grilling` or `working`.
- `active_skills` is the list of child skills to re-read. One name per line.
- `confirmed_understanding` is `true` or `false`. It matters only while `phase` is `grilling`.
- `Settled` is every decision already made, in the user's words where you have them.
- `Frontier` is every open decision whose prerequisites are settled. Recompute it after each reply.
- `Notes` records output paths, such as a research note, so a later turn does not launch the same task again.

## Entry

If the user named a skill:

- `grilling`, `grill-me`, or `grill-with-docs` sets `phase` to `grilling`. `grill-with-docs` sets `active_skills` to `grilling` and `domain-modeling`. The other two set `active_skills` to `grilling`.
- Any other named skill sets `phase` to `working` and `active_skills` to that skill. Do not arm the grill gate.

If the user did not name a skill, read the message:

- A concrete task that matches a skill (triage an issue, teach a topic, diagnose a bug, implement a ticket, review a diff) uses that skill with `phase` `working`.
- An idea to sharpen uses grill-with-docs in a repo, and grill-me otherwise, with `phase` `grilling`.

## Grill gate

The gate applies only while `phase` is `grilling` and `confirmed_understanding` is `false`.

- Do not edit production code.
- You may edit `GLOSSARY.md`, ADRs, and throwaway prototype code. A prototype detour stays inside the grill.
- Ask the frontier in the grilling format, record settled decisions and the new frontier in the session file, then wait.
- Set `confirmed_understanding: true` only when the frontier is empty and the user has confirmed a shared understanding.

Leave the gate when the user names any other skill, or confirms a shared understanding and asks you to build. Set `phase` to `working` and `active_skills` to that work, then read and follow those skills.

## Leaving

Set `status` to `closed` when the user leaves engineering, when a phase boundary discards the context (a new chat, or a handoff into a new chat), and when a wayfinder ticket is resolved. A closed session is not resumed by the always-on rule.

Do not close the session between turns of the same effort. That is what keeps a grill alive.
