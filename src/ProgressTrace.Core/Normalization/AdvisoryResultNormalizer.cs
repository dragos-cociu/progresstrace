using System.Buffers;
using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Normalization;

public static class AdvisoryResultNormalizer
{
    public static byte[] Normalize(AdvisoryResult result)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject(); writer.WriteString("schemaVersion", result.SchemaVersion); writer.WriteString("sessionId", result.SessionId); writer.WriteString("taskContractId", result.TaskContractId); writer.WriteString("ledgerTraceId", result.LedgerTraceId); writer.WriteString("generatedAt", result.GeneratedAt); writer.WriteString("sourceDigest", result.SourceDigest); writer.WriteString("classification", result.Classification); writer.WriteString("recommendation", result.Recommendation); writer.WriteStartArray("obligations");
            foreach (var item in result.Obligations) { writer.WriteStartObject(); writer.WriteString("obligationId", item.ObligationId); writer.WriteString("status", item.Status); writer.WriteString("classification", item.Classification); writer.WriteBoolean("stable", item.Stable); writer.WriteStartArray("evidenceEventIds"); foreach (var id in item.EvidenceEventIds) writer.WriteStringValue(id); writer.WriteEndArray(); writer.WriteEndObject(); }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        return [.. buffer.WrittenSpan, (byte)'\n'];
    }
}
