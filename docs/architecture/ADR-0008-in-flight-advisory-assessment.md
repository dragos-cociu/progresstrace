# ADR-0008: In-Flight Advisory Assessment

## Status

Accepted (human gate approved, 2026-08-20). Scope frozen to the six artifacts
listed in "Deliverables"; implementation code, fixtures, and tests are
out of scope for this ADR and are tracked separately.

## Context

Phases 4.1–4.3 established a closed, local pipeline: `AgentSession` and
`GateOutcome` (Phase 4.1/4.2) are projected into an `ObligationLedger`
(Phase 4.3) and classified by the existing `Evaluator.Evaluate` method, which
SessionEvaluator and GateOutcomeProjector already depend on. That pipeline
runs at session boundaries.

There is a recurring need to ask "what would the evaluator say right now,
given the evidence collected so far" while a session is still in flight,
without waiting for the session to end and without altering what the session
boundary itself will eventually conclude. Phase 4.4 defines the contracts for
that in-flight, read-only advisory path.

## Decision

### Scope

- Define `AdvisoryResult` (`contracts/advisory-result.schema.json`,
  `docs/contracts/advisory-result.md`): the output of an `advise` operation
  that snapshots the current classification of a session from explicit,
  already-materialized inputs.
- Define `AdvisoryDivergenceReport` (`contracts/advisory-divergence-report.schema.json`,
  `docs/contracts/advisory-divergence-report.md`): an independent sidecar
  artifact that records, per obligation, where a previously generated ledger's
  declared status disagrees with the advisory's live-computed status.
- Record the task contract for this phase in
  `tasks/phase-4-4-in-flight-advisory.json`.

### `advise` inputs

The `advise` operation takes exactly three explicit inputs, each a path to an
already-materialized local artifact:

1. `session-path` — an `AgentSession` document.
2. `ledger-path` — an `ObligationLedger` document, generated ahead of time
   (e.g. by the Phase 4.3 ledger generator) and supplied externally.
3. `gate-outcomes-array-path` — a JSON array of `GateOutcome` records
   observed so far in the session.

No other input is accepted. `advise` never reads conversation text, calls a
model, executes `command`, or performs an online action — the same
restrictions already binding `GateOutcomeProjector` and `SessionEvaluator`.

### Evaluator reuse

`Evaluator.Evaluate` remains the single, unmodified classification authority.
Phase 4.4 introduces no new evaluator, no forked classification logic, and no
alternate vocabulary. `AdvisoryResult.classification` is populated from the
same closed vocabulary already produced by `Evaluator.Evaluate` for
`SessionEvaluator`: `progress`, `recovery-after-failed-attempt`,
`repeated-attempt-without-obligation-advancement`, `insufficient-evidence`.

### Insufficient-evidence priority

When the supplied evidence is absent, incomplete, or incoherent for any
targeted obligation, `classification` is `insufficient-evidence` regardless
of what partial progress other obligations show. This mirrors the existing
priority rule for `SessionEvaluator` and is not re-derived or overridden by
Phase 4.4.

### Recommendation vocabulary

`AdvisoryResult.recommendation` is a required field with a closed vocabulary
of exactly `continue`, `stop-recommended`, `insufficient-evidence`. It is a
deterministic derivation on top of `classification` and per-obligation
`stable`, not a second evaluation authority: it calls no additional
evaluator, reads no additional input, and — like `classification` itself —
never stops, interrupts, or escalates anything. The derivation rule is
total and applied in this order:

1. If `classification` is `insufficient-evidence`, `recommendation` is
   `insufficient-evidence`.
2. Otherwise, if every entry in `obligations` has `stable: true`,
   `recommendation` is `stop-recommended`.
3. Otherwise, `recommendation` is `continue`.

`obligations` is non-empty (`minItems: 1`), so rule 2 is never satisfied
vacuously.

### Per-obligation structured reason

Each `AdvisoryResult.obligations[]` entry carries a structured reason
instead of free text derived from `description`: `obligationId`, `status`
(the existing ledger signal vocabulary, sourced for
`AdvisoryDivergenceReport` comparison), `classification` (a per-obligation
instance of the same closed `Evaluator.Evaluate` vocabulary as the
top-level `classification`), `stable` (the existing boolean from the
stable-attainment helper below — the "outcome" of that helper for this
obligation), and `evidenceEventIds` (the event ids backing `classification`
and `stable` for that obligation, following the same content-and-ordering
convention as `EvaluationResult.obligationResults[].evidenceEventIds` in
`docs/contracts/evaluation-result.md`). No field is generated from
`description` text.

