using ProgressTrace.Core.Models;

namespace ProgressTrace.Benchmarks;

public sealed record BenchmarkRun(
    string Format,
    int CaseCount,
    BenchmarkSummary Summary,
    IReadOnlyList<BenchmarkCaseResult> Cases);

public sealed record BenchmarkCaseResult(
    string CaseId,
    EvaluationSummary Evaluation,
    MaxTurnsResult MaxTurns,
    RepeatResult ExactRepeat,
    RepeatResult FuzzyRepeatCycle,
    BenchmarkGroundTruth GroundTruth,
    BenchmarkCaseStatus Status);

public sealed record BenchmarkSummary(
    int CaseCount,
    int MatchedCaseCount,
    int MismatchedCaseCount,
    string Status);

public sealed record BenchmarkGroundTruth(
    string TraceClassification,
    ExpectedMaxTurns MaxTurns,
    ExpectedRepeat ExactRepeat,
    ExpectedRepeat FuzzyRepeatCycle);

public sealed record ExpectedMaxTurns(bool Triggered);

public sealed record ExpectedRepeat(bool Detected, string? Kind);

public sealed record BenchmarkCaseStatus(
    string Overall,
    string TraceClassification,
    string MaxTurns,
    string ExactRepeat,
    string FuzzyRepeatCycle);

public sealed record EvaluationSummary(
    string TraceClassification,
    IReadOnlyList<ObligationSummary> Obligations);

public sealed record ObligationSummary(
    string ObligationId,
    string Classification,
    IReadOnlyList<string> EvidenceEventIds);

public sealed record MaxTurnsResult(
    int Limit,
    bool Triggered,
    int? StopRank);

public sealed record RepeatResult(
    bool Detected,
    string? Kind,
    int? StartRank,
    int? EndRank,
    double? Similarity);

internal sealed record LoadedBenchmarkCase(
    string CaseId,
    int MaxTurns,
    TraceEnvelope Trace,
    ObligationLedger Ledger,
    BenchmarkGroundTruth GroundTruth);
