namespace ProgressTrace.Core.Models;

public sealed record TokenUsage(
    string? SchemaVersion,
    string? SessionId,
    string? TaskContractId,
    IReadOnlyList<TokenUsageRecord>? Records);

public sealed record TokenUsageRecord(
    string? SessionId,
    string? InvocationId,
    long? Sequence,
    long? TokensTotal,
    string? ProducerType,
    string? ProducerName,
    string? ProducerVersion,
    string? EvidenceBasis);
