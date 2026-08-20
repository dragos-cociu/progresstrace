using System.Buffers;
using System.Globalization;
using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Normalization;

public static class LedgerGenerationReportNormalizer
{
    public static byte[] Normalize(LedgerGenerationReport report)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", report.SchemaVersion);
            writer.WriteString("reportType", report.ReportType);
            writer.WriteStartObject("generator"); writer.WriteString("name", report.Generator.Name); writer.WriteString("version", report.Generator.Version); writer.WriteEndObject();
            writer.WriteString("generatedAt", report.GeneratedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture));
            writer.WriteString("taskContractId", report.TaskContractId);
            writer.WriteString("taskContractPath", report.TaskContractPath);
            writer.WriteString("taskContractDigest", report.TaskContractDigest);
            writer.WriteString("traceId", report.TraceId);
            writer.WriteString("ledgerDigest", report.LedgerDigest);
            writer.WriteStartArray("obligations");
            foreach (var obligation in report.Obligations)
            {
                writer.WriteStartObject();
                writer.WriteString("obligationId", obligation.ObligationId);
                writer.WriteString("coverageStatus", obligation.CoverageStatus);
                if (obligation.SourceField is not null) writer.WriteString("sourceField", obligation.SourceField);
                if (obligation.SourcePointer is not null) writer.WriteString("sourcePointer", obligation.SourcePointer);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("unsupportedSourceFields");
            foreach (var field in report.UnsupportedSourceFields) writer.WriteStringValue(field);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return [.. buffer.WrittenSpan, (byte)'\n'];
    }
}
