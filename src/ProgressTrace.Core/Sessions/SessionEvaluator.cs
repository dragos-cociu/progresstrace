using ProgressTrace.Core.Models;
using ProgressTrace.Core.Validation;

namespace ProgressTrace.Core.Sessions;

public static class SessionEvaluator
{
    public const string Progress = "progress";
    public const string RepeatedAttemptWithoutObligationAdvancement = "repeated-attempt-without-obligation-advancement";
    public const string RecoveryAfterFailedAttempt = "recovery-after-failed-attempt";
    public const string InsufficientEvidence = "insufficient-evidence";

    public static SessionEvaluation Evaluate(AgentSession session, IReadOnlyList<GateOutcome> outcomes)
    {
        var coherence = SessionContractValidator.Validate(session, outcomes);
        if (!coherence.IsValid)
            return Insufficient(session, outcomes);

        var invocations = (session.Invocations ?? []).OrderBy(i => i.Sequence).ToList();
        var obligations = invocations.SelectMany(i => i.ObligationIds ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var evidence = outcomes.OrderBy(o => o.Sequence).ThenBy(o => o.OutcomeId, StringComparer.Ordinal).Select(o => o.OutcomeId!).ToArray();
        if (obligations.Length == 0 || outcomes.Count == 0)
            return new(session.SessionId!, InsufficientEvidence, obligations, evidence, false);

        var targeted = outcomes.Where(o => o.ObligationId is not null).ToList();
        if (targeted.Count == 0 || obligations.Any(id => targeted.All(o => o.ObligationId != id)))
            return new(session.SessionId!, InsufficientEvidence, obligations, evidence, false);

        var hasPass = targeted.Any(o => o.Verdict == "pass");
        var hasPriorFailure = targeted.Any(o => o.Verdict is "fail" or "error") && hasPass;
        var repeatedWithoutAdvance = invocations.Any(i => i.Attempt > 1 && (i.ObligationIds ?? []).Any()) && !hasPass;
        var classification = hasPriorFailure ? RecoveryAfterFailedAttempt
            : repeatedWithoutAdvance ? RepeatedAttemptWithoutObligationAdvancement
            : hasPass ? Progress : InsufficientEvidence;
        return new(session.SessionId!, classification, obligations, evidence, false);
    }

    private static SessionEvaluation Insufficient(AgentSession session, IReadOnlyList<GateOutcome> outcomes) =>
        new(session.SessionId ?? string.Empty, InsufficientEvidence, (session.Invocations ?? []).SelectMany(i => i.ObligationIds ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), outcomes.Where(o => o.OutcomeId is not null).Select(o => o.OutcomeId!).ToArray(), false);
}
