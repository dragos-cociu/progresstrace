# Termination declaration

Version `1.0` is the Phase 2a termination-declaration contract. It is a
third supplied input, paired with a trace envelope
(`contracts/trace-envelope.schema.json`) and an obligation ledger
(`contracts/obligation-ledger.schema.json`), consumed by `assess`. The
normative machine-readable contract is
`contracts/termination-declaration.schema.json`, implemented by Codex from
this document; the .NET Core applies the same structural rules plus the
referential-integrity and terminal-event invariants below.

A termination declaration is a ProgressTrace input artifact at the internal
interoperability boundary after capture: an adapter, harness, controller,
operator, or optionally the agent itself normalizes source-native evidence
into this contract. It is not an agent-native wire protocol and is not a
claimed industry standard, and no agent is ever required to emit it. Which
role produced a given declaration, and what evidence it rests on, is recorded
explicitly in the required `declarationSource` object (see "Declaration
source and provenance" below), so agent-produced, harness-observed,
operator-annotated, and adapter-inferred facts stay distinguishable
downstream.

## Purpose

A termination declaration is a supplied attestation of how and where a
trace's observation ended, together with a record of which role produced that
attestation and on what evidence. It never claims that ending was voluntary or
correct; it only names one specific, closed-vocabulary cause for why
observation stopped at a given event, or, for `capture-truncated` and
`unknown`, explicitly withholds that commitment. The producing role is not
required to be the agent under observation: any of an agent, a harness or
controller, a human operator, or an adapter may produce a declaration, and
the `declarationSource` object records which one did. `docs/contracts/stop-assessment-result.md`
defines the derived `terminationAttested` boolean and every quantity computed
from this declaration; this document defines only the shape, validation, and
CLI handling of the declaration itself.

## Encoding

A termination declaration is UTF-8 encoded JSON. Malformed JSON is a
distinct failure mode from a structurally or referentially invalid document
and is rejected before any semantic rule is applied. Input larger than
16 MiB (16,777,216 bytes) is rejected before JSON parsing, reusing the same
limit as the trace envelope and obligation ledger
(`TraceValidator.MaximumInputSizeBytes`). Structural objects reject unknown
members, and duplicate JSON property names are rejected throughout the
document, mirroring the trace envelope's and obligation ledger's `PT004`
precedent.

## Document structure

- `schemaVersion`: contract major/minor version. Phase 2a accepts only
  `1.0`.
- `traceId`: non-empty identity that must equal the paired trace envelope's
  `traceId`.
- `terminationEventId`: non-empty identity that must dereference an
  `events[].id` present in the paired trace envelope, and that dereferenced
  event must be the trace's own maximum-canonical-rank event (the
  terminal-required rule; see "Canonical rank and the terminal-required
  rule" below). A dangling reference and a non-terminal but existing
  reference are both validation failures, never silently accepted.
- `terminationKind`: one of the fixed closed enum `agent-self-reported-stop`,
  `harness-declared-stop`, `natural-completion`, `external-cancellation`,
  `timeout`, `crash-or-error`, `capture-truncated`, `unknown`. No other
  value, casing, or free text is accepted, and there is no default.
- `declarationSource`: a required closed object recording who produced this
  declaration and on what evidence, distinct from `terminationKind`'s record
  of the cause. Its four members, in fixed canonical nested order, are
  `producerType`, `producerName`, `producerVersion`, and `evidenceBasis`; see
  "Declaration source and provenance" below for their exact values,
  requiredness, redaction, and coherence rules. It never requires the agent
  to produce the declaration, and it is never a confidence, quality, or truth
  score.

There is no authored per-obligation field of any kind on this contract:
success semantics are fixed at `status=satisfied` for every ledger-declared
obligation by `docs/architecture/ADR-0003-phase-2-architecture-and-stop-assessment.md`,
so no target needs to be authored here.

## Field types and requiredness

The root object is a JSON object with `additionalProperties: false`;
unknown top-level members are rejected. All five root properties are
required, and none has a default, in this canonical order: `schemaVersion`
(string, exactly `"1.0"` in this version), `traceId` (string, non-empty),
`terminationEventId` (string, non-empty), `terminationKind` (string, exactly
one of the fixed closed eight-value enum above), and `declarationSource`
(closed object, described in "Declaration source and provenance" below).

`declarationSource` is a JSON object with `additionalProperties: false`; all
four of its members are required and none has a default, in this canonical
nested order: `producerType` (string, one of the fixed closed four-value enum
`agent`, `harness`, `operator`, `adapter`), `producerName` (string,
non-empty), `producerVersion` (string non-empty, or JSON `null`), and
`evidenceBasis` (string, one of the fixed closed four-value enum
`agent-output`, `harness-lifecycle`, `operator-annotation`,
`adapter-inference`).

## Canonical rank and the terminal-required rule

Canonical rank is the zero-based position of a trace event in the trace
envelope's already-normative canonical order, fixed in
`docs/contracts/trace-envelope.md`'s "Compatibility and normalization"
section: ascending by `sequence`, then by timestamp instant, then by `id`
using ordinal comparison. Canonical rank is always contiguous `0..N-1` over
the `N` events present in the paired trace envelope, regardless of gaps in
raw `sequence` values.

`terminationEventId` must dereference an event id present in the paired
trace envelope (a dangling reference fails validation), and the
dereferenced event's canonical rank must equal `N-1`, the maximum canonical
rank present in the trace (a reference to any other, existing event fails
validation). This is the terminal-required rule: a termination declaration
can only ever attest to the trace's own last event in canonical order,
never to an earlier event, regardless of that earlier event's raw
`sequence` value.

## Termination attestation

`terminationKind` fixes a lookup table, defined once, normatively, here:

| `terminationKind`          | `terminationAttested` |
|----------------------------|------------------------|
| `agent-self-reported-stop` | `true`                 |
| `harness-declared-stop`    | `true`                 |
| `natural-completion`       | `true`                 |
| `external-cancellation`    | `true`                 |
| `timeout`                  | `true`                 |
| `crash-or-error`           | `true`                 |
| `capture-truncated`        | `false`                |
| `unknown`                  | `false`                |

`terminationAttested` is derived and never authored; it is computed by
`assess` and carried on `StopAssessmentResult`, not on this contract. Both
non-attested kinds withhold the same commitment for different reasons:
`capture-truncated` means the capture itself is known to have ended before
whatever really happened to the agent; `unknown` means no cause was ever
committed to, which is not itself evidence that a real, named-cause
termination occurred. Treating `unknown` as attested would wrongly read an
authoring gap as a positive attestation; neither this document nor
`stop-assessment-result.md` ever does so.

`terminationAttested` is a cause-commitment lookup over the closed
`terminationKind` enum, not an evidence-quality or truth-confidence score. It
is independent of `declarationSource`: a declaration whose
`evidenceBasis = adapter-inference` still yields `terminationAttested = true`
for any of the six concrete, named-cause kinds, because attestation records
only that a specific cause was committed to, not who committed to it or how
strong the evidence is. Such a value remains authored and inferred; it is
never relabeled as observed agent output or as a direct agent assertion, in
this contract or in `StopAssessmentResult`.

## Declaration source and provenance

`declarationSource` records who produced this termination declaration,
separately from `terminationKind`, which records the cause. It exists because
a termination declaration is not required to be emitted by the agent under
observation: it may be produced by the agent itself, by a harness or
controller that observed the run, by a human operator annotating after the
fact, or by an adapter that normalized source-native evidence into this
contract. Recording the producer keeps agent-produced, harness-observed,
operator-annotated, and adapter-inferred facts distinguishable downstream, so
no consumer mistakes an inferred or annotated cause for a direct agent
assertion. It never requires the agent to emit this declaration, and it is
never a confidence, quality, or truth score.

`declarationSource` is a closed JSON object with `additionalProperties:
false`; all four of its members are required, in this fixed canonical nested
order:

1. `producerType` (string, required): exactly one of the closed four-value
   enum `agent`, `harness`, `operator`, `adapter`. The role that produced the
   declaration.
2. `producerName` (string, required, non-empty): identifies the producing
   role or system. It is never echoed in any diagnostic `Message` (see the
   redaction note below). For `producerType=operator`, this may be a
   non-personal role name or an opaque identifier, so a real person's identity
   need never appear in the document, supporting redaction and privacy.
3. `producerVersion` (string non-empty, or JSON `null`; required, never
   omitted): the producing software's version, or the literal `null` when no
   meaningful software version exists (for example a human operator
   annotation). `null` is a valid, distinct value; an empty or whitespace-only
   string is not.
4. `evidenceBasis` (string, required): exactly one of the closed four-value
   enum `agent-output`, `harness-lifecycle`, `operator-annotation`,
   `adapter-inference`. The kind of evidence the declaration rests on.

`producerName` is redacted from diagnostics exactly like every other
untrusted document value: no `PT303`, `PT001`, `PT002`, `PT003`, `PT004`, or
`PT101` diagnostic ever copies a `producerName`, or any other
`declarationSource` value, into its `Message`, matching the `PT004` redaction
precedent.

### Declaration-source coherence rules

Beyond the per-field structural and enum checks above, `declarationSource`
must be coherent with `terminationKind` and internally coherent across its own
members. The following closed set of five coherence rules is normative; each
is a single implication, and any violated implication is a `PT303` failure
(see the diagnostic registry):

| # | Antecedent | Required consequent |
|---|------------|---------------------|
| 1 | `terminationKind = agent-self-reported-stop` | `evidenceBasis = agent-output` |
| 2 | `terminationKind = harness-declared-stop` | `evidenceBasis = harness-lifecycle` |
| 3 | `producerType = agent` | `evidenceBasis = agent-output` |
| 4 | `producerType = operator` | `evidenceBasis = operator-annotation` |
| 5 | `evidenceBasis = adapter-inference` | `producerType = adapter` |

Any combination not forbidden by one of these five rules is accepted. In
particular, a harness or an adapter may faithfully wrap evidence whose origin
differs from its own role: a `harness` or `adapter` producer may carry
`evidenceBasis = agent-output` for an `agent-self-reported-stop` it observed
or normalized, because rules 1 and 2 constrain only `evidenceBasis`, and rules
3-5 do not constrain a `harness` producer or a non-`adapter-inference`
`adapter` producer. This is the intended way to record that a non-agent role
captured or normalized agent-origin evidence without misrepresenting who
produced the declaration.

`PT303` pointer targeting: when a single field alone establishes the
violation, `PT303`'s pointer targets that field; otherwise it targets
`/declarationSource`. Rules 1 and 2 are anchored by the authoritative
root-level `terminationKind`, so the single offending `declarationSource`
member is `evidenceBasis`, and `PT303`'s pointer is
`/declarationSource/evidenceBasis`. Rules 3, 4, and 5 each couple two
`declarationSource` members (`producerType` and `evidenceBasis`) jointly, with
neither member individually authoritative over the other, so `PT303`'s pointer
is `/declarationSource`. `PT303`'s `Message` is one fixed, safe template that
never echoes any document value.

`declarationSource` is authored provenance metadata, never observed fact.
`evidenceBasis = adapter-inference` paired with a concrete, attested
`terminationKind` (any of the six attested kinds) records that an adapter
inferred that named cause from source-native evidence; the cause stays
authored and inferred and is never relabeled as observed agent output or a
direct agent assertion. See "Termination attestation" above for why
`terminationAttested` is a cause-commitment lookup, not an evidence-quality or
truth-confidence score.

## Referential integrity

- `traceId` must equal the paired trace envelope's `traceId`; a mismatch
  fails validation (exit 1).
- `terminationEventId` must dereference an existing event id in the paired
  trace envelope; a dangling reference fails validation (exit 1).
- The event dereferenced by `terminationEventId` must be the trace's own
  maximum-canonical-rank event; a reference to any other existing event
  fails validation (exit 1).

`TerminationDeclaration`'s own referential rules depend only on the paired
trace envelope; they do not reference the obligation ledger. The `assess`
CLI additionally requires the ledger to validate, in full, before the
declaration is ever opened, per "CLI assess behavior" below and per
`docs/architecture/ADR-0003-phase-2-architecture-and-stop-assessment.md`'s
trace-then-ledger-then-declaration precedence; this is a CLI-ordering
requirement, not a referential rule of this contract.

## Diagnostic registry

Every declaration and CLI-`assess` validation failure reports one or more
`Diagnostic{Code, Pointer, Message}` values
(`src/ProgressTrace.Core/Diagnostics/Diagnostic.cs`). In every `Message`
below, a `{name}` placeholder is the static JSON property name fixed by
this schema (for example `traceId`, `terminationEventId`,
`terminationKind`), never a value read from the document; no diagnostic in
this registry echoes an untrusted document value in its `Message`.

Phase 2a termination-declaration validation reuses the following codes
already defined for the trace envelope and obligation ledger, unmodified in
meaning:

- `PT000` malformed JSON: the declaration document is not syntactically
  valid JSON. Pointer `""`. Message: `"Input is not valid JSON."`
  Short-circuits; no other declaration diagnostic accompanies it.
- `PT001` required property missing. Pointer: the missing property's own
  pointer (for example `/traceId`, `/terminationEventId`,
  `/terminationKind`). Message: `"Required property {name} is missing."`
- `PT002` wrong JSON type for a value that is present. Pointer: that
  value's own pointer, or `""` if the declaration root itself is not a
  JSON object. Message: `"{name} must be {expected type}."`, or, for a
  non-object root, `"TerminationDeclaration must be an object."`
- `PT003` unknown property rejected by a closed object schema. Pointer:
  the unknown property's own pointer. Message: `"Property is not
  allowed."`
- `PT004` duplicate JSON object property name, checked throughout the
  document. Pointer: the duplicate occurrence's own pointer. Message:
  `"JSON object property names must be unique."`
- `PT005` input exceeds 16 MiB (16,777,216 bytes,
  `TraceValidator.MaximumInputSizeBytes`), checked before JSON parsing.
  Pointer `""`. Message: `"Input exceeds the maximum size of
  {MaximumInputSizeBytes} bytes."` Short-circuits; no parsing is attempted
  and no other declaration diagnostic accompanies it.
- `PT100` unsupported `schemaVersion` value (present, a string, but not
  exactly `"1.0"`). Pointer `/schemaVersion`. Message: `"Schema version is
  not supported."`
- `PT101` invalid value for a field of the correct type: an empty or
  whitespace-only required string (`traceId`, `terminationEventId`,
  `declarationSource.producerName`, or a `declarationSource.producerVersion`
  that is present as a string), a `terminationKind` string that is not
  exactly one of the fixed closed eight-value enum, a
  `declarationSource.producerType` string that is not exactly one of the
  fixed closed four-value enum, or a `declarationSource.evidenceBasis` string
  that is not exactly one of the fixed closed four-value enum. Pointer: the
  invalid value's own pointer (for example `/declarationSource/producerName`,
  `/declarationSource/producerType`, `/declarationSource/evidenceBasis`).
  Message: `"{name} must not be empty."`, `"Termination kind is not
  recognized."`, `"Producer type is not recognized."`, or `"Evidence basis is
  not recognized."`, as applicable. A `null` `producerVersion` is valid and is
  never a `PT101`; only a present-but-empty `producerVersion` string is.

