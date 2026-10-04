# Install in Cursor

Requires Node.js 20+ and an existing project. From this extracted package:

```sh
node install.mjs /path/to/project --check
node install.mjs /path/to/project
```

Add `--agents` for four optional native profiles. To add them later:

```sh
node scripts/install-agents.mjs /path/to/project
```

The skill goes in `.cursor/skills/documancer/`; profiles go in `.cursor/agents/`. No global defaults are changed. `--check` checks conflicts without writing. Existing skills, legacy forest-documentation installations and customized profiles require review; the installer never silently replaces them. Matching profiles may be reused.

For manual installation, copy this entire package into `.cursor/skills/documancer/`. Keep the directory name `documancer`. Copy optional profiles from `subagents/` into the agent directory. Start a fresh session.

For upgrades, back up the installed skill outside skill discovery directories, compare and preserve customizations, remove the old installed folder after review, then reinstall. Merge customized profiles explicitly. To uninstall, remove only the installed `documancer` folder and optional `documancer-*` profiles. Keep authored JSON and HTML in your project documentation folder.

Use one edition per project: other editors also discover `.agents/skills`, so same-name copies may be ambiguous. See the standalone repository README for native editions and shared-editor use.
