using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Assessment;

public static class StableAttainment
{
    public static int? GetRank(string obligationId, ObligationLedger ledger, IReadOnlyDictionary<string, int> eventRanks)
    {
        var signals = (ledger.Signals ?? []).Where(s => s.ObligationId == obligationId && s.EventId is not null && eventRanks.ContainsKey(s.EventId))
            .OrderBy(s => eventRanks[s.EventId!]).ThenBy(s => s.Index).ToList();
        if (signals.Count == 0 || signals[^1].Status != "satisfied") return null;
        var start = signals.Count - 1; while (start > 0 && signals[start - 1].Status == "satisfied") start--;
        return eventRanks[signals[start].EventId!];
    }

    public static bool IsStable(string obligationId, ObligationLedger ledger, IReadOnlyDictionary<string, int> eventRanks) => GetRank(obligationId, ledger, eventRanks) is not null;
}