Phase 2a Task A allocates the following four new, non-colliding codes for
termination-declaration-specific semantic failures that have no trace-
envelope or obligation-ledger precedent: `PT300`, `PT301`, and `PT302` for
referential and terminal-event failures, and `PT303` for
declaration-source coherence failures. All four are allocated by this
document for Task A now. The remaining `PT3xx` codes are not carried forward
as Task B's block: Task B's future `BaselineDefinition`-specific failures are
reserved to a fresh, as-yet-unallocated `PT4xx` block, to be allocated by a
future `ADR-0004`, so that Task A's diagnostic surface (`PT300`-`PT303`) and
Task B's remain cleanly separated. No `PT3xx` code beyond `PT303` is
allocated, reserved for, or claimed by Task B.

- `PT300` dangling `terminationEventId`: the value does not dereference any
  event id present in the paired trace envelope. Pointer:
  `/terminationEventId`. Message: `"TerminationDeclaration
  terminationEventId does not reference an event in the paired trace
  envelope."`
- `PT301` non-terminal `terminationEventId`: the value dereferences an
  event id present in the paired trace envelope, but that event is not the
  trace's maximum-canonical-rank event. Pointer: `/terminationEventId`.
  Message: `"TerminationDeclaration terminationEventId must reference the
  trace's terminal event."` Never evaluated when `PT300` already fired for
  the same field, since a dangling reference has no dereferenced event to
  test for terminality.
