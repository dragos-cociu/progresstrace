namespace ProgressTrace.Core.Models;

public sealed record StopAssessmentResult(
    string SchemaVersion,
    string TraceId,
    AssessmentAlgorithm Algorithm,
    string TerminationEventId,
    string TerminationKind,
    DeclarationSource DeclarationSource,
    bool TerminationAttested,
    int TerminationRank,
    IReadOnlyList<ObligationAssessment> ObligationResults,
    string TraceClassification,
    string StopClassification,
    int? SafeStopRank,
    int? TraceOverhead);

public sealed record AssessmentAlgorithm(string Name, string Version);

public sealed record ObligationAssessment(
    string ObligationId,
    string Classification,
    IReadOnlyList<string> EvidenceEventIds,
    string Outcome,
    int? StableAttainmentRank,
    int? Overhead);
