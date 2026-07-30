using System.Text.Json;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Validation;

public static class TerminationDeclarationValidator
{
    private static readonly HashSet<string> TerminationKinds = new(StringComparer.Ordinal)
    {
        "agent-self-reported-stop", "harness-declared-stop", "natural-completion",
        "external-cancellation", "timeout", "crash-or-error", "capture-truncated", "unknown"
    };
    private static readonly HashSet<string> ProducerTypes = new(StringComparer.Ordinal)
        { "agent", "harness", "operator", "adapter" };
    private static readonly HashSet<string> EvidenceBases = new(StringComparer.Ordinal)
        { "agent-output", "harness-lifecycle", "operator-annotation", "adapter-inference" };

    public static TerminationDeclarationValidationResult ParseAndValidate(ReadOnlyMemory<byte> json, TraceEnvelope trace)
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
                diagnostics.Add(new(DiagnosticCodes.Type, "", "TerminationDeclaration must be an object."));
                return new(null, diagnostics);
            }
            JsonValidationHelpers.RejectUnknown(root, "", ["schemaVersion", "traceId", "terminationEventId", "terminationKind", "declarationSource"], diagnostics);
            var version = ReadString(root, "schemaVersion", "", diagnostics);
            var traceId = ReadString(root, "traceId", "", diagnostics);
            var eventId = ReadString(root, "terminationEventId", "", diagnostics);
            var kind = ReadString(root, "terminationKind", "", diagnostics);
            var sourceElement = ReadObject(root, "declarationSource", "", diagnostics);

            if (version is not null && version != "1.0")
                diagnostics.Add(new(DiagnosticCodes.UnsupportedSchemaVersion, "/schemaVersion", "Schema version is not supported."));
            CheckNonEmpty(traceId, "/traceId", "traceId", diagnostics);
            CheckNonEmpty(eventId, "/terminationEventId", "terminationEventId", diagnostics);
            var kindRecognized = kind is not null && TerminationKinds.Contains(kind);
            if (kind is not null && !kindRecognized)
                diagnostics.Add(new(DiagnosticCodes.InvalidValue, "/terminationKind", "Termination kind is not recognized."));

            string? producerType = null, producerName = null, producerVersion = null, evidenceBasis = null;
            bool producerVersionNull = false;
            if (sourceElement is { } source)
            {
                JsonValidationHelpers.RejectUnknown(source, "/declarationSource",
                    ["producerType", "producerName", "producerVersion", "evidenceBasis"], diagnostics);
                producerType = ReadString(source, "producerType", "/declarationSource", diagnostics);
                producerName = ReadString(source, "producerName", "/declarationSource", diagnostics);
                if (JsonValidationHelpers.TryRequired(source, "producerVersion", "/declarationSource", diagnostics, out var pv))
                {
                    producerVersionNull = pv.ValueKind == JsonValueKind.Null;
                    if (!producerVersionNull)
                    {
                        if (pv.ValueKind == JsonValueKind.String) producerVersion = pv.GetString();
                        else diagnostics.Add(new(DiagnosticCodes.Type, "/declarationSource/producerVersion", "producerVersion must be a string or null."));
                    }
                }
                evidenceBasis = ReadString(source, "evidenceBasis", "/declarationSource", diagnostics);
                var producerRecognized = producerType is not null && ProducerTypes.Contains(producerType);
                var evidenceRecognized = evidenceBasis is not null && EvidenceBases.Contains(evidenceBasis);
                if (producerType is not null && !producerRecognized)
                    diagnostics.Add(new(DiagnosticCodes.InvalidValue, "/declarationSource/producerType", "Producer type is not recognized."));
                CheckNonEmpty(producerName, "/declarationSource/producerName", "producerName", diagnostics);
                if (!producerVersionNull) CheckNonEmpty(producerVersion, "/declarationSource/producerVersion", "producerVersion", diagnostics);
                if (evidenceBasis is not null && !evidenceRecognized)
                    diagnostics.Add(new(DiagnosticCodes.InvalidValue, "/declarationSource/evidenceBasis", "Evidence basis is not recognized."));
                if (kindRecognized && producerRecognized && evidenceRecognized)
                {
                    AddCoherence(kind == "agent-self-reported-stop" && evidenceBasis != "agent-output", "/declarationSource/evidenceBasis", diagnostics);
                    AddCoherence(kind == "harness-declared-stop" && evidenceBasis != "harness-lifecycle", "/declarationSource/evidenceBasis", diagnostics);
                    AddCoherence(producerType == "agent" && evidenceBasis != "agent-output", "/declarationSource", diagnostics);
                    AddCoherence(producerType == "operator" && evidenceBasis != "operator-annotation", "/declarationSource", diagnostics);
                    AddCoherence(evidenceBasis == "adapter-inference" && producerType != "adapter", "/declarationSource", diagnostics);
                }
            }
            var declaration = new TerminationDeclaration(version, traceId, eventId, kind,
                sourceElement is null ? null : new(producerType, producerName, producerVersion, evidenceBasis));
            if (!string.IsNullOrWhiteSpace(traceId) && traceId != trace.TraceId)
                diagnostics.Add(new(DiagnosticCodes.DeclarationTraceIdMismatch, "/traceId",
                    "TerminationDeclaration traceId must equal the paired trace envelope's traceId."));
            if (!string.IsNullOrWhiteSpace(eventId))
            {
                var ordered = trace.Events!.OrderBy(e => e.Sequence).ThenBy(e => e.Timestamp).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
                var rank = ordered.FindIndex(e => e.Id == eventId);
                if (rank < 0)
                    diagnostics.Add(new(DiagnosticCodes.DanglingTerminationEventId, "/terminationEventId",
                        "TerminationDeclaration terminationEventId does not reference an event in the paired trace envelope."));
                else if (rank != ordered.Count - 1)
                    diagnostics.Add(new(DiagnosticCodes.NonTerminalTerminationEventId, "/terminationEventId",
                        "TerminationDeclaration terminationEventId must reference the trace's terminal event."));
            }
            return new(declaration, diagnostics);
        }
    }

    private static void AddCoherence(bool violated, string pointer, List<Diagnostic> diagnostics)
    {
        if (violated) diagnostics.Add(new(DiagnosticCodes.DeclarationSourceCoherence, pointer,
            "TerminationDeclaration declarationSource is not coherent with the declared termination."));
    }

    private static JsonElement? ReadObject(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics)
    {
        if (!JsonValidationHelpers.TryRequired(parent, name, pointer, diagnostics, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Object) return value;
        diagnostics.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be an object."));
        return null;
    }

    private static string? ReadString(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics)
    {
        if (!JsonValidationHelpers.TryRequired(parent, name, pointer, diagnostics, out var value)) return null;
        if (value.ValueKind == JsonValueKind.String) return value.GetString();
        diagnostics.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be a string."));
        return null;
    }

    private static void CheckNonEmpty(string? value, string pointer, string name, List<Diagnostic> diagnostics)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
            diagnostics.Add(new(DiagnosticCodes.InvalidValue, pointer, $"{name} must not be empty."));
    }

    private static TerminationDeclarationValidationResult Invalid(string code, string pointer, string message) =>
        new(null, [new(code, pointer, message)]);
}
