# ProgressTrace

ProgressTrace is a contract-first toolkit for capturing, normalizing, evaluating, comparing, and replaying AI-agent execution traces.

Its differentiator is not generic loop detection. ProgressTrace is intended to explain whether an agent reduced explicit task obligations, obtained useful evidence, stagnated, or regressed, while measuring the cost of false halts and late halts against simpler baselines.

## Current phase

**Phase 2a: Stop assessment (approved architecture, not yet implemented)**

Phase 0 and Phase 1 are integrated: a versioned trace envelope, deterministic validation and normalization, obligation-ledger validation, and evaluation-result output, all via a local .NET 10 CLI. Phase 2a adds a versioned termination declaration and a deterministic stop-assessment result, computed from the existing trace and obligation-ledger contracts, plus a fourth CLI verb, `assess <trace-path> <ledger-path> <termination-declaration-path>`. Phase 2a is approved architecture and contract documentation only; implementation is a separate, later task, and Phase 2a is not integrated until that implementation lands. Phase 2 overall also includes Task B (baseline comparison and false-halt cost), which is architecturally decided but remains fully unauthored pending a further ADR-0004; Phase 2 is not complete until both tasks ship or Dragos explicitly amends the product claim.

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

## Phase 1

Phase 1 adds two new versioned contracts, an obligation ledger
(`docs/contracts/obligation-ledger.md`) and an evaluation result
(`docs/contracts/evaluation-result.md`), plus a third CLI verb,
`evaluate <trace-path> <obligation-ledger-path>`, that classifies each
declared obligation and the trace overall as `progress`, `stagnation`,
`regression`, or `insufficient-evidence`. Baseline
comparison and false-halt or late-halt cost remain fully absent from
Phase 1 and are explicitly deferred to Phase 2.

See `docs/architecture/ADR-0002-obligation-and-evaluation-contracts.md`,
`docs/contracts/obligation-ledger.md`, `docs/contracts/evaluation-result.md`,
and `tasks/phase-1-obligation-evaluation.json`.

## Phase 2a

Phase 2a adds a new termination declaration
(`docs/contracts/termination-declaration.md`), an authored attestation of
how and where a trace's observation ended, and a new stop-assessment result
(`docs/contracts/stop-assessment-result.md`) that deterministically reports,
per obligation, whether it was stably attained through termination and, for
the trace overall, how much later than necessary termination occurred, when
termination is attested and every obligation is stable. Task A never claims
a false halt: an unmet target is reported as the purely observational
`unmet-target-at-termination`, never as a counterfactual claim. A fourth CLI
verb, `assess <trace-path> <ledger-path> <termination-declaration-path>`,
extends `validate`/`normalize`/`evaluate`'s exit-code contract unchanged.
Phase 2 also includes Task B (baseline comparison and false-halt cost),
architecturally decided but not yet authored; Phase 2 is not complete until
Task B ships or Dragos explicitly amends the product claim. See
`docs/architecture/ADR-0003-phase-2-architecture-and-stop-assessment.md`,
`docs/contracts/termination-declaration.md`,
`docs/contracts/stop-assessment-result.md`, and
`tasks/phase-2a-stop-assessment.json`.

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

Evaluate a trace and obligation ledger to a canonical evaluation result:

```sh
dotnet run --project src/ProgressTrace.Cli/ProgressTrace.Cli.csproj \
  --configuration Release -- evaluate fixtures/valid/multi-event-trace.json \
  fixtures/obligations/valid/partial-progress-ledger.json
```

Run the conformance executable:

```sh
dotnet run --project tests/ProgressTrace.ConformanceTests/ProgressTrace.ConformanceTests.csproj \
  --configuration Release
```
