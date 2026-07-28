# Repository policy

- Repository visibility is private by default.
- Never publish this repository or change its visibility without an explicit instruction from Dragos Cociu for this specific repository.
- Do not push directly to `main`; use a task branch or isolated worktree.
- Do not merge automatically. Final integration requires the configured human gate.
- Never commit credentials, OAuth caches, secrets, private traces, or personal data.
- A task is complete only after its deterministic acceptance checks have run successfully.
