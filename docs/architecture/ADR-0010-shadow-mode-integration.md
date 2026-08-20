# ADR-0010: Shadow Mode Integration

- **Status:** Accepted after human gate on 2026-08-20
- **Scope:** ProgressTrace Phase 4.6

## Context

Phases 4.0–4.5 provide deterministic `AgentSession`, `GateOutcome`, `ObligationLedger`, `AdvisoryResult`, `AdvisoryDivergenceReport`, and `ObservedBudget` contracts. Persistence and shadow mode were deliberately deferred. Phase 4.6 must observe repeated advisory snapshots beside Hermes decisions without changing the live decision path.

## Decision

Add two additive diagnostic contracts: `RealDecisionRecord 1.0` and `ShadowSessionSummary 1.0`, plus the deterministic CLI verb `shadow-summarize`. Existing `advise`, `advise --divergence-report`, and `budget` remain byte-for-byte unchanged.

The Hermes hook is external tooling, not a Hermes core/plugin lifecycle change and not part of this repository's implementation. It appends the already-existing `GateOutcome` to the gate-outcomes array, invokes unmodified `advise`, stores the advisory snapshot and divergence sidecar, records a manifest entry, and at session close authors `RealDecisionRecord` and invokes `shadow-summarize`.

Shadow artifacts live outside both Git trees under `/srv/projects/.tooling/progresstrace-shadow/<sessionId>/`, with user-only permissions. The hook is fire-and-forget with a bounded timeout: its success or failure never gates, changes, stops, interrupts, or escalates the real Hermes pipeline. Retention is external, explicit, and separately scheduled/manual; this phase does not prune data.

Correlation uses existing `sessionId`, `taskContractId`, `ledgerTraceId`, plus manifest-only `shadowSequence`, `triggeringGateOutcomeId`, and `advisorySourceDigest`. Reprocessing the same snapshot is an idempotent overwrite.

`ShadowSessionSummary.aligned` is fixed as follows: `continue` + (`continued` or `escalated`) is true; `stop-recommended` + (`stopped`, `merged`, `rejected`, or `abandoned`) is true; all other non-insufficient combinations are false; `insufficient-evidence` or missing decision is null.

## Diagnostics

PT900 cross-reference mismatch; PT901 invalid referenced AdvisoryResult; PT902 invalid RealDecisionRecord; PT903 duplicate/out-of-order manifest sequence; PT904 deterministic assembly invariant failure; PT905 informational missing decision, non-blocking.

PT900–PT904 write no partial output. PT905 exits zero and writes a complete summary with null decision/alignment.

## Non-goals and protected surface

No active mode, automatic stopping, interruption, escalation, network, model calls, free-text interpretation, cross-session aggregation, pruning tool, or changes to any Phase 0–4.5 contract, evaluator, projector, advisory, budget, or existing CLI. The external Hermes hook is explicitly outside this repository. New files are limited to the two schemas/docs, this ADR, the task contract, `Core/Shadow`, additive models/validators/normalizers/diagnostics, a new CLI verb, fixtures, and conformance tests.

## Human-gated decisions

The approved gate binds: external fire-and-forget hook; the closed real-decision vocabulary; the alignment table; external storage location; separate retention policy; unchanged existing CLIs; PT900–PT999 allocation; additive diagnostic-only schemas; local-only persistence; and external ownership of the Hermes hook. Any change requires a superseding ADR and a new gate.
