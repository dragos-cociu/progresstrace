# ADR-0007: Task-contract ledger generation (Phase 4.3)

## Status

Accepted for Phase 4.3 architecture by explicit human direction from Dragos
on 2026-08-20. Implementation is a separate, later task governed by
`tasks/phase-4-3-ledger-generator.json`. Extends ADR-0002 without modifying
its decision text; adopts the capability-role vocabulary and provider/model
neutrality fixed by ADR-0005; does not implement, reopen, or depend on the
attestation layer proposed in ADR-0006.

## Context

`docs/project-roadmap-and-status.md` section 6 replaces Phase 4's evidence
sources: instead of retrospective conversation reconstruction, `ledger.json`
must come from the task contract written before a session runs. Today every
`ObligationLedger` in this repository is hand-authored. Task contracts
(`tasks/phase-*.json`) already carry two kinds of relevant structured data —
an array of `deliverables` and, depending on the task, either an array of
`required_contract_decisions` or an array of `required_invariants` — written
as short, discrete, obligation-shaped statements. They also carry an
`objective` narrative paragraph and an `acceptance_commands` array of shell
argv vectors, neither of which is a list of discrete claims: `objective` is
free text and `acceptance_commands` is executable material, not a
declaration of what must become true.

`ObligationLedger` 1.0 (`docs/contracts/obligation-ledger.md`) is frozen: its
schema, referential-integrity rules, signal total order, and abandoned-
terminal rule are Phase 1 contract, changeable only by a new major version
with Dragos's approval. It also carries no field for provenance back to a
source document, because Phase 1 never had a generator; ledgers were
authored directly. A generator that invents such a field inside
`ObligationLedger` would be a breaking change under ADR-0002's version
policy. Separately, `ObligationLedger.traceId` must equal a real trace
envelope's `traceId`, but no trace envelope exists yet at the moment a task
contract is written or a ledger is generated from it: the trace is produced
later, by the session the ledger will be evaluated against.

Dragos's decision for Task 4.3 is **Narrow**: generate ledgers only from
task contracts that expose the structured fields this ADR names; never
touch `ObligationLedger` 1.0; require the caller to supply `traceId`
explicitly at generation time; and keep provenance and coverage entirely in
a separate, independently versioned sidecar report rather than inside the
ledger.

## Decision

1. **Scope is Narrow.** The generator processes exactly one input shape: a
   task contract JSON document matching the structure already in use under
   `tasks/*.json`. It is not a general document-to-obligation converter, is
   not an ingestion adapter, and makes no claim of applying to any other
   document type.
2. **Exact structured sources.** The generator reads obligation candidates
   from exactly two places in a task contract, and no others:
   - every entry of `deliverables`, if that property is present and is a
     non-empty array;
   - every entry of `required_contract_decisions`, if present and a
     non-empty array, **or**, if that property is absent, every entry of
     `required_invariants`, if present and a non-empty array. A task
     contract in this repository uses one or the other, never both
     (`tasks/phase-4-0-c-verifiable-invariants.json` uses
     `required_invariants`; every other existing task contract uses
     `required_contract_decisions`). The generator reads whichever of the
     two keys is present; if both happen to be present and non-empty, it
     reads both, since nothing in this decision forbids that shape.
   Each qualifying array entry becomes exactly one candidate obligation.
   Entries that are not non-empty strings after trimming are a fail-closed
   condition (Decision 9), never silently skipped.
3. **`objective`, `acceptance_commands`, and all other free text are
   excluded.** `objective` is narrative and is never parsed for obligations
   at any granularity (sentence, clause, or bullet). `acceptance_commands`
   is executable material describing how to check a result, not a
   declaration of what must be true, and is never converted to an
   obligation, never executed, and never inspected beyond confirming it is
   not one of the two permitted source fields. `non_goals`,
   `stop_conditions`, `human_gates`, `allowed_paths`, `protected_paths`,
   `depends_on`, `title`, `id`, `repository`, `base_branch`, and every other
   task-contract property are likewise never read as an obligation source.
   This exclusion list is closed, not illustrative: a future need to widen
   it is a new architecture decision, not a generator default.
