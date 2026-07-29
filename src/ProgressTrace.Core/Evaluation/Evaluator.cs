using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Evaluation;

public static class Evaluator
{
    public static EvaluationResult Evaluate(TraceEnvelope trace, ObligationLedger ledger)
    {
        var sequences = trace.Events!.ToDictionary(e => e.Id!, e => e.Sequence!.Value, StringComparer.Ordinal);
        var results = new List<ObligationEvaluation>();
        foreach (var obligation in ledger.Obligations!)
        {
            var signals = ledger.Signals!.Where(s => s.ObligationId == obligation.Id)
                .OrderBy(s => sequences[s.EventId!]).ThenBy(s => s.EventId, StringComparer.Ordinal).ThenBy(s => s.Index).ToList();
            results.Add(new(obligation.Id!, Classify(signals), signals.Select(s => s.EventId!).ToList()));
        }
        var classification = results.Any(r => r.Classification == "regression") ? "regression"
            : results.Any(r => r.Classification == "insufficient-evidence") ? "insufficient-evidence"
            : results.Any(r => r.Classification == "stagnation") ? "stagnation" : "progress";
        return new("1.0", trace.TraceId!, new("progresstrace-evaluator", "1.0.0"), results, classification);
    }

    private static string Classify(IReadOnlyList<ObligationSignal> signals)
    {
        if (signals.Count == 0) return "insufficient-evidence";
        var last = signals[^1].Status!;
        if (last == "abandoned") return "stagnation";
        if (last == "regressed") return "regression";
        if (signals.Count == 1) return last is "satisfied" or "in-progress" ? "progress" : "stagnation";
        var firstRank = signals[0].Status == "regressed" ? -1 : Rank(signals[0].Status!);
        var lastRank = Rank(last);
        return lastRank > firstRank ? "progress" : lastRank == firstRank ? "stagnation" : "regression";
    }

    private static int Rank(string status) => status switch { "open" => 0, "in-progress" => 1, "satisfied" => 2, _ => -1 };
}
