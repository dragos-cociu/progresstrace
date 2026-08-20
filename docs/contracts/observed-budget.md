# ObservedBudget 1.0 normative contract

`ObservedBudget` is the output of the `budget` operation: a read-only
report that combines a validated `AgentSession`, `TokenUsage`, and
`ObligationLedger` into one per-obligation view of observed invocation
count, elapsed time, and tokens, juxtaposed next to the ledger's own
declared status. It renders no efficiency, waste, over-budget, or
under-budget verdict of any kind. See
[ADR-0009](../architecture/ADR-0009-observed-budget.md) for the approved
decision record.

## Scope

`budget` accepts exactly three inputs, each a path to an already-materialized
local artifact:

- `session-path` — an `AgentSession` document.
- `token-usage-path` — a `TokenUsage` document
  (`docs/contracts/token-usage.md`), paired with `session-path`.
- `ledger-path` — an `ObligationLedger` document, generated ahead of time
  and supplied externally. The ledger is mandatory in every invocation;
  there is no ledger-less mode.

No other input is read. `budget` never executes a command, reads
conversation text, calls a model, or performs an online action.

## Required fields

`schemaVersion` is `1.0`. `sessionId` and `taskContractId` are taken from
`session-path`. `ledgerTraceId` is the `traceId` of the supplied ledger.
`generatedAt` is the fixed sentinel `1970-01-01T00:00:00.0000000Z` (see
"Determinism" below), not a wall-clock capture. `sourceDigest` is a
lowercase SHA-256 digest over the three canonicalized inputs, for
provenance and tamper-evidence — not authentication. `obligations` is
required.

## Cross-reference between the three inputs

`ledgerTraceId` (the paired `ObligationLedger.traceId`) must equal the
`traceId` of `session-path`'s earliest invocation, ordered by `sequence` —
the same session-to-ledger cross-reference convention already fixed for
`advise`'s `session-path`/`ledger-path` pair
(`docs/architecture/ADR-0008-in-flight-advisory-assessment.md`, `PT700`).
`token-usage-path`'s own `sessionId` and `taskContractId` must equal
`session-path`'s. Either mismatch fails closed with `PT800`.

## Per-obligation observation

`obligations` is a non-empty array, ordered exactly as the paired
`ObligationLedger.obligations[]` array — the ledger's own declaration
order. Each entry juxtaposes observed activity next to the ledger's
existing status, with no field expressing a verdict about that activity:

- `obligationId`.
- `ledgerStatus` — the existing ledger signal vocabulary (`open`,
  `in-progress`, `satisfied`, `regressed`, `abandoned`): this obligation's
  last signal status in `ledger-path`, or `open` if the obligation has no
  signals, computed the same way
  `AdvisoryDivergenceReport.divergences[].ledgerStatus` already is
  (ADR-0008).
- `observedInvocationCount` — the count of `session-path`'s
  `invocations[]` whose `obligationIds` contains this obligation's id.
  Derived entirely from `session-path`; `TokenUsage` and `ledger-path` play
  no part in this count.
- `observedElapsedMillis` — the sum, over that same set of invocations, of
  each invocation's validated `endedAt - startedAt` duration in
  milliseconds. A 64-bit nonnegative integer (`0..9223372036854775807`,
  `Int64.MaxValue`); never `null`, since it depends only on the
  already-required `AgentSession` fields.
