# ProgressTrace product brief

## Product horizon

ProgressTrace is designed as a 3–5 year product, not as a disposable MVP. The mature product may include trace contracts, ingestion adapters, a deterministic obligation-aware evaluation core, benchmark corpora, local/CI/batch execution, comparison and reporting, process-isolated plugins, and eventually hosted history or collaboration.

Phase 0's contract/conformance foundation is integrated. Phase 1
obligation-aware evaluation is the approved current milestone, not yet
implemented; see "Phase 1 objective" below.

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

## Phase 1 objective (approved architecture, not yet implemented)

Extend the Phase 0 foundation with two new versioned contracts, an
obligation ledger and an evaluation result, so a trace's explicit
obligations can be classified deterministically as `progress`,
`stagnation`, `regression`, or `insufficient-evidence`:

1. declare explicit, structured obligations and status signals correlated
   to existing trace event ids;
2. validate obligation-ledger structure and cross-document referential
   integrity against a paired trace envelope;
3. compute a deterministic per-obligation classification and a
   highest-precedence trace-level classification;
4. emit a versioned, byte-idempotent evaluation result carrying algorithm
   identity for future comparison;
5. add a third CLI verb, `evaluate <trace-path> <obligation-ledger-path>`,
   mirroring `validate`'s exit-code contract.

This phase is currently approved architecture and semantic documentation
only; implementation is a separate, later task and Phase 1 is not claimed
complete until that implementation is integrated.

## Explicit non-goals for Phase 1

- baseline comparison, explicitly deferred to Phase 2;
- false-halt cost and late-halt cost measurement, explicitly deferred to
  Phase 2;
- multi-trace history, comparison, or reporting;
- any natural-language or semantic interpretation of payload or obligation
  description text;
- obligation-authoring tooling or UI;
- OpenTelemetry or AI-framework ingestion adapters;
- Parquet/DuckDB persistence, web API, or UI;
- Python/TypeScript packages;
- plugin execution or LLM-as-judge.
