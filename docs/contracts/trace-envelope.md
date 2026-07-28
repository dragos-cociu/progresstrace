# Canonical trace envelope

Version `1.0` is the Phase 0 canonical interchange contract. The normative
machine-readable contract is `contracts/trace-envelope.schema.json`; the .NET
Core applies the same structural rules plus the ordering invariants below.

## Fields

- `schemaVersion`: contract major/minor version. Phase 0 accepts only `1.0`.
- `traceId`: non-empty source-independent trace identity.
- `source`: the producing system's non-empty `name` and `version`.
- `createdAt`: UTC or offset ISO 8601 timestamp for envelope creation.
- `events`: trace events in observed order.
- `events[].id`: non-empty identity unique within the envelope.
- `events[].sequence`: non-negative integer, strictly increasing in input.
- `events[].timestamp`: ISO 8601 timestamp, nondecreasing in input.
- `events[].type`: non-empty source-defined event kind.
- `events[].actor`: one of `system`, `user`, `assistant`, `tool`, or `other`.
- `events[].payload`: opaque JSON object. Its members are deliberately
  unconstrained and are preserved as data during normalization.
- `events[].provenance.sourceEventId`: non-empty identity in the source system.

Structural objects reject unknown members so misspellings fail closed. Payload
alone remains open to preserve source-specific data.

## Compatibility and normalization

Contract versions use `major.minor`. A reader accepts only versions it
explicitly supports; this implementation accepts `1.0`. Additive payload
changes do not change the envelope version. Breaking structural or semantic
changes require a new major version and new conformance fixtures.

Normalization orders events by `sequence`, then timestamp instant, then `id`
using ordinal comparison. It emits fixed property order, UTF-8 JSON without
insignificant whitespace, and a trailing newline. Repeated normalization is
byte-identical.

## Diagnostics and redaction

Diagnostics contain a stable code, JSON Pointer, and safe message. They identify
the structural location but do not include payload values or trace content.
Duplicate property names are rejected throughout the document, including inside
payload objects, with `PT004`. Inputs larger than 16 MiB (16,777,216 bytes) are
rejected before JSON parsing with `PT005`.
Trace files are untrusted data: normalization never executes or interprets
payloads. Future upload or model-assisted features must redact before data
leaves the local boundary.
