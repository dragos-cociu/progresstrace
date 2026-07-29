# ADR-0002: Obligation ledger and evaluation-result contract boundary

## Status

Accepted for Phase 1 architecture and semantic documentation; implementation
is a separate, later task. Extends ADR-0001 without modifying its decision
text.

## Context

Phase 0 stabilized the trace-envelope contract and a deterministic
validate/normalize core, but implements no obligation model or evaluation
semantics. ProgressTrace's core product claim is that it can explain whether
an agent trace reduced explicit task obligations, produced useful evidence,
stagnated, or regressed. Realizing any part of that claim requires two new
things: an explicit, structured way to declare obligations and report their
status, and a deterministic function that classifies obligations and a
trace from those structured signals. No document in this repository yet
defines what a baseline or a halt event structurally is, so specifying
baseline or cost semantics now would invent undocumented behavior rather
than describe an evidenced model. Dragos approved decisions D1A-D5A before
this ADR was authored; those decisions are transcribed here, not reopened.

## Decision

1. Introduce two new versioned, language-neutral contracts, the obligation
   ledger and the evaluation result, implemented only in the one normative
   .NET core, `ProgressTrace.Core`. The Phase 0 trace-envelope contract and
   ADR-0001 remain unmodified in decision substance and are extended only
   via the Status-line append described under "Compatibility with frozen
   Phase 0" below.
2. Baseline comparison and false-halt or late-halt cost semantics are
   completely absent from Phase 1 and are explicitly deferred to Phase 2,
   pending a future ADR once a baseline or cost model is evidenced (D2A).
3. Evaluation reads only explicit, structured obligation-status signals
   correlated to trace event ids; it never infers progress from
   unstructured payload text.
4. The obligation-status vocabulary is the fixed closed enum `open`,
   `in-progress`, `satisfied`, `regressed`, `abandoned` (D1A).
5. The CLI adds one new verb, `evaluate <trace-path> <ledger-path>`, two
   positional file arguments, preserving the existing one-file-per-artifact
   convention instead of merging trace and ledger into one document (D3A).
6. A new ADR-0002 is authored, and exactly one Status line is appended to
   `docs/architecture/ADR-0001-contract-first-modular-monolith.md` noting
   the extension; no other line of ADR-0001's existing decision text
   changes (D4A).
7. The Phase 1 executable task specification lives at one file,
   `tasks/phase-1-obligation-evaluation.json` (D5A).

## Normative boundaries

- **Signal total order**: for each obligation, its associated signals are
  totally ordered ascending by the sequence value of the trace event
  dereferenced by each signal's `eventId`, never by JSON array order alone;
  signals sharing an identical `eventId` are tie-broken by their index
  position in the ledger's `signals` array. This same total order governs
  both classification and the abandoned-terminal rule.
- **Abandoned-terminal rule**: abandoned is terminal. For a given
  obligation, if any signal has `status=abandoned`, no other signal for
  that same obligation may occupy a position after it in the total order
  above, whether that later position arises from a strictly greater
  dereferenced sequence or, for signals tied on an identical `eventId`,
  from a higher `signals`-array index. Violation always fails structural
  validation (exit 1) and never reaches evaluation.
- **Referential integrity**: every `ObligationSignal.eventId` must
  dereference an event id present in the paired trace envelope; every
  `ObligationSignal.obligationId` must dereference an obligation id
  declared in `obligations`; `ledger.traceId` must equal
  `envelope.traceId`. Dangling references, id mismatches, duplicate
  obligation ids, and an empty `obligations` array always fail structural
  validation (exit 1) and never reach evaluation.
- **Per-obligation classification table**: computed over the totally
  ordered signal sequence `s1..sn` for each obligation, using
  `rank(open)=0`, `rank(in-progress)=1`, `rank(satisfied)=2`: `n=0` yields
  `insufficient-evidence`; for `n>=1`, `sn=abandoned` yields `stagnation`
  and `sn=regressed` yields `regression`; `n=1` with `sn` in `{satisfied,
  in-progress}` yields `progress`; `n=1` with `sn=open` yields
  `stagnation`; for `n>1` with `sn` in `{open, in-progress, satisfied}`,
  compare `rank(sn)` to `rank(s1)`, treating `s1=regressed` as `rank -1`
  for this comparison only: `rank(sn) > rank(s1)` yields `progress`,
  `rank(sn) = rank(s1)` yields `stagnation`, `rank(sn) < rank(s1)` yields
  `regression`. There is no `n>1, s1=abandoned` branch: abandoned-terminal
  validation guarantees abandoned can only ever be the final signal when
  present, so any obligation whose first ordered signal is abandoned
  necessarily has `n=1`.
