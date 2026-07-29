namespace ProgressTrace.Core.Models;

public sealed record EvaluationResult(
    string SchemaVersion,
    string TraceId,
    EvaluationAlgorithm Algorithm,
    IReadOnlyList<ObligationEvaluation> ObligationResults,
    string TraceClassification);

public sealed record EvaluationAlgorithm(string Name, string Version);

public sealed record ObligationEvaluation(
    string ObligationId,
    string Classification,
    IReadOnlyList<string> EvidenceEventIds);