4. **Policy for a task contract with no structured fields.** If, after
   Decision 2, zero candidate obligations exist — `deliverables` absent or
   empty, and both `required_contract_decisions` and `required_invariants`
   absent or empty — the generator produces no ledger and no report. This
   is not a fallback to `objective` or any other free text; it is a
   fail-closed rejection with an explicit diagnostic (Decision 9), because
   `ObligationLedger` 1.0 itself already requires a non-empty `obligations`
   array, so no valid ledger could exist for such an input regardless of
   this ADR.
5. **Stable id generation.** Each generated obligation's id is
   `"<taskContractId>:<sourceField>:<index>"`, where `taskContractId` is the
   source task contract's own `id` property, `sourceField` is the literal
   string `deliverables`, `required_contract_decisions`, or
   `required_invariants`, and `index` is the zero-based position of that
   entry within that field's array as it appears in the source document.
   This id is fully determined by the source document's content and JSON
   array order; the generator introduces no random, time-based, or
   environment-dependent component. Regenerating from byte-identical input
   produces byte-identical ids. Editing, reordering, inserting, or removing
   entries in a task contract's source arrays changes downstream ids on the
   next generation; this is expected, not an error, and is exactly why the
   sidecar report's `taskContractDigest` (Decision 7) exists — to make which
   source version produced which ids independently checkable.
6. **`traceId` is bound explicitly, never inferred.** Task contracts have no
   `traceId` field and the generator never invents one. The generator
   requires `traceId` as an explicit argument at generation time, supplied
   by the caller from the trace envelope the resulting ledger is meant to
   pair with later. The generator writes that exact value, unmodified, into
   the produced ledger's `traceId`. Generation fails closed (Decision 9) if
   the argument is missing, empty, or whitespace-only; the generator never
   generates a placeholder or synthetic `traceId`.
7. **Provenance lives in a sidecar report, never inside the ledger.**
   `ObligationLedger` 1.0's schema and required-field set are unchanged by
   this ADR; zero diffs are permitted to
   `contracts/obligation-ledger.schema.json` or
   `docs/contracts/obligation-ledger.md`. Every provenance fact — which
   source field and array index produced an obligation, the exact source
   task contract file's content digest, and the exact generated ledger's
   content digest — is recorded in a new, independently versioned contract,
   `LedgerGenerationReport` 1.0
   (`contracts/ledger-generation-report.schema.json`,
   `docs/contracts/ledger-generation-report.md`), written as a second output
   file alongside the ledger. The report binds to its ledger and source by
   content digest (`taskContractDigest`, `ledgerDigest`), not by path alone,
   so a report is falsifiable against the exact bytes it describes.
8. **Coverage has exactly three closed statuses**, matching
   `docs/project-roadmap-and-status.md` section 6's Task 4.3 objective
   verbatim (`derivat automat, manual, fără suport în sursă`):
   - `derived-automatic`: the obligation was mechanically produced by
     Decision 2 from one source array entry. This is the only status the
     generator itself ever emits for an obligation it created.
   - `manual`: reserved for an obligation a human adds to a generated
     ledger/report pair by hand, after generation, that the generator did
     not derive. The generator never emits this status; it exists so a
     human edit stays inside the closed vocabulary instead of producing an
     undocumented shape or an untracked obligation.
   - `unsupported-no-source-field`: recorded once per source-field category
     (`deliverables`, `required_contract_decisions`, `required_invariants`)
     that was absent or empty in the source task contract, in the report's
     `unsupportedSourceFields` array, not attached to any obligation id.
     This is how a report explains, for a task contract that supplied only
     one of the two categories, why the other category contributed zero
     obligations, without that absence blocking generation (Decision 4
     blocks generation only when *both* categories are empty).
9. **Fail-closed diagnostics.** The generator never writes a partial or
   best-effort ledger or report. Every one of the following is a rejection
   that produces zero output files and a diagnostic identifying the failure
   without echoing untrusted task-contract text, mirroring the trace
   envelope and obligation ledger's existing `PT004`-style redaction
   precedent: the task contract path is unreadable; the task contract is
   not syntactically valid JSON; the task contract's `id` is missing, empty,
   or not a string; a qualifying source array (Decision 2) contains an
   entry that is not a non-empty string after trimming; zero candidate
   obligations exist (Decision 4); the `traceId` argument is missing or
   empty (Decision 6); two candidate obligations resolve to the same stable
   id (only possible if the same `taskContractId` is reused across distinct
   source documents, or a source field's own array index scheme is somehow
   duplicated, both of which the generator must detect rather than assume
   impossible); or the assembled ledger fails Phase A structural validation
   under the unchanged `ObligationLedgerValidator` (Decision 10). The exact
   diagnostic code allocation, one JSON Pointer target per condition, and
   message templates are fixed in `docs/contracts/ledger-generation-report.md`
   for Codex to implement without inventing a code, pointer, or exit
   behavior, following the same discipline ADR-0002 required for
   `docs/contracts/obligation-ledger.md`.
