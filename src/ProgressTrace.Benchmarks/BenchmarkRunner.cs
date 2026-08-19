using System.Text;
using System.Text.Json;
using ProgressTrace.Core.Evaluation;
using ProgressTrace.Core.Validation;

namespace ProgressTrace.Benchmarks;

public static class BenchmarkRunner
{
    public static BenchmarkRun Run(string inputDirectory)
    {
        var cases = Directory.EnumerateFiles(inputDirectory, "*.json")
            .Order(StringComparer.Ordinal)
            .Select(Load)
            .Select(Evaluate)
            .ToList();
        var mismatchedCaseCount = cases.Count(item => item.Status.Overall != "match");
        return new(
            "progresstrace-benchmark-mvp-2",
            cases.Count,
            new(cases.Count, cases.Count - mismatchedCaseCount, mismatchedCaseCount,
                mismatchedCaseCount == 0 ? "pass" : "fail"),
            cases);
    }

    private static LoadedBenchmarkCase Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        var caseId = RequiredString(root, "caseId", path);
        var maxTurns = root.GetProperty("maxTurns").GetInt32();
        if (maxTurns < 1) throw new InvalidDataException($"Benchmark case {caseId} has an invalid maxTurns value.");
        var groundTruth = ReadGroundTruth(root, caseId, path);

        var traceBytes = Encoding.UTF8.GetBytes(root.GetProperty("trace").GetRawText());
        var traceResult = TraceValidator.ParseAndValidate(traceBytes);
        if (!traceResult.IsValid)
            throw new InvalidDataException($"Benchmark case {caseId} has an invalid trace: {string.Join(',', traceResult.Diagnostics.Select(item => item.Code))}.");

        var ledgerBytes = Encoding.UTF8.GetBytes(root.GetProperty("ledger").GetRawText());
        var ledgerResult = ObligationLedgerValidator.ParseAndValidate(ledgerBytes, traceResult.Envelope!);
        if (!ledgerResult.IsValid)
            throw new InvalidDataException($"Benchmark case {caseId} has an invalid ledger: {string.Join(',', ledgerResult.Diagnostics.Select(item => item.Code))}.");

