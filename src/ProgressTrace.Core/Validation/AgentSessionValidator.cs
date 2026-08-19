using System.Text.Json;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Validation;

public static class AgentSessionValidator
{
    public static AgentSessionValidationResult ParseAndValidate(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > TraceValidator.MaximumInputSizeBytes)
            return new(null, [new("PT500", "", "Session input exceeds the maximum size.")]);
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            var diagnostics = new List<Diagnostic>();
            if (root.ValueKind != JsonValueKind.Object) return Invalid(diagnostics, "", "Session must be an object.");
            JsonValidationHelpers.RejectDuplicateProperties(root, "", diagnostics);
            JsonValidationHelpers.RejectUnknown(root, "", ["schemaVersion", "sessionId", "taskContractId", "invocations"], diagnostics);
            var version = String(root, "schemaVersion", "", diagnostics);
            var sessionId = String(root, "sessionId", "", diagnostics);
            var taskContractId = String(root, "taskContractId", "", diagnostics);
            if (version is not null && version != "1.0") diagnostics.Add(new("PT501", "/schemaVersion", "Session schema version is not supported."));
            var invocations = ReadInvocations(root, diagnostics);
            if (invocations is not null)
            {
                long? previousSequence = null;
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in invocations)
                {
                    if (item.InvocationId is not null && !ids.Add(item.InvocationId)) diagnostics.Add(new(DiagnosticCodes.DuplicateInvocationId, "/invocations", "Invocation identifiers must be unique."));
                    if (item.Sequence is not null && previousSequence is not null && item.Sequence <= previousSequence) diagnostics.Add(new(DiagnosticCodes.NonMonotonicInvocationSequence, "/invocations", "Invocation sequence must be strictly increasing."));
                    previousSequence = item.Sequence;
                    if (item.Attempt is not null && item.Attempt < 1) diagnostics.Add(new(DiagnosticCodes.InvalidAttempt, "/invocations", "Attempt must be positive."));
                    if (item.StartedAt is not null && item.EndedAt is not null && item.EndedAt < item.StartedAt) diagnostics.Add(new(DiagnosticCodes.InvalidInvocationWindow, "/invocations", "Invocation end must not precede start."));
                    if (item.ObligationIds is not null && item.ObligationIds.Any(string.IsNullOrWhiteSpace)) diagnostics.Add(new(DiagnosticCodes.EmptyObligationId, "/invocations", "Obligation identifiers must not be empty."));
                }
            }
            var session = new AgentSession(version, sessionId, taskContractId, invocations);
            return new(session, diagnostics);
        }
        catch (JsonException) { return new(null, [new(DiagnosticCodes.InvalidJson, "", "Input is not valid JSON.")]); }
    }

    private static IReadOnlyList<SessionInvocation>? ReadInvocations(JsonElement root, List<Diagnostic> diagnostics)
    {
        if (!root.TryGetProperty("invocations", out var value)) { diagnostics.Add(new(DiagnosticCodes.Required, "/invocations", "Required property invocations is missing.")); return null; }
        if (value.ValueKind != JsonValueKind.Array) { diagnostics.Add(new(DiagnosticCodes.Type, "/invocations", "Invocations must be an array.")); return null; }
        var result = new List<SessionInvocation>(); var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var p = $"/invocations/{index++}";
            if (item.ValueKind != JsonValueKind.Object) { diagnostics.Add(new(DiagnosticCodes.Type, p, "Invocation must be an object.")); continue; }
            JsonValidationHelpers.RejectUnknown(item, p, ["invocationId", "sequence", "attempt", "traceId", "obligationIds", "startedAt", "endedAt"], diagnostics);
            var id = String(item, "invocationId", p, diagnostics); var sequence = Integer(item, "sequence", p, diagnostics); var attempt = Integer(item, "attempt", p, diagnostics);
            var traceId = String(item, "traceId", p, diagnostics); var obligations = Strings(item, "obligationIds", p, diagnostics);
            var started = Timestamp(item, "startedAt", p, diagnostics); var ended = Timestamp(item, "endedAt", p, diagnostics);
            var safeAttempt = attempt is >= 1 and <= int.MaxValue ? (int?)attempt.Value : null;
            result.Add(new(id, sequence, safeAttempt, traceId, obligations, started, ended));
        }
        return result;
    }

    internal static string? String(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics, bool optional = false)
    {
        if (!parent.TryGetProperty(name, out var value)) { if (!optional) diagnostics.Add(new(DiagnosticCodes.Required, Pointer(pointer, name), $"Required property {name} is missing.")); return null; }
        if (value.ValueKind != JsonValueKind.String) { diagnostics.Add(new(DiagnosticCodes.Type, Pointer(pointer, name), $"{name} must be a string.")); return null; }
        var result = value.GetString(); if (string.IsNullOrWhiteSpace(result)) diagnostics.Add(new(DiagnosticCodes.InvalidValue, Pointer(pointer, name), $"{name} must not be empty.")); return result;
    }

    internal static long? Integer(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics)
    {
        if (!parent.TryGetProperty(name, out var value)) { diagnostics.Add(new(DiagnosticCodes.Required, Pointer(pointer, name), $"Required property {name} is missing.")); return null; }
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var result)) { diagnostics.Add(new(DiagnosticCodes.Type, Pointer(pointer, name), $"{name} must be an integer.")); return null; }
        return result;
    }

    internal static DateTimeOffset? Timestamp(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics)
    {
        if (!parent.TryGetProperty(name, out var value)) { diagnostics.Add(new(DiagnosticCodes.Required, Pointer(pointer, name), $"Required property {name} is missing.")); return null; }
        if (value.ValueKind != JsonValueKind.String || !value.TryGetDateTimeOffset(out var result)) { diagnostics.Add(new(DiagnosticCodes.InvalidValue, Pointer(pointer, name), $"{name} must be an ISO 8601 date-time.")); return null; }
        return result;
    }

    private static IReadOnlyList<string>? Strings(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics)
    {
        if (!parent.TryGetProperty(name, out var value)) { diagnostics.Add(new(DiagnosticCodes.Required, Pointer(pointer, name), $"Required property {name} is missing.")); return null; }
        if (value.ValueKind != JsonValueKind.Array) { diagnostics.Add(new(DiagnosticCodes.Type, Pointer(pointer, name), $"{name} must be an array.")); return null; }
        var result = new List<string>(); foreach (var item in value.EnumerateArray()) { if (item.ValueKind != JsonValueKind.String) diagnostics.Add(new(DiagnosticCodes.Type, Pointer(pointer, name), "Array values must be strings.")); else result.Add(item.GetString()!); }
        return result;
    }

    private static AgentSessionValidationResult Invalid(List<Diagnostic> diagnostics, string pointer, string message) { diagnostics.Add(new(DiagnosticCodes.Type, pointer, message)); return new(null, diagnostics); }
    internal static string Pointer(string parent, string name) => parent + "/" + name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
