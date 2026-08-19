using System.Text.Json;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Adapters;

public sealed record GateOutcomeProjection(TraceEvent Event, ObligationSignal? Signal);

public static class GateOutcomeProjector
{
    public static GateOutcomeProjection Project(GateOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome.OutcomeId);
        var eventId = "gate-" + outcome.OutcomeId;
        var payload = JsonSerializer.SerializeToElement(new
        {
            outcomeId = outcome.OutcomeId,
            command = outcome.Command,
            exitCode = outcome.ExitCode,
            verdict = outcome.Verdict,
            sourceDigest = outcome.SourceDigest
        });
        var traceEvent = new TraceEvent(eventId, outcome.Sequence, outcome.Timestamp, "gate-outcome", "tool", payload, new EventProvenance(outcome.OutcomeId));
        var signal = outcome.ObligationId is null ? null : new ObligationSignal(outcome.ObligationId, eventId, outcome.Verdict switch
        {
            "pass" => "satisfied",
            "fail" or "error" => "regressed",
            "skipped" => "open",
            _ => "open"
        }, checked((int)outcome.Sequence!.Value));
        return new(traceEvent, signal);
    }
}
