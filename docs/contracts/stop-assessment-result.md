# Stop assessment result

Version `1.0` is the Phase 2a stop-assessment-result contract. It is the
versioned, deterministic output of `assess <trace-path> <ledger-path>
<termination-declaration-path>`: a per-obligation and trace-level
observational assessment of whether a trace's explicit obligations were
stably attained by its declared termination, and, when termination is
attested and every obligation is stably successful, how much later than
necessary that termination occurred. The normative machine-readable
contract is `contracts/stop-assessment-result.schema.json`, implemented by
Codex from this document; the .NET Core's stop-assessment algorithm in the
`ProgressTrace.Core.Assessment` namespace applies the rank-distance,
stable-attainment, and rollup rules below as a pure, deterministic function
with no hidden state, clock, network, database, or model dependency. It
invokes, but never reimplements, the unmodified Phase 1
`ProgressTrace.Core.Evaluation.Evaluator`.

## Purpose

`StopAssessmentResult` reports, for a normalized trace envelope, a
structurally and referentially valid obligation ledger, and a structurally
and referentially valid termination declaration, an exact deterministic,
purely observational assessment of whether each ledger-declared obligation
was stably attained through termination and, only when every obligation is
stable and termination is attested, a single trace-level late-termination
cost figure. This document never asserts, computes, or names a false-halt
or counterfactual claim of any kind; that is reserved for Task B, gated on
a future `ADR-0004`, per
`docs/architecture/ADR-0003-phase-2-architecture-and-stop-assessment.md`.

## Preconditions

A trace envelope, an obligation ledger paired to it, and a termination
declaration paired to both must already be structurally and referentially
valid, per `docs/contracts/trace-envelope.md`,
`docs/contracts/obligation-ledger.md`, and
`docs/contracts/termination-declaration.md`, before stop assessment runs.
The full diagnostic code registry for every trace, ledger, or declaration
validation failure, the deterministic three-phase-document validation order
across all three input documents, and the `assess` CLI's exact
stdout/stderr shape and exit codes are normatively defined in
`docs/contracts/obligation-ledger.md`'s "Diagnostic registry" and
"Validation order" sections (trace and ledger) and
`docs/contracts/termination-declaration.md`'s "Diagnostic registry",
"Validation order", and "CLI assess behavior" sections (declaration and the
full CLI shape). This document defines only the shape, content, algorithm,
and canonical serialization of the `StopAssessmentResult` artifact that
those sections describe as `assess`'s sole successful output.

## Provenance labels

Every field below is labeled with exactly one of six mutually exclusive
provenance classes, so no unlike-provenance value is ever blended into
another without a label, and no field is ever assigned more than one class:

- **contract-constant**: a value fixed by this contract itself, identical
  in every valid `1.0` result, and never computed from any of the three
  input documents (for example `schemaVersion` and every member of
  `algorithm`).
- **authored**: copied by value from the termination declaration,
  unchanged, with no computation applied.
- **observed**: copied by value from the trace envelope or obligation
  ledger, unchanged, with no computation applied.
- **derived-from-authored**: computed purely from one or more authored
  fields, via a fixed lookup table or rule, with no dependency on trace or
  ledger content.
- **derived-from-observed**: computed purely from trace envelope and/or
  obligation ledger content, with no dependency on any authored field.
- **derived-from-authored-and-observed**: computed by combining an
  authored field's value (directly, such as `terminationEventId`, or
  indirectly through a derived-from-authored value such as
  `terminationAttested`) with observed trace or ledger content; neither
  the authored input alone nor the observed input alone determines the
  value. This class covers both `terminationRank` (which dereferences the
  authored `terminationEventId` against the observed trace's canonical
  order) and every field whose value or nullness branches on
  `terminationAttested` together with observed obligation outcomes
  (`stopClassification`, `safeStopRank`, `traceOverhead`), and it also
  covers `obligationResults[].overhead`, since `overhead` subtracts the
  purely observed `stableAttainmentRank` from the authored-and-observed
  `terminationRank` (`terminationRank - stableAttainmentRank`).

