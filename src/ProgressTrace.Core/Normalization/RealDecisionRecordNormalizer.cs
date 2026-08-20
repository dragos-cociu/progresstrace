using System.Buffers;
using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Normalization;

public static class RealDecisionRecordNormalizer
{
    public static byte[] Normalize(RealDecisionRecord record)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", record.SchemaVersion);
            writer.WriteString("sessionId", record.SessionId);
            writer.WriteString("taskContractId", record.TaskContractId);
            writer.WriteString("decision", record.Decision);
            writer.WriteString("decidedAt", record.DecidedAt);
            writer.WriteString("source", record.Source);
            writer.WriteEndObject();
        }
        return [.. buffer.WrittenSpan, (byte)'\n'];
    }
}
