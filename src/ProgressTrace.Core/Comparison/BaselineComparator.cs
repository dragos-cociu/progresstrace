using ProgressTrace.Core.Assessment;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Comparison;

public static class BaselineComparator
{
    public static BaselineComparisonResult Compare(TraceEnvelope trace, ObligationLedger ledger,
        TerminationDeclaration declaration, BaselineDefinition baseline)
    {
        var assessment = StopAssessor.Assess(trace, ledger, declaration);
        var observedEventCount = assessment.TerminationRank + 1;
        var budgets = baseline.ObligationBudgets!.ToDictionary(b => b.ObligationId!, StringComparer.Ordinal);
        var comparisons = new List<ObligationComparison>();
        foreach (var assessed in assessment.ObligationResults)
        {
            var eventBudget = budgets[assessed.ObligationId].EventBudget!.Value;
            string applicability;
            int? unused;
            if (!assessment.TerminationAttested) (applicability, unused) = ("incomplete-observation", null);
            else if (assessed.Outcome == "stable-attainment") (applicability, unused) = ("not-applicable-stable-attainment", null);
            else if (eventBudget > observedEventCount) (applicability, unused) = ("unused-authored-budget", eventBudget - observedEventCount);
            else if (eventBudget == observedEventCount) (applicability, unused) = ("budget-exhausted-exactly", 0);
            else (applicability, unused) = ("budget-exceeded", null);
            comparisons.Add(new(assessed.ObligationId, assessed.Outcome, eventBudget, applicability, unused));
        }
        var positives = comparisons.Where(c => c.Applicability == "unused-authored-budget")
            .Select(c => c.AuthoredEstimateUnusedEventBudget!.Value).ToList();
        return new("1.0", trace.TraceId!, new("progresstrace-baseline-comparator", "1.0.0"),
            baseline.BaselineSource!, declaration.TerminationEventId!, declaration.TerminationKind!,
            assessment.TerminationAttested, assessment.TerminationRank, observedEventCount, comparisons,
            positives.Count > 0, positives.Count > 0 ? positives.Max() : null);
    }
}
