namespace ProgressTrace.Core.Models;

public sealed record ObligationLedger(
    string? SchemaVersion,
    string? TraceId,
    IReadOnlyList<Obligation>? Obligations,
    IReadOnlyList<ObligationSignal>? Signals);

public sealed record Obligation(string? Id, string? Description);

public sealed record ObligationSignal(string? ObligationId, string? EventId, string? Status, int Index);
