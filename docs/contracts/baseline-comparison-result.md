# Baseline comparison result

Version `1.0` is the Phase 2b `BaselineComparisonResult` contract. It is the
versioned, deterministic output of `compare <trace-path> <ledger-path>
<termination-declaration-path> <baseline-definition-path>`: a per-obligation
and trace-level classification of how an unmet obligation's observed
termination point relates to an externally authored counterfactual event
budget, always labeled as a comparison against an authored estimate and
never as an observed fact or as proof of the stopped run's own counterfactual
future. The normative machine-readable contract is
`contracts/baseline-comparison-result.schema.json`, to be implemented by
Codex from this document once a task contract authorizes Task B
implementation; this document fixes the contract now and authorizes no
implementation. The .NET Core's baseline-comparison algorithm applies the
unit, formula, and rollup rules below as a pure, deterministic function with
no hidden state, clock, network, database, or model dependency. It invokes,
but never reimplements, the unmodified `ProgressTrace.Core.Assessment`
stop-assessment logic and the unmodified
`ProgressTrace.Core.Evaluation.Evaluator`.

## Purpose

`BaselineComparisonResult` reports, for a normalized trace envelope, a
structurally and referentially valid obligation ledger, a structurally and
referentially valid termination declaration, and a structurally,
referentially, and coverage-valid baseline definition, an exact
single-comparison classification of whether each unmet obligation stopped
with authored event budget left unused, exhausted its authored budget
exactly, or exceeded it, plus a trace-level rollup of whether any such
unused-budget obligation exists at all. The phrase "false halt" appears in
this document only inside the explicitly qualified public field name
`authoredEstimateFalseHaltPresent` and in explanatory prose describing it;
no bare `falseHalt`-named field and no bare `falseHaltMagnitude` field
exists anywhere in this contract. `authoredEstimateFalseHaltPresent`'s
`true` value is an authored-estimate-relative proxy only: it never asserts
that an actual false halt occurred — see "Interpretation and non-claims"
below for the exact boundary of what a positive result does and does not
claim.

`docs/architecture/ADR-0004-baseline-comparison-and-authored-estimate.md`
fixes the evidence model this contract implements: every `eventBudget` this
document compares against is an externally authored counterfactual, labeled
`authored-estimate` throughout, and every quantity derived from it remains a
counterfactual proxy, never an observed fact and never proof of what the
stopped run's own continuation would actually have done.

## Preconditions

A trace envelope, an obligation ledger paired to it, a termination
declaration paired to both, and a baseline definition paired to all three
must already be structurally, referentially, and (for the baseline
definition) coverage-valid, per `docs/contracts/trace-envelope.md`,
`docs/contracts/obligation-ledger.md`, `docs/contracts/termination-declaration.md`,
and `docs/contracts/baseline-definition.md`, before baseline comparison
runs. The full diagnostic code registry for every trace, ledger,
declaration, or baseline-definition validation failure, the deterministic
four-phase-document validation order across all four input documents, and
the `compare` CLI's exact stdout/stderr shape and exit codes are normatively
defined in `docs/contracts/obligation-ledger.md`'s "Diagnostic registry"
section (trace and ledger codes), `docs/contracts/termination-declaration.md`'s
"Diagnostic registry" section (declaration codes), and
`docs/contracts/baseline-definition.md`'s "Diagnostic registry", "Validation
order", and "CLI compare behavior" sections (baseline-definition codes and
the full CLI shape). This document defines only the shape, content,
algorithm, and canonical serialization of the `BaselineComparisonResult`
artifact that those sections describe as `compare`'s sole successful
output.

`compare` never accepts a precomputed `StopAssessmentResult` file as an
input of any kind. Every quantity this document labels `derived-from-declaration`,
`derived-from-trace-and-declaration`, `from-ledger`, or
`derived-from-trace-and-ledger` below is recomputed internally, from the
validated trace, ledger, and declaration, by invoking the unmodified Phase
2a `ProgressTrace.Core.Assessment` and `ProgressTrace.Core.Evaluation.Evaluator`
logic exactly as `assess` does, per
`docs/architecture/ADR-0003-phase-2-architecture-and-stop-assessment.md`'s
`P2-D10` recompute-internally principle, extended by
`docs/architecture/ADR-0004-baseline-comparison-and-authored-estimate.md`
to Task B. `compare` does not write a `StopAssessmentResult` to stdout and
does not require one to already exist; a caller who separately needs the
full Task A result, including fields this document deliberately does not
duplicate (`stableAttainmentRank`, per-obligation `overhead`, `safeStopRank`,
`traceOverhead`, `traceClassification`, `stopClassification`), runs `assess`
directly against the same trace, ledger, and declaration.

