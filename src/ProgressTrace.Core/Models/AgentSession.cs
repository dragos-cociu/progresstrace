namespace ProgressTrace.Core.Models;

public sealed record AgentSession(
    string? SchemaVersion,
    string? SessionId,
    string? TaskContractId,
    IReadOnlyList<SessionInvocation>? Invocations);

public sealed record SessionInvocation(
    string? InvocationId,
    long? Sequence,
    int? Attempt,
    string? TraceId,
    IReadOnlyList<string>? ObligationIds,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt);
