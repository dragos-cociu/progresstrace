using System.Text.Json;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Validation;

public static class TraceValidator
{
    public const int MaximumInputSizeBytes = 16 * 1024 * 1024;

    private static readonly HashSet<string> Actors =
        new(StringComparer.Ordinal) { "system", "user", "assistant", "tool", "other" };

    public static ValidationResult ParseAndValidate(ReadOnlyMemory<byte> utf8Json)
    {
        var sizeValidation = ValidateInputSize(utf8Json.Length);
        if (!sizeValidation.IsValid)
        {
            return sizeValidation;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8Json);
        }
        catch (JsonException)
        {
            return Invalid(DiagnosticCodes.InvalidJson, "", "Input is not valid JSON.");
        }

        using (document)
        {
            var diagnostics = new List<Diagnostic>();
            var root = document.RootElement;
            RejectDuplicateProperties(root, "", diagnostics);
            if (root.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(Diagnostic(DiagnosticCodes.Type, "", "Envelope must be an object."));
                return new(null, diagnostics);
            }

            RejectUnknown(root, "", ["schemaVersion", "traceId", "source", "createdAt", "events"], diagnostics);
            var schemaVersion = ReadRequiredString(root, "schemaVersion", "", diagnostics);
            var traceId = ReadRequiredString(root, "traceId", "", diagnostics);
            if (schemaVersion is not null && schemaVersion != "1.0")
            {
                diagnostics.Add(Diagnostic(
                    DiagnosticCodes.UnsupportedSchemaVersion,
                    "/schemaVersion",
                    "Schema version is not supported."));
            }

            var source = ReadSource(root, diagnostics);
            var createdAt = ReadRequiredTimestamp(root, "createdAt", "", diagnostics);
            var events = ReadEvents(root, diagnostics);
            var envelope = new TraceEnvelope(schemaVersion, traceId, source, createdAt, events);
            return new(envelope, diagnostics);
        }
    }

    public static ValidationResult ValidateInputSize(long inputSizeBytes) =>
        inputSizeBytes <= MaximumInputSizeBytes
            ? new(null, [])
            : Invalid(
                DiagnosticCodes.InputTooLarge,
                "",
                $"Input exceeds the maximum size of {MaximumInputSizeBytes} bytes.");

    private static TraceSource? ReadSource(JsonElement root, List<Diagnostic> diagnostics)
    {
        if (!TryRequired(root, "source", "", diagnostics, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Diagnostic(DiagnosticCodes.Type, "/source", "Source must be an object."));
            return null;
        }

        RejectUnknown(value, "/source", ["name", "version"], diagnostics);
        return new(
            ReadRequiredString(value, "name", "/source", diagnostics),
            ReadRequiredString(value, "version", "/source", diagnostics));
    }

    private static IReadOnlyList<TraceEvent>? ReadEvents(JsonElement root, List<Diagnostic> diagnostics)
    {
        if (!TryRequired(root, "events", "", diagnostics, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(Diagnostic(DiagnosticCodes.Type, "/events", "Events must be an array."));
            return null;
        }

        var events = new List<TraceEvent>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        long? previousSequence = null;
        DateTimeOffset? previousTimestamp = null;
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var pointer = $"/events/{index}";
            if (item.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(Diagnostic(DiagnosticCodes.Type, pointer, "Event must be an object."));
                index++;
                continue;
            }

            RejectUnknown(item, pointer, ["id", "sequence", "timestamp", "type", "actor", "payload", "provenance"], diagnostics);
            var id = ReadRequiredString(item, "id", pointer, diagnostics);
            var sequence = ReadRequiredInteger(item, "sequence", pointer, diagnostics);
            var timestamp = ReadRequiredTimestamp(item, "timestamp", pointer, diagnostics);
            var type = ReadRequiredString(item, "type", pointer, diagnostics);
            var actor = ReadRequiredString(item, "actor", pointer, diagnostics);
            var payload = ReadPayload(item, pointer, diagnostics);
            var provenance = ReadProvenance(item, pointer, diagnostics);

            if (actor is not null && !Actors.Contains(actor))
            {
                diagnostics.Add(Diagnostic(DiagnosticCodes.InvalidValue, pointer + "/actor", "Actor is not recognized."));
            }

            if (id is not null && !ids.Add(id))
            {
                diagnostics.Add(Diagnostic(DiagnosticCodes.DuplicateEventId, pointer + "/id", "Event id must be unique."));
            }

            if (sequence is not null)
            {
                if (sequence < 0)
                {
                    diagnostics.Add(Diagnostic(DiagnosticCodes.InvalidValue, pointer + "/sequence", "Sequence must be non-negative."));
                }
                if (previousSequence is not null && sequence <= previousSequence)
                {
                    diagnostics.Add(Diagnostic(DiagnosticCodes.NonMonotonicSequence, pointer + "/sequence", "Sequence must be strictly increasing."));
                }
                previousSequence = sequence;
            }

            if (timestamp is not null)
            {
                if (previousTimestamp is not null && timestamp < previousTimestamp)
                {
                    diagnostics.Add(Diagnostic(DiagnosticCodes.NonMonotonicTimestamp, pointer + "/timestamp", "Timestamp must be nondecreasing."));
                }
                previousTimestamp = timestamp;
            }

            events.Add(new(id, sequence, timestamp, type, actor, payload, provenance));
            index++;
        }

        return events;
    }

    private static JsonElement ReadPayload(JsonElement item, string pointer, List<Diagnostic> diagnostics)
    {
        if (!TryRequired(item, "payload", pointer, diagnostics, out var value))
        {
            return default;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Diagnostic(DiagnosticCodes.Type, pointer + "/payload", "Payload must be an object."));
            return default;
        }

        return value.Clone();
    }

    private static EventProvenance? ReadProvenance(JsonElement item, string pointer, List<Diagnostic> diagnostics)
    {
        if (!TryRequired(item, "provenance", pointer, diagnostics, out var value))
        {
            return null;
        }

        var path = pointer + "/provenance";
        if (value.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Diagnostic(DiagnosticCodes.Type, path, "Provenance must be an object."));
            return null;
        }

        RejectUnknown(value, path, ["sourceEventId"], diagnostics);
        return new(ReadRequiredString(value, "sourceEventId", path, diagnostics));
    }

    private static string? ReadRequiredString(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics)
    {
        if (!TryRequired(parent, name, pointer, diagnostics, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            diagnostics.Add(Diagnostic(DiagnosticCodes.Type, Path(pointer, name), $"{name} must be a string."));
            return null;
        }

        var result = value.GetString();
        if (string.IsNullOrWhiteSpace(result))
        {
            diagnostics.Add(Diagnostic(DiagnosticCodes.InvalidValue, Path(pointer, name), $"{name} must not be empty."));
        }
        return result;
    }

    private static long? ReadRequiredInteger(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics)
    {
        if (!TryRequired(parent, name, pointer, diagnostics, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var result))
        {
            diagnostics.Add(Diagnostic(DiagnosticCodes.Type, Path(pointer, name), $"{name} must be an integer."));
            return null;
        }
        return result;
    }

    private static DateTimeOffset? ReadRequiredTimestamp(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics)
    {
        if (!TryRequired(parent, name, pointer, diagnostics, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            diagnostics.Add(Diagnostic(DiagnosticCodes.Type, Path(pointer, name), $"{name} must be a string."));
            return null;
        }

        if (!value.TryGetDateTimeOffset(out var result))
        {
            diagnostics.Add(Diagnostic(DiagnosticCodes.InvalidValue, Path(pointer, name), $"{name} must be an ISO 8601 date-time."));
            return null;
        }
        return result;
    }

    private static bool TryRequired(
        JsonElement parent,
        string name,
        string pointer,
        List<Diagnostic> diagnostics,
        out JsonElement value)
    {
        if (parent.TryGetProperty(name, out value))
        {
            return true;
        }

        diagnostics.Add(Diagnostic(DiagnosticCodes.Required, Path(pointer, name), $"Required property {name} is missing."));
        return false;
    }

    private static void RejectUnknown(
        JsonElement value,
        string pointer,
        string[] allowed,
        List<Diagnostic> diagnostics)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                diagnostics.Add(Diagnostic(
                    DiagnosticCodes.UnknownProperty,
                    Path(pointer, property.Name),
                    "Property is not allowed."));
            }
        }
    }

    private static void RejectDuplicateProperties(
        JsonElement value,
        string pointer,
        List<Diagnostic> diagnostics)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                var propertyPointer = Path(pointer, property.Name);
                if (!names.Add(property.Name))
                {
                    diagnostics.Add(Diagnostic(
                        DiagnosticCodes.DuplicateProperty,
                        propertyPointer,
                        "JSON object property names must be unique."));
                }
                RejectDuplicateProperties(property.Value, propertyPointer, diagnostics);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                RejectDuplicateProperties(item, pointer + "/" + index, diagnostics);
                index++;
            }
        }
    }

    private static string Path(string parent, string name) => parent + "/" + Escape(name);
    private static string Escape(string token) => token.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    private static Diagnostic Diagnostic(string code, string pointer, string message) => new(code, pointer, message);
    private static ValidationResult Invalid(string code, string pointer, string message) =>
        new(null, [Diagnostic(code, pointer, message)]);
}
