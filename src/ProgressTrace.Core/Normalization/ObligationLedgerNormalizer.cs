using System.Buffers;
using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Normalization;

public static class ObligationLedgerNormalizer
{
    public static byte[] Normalize(ObligationLedger ledger)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", ledger.SchemaVersion);
            writer.WriteString("traceId", ledger.TraceId);
            writer.WriteStartArray("obligations");
            foreach (var obligation in ledger.Obligations ?? [])
            {
                writer.WriteStartObject();
                writer.WriteString("id", obligation.Id);
                writer.WriteString("description", obligation.Description);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("signals");
            foreach (var signal in ledger.Signals ?? [])
            {
                writer.WriteStartObject();
                writer.WriteString("obligationId", signal.ObligationId);
                writer.WriteString("eventId", signal.EventId);
                writer.WriteString("status", signal.Status);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return [.. buffer.WrittenSpan, (byte)'\n'];
    }
}
