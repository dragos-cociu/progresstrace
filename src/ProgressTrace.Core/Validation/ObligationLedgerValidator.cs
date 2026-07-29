using System.Text.Json;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Validation;

public static class ObligationLedgerValidator
{
    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
        { "open", "in-progress", "satisfied", "regressed", "abandoned" };

    public static ObligationValidationResult ParseAndValidate(ReadOnlyMemory<byte> json, TraceEnvelope trace)
    {
        if (json.Length > TraceValidator.MaximumInputSizeBytes)
        {
            return Invalid(DiagnosticCodes.InputTooLarge, "",
                $"Input exceeds the maximum size of {TraceValidator.MaximumInputSizeBytes} bytes.");
        }
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
                diagnostics.Add(new(DiagnosticCodes.Type, "", "Ledger must be an object."));
                return new(null, diagnostics);
            }
            JsonValidationHelpers.RejectUnknown(root, "", ["schemaVersion", "traceId", "obligations", "signals"], diagnostics);
            var version = ReadString(root, "schemaVersion", "", diagnostics, false);
            var traceId = ReadString(root, "traceId", "", diagnostics, false);
            var obligationsElement = ReadArray(root, "obligations", "", diagnostics);
            var signalsElement = ReadArray(root, "signals", "", diagnostics);
            if (version is not null && version != "1.0")
                diagnostics.Add(new(DiagnosticCodes.UnsupportedSchemaVersion, "/schemaVersion", "Schema version is not supported."));
            if (traceId is not null && string.IsNullOrWhiteSpace(traceId))
                diagnostics.Add(new(DiagnosticCodes.InvalidValue, "/traceId", "traceId must not be empty."));

            var obligations = new List<Obligation>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (obligationsElement is { } oe)
            {
                if (oe.GetArrayLength() == 0)
                    diagnostics.Add(new(DiagnosticCodes.InvalidValue, "/obligations", "Obligations must contain at least one entry."));
                var index = 0;
                foreach (var item in oe.EnumerateArray())
                {
                    var pointer = $"/obligations/{index}";
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        diagnostics.Add(new(DiagnosticCodes.Type, pointer, "Obligation must be an object."));
                        index++; continue;
                    }
                    JsonValidationHelpers.RejectUnknown(item, pointer, ["id", "description"], diagnostics);
                    var id = ReadString(item, "id", pointer, diagnostics, true);
                    var description = ReadString(item, "description", pointer, diagnostics, false);
                    if (id is not null && !ids.Add(id))
                        diagnostics.Add(new(DiagnosticCodes.DuplicateObligationId, pointer + "/id", "Obligation id must be unique within the ledger."));
                    obligations.Add(new(id, description)); index++;
                }
            }

            var signals = new List<ObligationSignal>();
            if (signalsElement is { } se)
            {
                var index = 0;
                foreach (var item in se.EnumerateArray())
                {
                    var pointer = $"/signals/{index}";
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        diagnostics.Add(new(DiagnosticCodes.Type, pointer, "Signal must be an object."));
                        index++; continue;
                    }
                    JsonValidationHelpers.RejectUnknown(item, pointer, ["obligationId", "eventId", "status"], diagnostics);
                    var obligationId = ReadString(item, "obligationId", pointer, diagnostics, true);
                    var eventId = ReadString(item, "eventId", pointer, diagnostics, true);
                    var status = ReadString(item, "status", pointer, diagnostics, false);
                    if (status is not null && !Statuses.Contains(status))
                        diagnostics.Add(new(DiagnosticCodes.InvalidValue, pointer + "/status", "Status is not recognized."));
                    signals.Add(new(obligationId, eventId, status, index)); index++;
                }
            }

            var ledger = new ObligationLedger(version, traceId, obligations, signals);
            if (!string.IsNullOrWhiteSpace(traceId) && traceId != trace.TraceId)
                diagnostics.Add(new(DiagnosticCodes.TraceIdMismatch, "/traceId", "Ledger traceId must equal the paired trace envelope's traceId."));
            var eventSequence = (trace.Events ?? []).Where(e => e.Id is not null && e.Sequence is not null)
                .ToDictionary(e => e.Id!, e => e.Sequence!.Value, StringComparer.Ordinal);
            foreach (var signal in signals)
            {
                if (signal.ObligationId is not null && !ids.Contains(signal.ObligationId))
                    diagnostics.Add(new(DiagnosticCodes.DanglingObligationId, $"/signals/{signal.Index}/obligationId", "Signal obligationId does not reference a declared obligation."));
                if (signal.EventId is not null && !eventSequence.ContainsKey(signal.EventId))
                    diagnostics.Add(new(DiagnosticCodes.DanglingEventId, $"/signals/{signal.Index}/eventId", "Signal eventId does not reference an event in the paired trace envelope."));
            }
            foreach (var obligation in obligations)
            {
                var ordered = signals.Where(s => s.ObligationId == obligation.Id && s.EventId is not null && eventSequence.ContainsKey(s.EventId))
                    .OrderBy(s => eventSequence[s.EventId!]).ThenBy(s => s.EventId, StringComparer.Ordinal).ThenBy(s => s.Index).ToList();
                var abandoned = ordered.FindIndex(s => s.Status == "abandoned");
                if (abandoned >= 0)
                    foreach (var later in ordered.Skip(abandoned + 1))
                        diagnostics.Add(new(DiagnosticCodes.AbandonedTerminal, $"/signals/{later.Index}", "Signal violates the abandoned-terminal rule."));
            }
            return new(ledger, diagnostics);
        }
    }

    private static JsonElement? ReadArray(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics)
    {
        if (!JsonValidationHelpers.TryRequired(parent, name, pointer, diagnostics, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Array) return value;
        diagnostics.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be an array."));
        return null;
    }

    private static string? ReadString(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics, bool nonEmpty)
    {
        if (!JsonValidationHelpers.TryRequired(parent, name, pointer, diagnostics, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String)
        {
            diagnostics.Add(new(DiagnosticCodes.Type, JsonValidationHelpers.Path(pointer, name), $"{name} must be a string."));
            return null;
        }
        var result = value.GetString();
        if (nonEmpty && string.IsNullOrWhiteSpace(result))
            diagnostics.Add(new(DiagnosticCodes.InvalidValue, JsonValidationHelpers.Path(pointer, name), $"{name} must not be empty."));
        return result;
    }

    private static ObligationValidationResult Invalid(string code, string pointer, string message) =>
        new(null, [new(code, pointer, message)]);
}
