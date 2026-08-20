namespace ProgressTrace.Core.Models;

public sealed record AdvisoryDivergenceReport(
    string SchemaVersion,
    string ReportId,
    string SessionId,
    string LedgerTraceId,
    string AdvisorySourceDigest,
    string GeneratedAt,
    IReadOnlyList<AdvisoryDivergence> Divergences,
    AdvisoryDivergenceRollup? Rollup);

public sealed record AdvisoryDivergence(string ObligationId, string LedgerStatus, string AdvisoryStatus, bool Diverged);
public sealed record AdvisoryDivergenceRollup(int TotalObligations, int DivergentCount, bool AllConverged);
