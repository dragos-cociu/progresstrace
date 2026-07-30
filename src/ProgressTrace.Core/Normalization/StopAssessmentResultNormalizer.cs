using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Normalization;

public static class StopAssessmentResultNormalizer
{
    public static byte[] Normalize(StopAssessmentResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", result.SchemaVersion);
            writer.WriteString("traceId", result.TraceId);
            writer.WriteStartObject("algorithm");
            writer.WriteString("name", result.Algorithm.Name);
            writer.WriteString("version", result.Algorithm.Version);
            writer.WriteEndObject();
            writer.WriteString("terminationEventId", result.TerminationEventId);
            writer.WriteString("terminationKind", result.TerminationKind);
            writer.WriteStartObject("declarationSource");
            writer.WriteString("producerType", result.DeclarationSource.ProducerType);
            writer.WriteString("producerName", result.DeclarationSource.ProducerName);
            if (result.DeclarationSource.ProducerVersion is null) writer.WriteNull("producerVersion");
            else writer.WriteString("producerVersion", result.DeclarationSource.ProducerVersion);
            writer.WriteString("evidenceBasis", result.DeclarationSource.EvidenceBasis);
            writer.WriteEndObject();
            writer.WriteBoolean("terminationAttested", result.TerminationAttested);
            writer.WriteNumber("terminationRank", result.TerminationRank);
            writer.WriteStartArray("obligationResults");
            foreach (var item in result.ObligationResults)
            {
                writer.WriteStartObject();
                writer.WriteString("obligationId", item.ObligationId);
                writer.WriteString("classification", item.Classification);
                writer.WriteStartArray("evidenceEventIds");
                foreach (var id in item.EvidenceEventIds) writer.WriteStringValue(id);
                writer.WriteEndArray();
                writer.WriteString("outcome", item.Outcome);
                if (item.StableAttainmentRank is { } stable) writer.WriteNumber("stableAttainmentRank", stable); else writer.WriteNull("stableAttainmentRank");
                if (item.Overhead is { } overhead) writer.WriteNumber("overhead", overhead); else writer.WriteNull("overhead");
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteString("traceClassification", result.TraceClassification);
            writer.WriteString("stopClassification", result.StopClassification);
            if (result.SafeStopRank is { } safe) writer.WriteNumber("safeStopRank", safe); else writer.WriteNull("safeStopRank");
            if (result.TraceOverhead is { } traceOverhead) writer.WriteNumber("traceOverhead", traceOverhead); else writer.WriteNull("traceOverhead");
            writer.WriteEndObject();
        }
        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }
}
