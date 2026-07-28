using System.Buffers;
using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Normalization;

public static class TraceNormalizer
{
    public static byte[] Normalize(TraceEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope.Source);
        ArgumentNullException.ThrowIfNull(envelope.Events);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", envelope.SchemaVersion);
            writer.WriteString("traceId", envelope.TraceId);
            writer.WritePropertyName("source");
            writer.WriteStartObject();
            writer.WriteString("name", envelope.Source.Name);
            writer.WriteString("version", envelope.Source.Version);
            writer.WriteEndObject();
            writer.WriteString("createdAt", Format(envelope.CreatedAt));
            writer.WritePropertyName("events");
            writer.WriteStartArray();

            foreach (var item in envelope.Events
                .OrderBy(static item => item.Sequence)
                .ThenBy(static item => item.Timestamp)
                .ThenBy(static item => item.Id, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", item.Id);
                writer.WriteNumber("sequence", item.Sequence!.Value);
                writer.WriteString("timestamp", Format(item.Timestamp));
                writer.WriteString("type", item.Type);
                writer.WriteString("actor", item.Actor);
                writer.WritePropertyName("payload");
                item.Payload.WriteTo(writer);
                writer.WritePropertyName("provenance");
                writer.WriteStartObject();
                writer.WriteString("sourceEventId", item.Provenance!.SourceEventId);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var result = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(result);
        result[^1] = (byte)'\n';
        return result;
    }

    private static string Format(DateTimeOffset? value) =>
        value!.Value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