- **Trace-level precedence**: the highest-precedence classification
  present among all per-obligation results, under the fixed order
  `regression > insufficient-evidence > stagnation > progress`. This rule
  is presence-based, not count-based, so no tie case can arise. An
  obligation ledger with zero declared obligations is rejected at
  validation and never reaches this step.
- **Insufficient-evidence boundary**: an obligation with zero associated
  signals is structurally valid and is the only source of the
  `insufficient-evidence` classification; a dangling `eventId` or
  `obligationId` reference is instead always a validation failure, never
  an evaluation classification.

## Contract and version strategy

Both new contracts start at `schemaVersion 1.0`, using the same
major.minor accept-only-known-versions policy documented in
`docs/contracts/trace-envelope.md`. Evolution is additive-only: future
baseline or cost fields must be added as new optional properties in a
later minor or major version, never inserted retroactively into the Phase 1
`1.0` evaluation-result shape. The obligation-status vocabulary,
classification table, precedence rule, and signal total-order or
abandoned-terminal rule fixed in this document are the Phase 1 `1.0`
normative behavior; any change to them is a breaking change requiring a new
major version and Dragos's approval, not a silent patch. Phase 1 fixtures
live in a new `fixtures/obligations/**` subtree to avoid collision with
existing Phase 0 fixtures, and all existing Phase 0 acceptance commands
continue to pass unchanged.

## Zero-dependency decision

Obligation-ledger structural validation, evaluation classification, and
result normalization introduce zero new third-party runtime dependencies.
`ObligationLedgerValidator` and `TraceValidator` share one set of internal
structural-validation helpers (unknown-property rejection,
duplicate-property rejection, and size-limit enforcement), refactored into
a shared internal utility rather than left as `TraceValidator`-private
members called across classes or reimplemented; Codex determines the
concrete refactor as long as behavior is preserved and no normative
algorithm is duplicated. The deterministic evaluator lives in a new
`ProgressTrace.Core.Evaluation` namespace as the sole implementation of the
classification table and precedence rule; no other namespace may
reimplement this logic.

## Execution ownership

