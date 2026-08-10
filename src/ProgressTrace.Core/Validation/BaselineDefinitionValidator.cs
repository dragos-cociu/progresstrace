using System.Text.Json;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Validation;

public static class BaselineDefinitionValidator
{
    private static readonly HashSet<string> ProducerTypes = new(StringComparer.Ordinal)
        { "agent", "harness", "operator", "adapter" };
    private static readonly HashSet<string> EvidenceBases = new(StringComparer.Ordinal)
        { "agent-estimate", "harness-policy", "operator-estimate", "historical-analysis", "adapter-inference" };

    public static BaselineDefinitionValidationResult ParseAndValidate(
        ReadOnlyMemory<byte> json, TraceEnvelope trace, ObligationLedger ledger)
    {
        if (json.Length > TraceValidator.MaximumInputSizeBytes)
            return Invalid(DiagnosticCodes.InputTooLarge, "", $"Input exceeds the maximum size of {TraceValidator.MaximumInputSizeBytes} bytes.");
        JsonDocument document;
        try { document = JsonDocument.Parse(json); }
        catch (JsonException) { return Invalid(DiagnosticCodes.InvalidJson, "", "Input is not valid JSON."); }
        using (document)
        {
            var diagnostics = new List<Diagnostic>();
            var root = document.RootElement;
            JsonValidationHelpers.RejectDuplicateProperties(root, "", diagnostics);
            if (root.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(new(DiagnosticCodes.Type, "", "BaselineDefinition must be an object."));
                return new(null, diagnostics);
            }
            JsonValidationHelpers.RejectUnknown(root, "", ["schemaVersion", "traceId", "baselineSource", "obligationBudgets"], diagnostics);
            var version = ReadString(root, "schemaVersion", "", diagnostics);
            var traceId = ReadString(root, "traceId", "", diagnostics);
            var sourceElement = ReadObject(root, "baselineSource", "", diagnostics);
            var budgetsElement = ReadArray(root, "obligationBudgets", "", diagnostics);
            if (version is not null && version != "1.0")
                diagnostics.Add(new(DiagnosticCodes.UnsupportedSchemaVersion, "/schemaVersion", "Schema version is not supported."));
            CheckNonEmpty(traceId, "/traceId", "traceId", diagnostics);

            string? producerType = null, producerName = null, producerVersion = null, evidenceBasis = null;
            var producerVersionNull = false;
            if (sourceElement is { } source)
            {
                JsonValidationHelpers.RejectUnknown(source, "/baselineSource", ["producerType", "producerName", "producerVersion", "evidenceBasis"], diagnostics);
                producerType = ReadString(source, "producerType", "/baselineSource", diagnostics);
                producerName = ReadString(source, "producerName", "/baselineSource", diagnostics);
                if (JsonValidationHelpers.TryRequired(source, "producerVersion", "/baselineSource", diagnostics, out var pv))
                {
                    producerVersionNull = pv.ValueKind == JsonValueKind.Null;
                    if (!producerVersionNull)
                    {
                        if (pv.ValueKind == JsonValueKind.String) producerVersion = pv.GetString();
                        else diagnostics.Add(new(DiagnosticCodes.Type, "/baselineSource/producerVersion", "producerVersion must be a string or null."));
                    }
                }
                evidenceBasis = ReadString(source, "evidenceBasis", "/baselineSource", diagnostics);
                var producerRecognized = producerType is not null && ProducerTypes.Contains(producerType);
                var evidenceRecognized = evidenceBasis is not null && EvidenceBases.Contains(evidenceBasis);
                if (producerType is not null && !producerRecognized)
                    diagnostics.Add(new(DiagnosticCodes.InvalidValue, "/baselineSource/producerType", "Producer type is not recognized."));
                CheckNonEmpty(producerName, "/baselineSource/producerName", "producerName", diagnostics);
                if (!producerVersionNull) CheckNonEmpty(producerVersion, "/baselineSource/producerVersion", "producerVersion", diagnostics);
                if (evidenceBasis is not null && !evidenceRecognized)
                    diagnostics.Add(new(DiagnosticCodes.InvalidValue, "/baselineSource/evidenceBasis", "Evidence basis is not recognized."));
                if (producerRecognized && evidenceRecognized)
                {
                    AddCoherence(producerType == "agent" && evidenceBasis != "agent-estimate", diagnostics);
                    AddCoherence(producerType == "operator" && evidenceBasis is not ("operator-estimate" or "historical-analysis"), diagnostics);
                    AddCoherence(evidenceBasis == "harness-policy" && producerType != "harness", diagnostics);
                    AddCoherence(evidenceBasis == "adapter-inference" && producerType != "adapter", diagnostics);
                    AddCoherence(evidenceBasis == "historical-analysis" && producerType is not ("harness" or "operator" or "adapter"), diagnostics);
                }
            }

            var budgets = new List<ObligationBudget>();
            if (budgetsElement is { } array)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var index = 0;
                foreach (var entry in array.EnumerateArray())
                {
                    var pointer = $"/obligationBudgets/{index}";
                    if (entry.ValueKind != JsonValueKind.Object)
                    {
                        diagnostics.Add(new(DiagnosticCodes.Type, pointer, "obligationBudgets entry must be an object."));
                        budgets.Add(new(null, null)); index++; continue;
                    }
                    JsonValidationHelpers.RejectUnknown(entry, pointer, ["obligationId", "eventBudget"], diagnostics);
                    var obligationId = ReadString(entry, "obligationId", pointer, diagnostics);
                    int? eventBudget = null;
                    if (JsonValidationHelpers.TryRequired(entry, "eventBudget", pointer, diagnostics, out var eb))
                    {
                        if (eb.ValueKind == JsonValueKind.Number && eb.TryGetDecimal(out var number) && decimal.Truncate(number) == number)
                        {
                            if (number is >= 1 and <= int.MaxValue) eventBudget = (int)number;
                            else diagnostics.Add(new(DiagnosticCodes.InvalidValue, pointer + "/eventBudget", "eventBudget must be an integer between 1 and 2147483647 inclusive."));
                        }
                        else diagnostics.Add(new(DiagnosticCodes.Type, pointer + "/eventBudget", "eventBudget must be an integer."));
                    }
                    CheckNonEmpty(obligationId, pointer + "/obligationId", "obligationId", diagnostics);
                    if (obligationId is not null && !seen.Add(obligationId))
                        diagnostics.Add(new(DiagnosticCodes.DuplicateBaselineObligationId, pointer + "/obligationId", "Obligation id must be unique within the baseline definition."));
                    budgets.Add(new(obligationId, eventBudget));
                    index++;
                }
            }
            var baseline = new BaselineDefinition(version, traceId,
                sourceElement is null ? null : new(producerType, producerName, producerVersion, evidenceBasis), budgetsElement is null ? null : budgets);

            if (!string.IsNullOrWhiteSpace(traceId) && traceId != trace.TraceId)
                diagnostics.Add(new(DiagnosticCodes.BaselineTraceIdMismatch, "/traceId", "BaselineDefinition traceId must equal the paired trace envelope's traceId."));
            var ledgerIds = ledger.Obligations!.Select(o => o.Id!).ToHashSet(StringComparer.Ordinal);
            var covered = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < budgets.Count; i++)
            {
                var id = budgets[i].ObligationId;
                if (id is null) continue;
                if (!ledgerIds.Contains(id))
                    diagnostics.Add(new(DiagnosticCodes.DanglingBaselineObligationId, $"/obligationBudgets/{i}/obligationId", "Baseline obligationBudgets entry does not reference a declared ledger obligation."));
                else covered.Add(id);
            }
            foreach (var obligation in ledger.Obligations!)
                if (!covered.Contains(obligation.Id!)) diagnostics.Add(new(DiagnosticCodes.MissingBaselineObligationCoverage, "/obligationBudgets", "Baseline obligationBudgets is missing an entry for a declared ledger obligation."));
            return new(baseline, diagnostics);
        }
    }

    private static void AddCoherence(bool violated, List<Diagnostic> diagnostics)
    {
        if (violated) diagnostics.Add(new(DiagnosticCodes.BaselineSourceCoherence, "/baselineSource", "BaselineDefinition baselineSource is not coherent with the declared evidence basis."));
    }
    private static JsonElement? ReadObject(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics)
    {
        if (!JsonValidationHelpers.TryRequired(parent, name, pointer, diagnostics, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Object) return value;
        diagnostics.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be an object.")); return null;
    }
    private static JsonElement? ReadArray(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics)
    {
        if (!JsonValidationHelpers.TryRequired(parent, name, pointer, diagnostics, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Array) return value;
        diagnostics.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be an array.")); return null;
    }
    private static string? ReadString(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics)
    {
        if (!JsonValidationHelpers.TryRequired(parent, name, pointer, diagnostics, out var value)) return null;
        if (value.ValueKind == JsonValueKind.String) return value.GetString();
        diagnostics.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be a string.")); return null;
    }
    private static void CheckNonEmpty(string? value, string pointer, string name, List<Diagnostic> diagnostics)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value)) diagnostics.Add(new(DiagnosticCodes.InvalidValue, pointer, $"{name} must not be empty."));
    }
    private static BaselineDefinitionValidationResult Invalid(string code, string pointer, string message) => new(null, [new(code, pointer, message)]);
}
