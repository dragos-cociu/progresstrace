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
        return new("progresstrace-benchmark-mvp-1", cases.Count, cases);
    }

    private static LoadedBenchmarkCase Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        var caseId = RequiredString(root, "caseId", path);
        var maxTurns = root.GetProperty("maxTurns").GetInt32();
        if (maxTurns < 1) throw new InvalidDataException($"Benchmark case {caseId} has an invalid maxTurns value.");

        var traceBytes = Encoding.UTF8.GetBytes(root.GetProperty("trace").GetRawText());
        var traceResult = TraceValidator.ParseAndValidate(traceBytes);
        if (!traceResult.IsValid)
            throw new InvalidDataException($"Benchmark case {caseId} has an invalid trace: {string.Join(',', traceResult.Diagnostics.Select(item => item.Code))}.");

        var ledgerBytes = Encoding.UTF8.GetBytes(root.GetProperty("ledger").GetRawText());
        var ledgerResult = ObligationLedgerValidator.ParseAndValidate(ledgerBytes, traceResult.Envelope!);
        if (!ledgerResult.IsValid)
            throw new InvalidDataException($"Benchmark case {caseId} has an invalid ledger: {string.Join(',', ledgerResult.Diagnostics.Select(item => item.Code))}.");

        return new(caseId, maxTurns, traceResult.Envelope!, ledgerResult.Ledger!);
    }

    private static BenchmarkCaseResult Evaluate(LoadedBenchmarkCase item)
    {
        var evaluation = Evaluator.Evaluate(item.Trace, item.Ledger);
        return new(
            item.CaseId,
            new(evaluation.TraceClassification, evaluation.ObligationResults
                .Select(result => new ObligationSummary(result.ObligationId, result.Classification, result.EvidenceEventIds))
                .ToList()),
            BenchmarkBaselines.MaxTurns(item.Trace, item.MaxTurns),
            BenchmarkBaselines.ExactRepeat(item.Trace),
            BenchmarkBaselines.FuzzyRepeatOrCycle(item.Trace));
    }

    private static string RequiredString(JsonElement root, string name, string path)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException($"Benchmark case at {path} is missing a non-empty {name}.");
        return value.GetString()!;
    }
}
