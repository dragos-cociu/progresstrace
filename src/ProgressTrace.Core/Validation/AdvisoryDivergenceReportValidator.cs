using System.Text.Json;
using System.Text.RegularExpressions;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Validation;

public sealed record AdvisoryDivergenceReportValidationResult(AdvisoryDivergenceReport? Report, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => Report is not null && Diagnostics.Count == 0;
}

public static class AdvisoryDivergenceReportValidator
{
    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal) { "open", "in-progress", "satisfied", "regressed", "abandoned" };
    private static readonly Regex Digest = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    public static AdvisoryDivergenceReportValidationResult ParseAndValidate(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > TraceValidator.MaximumInputSizeBytes) return Invalid(DiagnosticCodes.InputTooLarge, "", "Input exceeds the maximum size.");
        try
        {
            using var doc = JsonDocument.Parse(bytes); var root = doc.RootElement; var d = new List<Diagnostic>();
            if (root.ValueKind != JsonValueKind.Object) return Invalid(DiagnosticCodes.Type, "", "Divergence report must be an object.");
            JsonValidationHelpers.RejectDuplicateProperties(root, "", d); JsonValidationHelpers.RejectUnknown(root, "", ["schemaVersion", "reportId", "sessionId", "ledgerTraceId", "advisorySourceDigest", "generatedAt", "divergences", "rollup"], d);
            string? S(string n) { if (!JsonValidationHelpers.TryRequired(root, n, "", d, out var v)) return null; if (v.ValueKind != JsonValueKind.String) { d.Add(new(DiagnosticCodes.Type, "/" + n, $"{n} must be a string.")); return null; } var s = v.GetString(); if (string.IsNullOrEmpty(s)) d.Add(new(DiagnosticCodes.InvalidValue, "/" + n, $"{n} must not be empty.")); return s; }
            var version = S("schemaVersion"); var reportId = S("reportId"); var session = S("sessionId"); var trace = S("ledgerTraceId"); var source = S("advisorySourceDigest"); var generated = S("generatedAt");
            if (version is not null && version != "1.0") d.Add(new(DiagnosticCodes.InvalidValue, "/schemaVersion", "Value does not match the contract constant."));
            if (generated is not null && generated != "1970-01-01T00:00:00.0000000Z") d.Add(new(DiagnosticCodes.InvalidValue, "/generatedAt", "Value does not match the contract constant."));
            if (reportId is not null && !Digest.IsMatch(reportId)) d.Add(new(DiagnosticCodes.InvalidValue, "/reportId", "Digest is not valid.")); if (source is not null && !Digest.IsMatch(source)) d.Add(new(DiagnosticCodes.InvalidValue, "/advisorySourceDigest", "Digest is not valid."));
            var divergences = new List<AdvisoryDivergence>();
            if (!JsonValidationHelpers.TryRequired(root, "divergences", "", d, out var a)) { }
            else if (a.ValueKind != JsonValueKind.Array) d.Add(new(DiagnosticCodes.Type, "/divergences", "divergences must be an array."));
            else { var i = 0; foreach (var item in a.EnumerateArray()) { var p = $"/divergences/{i++}"; if (item.ValueKind != JsonValueKind.Object) { d.Add(new(DiagnosticCodes.Type, p, "Divergence must be an object.")); continue; } JsonValidationHelpers.RejectUnknown(item, p, ["obligationId", "ledgerStatus", "advisoryStatus", "diverged"], d); string R(string n) { if (!JsonValidationHelpers.TryRequired(item, n, p, d, out var v) || v.ValueKind != JsonValueKind.String) { d.Add(new(DiagnosticCodes.Type, p + "/" + n, $"{n} must be a string.")); return ""; } return v.GetString() ?? ""; } var id = R("obligationId"); var ls = R("ledgerStatus"); var ass = R("advisoryStatus"); if (!Statuses.Contains(ls)) d.Add(new(DiagnosticCodes.InvalidValue, p + "/ledgerStatus", "Status is not recognized.")); if (!Statuses.Contains(ass)) d.Add(new(DiagnosticCodes.InvalidValue, p + "/advisoryStatus", "Status is not recognized.")); var diverged = false; if (!JsonValidationHelpers.TryRequired(item, "diverged", p, d, out var b) || b.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) d.Add(new(DiagnosticCodes.Type, p + "/diverged", "diverged must be a boolean.")); else diverged = b.GetBoolean(); divergences.Add(new(id, ls, ass, diverged)); } }
            AdvisoryDivergenceRollup? rollup = null; if (root.TryGetProperty("rollup", out var ro)) { if (ro.ValueKind != JsonValueKind.Object) d.Add(new(DiagnosticCodes.Type, "/rollup", "rollup must be an object.")); else { JsonValidationHelpers.RejectUnknown(ro, "/rollup", ["totalObligations", "divergentCount", "allConverged"], d); int N(string n) { if (!ro.TryGetProperty(n, out var v)) return 0; if (!v.TryGetInt32(out var x) || x < 0) d.Add(new(DiagnosticCodes.Type, "/rollup/" + n, $"{n} must be a non-negative integer.")); return x; } var total = N("totalObligations"); var count = N("divergentCount"); var all = ro.TryGetProperty("allConverged", out var av) && av.ValueKind is JsonValueKind.True or JsonValueKind.False && av.GetBoolean(); rollup = new(total, count, all); } }
            return new(new(version ?? "", reportId ?? "", session ?? "", trace ?? "", source ?? "", generated ?? "", divergences, rollup), d);
        }
        catch (JsonException) { return Invalid(DiagnosticCodes.InvalidJson, "", "Input is not valid JSON."); }
    }
    private static AdvisoryDivergenceReportValidationResult Invalid(string code, string pointer, string message) => new(null, [new(code, pointer, message)]);
}
