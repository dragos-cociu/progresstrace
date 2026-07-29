# Evaluation result

Version `1.0` is the Phase 1 evaluation-result contract. It is the
versioned, deterministic output of `evaluate <trace-path> <ledger-path>`: a
per-obligation and trace-level classification of whether a trace's explicit
obligations progressed, stagnated, regressed, or lack sufficient evidence.
The normative machine-readable contract is
`contracts/evaluation-result.schema.json`, implemented by Codex from this
document; the .NET Core's `Evaluator` in the `ProgressTrace.Core.Evaluation`
namespace applies the classification table and precedence rule below as a
pure, deterministic function with no hidden state, clock, network,
database, or model dependency.

## Purpose

`EvaluationResult` reports, for a normalized trace envelope and a
structurally and referentially valid obligation ledger, an exact
deterministic classification of every obligation and of the trace overall,
plus evidence-event references and algorithm identity sufficient to
reproduce or compare a result later. Baseline comparison and false-halt or
late-halt cost are explicitly out of scope for Phase 1; see "Baseline and
cost fields" below.

## Preconditions

An obligation ledger must already be structurally and referentially valid,
per `docs/contracts/obligation-ledger.md`, before evaluation runs. An empty
`obligations` array is invalid before evaluation and is rejected at
validation (exit 1); evaluation never receives a ledger with zero
obligations.

## Document structure

- `schemaVersion`: contract major/minor version. Phase 1 accepts only `1.0`.
- `traceId`: the evaluated trace envelope's `traceId`.
- `algorithm`: required object identifying the deterministic evaluator that
  produced this result, for future reproducibility and comparison.
- `algorithm.name`: required string, the fixed constant value
  `"progresstrace-evaluator"` for every Phase 1 `1.0` result; the evaluator
  never emits any other value.
- `algorithm.version`: required string, the fixed constant value `"1.0.0"`
  for the Phase 1 evaluator; this identifies the evaluator implementation
  version, not the contract's `schemaVersion`, and changes only when the
  evaluator's normative behavior changes.
- `obligationResults`: array of per-obligation classification results, one
  entry per obligation declared in the paired ledger; see "obligationResults
  ordering" below for its exact order.
- `obligationResults[].obligationId`: the classified obligation's id.
- `obligationResults[].classification`: one of the fixed enum `progress`,
  `stagnation`, `regression`, `insufficient-evidence`.
- `obligationResults[].evidenceEventIds`: array of event ids from the
  source trace that the classification was computed from; every id in this
  array references an id present in the source trace. See "evidenceEventIds
  content and ordering" below for its exact content, order, and duplicate
  policy.
- `traceClassification`: one of the same fixed enum `progress`,
  `stagnation`, `regression`, `insufficient-evidence`; the trace-level
  classification computed by the precedence rule below.

## Field types and requiredness

