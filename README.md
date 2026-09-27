# ProgressTrace

ProgressTrace is a contract-first toolkit for capturing, normalizing, evaluating, comparing, and replaying AI-agent execution traces.

Its differentiator is not generic loop detection. ProgressTrace is intended to explain whether an agent reduced explicit task obligations, obtained useful evidence, stagnated, or regressed, while measuring the cost of false halts and late halts against simpler baselines.

ProgressTrace analyzes the outputs and evidence that agents and their environment actually generated; it never requires an agent to emit a prescribed ProgressTrace response. It keeps three layers separate:

1. **Arbitrary source evidence** produced by an agent, tool, or runtime (model text, tool output, logs, lifecycle events). Opaque payload text is never interpreted semantically by the core.
2. **Normalized ProgressTrace input artifacts** (trace envelope, obligation ledger, termination declaration), produced from layer 1 by an adapter, harness, controller, operator, or optionally the agent itself. Contracts differ in the provenance they carry: the trace envelope `1.0` records the source system and version in required document-level `source.name` and `source.version`, but no per-field producer/evidence-basis metadata or adapter-transform manifest; the obligation ledger `1.0` carries no producer/evidence-basis metadata at all, so its supplied annotations have unknown such provenance unless an external record exists; the termination declaration `1.0` separates the declaration producer and evidence basis in `declarationSource`, the first ProgressTrace contract carrier that explicitly separates producer role from evidence basis. A ProgressTrace canonical contract is an internal interoperability/analysis boundary after capture, not an agent-native wire protocol and not a claimed industry standard.
3. **Deterministic derived results** (evaluation result, stop-assessment result), computed by one normative core purely from layer 2.

No agent is required to emit ProgressTrace-native JSON. A source may already emit a compatible artifact, but that is optional. Without explicit obligations and correlated signals there is no universal semantic-progress inference: `insufficient-evidence` is the honest, deterministic outcome. Labeling adapter or model inference as inference, rather than observed fact or a direct agent assertion, is a normative rule for new provenance-aware boundaries; Phase 2a applies it to the termination declaration's `declarationSource`. ProgressTrace does not yet claim a general ingestion-adapter feature or universal field-level provenance across all existing artifacts; a future ADR must first choose a non-breaking manifest/envelope or a compatible new contract version.

The primary initial users are builders of early agent workflows that do not yet have a mature harness; mature systems may integrate through adapters, independent audit, or conformance rather than replacing their harnesses. The later `reference-adapter-v0` dogfooding experiment is intentionally experimental/internal and is not a stable adapter contract or industry standard. Hosted history, UI, and persistent storage remain optional and out of current scope.

## Status: v1.0.0 — frozen

**ProgressTrace is frozen at `v1.0.0` as a personal tool** (decision B,
`docs/architecture/ADR-0017-phase-5-decision-freeze.md`). No further phases are planned;
reopening any direction requires a new question, a decision date and numeric thresholds
fixed in advance.

The decision follows a Phase 5 adversarial series of three real development sessions on an
external project, measured outside the orchestrating agent against a threshold committed
before the first session. Result: **0 human-confirmed cases in which ProgressTrace reported
something the agent's own report and the exit codes did not.** The full account is in
`docs/phase-5-final-report.md`.

`v1.0.0` includes one bounded bugfix found during the series (`advise`, F5/F1): status is
derived from the latest outcome of each gate, so a failing gate can no longer be masked by a
different gate passing later, and a current unrecovered failure is classified as
`failed-attempt` instead of `recovery-after-failed-attempt`.

### Known limitations

- **The verdict is at most as good as the gates that feed it.** ProgressTrace aggregates gate
  outcomes; it does not see what a gate does not check (in the series, every real defect was
  visual and escaped all automated layers).
- **Obligations without a gate pin the top-level result (F2).** Any obligation with no gate
  keeps the top-level `classification` and `recommendation` at `insufficient-evidence`; read
  the per-obligation `status` instead.
- **`PT703` (insufficient evidence) exits `0`.** It is a valid advisory, not a process error;
  a harness must read `classification`/`status` from `advisory.json`, not the exit code.
- **Gate identity in `advise` is the outcome `command` string.** Two different gates with an
  identical command are treated as one gate.