No `sourceClass` or similar field is added to the `1.0` JSON shape itself:
Task A's artifact boundary (one authored input document, one derived
output document) and this document's normative provenance labels below are
sufficient to fix provenance for every field without adding a redundant
runtime property.

## Document structure

- `schemaVersion` (contract-constant; string): contract major/minor
  version. Phase 2a accepts only `1.0`.
- `traceId` (observed): the assessed trace envelope's `traceId`.
- `algorithm` (contract-constant): required object identifying the
  deterministic stop-assessment algorithm that produced this result; both
  its members are fixed constants for every Phase 2a `1.0` result.
- `algorithm.name` (contract-constant): required string, the fixed
  constant value `"progresstrace-stop-assessor"` for every Phase 2a `1.0`
  result.
- `algorithm.version` (contract-constant): required string, the fixed
  constant value `"1.0.0"` for the Phase 2a stop-assessment algorithm;
  this identifies the algorithm implementation version, not the contract's
  `schemaVersion`.
- `terminationEventId` (authored): copied by value from the validated
  termination declaration.
- `terminationKind` (authored): copied by value from the validated
  termination declaration; one of the fixed closed eight-value enum fixed
  in `docs/contracts/termination-declaration.md`.
- `terminationAttested` (derived-from-authored boolean): `true` iff
  `terminationKind` is not `capture-truncated` and not `unknown`, per the
  fixed lookup table in `docs/contracts/termination-declaration.md`'s
  "Termination attestation" section; computed purely from the authored
  `terminationKind`, with no dependency on trace or ledger content.
- `terminationRank` (derived-from-authored-and-observed integer): the
  canonical rank of the event dereferenced by the authored
  `terminationEventId`, computed against the observed trace's canonical
  order; always equal to `N-1`, the maximum canonical rank present in the
  trace, guaranteed by the terminal-required validation already performed
  on the declaration. Its value requires both the authored reference
  (which event) and the observed trace (that event's rank), so it is
  neither purely authored nor purely observed.
- `obligationResults` (derived-from-observed array): one entry per
  obligation declared in the paired ledger, in ledger order; which
  obligations exist and their order are purely observed ledger facts. See
  "obligationResults ordering" below and the per-entry field labels
  immediately following, one of which (`overhead`) carries a different,
  more specific class than the array's own membership and ordering.
- `obligationResults[].obligationId` (observed): the classified
  obligation's id.
- `obligationResults[].classification` (derived-from-observed): the
  unmodified Phase 1 per-obligation classification, one of `progress`,
  `stagnation`, `regression`, `insufficient-evidence`, recomputed by
  invoking `ProgressTrace.Core.Evaluation.Evaluator` directly against the
  validated trace and ledger; identical in meaning and computation to
  `EvaluationResult.obligationResults[].classification`, with no
  dependency on any authored declaration field.
- `obligationResults[].evidenceEventIds` (derived-from-observed): the
  unmodified Phase 1 evidence-event-id array for this obligation, computed
  and ordered exactly as `docs/contracts/evaluation-result.md`'s
  "evidenceEventIds content and ordering" section defines.
- `obligationResults[].outcome` (derived-from-observed): one of the fixed
  closed enum `stable-attainment`, `unmet-target-at-termination`; see "Per-
  obligation stable-attainment algorithm" below. Computed purely from the
  obligation's own signals, with no dependency on any authored field.
- `obligationResults[].stableAttainmentRank` (derived-from-observed
  integer or `null`): the canonical rank at which this obligation's
  maximal trailing all-`satisfied` run begins; `null` if and only if
  `outcome = unmet-target-at-termination`.
- `obligationResults[].overhead` (derived-from-authored-and-observed
  integer or `null`): the rank distance from `stableAttainmentRank` to
  `terminationRank`; `null` under exactly the same condition as
  `stableAttainmentRank`, and always `>= 0` when not `null`. Its value
  depends on the authored-and-observed `terminationRank` as well as the
  purely observed `stableAttainmentRank`, so it is never purely observed.
- `traceClassification` (derived-from-observed): the unmodified Phase 1
  trace-level classification, one of `progress`, `stagnation`,
  `regression`, `insufficient-evidence`, computed by the unmodified
  precedence rule in `docs/contracts/evaluation-result.md`'s "Trace-level
  precedence" section over the `classification` values recomputed above,
  with no dependency on any authored declaration field.
- `stopClassification` (derived-from-authored-and-observed string): the
  Phase 2a trace-level stop-assessment classification, one of the fixed
  closed enum `on-target`, `late-termination`,
  `unmet-target-at-termination-present`, `incomplete-observation`; see
  "Trace-level rollup" below. Its value branches first on the
  derived-from-authored `terminationAttested`, then on observed
  obligation outcomes, so neither input alone determines it.
- `safeStopRank` (derived-from-authored-and-observed integer or `null`):
  the maximum `stableAttainmentRank` across all obligations; `null`
  unless `terminationAttested = true` (an authored-derived condition) and
  every obligation's `outcome` is `stable-attainment` (an observed
  condition).
