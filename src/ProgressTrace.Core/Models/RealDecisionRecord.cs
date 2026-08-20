namespace ProgressTrace.Core.Models;

public sealed record RealDecisionRecord(
    string SchemaVersion,
    string SessionId,
    string TaskContractId,
    string Decision,
    DateTimeOffset DecidedAt,
    string Source);
