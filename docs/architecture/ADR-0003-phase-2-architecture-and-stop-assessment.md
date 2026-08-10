# ADR-0003: Phase 2 overall architecture and Task A stop-assessment contracts

## Status

Accepted for Phase 2a architecture and semantic documentation; implementation
is a separate, later task. Extends ADR-0002 without modifying its decision
text, and does not modify ADR-0001. Task B (baseline comparison and
false-halt cost) has its evidence-model direction selected and its
artifact/CLI boundary fixed by this document, but its contract architecture
is not complete and its contracts are not authored here: the exact unit,
formula, and interpretation of false-halt magnitude remain unfixed until a
future ADR-0004, per "Task B follow-up gate" below.

## Context

Phase 1 stabilized the obligation ledger and evaluation-result contracts but
explicitly deferred both halves of ProgressTrace's core product claim to
Phase 2: whether a trace stopped safely relative to its own declared
obligations, and whether stopping was a false halt relative to some
independent baseline. ADR-0002 deferred both because no document defined what
a baseline or a halt event structurally was. A series of discovery passes
converged on a composable architecture and eleven dependency-ordered
decisions, `P2-D1` through `P2-D11`, that Dragos reviewed and approved,
subject to one required controller amendment: the derived attestation field
is named `terminationAttested`, not `attestedHalt`, and
`terminationKind=unknown` is non-attested alongside `capture-truncated`,
because an unknown termination kind is not itself positive evidence that the
agent stopped. The full discovery record, the itemized `P2-D1`-`P2-D11`
options and rationale, Dragos's decision-by-decision approval, and the
independent read-only architecture review that preceded this ADR are
retained in the controller's external audit record, outside this repository;
this ADR transcribes those approved decisions as this repository's normative,
self-contained architecture and does not reopen them, and this repository's
contracts and code never depend on that external record at build, test, or
run time.

Architecture-only authorization for this vertical slice has been recorded by
the controller as approved for architecture, not yet approved for
implementation. Implementation of Task A begins only under a separate, later
task contract (`tasks/phase-2a-stop-assessment.json`), and implementation of
Task B begins only after a further ADR-0004 fixes the exact evidentiary
model, unit, and formula for false-halt cost and that ADR is itself
independently reviewed.

Extended by ADR-0004 (baseline comparison and the authored-estimate evidence
model), which fixes Task B's exact unit, formula, interpretation ceiling,
`BaselineDefinition`/`BaselineComparisonResult` contract text, and `PT4xx`
diagnostic allocation, and records the independent-review gate this
document's "Task B follow-up gate" section required. That independent review
subsequently approved the immutable architecture candidate with no findings
or unverified assumptions. ADR-0004 is therefore Accepted for implementation
planning, with integration and implementation still subject to their separate
human gates. No other line of this document's decision is modified.

## Decision

1. **Phase 2 decomposes into two composable, bounded tasks (`P2-D5`)**: Task
   A, single-trace deterministic stop assessment and late-termination
   overhead, computed from a single trace, its obligation ledger, and a newly
   authored `TerminationDeclaration`; and Task B, baseline comparison and
   false-halt cost, computed by additionally consuming an externally authored
   counterfactual reference. Task A is fully specifiable and implementable
   now. Phase 2 is not complete, and the product brief's false-halt-cost
   claim remains unmet, until Task B ships or Dragos explicitly amends that
   claim; this document does not itself amend the product brief.
2. **Termination is a structured, authored attestation, not a bare final-event
   pointer (`P2-D1`, amended)**. A new versioned `TerminationDeclaration`
   contract requires `terminationEventId`, which must dereference the paired
   trace's own maximum-canonical-rank event (terminal-required), and a
   required `terminationKind` drawn from the closed public eight-value enum
   `agent-self-reported-stop`, `harness-declared-stop`, `natural-completion`,
   `external-cancellation`, `timeout`, `crash-or-error`, `capture-truncated`,
   `unknown`. A derived boolean, `terminationAttested`, is `true` for the six
   kinds that assert some concrete, authored cause and `false` for exactly
   `capture-truncated` and `unknown`. `terminationAttested=true` never means
   "the agent voluntarily chose to halt"; it means only that the
   termination-declaration producer (which `declarationSource` may record as a
   role distinct from the trace source) committed to one specific, named cause
   for why observation ended where it did, as opposed to `unknown` (no
   committed cause) or
   `capture-truncated` (the capture itself, not necessarily the agent's
   activity, is known to have ended early). No field in this contract or in
   `StopAssessmentResult` ever asserts voluntary agent intent. The
   declaration also carries a required closed `declarationSource` object,
   placed after `terminationKind` in canonical root order, recording who
   produced it (`producerType` in `{agent, harness, operator, adapter}`,
   `producerName`, `producerVersion` as a non-empty string or `null`) and on
   what evidence (`evidenceBasis` in `{agent-output, harness-lifecycle,
   operator-annotation, adapter-inference}`), so producer provenance is kept
   separate from the termination cause and no agent is required to produce
   the declaration. Five closed coherence rules bind `declarationSource` to
   `terminationKind` and internally (an `agent-self-reported-stop` requires
   `agent-output`; a `harness-declared-stop` requires `harness-lifecycle`; a
   `producerType=agent` requires `agent-output`; a `producerType=operator`
   requires `operator-annotation`; and `evidenceBasis=adapter-inference`
   requires `producerType=adapter`), with all other combinations accepted so
   a harness or adapter may faithfully wrap evidence of a different origin. A
   violated rule is diagnostic `PT303`, whose exact pointer targeting,
   message, and validation order are fixed in
   `docs/contracts/termination-declaration.md`. `terminationAttested` is
   unchanged by `declarationSource`: it stays a cause-commitment lookup, not
   an evidence-quality or truth-confidence score, so an `adapter-inference`
   declaration with a concrete cause remains a producer-supplied inference and
   is never relabeled as observed agent output or a direct agent assertion.
