---
name: documancer
description: Create or update self-contained HTML technical references, user guides, and interactive human UAT plans from editable JSON. Use when the user asks for Documancer, a technical reference, a user guide, a human acceptance plan, or offline HTML product documentation. Do not use for README files, other markdown docs, code comments, or automated test suites.
compatibility: Cursor Agent with Node.js 20+
icon: book-open
color: green
---

# Documancer

Create a source JSON document and a self-contained HTML export with the bundled renderer. Reuse this design for all three modes; do not recreate the layout per document or require this skill's original repository at runtime.

Choose only the reference needed for the requested work:

- **Technical** — architecture, configuration, contracts, integrations or operations: [technical.md](references/technical.md).
- **User guide** — task instructions for people using a product: [user-guide.md](references/user-guide.md).
- **UAT** — human acceptance journeys, results and notes: [uat.md](references/uat.md).

For multiple requested audiences, create separate documents with the same source identity and render them as an explicit set. Technical and user guides must never link to UAT, including in body content or sources. UAT links to either companion guide open in a new tab so test results remain intact. Match scope to the request; do not generate all three merely because they are available.

## Cursor integration

These instructions are already in context. Resolve bundled files relative to this SKILL.md. The shell starts in the project, so run `node` with the absolute path to this skill's `scripts/render.mjs`.

Search and read the implementation directly. Check the exported HTML with Cursor browser tools: both themes, narrow-screen reflow, navigation, links, and the relevant controls. If those tools cannot open the file, report that only renderer validation ran.

## Keep work proportional

For substantial independent discovery, a large unfamiliar codebase, or separate requested document drafts, launch the optional subagents in [delegation.md](references/delegation.md) with the Task tool when they are installed. Launch every ready task in one turn, and hold a task until the packet, outline, or draft it needs exists. Use subagent types `documancer-map`, `documancer-analyze`, `documancer-write`, and `documancer-review` only for the cases that file describes. A small edit stays in this agent. Do not also run the built-in Explore subagent over the same scope, and do not send rendering or file copying to a subagent.

## Create the document

1. Use the requested outcome, existing repository guidance, relevant implementation, actual interface labels and available evidence. Record the tested/documented version and distinguish verified behavior, uncertainty and illustrative material. Do not invent product behavior, accounts, credentials, fixtures or verification results.
2. Read [the data contract](references/data-contract.md). Author JSON using the applicable example in `examples/` as a structural guide, replacing its illustrative content. Keep editable JSON next to the output in the project's existing documentation location. Use plain text blocks; the renderer escapes them. Do not insert raw HTML or modify the renderer to accommodate ordinary content.
3. For companion guides, list only the documents being delivered in a set manifest (see `examples/set.json`) and run `node <skill-directory>/scripts/render.mjs --set <set.json> <output-directory>`. Navigation is derived from that set, not from files left over in the output folder. Regenerate the intended subset before distribution; do not remove a linked guide after export. For a standalone document, render with `node <skill-directory>/scripts/render.mjs <source.json> <output.html>`. Node.js 20+ is required; no npm install, server, build framework or network resources are needed. Paths are relative to the invoking working directory; package assets resolve from the script itself.
4. Open the resulting file and verify both themes, narrow-screen reflow, navigation, links and relevant controls. For UAT, exercise notes before selection, result changes and Generate summary, including denied clipboard access when practical. Report real checks and limitations; successful rendering is not browser verification or human acceptance.

The shared renderer embeds styles, scripts, Source Sans 3, its license and the configured wordmark. Set `brand.name` to the actual document publisher; the bundled Flynn wordmark is the default only for Flynn Technology LLC. Another publisher gets a text identity unless explicitly configured otherwise. Preserve brand truth; never attribute another project's work to Flynn merely because the template came from Flynn.

The reader retains a saved appearance choice when browser storage permits. UAT results and notes are **not saved on reload**; keep this visible in the document. Generate summary creates current plain-text results and copies them in the same action. Never add a separate copy-summary button or automatically submit results.

Return the HTML link, editable source link and concise readiness or verification limitations. Generating documentation does not authorize publication or replacement of another project's skills.
