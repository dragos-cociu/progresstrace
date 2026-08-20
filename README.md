# ProgressTrace

ProgressTrace is a contract-first toolkit for capturing, normalizing, evaluating, comparing, and replaying AI-agent execution traces.

Its differentiator is not generic loop detection. ProgressTrace is intended to explain whether an agent reduced explicit task obligations, obtained useful evidence, stagnated, or regressed, while measuring the cost of false halts and late halts against simpler baselines.

ProgressTrace analyzes the outputs and evidence that agents and their environment actually generated; it never requires an agent to emit a prescribed ProgressTrace response. It keeps three layers separate:

1. **Arbitrary source evidence** produced by an agent, tool, or runtime (model text, tool output, logs, lifecycle events). Opaque payload text is never interpreted semantically by the core.
2. **Normalized ProgressTrace input artifacts** (trace envelope, obligation ledger, termination declaration), produced from layer 1 by an adapter, harness, controller, operator, or optionally the agent itself. Contracts differ in the provenance they carry: the trace envelope `1.0` records the source system and version in required document-level `source.name` and `source.version`, but no per-field producer/evidence-basis metadata or adapter-transform manifest; the obligation ledger `1.0` carries no producer/evidence-basis metadata at all, so its supplied annotations have unknown such provenance unless an external record exists; the termination declaration `1.0` separates the declaration producer and evidence basis in `declarationSource`, the first ProgressTrace contract carrier that explicitly separates producer role from evidence basis. A ProgressTrace canonical contract is an internal interoperability/analysis boundary after capture, not an agent-native wire protocol and not a claimed industry standard.
3. **Deterministic derived results** (evaluation result, stop-assessment result), computed by one normative core purely from layer 2.

No agent is required to emit ProgressTrace-native JSON. A source may already emit a compatible artifact, but that is optional. Without explicit obligations and correlated signals there is no universal semantic-progress inference: `insufficient-evidence` is the honest, deterministic outcome. Labeling adapter or model inference as inference, rather than observed fact or a direct agent assertion, is a normative rule for new provenance-aware boundaries; Phase 2a applies it to the termination declaration's `declarationSource`. ProgressTrace does not yet claim a general ingestion-adapter feature or universal field-level provenance across all existing artifacts; a future ADR must first choose a non-breaking manifest/envelope or a compatible new contract version.

The primary initial users are builders of early agent workflows that do not yet have a mature harness; mature systems may integrate through adapters, independent audit, or conformance rather than replacing their harnesses. The later `reference-adapter-v0` dogfooding experiment is intentionally experimental/internal and is not a stable adapter contract or industry standard. Hosted history, UI, and persistent storage remain optional and out of current scope.

## Current phase

**Phase 4: Hermes-native operational dogfooding — directive accepted, Task 4.1/4.2 not yet implemented.**

Phase 0, Phase 1, Phase 2a Task A, Phase 2b Task B, benchmarkul MVP, validarea authored și extinderea la 60 de cazuri sunt implementate. Nucleul .NET 10 validează și normalizează trace-uri, evaluează obligații, face stop assessment și compară baseline-uri authored. Cele 60 de cazuri rămân exclusiv regression suite în CI; `fuzzyRepeatCycle` este explorator, iar raportarea operațională implicită se bazează pe `maxTurns` și `exactRepeat`.

Vertical slice-ul experimental Hermes a trecut pe 5 proiecții reale/redactate și 15 etape CLI. Phase 4 restrânge ingestia la gate log-ul Hermes: obligațiile vor proveni din task contracts, semnalele din verdicte deterministe, iar bugetele operaționale din consum observat. Nu se promite compatibilitate universală, OpenTelemetry, adaptor generic sau interpretare conversațională în core.

Următorul increment este Task `4.3` — generatorul de ledger din task contract,
cu trasabilitate către clauzele sursă și raport de coverage.

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

See `docs/product-brief.md`, `docs/architecture/ADR-0001-contract-first-modular-monolith.md`,
and `docs/architecture/ADR-0005-external-review-and-clean-room-reproducibility.md` for the
current deterministic-verifier/external-reviewer roles and clean-room reproduction policy
behind the last two points above.

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
(`docs/contracts/termination-declaration.md`), a supplied attestation of
how and where a trace's observation ended plus a required `declarationSource`
object recording which role produced it (`agent`, `harness`, `operator`, or
`adapter`) and on what evidence, so no agent is required to emit it and
producer provenance stays separate from the termination cause, and a new
stop-assessment result
(`docs/contracts/stop-assessment-result.md`) that deterministically reports,
per obligation, whether it was stably attained through termination and, for
the trace overall, how much later than necessary termination occurred, when
termination is attested and every obligation is stable. Task A never claims
a false halt: an unmet target is reported as `unmet-target-at-termination`, a
single-trace, non-counterfactual statement computed from supplied trace and
ledger artifacts, never as a counterfactual claim. A fourth CLI
verb, `assess <trace-path> <ledger-path> <termination-declaration-path>`,
extends `validate`/`normalize`/`evaluate`'s exit-code contract unchanged.
Phase 2 also includes Task B (baseline comparison and false-halt cost),
whose contract architecture was independently reviewed and accepted for
implementation planning in ADR-0004, and which has since been implemented
and integrated as Phase 2b (see "Phase 2b" below); Phase 2 is complete
under that ADR's authored-estimate interpretation ceiling. See
`docs/architecture/ADR-0003-phase-2-architecture-and-stop-assessment.md`,
`docs/contracts/termination-declaration.md`,
`docs/contracts/stop-assessment-result.md`, and
`tasks/phase-2a-stop-assessment.json`.

