namespace ProgressTrace.Core.Models;

public sealed record ShadowSessionSummary(
    string SchemaVersion,
    string SessionId,
    string TaskContractId,
    IReadOnlyList<ShadowSnapshot> Snapshots,
    RealDecisionRecord? RealDecision,
    string FinalRecommendation,
    bool? Aligned,
    string GeneratedAt,
    string SourceDigest);

public sealed record ShadowSnapshot(
    long ShadowSequence,
    string TriggeringGateOutcomeId,
    string AdvisorySourceDigest,
    string Recommendation,
    string Classification);
