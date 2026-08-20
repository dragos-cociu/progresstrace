namespace ProgressTrace.Core.Models;

public sealed record AdvisoryResult(
    string SchemaVersion,
    string SessionId,
    string TaskContractId,
    string LedgerTraceId,
    string GeneratedAt,
    string SourceDigest,
    string Classification,
    string Recommendation,
    IReadOnlyList<AdvisoryObligation> Obligations);

public sealed record AdvisoryObligation(
    string ObligationId,
    string Status,
    string Classification,
    bool Stable,
    IReadOnlyList<string> EvidenceEventIds);
