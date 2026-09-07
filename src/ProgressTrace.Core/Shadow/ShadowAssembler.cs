using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;
using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Validation;

namespace ProgressTrace.Core.Shadow;

public sealed record ShadowSnapshotInput(long ShadowSequence, string TriggeringGateOutcomeId, AdvisoryResult AdvisoryResult);

public sealed record ShadowAssemblyResult(ShadowSessionSummary? Summary, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => Summary is not null && Diagnostics.All(d => d.Code == DiagnosticCodes.ShadowMissingRealDecision);
}

public static class ShadowAssembler
{
    public const string GeneratedAt = "1970-01-01T00:00:00.0000000Z";

    public static ShadowAssemblyResult Assemble(IReadOnlyList<ShadowSnapshotInput> inputs, RealDecisionRecord? decision)
    {
        if (inputs is null || inputs.Count == 0 || inputs.Any(i => i.AdvisoryResult is null || string.IsNullOrEmpty(i.TriggeringGateOutcomeId))) return Failure(DiagnosticCodes.ShadowAssemblyInvariant, "Shadow summary cannot be assembled deterministically.");
        for (var index = 1; index < inputs.Count; index++) if (inputs[index].ShadowSequence <= inputs[index - 1].ShadowSequence) return Failure(DiagnosticCodes.ShadowSequenceInvalid, "Shadow sequence must be strictly increasing.");
        if (inputs.Any(i => i.ShadowSequence < 0)) return Failure(DiagnosticCodes.ShadowSequenceInvalid, "Shadow sequence must be non-negative.");
        var first = inputs[0].AdvisoryResult;
        if (inputs.Any(i => i.AdvisoryResult.SessionId != first.SessionId || i.AdvisoryResult.TaskContractId != first.TaskContractId || i.AdvisoryResult.LedgerTraceId != first.LedgerTraceId) ||
            decision is not null && (decision.SessionId != first.SessionId || decision.TaskContractId != first.TaskContractId)) return Failure(DiagnosticCodes.ShadowReferenceMismatch, "Shadow input references do not match.");
        var snapshots = inputs.Select(i => new ShadowSnapshot(i.ShadowSequence, i.TriggeringGateOutcomeId, i.AdvisoryResult.SourceDigest, i.AdvisoryResult.Recommendation, i.AdvisoryResult.Classification)).ToArray();
        var final = snapshots[^1].Recommendation; var aligned = Align(final, decision?.Decision);
        var digest = SourceDigest(inputs, decision);
        var summary = new ShadowSessionSummary("1.0", first.SessionId, first.TaskContractId, snapshots, decision, final, aligned, GeneratedAt, digest);
        var normalized = ShadowSessionSummaryNormalizer.Normalize(summary); var validation = ShadowSessionSummaryValidator.ParseAndValidate(normalized);
        if (!validation.IsValid || !normalized.AsSpan().SequenceEqual(ShadowSessionSummaryNormalizer.Normalize(validation.ShadowSessionSummary!))) return Failure(DiagnosticCodes.ShadowAssemblyInvariant, "Shadow summary cannot be assembled deterministically.");
        return new(summary, decision is null ? [new(DiagnosticCodes.ShadowMissingRealDecision, "", "Real decision record is missing.")] : []);
    }

    private static bool? Align(string recommendation, string? decision) => recommendation switch
    {
        "insufficient-evidence" => null,
        _ when decision is null => null,
        "continue" => decision is "continued" or "escalated",
        "stop-recommended" => decision is "stopped" or "merged" or "rejected" or "abandoned" or "completed",
        _ => null
    };

    private static string SourceDigest(IReadOnlyList<ShadowSnapshotInput> inputs, RealDecisionRecord? decision)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject(); writer.WriteStartArray("snapshots");
            foreach (var input in inputs) { writer.WriteStartObject(); writer.WriteNumber("shadowSequence", input.ShadowSequence); writer.WriteString("triggeringGateOutcomeId", input.TriggeringGateOutcomeId); writer.WritePropertyName("advisoryResult"); writer.WriteRawValue(AdvisoryResultNormalizer.Normalize(input.AdvisoryResult), skipInputValidation: true); writer.WriteEndObject(); }
            writer.WriteEndArray(); writer.WritePropertyName("realDecision"); if (decision is null) writer.WriteNullValue(); else writer.WriteRawValue(RealDecisionRecordNormalizer.Normalize(decision), skipInputValidation: true); writer.WriteEndObject();
        }
        return Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    private static ShadowAssemblyResult Failure(string code, string message) => new(null, [new(code, "", message)]);
}
