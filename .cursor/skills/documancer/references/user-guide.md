# User-guide mode

Use `mode: "user-guide"`. Organize around the reader's tasks and observable outcomes. Begin with Overview, then prerequisites or “Before you begin,” task guides, recovery and help as useful.

State the actual access, account role and starting location needed. Use the visible interface labels in ordered steps; name the outcome so the reader can recognize success. Put alternate paths and error recovery alongside the task they affect. Include consequences before irreversible steps. Avoid implementation detail unless it changes the reader's decision.

Do not invent support channels, roles or features. When access or behavior cannot be verified, identify the specific uncertainty. Use the same technical source identity as companion engineering documentation, but write for users rather than copying an architecture explanation.

See `../examples/user-guide.json` for a structural example. Steps are plain text; references and cross-document navigation use link blocks or `related`.