3. **Success is fixed at the contract level, not authored (`P2-D2`)**: the
   only success target for every ledger-declared obligation is
   `status=satisfied`; no per-obligation target field exists anywhere in
   Task A's contracts.
4. **Stable attainment replaces first-reach (`P2-D3`)**: for an obligation's
   signals in the existing Phase 1 total order `s1..sn`, stable attainment is
   the canonical rank of the dereferenced event of the earliest signal `sk`
   such that every signal from `sk` through `sn` has `status=satisfied` (the
   start of the maximal trailing all-`satisfied` run through termination).
   It is undefined whenever `sn` is not `satisfied`, including `n=0`. An
   obligation whose stable-attainment rank is undefined is
   `unmet-target-at-termination`: a single-trace, non-counterfactual absence of
   sustained target attainment computed from the supplied trace and ledger
   artifacts, computed the same way whether the obligation never reached
   `satisfied`, reached it and regressed away without recovering, or has zero
   signals. It is never called a false halt; see "Why `unmet-target-at-termination`
   is not false halt" below.
5. **Trace-level cost never sums per-obligation values (`P2-D4`)**: every
   ledger-declared obligation is required (no authored required-subset
   field); `safeStopRank` is the maximum stable-attainment rank across all
   obligations, defined only when `terminationAttested=true` and every
   obligation has a defined stable-attainment rank; `traceOverhead` is
   `terminationRank - safeStopRank`. Per-obligation `overhead` is
   `terminationRank - stableAttainmentRank`, reported diagnostically for
   every obligation that has stable attainment regardless of
   `terminationAttested`, and is never added across obligations to produce
   `traceOverhead`, because trailing trace events after the last
   obligation's stable attainment are shared, not obligation-exclusive, and
   summing would double count them.
6. **Rank distance is defined exactly once (`P2-D3`/`P2-D4` shared
   vocabulary)**: for canonical ranks `a <= b`, `b - a` is the count of trace
   events whose canonical rank is strictly greater than `a` and less than or
   equal to `b`, i.e. every event after `a` up to and including `b`. This
   phrase, "rank distance," is used for every subtraction in Task A's
   contracts and is never a raw `events[].sequence` delta, since `sequence`
   is only required to be strictly increasing, not contiguous.
7. **Four single-responsibility artifacts, authored versus derived kept
   separate (`P2-D8`)**: Task A introduces `TerminationDeclaration`
   (authored) and `StopAssessmentResult` (derived), mirroring the existing
   ledger/evaluation-result separation. Task B's future artifacts,
   `BaselineDefinition` (authored, shape gated on `P2-D6`) and
   `BaselineComparisonResult` (derived), are named and architecturally
   reserved by this document but not authored: no schema, diagnostic, or CLI
   verb for either exists yet.
8. **CLI verb and precedence (`P2-D9`)**: a new `assess <trace-path>
   <ledger-path> <termination-declaration-path>` verb, three positional
   arguments, extending the existing usage line without changing it,
   preserving the one-file-per-artifact positional convention already used
   by `validate`, `normalize`, and `evaluate`. Validation proceeds
   trace-then-ledger-then-declaration: each document validates to completion
   with zero diagnostics before the next is ever opened, extending
   `evaluate`'s trace-then-ledger precedent by one further stage.
9. **`StopAssessmentResult` recomputes internally; there is no fourth
   precomputed input (`P2-D10`, principle recorded for Task A now and
   reused by Task B later)**: `assess` invokes the unmodified Phase 1
   `Evaluator` directly against the validated trace and ledger to obtain
   `obligationResults`, `evidenceEventIds`, and `traceClassification`; it
   never reads a precomputed `EvaluationResult` file. Task B will apply the
   same recompute-internally principle to `StopAssessmentResult` once
   `ADR-0004` authors it.
10. **Fail-closed missing coverage (`P2-D11`, principle recorded now,
    inherited by Task B)**: Task A introduces no new coverage-gap surface,
    since every ledger-declared obligation is already required by Decision 5
    above and the existing ledger/trace referential rules already fail
    closed on any dangling or missing reference. This principle is recorded
    here because Task B's future baseline coverage, if `P2-D6` selects a
    model requiring per-obligation baseline entries, must fail closed the
    same way, mirroring `PT202`/`PT203`'s existing dangling-reference
    precedent, rather than introduce a soft partial-coverage classification.
