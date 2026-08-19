using System.Buffers;
using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Normalization;

public static class GateOutcomeNormalizer
{
    public static byte[] Normalize(GateOutcome outcome)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject(); writer.WriteString("schemaVersion", outcome.SchemaVersion); writer.WriteString("outcomeId", outcome.OutcomeId); writer.WriteString("sessionId", outcome.SessionId); writer.WriteString("invocationId", outcome.InvocationId); writer.WriteNumber("sequence", outcome.Sequence!.Value); writer.WriteString("command", outcome.Command); writer.WriteNumber("exitCode", outcome.ExitCode!.Value); writer.WriteString("timestamp", Format(outcome.Timestamp)); if (outcome.ObligationId is not null) writer.WriteString("obligationId", outcome.ObligationId); writer.WriteString("verdict", outcome.Verdict); writer.WriteString("sourceDigest", outcome.SourceDigest); writer.WriteEndObject();
        }
        return [.. buffer.WrittenSpan, (byte)'\n'];
    }

    private static string Format(DateTimeOffset? value) => value!.Value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
