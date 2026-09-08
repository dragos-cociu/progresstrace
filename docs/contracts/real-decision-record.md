# RealDecisionRecord 1.0

`RealDecisionRecord` is an externally authored capture of a decision already made by the Hermes/orchestration layer. It is evidence, not a ProgressTrace decision authority.

Required fields: `schemaVersion` (`1.0`), `sessionId`, `taskContractId`, `decision`, `decidedAt`, and opaque provenance `source`.

`decision` is closed: `continued`, `stopped`, `merged`, `rejected`, `escalated`, `abandoned`, or `completed`. `completed` means the externally observed task/session completed successfully. Like every value in this record, it is evidence and does not grant ProgressTrace action authority. `decidedAt` is the captured external ISO-8601 timestamp. The core never interprets `source`.

`completed` must be explicitly authored by the external source. ProgressTrace never infers it from an exit code, termination kind, free text, or missing evidence.

The record is local-only, schema-validated, normalized deterministically, and never causes stopping, escalation, or other action.
