namespace ProgressTrace.Core.Models;

public sealed record GateOutcome(
    string? SchemaVersion,
    string? OutcomeId,
    string? SessionId,
    string? InvocationId,
    long? Sequence,
    string? Command,
    int? ExitCode,
    DateTimeOffset? Timestamp,
    string? ObligationId,
    string? Verdict,
    string? SourceDigest);
