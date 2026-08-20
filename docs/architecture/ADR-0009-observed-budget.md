# ADR-0009: Observed Budget Architecture

## Status

Accepted (human gate approved, 2026-08-20). Scope frozen to the six
artifacts listed in "Deliverables"; implementation code, fixtures, and
tests are out of scope for this ADR and are tracked separately, following
the same architecture/implementation split established by
[ADR-0008](ADR-0008-in-flight-advisory-assessment.md).

## Context

Phases 4.1-4.4 established `AgentSession` (Phase 4.1), `GateOutcome`
(Phase 4.2), the generated `ObligationLedger` (Phase 4.3), and the
in-flight `AdvisoryResult`/`AdvisoryDivergenceReport` pair (Phase 4.4).
None of these carry token usage or elapsed-time observation: `AgentSession`
records invocation identity, targeting, and timing windows, but not what
those invocations cost in tokens; `ObligationLedger` records obligation
status signals, but not the invocation-level activity that produced them.

There is a recurring need to see, per obligation, how much observed
activity (invocation count, elapsed time, tokens) went into the status the
ledger already declares — without inventing a second, competing verdict
about whether that activity was efficient or wasteful, and without
touching the frozen `AgentSession` contract to add a field it was never
designed to carry. Phase 4.5 defines the contracts for that read-only,
juxtaposing observation.

## Decision

### Scope

- Define `TokenUsage` (`contracts/token-usage.schema.json`,
  `docs/contracts/token-usage.md`): a new, additive `1.0` sidecar that
  records per-invocation token totals and their provenance, keyed against
  an existing `AgentSession`. `AgentSession` itself is never modified.
- Define `ObservedBudget` (`contracts/observed-budget.schema.json`,
  `docs/contracts/observed-budget.md`): a generated report that combines a
  validated `AgentSession`, `TokenUsage`, and `ObligationLedger` into one
  per-obligation view of observed invocation count, elapsed time, and
  tokens, juxtaposed next to the ledger's own declared status.
- Record the architecture/contract-definition task contract for this phase
  in `tasks/phase-4-5-observed-budget.json`.

### `TokenUsage` is additive, `AgentSession` is frozen

`TokenUsage` is a new, independent sidecar document, schema version `1.0`.
It is not a new field, property, or extension point on `AgentSession`;
`contracts/agent-session.schema.json` and `docs/contracts/agent-session.md`
are not modified by this ADR. `TokenUsage` instead references an
`AgentSession` by `sessionId`/`taskContractId` and keys its own records
against that session's invocations, the same additive-sidecar pattern
already established by `GateOutcome` (which references a session without
being embedded inside it) and by `AdvisoryResult`/`AdvisoryDivergenceReport`
(ADR-0008) relative to `ObligationLedger`.

### `TokenUsage` record admissibility

Each `TokenUsage.records[]` entry carries its own `sessionId`,
`invocationId`, and `sequence`, mirroring `GateOutcome`'s existing
per-record identity fields. A record is admissible only when its
`sessionId` equals the `TokenUsage` document's own `sessionId`, its
`invocationId` names an invocation declared in the paired `AgentSession`,
and its `sequence` equals that invocation's `sequence` — the same
admissibility rule already established for `GateOutcome` in
`docs/contracts/agent-session.md`'s "Cross-contract boundary" section,
applied unchanged to a new record type. At most one record may target a
given `invocationId`; a second record for the same invocation is a
duplicate, not an update or an accumulation.

### Token provenance

Each `TokenUsage.records[]` entry carries `producerType`, `producerName`,
`producerVersion`, `evidenceBasis`, reusing the same four-value
`producerType` vocabulary (`agent`, `harness`, `operator`, `adapter`)
already established by `baseline-definition.schema.json`,
`termination-declaration.schema.json`, and `stop-assessment-result.schema.json`.
`evidenceBasis` is a new, Phase-4.5-specific closed vocabulary describing
how the token count was obtained, distinct from those contracts' own
estimate-oriented `evidenceBasis` vocabularies because a token count is a
measurement claim, not an estimate: `provider-usage-field` (read from a
model/provider API's own usage field), `harness-metered` (counted by the
harness itself, for example via a local tokenizer), `agent-self-reported`
(claimed by the agent's own output), `adapter-inference` (inferred by an
adapter from other captured data). Unlike those three existing contracts,
`TokenUsage` defines no coherence rule coupling `producerType` to
`evidenceBasis`; all sixteen combinations are structurally valid. Adding
such a coupling later, if warranted, is an additive `1.0`-compatible
change to the validator, not a schema-shape change.

