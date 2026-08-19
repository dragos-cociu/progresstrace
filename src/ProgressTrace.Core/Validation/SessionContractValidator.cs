using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Validation;

public sealed record SessionContractValidationResult(IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}

public static class SessionContractValidator
{
    public static SessionContractValidationResult Validate(
        AgentSession session,
        IReadOnlyList<GateOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(outcomes);
        var diagnostics = new List<Diagnostic>();
        var invocations = (session.Invocations ?? []).Where(i => i.InvocationId is not null).ToDictionary(i => i.InvocationId!, StringComparer.Ordinal);
        var outcomeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (outcome, index) in outcomes.Select((item, index) => (item, index)))
        {
            if (outcome.OutcomeId is not null && !outcomeIds.Add(outcome.OutcomeId))
                diagnostics.Add(new(DiagnosticCodes.DuplicateOutcomeId, $"/outcomes/{index}/outcomeId", "Outcome identifiers must be unique."));
            if (outcome.SessionId != session.SessionId)
                diagnostics.Add(new(DiagnosticCodes.SessionReferenceMismatch, $"/outcomes/{index}/sessionId", "Outcome sessionId must match the AgentSession sessionId."));
            if (outcome.InvocationId is null || !invocations.TryGetValue(outcome.InvocationId, out var invocation))
            {
                diagnostics.Add(new(DiagnosticCodes.InvocationReferenceMismatch, $"/outcomes/{index}/invocationId", "Outcome invocationId must reference an invocation in the AgentSession."));
                continue;
            }
            if (outcome.Sequence != invocation.Sequence)
                diagnostics.Add(new(DiagnosticCodes.InvocationReferenceMismatch, $"/outcomes/{index}/sequence", "Outcome sequence must match its referenced invocation sequence."));
            if (outcome.Timestamp is not null && invocation.StartedAt is not null && outcome.Timestamp < invocation.StartedAt)
                diagnostics.Add(new(DiagnosticCodes.InvocationReferenceMismatch, $"/outcomes/{index}/timestamp", "Outcome timestamp must be within its invocation window."));
            if (outcome.Timestamp is not null && invocation.EndedAt is not null && outcome.Timestamp > invocation.EndedAt)
                diagnostics.Add(new(DiagnosticCodes.InvocationReferenceMismatch, $"/outcomes/{index}/timestamp", "Outcome timestamp must be within its invocation window."));
            if (outcome.ObligationId is not null && !(invocation.ObligationIds ?? []).Contains(outcome.ObligationId, StringComparer.Ordinal))
                diagnostics.Add(new(DiagnosticCodes.InvocationReferenceMismatch, $"/outcomes/{index}/obligationId", "Outcome obligationId must be declared by its invocation."));
        }
        return new(diagnostics);
    }
}
