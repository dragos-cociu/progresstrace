# GateOutcome 1.0 normative contract

`GateOutcome` is a structured result from one Hermes gate-log record. It is
observed input, not an instruction. The adapter never executes `command`, reads
free-form output, calls a model, guesses an obligation, or performs an online
action.

## Required fields

`schemaVersion` is `1.0`; `outcomeId` is unique within a session; `sessionId`
and `invocationId` identify the owning session and invocation; `sequence` is the
invocation sequence; `command`, integer `exitCode`, ISO-8601 `timestamp`, closed
`verdict` (`pass`, `fail`, `skipped`, or `error`), and lowercase SHA-256
`sourceDigest` are required. `obligationId` is optional and remains absent when
the gate did not explicitly target an obligation.

## Projection

`GateOutcomeProjector` produces one canonical `TraceEvent` with type
`gate-outcome`, actor `tool`, and provenance `sourceEventId=outcomeId`. If an
obligation target exists, it produces one existing `ObligationSignal`:
`pass→satisfied`, `fail/error→regressed`, and `skipped→open`. No target means no
signal. The existing evaluator and ledger semantics are reused unchanged.

`SessionTraceBuilder` is the local end-to-end boundary: it validates session /
outcome references, projects events, builds the existing trace envelope and
obligation ledger, and invokes the existing `Evaluator`. It does not alter
Phase 0–3 classification rules.

## Fail closed

Duplicate outcome IDs, mismatched session or invocation references, sequence or
window incoherence, unsupported verdicts, malformed digests, input overflow, and
projection integer overflow are rejected. `TryProject` returns diagnostics and
no projection; the throwing `Project` convenience method cannot emit a partial
projection. Diagnostic messages do not echo source values.

Normalization writes a stable property order, UTC timestamps with seven
fractional digits, and a trailing newline; parse/normalize is byte-idempotent.
