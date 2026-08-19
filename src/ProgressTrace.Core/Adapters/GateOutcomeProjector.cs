using System.Text.Json;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Adapters;

public sealed record GateOutcomeProjection(TraceEvent Event, ObligationSignal? Signal);
public sealed record GateOutcomeProjectionResult(GateOutcomeProjection? Projection, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}

public static class GateOutcomeProjector
{
    public static GateOutcomeProjectionResult TryProject(GateOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var diagnostics = new List<Diagnostic>();
        if (string.IsNullOrWhiteSpace(outcome.OutcomeId)) diagnostics.Add(new(DiagnosticCodes.Required, "/outcomeId", "Outcome id is required."));
        if (outcome.Sequence is null || outcome.Sequence < 0) diagnostics.Add(new(DiagnosticCodes.InvalidValue, "/sequence", "Sequence must be non-negative."));
        if (outcome.Timestamp is null) diagnostics.Add(new(DiagnosticCodes.Required, "/timestamp", "Timestamp is required."));
        if (outcome.ObligationId is not null && (outcome.Sequence is null || outcome.Sequence > int.MaxValue))
            diagnostics.Add(new(DiagnosticCodes.ProjectionOverflow, "/sequence", "Sequence cannot be represented by an obligation signal index."));
        if (diagnostics.Count != 0) return new(null, diagnostics);

        var eventId = "gate-" + outcome.OutcomeId;
        var payload = JsonSerializer.SerializeToElement(new
        {
            outcomeId = outcome.OutcomeId,
            sessionId = outcome.SessionId,
            invocationId = outcome.InvocationId,
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
        }, (int)outcome.Sequence!.Value);
        return new(new GateOutcomeProjection(traceEvent, signal), diagnostics);
    }

    public static GateOutcomeProjection Project(GateOutcome outcome) =>
        TryProject(outcome).Projection ?? throw new InvalidOperationException("Gate outcome cannot be projected safely.");
}