11. **Diagnostics**: Task A allocates a new `PT3xx` diagnostic block,
    distinct from the trace-envelope `PT1xx` and ledger `PT2xx` blocks, for
    `TerminationDeclaration`-specific failures that have no existing
    precedent: `PT300`-`PT302` for referential and terminality failures and
    `PT303` for `declarationSource` coherence failures. All four
    (`PT300`-`PT303`) are allocated by this document for Task A now. Task B's
    future `BaselineDefinition`-specific failures are not carried forward in
    the remainder of the `PT3xx` block; instead they are reserved to a fresh,
    as-yet-unallocated `PT4xx` block, to be allocated only by a future
    `ADR-0004`. No `PT3xx` code beyond `PT303` is reserved for or claimed by
    Task B. The exact code identities, pointer-target rules, validation
    order, and `assess` CLI stdout/stderr/exit-code shape are fixed in
    `docs/contracts/termination-declaration.md`'s "Diagnostic registry",
    "Validation order", and "CLI assess behavior" sections; Codex implements
    them without inventing a code, pointer, message, pass order, or exit
    behavior.
12. **A new ADR-0003 is authored, and exactly one Status line is appended to
    `docs/architecture/ADR-0002-obligation-and-evaluation-contracts.md`**
    noting the extension; no other line of ADR-0002's existing decision text
    changes, mirroring ADR-0002's own precedent for extending ADR-0001.
13. **The Phase 2a executable task specification lives at one file,
    `tasks/phase-2a-stop-assessment.json`**, mirroring the existing
    `tasks/phase-1-obligation-evaluation.json` convention.
14. **Agent-output independence (`P2-D12`)**: no agent is ever required to
    emit ProgressTrace JSON or a ProgressTrace-native final answer.
    ProgressTrace analyzes outputs and evidence that agents and their
    environment actually produced. A source may already emit an artifact that
    happens to be contract-compatible, but that is optional and never
    required. Opaque trace payload text is not interpreted by the
    deterministic core: without explicit obligations and correlated signals
    there is no universal semantic-progress inference, and `insufficient-evidence`
    is the honest outcome.
15. **Adapter boundary (`P2-D13`)**: source-native evidence is captured
    first, then normalized by an adapter, harness, controller, operator, or
    optionally the agent itself into canonical ProgressTrace input artifacts.
    A ProgressTrace canonical contract is an internal
    interoperability/analysis boundary after capture, not an agent-native
    wire protocol and not a claimed industry standard. An adapter may transform
    arbitrary source evidence into these artifacts, preserving whatever
    provenance the target contract records, which differs by contract: the
    trace envelope `1.0` records the source system and version in required
    document-level `source.name` and `source.version` but no per-field
    producer/evidence-basis metadata or adapter-transform manifest;
    `ObligationLedger` `1.0` carries no producer/evidence-basis metadata at
    all; and `TerminationDeclaration` `1.0`'s `declarationSource` is the first
    ProgressTrace contract carrier that explicitly separates producer role
    from evidence basis (per `P2-D14`), not the first provenance metadata of
    any kind. Industry standardization is not a prerequisite for this internal
    canonical contract, and a future external standard would be an ingestion
    source through an adapter, never the canonical domain model.
