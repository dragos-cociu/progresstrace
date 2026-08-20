namespace ProgressTrace.Core.Models;

public sealed record LedgerGenerationReport(
    string SchemaVersion,
    string ReportType,
    GeneratorIdentity Generator,
    DateTimeOffset GeneratedAt,
    string TaskContractId,
    string TaskContractPath,
    string TaskContractDigest,
    string TraceId,
    string LedgerDigest,
    IReadOnlyList<GeneratedObligationCoverage> Obligations,
    IReadOnlyList<string> UnsupportedSourceFields);

public sealed record GeneratorIdentity(string Name, string Version);

public sealed record GeneratedObligationCoverage(
    string ObligationId,
    string CoverageStatus,
    string? SourceField,
    string? SourcePointer);
