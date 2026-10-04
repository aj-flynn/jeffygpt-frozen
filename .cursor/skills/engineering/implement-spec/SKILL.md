---
name: implement-spec
description: "Implement the result of /to-spec and /to-tickets in code."
disable-model-invocation: true
---

You have been provided a spec. This spec should have tickets associated with it, describing how to implement the spec.

The issue tracker should have been provided to you. If not, tell the user to run `/setup-matt-pocock-skills`.

The goal is the entire spec implemented on a single **integration branch**, with every ticket resolved the way the issue tracker closes work.

The tickets are not a list of steps. They are a **task graph** with blocking relationships between them. This means there is always a **frontier** of tickets which are ready to be grabbed.

Communication to and from subagents should be sparse. Communicate primarily through **context pointers**: to the spec, tickets, research notes, and previous commits. Don't duplicate information already available via pointers.

**Implementer subagents** are Cursor Task subagents (see `.cursor/skills/engineering/HARNESS.md`). Run them in the background for maximum concurrency. Before each one, create its git worktree with `git worktree add`. One worktree and one branch per ready ticket. Do not serialize the frontier onto one branch.

## Steps

1. Read the spec and tickets to understand the task graph.

2. (optional) Use an **exploration Task subagent** with `subagent_type` `generalPurpose` to conduct any exploration required by the tickets: relevant codebase files or external documentation. It must be able to write. Its prompt includes the spec path, the ticket paths, and where to save notes. It saves its markdown notes in a directory outside the repo, accessible by all future subagents. This lets **implementer subagents** focus on implementation rather than exploration.

3. Create the integration branch. If the issue tracker closes work through PRs, or the user asks for one, open a draft PR after the first merge in step 5 (a branch with no commits ahead of main can't open one), marked as closing the spec and tickets.

4. Use **implementer Task subagents** to implement each ready ticket. Create the worktree first. The prompt must stand alone, because the subagent does not see this conversation. Include the absolute worktree path, the branch, the integration branch, the ticket path, the spec path, and the absolute path of the `tdd` SKILL.md. Tell it to edit only that worktree. Each implementer subagent:
   - confirms its worktree is based on the integration branch before starting, and resets onto it if not;
   - reads and follows the `tdd` skill to build the ticket;
   - merges the integration branch tip into its own branch before reporting done

5. Once an **implementer subagent** completes, merge its work to the integration branch with a **merger subagent**.

6. If this changes the **frontier** of available tickets, kick off more **implementer subagents** to work on the new tickets. This allows for maximum concurrency.

7. Once all tickets are complete, read and follow the `code-review` skill on the integration branch. Fix all issues raised by the code review in a single **implementer Task subagent**.

8. If a draft PR exists, mark it ready for review. Otherwise, resolve each ticket the way the issue tracker closes work, and report the integration branch.

9. Clean up all **implementer subagent** worktrees.