## Phase 2b

Phase 2b, via ADR-0004 (status: Accepted and integrated),
fixes Task B's contract architecture: a new `BaselineDefinition`
(`docs/contracts/baseline-definition.md`), an authored, per-obligation
`eventBudget` — an externally authored counterfactual event-slot count,
always labeled `authored-estimate`, never an observed fact or proof of the
stopped run's own counterfactual future — and a new
`BaselineComparisonResult` (`docs/contracts/baseline-comparison-result.md`)
that recomputes Task A's stop-assessment context internally (never from a
precomputed file) and compares each unmet obligation's observed event count
against its authored budget, classified into a closed `applicability`
enum. The `compare <trace-path> <ledger-path>
<termination-declaration-path> <baseline-definition-path>` CLI verb and a
fresh `PT4xx` diagnostic block (`PT400`-`PT404`) are implemented. See
`docs/architecture/ADR-0004-baseline-comparison-and-authored-estimate.md`,
`docs/contracts/baseline-definition.md`, and
`docs/contracts/baseline-comparison-result.md`.

### Phase 2b usage

Compare a validated trace, ledger, termination declaration, and baseline definition:

```sh
dotnet run --project src/ProgressTrace.Cli/ProgressTrace.Cli.csproj --configuration Release -- compare fixtures/valid/multi-event-trace.json fixtures/termination/ledgers/valid/mixed-rollup-ledger.json fixtures/termination/valid/mixed-rollup-declaration.json fixtures/baseline/definitions/valid/unused-budget-baseline.json
```

## Phase 0 usage

## Phase 4.3 ledger generation

Generate an `ObligationLedger` 1.0 document and its independently versioned
`LedgerGenerationReport` sidecar from the task contract's structured source
arrays. The trace id is always explicit, and both files are written only after
the complete generation and ledger Phase A validation succeed:

```sh
dotnet run --project src/ProgressTrace.Cli/ProgressTrace.Cli.csproj \
  --configuration Release -- generate-ledger \
  fixtures/ledger-generation/valid/both-categories.json synthetic-trace \
  /tmp/generated-ledger.json /tmp/ledger-generation-report.json
```

The verb accepts exactly four positional arguments after `generate-ledger` and
returns `0` on success, `1` for semantic rejection, and `2` for usage or input
failures. It derives obligations only from `deliverables`,
`required_contract_decisions`, and `required_invariants`; it never derives from
`objective`, `acceptance_commands`, or other task-contract properties.

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

Assess a trace, obligation ledger, and termination declaration to a canonical
stop-assessment result:

```sh
dotnet run --project src/ProgressTrace.Cli/ProgressTrace.Cli.csproj \
  --configuration Release -- assess fixtures/valid/multi-event-trace.json \
  fixtures/termination/ledgers/valid/on-target-ledger.json \
  fixtures/termination/valid/on-target-declaration.json
```

Run the conformance executable:

```sh
dotnet run --project tests/ProgressTrace.ConformanceTests/ProgressTrace.ConformanceTests.csproj \
  --configuration Release
```

## Phase 3 benchmark MVP

The local benchmark corpus contains 20 synthetic cases. Each case keeps a
canonical trace envelope and obligation ledger under `fixtures/benchmarks`.
The benchmark runner validates both with Core, runs the existing evaluator,
and reports benchmark-only max-turns, exact-repeat, and fuzzy-repeat/cycle
observations as stable JSON. The detectors are not normative Core semantics.
Each fixture also contains explicit operator-authored `groundTruth` metadata
for the expected evaluator classification and baseline outcomes. These labels
are benchmark expectations for validation, not semantic truth and not a human
oracle. Output keeps those authored expectations separate from the derived
observations and reports per-case and aggregate match status.

Build and run it with:

```sh
dotnet build src/ProgressTrace.Benchmarks/ProgressTrace.Benchmarks.csproj \
  --configuration Release --nologo --warnaserror
dotnet run --project src/ProgressTrace.Benchmarks/ProgressTrace.Benchmarks.csproj \
  --configuration Release --no-build -- run --input fixtures/benchmarks \
  --output /tmp/progresstrace-benchmark.json
```

The runner sorts case files using ordinal ordering and writes compact UTF-8
JSON with a trailing newline, so repeated runs over the same local corpus are
byte-identical. A malformed expectation or mismatch exits non-zero. The
benchmark test executable checks complete case coverage, all expectation and
detector branches, mismatch handling, evaluator classifications, ordering, and
in-process determinism.
