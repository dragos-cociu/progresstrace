# Phase 4 verification control plane

## Authority

A claim emitted by the agent that produced a change is untrusted data with
classification `unverified-claim`. It cannot satisfy a gate. Only a complete
`VerificationReport` emitted by the independent runner can satisfy the
verification gate. Missing or partial reports fail closed.

## Contracts

- `VerificationReport` 1.0: independent checks, commit/tree identity, command
  set digest, and output digests.
- `ChangeClassification` 1.0: deterministic class A/B/C from the diff against
  `main`; unmatched paths and normative changes are class C.
- `InvariantResult` 1.0: reproducible property results with fixed seeds and
  minimal reproducers.
- `EscalationQueue` 1.0: bounded human-relevant exceptions only.

The repository implementation is `verification/ptverify.py`. The production
runner must be launched from an external clean-checkout wrapper; the evaluated
agent must not be able to modify the wrapper, CI definition, or policy while a
report is being produced.

## Safe boundary

The classifier's A/B effects are recommendations. This repository still
requires its existing human integration controls; no agent process may merge,
publish, or execute untrusted extracted content automatically.
