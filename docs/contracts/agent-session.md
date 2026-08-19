# AgentSession 1.0 normative contract

`AgentSession` is the explicit, local boundary for one task session. It does not
parse conversation text, infer obligations, persist state, interrupt an agent,
or request a model judgment.

## Required fields

- `schemaVersion`: exactly `1.0`.
- `sessionId`: non-empty stable session identity.
- `taskContractId`: non-empty task-contract identity.
- `invocations`: ordered invocation records.

Each invocation has a unique `invocationId`, non-negative strictly increasing
`sequence`, positive `attempt`, non-empty `traceId`, an explicit (possibly
empty) `obligationIds` array, and an ISO-8601 `startedAt`/`endedAt` window.
Filesystem order is never semantic; normalization orders by sequence and then
identifier and sorts obligation identifiers.

An empty `obligationIds` array means that the invocation explicitly carries no
obligation target. No obligation is inferred from payload or command text.

## Cross-contract boundary

A `GateOutcome` is admissible only when its `sessionId` matches, its
`invocationId` names an invocation, its `sequence` equals that invocation's
sequence, its timestamp is within the invocation window, and an optional
`obligationId` is declared by that invocation. Outcome identifiers are unique
within the session. Any failure is insufficient evidence and is never progress.

## Session evaluation

`SessionEvaluator` consumes only explicit gate outcomes. Its closed vocabulary is:

- `progress` — at least one targeted obligation has an explicit passing outcome;
- `recovery-after-failed-attempt` — a passing outcome follows failed/error evidence;
- `repeated-attempt-without-obligation-advancement` — a retry attempt is explicit,
  but no targeted obligation has a passing outcome;
- `insufficient-evidence` — evidence is absent, incomplete, or incoherent.

The returned `stopRequested` value is always `false`. This boundary reports an
observation; it never performs automatic stopping or interruption.

## Fail-closed behavior

Malformed JSON, unknown/duplicate properties, invalid references, overflow, and
unsupported values produce stable diagnostics and no trusted evaluation result.
Normalization is deterministic and byte-idempotent.
