# Obligation ledger

Version `1.0` is the Phase 1 obligation-ledger contract. It is a second raw
input, paired with a trace envelope (`contracts/trace-envelope.schema.json`),
consumed by `evaluate`. The normative machine-readable contract is
`contracts/obligation-ledger.schema.json`, implemented by Codex from this
document; the .NET Core applies the same structural rules plus the
referential-integrity and ordering invariants below.

## Purpose

An obligation ledger declares, explicitly and in a structured form, the task
obligations an agent trace is evaluated against, and the status signals that
report each obligation's observed status over time. Evaluation reads only
these structured signals; it never infers progress from unstructured event
payload text, which remains opaque exactly as in the trace envelope.

## Encoding

An obligation ledger is UTF-8 encoded JSON. Malformed JSON, meaning
syntactically invalid input, is a distinct failure mode from a structurally
or referentially invalid document: malformed JSON is rejected before any
semantic rule is applied. Input larger than 16 MiB (16,777,216 bytes) is
rejected before JSON parsing, reusing the trace envelope's size limit
(`TraceValidator.MaximumInputSizeBytes`). Structural objects reject unknown
members, and duplicate JSON property names are rejected throughout the
document, mirroring the trace envelope's `PT004` precedent.

## Document structure

- `schemaVersion`: contract major/minor version. Phase 1 accepts only `1.0`.
- `traceId`: non-empty identity that must equal the paired trace envelope's
  `traceId`.
- `obligations`: non-empty array of obligation declarations. An empty
  `obligations` array fails validation; it is never treated as vacuously
  valid.
- `obligations[].id`: non-empty identity, unique within the ledger.
- `obligations[].description`: opaque, unparsed string describing the
  obligation. It carries no normative meaning; evaluation never interprets
  it.
- `signals`: array of obligation-status signals correlated to trace events.
- `signals[].obligationId`: must dereference an `obligations[].id` declared
  in this ledger. A dangling reference fails validation.
- `signals[].eventId`: must dereference an `events[].id` present in the
  paired trace envelope. A dangling reference fails validation.
- `signals[].status`: one of the fixed closed enum `open`, `in-progress`,
  `satisfied`, `regressed`, `abandoned`. No other value is accepted.

An obligation with zero associated signals is structurally valid, not a
validation error; it is reserved for producing the `insufficient-evidence`
classification during evaluation, distinct from a dangling reference, which
always fails validation instead.

## Field types and requiredness

The root object is a JSON object with `additionalProperties: false`;
unknown top-level members are rejected. All four root properties are
required: `schemaVersion` (string, exactly `"1.0"` in this version),
`traceId` (string, non-empty), `obligations` (array, minimum 1 item), and
`signals` (array; may be empty).