        return new(caseId, maxTurns, traceResult.Envelope!, ledgerResult.Ledger!, groundTruth);
    }

    private static BenchmarkCaseResult Evaluate(LoadedBenchmarkCase item)
    {
        var evaluation = Evaluator.Evaluate(item.Trace, item.Ledger);
        var evaluationSummary = new EvaluationSummary(evaluation.TraceClassification, evaluation.ObligationResults
            .Select(result => new ObligationSummary(result.ObligationId, result.Classification, result.EvidenceEventIds))
            .ToList());
        var maxTurns = BenchmarkBaselines.MaxTurns(item.Trace, item.MaxTurns);
        var exactRepeat = BenchmarkBaselines.ExactRepeat(item.Trace);
        var fuzzyRepeatCycle = BenchmarkBaselines.FuzzyRepeatOrCycle(item.Trace);
        var status = Compare(item.GroundTruth, evaluationSummary, maxTurns, exactRepeat, fuzzyRepeatCycle);
        return new(item.CaseId, evaluationSummary, maxTurns, exactRepeat, fuzzyRepeatCycle, item.GroundTruth, status);
    }

    private static BenchmarkCaseStatus Compare(
        BenchmarkGroundTruth expected,
        EvaluationSummary actualEvaluation,
        MaxTurnsResult actualMaxTurns,
        RepeatResult actualExactRepeat,
        RepeatResult actualFuzzyRepeatCycle)
    {
        var traceClassification = Match(expected.TraceClassification == actualEvaluation.TraceClassification);
        var maxTurns = Match(expected.MaxTurns.Triggered == actualMaxTurns.Triggered);
        var exactRepeat = Match(expected.ExactRepeat.Detected == actualExactRepeat.Detected
            && expected.ExactRepeat.Kind == actualExactRepeat.Kind);
        var fuzzyRepeatCycle = Match(expected.FuzzyRepeatCycle.Detected == actualFuzzyRepeatCycle.Detected
            && expected.FuzzyRepeatCycle.Kind == actualFuzzyRepeatCycle.Kind);
        var overall = Match(traceClassification == "match"
            && maxTurns == "match"
            && exactRepeat == "match"
            && fuzzyRepeatCycle == "match");
        return new(overall, traceClassification, maxTurns, exactRepeat, fuzzyRepeatCycle);
    }

    private static string Match(bool matches) => matches ? "match" : "mismatch";

    private static BenchmarkGroundTruth ReadGroundTruth(JsonElement root, string caseId, string path)
    {
        if (!root.TryGetProperty("groundTruth", out var groundTruth) || groundTruth.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Benchmark case {caseId} at {path} is missing an object groundTruth.");

        RejectUnknown(groundTruth, ["traceClassification", "maxTurns", "exactRepeat", "fuzzyRepeatCycle"], $"{path}:groundTruth");
        var traceClassification = RequiredString(groundTruth, "traceClassification", $"{path}:groundTruth");
        if (traceClassification is not ("progress" or "insufficient-evidence" or "stagnation" or "regression"))
            throw new InvalidDataException($"Benchmark case {caseId} has an invalid groundTruth.traceClassification.");

        var maxTurns = ReadExpectedMaxTurns(groundTruth, caseId, path);
        var exactRepeat = ReadExpectedRepeat(groundTruth, "exactRepeat", ["exact-repeat"], caseId, path);
        var fuzzyRepeatCycle = ReadExpectedRepeat(groundTruth, "fuzzyRepeatCycle", ["fuzzy-repeat", "cycle"], caseId, path);
        return new(traceClassification, maxTurns, exactRepeat, fuzzyRepeatCycle);
    }

    private static ExpectedMaxTurns ReadExpectedMaxTurns(JsonElement root, string caseId, string path)
    {
        var value = RequiredObject(root, "maxTurns", caseId, path);
        RejectUnknown(value, ["triggered"], $"{path}:groundTruth.maxTurns");
        return new(RequiredBoolean(value, "triggered", $"{path}:groundTruth.maxTurns"));
    }

    private static ExpectedRepeat ReadExpectedRepeat(
        JsonElement root,
        string name,
        string[] allowedKinds,
        string caseId,
        string path)
    {
        var value = RequiredObject(root, name, caseId, path);
        RejectUnknown(value, ["detected", "kind"], $"{path}:groundTruth.{name}");
        var detected = RequiredBoolean(value, "detected", $"{path}:groundTruth.{name}");
        if (!value.TryGetProperty("kind", out var kindValue))
            throw new InvalidDataException($"Benchmark case {caseId} is missing groundTruth.{name}.kind.");
        string? kind = kindValue.ValueKind == JsonValueKind.Null ? null :
            kindValue.ValueKind == JsonValueKind.String ? kindValue.GetString() :
            throw new InvalidDataException($"Benchmark case {caseId} has an invalid groundTruth.{name}.kind.");
        if (detected != (kind is not null) || kind is not null && !allowedKinds.Contains(kind, StringComparer.Ordinal))
            throw new InvalidDataException($"Benchmark case {caseId} has incoherent groundTruth.{name} detection metadata.");
        return new(detected, kind);
    }

    private static JsonElement RequiredObject(JsonElement root, string name, string caseId, string path)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Benchmark case {caseId} at {path} is missing an object groundTruth.{name}.");
        return value;
    }

    private static bool RequiredBoolean(JsonElement root, string name, string path)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            throw new InvalidDataException($"Benchmark metadata at {path} is missing a boolean {name}.");
        return value.GetBoolean();
    }

    private static void RejectUnknown(JsonElement root, IEnumerable<string> allowed, string path)
    {
        var names = allowed.ToHashSet(StringComparer.Ordinal);
        var unknown = root.EnumerateObject().Select(item => item.Name).FirstOrDefault(name => !names.Contains(name));
        if (unknown is not null) throw new InvalidDataException($"Benchmark metadata at {path} has an unknown property {unknown}.");
    }

    private static string RequiredString(JsonElement root, string name, string path)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException($"Benchmark case at {path} is missing a non-empty {name}.");
        return value.GetString()!;
    }
}
