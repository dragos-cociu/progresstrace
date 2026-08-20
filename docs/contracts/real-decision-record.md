# RealDecisionRecord 1.0

`RealDecisionRecord` is an externally authored capture of a decision already made by the Hermes/orchestration layer. It is evidence, not a ProgressTrace decision authority.

Required fields: `schemaVersion` (`1.0`), `sessionId`, `taskContractId`, `decision`, `decidedAt`, and opaque provenance `source`.

`decision` is closed: `continued`, `stopped`, `merged`, `rejected`, `escalated`, or `abandoned`. `decidedAt` is the captured external ISO-8601 timestamp. The core never interprets `source`.

The record is local-only, schema-validated, normalized deterministically, and never causes stopping, escalation, or other action.
