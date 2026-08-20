# AdvisoryDivergenceReport 1.0 normative contract

`AdvisoryDivergenceReport` is an independent sidecar artifact that records,
per obligation, where a previously generated ledger's declared status
disagrees with an `AdvisoryResult`'s live-computed status. It is diagnostic:
it carries no action, priority, or workflow state, and produces no side
effect. It is not an `EscalationQueue`. See
[ADR-0008](../architecture/ADR-0008-in-flight-advisory-assessment.md) for the
approved decision record.

## Scope

`AdvisoryDivergenceReport` is generated from one `AdvisoryResult` and the
`ObligationLedger` that was supplied as its `ledger-path` input. It compares,
for each obligation present in either source, the ledger-declared `status`
(`ObligationLedger.obligations[].status`) against the advisory-computed
`status` (`AdvisoryResult.obligations[].status`).

## Required fields

`schemaVersion` is `1.0`. `reportId` is deterministically derived, never a
randomly generated UUID (see "Determinism" below).
`sessionId` and `ledgerTraceId` are carried over from the source
`AdvisoryResult`. `advisorySourceDigest` must equal the `sourceDigest` of the
`AdvisoryResult` this report was derived from — this is the report's
provenance link, not an authentication mechanism. `generatedAt` is the fixed
sentinel `1970-01-01T00:00:00.0000000Z` (see "Determinism" below), not a
wall-clock capture. `divergences` is required.

## Divergence is normative per obligation

`divergences` is the normative, required part of this contract. Each entry
reports `obligationId`, `ledgerStatus`, `advisoryStatus` (both drawn from the
existing ledger signal vocabulary: `open`, `in-progress`, `satisfied`,
`regressed`, `abandoned`), and `diverged` (`true` when `ledgerStatus` and
`advisoryStatus` differ). Every obligation present in the ledger or in the
advisory result's `obligations` array appears exactly once.

## Rollup is session-only diagnostic

`rollup` (`totalObligations`, `divergentCount`, `allConverged`) is optional
and diagnostic only. It is a session-level summary, not a binding verdict: it
carries no compatibility guarantee equivalent to `divergences`, and a
conformant implementation may omit it, extend it, or recompute it without a
`schemaVersion` change.

## Fail-closed behavior

A report whose `advisorySourceDigest` does not match the source
`AdvisoryResult`, or whose ledger/advisory obligation sets cannot be
reconciled one-to-one, fails closed with a `PT700`-range diagnostic (see
[ADR-0008](../architecture/ADR-0008-in-flight-advisory-assessment.md)) and no
partial report. Diagnostic messages do not echo source values.

Normalization writes a stable property order and a trailing newline;
parse/normalize is byte-idempotent.

## Determinism

`generatedAt` is fixed to the sentinel `1970-01-01T00:00:00.0000000Z`,
enforced as a schema `const` — the same convention used by
`AdvisoryResult.generatedAt` and by
`LedgerGenerationReport.generatedAt` (`docs/contracts/ledger-generation-report.md`).

`reportId` is the lowercase hex-encoded SHA-256 digest of the concatenation
of the source `AdvisoryResult`'s canonical bytes and the paired
`ObligationLedger`'s canonical bytes, using the same digest algorithm and
canonicalization already used for `sourceDigest` / `advisorySourceDigest` /
`ledgerDigest` elsewhere in this repository (`^[0-9a-f]{64}$`). It is never
a randomly generated UUID: the same `(AdvisoryResult, ObligationLedger)`
pair always yields the same `reportId`, and regenerating a report from
byte-identical inputs always produces a byte-identical
`AdvisoryDivergenceReport`, including `generatedAt` and `reportId`
themselves.

## CLI

```
progresstrace advise --divergence-report \
  --advisory-result-path <path> \
  --ledger-path <path> \
  [--out <path>]
```

Writes a normalized `AdvisoryDivergenceReport` to `--out`, or to stdout if
omitted. Exits non-zero with a `PT700`-range diagnostic on any fail-closed
condition.

## Non-goals

`AdvisoryDivergenceReport` never queues, ranks, or escalates a divergence —
it is not an `EscalationQueue` and has no such vocabulary. Persistence beyond
the invocation that produced it and "shadow mode" generation are explicitly
out of scope and deferred to Hermes / Phase 4.6.

## Security

Read-only over local files: no command execution, no network access, no
credential handling.

## Ownership

Owned by the ProgressTrace core contract maintainers. Changes to
`divergences` or any other required field require a superseding ADR and a
new human gate. `rollup` may evolve under normal review.

## Compatibility

Schema version `1.0`. `divergences` and its required fields follow the same
compatibility posture as other normative contracts in this repository: no
shape or vocabulary change without a new `schemaVersion`. `rollup` may gain
optional fields within `1.0`.
