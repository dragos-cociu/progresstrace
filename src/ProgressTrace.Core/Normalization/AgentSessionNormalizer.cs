using System.Buffers;
using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Normalization;

public static class AgentSessionNormalizer
{
    public static byte[] Normalize(AgentSession session)
    {
        ArgumentNullException.ThrowIfNull(session.Invocations);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject(); writer.WriteString("schemaVersion", session.SchemaVersion); writer.WriteString("sessionId", session.SessionId); writer.WriteString("taskContractId", session.TaskContractId); writer.WritePropertyName("invocations"); writer.WriteStartArray();
            foreach (var item in session.Invocations.OrderBy(x => x.Sequence).ThenBy(x => x.InvocationId, StringComparer.Ordinal))
            {
                writer.WriteStartObject(); writer.WriteString("invocationId", item.InvocationId); writer.WriteNumber("sequence", item.Sequence!.Value); writer.WriteNumber("attempt", item.Attempt!.Value); writer.WriteString("traceId", item.TraceId); writer.WritePropertyName("obligationIds"); writer.WriteStartArray(); foreach (var id in (item.ObligationIds ?? []).Order(StringComparer.Ordinal)) writer.WriteStringValue(id); writer.WriteEndArray(); writer.WriteString("startedAt", Format(item.StartedAt)); writer.WriteString("endedAt", Format(item.EndedAt)); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        return [.. buffer.WrittenSpan, (byte)'\n'];
    }

    private static string Format(DateTimeOffset? value) => value!.Value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
