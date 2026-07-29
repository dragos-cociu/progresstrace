# ProgressTrace

ProgressTrace is a contract-first toolkit for capturing, normalizing, evaluating, comparing, and replaying AI-agent execution traces.

Its differentiator is not generic loop detection. ProgressTrace is intended to explain whether an agent reduced explicit task obligations, obtained useful evidence, stagnated, or regressed, while measuring the cost of false halts and late halts against simpler baselines.

## Current phase

**Phase 1: Deterministic Obligation-Aware Evaluation (approved current milestone)**

Phase 0 is integrated: a versioned trace envelope, deterministic validation and normalization, conformance fixtures, and a local .NET 10 CLI. Phase 1 extends that foundation with the obligation-ledger and evaluation-result contracts described below and is the approved current milestone; its implementation is not yet complete. It intentionally does not implement baseline comparison, cost measurement, storage, UI, hosted services, or Python adapters yet.

## Architectural direction

- contract-first;
- modular monolith initially;
- one normative deterministic core;
- .NET 10 for core, CLI, and future backend;
- Python only at adapter/research boundaries where its ecosystem adds value;
- TypeScript only when UI or Node consumers exist;
- out-of-process extensions at untrusted or polyglot boundaries;
- deterministic checks outrank model verdicts;
- no automatic merge or publication.

See `docs/product-brief.md` and `docs/architecture/ADR-0001-contract-first-modular-monolith.md`.

## Phase 1 (approved architecture, not yet implemented)

Phase 1 adds two new versioned contracts, an obligation ledger
(`docs/contracts/obligation-ledger.md`) and an evaluation result
(`docs/contracts/evaluation-result.md`), plus a third CLI verb,
`evaluate <trace-path> <obligation-ledger-path>`, that classifies each
declared obligation and the trace overall as `progress`, `stagnation`,
`regression`, or `insufficient-evidence`. This scope is currently approved
architecture and an executable task specification only; implementation,
fixtures, and CI updates are a separate, later task, and Phase 1 is not
claimed complete until that implementation is integrated. Baseline
comparison and false-halt or late-halt cost remain fully absent from
Phase 1 and are explicitly deferred to Phase 2.

See `docs/architecture/ADR-0002-obligation-and-evaluation-contracts.md`,
`docs/contracts/obligation-ledger.md`, `docs/contracts/evaluation-result.md`,
and `tasks/phase-1-obligation-evaluation.json`.

## Phase 0 usage

The SDK is pinned by `global.json`. Build all three zero-package projects:

```sh
dotnet build ProgressTrace.slnx --configuration Release --nologo --warnaserror
```

Validate a trace. Valid input produces machine-readable JSON and exit code `0`;
semantic-invalid input returns `1`, while malformed JSON, unreadable input, or
bad usage returns `2`.

```sh
dotnet run --project src/ProgressTrace.Cli/ProgressTrace.Cli.csproj \
  --configuration Release -- validate fixtures/valid/minimal-trace.json
```

Normalize a valid trace to canonical JSON on standard output:

```sh
dotnet run --project src/ProgressTrace.Cli/ProgressTrace.Cli.csproj \
  --configuration Release -- normalize fixtures/valid/multi-event-trace.json
```

Run the conformance executable:

```sh
dotnet run --project tests/ProgressTrace.ConformanceTests/ProgressTrace.ConformanceTests.csproj \
  --configuration Release
```
