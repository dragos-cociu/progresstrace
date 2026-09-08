using System.Globalization;
using System.Text.Json;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Validation;

public sealed record RealDecisionRecordValidationResult(RealDecisionRecord? RealDecisionRecord, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => RealDecisionRecord is not null && Diagnostics.Count == 0;
}

public static class RealDecisionRecordValidator
{
    private static readonly HashSet<string> Decisions = new(StringComparer.Ordinal) { "continued", "stopped", "merged", "rejected", "escalated", "abandoned", "completed" };

    public static RealDecisionRecordValidationResult ParseAndValidate(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > TraceValidator.MaximumInputSizeBytes) return Invalid(DiagnosticCodes.InputTooLarge, "", "Input exceeds the maximum size.");
        try
        {
            using var document = JsonDocument.Parse(bytes); var root = document.RootElement; var diagnostics = new List<Diagnostic>();
            if (root.ValueKind != JsonValueKind.Object) return Invalid(DiagnosticCodes.Type, "", "Real decision record must be an object.");
            JsonValidationHelpers.RejectDuplicateProperties(root, "", diagnostics); JsonValidationHelpers.RejectUnknown(root, "", ["schemaVersion", "sessionId", "taskContractId", "decision", "decidedAt", "source"], diagnostics);
            var version = String(root, "schemaVersion", diagnostics); var session = String(root, "sessionId", diagnostics); var task = String(root, "taskContractId", diagnostics); var decision = String(root, "decision", diagnostics); var decided = String(root, "decidedAt", diagnostics); var source = String(root, "source", diagnostics);
            if (version is not null && version != "1.0") diagnostics.Add(new(DiagnosticCodes.UnsupportedSchemaVersion, "/schemaVersion", "Schema version is not supported."));
            if (decision is not null && !Decisions.Contains(decision)) diagnostics.Add(new(DiagnosticCodes.InvalidValue, "/decision", "Decision is not recognized."));
            DateTimeOffset timestamp = default;
            if (decided is not null && (!DateTimeOffset.TryParseExact(decided, ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"], CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp) || decided.IndexOf('T', StringComparison.Ordinal) < 0)) diagnostics.Add(new(DiagnosticCodes.InvalidValue, "/decidedAt", "decidedAt must be an ISO-8601 date-time."));
            return new(new(version ?? "", session ?? "", task ?? "", decision ?? "", timestamp, source ?? ""), diagnostics);
        }
        catch (JsonException) { return Invalid(DiagnosticCodes.InvalidJson, "", "Input is not valid JSON."); }
    }

    private static string? String(JsonElement root, string name, List<Diagnostic> diagnostics)
    {
        if (!JsonValidationHelpers.TryRequired(root, name, "", diagnostics, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String) { diagnostics.Add(new(DiagnosticCodes.Type, "/" + name, $"{name} must be a string.")); return null; }
        var text = value.GetString(); if (string.IsNullOrEmpty(text)) diagnostics.Add(new(DiagnosticCodes.InvalidValue, "/" + name, $"{name} must not be empty.")); return text;
    }

    private static RealDecisionRecordValidationResult Invalid(string code, string pointer, string message) => new(null, [new(code, pointer, message)]);
}
