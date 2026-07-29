# ADR-0001: Contract-first modular monolith with one normative .NET core

## Status

Provisional, accepted for Phase 0 and subject to falsifiable spikes.

Extended by ADR-0002 (obligation ledger and evaluation-result contracts) for Phase 1; no other line of this decision is modified.

## Context

ProgressTrace needs durable cross-language contracts, deterministic domain semantics, local/CI usability, AI-framework adapters, statistical research workflows, and potentially a hosted service. A single-language decision for every future component would either weaken the normative core or isolate the project from the AI ecosystem. Starting multiple services and runtimes immediately would impose cost before boundaries are proven.

## Decision

1. Start as a contract-first modular monolith.
2. Implement one normative deterministic core in .NET 10.
3. Use the same core from local CLI, future CI integration, and future server workers.
4. Keep trace, progress-contract, evaluation-result, and plugin-manifest contracts language-neutral and versioned.
5. Treat OTLP and framework formats as ingestion sources, not as the canonical ProgressTrace domain model.
6. Add Python only for adapters, benchmark/statistical research, or process-isolated evaluators.
7. Add TypeScript only for UI and Node/web SDK consumers.
8. Prefer out-of-process plugin boundaries over an early stable in-process ABI.
9. Do not extract services until measured operational constraints justify them.

## Phase 0 consequences

- `.NET 10` is pinned for the canonical contract vertical slice.
- JSON Schema 2020-12 is the human-inspectable public contract.
- Runtime validation is deterministic and has no network/database/model calls.
- Tests use conformance fixtures and a zero-third-party-dependency executable.
- UI, hosted topology, storage, adapters, and evaluation semantics remain deferred.

## Evidence that may overturn the runtime recommendation

- unacceptable cross-platform packaging or startup characteristics;
- inability to preserve/stream representative real traces within measured resource thresholds;
- contributor friction that materially prevents adapters or conformance implementations;
- a comparative Python/native spike demonstrating materially lower total maintenance cost without weakening invariants or distribution.