Each entry of `obligations` is a JSON object with
`additionalProperties: false`; both properties are required: `id` (string,
non-empty, unique within the ledger's `obligations` array) and
`description` (string; no minimum length, since it is opaque and carries no
normative meaning — an empty string is valid).

Each entry of `signals` is a JSON object with `additionalProperties: false`;
all three properties are required: `obligationId` (string, non-empty, must
dereference an `obligations[].id`), `eventId` (string, non-empty, must
dereference an event id in the paired trace envelope), and `status` (string,
must be exactly one of the fixed closed enum `open`, `in-progress`,
`satisfied`, `regressed`, `abandoned`; no other value, casing, or free text
is accepted).

## Referential integrity

- `traceId` must equal the paired trace envelope's `traceId`; a mismatch
  fails validation (exit 1).
- Obligation ids must be unique within the ledger; a duplicate id fails
  validation (exit 1).
- Every `signals[].eventId` must dereference an existing event id in the
  paired trace envelope; a dangling reference fails validation (exit 1).
- Every `signals[].obligationId` must dereference an obligation id declared
  in `obligations`; a dangling reference fails validation (exit 1).

## Signal total order

For each obligation, its associated signals are totally ordered ascending
by the `sequence` value of the trace event dereferenced by each signal's
`eventId`, never by the signal's position in the ledger's `signals` array on
its own. Signals for the same obligation that share an identical `eventId`,
and therefore an identical dereferenced sequence, are tie-broken by their
index position within the ledger's `signals` array, with a lower array
index ordering first. This total order is the order used both for
per-obligation evaluation classification (`docs/contracts/evaluation-result.md`)
and for the abandoned-terminal rule below.

## Abandoned-terminal rule

Abandoned is terminal. For a given obligation, if any signal has
`status=abandoned`, no other signal for that same obligation may occupy a
position after it in the signal total order defined above. Both of the
following are violations, and either one fails validation (exit 1) before
the ledger ever reaches evaluation:

- a signal whose dereferenced event sequence is strictly greater than the
  abandoned signal's dereferenced event sequence;
- a signal that shares the abandoned signal's `eventId`, and therefore its
  dereferenced sequence, but occupies a higher index position in the
  ledger's `signals` array.

Because abandoned-terminal validation guarantees this, an abandoned signal
can only ever be the last signal in an obligation's total order whenever it
is present.

## Validation diagnostics versus evaluation classification

Validation diagnostics and evaluation classifications are distinct outcomes
and are never conflated. Dangling `eventId` or `obligationId` references, a
duplicate obligation id, an empty `obligations` array, a `traceId`
mismatch, and any abandoned-terminal violation are all validation failures
(exit 1); none of them reach evaluation. An obligation with zero associated
signals is not a validation failure: it is structurally valid and
deterministically classifies as `insufficient-evidence` at evaluation time,
per `docs/contracts/evaluation-result.md`. The exact diagnostic code and
JSON Pointer target for each of these validation failures, the
deterministic order in which they are checked, and the `evaluate` CLI's
resulting stdout/stderr shape and exit codes are fixed in "Diagnostic
registry", "Validation order", and "CLI evaluate behavior" below.

## Diagnostic registry

Every ledger and CLI-`evaluate` validation failure reports one or more
`Diagnostic{Code, Pointer, Message}` values
(`src/ProgressTrace.Core/Diagnostics/Diagnostic.cs`). In every `Message`
below, a `{name}` placeholder is the static JSON property name fixed by
this schema (for example `traceId`, `id`, `status`), never a value read
from the document; no diagnostic in this registry echoes an untrusted
document value in its `Message`.

Phase 1 ledger and cross-document validation reuses the following codes
already defined for the trace envelope, unmodified in meaning:

- `PT000` malformed JSON: the ledger document is not syntactically valid
  JSON. Pointer `""`. Message: `"Input is not valid JSON."` Short-circuits;
  no other ledger diagnostic accompanies it.
- `PT001` required property missing. Pointer: the missing property's own
  pointer (for example `/traceId`, `/obligations/0/id`,
  `/signals/2/status`). Message: `"Required property {name} is missing."`
- `PT002` wrong JSON type for a value that is present. Pointer: that
  value's own pointer, or `""` if the ledger root itself is not a JSON
  object. Message: `"{name} must be {expected type}."`, or, for a
  non-object root, `"Ledger must be an object."`
- `PT003` unknown property rejected by a closed object schema. Pointer:
  the unknown property's own pointer. Message: `"Property is not
  allowed."`
- `PT004` duplicate JSON object property name, checked throughout the
  document, including inside `description` and any other nested value.
  Pointer: the duplicate occurrence's own pointer. Message: `"JSON object
  property names must be unique."`
- `PT005` input exceeds 16 MiB (16,777,216 bytes,
  `TraceValidator.MaximumInputSizeBytes`), checked before JSON parsing.
  Pointer `""`. Message: `"Input exceeds the maximum size of
  {MaximumInputSizeBytes} bytes."` Short-circuits; no parsing is attempted
  and no other ledger diagnostic accompanies it.
- `PT100` unsupported `schemaVersion` value (present, a string, but not
  exactly `"1.0"`). Pointer `/schemaVersion`. Message: `"Schema version is
  not supported."`
- `PT101` invalid value for a field of the correct type: an empty or
  whitespace-only required string (`traceId`, `obligations[].id`,
  `signals[].obligationId`, `signals[].eventId`), an empty `obligations`
  array, or a `signals[].status` string that is not exactly one of
  `open`, `in-progress`, `satisfied`, `regressed`, `abandoned`. Pointer:
  the invalid value's own pointer, `/obligations` for the empty-array
  case. Message: `"{name} must not be empty."`, `"Obligations must
  contain at least one entry."`, or `"Status is not recognized."`, as
  applicable.

Phase 1 allocates the following new, non-colliding codes for
obligation-specific semantic failures that have no trace-envelope
precedent. They form a `PT2xx` block, distinct from the
trace-envelope-specific `PT102`-`PT104` block: `PT102`
(`DuplicateEventId`), `PT103` (`NonMonotonicSequence`), and `PT104`
(`NonMonotonicTimestamp`) describe trace-envelope-only invariants that do
not apply to the ledger and are not reused or reinterpreted here.

- `PT200` duplicate obligation id: `obligations[].id` repeats an id
  already declared earlier in the array. Pointer: the repeated entry's
  own `/obligations/{i}/id`, where `i` is the array index of the later
  (duplicate) occurrence. Message: `"Obligation id must be unique within
  the ledger."`
- `PT201` traceId mismatch: the ledger's `traceId` does not equal the
  paired trace envelope's `traceId`. Pointer `/traceId`. Message:
  `"Ledger traceId must equal the paired trace envelope's traceId."`
- `PT202` dangling `obligationId`: a `signals[].obligationId` does not
  dereference any id declared in this ledger's `obligations`. Pointer:
  `/signals/{i}/obligationId`, where `i` is that signal's array index.
  Message: `"Signal obligationId does not reference a declared
  obligation."`
- `PT203` dangling `eventId`: a `signals[].eventId` does not dereference
  any event id present in the paired trace envelope. Pointer:
  `/signals/{i}/eventId`, where `i` is that signal's array index.
  Message: `"Signal eventId does not reference an event in the paired
  trace envelope."`
- `PT204` abandoned-terminal violation: a signal occupies a position
  after an `abandoned` signal for the same obligation in the signal
  total order. One code covers both violation shapes defined in
  "Abandoned-terminal rule" above, the strictly-greater-dereferenced-
  sequence case and the same-`eventId` higher-array-index tie case,
  since both violate the same normative rule at a different point in the
  total order. Pointer: `/signals/{i}`, where `i` is the array index, in
  the ledger's `signals` array, of the signal that occupies the invalid
  later position, not the abandoned signal itself. Message: `"Signal
  violates the abandoned-terminal rule."`

No code is reused across meanings, and no two codes above share a
pointer-target rule for the same failure. `PT200`-`PT204` are Phase 1
`1.0` obligation-ledger codes, defined here for the first time; they are
not defined or reserved anywhere else.

### Contract status

This diagnostic registry, meaning every code's identity, its exact
`Pointer` target rule, and which validation condition produces it, is
part of the public Phase 1 `1.0` contract for both
`contracts/obligation-ledger.schema.json`'s implementation and the
`evaluate` CLI's stdout/stderr shape. Conformance tests assert the exact
`Code`, and `Pointer` where material, for every invalid fixture.
Changing a code's meaning, removing a code, changing a pointer-target
rule, or changing which validation condition a code reports is a
breaking change under the version policy below: it requires a new major
version, Dragos's approval, and new conformance fixtures, and is never
introduced as a silent change to `1.0`. `Message` text is a fixed, safe
template documented here for implementation consistency; conformance
tests do not assert its exact bytes, unlike `Code` and `Pointer`.

## Validation order

Ledger validation runs in two phases, always in this order, using
`System.Text.Json`'s order-preserving `JsonElement` enumeration for every
JSON object and array traversal below; no step ever depends on
`Dictionary`/`HashSet`/`HashCode` iteration order, only on document order
for producing diagnostics and on set membership for referential checks.

**Phase A, ledger-only structural checks** (do not require the paired
trace envelope):

1. Size check (`PT005`). Short-circuits: if the input exceeds the limit,
   this is the only diagnostic and JSON parsing is never attempted.
2. JSON parse (`PT000`). Short-circuits: if parsing fails, this is the
   only diagnostic and no further check runs.
3. Duplicate-property whole-document walk (`PT004`), over the entire
   parsed tree in pre-order (an object's properties in document
   declaration order, then recursing into each property's value; an
   array's elements by ascending index, recursing into each element).
   This runs unconditionally before any field-specific check, mirroring
   the trace envelope's `PT004` precedent, and never short-circuits any
   later step.
4. Root type check: if the root is not a JSON object, add `PT002` at
   pointer `""` and stop; no further ledger check runs, though step 3's
   diagnostics, if any, are preserved.
5. Root unknown-property check (`PT003`), in document property
   declaration order.
6. Root required-property presence and type checks (`PT001`/`PT002`),
   evaluated in this fixed order regardless of document order:
   `schemaVersion`, `traceId`, `obligations`, `signals`.
7. `schemaVersion` value check (`PT100`), only if step 6 read it as a
   present string.
8. `traceId` non-empty check (`PT101`), only if step 6 read it as a
   present string.
9. `obligations` walk: if step 6 confirmed `obligations` is an array of
   length zero, add `PT101` at `/obligations` once. Otherwise, walk its
   entries in ascending index order; for each entry: unknown-property
   check (`PT003`), required/type checks for `id` and `description`
   (`PT001`/`PT002`), non-empty check for `id` (`PT101`), then a
   duplicate-id check (`PT200`) against every `id` string successfully
   read from a lower index in this same walk.
10. `signals` walk, in ascending index order; for each entry:
    unknown-property check (`PT003`), required/type checks for
    `obligationId`, `eventId`, `status` (`PT001`/`PT002`), non-empty
    checks for `obligationId` and `eventId` (`PT101`), and a closed-enum
    check for `status` (`PT101`).

**Phase B, cross-document referential checks** (require the paired trace
envelope; per "CLI evaluate behavior" below, Phase B runs only when the
trace envelope itself has already validated with zero diagnostics):

1. `traceId` mismatch check (`PT201`), evaluated once, only if step A8
   read a non-empty `traceId`.
2. For every `signals` entry, in ascending index order: dangling
   `obligationId` check (`PT202`) against the set of `id` strings read in
   A9 (independent of whether that id also triggered `PT200` or
   `PT101`), then dangling `eventId` check (`PT203`) against the set of
   event ids in the paired trace envelope. Both may fire for the same
   entry.
3. Abandoned-terminal check (`PT204`), evaluated once, after B2
   completes, per obligation in `obligations` array order: gather the
   subset of `signals` entries whose `obligationId` and `eventId` both
   dereferenced successfully in B2 for this obligation (an entry with
   either dangling reference is excluded, since its sequence cannot be
   dereferenced and it already carries its own `PT202`/`PT203`
   diagnostic); order that subset ascending by the dereferenced trace
   event's `sequence`, ties on identical `eventId` broken by ascending
   `signals`-array index, exactly as in "Signal total order" above; if an
   `abandoned`-status signal in that order is not the last element, add
   one `PT204` diagnostic for every signal that occupies a position after
   it, in ascending total-order sequence.

Every diagnostic produced by Phase A and Phase B is appended to one
result list in exactly the step order above: all of Phase A before any
of Phase B, and within each phase in the numbered sub-step order. No
later step ever removes, reorders, or suppresses a diagnostic added by
an earlier step; the only short-circuits are Phase A steps 1, 2, and 4.
Duplicate-property diagnostics (`PT004`) therefore always coexist with
every other diagnostic that fires in the same validation run, consistent
with the trace envelope's existing `PT004` behavior. A ledger is valid
only when both Phase A and Phase B produce zero diagnostics.

## CLI evaluate behavior

Usage: `evaluate <trace-path> <ledger-path>`, exactly two positional
arguments, extending the existing `validate`/`normalize` usage line
without changing it. Bad usage, meaning an argument count other than 2
combined with a first argument that is not `validate`, `normalize`, or
`evaluate`, or an `evaluate` invocation with an argument count other than
3, writes `{"error":"usage","message":"Usage: progresstrace
<validate|normalize> <path> | progresstrace evaluate <trace-path>
<ledger-path>"}` to stderr and exits `2`; neither file is opened.

Exact order of operations for a well-formed `evaluate` invocation:

1. Size-check `trace-path`. If it exceeds
   `TraceValidator.MaximumInputSizeBytes`, write
   `{"valid":false,"document":"trace","diagnostics":[<PT005>]}` to stdout
   and exit `2`. `ledger-path` is never opened.
2. If `trace-path` cannot be read (`IOException`/
   `UnauthorizedAccessException`), write
   `{"error":"input","document":"trace","message":"Input file could not
   be read."}` to stderr and exit `2`. `ledger-path` is never opened.
3. Parse and validate `trace-path` via `TraceValidator.ParseAndValidate`.
   If it returns one or more diagnostics, write
   `{"valid":false,"document":"trace","diagnostics":[...]}` to stdout;
   exit `2` if any returned diagnostic's code is `PT000`, otherwise exit
   `1`. In both cases `ledger-path` is never opened, size-checked, read,
   or parsed: the trace is checked, in full, to completion, before the
   ledger is touched at all.
4. Only when `trace-path` validates with zero diagnostics, size-check
   `ledger-path`; on excess size, write
   `{"valid":false,"document":"ledger","diagnostics":[<PT005>]}` to
   stdout and exit `2`.
5. If `ledger-path` cannot be read, write
   `{"error":"input","document":"ledger","message":"Input file could not
   be read."}` to stderr and exit `2`.
6. Run ledger Phase A then Phase B, as defined in "Validation order"
   above, using the already-validated trace envelope from step 3 for
   Phase B. If Phase A's JSON parse fails, write
   `{"valid":false,"document":"ledger","diagnostics":[<PT000>]}` to
   stdout and exit `2`. If Phase A or Phase B produces one or more other
   diagnostics, write
   `{"valid":false,"document":"ledger","diagnostics":[...]}` to stdout
   and exit `1`.
7. Only when the ledger validates with zero diagnostics across both
   phases, run `Evaluator` over the validated trace envelope and ledger
   to produce an `EvaluationResult`, and write it to stdout using the
   exact canonical serialization fixed in
   `docs/contracts/evaluation-result.md` (fixed property order, UTF-8, no
   byte-order mark, no insignificant whitespace, exactly one trailing
   newline byte), with no `{"valid":true,...}` wrapper, mirroring the
   existing `normalize` verb's precedent of writing the raw canonical
   artifact directly to stdout on success. Exit `0`.

`EvaluationResult` is written to stdout if and only if step 7 is
reached: both the trace envelope and the obligation ledger, including
every cross-document referential and abandoned-terminal check, validate
with zero diagnostics. There is no partial, best-effort, or
diagnostics-plus-result output; a failing trace or ledger never produces
an `EvaluationResult`, and a successful `EvaluationResult` never carries
diagnostics.

Exit codes, consistent with the existing `0`/`1`/`2` convention
established by `validate` and `normalize`: `0` only when step 7 emits an
`EvaluationResult`; `1` when either document produces a structural,
type, referential, or semantic diagnostic (`PT001`-`PT004`,
`PT100`-`PT101`, `PT200`-`PT204`) but is not malformed, oversized, or
unreadable; `2` for bad usage, an unreadable file, an oversized file
(`PT005`), or malformed JSON (`PT000`), on either document.

`evaluate` introduces no network, external process, database, or
filesystem access beyond reading the two given file paths. The two
diagnostic-reporting shapes above
(`{"valid":...,"document":...,"diagnostics":[...]}` and
`{"error":...,"document":...,"message":...}`) plus the raw
`EvaluationResult` document are the complete, stable set of `evaluate`'s
stdout contents on their respective paths, and the `{"error":"usage",...}`
and `{"error":"input",...}` shapes are the complete, stable set of its
stderr contents.

## Version policy

A reader accepts only the schema versions it explicitly supports. This
contract's Phase 1 implementation accepts exactly `schemaVersion` `1.0` and
rejects an obligation ledger declaring any other value. Contract evolution
distinguishes two kinds of change:

- **Additive compatible evolution**: a new optional property or another
  purely additive extension that does not alter any existing required
  field, the closed status enum, the signal total order, or the
  abandoned-terminal rule may be introduced in a later minor version,
  consumable by newer readers without breaking readers pinned to `1.0`.
- **Breaking change**: adding or changing a value in the closed status
  enum, the signal total-order rule, the abandoned-terminal rule, or any
  required field is a breaking change. It requires a new major version and
  Dragos's approval, new conformance fixtures, and is never introduced as a
  minor-version patch or a silent change to `1.0`.

## Synthetic examples

The following examples are illustrative of document shape only; they are
not an exhaustive statement of validation coverage, and every normative rule
is fixed in the sections above. All example data is synthetic and contains
no employer, medical, credential, or production content. Both examples
correlate to a hypothetical trace envelope containing events `evt-1`
(`sequence` 0) and `evt-2` (`sequence` 1).

A ledger with one obligation and one signal:

```json
{
  "schemaVersion": "1.0",
  "traceId": "trace-example-1",
  "obligations": [
    { "id": "obl-1", "description": "Write the failing test before the fix." }
  ],
  "signals": [
    { "obligationId": "obl-1", "eventId": "evt-1", "status": "open" }
  ]
}
```

A ledger with an obligation that has zero signals, structurally valid and
reserved for the `insufficient-evidence` classification:

```json
{
  "schemaVersion": "1.0",
  "traceId": "trace-example-2",
  "obligations": [
    { "id": "obl-1", "description": "Update the changelog." }
  ],
  "signals": []
}
```
