# Baseline definition

Version `1.0` is the Phase 2b `BaselineDefinition` contract. It is a fourth
supplied input, paired with a trace envelope
(`contracts/trace-envelope.schema.json`), an obligation ledger
(`contracts/obligation-ledger.schema.json`), and a termination declaration
(`contracts/termination-declaration.schema.json`), consumed by `compare`. The
normative machine-readable contract is
`contracts/baseline-definition.schema.json`, to be implemented by Codex from
this document once a task contract authorizes Task B implementation; this
document fixes the contract now and authorizes no implementation. The .NET
Core applies the same structural rules already established for the trace
envelope, obligation ledger, and termination declaration, plus the
referential-integrity, coverage, and coherence invariants below.

A baseline definition is an authored counterfactual budget: an externally
supplied, per-obligation count of canonical event slots that some author
believes would have sufficed, had the trace continued, to reach
`status=satisfied` for that obligation. It is never an observed fact, never a
measurement of the stopped run's own actual continuation, and never proof of
what would have happened. `docs/architecture/ADR-0004-baseline-comparison-and-authored-estimate.md`
fixes this evidence model, its unit, and its interpretation; this document
defines only the shape, validation, and CLI handling of the authored
artifact itself.

## Purpose

A baseline definition authors, for every obligation declared in a paired
obligation ledger, a single `eventBudget`: an authored total count of
canonical event slots, not a canonical rank and not a raw `events[].sequence`
value. It exists so `compare` can classify, per obligation and for the trace
overall, whether an unmet obligation stopped with authored budget left
unused, exhausted its authored budget exactly, or exceeded it, always
labeled as an authored estimate and never as an observed fact or as proof of
the stopped run's own counterfactual future. It never authors a target
status, a success criterion, or a continuation policy: the only success
target for every obligation remains `status=satisfied`, fixed at the
contract level since Phase 2a and unchanged by this document.

