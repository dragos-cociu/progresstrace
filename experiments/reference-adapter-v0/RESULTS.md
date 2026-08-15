# Reference adapter v0 — dogfood results

## Scope and evidence

This experiment used five concise, redacted projections of real ProgressTrace development runs. Source digests bind each projection to controller-held invocation or closeout metadata, but they do not prove the operator-authored obligations or signals. The adapter generated the four existing canonical inputs and the unchanged .NET CLI executed `evaluate`, `assess`, and `compare` for every case.

Result: **5/5 cases and 15/15 CLI stages completed successfully.** The retained machine-readable evidence is in `outputs/summary.json` and the per-case output directories.

| Case | Evaluation | Stop assessment | Baseline comparison |
| --- | --- | --- | --- |
| Architecture discovery | `progress` | `on-target` | stable attainment; not applicable |
| Task-contract materialization | `progress` | `late-termination` | stable attainment; not applicable |
| Build with path-policy defect | `regression` | `unmet-target-at-termination-present` | path-policy budget exhausted exactly |
| Repair after path-policy defect | `progress` | `late-termination` | stable attainment; not applicable |
| Review, integration, hosted verification | `progress` | `late-termination` | stable attainment; not applicable |

No case produced an authored-estimate false-halt claim.

## What the test demonstrated

1. **The existing canonical pipeline can already analyze projected real workflow evidence.** No core, contract, schema, or CLI change was needed.
2. **The core distinguishes failure and recovery usefully.** The initial build was classified as overall `regression` because implementation progressed while path-policy regressed; the repair returned both obligations to `progress`.
3. **The core stayed honest at termination.** The failed build was reported as an unmet target at a harness-declared stop, not mislabeled as a false halt.
4. **Provenance coherence is enforceable.** The projected harness stop required `harness-lifecycle`; adapter-produced declarations used `adapter-inference`. Observed metadata was not relabeled as an agent assertion.
5. **`late-termination` is sensitive to how events and attainment are projected.** Three successful cases reached their obligations before the terminal event and were therefore classified as late termination. This is deterministic and explainable, but more real cases are needed before treating it as an operationally useful lateness signal.
6. **Baseline comparison was only lightly exercised.** Four cases were inapplicable because of stable attainment; the regression case exhausted its authored budget exactly. This corpus validates integration, not the usefulness or calibration of false-halt cost estimates.

## Actual friction

- Invocation and closeout metadata do not contain enough semantic authority to derive obligations and signals automatically. Each case needed an explicit operator-authored projection.
- Source metadata, obligation authority, inferred termination, and baseline authorship had to remain separate; a source digest alone cannot bridge those roles.
- Converting the small manifest into canonical JSON was straightforward. The hard part was deciding what claims the source legitimately supports.
- The first runner version could leave a previous successful summary after a later failed run. Dogfooding exposed this, and startup cleanup plus a focused regression test now prevent stale success evidence.
- The runner performs one Release CLI build and then uses `--no-build` for the 15 retained invocations.

## Decision after this test

Keep the adapter explicitly **experimental/internal v0**. The evidence does not justify a stable adapter schema, field-level lineage system, general ingestion framework, UI, or compatibility promise.

The next useful increment is smaller than a contract redesign:

1. mechanically extract safe lifecycle metadata and source digests from one controller export;
2. keep obligation/signal authoring explicit unless a trusted task contract supplies them directly;
3. add a few real redacted cases that cover `insufficient-evidence`, abandonment or capture truncation, and a genuinely authored counterfactual baseline;
4. re-evaluate whether repeated manual friction warrants a stable manifest only after those runs.