- `PT302` `traceId` mismatch: the declaration's `traceId` does not equal
  the paired trace envelope's `traceId`. Pointer: `/traceId`. Message:
  `"TerminationDeclaration traceId must equal the paired trace envelope's
  traceId."`
- `PT303` declaration-source coherence violation: one of the five
  declaration-source coherence rules in "Declaration source and provenance"
  above is violated. Pointer: `/declarationSource/evidenceBasis` when a
  single field alone establishes the violation (coherence rules 1 and 2,
  anchored by the authoritative root-level `terminationKind`); otherwise
  `/declarationSource` (coherence rules 3, 4, and 5, which each jointly couple
  `producerType` and `evidenceBasis`). Message, one fixed safe template for
  every case that never echoes any document value:
  `"TerminationDeclaration declarationSource is not coherent with the
  declared termination."` Evaluated only after `producerType`,
  `evidenceBasis`, and `terminationKind` have each validated as a recognized
  enum value; a declaration violating more than one coherence rule emits one
  `PT303` per violated rule, in ascending rule number, at each rule's fixed
  pointer.

No code is reused across meanings, and no two codes above share a
pointer-target rule for the same failure; `PT303`'s two pointer targets
(`/declarationSource/evidenceBasis` and `/declarationSource`) are selected by
the fixed single-offending-field rule above, never ambiguously.

