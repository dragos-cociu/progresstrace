# ADR-0011: Completed Real Decision

- **Status:** Accepted after human gate on 2026-09-07
- **Scope:** ProgressTrace Phase 5

## Context

`RealDecisionRecord 1.0` captures externally authored decisions as evidence. Its original closed vocabulary did not represent a task or session that the external orchestration layer observed to have completed successfully.

## Decision

Add `completed` to the existing `RealDecisionRecord 1.0` decision vocabulary. `completed` means the externally observed task/session completed successfully. It is terminal: it aligns with `stop-recommended` and does not align with `continue`.

The value remains externally authored evidence, not ProgressTrace action authority. ProgressTrace does not infer `completed` from an exit code, termination kind, free text, or missing evidence. Unknown decision values continue to fail closed. The model and deterministic normalizer remain unchanged because `decision` is already represented as an opaque string and canonical serialization is unaffected.

## Consequences

The schema and validators accept seven decision values while retaining schema version `1.0`. Both shadow alignment implementations use the same completed-as-terminal semantics. Existing fixtures and golden outputs remain unchanged; a dedicated fixture and conformance assertions cover validation, schema acceptance, normalization idempotence, and both alignment outcomes.

No automatic decision generation, receiver or Hermes change, active-mode behavior, dependency, or action authority is introduced.
