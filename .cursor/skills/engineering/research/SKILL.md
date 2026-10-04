---
name: research
description: Investigate a question against high-trust primary sources and capture the findings as a Markdown file in the repo. Use when the user wants a topic researched, docs or API facts gathered, or reading legwork delegated to a background agent.
---

If `.scratch/engineering/session.md` Notes already name an output path for this question, read that note and do not dispatch again.

Otherwise dispatch one background Cursor Task subagent (`generalPurpose`) so you keep working while it reads (see `.cursor/skills/engineering/HARNESS.md`). The prompt must include this skill's job list, the question, and the absolute path of this SKILL.md. When the session file is active, record the output path in Notes before you reply.

Its job:

1. Investigate the question against **primary sources** (official docs, source code, specs, first-party APIs), not a secondary write-up of them. Follow every claim back to the source that owns it.
2. Write the findings to a single Markdown file, citing each claim's source.
3. Save it where the repo already keeps such notes; match the existing convention, and if there is none, put it somewhere sensible and say where.
