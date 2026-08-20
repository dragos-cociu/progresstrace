using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Generation;

public sealed record LedgerGenerationResult(
    ObligationLedger? Ledger,
    LedgerGenerationReport? Report,
    byte[]? LedgerBytes,
    byte[]? ReportBytes,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => Ledger is not null && Report is not null && Diagnostics.Count == 0;
}