### Contract status

This diagnostic registry, meaning every code's identity, its exact
`Pointer` target rule, and which validation condition produces it, is part
of the public Phase 2a `1.0` contract for both
`contracts/termination-declaration.schema.json`'s implementation and the
`assess` CLI's stdout/stderr shape. Conformance tests assert the exact
`Code`, and `Pointer` where material, for every invalid fixture. Changing a
code's meaning, removing a code, changing a pointer-target rule, or
changing which validation condition a code reports is a breaking change
under the version policy below: it requires a new major version, Dragos's
approval, and new conformance fixtures, and is never introduced as a silent
change to `1.0`. `Message` text is a fixed, safe template documented here
for implementation consistency; conformance tests do not assert its exact
bytes, unlike `Code` and `Pointer`.

## Validation order

Termination-declaration validation runs in two phases, always in this
order, using `System.Text.Json`'s order-preserving `JsonElement`
enumeration for every JSON object and array traversal below; no step ever
depends on `Dictionary`/`HashSet`/`HashCode` iteration order, only on
document order for producing diagnostics and on set membership for
referential checks.

**Phase A, declaration-only structural checks** (do not require the paired
trace envelope):

1. Size check (`PT005`). Short-circuits: if the input exceeds the limit,
   this is the only diagnostic and JSON parsing is never attempted.
