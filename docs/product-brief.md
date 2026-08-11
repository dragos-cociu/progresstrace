# ProgressTrace product brief

## Product horizon

ProgressTrace is designed as a 3–5 year product, not as a disposable MVP. The mature product may include trace contracts, ingestion adapters, a deterministic obligation-aware evaluation core, benchmark corpora, local/CI/batch execution, comparison and reporting, process-isolated plugins, and eventually hosted history or collaboration.

Phase 0's contract/conformance foundation, Phase 1's obligation-aware
evaluation, Phase 2a Task A's stop assessment, and Phase 2b Task B's
baseline comparison against an externally authored counterfactual event
budget are all implemented and integrated. ADR-0004 fixed Task B's
architecture and contracts, cleared its independent-review gate, and a
separately authorized task contract shipped `BaselineDefinition`,
`BaselineComparisonResult`, and the `compare` CLI verb. The core product
claim below is therefore implemented in full, subject to ADR-0004's fixed
interpretation ceiling: every `BaselineComparisonResult` value derived from
an authored `eventBudget` is a counterfactual proxy against that authored
budget, never an observed fact, a measurement, and never proof of the
stopped run's own counterfactual future. See the phase objectives below and
`docs/architecture/ADR-0004-baseline-comparison-and-authored-estimate.md`.

## Core product claim

ProgressTrace analyzes an agent trace and explains whether observed activity reduced task obligations, produced useful evidence, stagnated, or regressed. It compares obligation-aware signals with simple baselines and explicitly measures false-halt and late-halt costs. It does not claim to solve the halting problem or universally understand semantic progress.

ProgressTrace analyzes the outputs and evidence that agents and their environment actually generated; it never requires an agent to emit a prescribed ProgressTrace response, and no agent is required to emit ProgressTrace-native JSON. It separates three layers: (1) arbitrary source evidence produced by an agent, tool, or runtime; (2) normalized ProgressTrace input artifacts produced from it by an adapter, harness, controller, operator, or optionally the agent itself, where the provenance a contract carries differs by contract (the trace envelope `1.0` records the source system and version in required document-level `source.name` and `source.version`, but no per-field producer/evidence-basis metadata or adapter-transform manifest; the obligation ledger `1.0` carries no producer/evidence-basis metadata at all; the termination declaration `1.0` separates the declaration producer and evidence basis in `declarationSource`, the first ProgressTrace contract carrier that explicitly separates producer role from evidence basis); and (3) deterministic derived results computed by one normative core. A ProgressTrace canonical contract is an internal interoperability/analysis boundary after capture, not an agent-native wire protocol and not a claimed industry standard; a future external standard would be an ingestion source through an adapter, not the canonical domain model. The deterministic core does not interpret opaque payload text: without explicit obligations and correlated signals there is no universal semantic-progress inference, and `insufficient-evidence` is the honest outcome. Labeling model or adapter inference as inference, rather than observed fact or a direct agent assertion, is a normative rule for new provenance-aware boundaries; ProgressTrace does not yet claim a general ingestion-adapter feature or universal field-level provenance across all existing artifacts, which awaits a future ADR choosing a non-breaking manifest/envelope or a compatible new contract version.

## Target users and dogfooding

The primary initial users are builders of early agent workflows that do not yet have a mature harness; for them an operator or a thin adapter authors the input artifacts by hand or from simple logs. Mature systems may instead integrate through adapters, independent audit, or conformance, and ProgressTrace does not presume to replace their existing harnesses. ProgressTrace's own Claude/Codex/controller/external-reviewer development workflow (see `docs/architecture/ADR-0005-external-review-and-clean-room-reproducibility.md` for the current, provider-agnostic role definitions) is the first planned reference corpus and adapter source — agent text stays arbitrary, task contracts supply obligations, and controller/tool/build/test/CI/verifier/human-gate events supply evidence — but that adapter is planned, not implemented, is not an industry standard, and is not claimed to exist yet. Hosted history, UI, collaboration, and persistent storage remain optional and out of current scope.

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

## Phase 1 objective (implemented and integrated)

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

This phase is implemented and integrated: the two contracts, the
deterministic evaluator, and the `evaluate` CLI verb all ship. Its
obligations and status signals are supplied, explicit structured annotations
consumed by the evaluator — not necessarily agent responses, and never
semantic facts the core infers from opaque payload text; the core returns
`insufficient-evidence` when such explicit, correlated signals are absent.
`ObligationLedger` `1.0` records no producer or evidence-basis metadata
(whether an obligation or signal was, for example, agent-produced,
harness-observed, operator-annotated, or adapter-inferred), so its values are
annotations of unknown producer and evidence provenance unless an external
provenance record exists. This is a known, bounded limitation that Phase 2a's
termination-declaration `declarationSource` does not solve for the ledger.
Before any general ingestion-adapter feature or universal field-level
provenance is claimed, a future ADR must choose a non-breaking adapter
manifest/provenance envelope or a new compatible contract version; that schema
is not designed by ADR-0003 or Phase 2a Task A.

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