### `tokensTotal`: nonnegative, bounded, zero distinct from missing

`tokensTotal` is a required integer field of a present `TokenUsage` record:
`minimum: 0`, `maximum: 2147483647` (`Int32.MaxValue`), the same bounded-
integer convention already used for `eventBudget`
(`contracts/baseline-definition.schema.json`). A record with
`tokensTotal: 0` is present and valid — it asserts "zero tokens observed
for this invocation," a fact — and is never conflated with the absence of
a record for that invocation, which asserts nothing.

### Missing token record: informational, never a silent partial sum

`TokenUsage.records[]` need not contain an entry for every `AgentSession`
invocation. An invocation with no corresponding record is a missing token
record: `budget` still writes a complete `ObservedBudget`, but reports one
informational `PT806` diagnostic per such invocation and forces
`observedTokensTotal` to `null` for every obligation whose observed
invocation set includes that invocation. A missing constituent is never
dropped silently from a sum that would otherwise look complete; `null`
means "not computable from the evidence given," and is never conflated
with `0`, which means "computed and zero."

### `ObservedBudget` requires all three inputs; the ledger is mandatory

`ObservedBudget` requires `AgentSession`, `TokenUsage`, and
`ObligationLedger` as its three inputs; none is optional, and the ledger in
particular is mandatory in every invocation — there is no ledger-less mode.
This differs from `advise` (ADR-0008), which treats its supplied ledger as
one of three required inputs for the same reason: `ObservedBudget` cannot
juxtapose "existing ledger status" against observed activity if no ledger
is supplied.

`ledgerTraceId` (the paired `ObligationLedger.traceId`) must equal the
`traceId` of the `AgentSession`'s earliest invocation, ordered by
`sequence` — the same session-to-ledger cross-reference convention already
fixed for `advise`'s `session-path`/`ledger-path` pair
(`docs/architecture/ADR-0008-in-flight-advisory-assessment.md`, `PT700`).

### Invocation count and elapsed time are derived, not stored

`observedInvocationCount` and `observedElapsedMillis` are computed
entirely from the validated `AgentSession`, never read from `TokenUsage`.
For a given obligation, `observedInvocationCount` is the count of
`AgentSession.invocations[]` whose `obligationIds` contains that
obligation's id. `observedElapsedMillis` is computed by summing individual
`endedAt - startedAt` durations at native `TimeSpan` tick precision with
checked arithmetic, then converting the final per-obligation sum to integer
milliseconds by truncation toward zero. Individual durations are never
truncated or rounded before summation; rounding is never used. `AgentSession`'s existing schema does not itself assert
`endedAt >= startedAt`; `ObservedBudget` assembly performs that check as
part of "validating" each duration before summing it, and an invocation
whose window cannot be parsed as ISO-8601 or whose `endedAt` precedes its
`startedAt` fails closed (`PT803`) rather than being skipped or clamped to
zero. `observedElapsedMillis` is a 64-bit nonnegative integer
(`minimum: 0`, `maximum: 9223372036854775807`, `Int64.MaxValue`); it is
never `null`, since it depends only on the already-required `AgentSession`
fields.

An invocation that targets more than one obligation (a non-empty
`obligationIds` with more than one entry) contributes its full elapsed
time and, when present, its full `tokensTotal` to each targeted
obligation's row independently. `ObservedBudget` performs no partition or
proportional allocation of a shared invocation's activity across the
obligations it targets; per-obligation totals are a juxtaposed view of
shared activity, not a partition of it, and are not expected to sum to a
session-wide total across obligations.

### Tokens are summed per obligation, or null

`observedTokensTotal`, per obligation, is the sum of `tokensTotal` over
every `TokenUsage` record whose `invocationId` is in that obligation's
observed invocation set, or `null` if that set includes any invocation
with a missing token record (see "Missing token record" above). When
present, it is a 64-bit nonnegative integer
(`minimum: 0`, `maximum: 9223372036854775807`): although each individual
record is bounded by `Int32.MaxValue`, a per-obligation sum over many
invocations is bounded independently at `Int64.MaxValue`.

### Overflow fails closed

Both aggregate sums — `observedElapsedMillis` and `observedTokensTotal` —
are computed with overflow-checked arithmetic. If either sum would exceed
its 64-bit nonnegative bound, `budget` fails closed (`PT804`) and writes no
partial `ObservedBudget`. Overflow is never silently wrapped, truncated, or
saturated to the maximum value.

### No efficiency or waste verdict

