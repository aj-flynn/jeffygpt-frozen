# UAT mode

Use `mode: "uat"` for an explicitly requested HTML acceptance plan or a complex human exercise needing prepared states and result recording. For a small change, short inline acceptance steps may suffice unless the user requested the document. No human judgment needed means report automated evidence; do not invent manual work.

Select a small set of affected user journeys tied to the promised outcome. Keep broader state-space and infrastructure checks in automated evidence. Prefer independent tests; disclose unavoidable ordering, data changes and repeat/reset instructions.

## Prepare real starting states

Within the existing task authorization, prepare and rehearse the application instance and synthetic fixtures. Verify exact URLs, labels, roles, identifiers, dates and distinguishing data. Record absolute checkout, branch (including detached HEAD), commit plus relevant uncommitted changes, current app readiness and restart instructions in identity/setup. After worktree recreation or handoff, recheck instance and fixtures without rewriting historical evidence. Do not embed secrets or assume authorization for external writes or destructive resets.

If rehearsal is blocked, identify the blocked journeys and deliver usable instructions without claiming readiness. Successful agent rehearsal establishes executability, never human acceptance. Each new plan starts Untested for every test.

## Required reading order

The renderer fixes the sequence:

1. **Overview:** what is being tested, scope, identity and readiness.
2. **Shared setup:** environment, accounts, common data and startup instructions.
3. **Tests:** each has a stable ID, title, what to judge, prerequisites (specific data/additional setup/starting state), ordered test plan, observable pass conditions, problem signs, repeat/reset or dependencies, rehearsal state, five result choices and notes.
4. **Automated evidence**, when available, separately labeled and read-only.
5. **Summary**, always last, with one Generate summary action that also attempts to copy.

Preserve exactly **Passed, Failed, Skipped, Blocked, Untested** for human results. Notes must work before selecting a result. Never prepopulate Passed from rehearsal or automated evidence. Skipped and Untested remain unaccepted; failed and blocked journeys need attention.

Evidence includes check/command, outcome, date, code/configuration/fixture identity and observations. Preserve earlier failure history and explain any correction; a retry alone does not establish a valid pass. Reuse applicable evidence and run only missing or invalidated checks. Never convert missing or blocked checks into passing evidence.

The final summary includes plan identity, five counts, every scenario's latest selection and notes, and separate automated evidence/history. Clipboard rejection leaves generated text visible and selected with an honest recovery message. Edits after generation mark it stale. Results are session-only: tell the reader to generate and retain the summary before reloading or closing.

See `../examples/uat.json` and `data-contract.md`. This mode supersedes the design and HTML schema of the earlier Legimancer `uat-test-plan` skill when adopted; it preserves that skill's meaningful human-acceptance practices. Installing here does not remove that skill from any other repository.
