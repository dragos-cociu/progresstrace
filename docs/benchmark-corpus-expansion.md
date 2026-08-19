# Benchmark corpus expansion

## Scope

The original 20-case Phase 3 MVP corpus is retained unchanged. Forty
additional cases were added under `fixtures/benchmarks`, bringing the local
runner corpus to 60 cases.

The added cases are two metamorphic variants of each original case. They vary
trace and event identities and add controlled evidence-text variants while
preserving the intended obligation-status sequence and baseline branch. This
checks that behavior is not accidentally coupled to the original identifiers
or exact wording.

## What this does not provide

The 40 variants are not independent samples from production and their copied
`groundTruth` fields remain authored expectations. The 60/60 result therefore
means deterministic implementation plus metamorphic fixture consistency; it is
not a human-oracle accuracy result.

Independent annotation must follow `docs/benchmark-annotation-rubric.md`.
Until two evaluators and an adjudicator produce an oracle, do not claim
precision, recall, false-halt rate, late-halt cost, generalization, or user
value from this corpus alone.

## Reproduction

```sh
dotnet build ProgressTrace.slnx --configuration Release --nologo --warnaserror
dotnet run --project src/ProgressTrace.Benchmarks/ProgressTrace.Benchmarks.csproj \
  --configuration Release --no-build -- run --input fixtures/benchmarks \
  --output /tmp/progresstrace-60.json
```

Expected summary:

```text
caseCount: 60
matchedCaseCount: 60
mismatchedCaseCount: 0
status: pass
```
