using System.Runtime.CompilerServices;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Generation;
using ProgressTrace.Core.Models;
using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Validation;

static class AgentSessionDerivationConformance
{
    private const string SessionId = "session-derived";
    private const string TaskContractId = "task-derived";
    private const string TraceId = "trace-derived";
    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [ModuleInitializer]
    public static void Assert()
    {
        var ledger = new ObligationLedger("1.0", TraceId,
            [new("o-1", "covered"), new("o-2", "intentionally missing evidence")], []);
        var later = Outcome("outcome-2", "invocation-2", 20, "o-1", DateTimeOffset.Parse("2026-09-07T12:00:02+00:00"));
        var earlier = Outcome("outcome-1", "invocation-1", 10, null, DateTimeOffset.Parse("2026-09-07T12:00:01+00:00"));

        var first = AgentSessionFromGateOutcomesProjector.Project(SessionId, TaskContractId, TraceId, [later, earlier], ledger);
        var second = AgentSessionFromGateOutcomesProjector.Project(SessionId, TaskContractId, TraceId, [later, earlier], ledger);
        Require(first.IsValid && first.Session is not null && first.SessionBytes is not null, "valid projection failed");
        var session = first.Session!;
        var sessionBytes = first.SessionBytes!;
        Require(sessionBytes.AsSpan().SequenceEqual(second.SessionBytes), "projection is not deterministic");
        Require(sessionBytes.AsSpan().SequenceEqual(AgentSessionNormalizer.Normalize(session)), "projection bytes are not canonical");
        Require(AgentSessionValidator.ParseAndValidate(sessionBytes).IsValid, "projected bytes failed the canonical validator");
        var invocations = session.Invocations!;
        Require(invocations.Select(static item => item.Sequence).SequenceEqual([10L, 20L]), "outcomes were not sorted by sequence");
        var noObligation = invocations[0];
        var withObligation = invocations[1];
        Require(noObligation.InvocationId == earlier.InvocationId && noObligation.Attempt == 1 && noObligation.TraceId == TraceId, "invocation fields were not projected exactly");
        Require(noObligation.ObligationIds is { Count: 0 }, "null obligation produced an obligation id");
        Require(withObligation.ObligationIds!.SequenceEqual(["o-1"]), "non-null obligation was not projected");
        Require(withObligation.StartedAt == later.Timestamp && withObligation.EndedAt == later.Timestamp, "timestamp bounds were not copied");

        Invalid(null, TaskContractId, TraceId, [earlier], ledger, DiagnosticCodes.Required, "missing session identity");
        Invalid(SessionId, " ", TraceId, [earlier], ledger, DiagnosticCodes.Required, "missing task identity");
        Invalid(SessionId, TaskContractId, "", [earlier], ledger, DiagnosticCodes.MissingTraceId, "missing trace identity");
        Invalid(SessionId, TaskContractId, TraceId, null, ledger, DiagnosticCodes.Required, "missing outcomes");
        Invalid(SessionId, TaskContractId, TraceId, [null], ledger, DiagnosticCodes.Required, "null outcome");
        Invalid(SessionId, TaskContractId, TraceId, [earlier], null, DiagnosticCodes.Required, "missing ledger");

        var requiredNulls = new GateOutcome[]
        {
            earlier with { SchemaVersion = null }, earlier with { OutcomeId = null }, earlier with { SessionId = null },
            earlier with { InvocationId = null }, earlier with { Sequence = null }, earlier with { Command = null },
            earlier with { ExitCode = null }, earlier with { Timestamp = null }, earlier with { Verdict = null },
            earlier with { SourceDigest = null }
        };
        foreach (var outcome in requiredNulls)
            Invalid(SessionId, TaskContractId, TraceId, [outcome], ledger, DiagnosticCodes.Required, "null required outcome field");

        Invalid(SessionId, TaskContractId, TraceId,
            [earlier, later with { OutcomeId = earlier.OutcomeId }], ledger, DiagnosticCodes.DuplicateOutcomeId, "duplicate outcome id");
        Invalid(SessionId, TaskContractId, TraceId,
            [earlier, later with { InvocationId = earlier.InvocationId }], ledger, DiagnosticCodes.DuplicateInvocationId, "duplicate invocation id");
        Invalid(SessionId, TaskContractId, TraceId,
            [earlier, later with { Sequence = earlier.Sequence }], ledger, DiagnosticCodes.NonMonotonicInvocationSequence, "duplicate sequence");
        Invalid(SessionId, TaskContractId, TraceId,
            [earlier with { SessionId = "other-session" }], ledger, DiagnosticCodes.SessionReferenceMismatch, "session mismatch");
        Invalid(SessionId, TaskContractId, TraceId,
            [earlier with { ObligationId = "unknown" }], ledger, DiagnosticCodes.DanglingObligationId, "unknown obligation");
    }

    private static GateOutcome Outcome(string outcomeId, string invocationId, long sequence, string? obligationId, DateTimeOffset timestamp) =>
        new("1.0", outcomeId, SessionId, invocationId, sequence, "dotnet test", 0, timestamp, obligationId, "pass", Digest);

    private static void Invalid(
        string? sessionId,
        string? taskContractId,
        string? traceId,
        IReadOnlyList<GateOutcome?>? outcomes,
        ObligationLedger? ledger,
        string code,
        string name)
    {
        var result = AgentSessionFromGateOutcomesProjector.Project(sessionId, taskContractId, traceId, outcomes, ledger);
        Require(!result.IsValid && result.Session is null && result.SessionBytes is null, $"{name} did not fail closed");
        Require(result.Diagnostics.Count == 1 && result.Diagnostics[0].Code == code, $"{name} returned the wrong diagnostic");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("AgentSession derivation conformance: " + message);
    }
}
