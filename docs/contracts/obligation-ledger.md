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
per `docs/contracts/evaluation-result.md`.

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