Which role produced a given baseline definition, and on what evidence, is
recorded explicitly in the required `baselineSource` object (see "Baseline
source and provenance" below), mirroring `TerminationDeclaration`'s
`declarationSource` separation of producer role from evidence basis. No
agent is ever required to produce a baseline definition; an agent-produced
budget is allowed but remains an explicitly self-authored estimate with no
privileged truth status over a harness-, operator-, or adapter-produced one.

## Encoding

A baseline definition is UTF-8 encoded JSON. Malformed JSON is a distinct
failure mode from a structurally, referentially, or coverage-invalid
document and is rejected before any semantic rule is applied. Input larger
than 16 MiB (16,777,216 bytes) is rejected before JSON parsing, reusing the
same limit as the trace envelope, obligation ledger, and termination
declaration (`TraceValidator.MaximumInputSizeBytes`). Structural objects
reject unknown members, and duplicate JSON property names are rejected
throughout the document, mirroring the existing `PT004` precedent.

## Document structure

- `schemaVersion`: contract major/minor version. Phase 2b accepts only
  `1.0`.
- `traceId`: non-empty identity that must equal the paired trace envelope's
  `traceId`. `BaselineDefinition` `1.0` binds to exactly one trace; a
  reusable cross-trace template is not part of this version (see
  "Non-goals" in `docs/architecture/ADR-0004-baseline-comparison-and-authored-estimate.md`).
- `baselineSource`: a required closed object recording who authored this
  baseline definition and on what evidence, distinct from the per-obligation
  budgets themselves. Its four members, in fixed canonical nested order, are
  `producerType`, `producerName`, `producerVersion`, and `evidenceBasis`; see
  "Baseline source and provenance" below.
- `obligationBudgets`: array of per-obligation authored budgets. Exactly one
  entry must exist for every obligation declared in the paired ledger,
  neither fewer nor more; see "Referential integrity and coverage" below.
- `obligationBudgets[].obligationId`: must dereference an `obligations[].id`
  declared in the paired obligation ledger. Unique within `obligationBudgets`.
- `obligationBudgets[].eventBudget`: an authored total count of canonical
  event slots, an integer in the closed range `1..2147483647`. It is not a
  canonical rank and not a raw `events[].sequence` value; see
  `docs/contracts/baseline-comparison-result.md`'s "Unit and formula" section
  for how it is compared against observed trace length.

There is no authored target-status field of any kind on this contract:
success semantics remain fixed at `status=satisfied` for every
ledger-declared obligation, exactly as fixed since Phase 2a; no field here
overrides or extends that fixed target.

## Field types and requiredness

The root object is a JSON object with `additionalProperties: false`;
unknown top-level members are rejected. All four root properties are
required, and none has a default, in this canonical order: `schemaVersion`
(string, exactly `"1.0"` in this version), `traceId` (string, non-empty),
`baselineSource` (closed object, described below), and `obligationBudgets`
(array; may be structurally empty, though an empty array always fails
Phase B coverage whenever the paired ledger declares at least one
obligation, which it always does — see "Referential integrity and coverage"
below for why no separate non-empty-array check is defined here, unlike
`ObligationLedger.obligations`).

`baselineSource` is a JSON object with `additionalProperties: false`; all
four of its members are required and none has a default, in this canonical
nested order: `producerType` (string, one of the fixed closed four-value
enum `agent`, `harness`, `operator`, `adapter`), `producerName` (string,
non-empty), `producerVersion` (string non-empty, or JSON `null`), and
`evidenceBasis` (string, one of the fixed closed five-value enum
`agent-estimate`, `harness-policy`, `operator-estimate`,
`historical-analysis`, `adapter-inference`).

Each entry of `obligationBudgets` is a JSON object with
`additionalProperties: false`; both properties are required: `obligationId`
(string, non-empty, must dereference an `obligations[].id` in the paired
ledger, unique within `obligationBudgets`) and `eventBudget` (JSON integer —
a JSON number with zero fractional component; any other JSON type, or a JSON
number with a nonzero fractional component, is a type failure, not a range
failure — in the closed range `1..2147483647` inclusive).

## Referential integrity and coverage

- `traceId` must equal the paired trace envelope's `traceId`; a mismatch
  fails validation (exit 1).
- Every `obligationBudgets[].obligationId` must dereference an obligation id
  declared in the paired ledger's `obligations` array; a dangling reference
  fails validation (exit 1).
- `obligationBudgets[].obligationId` values must be unique within
  `obligationBudgets`; a duplicate fails validation (exit 1).
- Every obligation id declared in the paired ledger's `obligations` array
  must have exactly one corresponding `obligationBudgets` entry; an
  obligation with zero corresponding entries fails validation (exit 1).

Together these four rules enforce "exactly one budget entry per ledger
obligation." Four failure shapes are possible against that requirement —
missing, duplicate, dangling, and extra — and all four fail closed, per
`docs/architecture/ADR-0004-baseline-comparison-and-authored-estimate.md`'s
`B3` decision. "Missing" and "duplicate" are directly named validation
conditions below (`PT403` and `PT400` respectively). "Dangling" (an entry
whose `obligationId` does not correspond to any declared ledger obligation)
is `PT402`. "Extra" is not a fifth, independent structural condition: any
`obligationBudgets` entry beyond the required one-per-obligation count is,
by construction, either a duplicate of an already-covered obligation id
(caught by `PT400`) or a reference to an obligation id the ledger never
declared (caught by `PT402`); there is no third shape an "extra" entry can
take, so no separate code is allocated for it. An empty `obligationBudgets`
array is structurally valid at Phase A — unlike `ObligationLedger.obligations`,
which fails Phase A directly via `PT101` for being empty — because
`obligationBudgets`'s required cardinality is defined relationally against
the paired ledger, not in isolation; an empty array against a non-empty
ledger (the ledger's `obligations` array is always non-empty, since an empty
one is already rejected before the ledger itself validates) always produces
one `PT403` per ledger-declared obligation at Phase B.

`BaselineDefinition`'s own referential rules depend on the paired trace
envelope (for `traceId`) and the paired obligation ledger (for
`obligationBudgets` coverage); they do not reference the termination
declaration. The `compare` CLI additionally requires the ledger and the
declaration to each validate, in full, before the baseline definition is
ever opened, per "CLI compare behavior" below and per
`docs/architecture/ADR-0004-baseline-comparison-and-authored-estimate.md`'s
trace-then-ledger-then-declaration-then-baseline precedence; this is a
CLI-ordering requirement, not a referential rule of this contract, and it
does not change the fact that this contract's own Phase B checks read only
the trace and the ledger, never the declaration.

## Baseline source and provenance

`baselineSource` records who authored this baseline definition, separately
from the per-obligation budgets themselves, mirroring
`TerminationDeclaration.declarationSource`'s producer/evidence-basis
separation (`docs/contracts/termination-declaration.md`'s "Declaration
source and provenance" section). It exists because a baseline definition is
not required to be authored by the agent under observation: it may be
authored by the agent itself (an explicitly self-estimated budget with no
privileged truth status), by a harness or controller applying a fixed
policy, by a human operator, or by an adapter inferring a budget from
historical or source-native evidence. Recording the producer keeps
agent-estimated, harness-policy, operator-estimated, and adapter-inferred
budgets distinguishable downstream, so no consumer mistakes an authored
counterfactual guess for an observed fact.

`baselineSource` is a closed JSON object with `additionalProperties: false`;
all four of its members are required, in this fixed canonical nested order:

1. `producerType` (string, required): exactly one of the closed four-value
   enum `agent`, `harness`, `operator`, `adapter`. The role that authored the
   baseline definition.
2. `producerName` (string, required, non-empty): identifies the authoring
   role or system. It is never echoed in any diagnostic `Message` (see the
   redaction note below).
3. `producerVersion` (string non-empty, or JSON `null`; required, never
   omitted): the authoring software's version, or the literal `null` when no
   meaningful software version exists (for example a human operator
   annotation). `null` is a valid, distinct value; an empty or
   whitespace-only string is not.
4. `evidenceBasis` (string, required): exactly one of the closed five-value
   enum `agent-estimate`, `harness-policy`, `operator-estimate`,
   `historical-analysis`, `adapter-inference`. The kind of evidence the
   authored budget rests on.

`producerName` is redacted from diagnostics exactly like every other
untrusted document value: no `PT404`, `PT001`, `PT002`, `PT003`, `PT004`, or
`PT101` diagnostic ever copies a `producerName`, or any other
`baselineSource` value, into its `Message`, matching the `PT004` redaction
precedent already used for `TerminationDeclaration.declarationSource`.

### Baseline-source coherence rules

Beyond the per-field structural and enum checks above, `baselineSource` must
be internally coherent between `producerType` and `evidenceBasis`. Five
closed rules are normative; any violated rule is a `PT404` failure (see the
diagnostic registry). These five rules are not a direct restatement of
`TerminationDeclaration`'s five `declarationSource` coherence rules: the
`evidenceBasis` vocabulary differs (five values here, versus four there,
because `historical-analysis` has no analog on `declarationSource`), and one
rule below resolves an apparent tension between two of the plain-language
decision bullets this document transcribes, documented explicitly in
"Resolving the `operator`/`historical-analysis` overlap" immediately after
the table.

| # | Antecedent | Required consequent |
|---|------------|---------------------|
| 1 | `producerType = agent` | `evidenceBasis = agent-estimate` |
| 2 | `producerType = operator` | `evidenceBasis` in `{operator-estimate, historical-analysis}` |
| 3 | `evidenceBasis = harness-policy` | `producerType = harness` |
| 4 | `evidenceBasis = adapter-inference` | `producerType = adapter` |
| 5 | `evidenceBasis = historical-analysis` | `producerType` in `{harness, operator, adapter}` |

Rules 3, 4, and 5 are anchored on `evidenceBasis`, requiring a specific
`producerType`; rules 1 and 2 are anchored on `producerType`, requiring a
specific `evidenceBasis` (or, for rule 2, one of two). Because every
combination is checked against all five rules, the effect for each producer
role, stated exhaustively, is:

- `producerType = agent`: `evidenceBasis` must be `agent-estimate`. No other
  value is reachable, because rule 1 forbids every alternative and no other
  rule grants `agent` an exception. An agent never claims
  `historical-analysis`, `harness-policy`, or `adapter-inference`: those
  represent evidence an agent has no standing to assert about itself.
- `producerType = operator`: `evidenceBasis` must be `operator-estimate` or
  `historical-analysis`. Rule 2 grants the explicit `historical-analysis`
  exception; rules 3 and 4 independently forbid `harness-policy` and
  `adapter-inference` for any non-`harness`/non-`adapter` producer,
  including `operator`.
- `producerType = harness`: `evidenceBasis` may be `harness-policy` (its own
  label), `historical-analysis` (rule 5 includes `harness`), `agent-estimate`,
  or `operator-estimate` — the last two record a harness wrapping an
  evidence-basis category that originated with the agent or the operator,
  without relabeling it as the harness's own policy or as observed fact.
  Only `adapter-inference` is unreachable for a `harness` producer, since
  rule 4 reserves it exclusively to `producerType = adapter`.
- `producerType = adapter`: symmetrically, `evidenceBasis` may be
  `adapter-inference` (its own label), `historical-analysis` (rule 5
  includes `adapter`), `agent-estimate`, or `operator-estimate` (wrapping).
  Only `harness-policy` is unreachable for an `adapter` producer, since rule
  3 reserves it exclusively to `producerType = harness`.

A `harness` or `adapter` producer wrapping an `agent-estimate` or
`operator-estimate` evidence basis preserves only that evidence-basis
category, not the original estimator's identity: `producerName` and
`producerVersion` in that case name the wrapping harness or adapter, not
the agent or operator whose estimate it is wrapping, and no field on this
contract identifies that original estimator. This is not a full provenance
chain back to whoever originally produced the estimate; a multi-hop
provenance chain of that kind is deferred and is not represented by this
version of `baselineSource` or by any other contract in this repository.

#### Resolving the `operator`/`historical-analysis` overlap

The plain-language decision this table transcribes states both "producerType
`operator` requires `operator-estimate`" and "`historical-analysis` may be
produced by harness, operator, or adapter." Read as two independent,
exceptionless forward rules, these conflict: an unqualified
"`operator` requires `operator-estimate`" would forbid the
`operator`+`historical-analysis` pairing the second bullet explicitly
permits. This document resolves the conflict deterministically, rather than
silently dropping either bullet, by reading the more specific bullet
(`historical-analysis`'s named three-role list) as a documented, narrow
exception to the more general bullet (`operator`'s default label), yielding
rule 2's two-value consequent above. `producerType = agent` receives no
equivalent exception, because the `historical-analysis` bullet names exactly
`{harness, operator, adapter}` and deliberately omits `agent`; rule 1 remains
a single-value, exceptionless consequent for that reason. This resolution is
Phase 2b `1.0` normative behavior, on equal footing with every other rule in
the table above; it is not a provisional or non-normative reading.

`baselineSource` is authored provenance metadata, never observed fact.
`evidenceBasis = adapter-inference` or `evidenceBasis = historical-analysis`
paired with `producerType = harness` or `producerType = adapter` records
that the named role wrapped or inferred a budget from evidence whose origin
may differ from that role; the budget stays authored and inferred and is
never relabeled as an observed fact about the stopped run's actual
continuation. See
`docs/architecture/ADR-0004-baseline-comparison-and-authored-estimate.md`
for the full evidence-model framing this contract implements.

`PT404` pointer targeting: unlike `TerminationDeclaration`'s `PT303`, which
has two possible pointer targets depending on whether an external
root-level field (`terminationKind`) or two jointly coupled
`declarationSource` members anchor the violation, every one of this
contract's five coherence rules couples two members of the same
`baselineSource` object (`producerType` and `evidenceBasis`) with neither
member external to that object. `PT404` therefore has exactly one pointer
target for every violated rule: `/baselineSource`. Its `Message` is one
fixed, safe template that never echoes any document value.

## Diagnostic registry

Every baseline-definition and CLI-`compare` validation failure reports one
or more `Diagnostic{Code, Pointer, Message}` values
(`src/ProgressTrace.Core/Diagnostics/Diagnostic.cs`). In every `Message`
below, a `{name}` placeholder is the static JSON property name fixed by this
schema (for example `traceId`, `obligationId`, `eventBudget`), never a value
read from the document; no diagnostic in this registry echoes an untrusted
document value in its `Message`.

Phase 2b baseline-definition validation reuses the following codes already
defined for the trace envelope, obligation ledger, and termination
declaration, unmodified in meaning:

- `PT000` malformed JSON: the baseline-definition document is not
  syntactically valid JSON. Pointer `""`. Message: `"Input is not valid
  JSON."` Short-circuits; no other baseline diagnostic accompanies it.
- `PT001` required property missing. Pointer: the missing property's own
  pointer (for example `/traceId`, `/obligationBudgets/0/obligationId`).
  Message: `"Required property {name} is missing."`
- `PT002` wrong JSON type for a value that is present, including a JSON
  number with a nonzero fractional component supplied for `eventBudget`.
  Pointer: that value's own pointer, or `""` if the baseline-definition root
  itself is not a JSON object. Message: `"{name} must be {expected type}."`,
  or, for a non-object root, `"BaselineDefinition must be an object."`
- `PT003` unknown property rejected by a closed object schema. Pointer: the
  unknown property's own pointer. Message: `"Property is not allowed."`
- `PT004` duplicate JSON object property name, checked throughout the
  document. Pointer: the duplicate occurrence's own pointer. Message:
  `"JSON object property names must be unique."`
- `PT005` input exceeds 16 MiB (16,777,216 bytes,
  `TraceValidator.MaximumInputSizeBytes`), checked before JSON parsing.
  Pointer `""`. Message: `"Input exceeds the maximum size of
  {MaximumInputSizeBytes} bytes."` Short-circuits; no parsing is attempted
  and no other baseline diagnostic accompanies it.
- `PT100` unsupported `schemaVersion` value (present, a string, but not
  exactly `"1.0"`). Pointer `/schemaVersion`. Message: `"Schema version is
  not supported."`
- `PT101` invalid value for a field of the correct type: an empty or
  whitespace-only required string (`traceId`, `obligationBudgets[].obligationId`,
  `baselineSource.producerName`, or a `baselineSource.producerVersion` that
  is present as a string), a `baselineSource.producerType` string that is
  not exactly one of the fixed closed four-value enum, a
  `baselineSource.evidenceBasis` string that is not exactly one of the
  fixed closed five-value enum, or an `obligationBudgets[].eventBudget`
  integer outside the closed range `1..2147483647`. Pointer: the invalid
  value's own pointer (for example `/baselineSource/producerType`,
  `/obligationBudgets/2/eventBudget`). Message: `"{name} must not be
  empty."`, `"Producer type is not recognized."`, `"Evidence basis is not
  recognized."`, or `"eventBudget must be an integer between 1 and
  2147483647 inclusive."`, as applicable. A `null` `producerVersion` is
  valid and is never a `PT101`; only a present-but-empty `producerVersion`
  string is.

Phase 2b allocates a fresh `PT4xx` block, distinct from and not carried
forward from the `PT3xx` block Task A allocated through `PT303`, for
baseline-definition-specific structural, coherence, referential, and
coverage failures that have no existing precedent: `PT400`-`PT404`. All five
are allocated by this document, transcribing
`docs/architecture/ADR-0004-baseline-comparison-and-authored-estimate.md`'s
architecture, now.

- `PT400` duplicate `obligationId`: `obligationBudgets[].obligationId`
  repeats an id already declared earlier in the array. Pointer: the
  repeated entry's own `/obligationBudgets/{i}/obligationId`, where `i` is
  the array index of the later (duplicate) occurrence. Message: `"Obligation
  id must be unique within the baseline definition."`
- `PT401` `traceId` mismatch: the baseline definition's `traceId` does not
  equal the paired trace envelope's `traceId`. Pointer `/traceId`. Message:
  `"BaselineDefinition traceId must equal the paired trace envelope's
  traceId."`
- `PT402` dangling `obligationId`: an `obligationBudgets[].obligationId`
  does not dereference any obligation id declared in the paired ledger's
  `obligations` array. Pointer: `/obligationBudgets/{i}/obligationId`,
  where `i` is that entry's array index. Message: `"Baseline
  obligationBudgets entry does not reference a declared ledger
  obligation."`
- `PT403` missing obligation coverage: an obligation id declared in the
  paired ledger's `obligations` array has no corresponding
  `obligationBudgets` entry. Pointer `/obligationBudgets`, since the missing
  coverage has no location of its own within the baseline-definition
  document to point to more precisely, and the ledger-side obligation id is
  never echoed in the message per the redaction precedent. Message:
  `"Baseline obligationBudgets is missing an entry for a declared ledger
  obligation."` One `PT403` diagnostic is emitted per uncovered ledger
  obligation, in the ledger's own `obligations` array order (see
  "Validation order" below); all share this same pointer.
- `PT404` baseline-source coherence violation: one of the five
  baseline-source coherence rules in "Baseline-source coherence rules"
  above is violated. Pointer `/baselineSource` for every case, per that
  section's pointer-targeting note. Message, one fixed safe template for
  every case that never echoes any document value: `"BaselineDefinition
  baselineSource is not coherent with the declared evidence basis."`
  Evaluated only after `producerType` and `evidenceBasis` have each
  validated as a recognized enum value; a baseline definition violating more
  than one coherence rule emits one `PT404` per violated rule, in ascending
  rule number. At most two of the five rules can be simultaneously violated
  for any single `(producerType, evidenceBasis)` pair — at most one of rules
  1-2 (mutually exclusive antecedents: `producerType` is exactly one value)
  and at most one of rules 3-5 (mutually exclusive antecedents:
  `evidenceBasis` is exactly one value) — so `PT404` never fires more than
  twice for the same document.

No code is reused across meanings, and no two codes above share a
pointer-target rule for the same failure.

### Contract status

This diagnostic registry, meaning every code's identity, its exact
`Pointer` target rule, and which validation condition produces it, is part
of the public Phase 2b `1.0` contract for both
`contracts/baseline-definition.schema.json`'s implementation and the
`compare` CLI's stdout/stderr shape. Conformance tests assert the exact
`Code`, and `Pointer` where material, for every invalid fixture. Changing a
code's meaning, removing a code, changing a pointer-target rule, or
changing which validation condition a code reports is a breaking change
under the version policy below: it requires a new major version, Dragos's
approval, and new conformance fixtures, and is never introduced as a silent
change to `1.0`. `Message` text is a fixed, safe template documented here
for implementation consistency; conformance tests do not assert its exact
bytes, unlike `Code` and `Pointer`.

## Validation order

Baseline-definition validation runs in two phases, always in this order,
using `System.Text.Json`'s order-preserving `JsonElement` enumeration for
every JSON object and array traversal below; no step ever depends on
`Dictionary`/`HashSet`/`HashCode` iteration order, only on document order
for producing diagnostics and on set membership for referential checks.

**Phase A, baseline-only structural checks** (do not require the paired
trace envelope or obligation ledger):

1. Size check (`PT005`). Short-circuits: if the input exceeds the limit,
   this is the only diagnostic and JSON parsing is never attempted.
2. JSON parse (`PT000`). Short-circuits: if parsing fails, this is the only
   diagnostic and no further check runs.
3. Duplicate-property whole-document walk (`PT004`), over the entire parsed
   tree in pre-order (an object's properties in document declaration order,
   then recursing into each property's value; an array's elements by
   ascending index, recursing into each element). This runs unconditionally
   before any field-specific check and never short-circuits any later step.
4. Root type check: if the root is not a JSON object, add `PT002` at
   pointer `""` and stop; no further baseline check runs, though step 3's
   diagnostics, if any, are preserved.
5. Root unknown-property check (`PT003`), in document property declaration
   order.
6. Root required-property presence and type checks (`PT001`/`PT002`),
   evaluated in this fixed order regardless of document order:
   `schemaVersion`, `traceId`, `baselineSource`, `obligationBudgets`.
   `baselineSource`'s type check adds `PT002` if it is present but not a
   JSON object; `obligationBudgets`'s type check adds `PT002` if it is
   present but not a JSON array.
7. `schemaVersion` value check (`PT100`), only if step 6 read it as a
   present string.
8. `traceId` non-empty check (`PT101`), only if step 6 read it as a present
   string.
9. `baselineSource` nested structural checks, only if step 6 read
   `baselineSource` as a present JSON object:
   a. Nested unknown-property check (`PT003`), in document property
      declaration order.
   b. Nested required-property presence and type checks (`PT001`/`PT002`),
      evaluated in this fixed order regardless of document order:
      `producerType`, `producerName`, `producerVersion`, `evidenceBasis`.
      `producerVersion`'s type check accepts a JSON string or the JSON
      `null` literal; any other JSON type adds `PT002`.
10. `baselineSource` nested value checks, each only if step 9b read the
    corresponding member as a present value of the correct type, evaluated
    in this fixed order: `producerType` closed-enum check (`PT101`),
    `producerName` non-empty check (`PT101`), `producerVersion` non-empty
    check (`PT101`, only when `producerVersion` is present as a string; a
    `null` `producerVersion` is skipped), `evidenceBasis` closed-enum check
    (`PT101`).
11. `baselineSource` coherence checks (`PT404`), evaluated once, only if
    step 10 read `producerType` and `evidenceBasis` both as recognized enum
    values. Test the five coherence rules from "Baseline-source coherence
    rules" in ascending rule number; for each violated rule append one
    `PT404` at pointer `/baselineSource`. All `PT404` diagnostics are Phase
    A diagnostics and therefore always precede any Phase B diagnostic.
12. `obligationBudgets` walk, only if step 6 read `obligationBudgets` as a
    present JSON array; walk its entries in ascending index order. For each
    entry: unknown-property check (`PT003`), required/type checks for
    `obligationId` and `eventBudget` (`PT001`/`PT002`, `eventBudget`'s type
    check rejecting any JSON number with a nonzero fractional component as
    well as any non-numeric type), non-empty check for `obligationId`
    (`PT101`), range check for `eventBudget` (`PT101`, only if its type
    check passed), then a duplicate-id check (`PT400`) against every
    `obligationId` string successfully read from a lower index in this same
    walk.

**Phase B, cross-document referential and coverage checks** (require the
paired trace envelope and the paired obligation ledger; per "CLI compare
behavior" below, Phase B runs only when the trace envelope and the
obligation ledger have each already validated with zero diagnostics; the
`compare` CLI additionally requires the termination declaration to have
already validated with zero diagnostics before Phase B ever runs, per that
section's precedence, even though Phase B's own checks do not read the
declaration):

1. `traceId` mismatch check (`PT401`), evaluated once, only if step A8 read
   a non-empty `traceId`.
2. For every `obligationBudgets` entry, in ascending index order: dangling
   `obligationId` check (`PT402`) against the set of `id` strings declared
   in the paired ledger's `obligations` array (independent of whether that
   entry's `obligationId` also triggered `PT400`).
3. Missing obligation coverage check (`PT403`), evaluated once, after B2
   completes: for every obligation id in the paired ledger's `obligations`
   array, in that array's own order, if no `obligationBudgets` entry
   dereferenced it successfully in B2 (an entry whose `obligationId`
   triggered `PT402` does not count as covering any obligation), add one
   `PT403` at pointer `/obligationBudgets`.

Every diagnostic produced by Phase A and Phase B is appended to one result
list in exactly the step order above: all of Phase A before any of Phase B,
and within each phase in the numbered sub-step order. No later step ever
removes, reorders, or suppresses a diagnostic added by an earlier step; the
only short-circuits are Phase A steps 1, 2, and 4. A baseline definition is
valid only when both Phase A and Phase B produce zero diagnostics.

## CLI compare behavior

Usage: `compare <trace-path> <ledger-path> <termination-declaration-path>
<baseline-definition-path>`, exactly four positional arguments, extending
the existing usage line without changing it: `progresstrace
<validate|normalize> <path> | progresstrace evaluate <trace-path>
<ledger-path> | progresstrace assess <trace-path> <ledger-path>
<termination-declaration-path> | progresstrace compare <trace-path>
<ledger-path> <termination-declaration-path> <baseline-definition-path>`.
Bad usage, meaning an argument count and leading verb combination that does
not match `validate`/`normalize` (2 arguments), `evaluate` (3 arguments),
`assess` (4 arguments), or `compare` (5 arguments), writes
`{"error":"usage","message":"Usage: progresstrace <validate|normalize>
<path> | progresstrace evaluate <trace-path> <ledger-path> | progresstrace
assess <trace-path> <ledger-path> <termination-declaration-path> |
progresstrace compare <trace-path> <ledger-path>
<termination-declaration-path> <baseline-definition-path>"}` to stderr and
exits `2`; no file is opened.

Exact order of operations for a well-formed `compare` invocation, extending
`assess`'s trace-then-ledger-then-declaration precedence by one further
stage, trace-then-ledger-then-declaration-then-baseline; the `document`
value used in every diagnostic and error shape for the fourth input is
exactly the string `"baseline"`:

1. Size-check `trace-path`. If it exceeds
   `TraceValidator.MaximumInputSizeBytes`, write
   `{"valid":false,"document":"trace","diagnostics":[<PT005>]}` to stdout
   and exit `2`. No later path is ever opened.
2. If `trace-path` cannot be read (`IOException`/
   `UnauthorizedAccessException`), write
   `{"error":"input","document":"trace","message":"Input file could not be
   read."}` to stderr and exit `2`. No later path is opened.
3. Parse and validate `trace-path` via `TraceValidator.ParseAndValidate`. If
   it returns one or more diagnostics, write
   `{"valid":false,"document":"trace","diagnostics":[...]}` to stdout; exit
   `2` if any returned diagnostic's code is `PT000`, otherwise exit `1`. No
   later path is ever opened, size-checked, read, or parsed: the trace is
   checked, in full, to completion, before any later document is touched at
   all.
4. Only when `trace-path` validates with zero diagnostics, size-check
   `ledger-path`; on excess size, write
   `{"valid":false,"document":"ledger","diagnostics":[<PT005>]}` to stdout
   and exit `2`. Neither later path is opened.
5. If `ledger-path` cannot be read, write
   `{"error":"input","document":"ledger","message":"Input file could not be
   read."}` to stderr and exit `2`.
6. Run ledger Phase A then Phase B, exactly as defined in
   `docs/contracts/obligation-ledger.md`'s "Validation order", using the
   already-validated trace envelope from step 3 for Phase B. If Phase A's
   JSON parse fails, write
   `{"valid":false,"document":"ledger","diagnostics":[<PT000>]}` to stdout
   and exit `2`. If Phase A or Phase B produces one or more other
   diagnostics, write `{"valid":false,"document":"ledger","diagnostics":[...]}`
   to stdout and exit `1`. Neither later path is opened in either case.
7. Only when `ledger-path` validates with zero diagnostics across both
   phases, size-check `termination-declaration-path`; on excess size, write
   `{"valid":false,"document":"declaration","diagnostics":[<PT005>]}` to
   stdout and exit `2`. `baseline-definition-path` is not opened.
8. If `termination-declaration-path` cannot be read, write
   `{"error":"input","document":"declaration","message":"Input file could
   not be read."}` to stderr and exit `2`.
9. Run declaration Phase A then Phase B, as defined in
   `docs/contracts/termination-declaration.md`'s "Validation order", using
   the already-validated trace envelope from step 3 for Phase B. If Phase
   A's JSON parse fails, write
   `{"valid":false,"document":"declaration","diagnostics":[<PT000>]}` to
   stdout and exit `2`. If Phase A or Phase B produces one or more other
   diagnostics, write
   `{"valid":false,"document":"declaration","diagnostics":[...]}` to stdout
   and exit `1`. `baseline-definition-path` is not opened in either case.
10. Only when `termination-declaration-path` validates with zero
    diagnostics across both phases, size-check `baseline-definition-path`;
    on excess size, write
    `{"valid":false,"document":"baseline","diagnostics":[<PT005>]}` to
    stdout and exit `2`.
11. If `baseline-definition-path` cannot be read, write
    `{"error":"input","document":"baseline","message":"Input file could not
    be read."}` to stderr and exit `2`.
12. Run baseline-definition Phase A then Phase B, as defined in "Validation
    order" above, using the already-validated trace envelope from step 3
    and the already-validated ledger from step 6 for Phase B. If Phase A's
    JSON parse fails, write
    `{"valid":false,"document":"baseline","diagnostics":[<PT000>]}` to
    stdout and exit `2`. If Phase A or Phase B produces one or more other
    diagnostics, write
    `{"valid":false,"document":"baseline","diagnostics":[...]}` to stdout
    and exit `1`.
13. Only when the trace, the ledger, the declaration, and the baseline
    definition all validate with zero diagnostics, recompute
    `StopAssessmentResult`'s underlying fields internally, exactly as
    `docs/contracts/stop-assessment-result.md` defines, by invoking the
    unmodified `ProgressTrace.Core.Assessment` and
    `ProgressTrace.Core.Evaluation.Evaluator` logic over the validated
    trace, ledger, and declaration — never by reading a precomputed
    `StopAssessmentResult` file, since no such file is ever an input to
    `compare` — then run the deterministic baseline-comparison algorithm
    fixed in `docs/contracts/baseline-comparison-result.md` over that
    recomputed context and the validated baseline definition to produce a
    `BaselineComparisonResult`, and write it to stdout using the exact
    canonical serialization fixed in that document (fixed property order,
    UTF-8, no byte-order mark, no insignificant whitespace, exactly one
    trailing newline byte), with no `{"valid":true,...}` wrapper, mirroring
    `assess`'s existing precedent of writing the raw canonical artifact
    directly to stdout on success. Exit `0`.

`BaselineComparisonResult` is written to stdout if and only if step 13 is
reached: the trace envelope, the obligation ledger, the termination
declaration, and the baseline definition, including every cross-document
referential and coverage check, all validate with zero diagnostics. There is
no partial, best-effort, or diagnostics-plus-result output; a failing trace,
ledger, declaration, or baseline definition never produces a
`BaselineComparisonResult`, and a successful `BaselineComparisonResult`
never carries diagnostics.

Exit codes, consistent with the existing `0`/`1`/`2` convention: `0` only
when step 13 emits a `BaselineComparisonResult`; `1` when any one of the
four documents produces a structural, type, referential, terminal-required,
enum-membership, coherence, or coverage diagnostic (`PT001`-`PT004`,
`PT100`-`PT101`, `PT200`-`PT204`, `PT300`-`PT303`, `PT400`-`PT404`) but none
of the four is malformed, oversized, or unreadable; `2` for bad usage, an
unreadable file, an oversized file (`PT005`), or malformed JSON (`PT000`),
on any of the four documents.

`compare` introduces no network, external process, database, or filesystem
access beyond reading the four given file paths. The two diagnostic-
reporting shapes above (`{"valid":...,"document":...,"diagnostics":[...]}`
and `{"error":...,"document":...,"message":...}`) plus the raw
`BaselineComparisonResult` document are the complete, stable set of
`compare`'s stdout contents on their respective paths, and the
`{"error":"usage",...}` and `{"error":"input",...}` shapes are the
complete, stable set of its stderr contents.

## Canonical serialization

The `compare` CLI never emits, echoes, or normalizes a `BaselineDefinition`
document; it is a pure authored input, consumed only by `compare`, and there
is no verb or flag that writes a canonicalized `BaselineDefinition` to
stdout, mirroring `TerminationDeclaration`'s precedent. No
canonical-serialization rule is therefore defined for this contract.
`BaselineComparisonResult` copies `baselineSource`'s four member values
unchanged into its own canonically serialized output, serialized in that
result's own fixed canonical nested property order, defined in
`docs/contracts/baseline-comparison-result.md`; this is
`BaselineComparisonResult`'s own canonical order, not a claim that this
document's raw property order is preserved or reproduced byte-for-byte.

## Version policy

A reader accepts only the schema versions it explicitly supports. This
contract's Phase 2b implementation accepts exactly `schemaVersion` `1.0` and
rejects a baseline definition declaring any other value. Contract evolution
distinguishes two kinds of change:

- **Additive compatible evolution**: a new optional property or another
  purely additive extension that does not alter any existing required
  field, either closed enum, the one-entry-per-obligation coverage rule, or
  the `1..2147483647` `eventBudget` range may be introduced in a later minor
  version, consumable by newer readers without breaking readers pinned to
  `1.0`.
- **Breaking change**: adding or changing a value in either closed enum, the
  coverage rule, the `eventBudget` range or unit, the single-trace binding
  (introducing a reusable cross-trace template), or any required field is a
  breaking change. It requires a new major version and Dragos's approval,
  new conformance fixtures, and is never introduced as a minor-version patch
  or a silent change to `1.0`.

## Synthetic examples

The following examples are illustrative of document shape only; they are
not an exhaustive statement of validation coverage, and every normative
rule is fixed in the sections above. All example data is synthetic and
contains no employer, medical, credential, or production content. Both
examples correlate to a hypothetical obligation ledger declaring a single
obligation `obl-1`, paired with a trace envelope whose `traceId` is
`trace-example-1`.

A harness-authored baseline definition applying a fixed policy budget:

```json
{
  "schemaVersion": "1.0",
  "traceId": "trace-example-1",
  "baselineSource": {
    "producerType": "harness",
    "producerName": "example-harness",
    "producerVersion": "2.3.0",
    "evidenceBasis": "harness-policy"
  },
  "obligationBudgets": [
    { "obligationId": "obl-1", "eventBudget": 20 }
  ]
}
```

An agent-authored, self-estimated baseline definition, remaining an
explicitly self-authored estimate with no privileged truth status:

```json
{
  "schemaVersion": "1.0",
  "traceId": "trace-example-1",
  "baselineSource": {
    "producerType": "agent",
    "producerName": "example-agent",
    "producerVersion": null,
    "evidenceBasis": "agent-estimate"
  },
  "obligationBudgets": [
    { "obligationId": "obl-1", "eventBudget": 12 }
  ]
}
```

An adapter-produced baseline definition wrapping an operator's estimate,
recording `producerType = adapter` while `evidenceBasis = operator-estimate`
preserves the evidence-basis category (an operator-authored estimate)
without relabeling it as the adapter's own inference; `producerName` and
`producerVersion` here name the adapter, not the original operator, and no
field identifies that operator:

```json
{
  "schemaVersion": "1.0",
  "traceId": "trace-example-1",
  "baselineSource": {
    "producerType": "adapter",
    "producerName": "example-otel-adapter",
    "producerVersion": "0.9.1",
    "evidenceBasis": "operator-estimate"
  },
  "obligationBudgets": [
    { "obligationId": "obl-1", "eventBudget": 30 }
  ]
}
```
