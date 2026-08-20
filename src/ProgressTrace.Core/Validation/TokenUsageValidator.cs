using System.Text.Json;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Validation;

public sealed record TokenUsageValidationResult(TokenUsage? TokenUsage, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => TokenUsage is not null && Diagnostics.Count == 0;
}

public static class TokenUsageValidator
{
    private static readonly HashSet<string> ProducerTypes = new(["agent", "harness", "operator", "adapter"], StringComparer.Ordinal);
    private static readonly HashSet<string> EvidenceBases = new(["provider-usage-field", "harness-metered", "agent-self-reported", "adapter-inference"], StringComparer.Ordinal);

    public static TokenUsageValidationResult ParseAndValidate(ReadOnlyMemory<byte> json)
    {
        if (json.Length > TraceValidator.MaximumInputSizeBytes) return Invalid(DiagnosticCodes.InputTooLarge, "", "Input exceeds the maximum size.");
        JsonDocument document;
        try { document = JsonDocument.Parse(json); }
        catch (JsonException) { return Invalid(DiagnosticCodes.InvalidJson, "", "Input is not valid JSON."); }
        using (document)
        {
            var diagnostics = new List<Diagnostic>(); var root = document.RootElement;
            JsonValidationHelpers.RejectDuplicateProperties(root, "", diagnostics);
            if (root.ValueKind != JsonValueKind.Object) return Invalid(DiagnosticCodes.Type, "", "Token usage must be an object.");
            JsonValidationHelpers.RejectUnknown(root, "", ["schemaVersion", "sessionId", "taskContractId", "records"], diagnostics);
            var version = String(root, "schemaVersion", "", diagnostics); var sessionId = String(root, "sessionId", "", diagnostics); var task = String(root, "taskContractId", "", diagnostics);
            if (version is not null && version != "1.0") diagnostics.Add(new(DiagnosticCodes.UnsupportedSchemaVersion, "/schemaVersion", "Schema version is not supported."));
            var records = new List<TokenUsageRecord>();
            if (!JsonValidationHelpers.TryRequired(root, "records", "", diagnostics, out var array)) { }
            else if (array.ValueKind != JsonValueKind.Array) diagnostics.Add(new(DiagnosticCodes.Type, "/records", "records must be an array."));
            else
            {
                var index = 0;
                foreach (var item in array.EnumerateArray())
                {
                    var pointer = $"/records/{index++}";
                    if (item.ValueKind != JsonValueKind.Object) { diagnostics.Add(new(DiagnosticCodes.Type, pointer, "Token usage record must be an object.")); continue; }
                    JsonValidationHelpers.RejectUnknown(item, pointer, ["sessionId", "invocationId", "sequence", "tokensTotal", "producerType", "producerName", "producerVersion", "evidenceBasis"], diagnostics);
                    var recordSession = String(item, "sessionId", pointer, diagnostics); var invocation = String(item, "invocationId", pointer, diagnostics);
                    var sequence = Integer(item, "sequence", pointer, diagnostics, 0, long.MaxValue); var tokens = Integer(item, "tokensTotal", pointer, diagnostics, 0, int.MaxValue);
                    var producerType = String(item, "producerType", pointer, diagnostics); var producerName = String(item, "producerName", pointer, diagnostics, whitespace: true);
                    var producerVersion = NullableString(item, "producerVersion", pointer, diagnostics); var evidence = String(item, "evidenceBasis", pointer, diagnostics);
                    if (producerType is not null && !ProducerTypes.Contains(producerType)) diagnostics.Add(new(DiagnosticCodes.InvalidValue, pointer + "/producerType", "producerType is not supported."));
                    if (evidence is not null && !EvidenceBases.Contains(evidence)) diagnostics.Add(new(DiagnosticCodes.InvalidValue, pointer + "/evidenceBasis", "evidenceBasis is not supported."));
                    records.Add(new(recordSession, invocation, sequence, tokens, producerType, producerName, producerVersion, evidence));
                }
            }
            return new(new(version, sessionId, task, records), diagnostics);
        }
    }

    private static string? String(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics, bool whitespace = false)
    {
        if (!JsonValidationHelpers.TryRequired(parent, name, pointer, diagnostics, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String) { diagnostics.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be a string.")); return null; }
        var result = value.GetString();
        if (string.IsNullOrEmpty(result) || (whitespace && string.IsNullOrWhiteSpace(result))) diagnostics.Add(new(DiagnosticCodes.InvalidValue, JsonValidationHelpers.Path(pointer, name), $"{name} must not be empty."));
        return result;
    }

    private static string? NullableString(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics)
    {
        if (!JsonValidationHelpers.TryRequired(parent, name, pointer, diagnostics, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) { diagnostics.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be a string or null.")); return null; }
        var result = value.GetString(); if (string.IsNullOrWhiteSpace(result)) diagnostics.Add(new(DiagnosticCodes.InvalidValue, JsonValidationHelpers.Path(pointer, name), $"{name} must not be empty.")); return result;
    }

    private static long? Integer(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics, long minimum, long maximum)
    {
        if (!JsonValidationHelpers.TryRequired(parent, name, pointer, diagnostics, out var value)) return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var result)) { diagnostics.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be an integer.")); return null; }
        if (result < minimum || result > maximum) diagnostics.Add(new(DiagnosticCodes.InvalidValue, JsonValidationHelpers.Path(pointer, name), $"{name} is outside the allowed range.")); return result;
    }

    private static TokenUsageValidationResult Invalid(string code, string pointer, string message) => new(null, [new(code, pointer, message)]);
}
