using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Validation;

public sealed record ObligationValidationResult(ObligationLedger? Ledger, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}
