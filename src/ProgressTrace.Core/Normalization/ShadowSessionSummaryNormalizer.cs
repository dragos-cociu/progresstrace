using System.Buffers;
using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Normalization;

public static class ShadowSessionSummaryNormalizer
{
    public static byte[] Normalize(ShadowSessionSummary summary)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject(); writer.WriteString("schemaVersion", summary.SchemaVersion); writer.WriteString("sessionId", summary.SessionId); writer.WriteString("taskContractId", summary.TaskContractId); writer.WriteStartArray("snapshots");
            foreach (var snapshot in summary.Snapshots) { writer.WriteStartObject(); writer.WriteNumber("shadowSequence", snapshot.ShadowSequence); writer.WriteString("triggeringGateOutcomeId", snapshot.TriggeringGateOutcomeId); writer.WriteString("advisorySourceDigest", snapshot.AdvisorySourceDigest); writer.WriteString("recommendation", snapshot.Recommendation); writer.WriteString("classification", snapshot.Classification); writer.WriteEndObject(); }
            writer.WriteEndArray();
            if (summary.RealDecision is null) writer.WriteNull("realDecision");
            else { writer.WriteStartObject("realDecision"); writer.WriteString("schemaVersion", summary.RealDecision.SchemaVersion); writer.WriteString("sessionId", summary.RealDecision.SessionId); writer.WriteString("taskContractId", summary.RealDecision.TaskContractId); writer.WriteString("decision", summary.RealDecision.Decision); writer.WriteString("decidedAt", summary.RealDecision.DecidedAt); writer.WriteString("source", summary.RealDecision.Source); writer.WriteEndObject(); }
            writer.WriteString("finalRecommendation", summary.FinalRecommendation); if (summary.Aligned is null) writer.WriteNull("aligned"); else writer.WriteBoolean("aligned", summary.Aligned.Value); writer.WriteString("generatedAt", summary.GeneratedAt); writer.WriteString("sourceDigest", summary.SourceDigest); writer.WriteEndObject();
        }
        return [.. buffer.WrittenSpan, (byte)'\n'];
    }
}
