namespace ProgressTrace.Core.Diagnostics;

public sealed record Diagnostic(string Code, string Pointer, string Message);

public static class DiagnosticCodes
{
    public const string InvalidJson = "PT000";
    public const string Required = "PT001";
    public const string Type = "PT002";
    public const string UnknownProperty = "PT003";
    public const string DuplicateProperty = "PT004";
    public const string InputTooLarge = "PT005";
    public const string UnsupportedSchemaVersion = "PT100";
    public const string InvalidValue = "PT101";
    public const string DuplicateEventId = "PT102";
    public const string NonMonotonicSequence = "PT103";
    public const string NonMonotonicTimestamp = "PT104";
    public const string DuplicateObligationId = "PT200";
    public const string TraceIdMismatch = "PT201";
    public const string DanglingObligationId = "PT202";
    public const string DanglingEventId = "PT203";
    public const string AbandonedTerminal = "PT204";
    public const string DanglingTerminationEventId = "PT300";
    public const string NonTerminalTerminationEventId = "PT301";
    public const string DeclarationTraceIdMismatch = "PT302";
    public const string DeclarationSourceCoherence = "PT303";
    public const string DuplicateBaselineObligationId = "PT400";
    public const string BaselineTraceIdMismatch = "PT401";
    public const string DanglingBaselineObligationId = "PT402";
    public const string MissingBaselineObligationCoverage = "PT403";
    public const string BaselineSourceCoherence = "PT404";
    public const string DuplicateInvocationId = "PT502";
    public const string NonMonotonicInvocationSequence = "PT503";
    public const string InvalidAttempt = "PT504";
    public const string InvalidInvocationWindow = "PT505";
    public const string EmptyObligationId = "PT506";
    public const string DuplicateOutcomeId = "PT507";
    public const string SessionReferenceMismatch = "PT508";
    public const string InvocationReferenceMismatch = "PT509";
    public const string ProjectionOverflow = "PT515";
}
