using System.Text.Json;
using System.Text.RegularExpressions;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Validation;

public sealed record ObservedBudgetValidationResult(ObservedBudget? ObservedBudget, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => ObservedBudget is not null && Diagnostics.Count == 0;
}

public static partial class ObservedBudgetValidator
{
    private static readonly HashSet<string> Statuses = new(["open", "in-progress", "satisfied", "regressed", "abandoned"], StringComparer.Ordinal);

    public static ObservedBudgetValidationResult ParseAndValidate(ReadOnlyMemory<byte> json)
    {
        if (json.Length > TraceValidator.MaximumInputSizeBytes) return Invalid(DiagnosticCodes.InputTooLarge, "", "Input exceeds the maximum size.");
        JsonDocument document; try { document = JsonDocument.Parse(json); } catch (JsonException) { return Invalid(DiagnosticCodes.InvalidJson, "", "Input is not valid JSON."); }
        using (document)
        {
            var d = new List<Diagnostic>(); var root = document.RootElement; JsonValidationHelpers.RejectDuplicateProperties(root, "", d);
            if (root.ValueKind != JsonValueKind.Object) return Invalid(DiagnosticCodes.Type, "", "Observed budget must be an object.");
            JsonValidationHelpers.RejectUnknown(root, "", ["schemaVersion", "sessionId", "taskContractId", "ledgerTraceId", "generatedAt", "sourceDigest", "obligations"], d);
            var version = String(root, "schemaVersion", "", d); var session = String(root, "sessionId", "", d); var task = String(root, "taskContractId", "", d); var trace = String(root, "ledgerTraceId", "", d); var generated = String(root, "generatedAt", "", d); var digest = String(root, "sourceDigest", "", d);
            if (version is not null && version != "1.0") d.Add(new(DiagnosticCodes.UnsupportedSchemaVersion, "/schemaVersion", "Schema version is not supported."));
            if (generated is not null && generated != Budget.BudgetAssembler.GeneratedAt) d.Add(new(DiagnosticCodes.InvalidValue, "/generatedAt", "generatedAt must equal the deterministic sentinel."));
            if (digest is not null && !DigestPattern().IsMatch(digest)) d.Add(new(DiagnosticCodes.InvalidValue, "/sourceDigest", "sourceDigest must be lowercase hexadecimal SHA-256."));
            var observations = new List<ObligationObservation>();
            if (!JsonValidationHelpers.TryRequired(root, "obligations", "", d, out var array)) { }
            else if (array.ValueKind != JsonValueKind.Array) d.Add(new(DiagnosticCodes.Type, "/obligations", "obligations must be an array."));
            else
            {
                if (array.GetArrayLength() == 0) d.Add(new(DiagnosticCodes.InvalidValue, "/obligations", "obligations must not be empty.")); var index = 0;
                foreach (var item in array.EnumerateArray())
                {
                    var p = $"/obligations/{index++}"; if (item.ValueKind != JsonValueKind.Object) { d.Add(new(DiagnosticCodes.Type, p, "Observation must be an object.")); continue; }
                    JsonValidationHelpers.RejectUnknown(item, p, ["obligationId", "ledgerStatus", "observedInvocationCount", "observedElapsedMillis", "observedTokensTotal"], d);
                    var id = String(item, "obligationId", p, d); var status = String(item, "ledgerStatus", p, d); var count = Integer(item, "observedInvocationCount", p, d, int.MaxValue); var elapsed = Integer(item, "observedElapsedMillis", p, d, long.MaxValue); var tokens = NullableInteger(item, "observedTokensTotal", p, d);
                    if (status is not null && !Statuses.Contains(status)) d.Add(new(DiagnosticCodes.InvalidValue, p + "/ledgerStatus", "ledgerStatus is not supported."));
                    observations.Add(new(id, status, count is null ? null : (int)count, elapsed, tokens));
                }
            }
            return new(new(version, session, task, trace, generated, digest, observations), d);
        }
    }

    private static string? String(JsonElement parent, string name, string pointer, List<Diagnostic> d)
    {
        if (!JsonValidationHelpers.TryRequired(parent, name, pointer, d, out var value)) return null; if (value.ValueKind != JsonValueKind.String) { d.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be a string.")); return null; }
        var result = value.GetString(); if (string.IsNullOrEmpty(result)) d.Add(new(DiagnosticCodes.InvalidValue, JsonValidationHelpers.Path(pointer, name), $"{name} must not be empty.")); return result;
    }
    private static long? Integer(JsonElement parent, string name, string pointer, List<Diagnostic> d, long maximum)
    {
        if (!JsonValidationHelpers.TryRequired(parent, name, pointer, d, out var value)) return null; if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var result)) { d.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be an integer.")); return null; }
        if (result < 0 || result > maximum) d.Add(new(DiagnosticCodes.InvalidValue, JsonValidationHelpers.Path(pointer, name), $"{name} is outside the allowed range.")); return result;
    }
    private static long? NullableInteger(JsonElement parent, string name, string pointer, List<Diagnostic> d)
    {
        if (!JsonValidationHelpers.TryRequired(parent, name, pointer, d, out var value) || value.ValueKind == JsonValueKind.Null) return null; if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var result)) { d.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be an integer or null.")); return null; }
        if (result < 0) d.Add(new(DiagnosticCodes.InvalidValue, JsonValidationHelpers.Path(pointer, name), $"{name} is outside the allowed range.")); return result;
    }
    private static ObservedBudgetValidationResult Invalid(string code, string pointer, string message) => new(null, [new(code, pointer, message)]);
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)] private static partial Regex DigestPattern();
}
