# ADR-0012: Phase 5 authored gate bindings

## Status

Accepted by human gate on 2026-09-07 for ProgressTrace Phase 5.

## Context

Phase 5 requires that a task contract be the single authored source for the
relationship between an acceptance gate and an obligation. The existing
`acceptance_commands` array is a legacy array of argv arrays, and the existing
ledger generator deliberately excludes it under ADR-0007 Decision 3. That
exclusion must be narrowed for the Phase 5 authored binding shape without
allowing free-text inference.

## Decision

Supersede only ADR-0007 Decision 3's exclusion of `acceptance_commands` for the
following additive Phase 5 shape. ADR-0007 remains unchanged as historical
record for the Phase 4.3 implementation.

A task contract may contain mixed acceptance-command entries:

```json
"acceptance_commands": [
  {
    "command": ["dotnet", "test", "..."],
    "gateKey": "unit-tests",
    "obligationRef": {"sourceField": "deliverables", "index": 0}
  },
  ["dotnet", "build", "..."]
]
```

A bound object must explicitly contain a non-empty `gateKey` and an
`obligationRef`. `sourceField` is one of `deliverables`,
`required_contract_decisions`, or `required_invariants`; `index` is the
zero-based index in that source array. `gateKey` is unique within the task
contract and is copied verbatim to derived correlation data and explicit
Hermes telemetry.

Bindings are never inferred from command text, `cwd`, path, transcript, or
array order. Legacy argv-array entries remain valid, manual, and unbound; they
are never automatically promoted to bound or automatic coverage. Malformed
bindings fail closed rather than being silently downgraded to legacy form.

This ADR defines authoring semantics only. It does not add a new Core contract,
change `ObligationLedger 1.0`, activate runtime action, or create a persistent
Hermes hook.

## Consequences

P0.2 may read explicit binding objects while preserving all existing task
contracts and the four-argument `generate-ledger` behavior. The manifest output
is a later derived artifact, not a second authored mapping. Existing Phase 4
contracts remain valid because bare argv arrays continue to be accepted.