`ObservedBudget.obligations[]` juxtaposes each obligation's
`observedInvocationCount`, `observedElapsedMillis`, and
`observedTokensTotal` next to that obligation's existing `ledgerStatus`
(the ledger's own last-signal status for that obligation, computed the
same way `AdvisoryDivergenceReport.divergences[].ledgerStatus` already is
— ADR-0008). It renders no efficiency, waste, over-budget, or under-budget
verdict of any kind: no `applicability` field, no comparison against an
authored or historical baseline, and no derived rank or score. A reader
who wants such a verdict must compute it themselves from the observed
numbers and the existing `BaselineComparisonResult`
(`docs/contracts/baseline-comparison-result.md`) contract; `ObservedBudget`
does not produce one, and this ADR does not authorize a future minor
version of `1.0` to add one silently.

### CLI verb

The CLI verb is `budget`, taking exactly three required inputs and one
optional output path, following the same flag-based invocation shape
`advise` established (ADR-0008):

```
progresstrace budget \
  --session-path <path> \
  --token-usage-path <path> \
  --ledger-path <path> \
  [--out <path>]
```

### Diagnostics

Phase 4.5 diagnostics are allocated the `PT800`-`PT899` block, explicit
and non-overlapping with every prior phase:

| Code  | Condition |
|-------|-----------|
| PT800 | `token-usage-path`'s `sessionId`/`taskContractId` does not match `session-path`, or `ledger-path`'s `traceId` does not equal `session-path`'s earliest-invocation (by sequence) `traceId` |
| PT801 | A `TokenUsage` record is inadmissible: its `sessionId` does not match the document's own `sessionId`, its `invocationId` does not name an invocation in `session-path`, or its `sequence` does not equal that invocation's `sequence` |
| PT802 | Two or more `TokenUsage.records[]` entries target the same `invocationId` |
| PT803 | An `AgentSession` invocation's `startedAt`/`endedAt` window cannot be parsed as ISO-8601, or `endedAt` precedes `startedAt` |
| PT804 | Overflow: summing per-invocation elapsed durations or `tokensTotal` values for one obligation would exceed the 64-bit nonnegative bound |
| PT805 | `ObservedBudget` cannot be normalized deterministically from the given inputs (an internal assembly invariant is violated, for example an obligation id present in `ledger-path` is absent from the assembled `obligations[]`, or vice versa) |
| PT806 | Informational, non-blocking: an `AgentSession` invocation has no corresponding `TokenUsage` record |

`TokenUsage`'s own Phase A structural validation (malformed JSON, wrong
type, missing/unknown property, duplicate property name, oversized input,
unsupported `schemaVersion`, an out-of-range or empty field value) reuses
the existing generic codes `PT000`-`PT005` and `PT100`-`PT101` unmodified,
exactly as `ObligationLedger`'s own Phase A does
(`docs/contracts/obligation-ledger.md`); Phase 4.5 defines no new codes for
that layer. `PT806` is the only `PT800`-range code that does not suppress
output: `budget` still writes a complete, normalized `ObservedBudget` and
exits `0` when one or more `PT806` diagnostics apply, mirroring
`PT703`'s existing non-blocking precedent (ADR-0008). Every other
`PT800`-range code is fail-closed: no partial `ObservedBudget` is ever
written. Diagnostic messages do not echo source values, matching the
existing convention in `docs/contracts/gate-outcome.md`.

### Determinism

`ObservedBudget.generatedAt` is fixed to the canonical sentinel
`1970-01-01T00:00:00.0000000Z`, enforced as a schema `const` — the same
deterministic-generation-timestamp convention already established for
`LedgerGenerationReport.generatedAt` and `AdvisoryResult.generatedAt`. It
is never read from wall-clock time.

`ObservedBudget.sourceDigest` is the lowercase hex-encoded SHA-256 digest
of the concatenation, in the fixed order `AgentSession`, `TokenUsage`,
`ObligationLedger`, of each input's own canonical normalized bytes, with no
additional wrapping or separator — the same digest algorithm and
canonicalization convention already used for `sourceDigest` /
`advisorySourceDigest` / `ledgerDigest` / `reportId` elsewhere in this
repository. Identical `session-path`/`token-usage-path`/`ledger-path`
inputs always produce a byte-identical `ObservedBudget`, including
`generatedAt` and `sourceDigest` themselves.

`ObservedBudget.obligations[]` is ordered exactly as the paired
`ObligationLedger.obligations[]` array — the ledger's own declaration
order, not an id sort or an insertion order derived from `TokenUsage` or
`AgentSession` — the same "same order as the source ledger's obligations
array" convention already fixed for `LedgerGenerationReport.obligations[]`.

