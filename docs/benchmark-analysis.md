# Benchmark MVP analysis

## Status

Analysis of the Phase 3 benchmark MVP, using the deterministic runner and the
20 synthetic cases under `fixtures/benchmarks`.

This document is an evidence review, not a human-oracle evaluation. The
`groundTruth` fields in the fixtures are operator-authored expectations and
must not be described as semantic truth.

## Reproduction

```sh
dotnet build src/ProgressTrace.Benchmarks/ProgressTrace.Benchmarks.csproj \
  --configuration Release --nologo --warnaserror
dotnet run --project src/ProgressTrace.Benchmarks/ProgressTrace.Benchmarks.csproj \
  --configuration Release --no-build -- run --input fixtures/benchmarks \
  --output /tmp/progresstrace-analysis.json
```

Observed result:

- format: `progresstrace-benchmark-mvp-2`
- cases: `20`
- matched: `20`
- mismatched: `0`
- status: `pass`
- Release build: 0 warnings, 0 errors

## Aggregate observations

| Signal | Observed distribution |
|---|---|
| ProgressTrace classification | progress 9; insufficient-evidence 6; stagnation 4; regression 1 |
| `max-turns` | triggered 2; not triggered 18 |
| `exact-repeat` | detected 4; not detected 16 |
| `fuzzy-repeat/cycle` | cycle 2; fuzzy-repeat 4; not detected 14 |

The 20/20 result means the implementation reproduced the expectations encoded
in the fixtures. It does **not** establish accuracy, generalization, human
agreement, or external user value, because the same local cases supply both
the evaluated input and the expected labels.

## Per-case result

| Case | ProgressTrace | max-turns | exact-repeat | fuzzy-repeat/cycle |
|---|---|---:|---|---|
| 01-progress | progress | no | — | — |
| 02-insufficient | insufficient-evidence | no | — | — |
| 03-regression | regression | no | — | — |
| 04-stagnation | stagnation | no | — | fuzzy-repeat |
| 05-max-turns | progress | yes | — | — |
| 06-exact-repeat | stagnation | no | exact-repeat | fuzzy-repeat |
| 07-fuzzy-repeat | progress | no | — | fuzzy-repeat |
| 08-cycle-ab | insufficient-evidence | no | exact-repeat | cycle |
| 09-cycle-abc | insufficient-evidence | no | exact-repeat | cycle |
| 10-no-repeat | progress | no | — | — |
| 11-late-max-turns | progress | yes | — | — |
| 12-exact-tool-repeat | insufficient-evidence | no | exact-repeat | fuzzy-repeat |
| 13-fuzzy-paraphrase | insufficient-evidence | no | — | — |
| 14-regression-recovery | progress | no | — | — |
| 15-mixed-obligations | stagnation | no | — | — |
| 16-empty-signal | insufficient-evidence | no | — | — |
| 17-repeated-progress | progress | no | — | — |
| 18-long-stagnation | stagnation | no | — | — |
| 19-short-max-turns | progress | no | — | — |
| 20-clean-completion | progress | no | — | — |

## What the benchmark demonstrates

1. The Core evaluator and the benchmark baselines are deterministic and
   reproducible over the current corpus.
2. The semantic evaluator can distinguish cases that surface-level detectors
   cannot distinguish by themselves. For example, `05-max-turns` and
   `11-late-max-turns` trigger the turn limit while ProgressTrace still
   classifies the traces as progress.
3. Repeat signals are not equivalent to semantic stagnation. `07-fuzzy-repeat`
   is classified as progress, while `06-exact-repeat` is classified as
   stagnation; `08-cycle-ab` and `09-cycle-abc` are classified as
   insufficient-evidence.
4. Obligation-aware evaluation contributes information in the mixed case:
   `15-mixed-obligations` contains both a progressing and a stagnating
   obligation and receives a trace-level stagnation classification.
5. The cases exercise the intended implementation branches, including
   regression, recovery, empty evidence, repeated progress, cycles, and late
   turn limits.

These are useful engineering and hypothesis-validation results. They are not
comparative accuracy results.

## Limitations and risks

- The corpus has only 20 synthetic cases and is not sampled from external runs.
- Ground truth is authored in the same fixture set; there is no independent
  human oracle or adjudication process.
- The cases are short (1–6 events), so long-horizon behavior and delayed
  recovery are not established.
- The baseline detectors use deliberately narrow algorithms and fixed
  thresholds; their performance cannot be generalized to production detectors.
- There is no measurement of false-halt, late-halt, precision, recall, cost
  savings, or inter-rater agreement.
- The result does not show that ProgressTrace is better than a well-designed
  production baseline; it only shows that the current implementation matches
  its authored fixture expectations.

## Decision

**Continue, narrowly and conditionally.** The benchmark is strong enough to
justify a validation phase, but not enough to justify an OpenTelemetry/Hermes
adapter, product alpha, or claims of general utility.

The next implementation/research step is therefore:

1. expand to an approximately 60-case corpus with deliberately difficult and
   adversarial cases;
2. define an annotation rubric that separates progress, stagnation,
   regression, and insufficient evidence;
3. have two independent evaluators label the cases without seeing the derived
   output;
4. adjudicate disagreements and report agreement;
5. compare ProgressTrace and baselines against that oracle using explicit
   confusion matrices and halt-timing/cost metrics;
6. make a second `continue / narrow / stop` decision.

The real adapter and richer reporting remain deferred until that validation
produces evidence of distinct value.