## Provenance labels

Every field below is labeled with exactly one of nine mutually exclusive
artifact-lineage classes, extending `StopAssessmentResult`'s eight-class
taxonomy (`docs/contracts/stop-assessment-result.md`'s "Provenance labels"
section) with one new class for the fourth input this contract introduces.
As with that taxonomy, these classes name only the source artifact(s) a
value was copied from and, for derived values, which input artifacts the
computation consumed; they are not claims of truth, observation quality,
producer identity, or evidence confidence.

- **contract-constant**: a value fixed by this contract itself, identical
  in every valid `1.0` result (for example `schemaVersion` and every member
  of `algorithm`).
- **from-trace**: copied by value from the trace envelope, unchanged.
- **from-ledger**: copied by value from the obligation ledger, unchanged, or
  determined solely by the ledger's own structure (which obligations exist
  and their order).
- **from-declaration**: copied by value from the termination declaration,
  unchanged (`terminationEventId`, `terminationKind`).
- **from-baseline-definition**: copied by value from the baseline
  definition, unchanged (`baselineSource` and its four nested members, and
  each obligation's `eventBudget`).
- **derived-from-declaration**: computed purely from declaration fields, via
  a fixed lookup table, with no dependency on trace, ledger, or baseline
  content (`terminationAttested`).
- **derived-from-trace-and-declaration**: computed by combining declaration
  content with trace content (`terminationRank`, `observedEventCount`).
- **derived-from-trace-and-ledger**: computed purely from trace and ledger
  content, with no dependency on declaration or baseline content (each
  obligation's `outcome`, recomputed via the unmodified Phase 2a
  stable-attainment algorithm).
- **derived-from-all-inputs**: computed from trace, ledger, declaration, and
  baseline-definition content together, where removing any one input could
  change the value (`applicability`, `authoredEstimateUnusedEventBudget`,
  `authoredEstimateFalseHaltPresent`, `maxAuthoredEstimateUnusedEventBudget`).

No `sourceClass` or similar field is added to the `1.0` JSON shape itself,
mirroring `StopAssessmentResult`'s precedent.

## Document structure

- `schemaVersion` (contract-constant; string): contract major/minor
  version. Phase 2b accepts only `1.0`.
- `traceId` (from-trace): the compared trace envelope's `traceId`.
- `algorithm` (contract-constant object): required object identifying the
  deterministic baseline-comparison algorithm that produced this result.
- `algorithm.name` (contract-constant): required string, the fixed constant
  value `"progresstrace-baseline-comparator"` for every Phase 2b `1.0`
  result, distinct from `"progresstrace-evaluator"` and
  `"progresstrace-stop-assessor"`.
- `algorithm.version` (contract-constant): required string, the fixed
  constant value `"1.0.0"` for the Phase 2b baseline-comparison algorithm.
- `baselineSource` (from-baseline-definition object): its four member
  values (`producerType`, `producerName`, `producerVersion`,
  `evidenceBasis`) are copied unchanged from the validated baseline
  definition and serialized in this result's own fixed canonical nested
  order, `producerType`, `producerName`, `producerVersion`,
  `evidenceBasis` — this is `BaselineComparisonResult`'s own canonical
  order, not a claim that the source baseline-definition document's raw
  property order is preserved or reproduced byte-for-byte. Its presence
  lets a reader interpret every `eventBudget`-derived value below without a
  separate lookup of the source baseline document.
- `terminationEventId` (from-declaration): copied by value from the
  validated termination declaration.
- `terminationKind` (from-declaration): copied by value from the validated
  termination declaration.
- `terminationAttested` (derived-from-declaration boolean): computed
  exactly as `docs/contracts/stop-assessment-result.md` defines, from the
  declaration's `terminationKind` alone.
- `terminationRank` (derived-from-trace-and-declaration integer): computed
  exactly as `docs/contracts/stop-assessment-result.md` defines; always
  `N-1`, the maximum canonical rank present in the trace.
- `observedEventCount` (derived-from-trace-and-declaration integer,
  `>= 1`): `terminationRank + 1`, the count of canonical event slots
  observed through and including termination. See "Unit and formula" below
  for why this, and not `terminationRank` itself, is the quantity compared
  against `eventBudget`.
- `obligationComparisons` (from-ledger array): one entry per obligation
  declared in the paired ledger, in ledger order, identical in membership
  and ordering to `StopAssessmentResult.obligationResults` and
  `EvaluationResult.obligationResults`. Guaranteed non-empty by the paired
  ledger's own non-empty `obligations` requirement, and guaranteed to have
  exactly one `eventBudget` per entry by the baseline definition's coverage
  validation (`docs/contracts/baseline-definition.md`'s "Referential
  integrity and coverage" section).
- `obligationComparisons[].obligationId` (from-ledger): the compared
  obligation's id, copied from the ledger.
- `obligationComparisons[].outcome` (derived-from-trace-and-ledger): the
  unmodified Phase 2a per-obligation outcome, one of `stable-attainment`,
  `unmet-target-at-termination`, recomputed exactly as
  `docs/contracts/stop-assessment-result.md`'s "Per-obligation
  stable-attainment algorithm" defines, with no dependency on the
  declaration or the baseline definition. Carried here, rather than
  silently discarded, because `applicability` below is computed directly
  from it and an auditor must be able to see which branch fired without a
  separate `assess` invocation.
- `obligationComparisons[].eventBudget` (from-baseline-definition integer,
  `1..2147483647`): copied by value from the validated baseline
  definition's matching `obligationBudgets` entry.
- `obligationComparisons[].applicability` (derived-from-all-inputs): one of
  the fixed closed five-value enum `incomplete-observation`,
  `not-applicable-stable-attainment`, `unused-authored-budget`,
  `budget-exhausted-exactly`, `budget-exceeded`; see "Unit and formula"
  below.
- `obligationComparisons[].authoredEstimateUnusedEventBudget`
  (derived-from-all-inputs integer `>= 0`, or `null`): the count of
  authored event-budget slots left unused when `applicability =
  unused-authored-budget` (always `> 0` in that case), exactly `0` when
  `applicability = budget-exhausted-exactly`, and `null` for every other
  applicability value. This field is never named `falseHaltMagnitude` and
  never presented as a measured cost; see "Interpretation and non-claims"
  below.
- `authoredEstimateFalseHaltPresent` (derived-from-all-inputs boolean):
  `true` iff at least one `obligationComparisons[]` entry has
  `applicability = unused-authored-budget` (equivalently, at least one
  `authoredEstimateUnusedEventBudget` is a positive integer); `false`
  otherwise, including when every obligation's `authoredEstimateUnusedEventBudget`
  is `null` or exactly `0`.
- `maxAuthoredEstimateUnusedEventBudget` (derived-from-all-inputs integer
  `> 0`, or `null`): the maximum `authoredEstimateUnusedEventBudget` value
  across every `obligationComparisons[]` entry with `applicability =
  unused-authored-budget`; `null` iff `authoredEstimateFalseHaltPresent =
  false`. Never a sum; see "Trace-level rollup" below.

## Field types and requiredness

The root object is a JSON object with `additionalProperties: false`; every
property listed above is required and always present; `null` is used, not
omission, whenever a value is undefined by the algorithm below.

Required root properties and types: `schemaVersion` (string, exactly
`"1.0"`), `traceId` (string, non-empty), `algorithm` (object),
`algorithm.name` (string, fixed constant
`"progresstrace-baseline-comparator"`), `algorithm.version` (string, fixed
constant `"1.0.0"`), `baselineSource` (object, described below),
`terminationEventId` (string, non-empty), `terminationKind` (string, one of
the fixed closed eight-value enum fixed in
`docs/contracts/termination-declaration.md`), `terminationAttested`
(boolean), `terminationRank` (integer, `>= 0`), `observedEventCount`
(integer, `>= 1`), `obligationComparisons` (array; one entry per obligation
in the paired ledger, never empty), `authoredEstimateFalseHaltPresent`
(boolean), `maxAuthoredEstimateUnusedEventBudget` (integer `> 0`, or
`null`).

`baselineSource` is a JSON object with `additionalProperties: false`; all
four of its members are required and always present, in this exact nested
order: `producerType` (string, one of the fixed closed four-value enum
`agent`, `harness`, `operator`, `adapter`), `producerName` (string,
non-empty), `producerVersion` (string non-empty, or `null`), and
`evidenceBasis` (string, one of the fixed closed five-value enum
`agent-estimate`, `harness-policy`, `operator-estimate`,
`historical-analysis`, `adapter-inference`). Every member is copied by
value from the validated baseline definition; the result never adds, drops,
reorders, or reinterprets a `baselineSource` member.

Each entry of `obligationComparisons` is a JSON object with
`additionalProperties: false`; all five properties are required and always
present: `obligationId` (string, non-empty), `outcome` (string, one of the
fixed enum `stable-attainment`, `unmet-target-at-termination`),
`eventBudget` (integer, `1..2147483647`), `applicability` (string, one of
the fixed closed five-value enum), `authoredEstimateUnusedEventBudget`
(integer `>= 0`, or `null`).

## Unit and formula

`eventBudget` and `observedEventCount` are compared on the same unit: a
count of canonical event slots in the paired trace's canonical order.
`eventBudget` is an authored total count, not a canonical rank (canonical
rank is zero-based) and not a raw `events[].sequence` value.
`observedEventCount = terminationRank + 1` converts the zero-based
`terminationRank` into the same one-based total-count unit `eventBudget`
uses, so the two are directly comparable without an off-by-one
misalignment: a trace whose termination event has canonical rank `0` (a
single-event trace) has `observedEventCount = 1`, meaning exactly one
canonical event slot was observed, matching an author who budgeted
`eventBudget = 1` for "the obligation should resolve within the trace's
first and only event."

For each obligation in `obligationComparisons`, using its recomputed Task A
`outcome`, the trace-level `terminationAttested`, and its own `eventBudget`,
`applicability` and `authoredEstimateUnusedEventBudget` are computed by
evaluating these conditions in order; the first matching condition
determines both fields, and no other order or tie case is possible:

1. If `terminationAttested = false`: `applicability =
   incomplete-observation`. `authoredEstimateUnusedEventBudget = null`.
   This condition is evaluated first, before any per-obligation branch, and
   applies identically to every obligation in `obligationComparisons`
   regardless of that obligation's own `outcome`, mirroring
   `StopAssessmentResult.stopClassification`'s identical
   `terminationAttested`-first precedence. Neither `capture-truncated` nor
   `unknown` is positive evidence a real, named-cause termination occurred,
   so neither ever contributes an authored-budget comparison for any
   obligation, even one whose `outcome` happens to be `stable-attainment`.
2. Else, if this obligation's `outcome = stable-attainment`: `applicability
   = not-applicable-stable-attainment`. `authoredEstimateUnusedEventBudget
   = null`. An authored budget is compared only against an obligation that
   failed to stably attain; a stably attained obligation has nothing to
   compare a budget against, regardless of what `eventBudget` an author
   supplied for it.
3. Else (`terminationAttested = true` and this obligation's `outcome =
   unmet-target-at-termination`), compare this obligation's `eventBudget`
   to the trace-level `observedEventCount`:
   - `eventBudget > observedEventCount`: `applicability =
     unused-authored-budget`. `authoredEstimateUnusedEventBudget =
     eventBudget - observedEventCount`, always a positive integer bounded
     above by `eventBudget - 1` (since `observedEventCount >= 1`).
   - `eventBudget = observedEventCount`: `applicability =
     budget-exhausted-exactly`. `authoredEstimateUnusedEventBudget = 0`, a
     defined, meaningful zero, distinct from `null` and never omitted or
     reported as absent.
   - `eventBudget < observedEventCount`: `applicability =
     budget-exceeded`. `authoredEstimateUnusedEventBudget = null`. The
     obligation stopped only after already consuming more canonical event
     slots than its own authored budget allotted; no unused quantity
     exists to report, and none is invented in its place.

Exactly one of the five `applicability` values is produced for every
obligation in `obligationComparisons`; the three conditions above, with
their nested three-way branch under condition 3, are collectively
exhaustive and mutually exclusive.

## Trace-level rollup

`authoredEstimateFalseHaltPresent = true` iff at least one
`obligationComparisons[]` entry has `applicability = unused-authored-budget`;
`false` otherwise. `maxAuthoredEstimateUnusedEventBudget` is the maximum
`authoredEstimateUnusedEventBudget` value across every entry with
`applicability = unused-authored-budget`, or `null` when no such entry
exists (equivalently, whenever `authoredEstimateFalseHaltPresent = false`).
It is never a sum: each unused-budget quantity is an independent authored
counterfactual computed against one obligation's own authored `eventBudget`
and the trace's single shared `observedEventCount`; summing across
obligations would combine independently authored guesses into a number no
author actually authored, mirroring
`docs/contracts/stop-assessment-result.md`'s `traceOverhead`, which never
sums per-obligation `overhead` for the analogous reason that trailing trace
events are shared, not obligation-exclusive.

## Interpretation and non-claims

A positive `authoredEstimateFalseHaltPresent` (or any single obligation's
`applicability = unused-authored-budget`) states only: at least one unmet
obligation's trace terminated at an `observedEventCount` strictly less than
some author's independently supplied `eventBudget` for that obligation. It
does not, and cannot, claim any of the following:

- that continuing the trace for the unused budget's worth of additional
  canonical events would have caused that obligation to reach
  `status=satisfied`;
- that the authored `eventBudget` is an accurate, validated, or
  independently verified estimate of what the obligation actually required;
- that the termination was premature, incorrect, or avoidable;
- that the stopped run's own actual counterfactual continuation is known,
  observed, or provable in any way.

`eventBudget`, and therefore every value this document derives from it
(`applicability`, `authoredEstimateUnusedEventBudget`,
`authoredEstimateFalseHaltPresent`, `maxAuthoredEstimateUnusedEventBudget`),
is a counterfactual proxy: a stand-in for an unknown, unobserved
continuation, supplied by whichever role `baselineSource.producerType`
names, under whichever evidence basis `baselineSource.evidenceBasis` names.
It is never observed fact, never a measurement of the stopped run's own
continuation, and never proof of what would have happened. Carrying
`baselineSource` verbatim on every result (see "Document structure" above)
keeps this counterfactual-proxy status auditable alongside every value it
produced, so a reader is never one field away from forgetting who
authored the budget being compared against.

## Boundary cases

- **Every obligation `stable-attainment`, termination attested**: every
  `obligationComparisons[]` entry has `applicability =
  not-applicable-stable-attainment`, `authoredEstimateFalseHaltPresent =
  false`, `maxAuthoredEstimateUnusedEventBudget = null`, regardless of how
  large or small any obligation's authored `eventBudget` was.
- **Termination not attested (`capture-truncated` or `unknown`)**: every
  `obligationComparisons[]` entry has `applicability =
  incomplete-observation`, identically for `capture-truncated` and
  `unknown`, regardless of any obligation's `outcome` or `eventBudget`;
  `authoredEstimateFalseHaltPresent = false`,
  `maxAuthoredEstimateUnusedEventBudget = null`.
- **`eventBudget` exactly equal to `observedEventCount`**: `applicability =
  budget-exhausted-exactly`, `authoredEstimateUnusedEventBudget = 0`. This
  is a defined, meaningful outcome distinct from both
  `unused-authored-budget` (strictly greater) and `budget-exceeded`
  (strictly less); it is never merged into either neighboring case.
  `authoredEstimateFalseHaltPresent` for the trace overall is unaffected by
  this entry alone (a `0` magnitude never counts as "present").
- **Mixed applicability across obligations**: `authoredEstimateFalseHaltPresent`
  and `maxAuthoredEstimateUnusedEventBudget` are computed only from entries
  with `applicability = unused-authored-budget`; entries with any other
  `applicability` value neither raise `authoredEstimateFalseHaltPresent` to
  `true` nor participate in the maximum.
- **Every obligation `unmet-target-at-termination` with wildly different
  authored budgets**: `maxAuthoredEstimateUnusedEventBudget` reports only
  the single largest per-obligation unused quantity; it is never a sum or
  an average across obligations, and a caller who needs the full
  per-obligation breakdown reads `obligationComparisons` directly rather
  than inferring it from the trace-level maximum.
- **Single-event trace (`terminationRank = 0`)**: `observedEventCount = 1`.
  An obligation with `eventBudget = 1` and `outcome =
  unmet-target-at-termination` has `applicability =
  budget-exhausted-exactly`; `eventBudget >= 2` yields `unused-authored-budget`
  with `authoredEstimateUnusedEventBudget = eventBudget - 1`.

## Canonical property order

Serialization emits properties in exactly this order at every level, with
no alternate or alphabetical ordering:

- Root object: `schemaVersion`, `traceId`, `algorithm`, `baselineSource`,
  `terminationEventId`, `terminationKind`, `terminationAttested`,
  `terminationRank`, `observedEventCount`, `obligationComparisons`,
  `authoredEstimateFalseHaltPresent`, `maxAuthoredEstimateUnusedEventBudget`.
- `algorithm` object: `name`, `version`.
- `baselineSource` object: `producerType`, `producerName`,
  `producerVersion`, `evidenceBasis`.
- Each `obligationComparisons` entry: `obligationId`, `outcome`,
  `eventBudget`, `applicability`, `authoredEstimateUnusedEventBudget`.

## Canonical serialization

Serialization of `BaselineComparisonResult` is canonical and byte-idempotent
under repeated normalization, matching `StopAssessmentResult`'s guarantee:
the canonical property order above, UTF-8 encoding without a byte-order
mark, no insignificant whitespace between tokens, `null` written as the
literal JSON `null` token (never omitted, never an empty string, never
`0`), `0` written as the literal JSON `0` token when
`authoredEstimateUnusedEventBudget = 0` (never omitted, never confused with
`null`), arrays serialized in the exact order defined by this document
(`obligationComparisons` per its ordering rule above) with no re-sorting,
and exactly one trailing newline byte terminating the document. Normalizing
the same `BaselineComparisonResult` any number of times produces
byte-identical output.

## Algorithm identity and reproducibility

`algorithm.name = "progresstrace-baseline-comparator"` and
`algorithm.version = "1.0.0"` identify the Phase 2b baseline-comparison
implementation, distinct from `EvaluationResult.algorithm`
(`"progresstrace-evaluator"`) and `StopAssessmentResult.algorithm`
(`"progresstrace-stop-assessor"`), even though the baseline-comparison
algorithm invokes both of those internally. Together with the copied
`baselineSource`, `terminationEventId`, `terminationKind`, and the full
`obligationComparisons` array, this is sufficient provenance to reproduce a
`BaselineComparisonResult` deterministically from its four source
documents: re-running `compare` against the same trace, ledger, termination
declaration, and baseline definition always produces the byte-identical
result.

## Version policy

A reader accepts only the schema versions it explicitly supports. This
contract's Phase 2b implementation accepts exactly `schemaVersion` `1.0`
and rejects a baseline comparison result, or any of its four paired source
documents, declaring any other version. Contract evolution distinguishes
two kinds of change:

- **Additive compatible evolution**: a new optional property that does not
  alter any existing required field, either closed enum, the unit-and-formula
  algorithm, the trace-level rollup rule, or the canonical property order
  may be introduced in a later minor version, consumable by newer readers
  without breaking readers pinned to `1.0`.
- **Breaking change**: adding or changing a value in either closed
  classification enum, the applicability algorithm, the
  `observedEventCount` formula, the trace-level rollup rule, the canonical
  property order, or any required field is a breaking change. It requires a
  new major version and Dragos's approval, new conformance fixtures, and is
  never introduced as a minor-version patch or a silent change to `1.0`.

## Synthetic example

The following example is illustrative of document shape only; every
normative rule is fixed in the sections above. The example data is
synthetic and contains no employer, medical, credential, or production
content, and correlates to the harness-authored baseline-definition example
in `docs/contracts/baseline-definition.md`, paired with a trace whose
terminal event has canonical rank `9` (`observedEventCount = 10`), a
termination declaration attesting `timeout`, and a single obligation
`obl-1` whose Task A `outcome` is `unmet-target-at-termination`:

```json
{
  "schemaVersion": "1.0",
  "traceId": "trace-example-1",
  "algorithm": { "name": "progresstrace-baseline-comparator", "version": "1.0.0" },
  "baselineSource": {
    "producerType": "harness",
    "producerName": "example-harness",
    "producerVersion": "2.3.0",
    "evidenceBasis": "harness-policy"
  },
  "terminationEventId": "evt-10",
  "terminationKind": "timeout",
  "terminationAttested": true,
  "terminationRank": 9,
  "observedEventCount": 10,
  "obligationComparisons": [
    {
      "obligationId": "obl-1",
      "outcome": "unmet-target-at-termination",
      "eventBudget": 20,
      "applicability": "unused-authored-budget",
      "authoredEstimateUnusedEventBudget": 10
    }
  ],
  "authoredEstimateFalseHaltPresent": true,
  "maxAuthoredEstimateUnusedEventBudget": 10
}
```