### Local, read-only, no pricing

`budget` and `ObservedBudget` generation are local and read-only over
supplied file paths: no network access, no model call, no persistence
beyond the single invocation that produces the output, no "shadow mode,"
no automatic stopping, and no cross-session aggregation — an `ObservedBudget`
describes exactly one session. `ObservedBudget` carries no pricing or
cost-in-dollars field of any kind; tokens are reported as a count, never
converted to a currency amount. Any future pricing or cost feature is
explicitly out of scope for `1.0` and requires its own ADR and human gate.

### Provenance and security caveat

Reported token counts are structurally validated — schema-conformant,
admissible against the paired `AgentSession`, internally consistent under
the aggregation rules above — but they are not independently truth-verified.
`TokenUsage` records what a producer (`agent`, `harness`, `operator`, or
`adapter`) claims, tagged with `evidenceBasis` describing the claim's
origin; `ObservedBudget` faithfully aggregates and juxtaposes those claims
against the ledger's status without re-deriving or auditing them against
an independent token count. A caller that needs an independently
verified count must supply `TokenUsage` records whose `evidenceBasis` and
`producerType` already reflect that stronger provenance (for example
`provider-usage-field` from a trusted provider integration); `ObservedBudget`
itself performs no such verification and makes no claim of having done so.

### Non-goals

- Any change to `AgentSession`, `GateOutcome`, `AdvisoryResult`,
  `AdvisoryDivergenceReport`, `ObligationLedger`, `eventBudget`
  (`contracts/baseline-definition.schema.json`), `Evaluator`,
  `SessionEvaluator`, or any existing CLI verb's behavior or diagnostics
  `PT000`-`PT706`.
- Any change to the `benchmark` corpus, harness, or annotation tooling
  (`docs/benchmark-analysis.md`, `docs/benchmark-annotation-rubric.md`,
  `annotations/**`).
- Network access, model calls, or LLM-as-judge of any kind.
- Persistence of `TokenUsage` or `ObservedBudget` beyond the invocation
  that produced or consumed them; no database, cache, or state file.
- "Shadow mode" (continuous background budget computation running
  alongside a live session), automatic stopping, or escalation triggered
  by observed activity, all deferred to Hermes / a later phase, consistent
  with ADR-0008's existing deferral.
- Pricing, cost-in-dollars, or any currency-denominated field.
- Cross-session aggregation of any kind; `ObservedBudget` is scoped to
  exactly one session.
- Any efficiency, waste, over-budget, or under-budget verdict.
- Implementation code, fixtures, conformance tests, or CI wiring for
  `TokenUsage` or `ObservedBudget`; tracked by a separate implementation
  task contract, not created by this ADR.

### Allowed / protected paths

Allowed for this ADR's follow-on implementation work:

- `contracts/token-usage.schema.json`
- `contracts/observed-budget.schema.json`
- `docs/contracts/token-usage.md`
- `docs/contracts/observed-budget.md`
- `docs/architecture/ADR-0009-observed-budget.md`
- `tasks/phase-4-5-observed-budget.json`
- A new, additive `budget` implementation module (path to be fixed by the
  implementation task, not this ADR).

Protected (no modification under this ADR):

- `contracts/agent-session.schema.json`
- `contracts/gate-outcome.schema.json`
- `contracts/obligation-ledger.schema.json`
- `contracts/advisory-result.schema.json`
- `contracts/advisory-divergence-report.schema.json`
- `contracts/baseline-definition.schema.json`
- `contracts/baseline-comparison-result.schema.json`
- `docs/contracts/agent-session.md`
- `docs/contracts/gate-outcome.md`
- `docs/contracts/obligation-ledger.md`
- `docs/contracts/advisory-result.md`
- `docs/contracts/advisory-divergence-report.md`
- Existing `Evaluator`, `SessionEvaluator`, `StopAssessor`,
  `GateOutcomeProjector`, `SessionTraceBuilder`, and `Advisory` assembly
  implementations, and every existing CLI verb's behavior.

### Human gates

This ADR itself is the record of the human gate that approved:

1. `TokenUsage` is a new, additive `1.0` sidecar; `AgentSession` is never
   modified.
2. `observedInvocationCount` is derived from validated `AgentSession`
   invocations, never from `TokenUsage` or `ObligationLedger`.
3. `observedElapsedMillis` is the sum of validated per-invocation
   `endedAt - startedAt` durations; it is a 64-bit nonnegative integer.
4. `TokenUsage.records[]` entries are keyed and cross-validated by
   `sessionId`, `invocationId`, `sequence` against the paired
   `AgentSession`.