10. **Referential validation is bounded by what exists at generation
    time.** The generator always emits `signals: []`: no trace envelope is
    paired at generation time, so no event ids exist to correlate a signal
    to, and the generator must not invent one. Consequently the generator
    runs and requires success from `ObligationLedgerValidator`'s Phase A
    (ledger-only structural) rules — non-empty `obligations`, unique
    obligation ids, non-empty required strings — before writing any output,
    but never runs Phase B (cross-document referential) rules, since Phase
    B requires a paired trace envelope this task does not have. The
    resulting ledger's Phase B validation, including dangling-reference and
    abandoned-terminal checks, happens later and unchanged, exactly as
    today, whenever a real trace envelope is paired with this ledger for an
    `evaluate` invocation. This task does not attach signals, does not
    integrate with `AgentSession` or `GateOutcome`, and does not call
    `evaluate`.
11. **Proposed CLI boundary.** A new verb, `generate-ledger`, is proposed
    with exactly four positional arguments:
    `generate-ledger <task-contract-path> <trace-id> <ledger-output-path>
    <report-output-path>`, preserving the existing one-argument-per-artifact
    positional convention `validate`/`normalize`/`evaluate`/`assess`/
    `compare` already established, extended here because this verb produces
    two output files rather than one stdout document. Both output files are
    written only on full success (Decision 9); a failing run writes
    neither. Exit codes reuse the existing `0`/`1`/`2` convention
    unmodified: `0` only when both files are written; `1` for a semantic
    rejection of an otherwise-readable, well-formed task contract (zero
    candidate obligations, a duplicate stable id, or a Phase A structural
    failure in the assembled ledger); `2` for bad usage, an unreadable
    task-contract path, malformed task-contract JSON, or a missing/empty
    `traceId` argument. This verb name, argument count, argument order, and
    exit-code mapping are fixed by this ADR; Codex implements the exact
    diagnostic codes, pointers, and stdout/stderr JSON shapes in
    `docs/contracts/ledger-generation-report.md`, following the
    `evaluate`/`assess` precedent instead of inventing a new response
    convention.

## Normative boundaries

- `ObligationLedger` 1.0's schema, referential-integrity rules, signal
  total order, and abandoned-terminal rule (`docs/contracts/obligation-ledger.md`,
  ADR-0002) are unmodified by this ADR and by Task 4.3's implementation;
  zero diffs are permitted to `contracts/obligation-ledger.schema.json` or
  `docs/contracts/obligation-ledger.md`.
- The generator's only obligation sources are `deliverables`,
  `required_contract_decisions`, and `required_invariants` (Decision 2);
  `objective`, `acceptance_commands`, and every other task-contract field
  are permanently excluded (Decision 3), not merely excluded for Phase 4.3.
- Stable obligation ids are a pure, deterministic function of
  `taskContractId`, source field name, and source array index (Decision 5);
  the generator introduces no random or time-based identity.
- `traceId` is always an explicit external argument, never derived from the
  task contract or invented by the generator (Decision 6).
- `LedgerGenerationReport` versions independently of `ObligationLedger`,
  mirroring ADR-0006's existing principle that a provenance layer's version
  must be independent of the classifier it describes; a future
  `LedgerGenerationReport` minor or major version never forces an
  `ObligationLedger` version change and vice versa.
- Coverage is exactly the three closed statuses in Decision 8; the
  generator itself only ever emits `derived-automatic` on obligations and
  `unsupported-no-source-field` on the source-field list; `manual` is
  reserved for human-authored edits made outside generation.
- The generator emits `signals: []` unconditionally and runs only Phase A
  ledger-only structural validation before writing output (Decision 10); it
  never runs Phase B and never claims to.

## Contract and version strategy

