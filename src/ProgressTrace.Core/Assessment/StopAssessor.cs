using ProgressTrace.Core.Evaluation;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Assessment;

public static class StopAssessor
{
    public static StopAssessmentResult Assess(TraceEnvelope trace, ObligationLedger ledger, TerminationDeclaration declaration)
    {
        var orderedEvents = trace.Events!.OrderBy(e => e.Sequence).ThenBy(e => e.Timestamp).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
        var ranks = orderedEvents.Select((item, index) => (item.Id!, index)).ToDictionary(x => x.Item1, x => x.index, StringComparer.Ordinal);
        var evaluation = Evaluator.Evaluate(trace, ledger);
        var terminationRank = ranks[declaration.TerminationEventId!];
        var obligationResults = new List<ObligationAssessment>();
        for (var i = 0; i < ledger.Obligations!.Count; i++)
        {
            var obligation = ledger.Obligations[i];
            var stableRank = StableAttainment.GetRank(obligation.Id!, ledger, ranks);
            var evaluated = evaluation.ObligationResults[i];
            obligationResults.Add(new(obligation.Id!, evaluated.Classification, evaluated.EvidenceEventIds,
                stableRank is null ? "unmet-target-at-termination" : "stable-attainment",
                stableRank, stableRank is null ? null : terminationRank - stableRank));
        }
        var attested = declaration.TerminationKind is not ("capture-truncated" or "unknown");
        int? safeStopRank = null, traceOverhead = null;
        string stopClassification;
        if (!attested) stopClassification = "incomplete-observation";
        else if (obligationResults.Any(r => r.StableAttainmentRank is null))
            stopClassification = "unmet-target-at-termination-present";
        else
        {
            safeStopRank = obligationResults.Max(r => r.StableAttainmentRank!.Value);
            traceOverhead = terminationRank - safeStopRank;
            stopClassification = traceOverhead > 0 ? "late-termination" : "on-target";
        }
        return new("1.0", trace.TraceId!, new("progresstrace-stop-assessor", "1.0.0"),
            declaration.TerminationEventId!, declaration.TerminationKind!, declaration.DeclarationSource!,
            attested, terminationRank, obligationResults, evaluation.TraceClassification,
            stopClassification, safeStopRank, traceOverhead);
    }
}
