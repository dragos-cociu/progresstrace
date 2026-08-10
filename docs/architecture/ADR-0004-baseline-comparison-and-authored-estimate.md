# ADR-0004: Baseline comparison and the authored-estimate evidence model

## Status

Accepted for implementation planning — independent review approved;
integration pending. The human decisions this document transcribes
(`B1`-`B5`) and the scope of architecture-only materialization they authorize
were discovered, itemized, and approved by Dragos through the
external-audit-record process described in "Context" below, and that approval
is not reopened by this document. The independent review required below has
also completed with a clean approval against the immutable architecture
candidate. This status authorizes neither Task B implementation nor its
integration; both remain subject to the separate gates below.
Extends ADR-0003's Task B follow-up gate by fixing the exact unit, formula,
and interpretation that gate's first two conditions required, and records
the third condition, independent review, as satisfied — see
"Independent-review gate" below. Extends ADR-0002 and ADR-0001 transitively,
through ADR-0003;
no line of ADR-0001, ADR-0002, or ADR-0003 is modified in substance by this
document, beyond the single Status-linkage paragraph
`docs/architecture/ADR-0003-phase-2-architecture-and-stop-assessment.md`
appends noting this extension, mirroring the append-only precedent ADR-0002
and ADR-0003 each already established for their own predecessor.

## Context

`docs/architecture/ADR-0003-phase-2-architecture-and-stop-assessment.md`
decomposed Phase 2 into Task A (single-trace deterministic stop assessment,
implemented) and Task B (baseline comparison and false-halt cost, named and
architecturally reserved but explicitly not authored). That document's "Task
B follow-up gate" section named `BaselineDefinition`, `BaselineComparisonResult`,
a future `compare` CLI verb, and a fresh `PT4xx` diagnostic block, but
prohibited any implementation of any of them until a future ADR-0004: fixes
the exact unit and formula for false-halt magnitude under the evidence model
`P2-D6` selected (an externally authored counterfactual budget, favored over
a paired independent baseline execution, a built-in stopping policy, or a
product-scope amendment computing no magnitude); fixes the exact
interpretation of that magnitude so it is never presented as observed fact
or as proof of the stopped run's own counterfactual future; and is itself
independently reviewed before any Task B implementation task contract is
authored. This document is that ADR-0004. Its architecture, contract
vocabulary, diagnostic allocation, and CLI boundary were discovered,
itemized as decisions `B1` through `B5`, and approved by Dragos through the
same external-audit-record process ADR-0003 itself used, per that record;
this document transcribes those approved decisions as this repository's
normative, self-contained architecture and does not reopen them, and this
repository's contracts and code never depend on that external record at
build, test, or run time.

The core problem this ADR resolves is narrower than it may first appear.
Task A already established that an obligation which never reaches, or
regresses away from, `status=satisfied` before termination is
`unmet-target-at-termination`: a single-trace, non-counterfactual fact,
because Task A has no independent reference to compare against. Task B
introduces exactly one such reference — an authored `eventBudget` per
obligation — and this ADR's entire job is to fix, precisely and
non-negotiably, what comparing an observed termination point against that
authored number is allowed to mean. Every design choice below exists to
prevent that comparison from being misread as a measurement, a proof, or an
observed fact, while still making the comparison useful enough to satisfy
half of `docs/product-brief.md`'s core product claim.

## Decision

1. **Evidence model: externally authored counterfactual budget (`P2-D6`,
   selected in ADR-0003, formula fixed here)**. `BaselineDefinition` is
   authored; `BaselineComparisonResult` is derived, mirroring the
   ledger/evaluation-result and declaration/stop-assessment-result
   separations already used in Phase 1 and Phase 2a. No paired baseline
   execution and no cross-trace alignment machinery of any kind exists in
   this version; a single `BaselineDefinition` binds to exactly one
   `traceId`. Every quantity this ADR's contracts derive from an authored
   `eventBudget` is a counterfactual proxy, never an observed fact and never
   proof of the stopped run's own counterfactual future; see "Claim
   taxonomy" below for the exhaustive vocabulary this repository uses to
   keep that boundary visible in every document, diagnostic, and field
   name.
