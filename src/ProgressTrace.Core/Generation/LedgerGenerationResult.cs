using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Generation;

public sealed record LedgerGenerationResult(
    ObligationLedger? Ledger,
    LedgerGenerationReport? Report,
    byte[]? LedgerBytes,
    byte[]? ReportBytes,
    CorrelationManifest? Manifest,
    byte[]? ManifestBytes,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => Ledger is not null && Report is not null && Diagnostics.Count == 0;
}

public sealed record CorrelationManifest(
    string SchemaVersion,
    string TaskContractId,
    string TraceId,
    IReadOnlyList<CorrelationGateBinding> GateBindings);

public sealed record CorrelationGateBinding(
    string GateKey,
    string ObligationId,
    string PassPredicate,
    CorrelationMatch Match);

public sealed record CorrelationMatch(string Type, string Value);