`LedgerGenerationReport` starts at `schemaVersion 1.0`, using the same
major.minor accept-only-known-versions policy already documented in
`docs/contracts/trace-envelope.md` and reused by every later contract in
this repository. Evolution is additive-only: a new optional property may be
introduced in a later minor version; changing the closed `coverageStatus`
enum, the stable-id algorithm in Decision 5, the excluded-fields list in
Decision 3, or the fail-closed conditions in Decision 9 is a breaking change
requiring a new major version and Dragos's approval, exactly as ADR-0002
fixed for `ObligationLedger`. Fixtures for this contract live in a new
`fixtures/ledger-generation/**` subtree, avoiding collision with the
existing `fixtures/obligations/**`, `fixtures/session/**`, and
`fixtures/gate-outcome/**` subtrees.

## Zero-dependency decision

Task-contract JSON parsing, stable-id computation, SHA-256 digesting, and
report/ledger normalization introduce zero new third-party runtime
dependencies, reusing `System.Text.Json` and the existing shared structural-
validation helpers `ObligationLedgerValidator` and `TraceValidator` already
factor into. The generator lives in a new
`ProgressTrace.Core.Generation` namespace as the sole implementation of
Decisions 2, 4, 5, 8, and 9; it calls the existing, unmodified
`ObligationLedgerValidator` for Phase A validation rather than
reimplementing any part of it, per this repository's standing rule that the
normative evaluation and validation semantics have one implementation.

## Execution ownership

Following ADR-0005's capability-role vocabulary: the deterministic
verifier/controller executes every build, conformance, schema, and CLI
acceptance command from `tasks/phase-4-3-ledger-generator.json` against the
Codex candidate, audits that only its `allowed_paths` changed and that every
`protected_paths` entry is byte-identical to `main`, and assembles an
evidence packet outside the repository. A stateless external reviewer API,
when used, judges only that bounded evidence and returns a structured
verdict; it holds no repository, build, run, network, or write tool.
Dragos reviews the candidate diff, the evidence packet, and any reviewer
verdict, and owns the sole final integration decision via the normal PR
flow. No role edits another role's owned artifacts, and no role substitutes
for another.

## Security

Task-contract `deliverables`, `required_contract_decisions`, and
`required_invariants` entries are opaque strings, exactly like obligation
descriptions in `ObligationLedger` today: preserved verbatim as the
generated obligation's `description`, never executed, never interpreted as
commands, links, or code, per `docs/security.md`. The generator introduces
no network, database, shell, or model invocation; it is a pure, local,
deterministic transformation from one file plus one explicit `traceId`
argument to two output files. Diagnostics never echo task-contract text,
matching the `PT004` redaction precedent reused by every diagnostic
registry in this repository. `taskContractDigest` and `ledgerDigest` use
lowercase SHA-256, matching `GateOutcome.sourceDigest`'s existing
convention. All new fixtures remain synthetic, with no employer, medical,
credential, or production content, per `docs/security.md`'s fixture rule.

## Consequences

- A ledger's obligation ids change whenever its source task contract's
  qualifying arrays are reordered or edited, even if the semantic content
  is unchanged; this is mitigated by requiring `taskContractDigest` in the
  sidecar report, so any reader can confirm exactly which source version
  produced a given set of ids, but it does mean generated ids are not
  stable across task-contract edits the way hand-authored ids can be.
- A task contract that supplies only `deliverables` or only
  `required_contract_decisions`/`required_invariants` still produces a
  valid ledger, with the other category's absence visible only in the
  sidecar report's `unsupportedSourceFields`, not in the ledger itself; a
  reader of the ledger alone cannot tell that a category was skipped
  without also reading the report.
- Every generated ledger initially has zero signals and therefore
  classifies every obligation as `insufficient-evidence` if evaluated
  immediately after generation; this is expected and matches the roadmap's
  pipeline (`task contract → ledger generat → gate log → sesiune + semnale`),
  not a defect of this task.
- Two files must now be produced and kept together (ledger and report),
  introducing a new two-artifact coupling analogous to the existing
  trace/ledger `traceId`-match coupling from Phase 1, mitigated by the
  report's digest-based binding to both its ledger and its source.

## Alternatives considered