2. **Unit and formula (`B1`)**. `BaselineDefinition` carries exactly one
   `eventBudget` per ledger-declared obligation: an authored total count of
   canonical event slots, an integer in the closed range
   `1..2147483647`, never a canonical rank (zero-based) and never a raw
   `events[].sequence` value. `BaselineComparisonResult` derives
   `observedEventCount = terminationRank + 1`, converting the zero-based
   `terminationRank` Task A already computes into the same one-based
   total-count unit `eventBudget` uses, so the two are directly comparable.
   A valid `compare` input always has a terminal `TerminationDeclaration`
   (guaranteed by Task A's terminal-required rule, independent of
   attestation), so `observedEventCount` is always defined. For each
   obligation, `applicability` and `authoredEstimateUnusedEventBudget` are
   computed by a fixed, ordered, exhaustive rule: non-attested termination
   yields `incomplete-observation` unconditionally; a stably attained
   obligation yields `not-applicable-stable-attainment`; otherwise, an
   unmet obligation's `eventBudget` is compared to `observedEventCount`,
   yielding `unused-authored-budget` (with a positive
   `authoredEstimateUnusedEventBudget = eventBudget - observedEventCount`),
   `budget-exhausted-exactly` (with `authoredEstimateUnusedEventBudget =
   0`, a defined, meaningful zero), or `budget-exceeded` (with `null`, no
   invented quantity). Trace-level `authoredEstimateFalseHaltPresent` is
   `true` iff any per-obligation magnitude is positive;
   `maxAuthoredEstimateUnusedEventBudget` is the maximum such positive
   magnitude, `null` when none, and is never a sum, for the same
   shared-events reasoning `docs/contracts/stop-assessment-result.md`
   already established for `traceOverhead`. The full algorithm, in
   normative step order, is fixed in
   `docs/contracts/baseline-comparison-result.md`'s "Unit and formula" and
   "Trace-level rollup" sections; this ADR does not restate every branch.
3. **Interpretation ceiling (`B1`, interpretation half)**. A positive
   `authoredEstimateFalseHaltPresent`, or any single obligation's
   `applicability = unused-authored-budget`, states only that the observed
   termination point fell short of some author's independently supplied
   budget for that obligation. It never claims that continuing would have
   satisfied the obligation, that the authored budget was accurate, that
   termination was premature or avoidable, or that the stopped run's actual
   continuation is known. This ceiling is fixed, word-for-word in intent,
   in `docs/contracts/baseline-comparison-result.md`'s "Interpretation and
   non-claims" section, and is binding on every future consumer,
   visualization, or report built on this contract; no future minor version
   may loosen it without Dragos's approval as a breaking change.
4. **Source/provenance (`B2`)**. `BaselineDefinition.baselineSource` mirrors
   `TerminationDeclaration.declarationSource`'s producer/evidence-basis
   separation: the same four allowed producer roles (`agent`, `harness`,
   `operator`, `adapter`), the same four-member nested object shape and
   order (`producerType`, `producerName`, `producerVersion`,
   `evidenceBasis`), and the same redaction and no-privileged-truth-status
   principles. `evidenceBasis` is a new, Task-B-specific five-value enum
   (`agent-estimate`, `harness-policy`, `operator-estimate`,
   `historical-analysis`, `adapter-inference`), not a reuse of
   `declarationSource.evidenceBasis`'s four-value enum, because the two
   contracts describe different kinds of evidence (a termination cause
   versus an authored budget) and the vocabularies are not
   interchangeable. An agent-produced budget is allowed but remains
   explicitly a self-authored estimate with no privileged truth status over
   a harness-, operator-, or adapter-produced one; `baselineSource` records
   this distinction, it does not adjudicate it. When a `harness` or
   `adapter` producer wraps an `agent-estimate` or `operator-estimate`
   evidence basis, `baselineSource` preserves only that evidence-basis
   category; it does not identify the original estimator's `producerName`
   or `producerVersion` (those fields name the wrapping harness or adapter,
   not the original agent or operator), and it does not constitute a full
   provenance chain back to whoever originally produced the estimate. A
   multi-hop provenance chain of that kind is deferred; it is not a goal of
   this version and is not represented anywhere else in this repository.
