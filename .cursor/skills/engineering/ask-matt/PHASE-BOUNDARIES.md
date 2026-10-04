# Phase boundaries

A **phase** is a chunk of work inside a session: the grilling, the implementation, the QA. The definition is fuzzy on purpose: a phase ends when you think *"ok, we're done with that"*.

The **phase boundary** is the gap between two phases, and it is the only place this decision belongs. Mid-phase there is no decision to make: continue, or split the work that's left into Task subagents. Starting a new chat mid-phase makes the agent lose the thread.

Cursor has no `/clear` and no `/compact`. A new chat and a handoff file replace them. See `.cursor/skills/engineering/HARNESS.md`. The engineering session file keeps a grill alive across turns of the same effort. A discard sets its `status` to `closed` first, so the next chat does not reload it.

## The five options

| Option | What it does |
| --- | --- |
| **Continue** | Stay in the session. No context switch at all. |
| **New chat** | Empty the context window and start from nothing. Set the engineering session `status` to `closed` first. The next chat must not reload it. The old chat stays in history. |
| **`/handoff`** | Write a portable markdown file and seed a session anywhere with it. |
| **Task subagent** | Send the task to its own context window and get a report back. |
| **Handoff into a new chat** | Write what the next phase must keep, set the engineering session to `closed`, then start a new chat from that file only. |

## The tree

Work top to bottom at the boundary. The first **yes** wins.

**1. Can you continue in this session?** Two things make the answer yes: the next phase needs this phase as a **primary source**, or you have enough [smart zone](https://www.aihero.dev/ai-coding-dictionary/smart-zone) left (~150k tokens) for the next phase to fit. Grilling to implementation is the standard yes: the implementation wants the reasoning verbatim, not a summary of it. Continue costs nothing and loses nothing, so rule it out before anything else.

**2. Is the context irrelevant to what comes next?** Is everything in this session (the exploration, the decisions, the dead ends) disposable? If so, start a **new chat**, and set the engineering session `status` to `closed` before you do. It is the cheapest move on the board: it takes no time and hands back the whole window. It also isn't terminal: the old chat stays in history. The next chat must not reload the session file.

The cost of getting this wrong is one-way. Leave a *relevant* context behind and you lose the **why** behind what you built, and no amount of reading the diff back gets it returned.

**3. Do you need to hand off?** `/handoff` is narrow. You need it only when you are:

- swapping to a **new harness** (Claude to Cursor, or the reverse),
- moving to a **new directory** or repo,
- sending the work to a **colleague**,
- or forking a side task you found **mid-phase** without derailing what you're doing.

That list is the whole clause. What `/handoff` buys is **portability**: a file that travels. If nothing is travelling, you don't need it.

**4. Can the task be done AFK?** Is it scoped tightly enough to run with you away from the keyboard, no steering? Then send it to a **Task subagent** and leave this session untouched. Automated review is the standard case: the agent reads the diff and reports, and you aren't needed while it does.

**5. Otherwise, hand off into a new chat.** Relevant context, same harness, same directory, and you need to stay in the loop: this is where the tree lands, and it lands here often. Tell the handoff what the next phase is for ("we're going to QA this area") so the file keeps what that phase needs. Set the engineering session `status` to `closed` before the new chat. The handoff is the seed. The session file is not.

A handoff into a new chat is the **default, not the first reach**. It sits at the bottom because the four questions above it are all cheaper or more precise. The failure mode when people start here is a fresh session that is confidently wrong about a decision the summary flattened.

## Primary and secondary sources

Every move except **Continue** turns a **primary source** into a **secondary source**: the session as it happened, replaced by a summary of it. The trade is always the same shape:

| Source | Information | Noise | Room to move |
| --- | --- | --- | --- |
| Primary (Continue) | Full | Lots | Little |
| Secondary (handoff into a new chat, `/handoff`) | Lossy | Less | Lots |

This is why question 1 comes first. You only pay the lossiness when staying costs more than it saves.

## These are judgement calls

The questions are not objective: each has taste in it, and the same boundary can go two ways on two days. The value is in asking them **in order**, at the boundary rather than in the middle of the work.