- `observedTokensTotal` — the sum, over that same set of invocations, of
  each invocation's `TokenUsage.records[].tokensTotal`, or `null` if that
  set includes any invocation with a missing token record (see "Missing
  token records" below). When present, a 64-bit nonnegative integer
  (`0..9223372036854775807`): a per-obligation sum over many invocations is
  bounded independently of each individual record's own
  `0..2147483647` bound.

An invocation that targets more than one obligation contributes its full
`observedElapsedMillis` and, when present, its full `tokensTotal` to each
targeted obligation's row independently. `ObservedBudget` performs no
partition or proportional allocation of a shared invocation's activity
across the obligations it targets; per-obligation totals are a juxtaposed
view of shared activity, not a partition of it, and are not expected to
sum to a session-wide total across obligations.

## Invocation window validation

`AgentSession`'s own schema does not assert `endedAt >= startedAt` for an
invocation. `budget` performs that check while computing
`observedElapsedMillis`: an invocation whose `startedAt`/`endedAt` cannot
be parsed as ISO-8601, or whose `endedAt` precedes its `startedAt`, fails
closed (`PT803`) rather than being skipped or clamped to zero. This
validation is local to `ObservedBudget` assembly and does not change
`AgentSession`'s own schema or existing validation behavior.

## Missing token records

An invocation in `observedInvocationCount`'s set with no corresponding
`TokenUsage` record is a missing token record. It produces one
informational `PT806` diagnostic per invocation, and forces
`observedTokensTotal` to `null` for every obligation whose observed
invocation set includes that invocation. A missing constituent is never
dropped silently from a sum that would otherwise look complete: `null`
means "not computable from the evidence given," distinct from a computed
`0`, which means "computed and zero token records existed with that
total." `PT806` is the only `PT800`-range code that does not suppress
output: `budget` still writes a complete, normalized `ObservedBudget` and
exits `0` when one or more `PT806` diagnostics apply, mirroring `PT703`'s
existing non-blocking precedent (ADR-0008).

## Overflow

Both `observedElapsedMillis` and `observedTokensTotal` are computed with
overflow-checked arithmetic. If either sum would exceed its 64-bit
nonnegative bound, `budget` fails closed (`PT804`) and writes no partial
`ObservedBudget`. Overflow is never silently wrapped, truncated, or
saturated to the maximum value.

## No efficiency or waste verdict

`ObservedBudget.obligations[]` carries no `applicability` field, no
comparison against an authored or historical baseline, and no derived
rank or score. A reader who wants an efficiency or waste verdict must
compute it themselves from the observed numbers and the existing
`BaselineComparisonResult` (`docs/contracts/baseline-comparison-result.md`)
contract; `ObservedBudget` does not produce one.

## Fail-closed behavior

Cross-reference mismatches between `session-path`, `token-usage-path`, and
`ledger-path` (`PT800`), an inadmissible `TokenUsage` record (`PT801`), a
duplicate `TokenUsage` record for one invocation (`PT802`), an incoherent
invocation window (`PT803`), aggregate overflow (`PT804`), and
non-deterministic assembly (`PT805`) all fail closed and produce no
partial `ObservedBudget`. Diagnostic messages do not echo source values.

Normalization writes a stable property order and a trailing newline;
parse/normalize is byte-idempotent, and identical inputs always produce a
byte-identical `ObservedBudget`.

## Determinism

`generatedAt` is fixed to the sentinel `1970-01-01T00:00:00.0000000Z` for
every `ObservedBudget`, enforced as a schema `const` — the same
deterministic-generation-timestamp convention already established for
`LedgerGenerationReport.generatedAt` and `AdvisoryResult.generatedAt`. It
is never read from wall-clock time.

`sourceDigest` is the lowercase hex-encoded SHA-256 digest of the
concatenation, in the fixed order `AgentSession`, `TokenUsage`,
`ObligationLedger`, of each input's own canonical normalized bytes, with no
additional wrapping or separator — the same digest algorithm and
canonicalization convention already used for `sourceDigest` /
`advisorySourceDigest` / `ledgerDigest` / `reportId` elsewhere in this
repository. Identical `session-path`/`token-usage-path`/`ledger-path`
inputs always produce a byte-identical `ObservedBudget`, including
`generatedAt` and `sourceDigest` themselves.

## CLI

```
progresstrace budget \
  --session-path <path> \
  --token-usage-path <path> \
  --ledger-path <path> \
  [--out <path>]
```

Writes a normalized `ObservedBudget` document to `--out`, or to stdout if
omitted. Exits `0` on success, including when one or more `PT806`
diagnostics apply. Exits non-zero with a `PT800`-range diagnostic on any
other fail-closed condition; never writes a partial result on failure.

## Non-goals

`ObservedBudget` is local, read-only, and scoped to exactly one session:
no network access, no model call, no persistence beyond the invocation
that produced it, no "shadow mode," no automatic stopping or escalation,
no pricing or cost-in-dollars field, and no cross-session aggregation. It
renders no efficiency, waste, over-budget, or under-budget verdict. See
[ADR-0009](../architecture/ADR-0009-observed-budget.md)'s "Non-goals" for
the complete list.

## Security

Read-only over local files: no command execution, no network access, no
credential handling. All three inputs are schema-validated before use.

### Provenance and security caveat

`ObservedBudget` faithfully aggregates and juxtaposes the token claims
recorded in `TokenUsage` against the ledger's status; it performs no
independent verification of those claims. See
`docs/contracts/token-usage.md`'s "Provenance and security caveat" for the
limits of what a validated `TokenUsage` record establishes about the truth
of a reported token count — those same limits apply, unchanged, to every
`observedTokensTotal` value in an `ObservedBudget`.

## Ownership

Owned by the ProgressTrace core contract maintainers. Changes to required
or normative fields require a superseding ADR and a new human gate.

## Compatibility

Schema version `1.0`. Only new optional fields may be added within `1.0`;
any change to a required field's shape or vocabulary requires a new
`schemaVersion`. `ObservedBudget` versions independently of `AgentSession`,
`TokenUsage`, and `ObligationLedger`.

## Synthetic example

Illustrative of document shape only; every normative rule is fixed above.
Correlates to a ledger with one obligation, `obl-1`, `satisfied`, observed
across two invocations, one of which has a missing token record.

```json
{
  "schemaVersion": "1.0",
  "sessionId": "session-example-1",
  "taskContractId": "phase-x-example",
  "ledgerTraceId": "trace-example-1",
  "generatedAt": "1970-01-01T00:00:00.0000000Z",
  "sourceDigest": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
  "obligations": [
    {
      "obligationId": "obl-1",
      "ledgerStatus": "satisfied",
      "observedInvocationCount": 2,
      "observedElapsedMillis": 4820,
      "observedTokensTotal": null
    }
  ]
}
```

`observedTokensTotal` is `null` here because one of the two invocations
targeting `obl-1` has no corresponding `TokenUsage` record, per "Missing
token records" above; `observedInvocationCount` and
`observedElapsedMillis` remain fully computed, since both depend only on
`session-path`.
