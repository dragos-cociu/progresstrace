# ProgressTrace

ProgressTrace is a contract-first toolkit for capturing, normalizing, evaluating, comparing, and replaying AI-agent execution traces.

Its differentiator is not generic loop detection. ProgressTrace is intended to explain whether an agent reduced explicit task obligations, obtained useful evidence, stagnated, or regressed, while measuring the cost of false halts and late halts against simpler baselines.

ProgressTrace analyzes the outputs and evidence that agents and their environment actually generated; it never requires an agent to emit a prescribed ProgressTrace response. It keeps three layers separate:

1. **Arbitrary source evidence** produced by an agent, tool, or runtime (model text, tool output, logs, lifecycle events). Opaque payload text is never interpreted semantically by the core.
2. **Normalized ProgressTrace input artifacts** (trace envelope, obligation ledger, termination declaration), produced from layer 1 by an adapter, harness, controller, operator, or optionally the agent itself. Contracts differ in the provenance they carry: the trace envelope `1.0` records the source system and version in required document-level `source.name` and `source.version`, but no per-field producer/evidence-basis metadata or adapter-transform manifest; the obligation ledger `1.0` carries no producer/evidence-basis metadata at all, so its supplied annotations have unknown such provenance unless an external record exists; the termination declaration `1.0` separates the declaration producer and evidence basis in `declarationSource`, the first ProgressTrace contract carrier that explicitly separates producer role from evidence basis. A ProgressTrace canonical contract is an internal interoperability/analysis boundary after capture, not an agent-native wire protocol and not a claimed industry standard.
3. **Deterministic derived results** (evaluation result, stop-assessment result), computed by one normative core purely from layer 2.

No agent is required to emit ProgressTrace-native JSON. A source may already emit a compatible artifact, but that is optional. Without explicit obligations and correlated signals there is no universal semantic-progress inference: `insufficient-evidence` is the honest, deterministic outcome. Labeling adapter or model inference as inference, rather than observed fact or a direct agent assertion, is a normative rule for new provenance-aware boundaries; Phase 2a applies it to the termination declaration's `declarationSource`. ProgressTrace does not yet claim a general ingestion-adapter feature or universal field-level provenance across all existing artifacts; a future ADR must first choose a non-breaking manifest/envelope or a compatible new contract version.

The primary initial users are builders of early agent workflows that do not yet have a mature harness; mature systems may integrate through adapters, independent audit, or conformance rather than replacing their harnesses. ProgressTrace's own agent workflow is a planned future reference adapter/corpus, not yet implemented and not an industry standard. Hosted history, UI, and persistent storage remain optional and out of current scope.

## Current phase

**Phase 2a Task A: Stop assessment implemented. Phase 2b Task B: contract architecture independently reviewed, accepted, and integrated through ADR-0004; implementation not authorized.**

Phase 0, Phase 1, and Phase 2a Task A are implemented: the local .NET 10 CLI validates and normalizes trace envelopes, evaluates obligation ledgers, validates termination declarations, and emits deterministic stop-assessment results through `assess <trace-path> <ledger-path> <termination-declaration-path>`. Phase 2 overall also includes Task B, baseline comparison against an externally authored counterfactual event budget. ADR-0004 fixes Task B's exact contract architecture — `BaselineDefinition`, `BaselineComparisonResult`, a future `compare` CLI verb, and a `PT4xx` diagnostic block — and its independent architecture review and integration are complete, but the ADR authorizes no implementation; Task B implementation remains out of scope pending a separate, Dragos-approved task contract and explicit implementation authorization.

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
whose contract architecture is now independently reviewed and accepted for
implementation planning in ADR-0004 (see "Phase 2b" below); Phase 2 is not complete until
Task B ships or Dragos explicitly amends the product claim. See
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
enum. A future `compare <trace-path> <ledger-path>
<termination-declaration-path> <baseline-definition-path>` CLI verb and a
fresh `PT4xx` diagnostic block (`PT400`-`PT404`) are fixed but not
implemented. This is architecture and contract documentation only: no
schema, code, fixture, or task contract for Task B implementation exists
yet, and none is authorized until a separate, Dragos-approved task contract
is authored and implementation is explicitly authorized. See
`docs/architecture/ADR-0004-baseline-comparison-and-authored-estimate.md`,
`docs/contracts/baseline-definition.md`, and
`docs/contracts/baseline-comparison-result.md`.

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
