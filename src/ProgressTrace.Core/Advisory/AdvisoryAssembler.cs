using System.Security.Cryptography;
using ProgressTrace.Core.Adapters;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Evaluation;
using ProgressTrace.Core.Models;
using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Validation;

namespace ProgressTrace.Core.Advisory;

public sealed record AdvisoryAssemblyResult(AdvisoryResult? Result, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => Result is not null && Diagnostics.All(d => d.Code == DiagnosticCodes.AdvisoryInsufficientEvidence);
}
public sealed record DivergenceAssemblyResult(AdvisoryDivergenceReport? Report, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => Report is not null && Diagnostics.Count == 0;
}

public static class AdvisoryAssembler
{
    public const string GeneratedAt = "1970-01-01T00:00:00.0000000Z";

    public static AdvisoryAssemblyResult Assemble(AgentSession session, ObligationLedger ledger, IReadOnlyList<GateOutcome> outcomes)
    {
        var traceId = (session.Invocations ?? []).OrderBy(i => i.Sequence).FirstOrDefault()?.TraceId;
        if (string.IsNullOrWhiteSpace(traceId) || traceId != ledger.TraceId || outcomes.Any(o => o.SessionId != session.SessionId)) return Failure(DiagnosticCodes.AdvisoryReferenceMismatch, "Session references do not match advisory inputs.");
        var coherence = SessionContractValidator.Validate(session, outcomes);
        if (!coherence.IsValid) return Failure(DiagnosticCodes.AdvisoryReferenceMismatch, "Session references do not match advisory inputs.");
        var projections = outcomes.OrderBy(o => o.Sequence).ThenBy(o => o.OutcomeId, StringComparer.Ordinal).Select(GateOutcomeProjector.TryProject).ToArray();
        if (projections.Any(p => !p.IsValid)) return Failure(DiagnosticCodes.AdvisoryOutcomeInvalid, "Gate outcome cannot be projected.");
        var events = projections.Select(p => p.Projection!.Event).ToArray();
        if (events.Length == 0) return Failure(DiagnosticCodes.AdvisoryNormalizationFailure, "Advisory result cannot be normalized deterministically.");
        var trace = new TraceEnvelope("1.0", traceId, new TraceSource("hermes-gate", "1.0.0"), events.Min(e => e.Timestamp), events);
        var projectedSignals = projections.Select(p => p.Projection!.Signal).Where(s => s is not null).Cast<ObligationSignal>().ToArray();
        var obligations = ledger.Obligations!;
        var ids = obligations.Select(o => o.Id!).ToHashSet(StringComparer.Ordinal);
        if (projectedSignals.Any(s => !ids.Contains(s.ObligationId!))) return Failure(DiagnosticCodes.AdvisoryNormalizationFailure, "Advisory result cannot be normalized deterministically.");
        var evaluationLedger = ledger with { Signals = projectedSignals };
        var evaluation = Evaluator.Evaluate(trace, evaluationLedger);
        if (events.Select(e => e.Id).GroupBy(id => id, StringComparer.Ordinal).Any(group => group.Count() > 1)) return Failure(DiagnosticCodes.AdvisoryNormalizationFailure, "Advisory result cannot be normalized deterministically.");
        var ranks = events.OrderBy(e => e.Sequence).ThenBy(e => e.Timestamp).ThenBy(e => e.Id, StringComparer.Ordinal).Select((e, i) => (e.Id!, i)).ToDictionary(x => x.Item1, x => x.i, StringComparer.Ordinal);
        // A gate is identified by its command; per obligation only the latest outcome of each gate counts,
        // so a failing gate cannot be masked by a different gate passing later (F5).
        var gateByEvent = outcomes.ToDictionary(o => "gate-" + o.OutcomeId, o => o.Command ?? string.Empty, StringComparer.Ordinal);
        var attempts = (session.Invocations ?? []).Where(i => i.InvocationId is not null).ToDictionary(i => i.InvocationId!, i => i.Attempt ?? 0, StringComparer.Ordinal);
        var attemptByEvent = outcomes.ToDictionary(o => "gate-" + o.OutcomeId, o => o.InvocationId is not null && attempts.TryGetValue(o.InvocationId, out var a) ? a : 0, StringComparer.Ordinal);
        var entries = new List<AdvisoryObligation>();
        foreach (var obligation in obligations)
        {
            var evaluated = evaluation.ObligationResults.Single(r => r.ObligationId == obligation.Id);
            var signals = projectedSignals.Where(s => s.ObligationId == obligation.Id).OrderBy(s => ranks[s.EventId!]).ThenBy(s => s.EventId, StringComparer.Ordinal).ThenBy(s => s.Index).ToList();
            var latestPerGate = signals.GroupBy(s => gateByEvent[s.EventId!], StringComparer.Ordinal).Select(g => g.Last()).ToList();
            var status = latestPerGate.Count == 0 ? "open"
                : latestPerGate.Any(s => s.Status == "regressed") ? "regressed"
                : latestPerGate.All(s => s.Status == "satisfied") ? "satisfied" : "in-progress";
            entries.Add(new(obligation.Id!, status, Classify(status, signals, attemptByEvent), status == "satisfied", evaluated.EvidenceEventIds));
        }
        var classification = new[] { InsufficientEvidence, FailedAttempt, RepeatedAttempt, Recovery }.FirstOrDefault(c => entries.Any(e => e.Classification == c)) ?? Progress;
        var recommendation = classification == InsufficientEvidence ? InsufficientEvidence : entries.All(e => e.Stable) ? "stop-recommended" : "continue";
        var digestInputs = new List<byte[]> { AgentSessionNormalizer.Normalize(session), ObligationLedgerNormalizer.Normalize(ledger) };
        digestInputs.AddRange(outcomes.Select(GateOutcomeNormalizer.Normalize));
        var digest = Hash(digestInputs.ToArray());
        var result = new AdvisoryResult("1.0", session.SessionId!, session.TaskContractId!, ledger.TraceId!, GeneratedAt, digest, classification, recommendation, entries);
        var normalized = AdvisoryResultNormalizer.Normalize(result); var validation = AdvisoryResultValidator.ParseAndValidate(normalized);
        if (!validation.IsValid || !normalized.AsSpan().SequenceEqual(AdvisoryResultNormalizer.Normalize(validation.AdvisoryResult!))) return Failure(DiagnosticCodes.AdvisoryNormalizationFailure, "Advisory result cannot be normalized deterministically.");
        return new(result, classification == "insufficient-evidence" ? [new(DiagnosticCodes.AdvisoryInsufficientEvidence, "", "Evidence is insufficient for a targeted obligation.")] : []);
    }

