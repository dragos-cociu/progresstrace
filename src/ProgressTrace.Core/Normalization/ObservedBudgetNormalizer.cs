using System.Buffers;
using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Normalization;

public static class ObservedBudgetNormalizer
{
    public static byte[] Normalize(ObservedBudget budget)
    {
        var buffer = new ArrayBufferWriter<byte>(); using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject(); writer.WriteString("schemaVersion", budget.SchemaVersion); writer.WriteString("sessionId", budget.SessionId); writer.WriteString("taskContractId", budget.TaskContractId); writer.WriteString("ledgerTraceId", budget.LedgerTraceId); writer.WriteString("generatedAt", budget.GeneratedAt); writer.WriteString("sourceDigest", budget.SourceDigest); writer.WriteStartArray("obligations");
            foreach (var item in budget.Obligations ?? []) { writer.WriteStartObject(); writer.WriteString("obligationId", item.ObligationId); writer.WriteString("ledgerStatus", item.LedgerStatus); writer.WriteNumber("observedInvocationCount", item.ObservedInvocationCount!.Value); writer.WriteNumber("observedElapsedMillis", item.ObservedElapsedMillis!.Value); if (item.ObservedTokensTotal is null) writer.WriteNull("observedTokensTotal"); else writer.WriteNumber("observedTokensTotal", item.ObservedTokensTotal.Value); writer.WriteEndObject(); }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        return [.. buffer.WrittenSpan, (byte)'\n'];
    }
}
