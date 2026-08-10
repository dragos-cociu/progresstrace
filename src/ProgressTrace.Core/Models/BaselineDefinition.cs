namespace ProgressTrace.Core.Models;

public sealed record BaselineDefinition(
    string? SchemaVersion,
    string? TraceId,
    BaselineSource? BaselineSource,
    IReadOnlyList<ObligationBudget>? ObligationBudgets);

public sealed record BaselineSource(
    string? ProducerType,
    string? ProducerName,
    string? ProducerVersion,
    string? EvidenceBasis);

public sealed record ObligationBudget(string? ObligationId, int? EventBudget);