The root object is a JSON object with `additionalProperties: false`;
unknown top-level members are rejected. All five root properties are
required: `schemaVersion` (string, exactly `"1.0"`), `traceId` (string,
non-empty, equal to the evaluated trace envelope's `traceId`), `algorithm`
(object), `obligationResults` (array; one entry per obligation in the
paired ledger, never empty, since an empty `obligations` array is rejected
before evaluation per "Preconditions" above), and `traceClassification`
(string, one of the fixed enum).

`algorithm` is a JSON object with `additionalProperties: false`; both
properties are required: `name` (string, fixed constant
`"progresstrace-evaluator"`) and `version` (string, fixed constant
`"1.0.0"`).

Each entry of `obligationResults` is a JSON object with
`additionalProperties: false`; all three properties are required:
`obligationId` (string, non-empty), `classification` (string, one of the
fixed enum `progress`, `stagnation`, `regression`, `insufficient-evidence`),
and `evidenceEventIds` (array of strings; may be empty, per the content and
order rule below).

## Per-obligation classification table

For each obligation, let `s1..sn` be its signals in the total order defined
in `docs/contracts/obligation-ledger.md`: ascending by the dereferenced
trace event's `sequence`, with ties on an identical `eventId` broken by
`signals` array index. Let `rank(open) = 0`, `rank(in-progress) = 1`,
`rank(satisfied) = 2`. The classification is computed exactly as follows,
with no other branch and no illustrative deviation:

- `n = 0` (no signal references this obligation): `insufficient-evidence`.
- `n >= 1` and `sn = abandoned`: `stagnation`. Abandoned-terminal
  validation in `docs/contracts/obligation-ledger.md` guarantees `abandoned`
  can only occupy the final position `sn`, never `s1..sn-1`.
- `n >= 1` and `sn = regressed`: `regression`.
- `n = 1` and `sn` in `{satisfied, in-progress}`: `progress`.
- `n = 1` and `sn = open`: `stagnation`.
- `n > 1` and `sn` in `{open, in-progress, satisfied}`: compare `rank(sn)`
  to `rank(s1)`, treating `s1 = regressed` as `rank -1` for this comparison
  only:
  - `rank(sn) > rank(s1)`: `progress`.
  - `rank(sn) = rank(s1)`: `stagnation` (the repeated-status case).
  - `rank(sn) < rank(s1)`: `regression`.

There is no `n > 1, s1 = abandoned` branch in this table. Abandoned-terminal
validation guarantees no signal follows an `abandoned` signal, so any
obligation whose first ordered signal is `abandoned` necessarily has
`n = 1`; an obligation with `n > 1` can therefore never have
`s1 = abandoned`. This branch is provably unreachable and is excluded
rather than left as an unspecified case.

## obligationResults ordering

`obligationResults` contains exactly one entry per obligation declared in
the paired obligation ledger, in the same order as that ledger's
`obligations` array. This is the sole deterministic source order for
`obligationResults`; it does not depend on signal timestamps, evidence
content, or classification outcome.

## evidenceEventIds content and ordering

`evidenceEventIds` contains the dereferenced `eventId` of every signal in
the obligation's total order `s1..sn`, defined in
`docs/contracts/obligation-ledger.md`, in that same order. Its length
always equals `n`, the obligation's total signal count: for `n = 0`
(`insufficient-evidence`), `evidenceEventIds` is the empty array `[]`. When
multiple signals for the same obligation dereference the same event id,
that event id appears once per contributing signal — duplicates are
preserved, not deduplicated — so `evidenceEventIds` always has exactly one
entry per signal that produced the classification.

## Trace-level precedence

Trace-level classification is the highest-precedence classification
present among all `obligationResults`, using the fixed order:

```
regression > insufficient-evidence > stagnation > progress
```

The trace classifies as `regression` if any obligation classifies as
`regression`; otherwise as `insufficient-evidence` if any obligation
classifies as `insufficient-evidence`; otherwise as `stagnation` if any
obligation classifies as `stagnation`; otherwise as `progress`. This rule is
presence-based, not count-based, so no tie case can arise.

## Canonical property order

Serialization emits properties in exactly this order at every level, with
no alternate or alphabetical ordering:

- Root object: `schemaVersion`, `traceId`, `algorithm`, `obligationResults`,
  `traceClassification`.
- `algorithm` object: `name`, `version`.
- Each `obligationResults` entry: `obligationId`, `classification`,
  `evidenceEventIds`.

## Canonical serialization

Serialization of `EvaluationResult` is canonical and byte-idempotent under
repeated normalization, matching `TraceNormalizer`'s guarantee: the
canonical property order above, UTF-8 encoding without a byte-order mark,
no insignificant whitespace between tokens, arrays serialized in the exact
order defined by this document (`obligationResults` per the ordering rule
above, `evidenceEventIds` per the content-and-ordering rule above) with no
re-sorting, and exactly one trailing newline byte terminating the document.
Normalizing the same `EvaluationResult` any number of times produces
byte-identical output.

## Baseline and cost fields

Baseline comparison and false-halt or late-halt cost are completely absent
from Phase 1: no field of `EvaluationResult` names, reserves, or implies a
baseline or a halt-cost value in this version of the contract. They are
explicitly deferred to Phase 2. Any future baseline or cost field is
additive-only, introduced in a later minor or major version, and is never
inserted retroactively into the Phase 1 `1.0` shape.

## Version policy

A reader accepts only the schema versions it explicitly supports. This
contract's Phase 1 implementation accepts exactly `schemaVersion` `1.0` and
rejects an evaluation result, or the paired obligation ledger, declaring
any other value. Contract evolution distinguishes two kinds of change:

- **Additive compatible evolution**: a new optional property, such as a
  future baseline or cost field (see "Baseline and cost fields" above),
  that does not alter any existing required field, the closed
  classification enum, the per-obligation classification table, the
  trace-level precedence rule, or the canonical property order may be
  introduced in a later minor version, consumable by newer readers without
  breaking readers pinned to `1.0`.
- **Breaking change**: adding or changing a value in the closed
  classification enum, the classification table, the precedence rule, the
  signal total-order or abandoned-terminal rule it depends on, the
  canonical property order, or any required field is a breaking change. It
  requires a new major version and Dragos's approval, new conformance
  fixtures, and is never introduced as a minor-version patch or a silent
  change to `1.0`.

## Synthetic example

The following example is illustrative of document shape only; every
normative rule is fixed in the sections above. The example data is
synthetic and contains no employer, medical, credential, or production
content, and correlates to the single-signal, single-obligation ledger
example in `docs/contracts/obligation-ledger.md`:

```json
{
  "schemaVersion": "1.0",
  "traceId": "trace-example-1",
  "algorithm": { "name": "progresstrace-evaluator", "version": "1.0.0" },
  "obligationResults": [
    {
      "obligationId": "obl-1",
      "classification": "stagnation",
      "evidenceEventIds": ["evt-1"]
    }
  ],
  "traceClassification": "stagnation"
}
```