- **CLI ergonomics (F4).** The CLI does not derive `session.json` (it must be authored or
  produced by external tooling), the coverage report generator is not exposed as a command,
  `RealDecisionRecord` can only be validated through `shadow-summarize`, and the top-level
  usage message does not list `advise`, `budget` or `shadow-summarize`.
- **`SessionEvaluator`** (not used by `advise`) counts any failure alongside a pass as
  `recovery-after-failed-attempt`, regardless of order.

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

## Phase 4.5 observed budget

The read-only `budget` operation combines one validated `AgentSession`, one
observed `TokenUsage` sidecar, and one `ObligationLedger` into deterministic
`ObservedBudget 1.0` output. It reports per-obligation invocation counts,
elapsed milliseconds, ledger status, and observed token totals. It does not
reinterpret authored `eventBudget` estimates, infer costs, persist state, call
models, or stop sessions.

```sh
dotnet run --project src/ProgressTrace.Cli/ProgressTrace.Cli.csproj \
  --configuration Release -- budget \
  --session-path fixtures/budget/valid/session.json \
  --token-usage-path fixtures/budget/valid/token-usage-complete.json \
  --ledger-path fixtures/budget/valid/ledger.json
```

The verb accepts exactly the three required input flags and optional `--out`.
Complete observed token evidence produces normal output; missing token records
produce PT806 diagnostics on stderr without blocking output or changing the
zero exit code. PT800–PT805 fail closed. Elapsed durations are summed at native
`TimeSpan` precision and truncated only after the final sum; aggregate token
overflow is checked. See `docs/architecture/ADR-0009-observed-budget.md`,
`docs/contracts/token-usage.md`, `docs/contracts/observed-budget.md`,
`tasks/phase-4-5-observed-budget.json`, and
`tasks/phase-4-5-observed-budget-implementation.json`.

## Phase 4.6 shadow summary

The additive `shadow-summarize` operation combines an ordered local snapshot
manifest of already-generated `AdvisoryResult` files with an optional external
`RealDecisionRecord` and produces a deterministic `ShadowSessionSummary`.
It is diagnostic-only: it never stops, interrupts, escalates, or changes a live
Hermes decision. The external Hermes hook and persistence directory remain
separate tooling responsibilities.

```sh
dotnet run --project src/ProgressTrace.Cli/ProgressTrace.Cli.csproj \\
  --configuration Release -- shadow-summarize \\
  --snapshot-manifest-path fixtures/shadow-summary/valid/manifest.json \\
  --real-decision-path fixtures/shadow-summary/valid/real-decision.json
```

PT900–PT904 fail closed; PT905 is informational and produces a complete summary
with no real decision and `aligned: null`. See `docs/architecture/ADR-0010-shadow-mode-integration.md`,
`docs/contracts/real-decision-record.md`, `docs/contracts/shadow-session-summary.md`,
and `tasks/phase-4-6-shadow-mode-implementation.json`.

## Core CLI usage

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

## Phase 4.4 in-flight advisory

The read-only in-flight advisory and its versioned contracts are implemented on
this branch. The CLI accepts explicit local input paths and emits deterministic
JSON; it does not persist state, access the network, call a model, interrupt a
session, or modify canonical session-boundary evaluation.

Generate an advisory result:

```sh
dotnet run --project src/ProgressTrace.Cli/ProgressTrace.Cli.csproj \
  --configuration Release -- advise \
  --session-path fixtures/advisory/valid/session.json \
  --ledger-path fixtures/advisory/valid/ledger.json \
  --gate-outcomes-array-path fixtures/advisory/valid/outcomes-continue.json
```

Generate a per-obligation divergence report:

```sh
dotnet run --project src/ProgressTrace.Cli/ProgressTrace.Cli.csproj \
  --configuration Release -- advise --divergence-report \
  --advisory-result-path fixtures/advisory/golden/advisory-stop.json \
  --ledger-path fixtures/advisory/valid/ledger.json
```

The advisory uses the existing evaluator classification and adds the closed
recommendation vocabulary `continue`, `stop-recommended`, and
`insufficient-evidence`. The divergence report is an independent sidecar and
its per-obligation entries are normative; any session-level rollup is
informational only.

See `docs/architecture/ADR-0008-in-flight-advisory-assessment.md`,
`docs/contracts/advisory-result.md`,
`docs/contracts/advisory-divergence-report.md`,
`tasks/phase-4-4-in-flight-advisory.json`, and
`tasks/phase-4-4-inflight-advisory-implementation.json`.

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