5. Each `TokenUsage.records[]` entry carries `producerType`,
   `producerName`, `producerVersion`, `evidenceBasis` provenance.
6. `tokensTotal` is a nonnegative bounded integer; zero is a distinct,
   valid, present value from a missing record.
7. A missing token record yields an informational `PT806` diagnostic per
   invocation and forces `null` for any `observedTokensTotal` that would
   sum a missing constituent; a missing constituent is never silently
   dropped from the sum.
8. Arithmetic overflow, in either aggregate sum, fails closed (`PT804`)
   rather than wrapping, truncating, or saturating.
9. `ObservedBudget` requires `AgentSession`, `TokenUsage`, and
   `ObligationLedger`; the ledger is mandatory, never optional.
10. `ObservedBudget.obligations[]` juxtaposes observed invocation count,
    tokens, and elapsed time next to the existing ledger-declared status;
    it renders no efficiency or waste verdict.
11. The CLI verb is `budget`.
12. `PT800`-`PT899` is explicitly allocated to this phase.
13. `generatedAt` is the fixed sentinel `1970-01-01T00:00:00.0000000Z`;
    `sourceDigest` is the SHA-256 digest over the normalized `AgentSession`,
    `TokenUsage`, and `ObligationLedger`, concatenated in that fixed order;
    `obligations[]` is ordered exactly as `ObligationLedger.obligations[]`.
14. `ObservedBudget` is local and read-only: no network access, no model
    call, no persistence, no shadow mode, no auto-stop, no pricing or
    cost-in-dollars field, no cross-session aggregation.
15. No change to `AgentSession`, `GateOutcome`, `AdvisoryResult`,
    `AdvisoryDivergenceReport`, `ObligationLedger`, `eventBudget`,
    `benchmark`, `Evaluator`, `SessionEvaluator`, existing CLI behavior, or
    diagnostics `PT000`-`PT706`.

Any change to these fifteen points requires a new human gate and a
superseding ADR.

### Security

`budget` and `TokenUsage`/`ObservedBudget` generation are read-only over
local files: no command execution, no network access, no credential
handling. All three `budget` inputs are validated before use; malformed or
cross-referenced-inconsistent input fails closed (see "Diagnostics") and
produces no partial `ObservedBudget`. `sourceDigest` provides
tamper-evidence, not authentication. See "Provenance and security caveat"
above for the specific limits of what a validated `TokenUsage` record
does and does not establish about the truth of a reported token count.

### Ownership

Owned by the ProgressTrace core contract maintainers, the same owners as
`agent-session.schema.json` and `obligation-ledger.schema.json`. Changes to
the normative fields of either new schema require a superseding ADR and a
new human gate, consistent with how Phases 4.1-4.4 were governed.

### Compatibility

`TokenUsage` and `ObservedBudget` both start at schema version `1.0`, and
version independently of each other and of `AgentSession`/`ObligationLedger`:
a change to one never forces a change to another. Within `1.0`, only new
optional, diagnostic-only fields may be added; no required or normative
field, closed vocabulary, or diagnostic code's meaning may change shape
without a new `schemaVersion`. This matches the compatibility posture
already established for every other contract in this repository.

## Consequences

- Callers get a per-obligation observed-activity view — invocation count,
  elapsed time, tokens — placed directly next to the existing ledger
  status, without a second contract inventing its own status vocabulary or
  competing with `ObligationLedger`'s own.
- Because `AgentSession` is untouched, no existing session-boundary
  behavior, fixture, or conformance test changes as a result of this ADR.
- `TokenUsage`'s admissibility rule, reused unchanged from `GateOutcome`,
  keeps token evidence bound to real, declared invocations rather than
  free-floating records that could be attributed to the wrong session.
- Treating a missing token record as informational (`PT806`) rather than
  fail-closed lets `ObservedBudget` still report an obligation's invocation
  count and elapsed time — both fully derivable from `AgentSession` alone —
  even when token capture was incomplete, while `null` on
  `observedTokensTotal` keeps that incompleteness from ever being mistaken
  for a computed zero.
- Explicitly excluding an efficiency or waste verdict, and any
  pricing/cost-in-dollars field, keeps `ObservedBudget` a pure observation
  contract: a future comparison or pricing feature can be layered on top
  as its own ADR without ever requiring `ObservedBudget` itself to change
  shape.
- A fixed `generatedAt` sentinel and a digest computed over all three
  canonicalized inputs in a fixed order make `ObservedBudget`
  byte-identical for identical inputs, closing the same non-determinism
  gap ADR-0008 closed for `AdvisoryResult`.