- **Adding an optional `provenance` block directly to `ObligationLedger`
  1.0**: rejected because it would be a breaking change to a frozen Phase 1
  contract under ADR-0002's version policy, and because it would couple a
  generation-time concern to a contract that must remain valid regardless
  of how or whether a ledger was generated.
- **Deriving `traceId` from the task contract's own `id`**: rejected
  because a task contract's `id` identifies the *task*, not a *trace*; a
  single task contract may be paired with more than one session/trace over
  retries, and inventing a `traceId` would silently violate the existing
  `traceId`-match invariant the first time a real trace envelope with a
  different id needed to be paired.
- **Falling back to `objective` when `deliverables` and
  `required_contract_decisions`/`required_invariants` are both absent**:
  rejected because `objective` is free narrative text; parsing it for
  obligations would reintroduce exactly the semantic-inference-from-
  unstructured-text risk ADR-0002 explicitly ruled out for evaluation, now
  at generation time instead.
- **Random or timestamp-based obligation ids**: rejected in favor of the
  deterministic Decision 5 scheme, so that regenerating from an unchanged
  source task contract is byte-reproducible, matching this repository's
  existing determinism requirements for normalization and benchmark output.
- **A single combined ledger+report document** instead of two files:
  rejected because it would either require changing the frozen
  `ObligationLedger` schema or inventing a new wrapper contract that
  duplicates `ObligationLedger`'s shape, contradicting the existing raw/
  normalized/evaluated separation principle already applied to trace and
  ledger as two coupled files.
- **Running Phase B referential validation at generation time against a
  synthetic empty trace envelope**: rejected because it would either always
  trivially pass (since `signals` is always empty, nothing dereferences
  anything) or require inventing a fake trace envelope, neither of which
  provides real assurance; Phase B validation is deferred to the real
  `evaluate` invocation where it can be meaningful.

## Non-goals

- Attaching signals, events, or an `AgentSession`/`GateOutcome` binding to a
  generated ledger; that pairing happens later, unchanged, through the
  existing `evaluate` verb and `SessionTraceBuilder`.
- Any change to `contracts/obligation-ledger.schema.json`,
  `docs/contracts/obligation-ledger.md`, or any other existing Phase 0–4.2
  contract, schema, diagnostic code, or CLI verb's behavior.
- Any semantic or natural-language interpretation of `objective`,
  `acceptance_commands`, or any task-contract field other than
  `deliverables`, `required_contract_decisions`, and `required_invariants`.
- A general document-to-obligation converter, ingestion adapter, or
  OpenTelemetry/framework-generic compatibility claim of any kind.
- Persistence, a hosted service, a UI, network calls, or automatic agent
  interruption.
- The cryptographic attestation layer proposed in ADR-0006.
- Synthetic benchmark corpus expansion or threshold tuning.
- Beginning Codex implementation before Dragos approves this ADR or
  supplies an approved replacement.

## Compatibility with frozen contracts

`contracts/trace-envelope.schema.json`, `contracts/obligation-ledger.schema.json`,
`contracts/evaluation-result.schema.json`,
`contracts/termination-declaration.schema.json`,
`contracts/stop-assessment-result.schema.json`,
`contracts/baseline-definition.schema.json`,
`contracts/baseline-comparison-result.schema.json`,
`contracts/agent-session.schema.json`, `contracts/gate-outcome.schema.json`,
and their paired `docs/contracts/*.md` documents are frozen for Task 4.3:
zero diffs are permitted. Any apparent need to change one of them is a stop
condition requiring a new Dragos-approved major version and new conformance
fixtures for that contract, not a Task 4.3 side effect. The existing CLI
exit-code contract (`0` valid/success, `1` semantic-invalid or rejected,
`2` malformed, unreadable, or usage error) is reused verbatim for
`generate-ledger`, per Decision 11.

## Related documents

- `docs/architecture/ADR-0002-obligation-and-evaluation-contracts.md`
- `docs/architecture/ADR-0005-external-review-and-clean-room-reproducibility.md`
- `docs/architecture/ADR-0006-session-provenance-and-attestation.md`
- `docs/contracts/obligation-ledger.md`
- `docs/contracts/agent-session.md`
- `docs/contracts/gate-outcome.md`
- `docs/contracts/ledger-generation-report.md`
- `docs/project-roadmap-and-status.md`
- `docs/security.md`
- `AGENTS.md`
