namespace ProgressTrace.Core.Models;

public sealed record ObservedBudget(
    string? SchemaVersion,
    string? SessionId,
    string? TaskContractId,
    string? LedgerTraceId,
    string? GeneratedAt,
    string? SourceDigest,
    IReadOnlyList<ObligationObservation>? Obligations);

public sealed record ObligationObservation(
    string? ObligationId,
    string? LedgerStatus,
    int? ObservedInvocationCount,
    long? ObservedElapsedMillis,
    long? ObservedTokensTotal);
