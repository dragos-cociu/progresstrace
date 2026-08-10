using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Validation;

public sealed record BaselineDefinitionValidationResult(
    BaselineDefinition? BaselineDefinition,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => BaselineDefinition is not null && Diagnostics.Count == 0;
}
