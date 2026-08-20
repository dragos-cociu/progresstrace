using System.Buffers;
using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Normalization;

public static class TokenUsageNormalizer
{
    public static byte[] Normalize(TokenUsage usage)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject(); writer.WriteString("schemaVersion", usage.SchemaVersion); writer.WriteString("sessionId", usage.SessionId); writer.WriteString("taskContractId", usage.TaskContractId); writer.WriteStartArray("records");
            foreach (var item in usage.Records ?? [])
            {
                writer.WriteStartObject(); writer.WriteString("sessionId", item.SessionId); writer.WriteString("invocationId", item.InvocationId); writer.WriteNumber("sequence", item.Sequence!.Value); writer.WriteNumber("tokensTotal", item.TokensTotal!.Value); writer.WriteString("producerType", item.ProducerType); writer.WriteString("producerName", item.ProducerName); if (item.ProducerVersion is null) writer.WriteNull("producerVersion"); else writer.WriteString("producerVersion", item.ProducerVersion); writer.WriteString("evidenceBasis", item.EvidenceBasis); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        return [.. buffer.WrittenSpan, (byte)'\n'];
    }
}