- `traceOverhead` (derived-from-authored-and-observed integer or `null`):
  the rank distance from `safeStopRank` to `terminationRank`; `null`
  under exactly the same condition as `safeStopRank`, and always `>= 0`
  when not `null`. Never a sum of `obligationResults[].overhead` values.

## Field types and requiredness

The root object is a JSON object with `additionalProperties: false`; every
property listed above is required and always present; `null` is used, not
omission, whenever a value is undefined by the algorithm below. There is no
default value for any field: a field's presence is unconditional, and only
its value may be `null`.

Required root properties and types: `schemaVersion` (string, exactly
`"1.0"`), `traceId` (string, non-empty), `algorithm` (object), `algorithm.name`
(string, fixed constant `"progresstrace-stop-assessor"`), `algorithm.version`
(string, fixed constant `"1.0.0"`), `terminationEventId` (string,
non-empty), `terminationKind` (string, one of the fixed closed eight-value
enum), `terminationAttested` (boolean), `terminationRank` (integer, `>= 0`),
`obligationResults` (array; one entry per obligation in the paired ledger,
never empty, since an empty `obligations` array is rejected before
assessment per `docs/contracts/obligation-ledger.md`'s "Preconditions"
precedent), `traceClassification` (string, one of the fixed enum `progress`,
`stagnation`, `regression`, `insufficient-evidence`), `stopClassification`
(string, one of the fixed enum `on-target`, `late-termination`,
`unmet-target-at-termination-present`, `incomplete-observation`),
`safeStopRank` (integer `>= 0`, or `null`), `traceOverhead` (integer `>= 0`,
or `null`).

Each entry of `obligationResults` is a JSON object with
`additionalProperties: false`; all six properties are required and always
present: `obligationId` (string, non-empty), `classification` (string, one
of the fixed enum `progress`, `stagnation`, `regression`,
`insufficient-evidence`), `evidenceEventIds` (array of strings; may be
empty), `outcome` (string, one of the fixed enum `stable-attainment`,
`unmet-target-at-termination`), `stableAttainmentRank` (integer `>= 0`, or
`null`), `overhead` (integer `>= 0`, or `null`).

## Canonical rank and rank distance

Canonical rank is the zero-based position of a trace event in the trace
envelope's already-normative canonical order (`sequence` ascending, then
timestamp instant, then `id` ordinal), fixed in
`docs/contracts/trace-envelope.md`'s "Compatibility and normalization"
section, and reused unmodified here. It is computed once per assessment
over every event in the validated trace and is always contiguous `0..N-1`
for a trace of `N` events, regardless of gaps in raw `sequence` values.

For canonical ranks `a <= b`, the rank distance `b - a` is defined exactly
once, precisely, and reused for every subtraction in this document: it is
the count of trace events whose canonical rank is strictly greater than `a`
and less than or equal to `b`, i.e. every event after `a` up to and
including `b`. It is never a raw `events[].sequence` delta and never the
count of events strictly between the two ranks.

