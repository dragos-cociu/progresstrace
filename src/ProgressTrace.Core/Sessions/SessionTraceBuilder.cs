using ProgressTrace.Core.Adapters;
using ProgressTrace.Core.Evaluation;
using ProgressTrace.Core.Models;
using ProgressTrace.Core.Validation;

namespace ProgressTrace.Core.Sessions;

public sealed record SessionTraceIntegration(TraceEnvelope Trace, ObligationLedger Ledger, EvaluationResult Evaluation, SessionEvaluation SessionEvaluation);

public static class SessionTraceBuilder
{
    public static SessionTraceIntegration Build(AgentSession session, IReadOnlyList<GateOutcome> outcomes)
    {
        var coherence = SessionContractValidator.Validate(session, outcomes);
        if (!coherence.IsValid) throw new InvalidOperationException("Session and gate outcomes are incoherent.");
        var projections = outcomes.OrderBy(o => o.Sequence).ThenBy(o => o.OutcomeId, StringComparer.Ordinal)
            .Select(o => GateOutcomeProjector.TryProject(o)).ToArray();
        if (projections.Any(p => !p.IsValid)) throw new InvalidOperationException("Gate outcome projection failed closed.");
        var events = projections.Select(p => p.Projection!.Event).ToArray();
        var traceId = (session.Invocations ?? []).OrderBy(i => i.Sequence).FirstOrDefault()?.TraceId;
        if (string.IsNullOrWhiteSpace(traceId)) throw new InvalidOperationException("Session has no trace identity.");
        var trace = new TraceEnvelope("1.0", traceId, new TraceSource("hermes-gate", "1.0.0"), events.Min(e => e.Timestamp), events);
        var obligationIds = (session.Invocations ?? []).SelectMany(i => i.ObligationIds ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var signals = projections.Select(p => p.Projection!.Signal).Where(s => s is not null).Cast<ObligationSignal>().ToArray();
        var ledger = new ObligationLedger("1.0", traceId, obligationIds.Select(id => new Obligation(id, "")).ToArray(), signals);
        var ledgerValidation = ObligationLedgerValidator.ParseAndValidate(SerializeLedger(ledger), trace);
        if (!ledgerValidation.IsValid) throw new InvalidOperationException("Projected obligation ledger failed validation.");
        return new(trace, ledgerValidation.Ledger!, Evaluator.Evaluate(trace, ledgerValidation.Ledger!), SessionEvaluator.Evaluate(session, outcomes));
    }

    private static byte[] SerializeLedger(ObligationLedger ledger) =>
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = ledger.SchemaVersion,
            traceId = ledger.TraceId,
            obligations = ledger.Obligations!.Select(o => new { id = o.Id, description = o.Description }),
            signals = ledger.Signals!.Select(s => new { obligationId = s.ObligationId, eventId = s.EventId, status = s.Status })
        });
}
