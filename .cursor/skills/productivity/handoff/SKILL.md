---
name: handoff
description: Compact the current conversation into a handoff document for another agent to pick up.
disable-model-invocation: true
---

Write a handoff document summarising the current conversation so a fresh agent can continue the work. Save to the temporary directory of the user's OS - not the current workspace.

Include a "suggested skills" section naming which skills the next human should invoke. Do not tell the next agent to open a user-invoked skill. Model-invoked skills may be named for that agent to read.

If the user passed text after the skill name, treat it as what the next session will focus on and tailor the doc accordingly.

Do not duplicate content already captured in other artifacts (specs, plans, ADRs, issues, commits, diffs). Reference them by path or URL instead.

Redact any sensitive information, such as API keys, passwords, or personally identifiable information.

