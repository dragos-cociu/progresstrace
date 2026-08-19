using System.Globalization;
using System.Text;
using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Benchmarks;

public static class BenchmarkBaselines
{
    public static MaxTurnsResult MaxTurns(TraceEnvelope trace, int limit)
    {
        var count = trace.Events!.Count;
        return new(limit, count >= limit, count >= limit ? limit - 1 : null);
    }

    public static RepeatResult ExactRepeat(TraceEnvelope trace)
    {
        var events = OrderedEvents(trace);
        var firstSeen = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var rank = 0; rank < events.Count; rank++)
        {
            var signature = ExactSignature(events[rank]);
            if (firstSeen.TryGetValue(signature, out var startRank))
                return new(true, "exact-repeat", startRank, rank, 1.0);
            firstSeen.Add(signature, rank);
        }

        return None();
    }

    public static RepeatResult FuzzyRepeatOrCycle(TraceEnvelope trace)
    {
        var events = OrderedEvents(trace);
        var signatures = events.Select(FuzzySignature).ToArray();

        for (var cycleLength = 2; cycleLength <= Math.Min(3, events.Count / 2); cycleLength++)
        {
            for (var start = 0; start + cycleLength * 2 <= events.Count; start++)
            {
                var similarities = Enumerable.Range(0, cycleLength)
                    .Select(offset => Similarity(signatures[start + offset], signatures[start + cycleLength + offset]))
                    .ToArray();
                if (similarities.All(item => item >= 0.75))
                {
                    return new(true, "cycle", start, start + cycleLength * 2 - 1,
                        Round(similarities.Average()));
                }
            }
        }

        for (var end = 1; end < signatures.Length; end++)
        {
            for (var start = 0; start < end; start++)
            {
                var similarity = Similarity(signatures[start], signatures[end]);
                if (similarity >= 0.75)
                    return new(true, "fuzzy-repeat", start, end, Round(similarity));
            }
        }

        return None();
    }

    private static RepeatResult None() => new(false, null, null, null, null);

    private static IReadOnlyList<TraceEvent> OrderedEvents(TraceEnvelope trace) => trace.Events!
        .OrderBy(item => item.Sequence)
        .ThenBy(item => item.Timestamp)
        .ThenBy(item => item.Id, StringComparer.Ordinal)
        .ToList();

    private static string ExactSignature(TraceEvent item) =>
        string.Join("|", item.Type, item.Actor, item.Payload.GetRawText());

    private static HashSet<string> FuzzySignature(TraceEvent item)
    {
        var values = new List<string> { item.Type!, item.Actor! };
        AddTextValues(item.Payload, values);
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var token = new StringBuilder();
            foreach (var character in value.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    token.Append(character);
                }
                else if (token.Length > 0)
                {
                    tokens.Add(token.ToString());
                    token.Clear();
                }
            }
            if (token.Length > 0) tokens.Add(token.ToString());
        }
        return tokens;
    }

    private static void AddTextValues(JsonElement element, List<string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                values.Add(element.GetString()!);
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject()) AddTextValues(property.Value, values);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) AddTextValues(item, values);
                break;
        }
    }

    private static double Similarity(HashSet<string> left, HashSet<string> right)
    {
        var intersection = left.Intersect(right, StringComparer.Ordinal).Count();
        var union = left.Union(right, StringComparer.Ordinal).Count();
        return union == 0 ? 1.0 : (double)intersection / union;
    }

    private static double Round(double value) =>
        double.Parse(value.ToString("0.###", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}
