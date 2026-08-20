using System.Text.Json;
using System.Text.RegularExpressions;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Validation;

public sealed record AdvisoryResultValidationResult(AdvisoryResult? AdvisoryResult, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => AdvisoryResult is not null && Diagnostics.Count == 0;
}

public static class AdvisoryResultValidator
{
    private static readonly HashSet<string> Classifications = new(StringComparer.Ordinal) { "progress", "recovery-after-failed-attempt", "repeated-attempt-without-obligation-advancement", "insufficient-evidence" };
    private static readonly HashSet<string> Recommendations = new(StringComparer.Ordinal) { "continue", "stop-recommended", "insufficient-evidence" };
    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal) { "open", "in-progress", "satisfied", "regressed", "abandoned" };
    private static readonly Regex Digest = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static AdvisoryResultValidationResult ParseAndValidate(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > TraceValidator.MaximumInputSizeBytes) return Invalid(DiagnosticCodes.InputTooLarge, "", "Input exceeds the maximum size.");
        try
        {
            using var document = JsonDocument.Parse(bytes); var root = document.RootElement; var d = new List<Diagnostic>();
            if (root.ValueKind != JsonValueKind.Object) return Invalid(DiagnosticCodes.Type, "", "Advisory result must be an object.");
            JsonValidationHelpers.RejectDuplicateProperties(root, "", d);
            JsonValidationHelpers.RejectUnknown(root, "", ["schemaVersion", "sessionId", "taskContractId", "ledgerTraceId", "generatedAt", "sourceDigest", "classification", "recommendation", "obligations"], d);
            var version = ReadString(root, "schemaVersion", "", d); var session = ReadString(root, "sessionId", "", d); var task = ReadString(root, "taskContractId", "", d);
            var trace = ReadString(root, "ledgerTraceId", "", d); var generated = ReadString(root, "generatedAt", "", d); var digest = ReadString(root, "sourceDigest", "", d);
            var classification = ReadString(root, "classification", "", d); var recommendation = ReadString(root, "recommendation", "", d);
            if (version is not null && version != "1.0") d.Add(new(DiagnosticCodes.InvalidValue, "/schemaVersion", "Value does not match the contract constant."));
            if (generated is not null && generated != "1970-01-01T00:00:00.0000000Z") d.Add(new(DiagnosticCodes.InvalidValue, "/generatedAt", "Value does not match the contract constant."));
            if (digest is not null && !Digest.IsMatch(digest)) d.Add(new(DiagnosticCodes.InvalidValue, "/sourceDigest", "Digest is not valid."));
            if (classification is not null && !Classifications.Contains(classification)) d.Add(new(DiagnosticCodes.InvalidValue, "/classification", "Classification is not recognized."));
            if (recommendation is not null && !Recommendations.Contains(recommendation)) d.Add(new(DiagnosticCodes.InvalidValue, "/recommendation", "Recommendation is not recognized."));
            var obligations = new List<AdvisoryObligation>();
            if (!JsonValidationHelpers.TryRequired(root, "obligations", "", d, out var array)) { }
            else if (array.ValueKind != JsonValueKind.Array) d.Add(new(DiagnosticCodes.Type, "/obligations", "obligations must be an array."));
            else
            {
                if (array.GetArrayLength() == 0) d.Add(new(DiagnosticCodes.InvalidValue, "/obligations", "obligations must not be empty."));
                var index = 0;
                foreach (var item in array.EnumerateArray())
                {
                    var p = $"/obligations/{index++}";
                    if (item.ValueKind != JsonValueKind.Object) { d.Add(new(DiagnosticCodes.Type, p, "Obligation must be an object.")); continue; }
                    JsonValidationHelpers.RejectUnknown(item, p, ["obligationId", "status", "classification", "stable", "evidenceEventIds"], d);
                    var id = ReadString(item, "obligationId", p, d); var status = ReadString(item, "status", p, d); var c = ReadString(item, "classification", p, d);
                    if (status is not null && !Statuses.Contains(status)) d.Add(new(DiagnosticCodes.InvalidValue, p + "/status", "Status is not recognized."));
                    if (c is not null && !Classifications.Contains(c)) d.Add(new(DiagnosticCodes.InvalidValue, p + "/classification", "Classification is not recognized."));
                    bool stable = false;
                    if (!JsonValidationHelpers.TryRequired(item, "stable", p, d, out var stableNode)) { }
                    else if (stableNode.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) d.Add(new(DiagnosticCodes.Type, p + "/stable", "stable must be a boolean.")); else stable = stableNode.GetBoolean();
                    var evidence = ReadStrings(item, "evidenceEventIds", p, d);
                    obligations.Add(new(id ?? "", status ?? "", c ?? "", stable, evidence));
                }
            }
            return new(new(version ?? "", session ?? "", task ?? "", trace ?? "", generated ?? "", digest ?? "", classification ?? "", recommendation ?? "", obligations), d);
        }
        catch (JsonException) { return Invalid(DiagnosticCodes.InvalidJson, "", "Input is not valid JSON."); }
    }

    private static string? ReadString(JsonElement parent, string name, string pointer, List<Diagnostic> d)
    {
        if (!JsonValidationHelpers.TryRequired(parent, name, pointer, d, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String) { d.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be a string.")); return null; }
        var text = value.GetString(); if (string.IsNullOrEmpty(text)) d.Add(new(DiagnosticCodes.InvalidValue, JsonValidationHelpers.Path(pointer, name), $"{name} must not be empty.")); return text;
    }

    private static IReadOnlyList<string> ReadStrings(JsonElement parent, string name, string pointer, List<Diagnostic> d)
    {
        var result = new List<string>();
        if (!JsonValidationHelpers.TryRequired(parent, name, pointer, d, out var value)) return result;
        if (value.ValueKind != JsonValueKind.Array) { d.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be an array.")); return result; }
        var index = 0; foreach (var item in value.EnumerateArray()) { if (item.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(item.GetString())) d.Add(new(DiagnosticCodes.Type, $"{pointer}/{name}/{index}", "Evidence id must be a non-empty string.")); else result.Add(item.GetString()!); index++; }
        return result;
    }

    private static AdvisoryResultValidationResult Invalid(string code, string pointer, string message) => new(null, [new(code, pointer, message)]);
}