16. **Provenance before interpretation (`P2-D14`)**: this is a normative rule
    for every new, provenance-aware artifact boundary: where a contract
    carries producer and evidence-basis metadata, agent-produced,
    harness-observed, operator-annotated, adapter-inferred, and core-derived
    facts must not be conflated, the deterministic core must not read semantic
    meaning from opaque payload text, and any model or adapter inference must
    be labeled as inference rather than presented as observed fact or a direct
    agent assertion. Task A implements this rule for `TerminationDeclaration`
    through the required `declarationSource` object (Decision 2 below) and
    preserves it in the result by copying `declarationSource` verbatim, lineage
    class `from-declaration`, onto `StopAssessmentResult`, which labels every
    field by artifact lineage (the eight lineage classes fixed in
    `docs/contracts/stop-assessment-result.md`), never by an epistemic
    truth or observation-quality status. This rule does not retroactively
    endow artifacts that lack producer/evidence-basis metadata with such
    provenance: `ObligationLedger` `1.0` carries none (see "Phase 1 ledger
    provenance and future adapter provenance" below), so its supplied
    obligations and signals are structured annotations of unknown producer and
    evidence provenance unless an external provenance record exists, and the
    trace envelope `1.0` records only a document-level `source.name` and
    `source.version`, not per-field producer/evidence-basis metadata; so
    ProgressTrace does not yet claim universal field-level provenance across
    all existing artifacts.
17. **Product positioning and dogfooding (`P2-D15`)**: the primary initial
    users are builders of early agent workflows that do not yet have a mature
    harness. Mature systems may integrate through adapters, independent audit,
    or conformance; ProgressTrace does not presume to replace their
    harnesses. ProgressTrace's own Claude/Codex/Hermes/Antigravity
    development workflow is the first planned reference corpus and adapter
    source — agent text remains arbitrary, task contracts provide
    obligations, and controller/tool/build/test/CI/verifier/human-gate events
    provide evidence — but that adapter is not implemented yet, is not an
    industry standard, and must not be claimed to exist.

## Ingestion boundary and product positioning

ProgressTrace draws three explicit layers, and never blurs them:

1. **Arbitrary source evidence** produced by an agent, tool, or runtime:
   free-form model text, tool output, logs, lifecycle events, and any other
   captured artifact. ProgressTrace makes no demand on its shape and does not
   interpret opaque payload text semantically.
2. **Normalized/annotated ProgressTrace input artifacts** — the trace
   envelope, obligation ledger, and termination declaration — produced by an
   adapter, harness, controller, operator, or optionally the agent itself,
   transforming layer 1 into the canonical contracts while preserving
   provenance. These contracts are an internal interoperability/analysis
   boundary after capture, not an agent-native wire protocol and not a
   claimed industry standard.
3. **Deterministic derived ProgressTrace results** — the evaluation result
   and the stop-assessment result — computed by the one normative core purely
   from layer 2, with no clock, network, database, or model dependency.

No agent is required to emit ProgressTrace-native JSON. A source may already
emit a compatible artifact, but that is optional; the normal path is that an
adapter, harness, controller, or operator captures source-native evidence and
normalizes it. Opaque trace payload remains uninterpreted by the core: absent
explicit obligations and correlated signals, there is no universal
semantic-progress inference, and `insufficient-evidence` is the honest,
deterministic outcome rather than a guessed verdict. Labeling model or adapter
inference as inference, rather than presenting it as observed fact or a direct
agent assertion, is a normative rule for every new provenance-aware boundary;
Task A realizes it for the termination declaration by recording
`declarationSource` on the declaration and copying it verbatim (lineage class
`from-declaration`) onto `StopAssessmentResult`. It does not endow artifacts
that predate producer metadata with provenance: `ObligationLedger` `1.0`
carries none, so its obligations and signals remain supplied structured
annotations of unknown producer and evidence provenance unless an external
provenance record exists, and ProgressTrace cannot yet claim a general
ingestion-adapter feature or universal field-level provenance across all
existing artifacts (see "Phase 1 ledger provenance and future adapter
provenance" below).

The primary initial users are builders of early agent workflows that do not
yet have a mature harness; for them, an operator or a thin adapter authors the
input artifacts by hand or from simple logs. Mature systems may instead
integrate through adapters, independent audit, or conformance, and
ProgressTrace does not presume to replace their existing harnesses.
ProgressTrace's own Claude/Codex/Hermes/Antigravity development workflow is
the first planned reference corpus and adapter source, where agent text stays
arbitrary, task contracts supply obligations, and
controller/tool/build/test/CI/verifier/human-gate events supply evidence; that
adapter is planned, not implemented, is not an industry standard, and is not
claimed to exist yet. Any optional hosted service, UI, or persistent storage
remains explicitly optional and out of Phase 2a scope.

## Phase 1 ledger provenance and future adapter provenance

Phase 1's obligation-ledger obligations and status signals are explicit,
structured annotations consumed by the deterministic evaluator. They are not
necessarily agent responses, and they are never semantic facts the core
infers from opaque payload text; the core computes progress only from these
explicit, correlated annotations, returning `insufficient-evidence` when they
are absent. `ObligationLedger` `1.0` currently carries no producer/evidence-basis
metadata analogous to `TerminationDeclaration`'s `declarationSource`: it does
not record whether an obligation or signal was agent-produced,
harness-observed, operator-annotated, or adapter-inferred, so its supplied
annotations have unknown such provenance unless an external record exists.
This differs from the trace envelope `1.0`, which does record a document-level
`source.name` and `source.version` (the producing system's name and version),
though still no per-field producer/evidence-basis metadata or adapter-transform
manifest. This is a known, bounded limitation, and it is not silently solved
by Task A's `declarationSource`, which applies only to the termination
declaration.

Before ProgressTrace claims a general ingestion-adapter feature complete, a
future ADR must decide how ledger-level (and, more broadly, artifact-level)
producer provenance is recorded — either a non-breaking adapter
manifest/provenance envelope or a new, compatible contract version — under the
same additive-only, accept-only-known-versions policy the existing contracts
use. That schema is deliberately not designed by ADR-0003 or Phase 2a Task A;
Phase 2a neither adds producer metadata to `ObligationLedger` `1.0` nor
changes its frozen shape.

## Why `unmet-target-at-termination` is not false halt

`unmet-target-at-termination` is a single-trace, non-counterfactual statement
computed from supplied trace and ledger artifacts: it reports only that a
specific obligation's signals do not end in a maximal trailing run of
`satisfied` through the declared termination event. It carries no claim about what would, could, or should have happened
had the trace continued, because Task A has no independent reference to
compare against: no second trace, no fixed continuation policy, and no
authored counterfactual budget. Calling this a "false halt" would assert a
counterfactual the data cannot support and was the specific defect repaired
across this architecture's discovery passes, per the controller's external
audit record of that repair. "False halt" is reserved exclusively for Task B,
where it is a
baseline-referenced claim computed only once an independent reference exists
under one of the evidence models `P2-D6` will select among. Task A's
contracts never use the words "false halt," "false-halt," or any synonym for
an unmet target; `docs/contracts/stop-assessment-result.md`'s classification
vocabulary is exhaustive on this point.

## Task B follow-up gate

Task B (`BaselineDefinition`, `BaselineComparisonResult`, a future `compare`
CLI verb, and a fresh `PT4xx` diagnostic block, not any code in the `PT3xx`
range, whose Task A allocation now runs through `PT303`) has its
decision-level boundary fixed by `P2-D6` through `P2-D8`, `P2-D10`, and
`P2-D11` above and in the controller's external audit record, but its
contract architecture is not complete and it is not authored by this
document. `P2-D6` selects
among four evidence models for false-halt magnitude: a paired independent
baseline execution (a comparator/proxy, never proof of the stopped run's own
counterfactual future), an externally authored counterfactual budget
(explicitly labeled `authored-estimate`, never represented as observed fact
or counterfactual proof), a deterministic built-in stopping policy (a
verdict only, no numeric magnitude without a separate cost model), or an
explicit product-scope amendment computing no numeric magnitude at all.
Dragos's approved decision record favors the externally authored
counterfactual budget as the first cut, but its exact unit, formula, and
interpretation are not fixed by this ADR or by any other Phase 2a document.
**Implementation of Task B, including any `BaselineDefinition` or
`BaselineComparisonResult` schema, code, fixture, or CLI verb, is prohibited
until a future `ADR-0004`:**

- fixes the exact unit and formula for false-halt magnitude under the
  selected evidence model;
- fixes the exact interpretation of that magnitude, so it is never presented
  as observed fact or as proof of the stopped run's own counterfactual
  future;
- is itself independently reviewed under the same controller-verification
  and Antigravity Mode A strict read-only review pattern used for this
  ADR's own architecture review, before any implementation task contract
  for Task B is authored.

Until `ADR-0004` exists and clears that gate, `docs/product-brief.md`'s core
product claim's false-halt-cost half remains explicitly unmet, exactly as
Phase 1 left it, and no code in this repository may claim to measure it.

## Normative boundaries

- **Canonical rank**: the zero-based position of a trace event in the trace
  envelope's already-normative canonical order (`sequence` ascending, then
  timestamp instant, then `id` ordinal), fixed in
  `docs/contracts/trace-envelope.md`'s "Compatibility and normalization"
  section. Canonical rank is always contiguous `0..N-1` over all `N` events
  in the trace, regardless of gaps in raw `sequence` values, and is computed
  identically for Task A's rank arithmetic as for `TraceNormalizer`'s
  ordering.
- **Rank distance**: for canonical ranks `a <= b`, `b - a` is the count of
  trace events with canonical rank strictly greater than `a` and less than
  or equal to `b`. This is the sole subtraction semantics used anywhere in
  `docs/contracts/stop-assessment-result.md`; a raw `events[].sequence`
  difference is never substituted for it.
- **Terminal-required rule**: `TerminationDeclaration.terminationEventId`
  must dereference an event id present in the paired trace envelope, and
  that event must be the trace's own maximum-canonical-rank event. Both a
  dangling reference and a non-terminal (but existing) reference fail
  structural/referential validation (exit 1) and never reach stop
  assessment; the exact codes and pointers are fixed in
  `docs/contracts/termination-declaration.md`.
- **Termination attestation**: `terminationAttested` is `true` for
  `terminationKind` in `{agent-self-reported-stop, harness-declared-stop,
  natural-completion, external-cancellation, timeout, crash-or-error}` and
  `false` for `terminationKind` in `{capture-truncated, unknown}`. This is a
  fixed lookup table over the closed enum, never an open or authored
  boolean, and is not itself a claim that a non-attested termination did not
  happen; it only withholds the specific, named-cause commitment the six
  attested kinds provide. It is a cause-commitment lookup, not an
  evidence-quality or truth-confidence score, and it is independent of
  `declarationSource`.
- **Declaration source**: `TerminationDeclaration` carries a required closed
  `declarationSource` object after `terminationKind`, with members
  `producerType` (`{agent, harness, operator, adapter}`), `producerName`
  (non-empty, never echoed in diagnostics), `producerVersion` (non-empty
  string or `null`), and `evidenceBasis` (`{agent-output, harness-lifecycle,
  operator-annotation, adapter-inference}`), in that nested order. Its five
  coherence rules and the single `PT303` code (pointer
  `/declarationSource/evidenceBasis` for the `terminationKind`-anchored rules,
  `/declarationSource` otherwise) are fixed in
  `docs/contracts/termination-declaration.md`. `StopAssessmentResult` copies
  the whole object verbatim as lineage class `from-declaration`, preserving
  nested order, so operator-annotated and adapter-inferred inputs stay
  distinguishable from a direct agent assertion downstream.
- **Result lineage labels**: `StopAssessmentResult` labels every field with
  exactly one of eight mutually exclusive artifact-lineage classes —
  `contract-constant`, `from-trace`, `from-ledger`, `from-declaration`,
  `derived-from-declaration`, `derived-from-trace-and-declaration`,
  `derived-from-trace-and-ledger`, and `derived-from-all-inputs` — that name
  only source artifacts and the inputs a computation consumed, never truth,
  observation quality, producer identity, or evidence confidence, and never an
  epistemic status such as "observed" or "authored". The exact per-field
  mapping is fixed in `docs/contracts/stop-assessment-result.md`'s "Provenance
  labels" section and must agree with it; these are documentation-only labels,
  with no runtime `sourceClass` property added to the `1.0` shape.
- **Stable-attainment reuse of the existing signal total order**: stable
  attainment is computed over the exact same per-obligation signal total
  order already fixed in `docs/contracts/obligation-ledger.md` ("Signal
  total order"), ascending by dereferenced event canonical position (rank),
  ties on identical `eventId` broken by `signals`-array index. No new
  tie-break rule is introduced; the abandoned-terminal rule from Phase 1
  continues to apply unmodified during ledger validation, before Task A's
  assessment ever runs.
- **`obligationResults` reuse**: Task A's `obligationResults` entries carry
  the unmodified Phase 1 `classification` and `evidenceEventIds` values,
  recomputed by invoking `ProgressTrace.Core.Evaluation.Evaluator` directly,
  in addition to the new `outcome`, `stableAttainmentRank`, and `overhead`
  fields defined in `docs/contracts/stop-assessment-result.md`. No
  reimplementation of the classification table or trace-level precedence
  rule is permitted outside that existing namespace.
- **Diagnostics and CLI order**: the exact `PT300`-`PT303` codes, their
  pointer-target rules, `TerminationDeclaration`'s two-phase validation
  order, and `assess`'s full stdout/stderr shape and 0/1/2 exit-code mapping,
  including trace-then-ledger-then-declaration precedence, are fixed in
  `docs/contracts/termination-declaration.md`'s "Diagnostic registry",
  "Validation order", and "CLI assess behavior" sections.

## Contract and version strategy

`TerminationDeclaration` and `StopAssessmentResult` both start at
`schemaVersion 1.0`, using the same major.minor accept-only-known-versions
policy already documented for the trace envelope, obligation ledger, and
evaluation result. Evolution is additive-only: any future Task B field
(`falseHalt`, a magnitude, or a source label) is added only to Task B's own
new contracts, never inserted into `StopAssessmentResult` `1.0` or into the
frozen `EvaluationResult` `1.0` shape. The closed `terminationKind` enum, the
required closed `declarationSource` object (its four members, their nested
order, its two closed enums, and its five coherence rules), the
terminal-required rule, the stable-attainment rule, and the non-summed
`traceOverhead` formula fixed in this document and in
`docs/contracts/termination-declaration.md`/`docs/contracts/stop-assessment-result.md`
are Phase 2a `1.0` normative behavior; any change to them is a breaking
change requiring a new major version and Dragos's approval, never a silent
patch. Phase 2a fixtures live in a new `fixtures/termination/**` subtree,
distinct from `fixtures/valid/**`, `fixtures/invalid/**`, and
`fixtures/obligations/**`, to avoid collision with existing Phase 0/Phase 1
fixtures; all existing Phase 0 and Phase 1 acceptance commands continue to
pass unchanged.

## Zero-dependency decision

`TerminationDeclaration` structural/referential validation and
`StopAssessmentResult` computation and normalization introduce zero new
third-party runtime dependencies. `TerminationDeclarationValidator` shares
the same internal structural-validation helpers already refactored for
`TraceValidator` and `ObligationLedgerValidator` (unknown-property rejection,
duplicate-property rejection, size-limit enforcement) rather than
reimplementing or duplicating them; Codex determines the concrete refactor
as long as behavior is preserved and no normative algorithm is duplicated.
The deterministic stop-assessment algorithm (canonical rank, terminal-rank
lookup, termination attestation, stable-attainment scan, rank-distance
arithmetic, and trace-level rollup) lives in a new
`ProgressTrace.Core.Assessment` namespace, which invokes but never
reimplements `ProgressTrace.Core.Evaluation.Evaluator`; no other namespace
may reimplement any part of this logic.

## Execution ownership

Codex implements `contracts/termination-declaration.schema.json`,
`contracts/stop-assessment-result.schema.json`, the
`ProgressTrace.Core` validator and assessment logic, the CLI `assess` verb,
fixtures, conformance-test coverage, and CI updates from this ADR and the
paired contract documents; Codex owns no architecture, ADR, or contract-
documentation file. Hermes, the controller, first independently executes
every deterministic acceptance command against the Codex candidate with zero
writable repository paths, audits that only Codex's allowed paths changed
and that the frozen trace-envelope, obligation-ledger, and evaluation-result
contracts remain byte-identical to `main`, and assembles an evidence packet
(command outputs, before/after repository fingerprints, and a diff bundle
restricted to Codex's allowed paths) written outside the repository. For
implementation review, Hermes then provisions a disposable, credential-free
checkout or worktree pinned to the exact Codex candidate commit, together
with that evidence packet, and hands both to Antigravity in Mode B
disposable reproducibility review. In that mode, Antigravity may
independently execute build, conformance, and CLI acceptance commands with
ordinary build tooling inside the disposable checkout Hermes provisioned,
and may write only ephemeral build artifacts (for example compiler output)
there; it has no network access, no credentials, no GitHub authority, no
push or merge authority, and no write access to the original repository or
to any location outside that disposable copy. Antigravity returns a
structured verdict as JSON on stdout for Hermes to persist, and the
disposable environment is deleted once the verdict is recorded. This is
distinct from the Mode A strict read-only review already used for this
ADR's own architecture/documentation discovery, where the reviewer holds no
repository, build, run, or write tool of any kind and never checks out a
working copy; Mode B is used only for implementation reproducibility review,
never for documentation-only review. Dragos reviews the Codex diff, Hermes's
evidence packet, and Antigravity's verdict, and owns the final integration
decision via the normal PR flow. No role edits another role's owned
artifacts, and no role substitutes for another.

## Security

`TerminationDeclaration` is treated exactly like the trace envelope and the
obligation ledger: an untrusted authored document, structurally validated
and never executed, interpreted as a command, or treated as anything but
data, per `docs/security.md`. No network, database, shell, or model
invocation is introduced anywhere in the `assess` path; stop assessment is a
pure, local, deterministic function of its three validated inputs.
Diagnostics for `TerminationDeclaration` failures follow the existing
`Diagnostic{Code, Pointer, Message}` shape and must not echo document
values, matching the `PT004` duplicate-property redaction precedent. A
dangling or non-terminal `terminationEventId`, a `traceId` mismatch, and an
out-of-enum `terminationKind` all fail closed with a diagnostic rather than
being silently ignored, defaulted, or best-effort matched. The exact stable
code, pointer-target rule, and validation order for every one of these
diagnostics is fixed in `docs/contracts/termination-declaration.md`'s
"Diagnostic registry" and "Validation order" sections, which are normative
for `assess`'s CLI behavior. All new fixtures remain synthetic, with no
employer, medical, credential, or production data, per `docs/security.md`'s
fixture rule.

## Consequences

- Three coupled input files, trace, ledger, and declaration, require callers
  to keep them synchronized, mitigated by the mandatory `traceId` match
  invariant already proven for the ledger and reused identically for the
  declaration.
- `StopAssessmentResult` does not yet demonstrate the false-halt half of the
  core product claim; `docs/product-brief.md` and this ADR's "Task B follow-up
  gate" record this as an explicit, tracked deferral, not a silent omission.
- `terminationAttested=false` (for `unknown` and `capture-truncated` alike)
  always suppresses `safeStopRank` and `traceOverhead`, even when every
  obligation happens to be mechanically stable-attainment; this is a
  deliberate accuracy choice, not a defect, since no attestation confirms a
  real, named-cause termination occurred.
- A closed `terminationKind` enum may force a future major-version bump,
  requiring Dragos's approval, if real termination models need a value this
  eight-value vocabulary does not cover; an open vocabulary was rejected for
  the same determinism reasons the obligation-status enum was closed in
  ADR-0002.
- Per-obligation `overhead` remaining purely diagnostic, never summed,
  means no single number describes "total wasted obligation-turns"; only
  `traceOverhead`, gated on every obligation being stable-attainment, is
  ever offered as a trace-level cost figure.

## Alternatives considered

- **End-of-observation, unattested (no `TerminationDeclaration` at all)**:
  derive the last trace event automatically with no authored field, naming
  every quantity with end-of-observation vocabulary and never claiming a
  halt occurred. Rejected because it permanently blocks the false-halt half
  of the product claim: no field would ever attest a real, named-cause
  termination, so Task B could never distinguish a genuine stop from a mere
  end of capture.
- **Optional `terminationKind`, defaulting to `unknown`**: rejected because
  an optional attestation is not an attestation; it would let every caller
  silently avoid ever attesting termination, reintroducing the
  optional/required contradiction already rejected once for the ledger's
  required fields.
- **Per-obligation authored success-target subset or a versioned
  `SuccessCriterion` built on Evaluator classifications**: rejected in favor
  of fixing the target at `status=satisfied` for every obligation; no
  evidence exists that any obligation needs a looser or different target,
  and either alternative reintroduces authored-field coverage-validation
  surface with no evidenced need.
- **First-reach target rank**: rejected because it reports a misleadingly
  favorable overhead when an obligation regresses and never recovers,
  exactly the defect this architecture's discovery passes repaired.
- **Final-status-boolean-only (no rank or overhead at all)**: rejected
  because it loses the late-termination overhead measurement half of Task
  A's own claim even for the fully observed, fully attested case.
- **Authored required-obligation subset, or a diagnostic-only model with no
  trace-level aggregate**: rejected in favor of "every obligation required,
  `safeStopRank` = max stable-attainment rank"; no evidence exists that any
  obligation is genuinely optional for a safe-stop determination, and
  dropping the trace-level aggregate entirely would lose the
  `traceOverhead` late-termination figure even where the data fully
  supports it.
- **A single combined Phase 2 task covering both stop assessment and
  baseline comparison, or an over-fragmented multi-task split**: rejected;
  a single combined task blocks the fully-observed half of Phase 2 on the
  harder, still-undecided baseline-evidence question, while
  over-fragmenting adds coordination overhead (e.g. separate tasks for
  declaration validation versus stable-attainment computation) with no
  evidenced benefit over the two-task split adopted here.
- **Merged authored/derived contracts (`TerminationDeclaration` folded into
  `StopAssessmentResult`, or a future combined baseline document)**:
  rejected because it contradicts the raw/authored/derived separation
  principle already used to reject a combined trace+ledger document in
  Phase 1, and makes it harder to audit which fields are authored
  assertions versus computed results.
- **Declaration path via a flag such as `--termination`** instead of a third
  positional argument: rejected in favor of the existing one-file-per-
  artifact positional convention, mirroring the already-approved rejection
  of a `--ledger` flag in ADR-0002.
- **Accepting a precomputed `StopAssessmentResult` file as a Task B input**
  instead of recomputing it internally: rejected (recorded now for Task B's
  future benefit) because it would require inventing an identity/provenance
  matching rule between the supplied result and the inputs being compared,
  with no repository precedent, mirroring the same reasoning already applied
  to `EvaluationResult` recomputation in Phase 1.

## Non-goals

- Authoring `BaselineDefinition`, `BaselineComparisonResult`, their
  diagnostics, or the future `compare` CLI verb; all are deferred to
  `ADR-0004`.
- Any numeric false-halt magnitude of any kind, computed by Task A or by
  this document.
- Cross-trace identity or alignment machinery of any kind.
- A `haltReason` free-text field or any open termination-kind vocabulary.
- Wall-clock or duration-based cost measurement.
- Multi-trace history, batch comparison, dashboards, or reporting beyond
  Task A/Task B.
- UI, hosted service, database, or Parquet/DuckDB persistence, carried over
  from Phase 0/Phase 1's non-goals.
- OpenTelemetry or AI-framework ingestion adapters, carried over from
  Phase 0/Phase 1's non-goals.
- Python or TypeScript packages, carried over from Phase 0/Phase 1's
  non-goals.
- Plugin execution, LLM-as-judge, online agent interruption, or auto-merge,
  carried over from Phase 0/Phase 1's non-goals.
- Any modification to the frozen trace-envelope contract or to the Phase
  0/Phase 1 `ObligationLedger`/`EvaluationResult` `1.0` shapes.
- Beginning Codex implementation of Task A before Dragos approves this
  ADR's decisions or supplies an approved replacement; beginning any Task B
  implementation before `ADR-0004` exists, is fixed, and is independently
  reviewed.

## Bounded Phase 2a vertical slice

Phase 2a is exactly and only Task A: given an already-validated trace
envelope, its paired obligation ledger, and a newly authored, validated
`TerminationDeclaration`, deterministically compute, per obligation, a
stable-attainment rank and rank-distance overhead, or an
`unmet-target-at-termination` outcome; and, only when termination is
attested and every obligation is stably successful, a single trace-level
`safeStopRank` and non-summed `traceOverhead`. Phase 2a introduces exactly
two new artifacts: the authored `TerminationDeclaration` input and the
derived `StopAssessmentResult` output, emitted through a new `assess` CLI
verb with the same `0`/`1`/`2` exit-code convention already established by
`validate`, `normalize`, and `evaluate`. Task B's evidence-model direction
and artifact/CLI boundary are decided by this document, but Task B's
contract architecture is not complete: its exact unit, formula, and
interpretation remain unfixed until `ADR-0004`, per "Task B follow-up gate"
above. Phase 2 as a whole remains incomplete until Task B ships or Dragos
formally amends `docs/product-brief.md`'s core product claim.

## Compatibility with frozen Phase 0 and Phase 1

`contracts/trace-envelope.schema.json`, `docs/contracts/trace-envelope.md`,
`contracts/obligation-ledger.schema.json`,
`docs/contracts/obligation-ledger.md`,
`contracts/evaluation-result.schema.json`, and
`docs/contracts/evaluation-result.md` are frozen for Phase 2a: zero diffs are
permitted in any of them. Any apparent need to change them is a stop
condition requiring a new Dragos-approved major version and new conformance
fixtures, not a Phase 2a side effect. The only permitted change to
`docs/architecture/ADR-0002-obligation-and-evaluation-contracts.md` is
appending a single Status line noting extension by this ADR; every other
existing line of ADR-0002, including its Decision, Normative boundaries, and
Compatibility sections, is preserved byte-for-byte, exactly as ADR-0002
itself preserved ADR-0001. The CLI exit-code contract (`0` valid/emitted,
`1` semantic-invalid, `2` malformed, unreadable, or usage error) established
by `validate`, `normalize`, and `evaluate` is treated as a stable convention
and reused verbatim for `assess`.