## Per-obligation stable-attainment algorithm

For each obligation declared in the paired ledger, let `s1..sn` be its
signals in the exact total order already fixed in
`docs/contracts/obligation-ledger.md`'s "Signal total order" section:
ascending by the canonical rank of each signal's dereferenced trace event,
ties on an identical `eventId` broken by ascending `signals`-array index.
No new tie-break rule is introduced.

1. If `n = 0`, no stable-attainment rank exists. `outcome =
   unmet-target-at-termination`, `stableAttainmentRank = null`, `overhead
   = null`.
2. Otherwise, find the smallest index `k` (`1 <= k <= n`) such that every
   signal from `sk` through `sn`, inclusive, has `status = satisfied`. This
   is the start of the obligation's maximal trailing all-`satisfied` run.
3. If no such `k` exists — equivalently, `sn` itself is not `satisfied` —
   no stable-attainment rank exists. `outcome =
   unmet-target-at-termination`, `stableAttainmentRank = null`, `overhead
   = null`. This is the only condition under which
   `unmet-target-at-termination` is produced, whether the obligation never
   reached `satisfied` at all, reached it once and regressed away without
   ever recovering, or has `n = 0`; no special-case branch distinguishes
   these, since all three fail the same single test.
4. If `k` exists, `stableAttainmentRank` is the canonical rank of `sk`'s
   dereferenced trace event. `outcome = stable-attainment`. `overhead =
   terminationRank - stableAttainmentRank`, the rank distance from
   `stableAttainmentRank` to `terminationRank`. Because `sk`'s dereferenced
   event is necessarily a trace event, and `terminationRank` is always the
   maximum canonical rank present in the trace (per the terminal-required
   rule), `stableAttainmentRank <= terminationRank` always holds, so
   `overhead` is always `>= 0`.

An obligation that attains `satisfied`, regresses away, and later
re-attains and holds `satisfied` through termination has
`stableAttainmentRank` equal to the rank of the later re-attainment, the
start of the final trailing all-`satisfied` run, not the rank of the first
attainment; this is the corrected behavior this rule was designed to
produce, replacing a rejected first-reach rule that would have used the
first attainment instead and reported a misleadingly favorable overhead
whenever the obligation later regressed and never recovered.

`obligationResults` contains exactly one entry per obligation declared in
the paired ledger, in the same order as the ledger's `obligations` array,
independent of `outcome`; this is the same ordering rule already fixed for
`EvaluationResult.obligationResults`.

## Trace-level rollup

`terminationAttested` and `terminationRank` are computed first, from the
validated termination declaration and trace, as fixed in "Document
structure" above and in
`docs/contracts/termination-declaration.md`. Per-obligation
`stableAttainmentRank`, `overhead`, and `outcome` are computed next, for
every obligation, exactly as above, regardless of `terminationAttested`:
per-obligation values are always computed and always emitted, since they
depend only on the obligation's own signals and on `terminationRank`, never
on attestation.

`traceClassification` is the unmodified Phase 1 trace-level precedence rule
from `docs/contracts/evaluation-result.md`'s "Trace-level precedence"
section (`regression > insufficient-evidence > stagnation > progress`),
applied to the recomputed `obligationResults[].classification` values.

`stopClassification` and the trace-level `safeStopRank`/`traceOverhead`
pair are computed by evaluating these conditions in order, the first
matching condition determining `stopClassification`, and no other order or
tie case is possible:

1. If `terminationAttested = false`: `stopClassification =
   incomplete-observation`. `safeStopRank = null` and `traceOverhead =
   null`, regardless of any obligation's `outcome`. This applies
   identically whether `terminationKind` is `capture-truncated` or
   `unknown`: neither is positive evidence a real, named-cause termination
   occurred, so neither ever contributes a trace-level safe-stop or
   overhead figure, even when every obligation happens to be mechanically
   `stable-attainment`.
