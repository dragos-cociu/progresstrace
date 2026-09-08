# ADR-0013: Derived correlation manifest

## Decision

`generate-ledger` accepts an optional `--manifest-output-path <path>`. When it
is present, the ledger, generation report, and CorrelationManifest are replaced
atomically only after all generation and binding validation succeeds. The
four-positional invocation retains its prior output bytes and behavior.

Bindings come exclusively from object entries in `acceptance_commands`.
Legacy argv arrays are accepted but omitted. Each object must explicitly give
`command`, a unique non-blank `gateKey`, and `obligationRef` containing the
closed `sourceField` and valid array `index`. No command text, path, working
directory, transcript, or ordering inference is permitted.

The deterministic manifest follows the external unchanged
`correlation-manifest.schema.json`: schema version
`pt-shadow-correlation-1.0`, the authored task id and requested trace id, and
bindings to the existing generated obligation ids. Every binding uses
`exit-code-zero` and matches envelope field
`result.verification_evidence.gateKey`. Invalid, duplicate, or dangling
bindings fail closed; requesting a manifest from an unbound legacy contract
also fails closed.

## Consequences

No wall-clock or environment value participates in serialization, and no new
dependency is introduced. The external schema remains the single manifest
contract; this change only derives conforming instances.
