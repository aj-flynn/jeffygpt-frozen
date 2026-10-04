# Cursor harness map

This is the Claude Code to Cursor map for these skills. The same text ships with the skills at `.cursor/skills/engineering/HARNESS.md`, so a project that installed the skills can still read it. Keep the two files identical.

## Read a skill

Cursor has no Skill tool. A slash name in prose does not load a skill on later turns.

To run a model-invoked skill, find the folder under `.cursor/skills/` whose `SKILL.md` frontmatter `name` matches, read that file, and carry out its instructions. Two skills means two reads, one skill each.

A user-invoked skill (`disable-model-invocation: true`) is started by the human. Do not open it because another skill named it. Two exceptions:

- The human already listed it in `.scratch/engineering/session.md` under `active_skills`. Re-read that `SKILL.md` every turn of the same effort. That is the same invocation, not a new one.
- Tell the human to run `setup-matt-pocock-skills` once per repo. Do not run it yourself.

`handoff` is user-invoked. When work must travel, tell the human to run it.

## Task subagents

Where a skill says to dispatch a sub-agent, use Cursor's Task tool.

- Fact-finding that only reads the repo: `subagent_type` `explore`.
- Fact-finding that must run commands or write files, including grilling's environment facts and implement-spec's notes: `subagent_type` `generalPurpose`.
- Implementation, review, research, and design: `subagent_type` `generalPurpose`.
- The subagent does not see this conversation. The prompt must include the question, the absolute path of each `SKILL.md` it should follow, the ticket or spec path, the branch, and the worktree path when there is one.
- Set `run_in_background` when this session should keep working. Research and implement-spec implementers are the usual case. If `.scratch/engineering/session.md` Notes already name an output path for that same question, do not dispatch another one.
- Parallel reviews and design-it-twice alternatives are separate Task calls in one turn.
- Implement-spec: before each Task, create one git worktree per ready ticket with `git worktree add`. Launch one background Task per ticket, and tell it to edit only that worktree. Do not serialize the frontier onto one branch.

## Questions

Ask in the skill's own format and wait for the next user message. Grilling uses numbered frontier questions with a recommended answer. Cursor's structured question UI belongs to Plan mode, not to this custom mode. If an AskQuestion tool is actually in the tool list, you may use it for that same frontier round.

## Phase changes

There is no `/clear` and no `/compact`.

- Continue when the next phase needs this conversation as a primary source.
- A new chat that discards context: set `.scratch/engineering/session.md` `status` to `closed` first. The next chat starts from nothing and must not reload that file. The old chat stays in history.
- When work must travel (a new directory, a colleague, or a side task forked mid-phase), tell the human to run `/handoff`.
- Use a Task subagent when the task can run without the user.
- Otherwise write a handoff that says what the next phase must keep, set the engineering session `status` to `closed`, and start a new chat from that file only. That is the lossy default, not the first reach.

While `status` stays `active`, the session file is the design tree across turns of the same effort. It is not a stand-in for a context you meant to discard.

## Steering files

`setup-matt-pocock-skills` writes the `## Agent skills` block to `AGENTS.md`. If `CLAUDE.md` already exists, update that file too. Do not create `CLAUDE.md` when it is absent.

## What stays the same

`disable-model-invocation: true` still means the human must start the skill. Re-reading a skill already listed in an active engineering session is persistence, not a second start. These skills do not use Claude-only frontmatter (`context: fork`, `allowed-tools`, `hooks`, `argument-hint`, shell injection). Custom Mode cannot lock tools the way Plan mode can. The grill gate applies only while the engineering session `phase` is `grilling`. It forbids production edits until the user confirms a shared understanding. `GLOSSARY.md`, ADRs, and a throwaway prototype are allowed during that gate. Other skills are not under it.
