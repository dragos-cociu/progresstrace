namespace ProgressTrace.Core.Models;

public sealed record TerminationDeclaration(
    string? SchemaVersion,
    string? TraceId,
    string? TerminationEventId,
    string? TerminationKind,
    DeclarationSource? DeclarationSource);

public sealed record DeclarationSource(
    string? ProducerType,
    string? ProducerName,
    string? ProducerVersion,
    string? EvidenceBasis);
