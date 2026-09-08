# ADR-0014: Derived AgentSession projection

## Decision

An `AgentSession` may be projected purely from explicit `sessionId`,
`taskContractId`, and `traceId` values, validated `GateOutcome` records, and a
known `ObligationLedger`. Outcomes are ordered by sequence alone. Each outcome
becomes one invocation with its invocation identity and sequence preserved,
attempt `1`, the explicit trace identity, its optional obligation as a
zero-or-one element list, and its outcome timestamp as both invocation bounds.

Projection fails closed with no session or bytes for absent identity, null
required outcome data, ambiguous outcome or invocation identity, duplicate
sequence, cross-session data, or an obligation absent from the known ledger.
Obligations without outcomes are intentionally accepted for later coverage
classification. Successful output is serialized by the existing
`AgentSessionNormalizer` and reparsed by the existing
`AgentSessionValidator`; validator diagnostics are returned unchanged.

## Consequences

The projection has no clock, random, environment, filesystem, network, CLI, or
dependency input. Equal explicit inputs therefore produce byte-identical
canonical session data. The frozen models, schemas, normalizers, and validators
remain unchanged.
