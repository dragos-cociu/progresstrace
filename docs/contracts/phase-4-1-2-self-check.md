# Phase 4.1/4.2 self-check evidence

Date/context: repair attempt 2/2, working tree at `b7186a0` (`main`, ahead 22 of `origin/main`), no push performed.

## Scope and fix

The only source fix made in this repair was removing the two unused using directives reported by `dotnet format` from `src/ProgressTrace.Cli/Program.cs`:

- `using ProgressTrace.Core.Models;`
- `using ProgressTrace.Core.Sessions;`

The first post-fix format run identified the second directive as the remaining IDE0005; it was then removed. No `policy/` path and no Phase 4.0 runner/policy task path was modified.

## Exact verification commands and real results

All commands below were run from `/srv/projects/progresstrace` after the fix.

1. `dotnet build ProgressTrace.slnx --configuration Release --nologo --warnaserror`

Result: exit 0.

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:04.96
```

2. `dotnet run --project tests/ProgressTrace.ConformanceTests/ProgressTrace.ConformanceTests.csproj --configuration Release --nologo --no-build`

Result: exit 0.

```text
PASS: Phase 0 3 valid/10 invalid; Phase 1 8 valid/9 invalid; Phase 2a 10 valid/30 invalid; Phase 2b 6 valid/40 invalid fixtures.
```

3. `python3 -m json.tool contracts/agent-session.schema.json`

Result: exit 0. Output: valid pretty-printed JSON for the `AgentSession` schema (`title: AgentSession`, `schemaVersion: 1.0`).

4. `python3 -m json.tool contracts/gate-outcome.schema.json`

Result: exit 0. Output: valid pretty-printed JSON for the `GateOutcome` schema (`title: GateOutcome`, `schemaVersion: 1.0`).

5. `dotnet format ProgressTrace.slnx --verify-no-changes --no-restore`

Result: exit 0; no output.

6. `git diff --check`

Result: exit 0; no output.

7. `python3 experiments/reference-adapter-v0/run_dogfood.py`

Result: exit 0.

```json
{"cases": "5/5", "cliStages": "15/15", "summary": "experiments/reference-adapter-v0/outputs/summary.json"}
```

8. `python3 -m unittest discover -s experiments/reference-adapter-v0/tests -p 'test_*.py'`

Result: exit 0.

```text
.....
----------------------------------------------------------------------
Ran 5 tests in 0.015s

OK
```

## Fixture counts

- `fixtures/valid`: 3
- `fixtures/invalid`: 10
- `fixtures/obligations/valid`: 8
- `fixtures/obligations/invalid`: 9
- `fixtures/termination/valid`: 10
- `fixtures/termination/invalid`: 30
- `fixtures/baseline/definitions/valid`: 6
- `fixtures/baseline/definitions/invalid`: 40
- `fixtures/session/valid`: 2
- `fixtures/session/invalid`: 2
- `fixtures/gate-outcome/valid`: 3
- `fixtures/gate-outcome/invalid`: 2
- `fixtures/gate-outcome/integration`: 3
- `experiments/reference-adapter-v0/cases`: 5

Dogfood result: 5/5 cases and 15/15 CLI stages successful.

## Changed paths at self-check time

```text
M  src/ProgressTrace.Cli/Program.cs
M  src/ProgressTrace.Core/Adapters/GateOutcomeProjector.cs
M  src/ProgressTrace.Core/Diagnostics/Diagnostic.cs
M  src/ProgressTrace.Core/Models/AgentSession.cs
M  src/ProgressTrace.Core/Validation/AgentSessionValidator.cs
M  src/ProgressTrace.Core/Validation/GateOutcomeValidator.cs
M  tests/ProgressTrace.ConformanceTests/SessionConformance.cs
?? docs/contracts/agent-session.md
?? docs/contracts/gate-outcome.md
?? fixtures/gate-outcome/integration/duplicate-outcomes.json
?? fixtures/gate-outcome/integration/invocation-reference-mismatch.PT509.json
?? fixtures/gate-outcome/integration/session-reference-mismatch.PT508.json
?? fixtures/gate-outcome/invalid/exit-code-overflow.PT515.json
?? fixtures/gate-outcome/valid/repeated-attempt-fail.json
?? fixtures/session/invalid/duplicate-invocation.PT502.json
?? src/ProgressTrace.Core/Sessions/SessionEvaluator.cs
?? src/ProgressTrace.Core/Sessions/SessionTraceBuilder.cs
?? src/ProgressTrace.Core/Validation/SessionContractValidator.cs
```

## Protected-path verification

Command used:

```text
git diff --name-only -- policy tasks/phase-4-0-a-independent-verification-runner.json tasks/phase-4-0-b-change-class-policy.json
```

Result: exit 0 with empty output. No tracked diff was present under `policy/` or in the Phase 4.0 independent-runner/change-class-policy task files.

## Unresolved items

None from the requested verification set. The worktree remains uncommitted and unpushed; integration/commit remains a separate human-gated action.

## Final state

All requested post-fix commands exited 0. Final status: PASS for this self-check; no claim is made that the pre-existing uncommitted worktree is clean or integrated.
