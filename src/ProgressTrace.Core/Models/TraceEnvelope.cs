using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProgressTrace.Core.Models;

public sealed record TraceEnvelope(
    string? SchemaVersion,
    string? TraceId,
    TraceSource? Source,
    DateTimeOffset? CreatedAt,
    IReadOnlyList<TraceEvent>? Events);

public sealed record TraceSource(string? Name, string? Version);

public sealed record TraceEvent(
    string? Id,
    long? Sequence,
    DateTimeOffset? Timestamp,
    string? Type,
    string? Actor,
    JsonElement Payload,
    EventProvenance? Provenance);

public sealed record EventProvenance(string? SourceEventId);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(TraceEnvelope))]
[JsonSerializable(typeof(TraceSource))]
[JsonSerializable(typeof(TraceEvent))]
[JsonSerializable(typeof(EventProvenance))]
[JsonSerializable(typeof(IReadOnlyList<TraceEvent>))]
public partial class TraceJsonContext : JsonSerializerContext;
