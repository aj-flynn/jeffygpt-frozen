# Documancer for Cursor

Create technical references, user guides and human UAT plans as editable JSON and self-contained offline HTML. All editions share the Forest dark/cream light design, renderer, examples and mode references.

Requires Node.js 20+. No dependencies or build step are needed.

```sh
node install.mjs /path/to/project --check
node install.mjs /path/to/project --agents
```

Run from this package directory; the target project must exist. Omit `--agents` for the skill alone. Installation creates `.cursor/skills/documancer/` and refuses an existing skill or customized agent conflict. Start a fresh Cursor session after installation. `/documancer` covers one message; Alt+Enter (Option+Enter on macOS) pins it as a Custom Mode for the session.

> /documancer to create a user guide for this project, with editable JSON beside the HTML.

Read [INSTALL.md](INSTALL.md), [USAGE.md](USAGE.md), and [SKILL.md](SKILL.md) for installation, authoring and the agent workflow. Optional roles and model behavior are in [references/delegation.md](references/delegation.md).

Maintained in the standalone Documancer repository; no original flynn-technology checkout is needed. Font attribution remains in `assets/OFL.txt` and embedded exports. Set `brand.name` to your actual publisher.
