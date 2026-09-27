# AdvisoryResult 1.0 normative contract

`AdvisoryResult` is the output of the `advise` operation: a read-only,
in-flight snapshot of what `Evaluator.Evaluate` would classify a session as,
given evidence collected so far. It is advisory only — it never stops,
interrupts, escalates, or alters canonical session-boundary evaluation. See
[ADR-0008](../architecture/ADR-0008-in-flight-advisory-assessment.md) for the
approved decision record.

## Scope

`advise` accepts exactly three inputs, each a path to an already-materialized
local artifact:

- `session-path` — an `AgentSession` document.
- `ledger-path` — an `ObligationLedger` document, generated ahead of time and
  supplied externally.
- `gate-outcomes-array-path` — a JSON array of `GateOutcome` records.

No other input is read. `advise` never executes `command`, reads
conversation text, calls a model, or performs an online action.

## Required fields

`schemaVersion` is `1.0`. `sessionId` and `taskContractId` identify the
session and its owning task contract, taken from `session-path`.
`ledgerTraceId` is the `traceId` of the supplied ledger. `generatedAt` is
the fixed sentinel `1970-01-01T00:00:00.0000000Z` (see "Determinism"
below), not a wall-clock capture. `sourceDigest` is a lowercase SHA-256
digest over the three canonicalized inputs, for provenance and
tamper-evidence — not authentication. `classification`, `recommendation`,
and `obligations` are required.

## Classification (revised in 1.0.0 bugfix, ADR-0017)

`Evaluator.Evaluate` still supplies `evidenceEventIds`. `status`, `classification` and
`stable` are derived in `advise` from the gate outcomes, because the evaluator's own
vocabulary (`regression`, `stagnation`) does not map onto the advisory vocabulary: the
earlier projection reported a current failure as `recovery-after-failed-attempt` and two
passing gates in one attempt as `repeated-attempt-without-obligation-advancement` (F1).

A gate is identified by its `command`. For each obligation only the **latest outcome of each
gate** counts (F5 — previously the single latest signal of any gate decided, so a failing gate
followed by a different passing gate reported `satisfied`):

| latest outcome per gate | `status` |
|---|---|
| none | `open` |
| at least one `fail`/`error` | `regressed` |
| all `pass` | `satisfied` |
| otherwise (`skipped`) | `in-progress` |

Per-obligation `classification` (closed vocabulary):

- `insufficient-evidence` — no outcome, or `status` is `open`/`in-progress`;
- `progress` — `satisfied` with no failing outcome for the obligation;
- `recovery-after-failed-attempt` — `satisfied` after at least one failing outcome;
- `repeated-attempt-without-obligation-advancement` — `regressed`, failing outcomes in at
  least two distinct attempts, and no passing outcome ever;
- `failed-attempt` — `regressed` in any other case (a current failure that has not been
  recovered). Added in the 1.0.0 bugfix: the four earlier values had no correct slot for it.
  **Compatibility note:** this extends the closed vocabulary of `AdvisoryResult 1.0` without a
  `schemaVersion` change; documents produced by `v1.0.0` may fail validation against the
  pre-`v1.0.0` schema. See ADR-0017, addendum.

Top-level `classification` is the first present per-obligation value in the priority order
`insufficient-evidence`, `failed-attempt`, `repeated-attempt-without-obligation-advancement`,
`recovery-after-failed-attempt`, else `progress`. `insufficient-evidence` therefore still takes
priority over partial progress elsewhere (known limitation F2: any obligation without a gate
keeps the top-level value and the recommendation at `insufficient-evidence`).

## Recommendation

`recommendation` is a required, closed-vocabulary field: exactly `continue`,
`stop-recommended`, or `insufficient-evidence`. It is a deterministic
derivation on top of `classification` and per-obligation `stable`, not a
second evaluation authority — it reads no additional input, calls no
additional evaluator, and never stops, interrupts, or escalates anything.
The rule, applied in order:

1. If `classification` is `insufficient-evidence`, `recommendation` is
   `insufficient-evidence`.