Codex implements `contracts/obligation-ledger.schema.json`,
`contracts/evaluation-result.schema.json`, the `ProgressTrace.Core`
validator and evaluator, the CLI `evaluate` verb, fixtures,
conformance-test coverage, and CI updates from this ADR and the paired
contract documents; Codex owns no architecture, ADR, or contract-
documentation file. Hermes independently executes every deterministic
acceptance command against the Codex candidate with zero writable
repository paths, audits that only Codex's allowed paths changed and that
the frozen Phase 0 contract is byte-identical to `main`, and assembles an
evidence packet (command outputs, before/after repository fingerprints, and
a diff bundle restricted to Codex's allowed paths) written outside the
repository. Antigravity receives only Hermes's evidence packet and diff
bundle, holds no repository, git, build, run, or write tool of any kind,
performs a read-only structural and security review, and returns a
structured verdict as JSON on stdout for Hermes to persist; Antigravity
never checks out the repository or invokes a build/run/restore command.
Dragos reviews the Codex diff, Hermes's evidence packet, and Antigravity's
verdict, and owns the final integration decision via the normal PR flow. No
role edits another role's owned artifacts, and no role substitutes for
another.

## Security

Obligation descriptions and any free-text ledger fields are treated
exactly like trace event payloads: preserved as opaque data, never
executed, never interpreted as commands, links, or code, per
`docs/security.md`. No network, database, shell, or model invocation is
introduced anywhere in the evaluation path; the evaluator is a pure, local,
deterministic function. Diagnostics for obligation-ledger and
evaluation-result failures follow the existing `Diagnostic{Code, Pointer,
Message}` shape and must not echo ledger or payload values, matching the
`PT004` duplicate-property redaction precedent. Dangling or mismatched
references, an empty `obligations` array, and abandoned-terminal
violations, whether sequence-based or same-`eventId` tie-index-based, fail
closed with a diagnostic rather than being silently ignored or best-effort
matched. All new fixtures remain synthetic, with no employer, medical,
credential, or production data, per `docs/security.md`'s fixture rule.

## Consequences

- Two coupled input files, trace and ledger, require callers to keep them
  synchronized, mitigated by the mandatory `traceId` match invariant.
- Two ADRs must be maintained instead of one; ADR-0001's decision text is
  never altered in substance, preserving auditability, and the extension
  relationship remains discoverable from ADR-0001's Status section.
- `EvaluationResult` does not yet demonstrate the full core product claim's
  cost-comparison half; `docs/product-brief.md` and `README.md` state this
  as explicit Phase 2 deferral rather than silent omission.
- A closed status enum may force a future major-version bump, requiring
  Dragos's approval, if real obligation models need more granularity than
  `open`, `in-progress`, `satisfied`, `regressed`, `abandoned`; an open
  vocabulary was rejected because it risks non-deterministic or
  unclassifiable evaluation-result outputs, and extending this closed enum
  is treated as a breaking change under the version policy in
  `docs/contracts/obligation-ledger.md` and
  `docs/contracts/evaluation-result.md`, not a minor-version patch.

## Alternatives considered

- **Amend ADR-0001 directly** instead of authoring ADR-0002: rejected
  because it would alter Phase 0's decision text, weakening auditability of
  what was actually decided and when.
- **One combined document bundling trace and ledger**: rejected because it
  would require a new wrapper contract and conflate raw and evaluated
  concerns, contradicting the raw/normalized/evaluated separation
  principle.
- **Ledger path via a required flag such as `--ledger`** instead of a
  second positional argument: rejected in favor of the existing
  one-file-per-artifact positional convention already used by `validate`
  and `normalize`.
- **Open or extensible obligation-status vocabulary**: rejected in favor of
  a fixed closed enum, mirroring the trace envelope's `actor` enum
  precedent, to keep evaluation-result classification fully deterministic.
- **Reserving optional baseline or cost fields in `EvaluationResult` now**:
  rejected because no document in this repository defines what a baseline
  or a halt event structurally is; reserving fields now would produce
  meaningless placeholder semantics rather than an evidenced model.
- **A per-sub-task task directory such as `tasks/phase-1/*.json`**:
  rejected in favor of one file at `tasks/phase-1-obligation-evaluation.json`,
  mirroring the existing Phase 0 convention for tooling and verification
  discoverability.

## Non-goals

- Baseline comparison and false-halt or late-halt cost measurement, fully
  deferred to Phase 2.
- Multi-trace history, comparison, or reporting.
- Any natural-language or semantic interpretation of payload or obligation
  description text.
- Obligation-authoring tooling or a user interface.
- UI, hosted service, database, or Parquet/DuckDB persistence, carried over
  from Phase 0's non-goals.
- OpenTelemetry or AI-framework ingestion adapters, carried over from
  Phase 0's non-goals.
- Python or TypeScript packages, carried over from Phase 0's non-goals.
- Plugin execution, LLM-as-judge, online agent interruption, or auto-merge,
  carried over from Phase 0's non-goals.
- Beginning Codex implementation before Dragos approves decisions D1A-D5A
  or supplies an approved replacement.

## Compatibility with frozen Phase 0

`contracts/trace-envelope.schema.json` and `docs/contracts/trace-envelope.md`
are frozen for Phase 1: zero diffs are permitted. Any apparent need to
change them is a stop condition requiring a new Dragos-approved major
version and new Phase 0 conformance fixtures, not a Phase 1 side effect.
The only permitted change to
`docs/architecture/ADR-0001-contract-first-modular-monolith.md` is
appending a single Status line noting extension by this ADR; every other
existing line of ADR-0001, including its Decision and Phase 0 consequences
sections, is preserved byte-for-byte. The CLI exit-code contract (`0`
valid, `1` semantic-invalid, `2` malformed, unreadable, or usage error)
established by `validate` and `normalize` is treated as a stable convention
and reused verbatim for `evaluate`.