5. **Coherence rules and their deterministic resolution (`B5`)**. Five
   closed rules bind `baselineSource.producerType` to
   `baselineSource.evidenceBasis`, fixed in
   `docs/contracts/baseline-definition.md`'s "Baseline-source coherence
   rules" section and reproduced here for traceability: `producerType =
   agent` requires `evidenceBasis = agent-estimate`; `producerType =
   operator` requires `evidenceBasis` in `{operator-estimate,
   historical-analysis}`; `evidenceBasis = harness-policy` requires
   `producerType = harness`; `evidenceBasis = adapter-inference` requires
   `producerType = adapter`; `evidenceBasis = historical-analysis` requires
   `producerType` in `{harness, operator, adapter}`. The second rule is not
   a literal transcription of the plain-language decision bullet
   "`producerType = operator` requires `operator-estimate`": read
   unconditionally, that bullet would conflict with the separate bullet
   permitting `operator`-produced `historical-analysis`. This ADR resolves
   that conflict deterministically, per the controller-approved decision
   record, by treating the `historical-analysis` bullet's explicit
   three-role list as a narrower, more specific exception to the general
   `operator`-label default, exactly as
   `docs/contracts/baseline-definition.md`'s "Resolving the
   `operator`/`historical-analysis` overlap" subsection documents in full;
   `producerType = agent` receives no equivalent exception because that
   three-role list deliberately omits `agent`. A violated rule is
   diagnostic `PT404`, pointer `/baselineSource` for every case (a single
   pointer target, unlike `declarationSource`'s `PT303`, because every one
   of these five rules couples two members of the same object with neither
   external to it, so there is no analog to `PT303`'s
   `terminationKind`-anchored two-pointer split); the exact validation
   order is fixed in `docs/contracts/baseline-definition.md`'s "Validation
   order" section.
6. **Binding and coverage (`B3`)**. `BaselineDefinition` `1.0` binds to
   exactly one `traceId`, mirroring `ObligationLedger` and
   `TerminationDeclaration`; a reusable cross-trace template is deferred,
   with no evidenced need yet. Exactly one budget entry must exist per
   ledger-declared obligation. Four failure shapes against that requirement
   — missing, duplicate, dangling, and extra — all fail closed, mirroring
   the fail-closed coverage principle ADR-0003's Decision 10 recorded in
   advance for Task B. "Extra" is not a fifth independent structural
   condition: by construction, any entry beyond the required
   one-per-obligation count is either a duplicate of an already-covered
   obligation id or a reference to an obligation id the ledger never
   declared, so it is fully classified by the duplicate or dangling
   checks, with no separate diagnostic code needed for it, exactly as
   `docs/contracts/baseline-definition.md`'s "Referential integrity and
   coverage" section documents.
7. **Public vocabulary (`B4`)**. `BaselineDefinition`'s root fields, in
   canonical order, are `schemaVersion`, `traceId`, `baselineSource`,
   `obligationBudgets`; each budget entry is `obligationId`, `eventBudget`,
   in that order. `BaselineComparisonResult` visibly uses
   `observedEventCount`, `authoredEstimateFalseHaltPresent`, and
   `maxAuthoredEstimateUnusedEventBudget` at the trace level, and
   `eventBudget`, `applicability`, and `authoredEstimateUnusedEventBudget`
   per obligation. `applicability` is the closed five-value enum
   `incomplete-observation`, `not-applicable-stable-attainment`,
   `unused-authored-budget`, `budget-exhausted-exactly`, `budget-exceeded`.
   No field anywhere in either contract is named `falseHaltMagnitude`, or
   any other bare name that would present an authored-estimate-derived
   quantity as an observed or measured cost; the chosen name,
   `authoredEstimateUnusedEventBudget`, carries the counterfactual-proxy
   qualification in the field name itself, not only in prose.
8. **Fresh `PT4xx` diagnostic block (Decision 11 of ADR-0003, fixed here)**.
   `PT400`-`PT404` are allocated by
   `docs/contracts/baseline-definition.md` for
   `BaselineDefinition`-specific structural, coherence, referential, and
   coverage failures with no existing precedent: `PT400` duplicate
   `obligationId`; `PT401` `traceId` mismatch; `PT402` dangling
   `obligationId`; `PT403` missing obligation coverage; `PT404`
   `baselineSource` coherence violation. `PT000`-`PT005` and `PT100`-`PT101`
   are reused unmodified in meaning, including one deliberate extension of
   `PT101`'s existing "invalid value for a field of the correct type"
   umbrella to cover an out-of-range `eventBudget` integer, consistent with
   how each prior contract document has extended `PT101` to its own new
   fields without redefining it. No code in the `PT2xx` (`ObligationLedger`)
   or `PT3xx` (`TerminationDeclaration`) blocks is reused, redefined, or
   reserved by this document; `PT3xx` remains bounded at `PT303`, exactly
   as ADR-0003 fixed. The exact pointer-target rule, message template,
   cardinality, and validation-order position for every one of `PT400`-`PT404`
   is fixed in `docs/contracts/baseline-definition.md`'s "Diagnostic
   registry" and "Validation order" sections; Codex implements them without
   inventing a code, pointer, message, cardinality, or order, exactly as the
   prior two contract documents already require of Task A.
9. **CLI verb, precedence, and recompute-internally (`P2-D9`/`P2-D10`,
   extended to Task B)**. A new `compare <trace-path> <ledger-path>
   <termination-declaration-path> <baseline-definition-path>` verb, four
   positional arguments, extending the existing usage line without
   changing it, preserving the one-file-per-artifact positional convention.
   Validation proceeds trace-then-ledger-then-declaration-then-baseline:
   each document validates to completion with zero diagnostics before the
   next is ever opened, extending `assess`'s three-stage precedence by one
   further stage. `compare` never accepts a precomputed `StopAssessmentResult`
   file as an input; it recomputes every Task A quantity it needs
   internally, by invoking the unmodified Phase 2a
   `ProgressTrace.Core.Assessment` and
   `ProgressTrace.Core.Evaluation.Evaluator` logic over the validated
   trace, ledger, and declaration, exactly as `assess` does, applying
   `P2-D10`'s recompute-internally principle to Task B as ADR-0003 already
   anticipated. `compare` preserves the `0`/`1`/`2` exit-code convention
   unchanged: `0` only when a `BaselineComparisonResult` is emitted, `1`
   for a diagnostic on any of the four documents when none is malformed,
   oversized, or unreadable, `2` for bad usage or a malformed, oversized,
   or unreadable document. The exact stdout/stderr shape and step order are
   fixed in `docs/contracts/baseline-definition.md`'s "CLI compare
   behavior" section.
10. **No implementation is authorized by this document.** This ADR fixes
    architecture and contract text only. `contracts/baseline-definition.schema.json`,
    `contracts/baseline-comparison-result.schema.json`, any
    `ProgressTrace.Core` validator or comparison logic, the CLI `compare`
    verb, fixtures, and conformance-test coverage do not exist yet and are
    not created by this document. No task contract for Task B
    implementation exists yet and none is created by this document; one may
    be authored only after this ADR clears the independent-review gate
    fixed below, mirroring exactly the gate ADR-0003 itself already passed
    before Task A's task contract, `tasks/phase-2a-stop-assessment.json`,
    was authored.

## Claim taxonomy

Every value this ADR's two contracts define falls into exactly one of four
categories; keeping this taxonomy explicit is the single most
consequence-bearing decision in this document, since the whole purpose of
deferring Task B behind this ADR was to prevent an authored guess from ever
being silently upgraded into something it is not.

- **Observed**: a value copied unchanged from a validated input document
  that itself records what was captured about the trace's actual execution
  — for example `terminationEventId`, `terminationKind`, and every trace
  event a ledger signal or a termination declaration dereferences. Observed
  values are not thereby guaranteed accurate or complete (an adapter may
  have inferred them), but they describe something that was captured about
  the run that actually happened.
- **Derived**: a value computed deterministically, by a fixed algorithm,
  purely from observed and/or authored inputs — for example
  `terminationAttested`, `terminationRank`, `observedEventCount`, and every
  Task A `outcome` this ADR's contracts recompute and carry forward.
  Derived values are exact and reproducible given their inputs, but they
  are only ever as meaningful as those inputs.
- **Authored**: a value supplied by a named producer as an explicit
  assertion about the world, not observed from the trace's own execution —
  `BaselineDefinition.obligationBudgets[].eventBudget` and the whole
  `baselineSource` object are the canonical Task B examples, alongside the
  already-existing `TerminationDeclaration.declarationSource`,
  `ObligationLedger.obligations`, and `ObligationLedger.signals` from prior
  phases. An authored value is data about what someone asserted, never data
  about what was measured.
- **Counterfactual-proxy**: a value derived from an authored input that
  stands in for an unknown, unobserved continuation of the trace —
  `applicability`, `authoredEstimateUnusedEventBudget`,
  `authoredEstimateFalseHaltPresent`, and `maxAuthoredEstimateUnusedEventBudget`
  are the only counterfactual-proxy values this repository defines as of
  this ADR. Every counterfactual-proxy value is also, mechanically, a
  derived value (it is computed by a fixed algorithm), but it earns its own
  category because its derivation traces back through an authored guess
  about a continuation that never happened and was never observed, which no
  other derived value in this repository does. `unmet-target-at-termination`
  and `unmet-target-at-termination-present`, from Task A, are explicitly
  **not** counterfactual-proxy values: they are single-trace derived facts
  with no authored counterfactual input at all, which is exactly why Task A
  was permitted to ship before this ADR existed and why
  `docs/contracts/stop-assessment-result.md`'s "Why `unmet-target-at-termination`
  is not false halt" section remains correct and unmodified.

`docs/contracts/baseline-comparison-result.md`'s "Interpretation and
non-claims" section states the consumer-facing consequence of this
taxonomy: a counterfactual-proxy value never asserts what a derived or
observed value is permitted to assert, no matter how confidently it is
computed or how deterministically it is reproduced.

## Normative boundaries

- **`eventBudget` unit**: an authored total count of canonical event slots,
  integer `1..2147483647`, never a canonical rank and never a raw
  `events[].sequence` value, fixed in
  `docs/contracts/baseline-definition.md`'s "Document structure" section.
- **`observedEventCount` formula**: `terminationRank + 1`, fixed in
  `docs/contracts/baseline-comparison-result.md`'s "Unit and formula"
  section; the sole conversion between Task A's zero-based canonical rank
  and Task B's one-based authored-budget unit.
- **Applicability precedence**: `terminationAttested = false` is tested
  before any per-obligation branch and overrides every obligation's own
  `outcome`; `outcome = stable-attainment` is tested next; the
  `eventBudget`-versus-`observedEventCount` three-way comparison is tested
  last, only for an unmet, attested obligation. This order is fixed,
  exhaustive, and reused unmodified from
  `docs/contracts/baseline-comparison-result.md`'s "Unit and formula"
  section.
- **No summation**: `maxAuthoredEstimateUnusedEventBudget` is a maximum,
  never a sum, over exactly the positive per-obligation magnitudes, for the
  same shared-trailing-events reasoning already fixed for
  `StopAssessmentResult.traceOverhead`.
- **`PT4xx` allocation**: `PT400`-`PT404`, fixed in
  `docs/contracts/baseline-definition.md`'s "Diagnostic registry" section;
  no other code in that block is claimed by this document, and none of
  `PT200`-`PT204` or `PT300`-`PT303` is redefined, reused, or reinterpreted.
- **Coherence-rule resolution**: the five-rule table and its documented
  resolution of the `operator`/`historical-analysis` overlap, fixed in
  `docs/contracts/baseline-definition.md`'s "Baseline-source coherence
  rules" section, is Phase 2b `1.0` normative behavior on equal footing
  with every other rule in that table, not a provisional reading.

## Contract and version strategy

`BaselineDefinition` and `BaselineComparisonResult` both start at
`schemaVersion 1.0`, using the same major.minor accept-only-known-versions
policy already documented for every prior contract. Evolution is
additive-only: a future field (for example a second evidence model's
output, should `P2-D6`'s reserved alternatives ever be revisited) is added
only to these contracts' own later minor or major versions, never inserted
into `TraceEnvelope`, `ObligationLedger`, `EvaluationResult`,
`TerminationDeclaration`, or `StopAssessmentResult` `1.0`. The closed
`baselineSource.evidenceBasis` five-value enum, the closed `applicability`
five-value enum, the `eventBudget` unit and range, the
`observedEventCount` formula, the applicability precedence order, the
non-summation rule, and the five coherence rules and their resolution fixed
in this document and in
`docs/contracts/baseline-definition.md`/`docs/contracts/baseline-comparison-result.md`
are Phase 2b `1.0` normative behavior; any change to them is a breaking
change requiring a new major version and Dragos's approval, never a silent
patch. Phase 2b fixtures, once implementation is authorized, will live in a
new `fixtures/baseline/**` subtree, distinct from every existing fixture
subtree, to avoid collision; all existing Phase 0, Phase 1, and Phase 2a
acceptance commands must continue to pass unchanged, since this document
changes no existing contract, fixture, or code path.

## Zero-dependency decision

This ADR introduces zero new third-party runtime dependencies, and
authorizes none for Task B's future implementation. When implementation is
eventually authorized, `BaselineDefinitionValidator` must share the same
internal structural-validation helpers already refactored for
`TraceValidator`, `ObligationLedgerValidator`, and
`TerminationDeclarationValidator` (unknown-property rejection,
duplicate-property rejection, size-limit enforcement) rather than
reimplementing or duplicating them, exactly as ADR-0002 and ADR-0003 already
require of every prior validator. The future deterministic
baseline-comparison algorithm (the applicability precedence, the
`observedEventCount` formula, and the trace-level rollup) must live in a new
namespace that invokes, but never reimplements, `ProgressTrace.Core.Assessment`
and `ProgressTrace.Core.Evaluation.Evaluator`; no other namespace may
reimplement any part of Task A's or Task B's normative algorithms. This
constraint binds whichever future task contract eventually authorizes Task
B implementation; it is recorded now so that constraint cannot be silently
weakened later.

## Execution ownership

This document is authored under the same architecture-and-contract-only
ownership boundary ADR-0002 and ADR-0003 already established: Codex owns no
architecture, ADR, or contract-documentation file, and implements only from
an ADR and its paired contract documents once a task contract authorizes
that implementation. No such task contract exists for Task B yet, and this
document does not create one (see "Implementation gate" below). When a
future task contract does authorize Task B implementation, the same
Hermes-independent-execution, Antigravity-Mode-B-disposable-reproducibility-review,
Dragos-final-integration pattern ADR-0003's "Execution ownership" section
fixes for Task A applies unmodified to Task B, substituting
`contracts/baseline-definition.schema.json`,
`contracts/baseline-comparison-result.schema.json`, the `compare` CLI verb,
and a new `fixtures/baseline/**` subtree for Task A's corresponding
artifacts; no role edits another role's owned artifacts, and no role
substitutes for another.

## Security

`BaselineDefinition` is treated exactly like the trace envelope, the
obligation ledger, and the termination declaration: an untrusted authored
document, structurally validated and never executed, interpreted as a
command, or treated as anything but data, per `docs/security.md`. No
network, database, shell, or model invocation is introduced anywhere in the
future `compare` path; baseline comparison is specified as a pure, local,
deterministic function of its four validated inputs, with the same
no-hidden-state guarantee already proven for `evaluate` and `assess`.
Diagnostics for `BaselineDefinition` failures follow the existing
`Diagnostic{Code, Pointer, Message}` shape and must not echo document
values, matching the `PT004` duplicate-property redaction precedent; this
is why `PT403`'s message never names the specific ledger obligation id it
found missing, and why `PT404`'s message never names the specific
`producerType`/`evidenceBasis` combination it rejected. A duplicate,
dangling, missing, or extra `obligationBudgets` entry, a `traceId`
mismatch, an out-of-range `eventBudget`, and an incoherent `baselineSource`
all fail closed with a diagnostic rather than being silently ignored,
defaulted, clamped, or best-effort matched. All new fixtures, once
implementation is authorized, must remain synthetic, with no employer,
medical, credential, or production data, per `docs/security.md`'s fixture
rule; this ADR authorizes no fixture to exist yet.

The security-relevant property specific to this ADR, beyond every prior
untrusted-input rule it inherits, is that an authored `eventBudget` is
untrusted *content*, not only untrusted *structure*: nothing in this
architecture validates that a supplied budget is honest, accurate, or
free of an author's incentive to inflate or deflate it to produce a
favorable or unfavorable `authoredEstimateFalseHaltPresent`. This
architecture does not attempt to solve that incentive problem; it solves
only the narrower, tractable problem of making every budget's authorship
and evidence basis visible and auditable via `baselineSource`, so a
downstream reader can weigh a `harness-policy`-sourced budget differently
from an `agent-estimate`-sourced one if they choose to. See "Non-goals"
below.

## Consequences

- `BaselineComparisonResult` demonstrates the false-halt half of
  `docs/product-brief.md`'s core product claim only in the specific,
  bounded, authored-estimate sense this ADR fixes; it does not, and cannot,
  demonstrate a stronger claim than the evidence model `P2-D6` selected
  supports. `docs/product-brief.md` is updated only to record that this
  contract architecture now exists, not to claim the stronger,
  unconditional reading of "false-halt cost" a casual reading of the
  product brief's original language might suggest.
- Four coupled input files, trace, ledger, declaration, and baseline
  definition, require callers to keep them synchronized, extending the
  three-file burden `docs/architecture/ADR-0003-phase-2-architecture-and-stop-assessment.md`
  already recorded as a consequence, mitigated the same way: a mandatory
  `traceId` match invariant, now proven for a fourth document.
  `BaselineDefinition`'s single-trace binding means a new baseline
  definition must be authored for every distinct trace compared, with no
  reusable-template relief in this version; this is recorded as a known,
  deferred limitation, not a defect.
  `authoredEstimateFalseHaltPresent = true` reflects a specific author's
  specific budget choice, never a property of the trace independent of who
  authored the comparison baseline. Two different `BaselineDefinition`
  documents, both otherwise valid, can legitimately disagree about whether
  the same trace shows `authoredEstimateFalseHaltPresent = true`, purely
  because they authored different `eventBudget` values; this is intended
  behavior, not a nondeterminism defect, since the algorithm itself remains
  fully deterministic given a fixed baseline definition.
- The `PT404` coherence-rule resolution documented in "Coherence rules and
  their deterministic resolution" above means this repository's own
  decision record contains an internal tension that this ADR resolves
  rather than silently papering over; a future reader auditing the
  original controller decision record against this ADR's contract text
  should expect to find that documented resolution, not a literal,
  unqualified transcription of every decision bullet.
- A closed `evidenceBasis` five-value enum and a closed `applicability`
  five-value enum may each force a future major-version bump, requiring
  Dragos's approval, if a real evidence model or comparison outcome needs
  more granularity than these enums provide; open vocabularies were
  rejected for both, for the same determinism reasons every other closed
  enum in this repository was rejected as open.

## Alternatives considered

- **Paired independent baseline execution** (a second, comparator trace):
  rejected as the first-cut evidence model, per `P2-D6`'s already-recorded
  preference, because it requires cross-trace identity and alignment
  machinery with no repository precedent and a materially larger
  implementation surface, for a benefit (an executed comparator rather
  than an authored guess) that remains available as a later, additive
  evidence model if evidenced need arises; nothing in this ADR's contracts
  forecloses adding it later as a new, separate evidence-model contract.
- **Deterministic built-in stopping policy** (a verdict only, no numeric
  magnitude without a separate cost model): rejected as the first-cut
  model because it would answer a narrower question (was stopping
  policy-compliant) than the product claim's false-halt-cost language
  asks for (how much was left unused), and would still require this same
  authored-estimate machinery, or something like it, to attach a magnitude
  later.
- **Explicit product-scope amendment computing no numeric magnitude at
  all**: rejected as the first-cut model because evidenced demand exists
  for a magnitude, and the authored-estimate model supplies one without
  requiring the harder paired-execution or built-in-policy machinery;
  remains available as a fallback if the authored-estimate model proves
  unworkable in practice, per "Falsification criteria" below.
- **Literal, unqualified transcription of the `operator`/`historical-analysis`
  coherence bullets, accepting the resulting contradiction**: rejected,
  since a contradictory validation rule cannot be implemented
  deterministically at all; some resolution was mandatory, and the
  documented specific-overrides-general resolution in "Coherence rules and
  their deterministic resolution" above was chosen because it preserves
  every plain-language bullet as true under some reading, rather than
  discarding any one of them.
- **Reusing `declarationSource.evidenceBasis`'s four-value enum for
  `baselineSource`** instead of a new five-value enum: rejected because
  `historical-analysis` has no analog in the termination-cause vocabulary,
  and because conflating "evidence for why a trace stopped" with "evidence
  for an authored budget guess" would blur two structurally different
  claims this ADR's entire purpose is to keep separate.
- **A single pointer-target rule mirroring `PT303`'s two-target split**
  (`/baselineSource/evidenceBasis` for some rules, `/baselineSource` for
  others): rejected in favor of one uniform pointer,
  `/baselineSource`, for every `PT404` violation, because none of this
  document's five coherence rules has an external anchor field analogous
  to `declarationSource`'s root-level `terminationKind`; every rule here
  couples two members of the same object, so `PT303`'s
  neither-individually-authoritative reasoning applies uniformly, not
  selectively.
- **A separate per-obligation boolean field mirroring the plain-language
  "present" language** in addition to `applicability`: rejected because it
  is fully redundant with `applicability = unused-authored-budget`
  (equivalently, `authoredEstimateUnusedEventBudget > 0`), and an
  additional field carrying no new information would only add a second
  place a future implementation or reader could let go stale relative to
  `applicability`.
- **Carrying `StopAssessmentResult`'s full field set** (`stableAttainmentRank`,
  per-obligation `overhead`, `safeStopRank`, `traceOverhead`,
  `traceClassification`, `stopClassification`) onto
  `BaselineComparisonResult`: rejected in favor of carrying only the
  specific Task A fields `applicability`'s own algorithm consumes
  (`outcome`, `terminationAttested`, `terminationRank`). A caller who needs
  Task A's late-termination-overhead figures runs `assess` directly against
  the same three files; duplicating every Task A field here would blur
  which document is authoritative for which claim and increase the
  drift-on-change surface between two independently versioned contracts.
- **Accepting a precomputed `StopAssessmentResult` file as a `compare`
  input** instead of recomputing it internally: rejected for the same
  reason ADR-0003 already rejected it for a hypothetical Task B input,
  recorded there in advance: no repository precedent exists for an
  identity/provenance matching rule between a supplied result and the
  inputs being compared.

## Non-goals

- Any implementation of `contracts/baseline-definition.schema.json`,
  `contracts/baseline-comparison-result.schema.json`, a
  `BaselineDefinitionValidator`, a baseline-comparison algorithm, the
  `compare` CLI verb, fixtures, or conformance-test coverage.
- Authoring or approving a Task B implementation task contract.
- Validating, scoring, or otherwise adjudicating the honesty, accuracy, or
  good faith of any authored `eventBudget`; this architecture makes
  authorship and evidence basis visible and auditable, it does not attempt
  to detect or prevent an author from inflating or deflating a budget.
- A paired independent baseline execution, cross-trace identity, or
  cross-trace alignment machinery of any kind, carried over from `P2-D6`'s
  rejected-for-now alternatives.
- A reusable, cross-trace `BaselineDefinition` template; `1.0` binds to
  exactly one `traceId`.
- Any numeric magnitude computed as a sum, average, or other aggregate
  across obligations; `maxAuthoredEstimateUnusedEventBudget` is a maximum
  only.
- A bare `falseHaltMagnitude` field, or any field name that presents an
  authored-estimate-derived quantity without a name-level counterfactual
  qualification.
- A full, multi-hop provenance chain identifying an original estimator's
  `producerName`/`producerVersion` when a `harness` or `adapter` wraps an
  `agent-estimate` or `operator-estimate` evidence basis; `baselineSource`
  preserves only the evidence-basis category in that case, not the original
  estimator's identity, and no other contract in this repository represents
  that chain either.
- Wall-clock or duration-based cost measurement, carried over from Phase
  2a's non-goals.
- Any new network, database, shell, or model dependency, carried over from
  every prior phase's non-goals.
- Any modification to the frozen trace-envelope, obligation-ledger,
  evaluation-result, termination-declaration, or stop-assessment-result
  contracts, or to their existing diagnostic codes (`PT000`-`PT005`,
  `PT100`-`PT104`, `PT200`-`PT204`, `PT300`-`PT303`).
- Beginning any Task B implementation before this ADR clears the
  independent-review gate below and a Dragos-approved task contract exists.

## Implementation gate

Task B implementation of any kind — `contracts/baseline-definition.schema.json`,
`contracts/baseline-comparison-result.schema.json`, any
`ProgressTrace.Core` validator or comparison logic, the CLI `compare` verb,
fixtures, or conformance-test coverage — remains prohibited until: this ADR
is independently reviewed per "Independent-review gate" below; a
Dragos-approved task contract for Task B implementation is separately
authored, mirroring `tasks/phase-2a-stop-assessment.json`'s precedent for
Task A; and that task contract's own authorization is explicit, not implied
by this ADR's existence. This document, by itself, authorizes none of that
implementation, exactly as ADR-0003 authorized none of this document's
contract text.

## Independent-review gate

`docs/architecture/ADR-0003-phase-2-architecture-and-stop-assessment.md`'s
"Task B follow-up gate" required this ADR to be "itself independently
reviewed under the same controller-verification and Antigravity Mode A
strict read-only review pattern used for [ADR-0003's] own architecture
review, before any implementation task contract for Task B is authored."
This document fixes this ADR's contract text and satisfies that gate's first
two conditions (the exact unit/formula and the exact interpretation ceiling).
The third condition was subsequently satisfied by an independent stateless,
tool-free review of immutable candidate commit
`dfdc4b9a206bc231706fcc981cc8d811e96d49c8`. The controller validated the
review packet and verdict identities; the verdict was `approve`, with no
findings and no unverified assumptions. That review did not execute or claim
to execute repository, build, run, network, or write tools. It therefore
records completion of this architecture-review gate without authorizing
implementation. A separate Dragos-approved Task B implementation contract
and explicit implementation authorization remain mandatory.

## Falsification criteria

The authored-estimate evidence model this ADR selects is a first cut, not a
final commitment; the following observations, if they occur once this
architecture reaches real use, would falsify the specific design choices
above and warrant revisiting `P2-D6`'s alternatives rather than patching
around them:

- If authored `eventBudget` values prove systematically unobtainable or
  unreliable in practice (for example, no role in a real workflow is
  positioned to author a meaningful per-obligation budget before a trace
  runs), the authored-estimate model has failed its basic precondition, and
  a deterministic built-in stopping policy or a paired-execution model
  should be revisited.
- If `authoredEstimateFalseHaltPresent` is observed, in practice, to be
  read or reported by any consumer as an unqualified false-halt claim
  despite this document's and `docs/contracts/baseline-comparison-result.md`'s
  "Interpretation and non-claims" language, the field naming and
  documentation strategy has failed, and a stronger technical barrier (for
  example, a mandatory wrapping/labeling requirement enforced at every
  known consumption point, not only in prose) is warranted.
- If real budgets are observed to be authored specifically to manufacture a
  favorable or unfavorable `authoredEstimateFalseHaltPresent` result at a
  rate that undermines the metric's usefulness, the "Security" section's
  explicit non-goal (this architecture does not adjudicate budget honesty)
  should be revisited, potentially by requiring `evidenceBasis =
  harness-policy` for any use case where an incentive-neutral producer is
  required.
- If the five-value `applicability` enum or the five-role coherence-rule
  resolution in "Coherence rules and their deterministic resolution" proves
  to not cover a real evidence-basis combination that a legitimate producer
  needs, that is evidence the resolution chosen here was too narrow, and a
  new minor or major version, per the version policy above, should extend
  it rather than working around it with an out-of-band field.

## Bounded Phase 2b architecture scope

Phase 2b, as fixed by this document, is exactly and only the contract
architecture for Task B: given an already-validated trace envelope, its
paired obligation ledger, its paired termination declaration, and a newly
authored, validated `BaselineDefinition`, a deterministic algorithm that
would compute, per obligation, an `applicability` classification and an
`authoredEstimateUnusedEventBudget`, and, for the trace overall, whether any
authored-estimate-relative unused budget is present and its maximum. This
document fixes that algorithm's exact unit, formula, interpretation ceiling,
provenance model, coherence rules, diagnostic codes, and CLI boundary,
completely enough for implementation to proceed once authorized, but
implements none of it. Phase 2, and `docs/product-brief.md`'s core product
claim, remain incomplete until a future, separately authorized Task B
implementation ships and passes the same deterministic, independently
reviewed acceptance process every prior phase has passed.

## Compatibility with frozen Phase 0, Phase 1, and Phase 2a

`contracts/trace-envelope.schema.json`, `docs/contracts/trace-envelope.md`,
`contracts/obligation-ledger.schema.json`,
`docs/contracts/obligation-ledger.md`,
`contracts/evaluation-result.schema.json`,
`docs/contracts/evaluation-result.md`,
`contracts/termination-declaration.schema.json`,
`docs/contracts/termination-declaration.md`,
`contracts/stop-assessment-result.schema.json`, and
`docs/contracts/stop-assessment-result.md` are frozen by this document
exactly as ADR-0003 already froze the first five of them and established
the sixth: zero diffs are permitted in any of them as a consequence of this
ADR. Any apparent need to change them is a stop condition requiring a new
Dragos-approved major version and new conformance fixtures, not a Phase 2b
side effect. The only permitted change to
`docs/architecture/ADR-0003-phase-2-architecture-and-stop-assessment.md` is
appending a single Status-linkage paragraph noting extension by this ADR;
every other existing line of ADR-0003, including its Decision, Normative
boundaries, "Task B follow-up gate," and Compatibility sections, is
preserved byte-for-byte, exactly as ADR-0002 and ADR-0003 each preserved
their own predecessor. The CLI exit-code contract (`0` valid/emitted, `1`
semantic-invalid, `2` malformed, unreadable, or usage error) established by
`validate`, `normalize`, `evaluate`, and `assess` is treated as a stable
convention and is fixed, for future reuse verbatim by `compare`, in
`docs/contracts/baseline-definition.md`'s "CLI compare behavior" section.
