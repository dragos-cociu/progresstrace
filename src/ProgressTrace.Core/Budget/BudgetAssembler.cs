using System.Security.Cryptography;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;
using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Validation;

namespace ProgressTrace.Core.Budget;

public sealed record BudgetAssemblyResult(ObservedBudget? Budget, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => Budget is not null && Diagnostics.All(d => d.Code == DiagnosticCodes.BudgetMissingTokenUsage);
}

public static class BudgetAssembler
{
    public const string GeneratedAt = "1970-01-01T00:00:00.0000000Z";

    public static BudgetAssemblyResult Assemble(AgentSession session, TokenUsage usage, ObligationLedger ledger)
    {
        var invocations = session.Invocations ?? []; var earliestTrace = invocations.OrderBy(i => i.Sequence).FirstOrDefault()?.TraceId;
        if (usage.SessionId != session.SessionId || usage.TaskContractId != session.TaskContractId || ledger.TraceId != earliestTrace) return Failure(DiagnosticCodes.BudgetReferenceMismatch, "Budget input references do not match.");
        var byInvocation = invocations.Where(i => i.InvocationId is not null).ToDictionary(i => i.InvocationId!, StringComparer.Ordinal); var records = usage.Records ?? [];
        foreach (var record in records) if (record.SessionId != usage.SessionId || record.InvocationId is null || !byInvocation.TryGetValue(record.InvocationId, out var invocation) || record.Sequence != invocation.Sequence) return Failure(DiagnosticCodes.BudgetInadmissibleTokenUsage, "Token usage record is inadmissible.");
        if (records.Where(r => r.InvocationId is not null).GroupBy(r => r.InvocationId!, StringComparer.Ordinal).Any(g => g.Count() > 1)) return Failure(DiagnosticCodes.BudgetDuplicateTokenUsage, "Token usage record targets a duplicate invocation.");
        var recordMap = records.ToDictionary(r => r.InvocationId!, StringComparer.Ordinal); var missing = invocations.Where(i => i.InvocationId is not null && !recordMap.ContainsKey(i.InvocationId)).ToArray();
        var durations = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var invocation in invocations)
        {
            if (invocation.InvocationId is null || invocation.StartedAt is null || invocation.EndedAt is null || invocation.EndedAt < invocation.StartedAt) return Failure(DiagnosticCodes.BudgetInvocationWindow, "Invocation window is invalid.");
            durations[invocation.InvocationId] = (invocation.EndedAt.Value - invocation.StartedAt.Value).Ticks;
        }
        var observations = new List<ObligationObservation>();
        try
        {
            foreach (var obligation in ledger.Obligations ?? [])
            {
                var targets = invocations.Where(i => (i.ObligationIds ?? []).Contains(obligation.Id, StringComparer.Ordinal)).ToArray(); long ticks = 0; foreach (var target in targets) ticks = CheckedAdd(ticks, durations[target.InvocationId!]);
                long tokens = 0; var complete = true; foreach (var target in targets) { if (!recordMap.TryGetValue(target.InvocationId!, out var record)) complete = false; else tokens = CheckedAdd(tokens, record.TokensTotal!.Value); }
                var status = (ledger.Signals ?? []).Where(s => s.ObligationId == obligation.Id).OrderBy(s => s.Index).LastOrDefault()?.Status ?? "open";
                observations.Add(new(obligation.Id!, status, targets.Length, ticks / TimeSpan.TicksPerMillisecond, complete ? tokens : null));
            }
        }
        catch (OverflowException) { return Failure(DiagnosticCodes.BudgetOverflow, "Observed budget aggregation overflowed."); }
        var expected = (ledger.Obligations ?? []).Select(o => o.Id).ToArray(); if (observations.Count != expected.Length || !observations.Select(o => o.ObligationId).SequenceEqual(expected)) return Failure(DiagnosticCodes.BudgetNormalizationFailure, "Observed budget cannot be normalized deterministically.");
        var digest = Convert.ToHexStringLower(SHA256.HashData(Concat(AgentSessionNormalizer.Normalize(session), TokenUsageNormalizer.Normalize(usage), ObligationLedgerNormalizer.Normalize(ledger))));
        var budget = new ObservedBudget("1.0", session.SessionId!, session.TaskContractId!, ledger.TraceId!, GeneratedAt, digest, observations); var normalized = ObservedBudgetNormalizer.Normalize(budget); var validation = ObservedBudgetValidator.ParseAndValidate(normalized);
        if (!validation.IsValid || !normalized.AsSpan().SequenceEqual(ObservedBudgetNormalizer.Normalize(validation.ObservedBudget!))) return Failure(DiagnosticCodes.BudgetNormalizationFailure, "Observed budget cannot be normalized deterministically.");
        return new(budget, missing.Select(_ => new Diagnostic(DiagnosticCodes.BudgetMissingTokenUsage, "", "Token usage record is missing for an invocation.")).ToArray());
    }

    internal static long CheckedAdd(long left, long right) => checked(left + right);
    private static byte[] Concat(params byte[][] values) { var length = values.Sum(v => v.Length); var result = new byte[length]; var offset = 0; foreach (var value in values) { value.CopyTo(result, offset); offset += value.Length; } return result; }
    private static BudgetAssemblyResult Failure(string code, string message) => new(null, [new(code, "", message)]);
}