### Stable-attainment reuse

Per-obligation `stable` flags in `AdvisoryResult` are computed by the
existing stable-attainment helper already shared internally with
`StopAssessor`. Phase 4.4 consumes that helper's existing boolean output; it
does not add a rank, priority score, or overhead field to the helper or to
its callers. `StopAssessor`'s own stop-decision behavior is unmodified.

### SessionTraceBuilder boundary

`SessionTraceBuilder` currently builds its own ledger internally and does not
accept an externally-supplied ledger. `advise` does not route through
`SessionTraceBuilder`; it is a separate, parallel boundary that consumes
`ledger-path` directly. Adding external-ledger support to
`SessionTraceBuilder` itself is additive future work, not part of this ADR.

### Divergence report shape

`AdvisoryDivergenceReport` is an independent sidecar, schema version `1.0`.
It is not an `EscalationQueue`: it carries no action, priority, or workflow
state, and produces no side effect. Its `divergences` array (per-obligation
comparison of ledger-declared status vs. advisory-computed status) is the
normative, required part of the contract. Its `rollup` object (session-level
counts and an overall converged flag) is diagnostic only — informative,
optional, and not covered by the same compatibility guarantee as
`divergences`.

### Determinism

`generatedAt` on both `AdvisoryResult` and `AdvisoryDivergenceReport` is
fixed to the canonical sentinel `1970-01-01T00:00:00.0000000Z` — the same
deterministic-generation-timestamp convention already established for
`LedgerGenerationReport.generatedAt`
(`docs/contracts/ledger-generation-report.md`). It is not a wall-clock
capture; it is enforced by each schema as a `const`. Identical inputs (the
three `advise` inputs for `AdvisoryResult`; the `AdvisoryResult`/
`ObligationLedger` pair for `AdvisoryDivergenceReport`) always produce
byte-identical output.

`AdvisoryDivergenceReport.reportId` is deterministically derived, never a
randomly generated UUID: it is the lowercase hex-encoded SHA-256 digest of
the concatenation of the source `AdvisoryResult`'s canonical bytes and the
paired `ObligationLedger`'s canonical bytes, using the same digest
algorithm and canonicalization already used for `sourceDigest` /
`advisorySourceDigest` / `ledgerDigest` elsewhere in this repository. The
same `(AdvisoryResult, ObligationLedger)` pair always yields the same
`reportId`; the schema constrains it to the same `^[0-9a-f]{64}$` shape as
those other digest fields.

### Diagnostics

Phase 4.4 diagnostics are allocated the `PT700`+ range, explicit and
non-overlapping with prior phases:

| Code  | Condition |
|-------|-----------|
| PT700 | `session-path` reference does not match `ledger-path` or `gate-outcomes-array-path` |
| PT701 | `ledger-path` document fails `ObligationLedger` schema validation |
| PT702 | `gate-outcomes-array-path` document fails `GateOutcome` schema validation for any element |
| PT703 | Evidence is absent, incomplete, or incoherent for a targeted obligation (`insufficient-evidence`) |
| PT704 | `AdvisoryResult` cannot be normalized deterministically from the given inputs |
| PT705 | `AdvisoryDivergenceReport` input does not reference a valid `AdvisoryResult` (`advisorySourceDigest` mismatch) |
| PT706 | `AdvisoryDivergenceReport.divergences` cannot be computed for an obligation present in the ledger but absent from the advisory result, or vice versa |

Diagnostic messages do not echo source values, matching the existing
convention in `docs/contracts/gate-outcome.md`.

### Non-goals

- Persistence of `AdvisoryResult` or `AdvisoryDivergenceReport` beyond the
  single invocation that produced them.
- "Shadow mode" (continuous background advisory evaluation running alongside
  a live session). Both persistence and shadow mode are explicitly deferred
  to Hermes / Phase 4.6 and require their own human gate.
- Any change to `Evaluator.Evaluate`, `SessionEvaluator`, `StopAssessor`, or
  `GateOutcomeProjector` semantics.
- Automatic stopping, interruption, or escalation of any kind.
- External-ledger support inside `SessionTraceBuilder` (additive, later).

### Allowed / protected paths

Allowed for this ADR's follow-on implementation work:

- `contracts/advisory-result.schema.json`
- `contracts/advisory-divergence-report.schema.json`
- `docs/contracts/advisory-result.md`
- `docs/contracts/advisory-divergence-report.md`
- `docs/architecture/ADR-0008-in-flight-advisory-assessment.md`
- `tasks/phase-4-4-in-flight-advisory.json`
- A new, additive `advise` implementation module (path to be fixed by the
  implementation task, not this ADR).

Protected (no modification under this ADR):

- `contracts/agent-session.schema.json`
- `contracts/gate-outcome.schema.json`
- `contracts/obligation-ledger.schema.json`
- `docs/contracts/agent-session.md`
- `docs/contracts/gate-outcome.md`
- Existing `Evaluator`, `SessionEvaluator`, `StopAssessor`,
  `GateOutcomeProjector`, and `SessionTraceBuilder` implementations.

### Human gates

This ADR itself is the record of the human gate that approved:

1. `advise` inputs are exactly `session-path`, `ledger-path`,
   `gate-outcomes-array-path`.
2. `Evaluator.Evaluate` stays unique and unchanged.
3. `insufficient-evidence` has priority over other classifications.
4. The stable-attainment helper is shared with `StopAssessor`, with no
   rank/overhead addition.
5. `SessionTraceBuilder` external-ledger support is deferred and additive.
6. `AdvisoryDivergenceReport` is an independent sidecar, not an
   `EscalationQueue`.
7. Divergence is normative per obligation; the rollup is session-only
   diagnostic.
8. `PT700`+ is explicitly allocated to this phase.
9. Persistence/shadow mode is a non-goal, deferred to Hermes/4.6.
10. `AdvisoryResult.recommendation` is a required, closed-vocabulary field
    (`continue`, `stop-recommended`, `insufficient-evidence`) derived
    deterministically from `classification` and per-obligation `stable`,
    per the three-step rule above.
11. Each `obligations[]` entry carries a structured reason (`obligationId`,
    `status`, `classification`, `stable`, `evidenceEventIds`) with no field
    generated from `description` free text.
12. `generatedAt` on both artifacts is the fixed sentinel
    `1970-01-01T00:00:00.0000000Z`, not a wall-clock timestamp.
13. `AdvisoryDivergenceReport.reportId` is deterministically derived from
    source digest/inputs, never a random UUID.

Any change to these thirteen points requires a new human gate and a
superseding ADR.

### Security

`advise` and `AdvisoryDivergenceReport` generation are read-only over local
files: no command execution, no network access, no credential handling. All
three `advise` inputs are validated against their existing schemas before
use; malformed or cross-referenced-inconsistent input fails closed (see
Diagnostics) and produces no partial `AdvisoryResult`. `sourceDigest` /
`advisorySourceDigest` fields provide tamper-evidence, not authentication.

### Ownership

Owned by the ProgressTrace core contract maintainers, the same owners as
`agent-session.schema.json` and `gate-outcome.schema.json`. Changes to the
normative fields of either new schema require a superseding ADR and a new
human gate, consistent with how Phases 4.1–4.3 were governed.

### Compatibility

`AdvisoryResult` and `AdvisoryDivergenceReport` both start at schema version
`1.0`. Within `1.0`, only new optional, diagnostic-only fields may be added
(e.g. extending `AdvisoryDivergenceReport.rollup`); no required or normative
field may change shape or vocabulary without a new `schemaVersion`. This
matches the compatibility posture already established for
`gate-outcome.schema.json` and `agent-session.schema.json`.

## Consequences

- Callers get a live, read-only advisory snapshot without any risk of it
  altering canonical session-boundary evaluation, since it calls the same
  unmodified `Evaluator.Evaluate`.
- `AdvisoryDivergenceReport` gives a normative, per-obligation signal for
  detecting stale or drifted ledgers, while explicitly not promising a
  binding session-level verdict via `rollup`.
- Because `SessionTraceBuilder` is untouched, no existing session-boundary
  behavior changes as a result of this ADR.
- Deferring persistence and shadow mode keeps this phase's blast radius to
  pure, single-invocation, local file-to-file transformations.
- `recommendation` gives callers a single, deterministic next-step signal
  without adding a second evaluation authority or weakening the advisory-only,
  non-blocking posture.
- Structured per-obligation reasons (`classification`, `stable`,
  `evidenceEventIds`) make `AdvisoryResult` auditable without free text
  generated from `description`.
- A fixed `generatedAt` sentinel and a digest-derived `reportId` make both
  artifacts byte-identical for identical inputs, closing the
  non-determinism gap that would otherwise block reliable comparison of two
  advisory runs over the same evidence.
