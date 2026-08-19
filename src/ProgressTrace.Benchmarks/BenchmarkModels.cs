using ProgressTrace.Core.Models;

namespace ProgressTrace.Benchmarks;

public sealed record BenchmarkRun(
    string Format,
    int CaseCount,
    IReadOnlyList<BenchmarkCaseResult> Cases);

public sealed record BenchmarkCaseResult(
    string CaseId,
    EvaluationSummary Evaluation,
    MaxTurnsResult MaxTurns,
    RepeatResult ExactRepeat,
    RepeatResult FuzzyRepeatCycle);

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
    ObligationLedger Ledger);