2. Else, if any obligation's `outcome = unmet-target-at-termination`:
   `stopClassification = unmet-target-at-termination-present`.
   `safeStopRank = null` and `traceOverhead = null`, since the maximum
   stable-attainment rank is only defined over a set where every element is
   defined.
3. Else (`terminationAttested = true` and every obligation's `outcome =
   stable-attainment`): `safeStopRank = max(stableAttainmentRank)` across
   all obligations. `traceOverhead = terminationRank - safeStopRank`, the
   rank distance from `safeStopRank` to `terminationRank`, and is `>= 0`
   by the same reasoning as per-obligation `overhead`. If `traceOverhead >
   0`: `stopClassification = late-termination`. If `traceOverhead = 0` (every
   obligation's stable attainment coincides with `terminationRank`):
   `stopClassification = on-target`.

`traceOverhead` is never computed as, and is never equal by construction
to, a sum of `obligationResults[].overhead` values: post-safe-stop trace
events are shared across every obligation's trailing run, and summation
would double count them. `obligationResults[].overhead` remains available
in every case as a per-obligation diagnostic value; it is never aggregated
into `traceOverhead` or into any other single trace-level number.

## Boundary cases

- **Zero signals**: an obligation with `n = 0` has no candidate `k` and is
  therefore `unmet-target-at-termination` with `stableAttainmentRank =
  null` and `overhead = null`; its `classification` is
  `insufficient-evidence` and its `evidenceEventIds` is `[]`, both via the
  unmodified Evaluator.
- **Regression with no recovery**: an obligation that attains `satisfied`,
  regresses (to `open`, `in-progress`, `regressed`, or `abandoned`), and
  never re-attains `satisfied` before termination has `sn != satisfied`, so
  no `k` exists; it is `unmet-target-at-termination`, even though it was
  `satisfied` at some earlier point. This is the rule's intended behavior,
  not a hidden edge case.
- **Regression then recovery**: an obligation that attains `satisfied`,
  regresses, then re-attains and holds `satisfied` through termination has
  `stableAttainmentRank` at the rank of the later re-attainment (the start
  of the final trailing all-`satisfied` run), never at the rank of the
  first attainment.
- **All obligations unmet**: `stopClassification =
  unmet-target-at-termination-present`; `safeStopRank` and `traceOverhead`
  are both `null`.
- **Mixed (some stable, some unmet)**: identical rollup to "all
  obligations unmet" above; `stopClassification =
  unmet-target-at-termination-present` fires whenever at least one
  obligation is unmet, regardless of how many others are stable.
- **`terminationKind = unknown`**: `terminationAttested = false`,
  `stopClassification = incomplete-observation`, `safeStopRank` and
  `traceOverhead` both `null`, exactly as for `capture-truncated`, even if
  every obligation happens to be `stable-attainment`.
- **`terminationKind = capture-truncated`**: identical rollup to
  `unknown` above; per-obligation `stableAttainmentRank`/`overhead` are
  still computed and emitted for diagnostic transparency, since they do
  not depend on attestation.
- **No overhead (on-target)**: when every obligation's
  `stableAttainmentRank` equals `terminationRank`, `overhead = 0` for each
  such obligation, `safeStopRank = terminationRank`, `traceOverhead = 0`,
  and `stopClassification = on-target`. `0` is a defined, meaningful value
  here, distinct from `null`; it is never omitted or reported as absent
  evidence.

## Canonical property order

Serialization emits properties in exactly this order at every level, with
no alternate or alphabetical ordering:

- Root object: `schemaVersion`, `traceId`, `algorithm`, `terminationEventId`,
  `terminationKind`, `terminationAttested`, `terminationRank`,
  `obligationResults`, `traceClassification`, `stopClassification`,
  `safeStopRank`, `traceOverhead`.
- `algorithm` object: `name`, `version`.
- Each `obligationResults` entry: `obligationId`, `classification`,
  `evidenceEventIds`, `outcome`, `stableAttainmentRank`, `overhead`.

## Canonical serialization

Serialization of `StopAssessmentResult` is canonical and byte-idempotent
under repeated normalization, matching `TraceNormalizer`'s and
`EvaluationResultNormalizer`'s guarantee: the canonical property order
above, UTF-8 encoding without a byte-order mark, no insignificant
whitespace between tokens, `null` written as the literal JSON `null` token
(never omitted, never an empty string, never `0`), arrays serialized in the
exact order defined by this document (`obligationResults` per its ordering
rule above, `evidenceEventIds` per `evaluation-result.md`'s content-and-
ordering rule) with no re-sorting, and exactly one trailing newline byte
terminating the document. Normalizing the same `StopAssessmentResult` any
number of times produces byte-identical output.

## Algorithm identity and reproducibility

`algorithm.name = "progresstrace-stop-assessor"` and `algorithm.version =
"1.0.0"` identify the Phase 2a stop-assessment implementation, distinct
from `EvaluationResult.algorithm` (`"progresstrace-evaluator"`), even
though the stop-assessment algorithm invokes the evaluator internally.
Together with `terminationEventId`, `terminationKind`, and the full
`obligationResults` array, this is sufficient provenance to reproduce a
`StopAssessmentResult` deterministically from its three source documents:
re-running `assess` against the same trace, ledger, and termination
declaration always produces the byte-identical result.

## Baseline and cost fields

Baseline comparison and false-halt cost are completely absent from this
contract: no field of `StopAssessmentResult` names, reserves, or implies a
baseline or a false-halt value. `unmet-target-at-termination` and
`unmet-target-at-termination-present` are purely observational; neither is
computed relative to any independent reference, and neither is ever named
or interpreted as a false halt, per
`docs/architecture/ADR-0003-phase-2-architecture-and-stop-assessment.md`'s
"Why `unmet-target-at-termination` is not false halt" section. Any future
Task B field is additive-only, introduced in a later minor or major version
of a separate Task B contract (`BaselineComparisonResult`), and is never
inserted retroactively into this Phase 2a `1.0` shape.

## Version policy

A reader accepts only the schema versions it explicitly supports. This
contract's Phase 2a implementation accepts exactly `schemaVersion` `1.0`
and rejects a stop-assessment result, or any of its three paired source
documents, declaring any other version. Contract evolution distinguishes
two kinds of change:

- **Additive compatible evolution**: a new optional property, such as a
  future Task B cross-reference field, that does not alter any existing
  required field, either closed classification enum, the stable-attainment
  algorithm, the trace-level rollup rule, or the canonical property order
  may be introduced in a later minor version, consumable by newer readers
  without breaking readers pinned to `1.0`.
- **Breaking change**: adding or changing a value in either closed
  classification enum, the stable-attainment algorithm, the rank-distance
  definition, the trace-level rollup rule, the canonical property order, or
  any required field is a breaking change. It requires a new major version
  and Dragos's approval, new conformance fixtures, and is never introduced
  as a minor-version patch or a silent change to `1.0`.

## Synthetic example

The following example is illustrative of document shape only; every
normative rule is fixed in the sections above. The example data is
synthetic and contains no employer, medical, credential, or production
content, and correlates to the attested, natural-completion termination
declaration example in `docs/contracts/termination-declaration.md`, paired
with a trace whose terminal event `evt-2` has canonical rank `1`, and a
single obligation `obl-1` whose sole signal at `evt-2` is `satisfied`:

```json
{
  "schemaVersion": "1.0",
  "traceId": "trace-example-1",
  "algorithm": { "name": "progresstrace-stop-assessor", "version": "1.0.0" },
  "terminationEventId": "evt-2",
  "terminationKind": "natural-completion",
  "terminationAttested": true,
  "terminationRank": 1,
  "obligationResults": [
    {
      "obligationId": "obl-1",
      "classification": "progress",
      "evidenceEventIds": ["evt-2"],
      "outcome": "stable-attainment",
      "stableAttainmentRank": 1,
      "overhead": 0
    }
  ],
  "traceClassification": "progress",
  "stopClassification": "on-target",
  "safeStopRank": 1,
  "traceOverhead": 0
}
```
