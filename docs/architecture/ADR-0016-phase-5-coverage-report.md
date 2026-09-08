# ADR-0016: Deterministic Phase 5 coverage report

## Decision

`CoverageReportGenerator` is a pure projection of an explicit
`CorrelationManifest`, `ObligationLedger`, and collection of valid
`GateOutcome` records. It emits one entry per obligation in ledger order.
Bindings and gate keys come only from the manifest. An obligation is
`automatic` when it is bound and has at least one distinct outcome identity,
`insufficient-evidence` when bound without such evidence, and `manual` when
unbound. Evidence is matched only by `obligationId`; command text, ordering,
exit code, verdict, and other content never infer identity or coverage.

Gate-key order is manifest order. Duplicate gate keys or outcome identities,
dangling bindings, unknown outcome obligation identities, null or invalid
inputs, trace disagreement, and a manifest task identity inconsistent with the
generated ledger obligation namespace fail closed without report bytes.
Outcomes with no obligation or with a known but unbound obligation are ignored
for counts, retained as diagnostics, and do not prevent the report from being
produced.

The JSON projection has schema version `pt-coverage-report-1.0`, uses a fixed
property order and UTF-8 representation, and ends with one newline. It has no
clock, random, filesystem, network, persistence, or CLI dependency.

## Consequences

Coverage is auditable from explicit identities and equal inputs are
byte-identical. This is a generation result rather than a new Core schema,
model, validator, or normalizer. Existing contracts and active decision paths
remain unchanged.
