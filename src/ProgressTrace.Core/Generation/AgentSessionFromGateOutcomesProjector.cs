using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;
using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Validation;

namespace ProgressTrace.Core.Generation;

public sealed record AgentSessionProjectionResult(
    AgentSession? Session,
    byte[]? SessionBytes,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => Session is not null && SessionBytes is not null && Diagnostics.Count == 0;
}

public static class AgentSessionFromGateOutcomesProjector
{
    public static AgentSessionProjectionResult Project(
        string? sessionId,
        string? taskContractId,
        string? traceId,
        IReadOnlyList<GateOutcome?>? outcomes,
        ObligationLedger? knownLedger)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return Invalid(DiagnosticCodes.Required, "/sessionId", "Explicit sessionId is missing or empty.");
        if (string.IsNullOrWhiteSpace(taskContractId))
            return Invalid(DiagnosticCodes.Required, "/taskContractId", "Explicit taskContractId is missing or empty.");
        if (string.IsNullOrWhiteSpace(traceId))
            return Invalid(DiagnosticCodes.MissingTraceId, "/traceId", "Explicit traceId is missing or empty.");
        if (outcomes is null)
            return Invalid(DiagnosticCodes.Required, "/outcomes", "GateOutcome collection is missing.");
        if (knownLedger?.Obligations is null)
            return Invalid(DiagnosticCodes.Required, "/knownLedger", "Known obligation ledger is missing.");

        var knownObligationIds = knownLedger.Obligations
            .Where(static obligation => obligation.Id is not null)
            .Select(static obligation => obligation.Id!)
            .ToHashSet(StringComparer.Ordinal);
        var outcomeIds = new HashSet<string>(StringComparer.Ordinal);
        var invocationIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < outcomes.Count; index++)
        {
            var outcome = outcomes[index];
            var pointer = $"/outcomes/{index}";
            if (outcome is null)
                return Invalid(DiagnosticCodes.Required, pointer, "GateOutcome is null.");
            var missing = MissingRequiredField(outcome);
            if (missing is not null)
                return Invalid(DiagnosticCodes.Required, pointer + "/" + missing, "GateOutcome required field is null.");
            if (!outcomeIds.Add(outcome.OutcomeId!))
                return Invalid(DiagnosticCodes.DuplicateOutcomeId, "/outcomes", "GateOutcome identifiers must be unique.");
            if (!invocationIds.Add(outcome.InvocationId!))
                return Invalid(DiagnosticCodes.DuplicateInvocationId, "/outcomes", "Invocation identifiers must be unique.");
            if (!string.Equals(outcome.SessionId, sessionId, StringComparison.Ordinal))
                return Invalid(DiagnosticCodes.SessionReferenceMismatch, pointer + "/sessionId", "GateOutcome session does not match the explicit session.");
            if (outcome.ObligationId is not null && !knownObligationIds.Contains(outcome.ObligationId))
                return Invalid(DiagnosticCodes.DanglingObligationId, pointer + "/obligationId", "GateOutcome references an unknown obligation.");
        }

        var ordered = outcomes.Select(static outcome => outcome!).OrderBy(static outcome => outcome.Sequence).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            if (ordered[index].Sequence <= ordered[index - 1].Sequence)
                return Invalid(DiagnosticCodes.NonMonotonicInvocationSequence, "/outcomes", "GateOutcome sequence must be unique and strictly increasing.");
        }

        var invocations = ordered.Select(outcome => new SessionInvocation(
            outcome.InvocationId,
            outcome.Sequence,
            1,
            traceId,
            outcome.ObligationId is null ? [] : [outcome.ObligationId],
            outcome.Timestamp,
            outcome.Timestamp)).ToArray();
        var session = new AgentSession("1.0", sessionId, taskContractId, invocations);
        var bytes = AgentSessionNormalizer.Normalize(session);
        var validation = AgentSessionValidator.ParseAndValidate(bytes);
        return validation.IsValid
            ? new(session, bytes, [])
            : new(null, null, validation.Diagnostics);
    }

    private static string? MissingRequiredField(GateOutcome outcome)
    {
        if (outcome.SchemaVersion is null) return "schemaVersion";
        if (outcome.OutcomeId is null) return "outcomeId";
        if (outcome.SessionId is null) return "sessionId";
        if (outcome.InvocationId is null) return "invocationId";
        if (outcome.Sequence is null) return "sequence";
        if (outcome.Command is null) return "command";
        if (outcome.ExitCode is null) return "exitCode";
        if (outcome.Timestamp is null) return "timestamp";
        if (outcome.Verdict is null) return "verdict";
        if (outcome.SourceDigest is null) return "sourceDigest";
        return null;
    }

    private static AgentSessionProjectionResult Invalid(string code, string pointer, string message) =>
        new(null, null, [new(code, pointer, message)]);
}
