# ShadowSessionSummary 1.0

`ShadowSessionSummary` is a deterministic, diagnostic-only rollup of an ordered manifest of already-produced `AdvisoryResult` snapshots and an optional externally authored `RealDecisionRecord`.

CLI: `progresstrace shadow-summarize --snapshot-manifest-path <path> [--real-decision-path <path>] [--out <path>]`.

The manifest is a JSON array of `{shadowSequence, triggeringGateOutcomeId, advisoryResultPath}`. Entries must be strictly increasing by `shadowSequence`; gaps are allowed. PT900–PT904 fail closed; PT905 is informational and writes a summary with `realDecision: null` and `aligned: null`.

`finalRecommendation` is the latest snapshot recommendation. When a decision exists, `aligned` is true for `continue` with `continued`/`escalated`, true for `stop-recommended` with `stopped`/`merged`/`rejected`/`abandoned`, false otherwise, and null for `insufficient-evidence`. This table is fixed by ADR-0010.

Output is local-file-only, atomic, deterministic, and uses the sentinel `generatedAt` `1970-01-01T00:00:00.0000000Z`. Shadow artifacts belong outside both repositories; retention is external and separately governed.