    public static DivergenceAssemblyResult AssembleDivergence(AdvisoryResult advisory, ObligationLedger ledger)
    {
        if (advisory.LedgerTraceId != ledger.TraceId) return DivergenceFailure(DiagnosticCodes.AdvisoryResultInvalid, "Advisory result does not reference the supplied ledger.");
        var obligations = ledger.Obligations!; var ledgerIds = obligations.Select(o => o.Id!).ToHashSet(StringComparer.Ordinal); var advisoryIds = advisory.Obligations.Select(o => o.ObligationId).ToHashSet(StringComparer.Ordinal);
        if (!ledgerIds.SetEquals(advisoryIds) || ledgerIds.Count != obligations.Count || advisoryIds.Count != advisory.Obligations.Count) return DivergenceFailure(DiagnosticCodes.AdvisoryObligationMismatch, "Advisory and ledger obligations cannot be reconciled.");
        var entries = new List<AdvisoryDivergence>();
        foreach (var obligation in obligations) { var ledgerStatus = ledger.Signals!.Where(s => s.ObligationId == obligation.Id).OrderBy(s => s.Index).LastOrDefault()?.Status ?? "open"; var advisoryStatus = advisory.Obligations.Single(o => o.ObligationId == obligation.Id).Status; entries.Add(new(obligation.Id!, ledgerStatus, advisoryStatus, ledgerStatus != advisoryStatus)); }
        var count = entries.Count(e => e.Diverged); var reportId = Hash(AdvisoryResultNormalizer.Normalize(advisory), ObligationLedgerNormalizer.Normalize(ledger));
        return new(new("1.0", reportId, advisory.SessionId, advisory.LedgerTraceId, advisory.SourceDigest, GeneratedAt, entries, new(entries.Count, count, count == 0)), []);
    }

    public const string Progress = "progress";
    public const string Recovery = "recovery-after-failed-attempt";
    public const string RepeatedAttempt = "repeated-attempt-without-obligation-advancement";
    public const string FailedAttempt = "failed-attempt";
    public const string InsufficientEvidence = "insufficient-evidence";

    // Per-obligation classification over the effective status (F1): a failure that is still current is
    // never reported as recovery, and several passing gates in one attempt are progress, not stagnation.
    private static string Classify(string status, IReadOnlyList<ObligationSignal> signals, IReadOnlyDictionary<string, int> attemptByEvent)
    {
        var failures = signals.Where(s => s.Status == "regressed").ToList();
        if (status == "satisfied") return failures.Count > 0 ? Recovery : Progress;
        if (status != "regressed") return InsufficientEvidence;
        var neverPassed = signals.All(s => s.Status != "satisfied");
        return neverPassed && failures.Select(s => attemptByEvent[s.EventId!]).Distinct().Count() >= 2 ? RepeatedAttempt : FailedAttempt;
    }
    private static string Hash(params byte[][] values) { using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); foreach (var value in values) hash.AppendData(value); return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(); }
    private static AdvisoryAssemblyResult Failure(string code, string message) => new(null, [new(code, "", message)]);
    private static DivergenceAssemblyResult DivergenceFailure(string code, string message) => new(null, [new(code, "", message)]);
}
