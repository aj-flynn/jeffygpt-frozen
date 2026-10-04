# Use Documancer

Documancer creates self-contained HTML technical references, user guides, and human UAT plans. Each document has an editable JSON source. The generated HTML embeds its styling, scripts, font, license, and configured identity, so readers can open it offline in a browser.

## Ask Cursor to create a document

In a project containing the installed skill, ask for the specific document you need. For example:

> /documancer to create a user guide for this project. Verify the actual interface and keep the editable JSON beside the HTML export.

`/documancer` applies to that message. To keep the skill for a longer documentation session, select it as a Custom Mode with Alt+Enter on Windows or Option+Enter on macOS. The installed agents are separate commands (`/documancer-map`, `/documancer-analyze`, `/documancer-write`, `/documancer-review`). Leave those to the skill unless you want one bounded role.

You can also request a technical reference or UAT plan, or a set of companion documents. Provide the project version, intended readers, source material, and any known limitations. The skill does not make unverified claims or invent test results.

## Render a document yourself

Use the mode-specific JSON examples in `examples/` as starting structures. Replace all illustrative facts. Read `references/data-contract.md` for the required fields and supported content blocks. Set `brand.name` to the actual publisher; the bundled Flynn wordmark is only for Flynn Technology LLC. A different publisher uses text branding by default.

From the installed skill directory, render one standalone document:

```sh
node scripts/render.mjs /path/to/source.json /path/to/output.html
```

For companion documents, create a set manifest like `examples/set.json` listing **only** the JSON sources and HTML outputs you will deliver:

```sh
node scripts/render.mjs --set /path/to/set.json /path/to/output-directory
```

The three examples can be rendered for a quick local demonstration:

```sh
node scripts/render.mjs --set examples/set.json dist
```

Keep the source JSON with the exported HTML for future edits. Re-render a smaller set rather than deleting one HTML file from a previously generated set; companion navigation is determined when rendering. Technical and user guides link to each other when included, and UAT may link to included guides in new tabs. A standalone export has no companion navigation.

Open the result in a browser and check content, links, both appearance themes, and narrow-screen layout. For UAT, check the result controls, notes, and Generate summary. UAT results and notes live only in the current browser session and are lost on reload; Generate summary copies the current text when clipboard access is available.

Rendering validates the JSON structure, but it cannot verify product facts, source freshness, remote links, or that a person has passed a UAT test. See [SKILL.md](SKILL.md) for the full authoring workflow and [README.md](README.md) for package details.
