using System.Buffers;
using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Normalization;

public static class AdvisoryDivergenceReportNormalizer
{
    public static byte[] Normalize(AdvisoryDivergenceReport report)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject(); writer.WriteString("schemaVersion", report.SchemaVersion); writer.WriteString("reportId", report.ReportId); writer.WriteString("sessionId", report.SessionId); writer.WriteString("ledgerTraceId", report.LedgerTraceId); writer.WriteString("advisorySourceDigest", report.AdvisorySourceDigest); writer.WriteString("generatedAt", report.GeneratedAt); writer.WriteStartArray("divergences");
            foreach (var item in report.Divergences) { writer.WriteStartObject(); writer.WriteString("obligationId", item.ObligationId); writer.WriteString("ledgerStatus", item.LedgerStatus); writer.WriteString("advisoryStatus", item.AdvisoryStatus); writer.WriteBoolean("diverged", item.Diverged); writer.WriteEndObject(); }
            writer.WriteEndArray(); if (report.Rollup is not null) { writer.WriteStartObject("rollup"); writer.WriteNumber("totalObligations", report.Rollup.TotalObligations); writer.WriteNumber("divergentCount", report.Rollup.DivergentCount); writer.WriteBoolean("allConverged", report.Rollup.AllConverged); writer.WriteEndObject(); }
            writer.WriteEndObject();
        }
        return [.. buffer.WrittenSpan, (byte)'\n'];
    }
}
