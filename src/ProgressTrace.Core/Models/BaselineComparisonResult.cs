namespace ProgressTrace.Core.Models;

public sealed record BaselineComparisonResult(
    string SchemaVersion,
    string TraceId,
    ComparisonAlgorithm Algorithm,
    BaselineSource BaselineSource,
    string TerminationEventId,
    string TerminationKind,
    bool TerminationAttested,
    int TerminationRank,
    int ObservedEventCount,
    IReadOnlyList<ObligationComparison> ObligationComparisons,
    bool AuthoredEstimateFalseHaltPresent,
    int? MaxAuthoredEstimateUnusedEventBudget);

public sealed record ComparisonAlgorithm(string Name, string Version);

public sealed record ObligationComparison(
    string ObligationId,
    string Outcome,
    int EventBudget,
    string Applicability,
    int? AuthoredEstimateUnusedEventBudget);
