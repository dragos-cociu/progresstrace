# ProgressTrace product brief

## Product horizon

ProgressTrace is designed as a 3–5 year product, not as a disposable MVP. The mature product may include trace contracts, ingestion adapters, a deterministic obligation-aware evaluation core, benchmark corpora, local/CI/batch execution, comparison and reporting, process-isolated plugins, and eventually hosted history or collaboration.

Only the contract/conformance foundation is in scope now.

## Core product claim

ProgressTrace analyzes an agent trace and explains whether observed activity reduced task obligations, produced useful evidence, stagnated, or regressed. It compares obligation-aware signals with simple baselines and explicitly measures false-halt and late-halt costs. It does not claim to solve the halting problem or universally understand semantic progress.

## Stable early

- semantic vocabulary and invariants;
- versioned public JSON Schemas;
- raw / normalized / evaluated separation;
- provenance and redaction rules;
- deterministic fixtures and expected results;
- result identity and compatibility policy;
- one normative evaluation implementation.

## Replaceable early

- UI framework;
- hosted topology;
- database and query engine;
- cloud provider;
- plugin transport details;
- optimization language;
- report rendering technology.

## Phase 0 objective

Deliver a canonical trace envelope and a local .NET 10 toolchain that can:

1. parse untrusted JSON without network or database access;
2. validate required structure and semantic invariants;
3. normalize event ordering deterministically;
4. preserve source provenance and source-specific payload data;
5. emit stable machine-readable diagnostics;
6. pass versioned valid and invalid conformance fixtures.

## Explicit non-goals for Phase 0

- obligation model;
- progress/stagnation scoring;
- OpenTelemetry or framework adapters;
- Parquet/DuckDB persistence;
- Python/TypeScript packages;
- web API or UI;
- LLM-as-judge;
- plugin execution;
- online agent interruption.
