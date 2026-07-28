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
}
