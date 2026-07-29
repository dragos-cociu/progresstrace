using System.Buffers;
using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Normalization;

public static class EvaluationResultNormalizer
{
    public static byte[] Normalize(EvaluationResult result)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", result.SchemaVersion);
            writer.WriteString("traceId", result.TraceId);
            writer.WritePropertyName("algorithm"); writer.WriteStartObject();
            writer.WriteString("name", result.Algorithm.Name); writer.WriteString("version", result.Algorithm.Version); writer.WriteEndObject();
            writer.WritePropertyName("obligationResults"); writer.WriteStartArray();
            foreach (var item in result.ObligationResults)
            {
                writer.WriteStartObject(); writer.WriteString("obligationId", item.ObligationId);
                writer.WriteString("classification", item.Classification);
                writer.WritePropertyName("evidenceEventIds"); writer.WriteStartArray();
                foreach (var id in item.EvidenceEventIds) writer.WriteStringValue(id);
                writer.WriteEndArray(); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteString("traceClassification", result.TraceClassification); writer.WriteEndObject();
        }
        var output = new byte[buffer.WrittenCount + 1]; buffer.WrittenSpan.CopyTo(output); output[^1] = (byte)'\n'; return output;
    }
}