2. JSON parse (`PT000`). Short-circuits: if parsing fails, this is the
   only diagnostic and no further check runs.
3. Duplicate-property whole-document walk (`PT004`), over the entire
   parsed tree in pre-order (an object's properties in document
   declaration order, then recursing into each property's value). This
   runs unconditionally before any field-specific check and never
   short-circuits any later step.
4. Root type check: if the root is not a JSON object, add `PT002` at
   pointer `""` and stop; no further declaration check runs, though step
   3's diagnostics, if any, are preserved.
5. Root unknown-property check (`PT003`), in document property
   declaration order.
6. Root required-property presence and type checks (`PT001`/`PT002`),
   evaluated in this fixed order regardless of document order:
   `schemaVersion`, `traceId`, `terminationEventId`, `terminationKind`,
   `declarationSource`. `declarationSource`'s type check adds `PT002` if it
   is present but not a JSON object.
7. `schemaVersion` value check (`PT100`), only if step 6 read it as a
   present string.
8. `traceId` non-empty check (`PT101`), only if step 6 read it as a
   present string.
9. `terminationEventId` non-empty check (`PT101`), only if step 6 read it
   as a present string.
10. `terminationKind` closed-enum check (`PT101`), only if step 6 read it
    as a present string.
11. `declarationSource` nested structural checks, only if step 6 read
    `declarationSource` as a present JSON object:
    a. Nested unknown-property check (`PT003`), in document property
       declaration order.
    b. Nested required-property presence and type checks (`PT001`/`PT002`),
       evaluated in this fixed order regardless of document order:
       `producerType`, `producerName`, `producerVersion`, `evidenceBasis`.
       `producerVersion`'s type check accepts a JSON string or the JSON
       `null` literal; any other JSON type adds `PT002`.
12. `declarationSource` nested value checks, each only if step 11b read the
    corresponding member as a present value of the correct type, evaluated in
    this fixed order: `producerType` closed-enum check (`PT101`),
    `producerName` non-empty check (`PT101`), `producerVersion` non-empty
    check (`PT101`, only when `producerVersion` is present as a string; a
    `null` `producerVersion` is skipped), `evidenceBasis` closed-enum check
    (`PT101`).
13. `declarationSource` coherence checks (`PT303`), evaluated once, only if
    steps 10, 12 read `terminationKind`, `producerType`, and `evidenceBasis`
    all as recognized enum values. Test the five coherence rules from
    "Declaration source and provenance" in ascending rule number; for each
    violated rule append one `PT303` at that rule's fixed pointer
    (`/declarationSource/evidenceBasis` for rules 1-2, `/declarationSource`
    for rules 3-5). All `PT303` diagnostics are Phase A diagnostics and
    therefore always precede any Phase B diagnostic.

**Phase B, cross-document referential checks** (require the paired trace
envelope; per "CLI assess behavior" below, Phase B runs only when the trace
envelope itself has already validated with zero diagnostics; the `assess`
CLI additionally requires the ledger to have already validated with zero
diagnostics before Phase B ever runs, per that section's precedence, even
though Phase B's own checks do not read the ledger):

1. `traceId` mismatch check (`PT302`), evaluated once, only if step A8
   read a non-empty `traceId`.
2. Dangling `terminationEventId` check (`PT300`), evaluated once, only if
   step A9 read a non-empty `terminationEventId`, against the set of event
   ids present in the paired trace envelope.
3. Terminal-required check (`PT301`), evaluated once, only if step B2 did
   not fire `PT300` (i.e. `terminationEventId` dereferenced an existing
   event): compare that event's canonical rank to the maximum canonical
   rank present in the trace; a mismatch fires `PT301`.

Every diagnostic produced by Phase A and Phase B is appended to one result
list in exactly the step order above: all of Phase A before any of Phase B,
and within each phase in the numbered sub-step order. No later step ever
removes, reorders, or suppresses a diagnostic added by an earlier step; the
only short-circuits are Phase A steps 1, 2, and 4, and Phase B step 3's
dependency on step 2 not having fired. A declaration is valid only when
both Phase A and Phase B produce zero diagnostics.

## CLI assess behavior

Usage: `assess <trace-path> <ledger-path> <termination-declaration-path>`,
exactly three positional arguments, extending the existing usage line
without changing it: `progresstrace <validate|normalize> <path> |
progresstrace evaluate <trace-path> <ledger-path> | progresstrace assess
<trace-path> <ledger-path> <termination-declaration-path>`. Bad usage,
meaning an argument count and leading verb combination that does not match
`validate`/`normalize` (2 arguments), `evaluate` (3 arguments), or `assess`
(4 arguments), writes `{"error":"usage","message":"Usage: progresstrace
<validate|normalize> <path> | progresstrace evaluate <trace-path>
<ledger-path> | progresstrace assess <trace-path> <ledger-path>
<termination-declaration-path>"}` to stderr and exits `2`; no file is
opened.

Exact order of operations for a well-formed `assess` invocation, extending
`evaluate`'s trace-then-ledger precedent by one further stage,
trace-then-ledger-then-declaration; the `document` value used in every
diagnostic and error shape for the third input is exactly the string
`"declaration"`:

1. Size-check `trace-path`. If it exceeds
   `TraceValidator.MaximumInputSizeBytes`, write
   `{"valid":false,"document":"trace","diagnostics":[<PT005>]}` to stdout
   and exit `2`. Neither `ledger-path` nor `termination-declaration-path`
   is ever opened.
2. If `trace-path` cannot be read (`IOException`/
   `UnauthorizedAccessException`), write
   `{"error":"input","document":"trace","message":"Input file could not
   be read."}` to stderr and exit `2`. Neither other path is opened.
3. Parse and validate `trace-path` via `TraceValidator.ParseAndValidate`.
   If it returns one or more diagnostics, write
   `{"valid":false,"document":"trace","diagnostics":[...]}` to stdout;
   exit `2` if any returned diagnostic's code is `PT000`, otherwise exit
   `1`. Neither other path is opened, size-checked, read, or parsed: the
   trace is checked, in full, to completion, before either later document
   is touched at all.
4. Only when `trace-path` validates with zero diagnostics, size-check
   `ledger-path`; on excess size, write
   `{"valid":false,"document":"ledger","diagnostics":[<PT005>]}` to
   stdout and exit `2`. `termination-declaration-path` is not opened.
5. If `ledger-path` cannot be read, write
   `{"error":"input","document":"ledger","message":"Input file could not
   be read."}` to stderr and exit `2`.
6. Run ledger Phase A then Phase B, exactly as defined in
   `docs/contracts/obligation-ledger.md`'s "Validation order", using the
   already-validated trace envelope from step 3 for Phase B. If Phase A's
   JSON parse fails, write
   `{"valid":false,"document":"ledger","diagnostics":[<PT000>]}` to
   stdout and exit `2`. If Phase A or Phase B produces one or more other
   diagnostics, write
   `{"valid":false,"document":"ledger","diagnostics":[...]}` to stdout
   and exit `1`. `termination-declaration-path` is not opened in either
   case.
7. Only when `ledger-path` validates with zero diagnostics across both
   phases, size-check `termination-declaration-path`; on excess size,
   write `{"valid":false,"document":"declaration","diagnostics":[<PT005>]}`
   to stdout and exit `2`.
8. If `termination-declaration-path` cannot be read, write
   `{"error":"input","document":"declaration","message":"Input file could
   not be read."}` to stderr and exit `2`.
9. Run declaration Phase A then Phase B, as defined in "Validation order"
   above, using the already-validated trace envelope from step 3 for Phase
   B. If Phase A's JSON parse fails, write
   `{"valid":false,"document":"declaration","diagnostics":[<PT000>]}` to
   stdout and exit `2`. If Phase A or Phase B produces one or more other
   diagnostics, write
   `{"valid":false,"document":"declaration","diagnostics":[...]}` to
   stdout and exit `1`.
10. Only when the trace, the ledger, and the declaration all validate with
    zero diagnostics, run the deterministic stop-assessment algorithm fixed
    in `docs/contracts/stop-assessment-result.md` over the validated trace,
    ledger, and declaration to produce a `StopAssessmentResult`, and write
    it to stdout using the exact canonical serialization fixed in that
    document (fixed property order, UTF-8, no byte-order mark, no
    insignificant whitespace, exactly one trailing newline byte), with no
    `{"valid":true,...}` wrapper, mirroring `evaluate`'s existing precedent
    of writing the raw canonical artifact directly to stdout on success.
    Exit `0`.

`StopAssessmentResult` is written to stdout if and only if step 10 is
reached: the trace envelope, the obligation ledger, and the termination
declaration, including every cross-document referential and terminal-
required check, all validate with zero diagnostics. There is no partial,
best-effort, or diagnostics-plus-result output; a failing trace, ledger, or
declaration never produces a `StopAssessmentResult`, and a successful
`StopAssessmentResult` never carries diagnostics.

Exit codes, consistent with the existing `0`/`1`/`2` convention: `0` only
when step 10 emits a `StopAssessmentResult`; `1` when any one of the three
documents produces a structural, type, referential, terminal-required,
enum-membership, or declaration-source coherence diagnostic (`PT001`-`PT004`,
`PT100`-`PT101`, `PT200`-`PT204`, `PT300`-`PT303`) but none of the three is
malformed, oversized, or unreadable; `2` for bad usage, an unreadable file, an
oversized file (`PT005`), or malformed JSON (`PT000`), on any of the three
documents.

`assess` introduces no network, external process, database, or filesystem
access beyond reading the three given file paths. The two diagnostic-
reporting shapes above (`{"valid":...,"document":...,"diagnostics":[...]}`
and `{"error":...,"document":...,"message":...}`) plus the raw
`StopAssessmentResult` document are the complete, stable set of `assess`'s
stdout contents on their respective paths, and the `{"error":"usage",...}`
and `{"error":"input",...}` shapes are the complete, stable set of its
stderr contents.

## Canonical serialization

The `assess` CLI never emits, echoes, or normalizes a
`TerminationDeclaration` document; it is a pure authored input, consumed
only by `assess`, and there is no verb or flag that writes a canonicalized
`TerminationDeclaration` to stdout. No canonical-serialization rule is
therefore defined for this contract. `StopAssessmentResult` copies
`terminationEventId`, `terminationKind`, and the entire `declarationSource`
object by value into its own canonically serialized output, preserving
`declarationSource`'s nested property order, defined in
`docs/contracts/stop-assessment-result.md`; that document's canonical
serialization is the sole canonical artifact Task A ever emits that carries
termination or producer-provenance information.

## Version policy

A reader accepts only the schema versions it explicitly supports. This
contract's Phase 2a implementation accepts exactly `schemaVersion` `1.0`
and rejects a termination declaration declaring any other value. Contract
evolution distinguishes two kinds of change:

- **Additive compatible evolution**: a new optional property or another
  purely additive extension that does not alter any existing required
  field, the closed `terminationKind` enum, or the terminal-required rule
  may be introduced in a later minor version, consumable by newer readers
  without breaking readers pinned to `1.0`.
- **Breaking change**: adding or changing a value in the closed
  `terminationKind` enum, the terminal-required rule, or any required
  field is a breaking change. It requires a new major version and Dragos's
  approval, new conformance fixtures, and is never introduced as a
  minor-version patch or a silent change to `1.0`.

## Synthetic examples

The following examples are illustrative of document shape only; they are
not an exhaustive statement of validation coverage, and every normative
rule is fixed in the sections above. All example data is synthetic and
contains no employer, medical, credential, or production content. Both
examples correlate to a hypothetical trace envelope whose maximum-
canonical-rank event is `evt-2`.

An attested, natural-completion termination declaration produced by a
harness that observed the run's lifecycle:

```json
{
  "schemaVersion": "1.0",
  "traceId": "trace-example-1",
  "terminationEventId": "evt-2",
  "terminationKind": "natural-completion",
  "declarationSource": {
    "producerType": "harness",
    "producerName": "example-harness",
    "producerVersion": "2.3.0",
    "evidenceBasis": "harness-lifecycle"
  }
}
```

A non-attested, capture-truncated termination declaration, still
terminal-required against the same trace, produced by an adapter that
inferred the truncation while normalizing source-native evidence:

```json
{
  "schemaVersion": "1.0",
  "traceId": "trace-example-1",
  "terminationEventId": "evt-2",
  "terminationKind": "capture-truncated",
  "declarationSource": {
    "producerType": "adapter",
    "producerName": "example-otel-adapter",
    "producerVersion": "0.9.1",
    "evidenceBasis": "adapter-inference"
  }
}
```

An `adapter-inference` producer with a concrete, attested cause: the
`crash-or-error` cause remains authored and inferred, never observed agent
output, and `producerVersion` is `null` here to show that a valid producer
may have no meaningful software version:

```json
{
  "schemaVersion": "1.0",
  "traceId": "trace-example-1",
  "terminationEventId": "evt-2",
  "terminationKind": "crash-or-error",
  "declarationSource": {
    "producerType": "adapter",
    "producerName": "example-crash-adapter",
    "producerVersion": null,
    "evidenceBasis": "adapter-inference"
  }
}
```