2. Otherwise, if every entry in `obligations` has `stable: true`,
   `recommendation` is `stop-recommended`.
3. Otherwise, `recommendation` is `continue`.

`obligations` is never empty (`minItems: 1`), so rule 2 cannot be satisfied
vacuously.

## Obligations and structured per-obligation reason

`obligations` is a non-empty array. Each entry reports a structured reason
with no field generated from free text:

- `obligationId`.
- `status` — the existing ledger signal vocabulary (`open`, `in-progress`,
  `satisfied`, `regressed`, `abandoned`), the advisory's current
  per-obligation status assessment. This is the field
  `AdvisoryDivergenceReport` compares against the supplied ledger's
  declared status.
- `classification` — per-obligation value of the closed vocabulary above
  (`progress`, `recovery-after-failed-attempt`,
  `repeated-attempt-without-obligation-advancement`, `failed-attempt`,
  `insufficient-evidence`).
- `stable` — `true` exactly when `status` is `satisfied` (every gate of the
  obligation passed on its latest outcome). Derived from the same effective
  status so it can never contradict it; `StopAssessor` is unaffected. This is
  the "outcome" the recommendation rule above reads.
- `evidenceEventIds` — the event ids backing this obligation's
  `classification` and `stable`, following the same content-and-ordering
  convention as `EvaluationResult.obligationResults[].evidenceEventIds`
  (`docs/contracts/evaluation-result.md`): is empty only when
  `classification` is `insufficient-evidence` for that obligation.

## SessionTraceBuilder boundary

`advise` does not route through `SessionTraceBuilder`. `SessionTraceBuilder`
currently builds its ledger internally and does not accept an
externally-supplied ledger; `advise` is a separate, parallel boundary that
reads `ledger-path` directly. Adding external-ledger support to
`SessionTraceBuilder` itself is additive future work, not covered by this
contract.

## Fail-closed behavior

Cross-reference mismatches between `session-path`, `ledger-path`, and
`gate-outcomes-array-path`, schema-invalid inputs, and non-deterministic
normalization all fail closed with a `PT700`-range diagnostic (see
[ADR-0008](../architecture/ADR-0008-in-flight-advisory-assessment.md)) and no
partial `AdvisoryResult`. Diagnostic messages do not echo source values.

Normalization writes a stable property order and a trailing newline;
parse/normalize is byte-idempotent, and identical inputs always produce a
byte-identical `AdvisoryResult`.

## Determinism

`generatedAt` is fixed to the sentinel `1970-01-01T00:00:00.0000000Z` for
every `AdvisoryResult`, enforced as a schema `const` — the same
deterministic-generation-timestamp convention already established for
`LedgerGenerationReport.generatedAt`
(`docs/contracts/ledger-generation-report.md`). It is never read from
wall-clock time, so identical `session-path`/`ledger-path`/
`gate-outcomes-array-path` inputs always normalize to a byte-identical
`AdvisoryResult`, including `generatedAt` itself.

## CLI

```
progresstrace advise \
  --session-path <path> \
  --ledger-path <path> \
  --gate-outcomes-array-path <path> \
  [--out <path>]
```

Writes a normalized `AdvisoryResult` document to `--out`, or to stdout if
omitted. Exits non-zero with a `PT700`-range diagnostic on any fail-closed
condition; never writes a partial result on failure.

## Non-goals

Persistence of `AdvisoryResult` beyond the invocation that produced it, and
"shadow mode" (continuous background advisory evaluation) are explicitly out
of scope and deferred to Hermes / Phase 4.6.

## Security

Read-only over local files: no command execution, no network access, no
credential handling. All three inputs are schema-validated before use.

## Ownership

Owned by the ProgressTrace core contract maintainers. Changes to required or
normative fields require a superseding ADR and a new human gate.

## Compatibility

Schema version `1.0`. Only new optional fields may be added within `1.0`; any
change to a required field's shape or vocabulary requires a new
`schemaVersion`.
