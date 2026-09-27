using System.Text.Json;
using System.Text.RegularExpressions;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;
using ProgressTrace.Core.Shadow;

namespace ProgressTrace.Core.Validation;

public sealed record ShadowSessionSummaryValidationResult(ShadowSessionSummary? ShadowSessionSummary, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => ShadowSessionSummary is not null && Diagnostics.Count == 0;
}

public static class ShadowSessionSummaryValidator
{
    private static readonly HashSet<string> Recommendations = new(StringComparer.Ordinal) { "continue", "stop-recommended", "insufficient-evidence" };
    private static readonly HashSet<string> Classifications = new(StringComparer.Ordinal) { "progress", "recovery-after-failed-attempt", "repeated-attempt-without-obligation-advancement", "failed-attempt", "insufficient-evidence" };
    private static readonly Regex Digest = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static ShadowSessionSummaryValidationResult ParseAndValidate(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > TraceValidator.MaximumInputSizeBytes) return Invalid(DiagnosticCodes.InputTooLarge, "", "Input exceeds the maximum size.");
        try
        {
            using var document = JsonDocument.Parse(bytes); var root = document.RootElement; var d = new List<Diagnostic>();
            if (root.ValueKind != JsonValueKind.Object) return Invalid(DiagnosticCodes.Type, "", "Shadow session summary must be an object.");
            JsonValidationHelpers.RejectDuplicateProperties(root, "", d); JsonValidationHelpers.RejectUnknown(root, "", ["schemaVersion", "sessionId", "taskContractId", "snapshots", "realDecision", "finalRecommendation", "aligned", "generatedAt", "sourceDigest"], d);
            var version = String(root, "schemaVersion", "", d); var session = String(root, "sessionId", "", d); var task = String(root, "taskContractId", "", d); var final = String(root, "finalRecommendation", "", d); var generated = String(root, "generatedAt", "", d); var digest = String(root, "sourceDigest", "", d);
            if (version is not null && version != "1.0") d.Add(new(DiagnosticCodes.UnsupportedSchemaVersion, "/schemaVersion", "Schema version is not supported.")); if (final is not null && !Recommendations.Contains(final)) d.Add(new(DiagnosticCodes.InvalidValue, "/finalRecommendation", "Recommendation is not recognized.")); if (generated is not null && generated != ShadowAssembler.GeneratedAt) d.Add(new(DiagnosticCodes.InvalidValue, "/generatedAt", "generatedAt must equal the deterministic sentinel.")); if (digest is not null && !Digest.IsMatch(digest)) d.Add(new(DiagnosticCodes.InvalidValue, "/sourceDigest", "sourceDigest must be lowercase hexadecimal SHA-256."));
            var snapshots = new List<ShadowSnapshot>();
            if (!JsonValidationHelpers.TryRequired(root, "snapshots", "", d, out var array)) { }
            else if (array.ValueKind != JsonValueKind.Array) d.Add(new(DiagnosticCodes.Type, "/snapshots", "snapshots must be an array."));
            else { if (array.GetArrayLength() == 0) d.Add(new(DiagnosticCodes.InvalidValue, "/snapshots", "snapshots must not be empty.")); var index = 0; foreach (var item in array.EnumerateArray()) { var p = $"/snapshots/{index++}"; if (item.ValueKind != JsonValueKind.Object) { d.Add(new(DiagnosticCodes.Type, p, "Snapshot must be an object.")); continue; } JsonValidationHelpers.RejectUnknown(item, p, ["shadowSequence", "triggeringGateOutcomeId", "advisorySourceDigest", "recommendation", "classification"], d); long sequence = 0; if (!JsonValidationHelpers.TryRequired(item, "shadowSequence", p, d, out var sequenceNode) || sequenceNode.ValueKind != JsonValueKind.Number || !sequenceNode.TryGetInt64(out sequence)) d.Add(new(DiagnosticCodes.Type, p + "/shadowSequence", "shadowSequence must be an integer.")); else if (sequence < 0) d.Add(new(DiagnosticCodes.InvalidValue, p + "/shadowSequence", "shadowSequence must be non-negative.")); var gate = String(item, "triggeringGateOutcomeId", p, d); var advisoryDigest = String(item, "advisorySourceDigest", p, d); var recommendation = String(item, "recommendation", p, d); var classification = String(item, "classification", p, d); if (advisoryDigest is not null && !Digest.IsMatch(advisoryDigest)) d.Add(new(DiagnosticCodes.InvalidValue, p + "/advisorySourceDigest", "advisorySourceDigest must be lowercase hexadecimal SHA-256.")); if (recommendation is not null && !Recommendations.Contains(recommendation)) d.Add(new(DiagnosticCodes.InvalidValue, p + "/recommendation", "Recommendation is not recognized.")); if (classification is not null && !Classifications.Contains(classification)) d.Add(new(DiagnosticCodes.InvalidValue, p + "/classification", "Classification is not recognized.")); snapshots.Add(new(sequence, gate ?? "", advisoryDigest ?? "", recommendation ?? "", classification ?? "")); } }
            RealDecisionRecord? decision = null; if (!JsonValidationHelpers.TryRequired(root, "realDecision", "", d, out var decisionNode)) { } else if (decisionNode.ValueKind != JsonValueKind.Null) { var validation = RealDecisionRecordValidator.ParseAndValidate(System.Text.Encoding.UTF8.GetBytes(decisionNode.GetRawText())); if (!validation.IsValid) d.AddRange(validation.Diagnostics.Select(x => x with { Pointer = "/realDecision" + x.Pointer })); else decision = validation.RealDecisionRecord; }
            bool? aligned = null; if (!JsonValidationHelpers.TryRequired(root, "aligned", "", d, out var alignedNode)) { } else if (alignedNode.ValueKind == JsonValueKind.Null) { } else if (alignedNode.ValueKind is JsonValueKind.True or JsonValueKind.False) aligned = alignedNode.GetBoolean(); else d.Add(new(DiagnosticCodes.Type, "/aligned", "aligned must be a boolean or null."));
            for (var index = 1; index < snapshots.Count; index++) if (snapshots[index].ShadowSequence <= snapshots[index - 1].ShadowSequence) d.Add(new(DiagnosticCodes.InvalidValue, $"/snapshots/{index}/shadowSequence", "shadowSequence must be strictly increasing."));
            if (snapshots.Count != 0 && final is not null && final != snapshots[^1].Recommendation) d.Add(new(DiagnosticCodes.InvalidValue, "/finalRecommendation", "finalRecommendation must equal the latest snapshot recommendation."));
            if (decision is not null && (decision.SessionId != session || decision.TaskContractId != task)) d.Add(new(DiagnosticCodes.InvalidValue, "/realDecision", "Real decision references must match the summary."));
            bool? expectedAlignment = final switch { "insufficient-evidence" => null, _ when decision is null => null, "continue" => decision.Decision is "continued" or "escalated", "stop-recommended" => decision.Decision is "stopped" or "merged" or "rejected" or "abandoned" or "completed", _ => null };
            if (aligned != expectedAlignment) d.Add(new(DiagnosticCodes.InvalidValue, "/aligned", "aligned does not match the fixed alignment table."));
            return new(new(version ?? "", session ?? "", task ?? "", snapshots, decision, final ?? "", aligned, generated ?? "", digest ?? ""), d);
        }
        catch (JsonException) { return Invalid(DiagnosticCodes.InvalidJson, "", "Input is not valid JSON."); }
    }

    private static string? String(JsonElement parent, string name, string pointer, List<Diagnostic> d) { if (!JsonValidationHelpers.TryRequired(parent, name, pointer, d, out var value)) return null; if (value.ValueKind != JsonValueKind.String) { d.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be a string.")); return null; } var text = value.GetString(); if (string.IsNullOrEmpty(text)) d.Add(new(DiagnosticCodes.InvalidValue, JsonValidationHelpers.Path(pointer, name), $"{name} must not be empty.")); return text; }
    private static ShadowSessionSummaryValidationResult Invalid(string code, string pointer, string message) => new(null, [new(code, pointer, message)]);
}
