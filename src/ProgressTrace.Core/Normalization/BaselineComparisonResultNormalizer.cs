using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Normalization;

public static class BaselineComparisonResultNormalizer
{
    public static byte[] Normalize(BaselineComparisonResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", result.SchemaVersion);
            writer.WriteString("traceId", result.TraceId);
            writer.WriteStartObject("algorithm"); writer.WriteString("name", result.Algorithm.Name); writer.WriteString("version", result.Algorithm.Version); writer.WriteEndObject();
            writer.WriteStartObject("baselineSource");
            writer.WriteString("producerType", result.BaselineSource.ProducerType);
            writer.WriteString("producerName", result.BaselineSource.ProducerName);
            if (result.BaselineSource.ProducerVersion is null) writer.WriteNull("producerVersion"); else writer.WriteString("producerVersion", result.BaselineSource.ProducerVersion);
            writer.WriteString("evidenceBasis", result.BaselineSource.EvidenceBasis); writer.WriteEndObject();
            writer.WriteString("terminationEventId", result.TerminationEventId);
            writer.WriteString("terminationKind", result.TerminationKind);
            writer.WriteBoolean("terminationAttested", result.TerminationAttested);
            writer.WriteNumber("terminationRank", result.TerminationRank);
            writer.WriteNumber("observedEventCount", result.ObservedEventCount);
            writer.WriteStartArray("obligationComparisons");
            foreach (var item in result.ObligationComparisons)
            {
                writer.WriteStartObject(); writer.WriteString("obligationId", item.ObligationId); writer.WriteString("outcome", item.Outcome);
                writer.WriteNumber("eventBudget", item.EventBudget); writer.WriteString("applicability", item.Applicability);
                if (item.AuthoredEstimateUnusedEventBudget is { } unused) writer.WriteNumber("authoredEstimateUnusedEventBudget", unused); else writer.WriteNull("authoredEstimateUnusedEventBudget");
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteBoolean("authoredEstimateFalseHaltPresent", result.AuthoredEstimateFalseHaltPresent);
            if (result.MaxAuthoredEstimateUnusedEventBudget is { } maximum) writer.WriteNumber("maxAuthoredEstimateUnusedEventBudget", maximum); else writer.WriteNull("maxAuthoredEstimateUnusedEventBudget");
            writer.WriteEndObject();
        }
        stream.WriteByte((byte)'\n'); return stream.ToArray();
    }
}
