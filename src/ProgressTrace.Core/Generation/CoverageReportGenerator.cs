using System.Buffers;
using System.Text.Json;
using System.Text.RegularExpressions;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Generation;

public sealed record CoverageReport(
    string SchemaVersion,
    string TaskContractId,
    string TraceId,
    IReadOnlyList<CoverageReportEntry> Obligations);

public sealed record CoverageReportEntry(
    string ObligationId,
    string CoverageStatus,
    int GateEvidenceCount,
    IReadOnlyList<string> GateKeys);

public sealed record CoverageReportGenerationResult(
    CoverageReport? Report,
    byte[]? ReportBytes,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => Report is not null && ReportBytes is not null &&
        Diagnostics.All(static diagnostic => diagnostic.Code == CoverageReportGenerator.UnboundOutcomeCode);
}

public static class CoverageReportGenerator
{
    internal const string UnboundOutcomeCode = "PT614";
    private static readonly HashSet<string> Verdicts = new(StringComparer.Ordinal) { "pass", "fail", "skipped", "error" };
    private static readonly Regex Digest = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static CoverageReportGenerationResult Generate(
        CorrelationManifest? manifest,
        ObligationLedger? ledger,
        IReadOnlyList<GateOutcome?>? outcomes)
    {
        if (manifest is null) return Failure("PT608", "/manifest", "Correlation manifest is missing.");
        if (ledger?.Obligations is null) return Failure("PT608", "/ledger", "Obligation ledger is missing.");
        if (outcomes is null) return Failure("PT608", "/outcomes", "GateOutcome collection is missing.");
        if (string.IsNullOrWhiteSpace(manifest.TaskContractId) || string.IsNullOrWhiteSpace(manifest.TraceId) || manifest.GateBindings is null)
            return Failure("PT609", "/manifest", "Correlation manifest identity or bindings are missing.");
        if (!string.Equals(manifest.TraceId, ledger.TraceId, StringComparison.Ordinal))
            return Failure("PT609", "/manifest/traceId", "Manifest and ledger trace identities do not match.");

        var obligations = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < ledger.Obligations.Count; index++)
        {
            var id = ledger.Obligations[index]?.Id;
            if (string.IsNullOrWhiteSpace(id)) return Failure("PT610", $"/ledger/obligations/{index}/id", "Ledger obligation identity is missing.");
            if (!id.StartsWith(manifest.TaskContractId + ":", StringComparison.Ordinal))
                return Failure("PT609", "/manifest/taskContractId", "Manifest task identity does not match the ledger obligation namespace.");
            if (!obligations.Add(id)) return Failure("PT610", $"/ledger/obligations/{index}/id", "Ledger obligation identities must be unique.");
        }

        var bindingsByObligation = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var gateKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < manifest.GateBindings.Count; index++)
        {
            var binding = manifest.GateBindings[index];
            if (binding is null || string.IsNullOrWhiteSpace(binding.GateKey) || string.IsNullOrWhiteSpace(binding.ObligationId))
                return Failure("PT611", $"/manifest/gateBindings/{index}", "Gate binding identity is missing.");
            if (!gateKeys.Add(binding.GateKey))
                return Failure("PT611", $"/manifest/gateBindings/{index}/gateKey", "Gate keys must be unique.");
            if (!obligations.Contains(binding.ObligationId))
                return Failure("PT612", $"/manifest/gateBindings/{index}/obligationId", "Gate binding references an unknown obligation.");
            if (!bindingsByObligation.TryGetValue(binding.ObligationId, out var keys))
                bindingsByObligation.Add(binding.ObligationId, keys = []);
            keys.Add(binding.GateKey);
        }

        var outcomeIds = new HashSet<string>(StringComparer.Ordinal);
        var evidence = new Dictionary<string, int>(StringComparer.Ordinal);
        var diagnostics = new List<Diagnostic>();
        for (var index = 0; index < outcomes.Count; index++)
        {
            var outcome = outcomes[index];
            if (!IsValid(outcome)) return Failure("PT613", $"/outcomes/{index}", "GateOutcome is invalid.");
            if (!outcomeIds.Add(outcome!.OutcomeId!))
                return Failure(DiagnosticCodes.DuplicateOutcomeId, $"/outcomes/{index}/outcomeId", "GateOutcome identifiers must be unique.");
            if (outcome.ObligationId is not null && !obligations.Contains(outcome.ObligationId))
                return Failure(DiagnosticCodes.DanglingObligationId, $"/outcomes/{index}/obligationId", "GateOutcome references an unknown obligation.");
            if (outcome.ObligationId is null || !bindingsByObligation.ContainsKey(outcome.ObligationId))
            {
                diagnostics.Add(new(UnboundOutcomeCode, $"/outcomes/{index}/obligationId", "GateOutcome is not associated with a manifest binding."));
                continue;
            }
            evidence[outcome.ObligationId] = evidence.GetValueOrDefault(outcome.ObligationId) + 1;
        }

        var entries = ledger.Obligations.Select(obligation =>
        {
            var id = obligation.Id!;
            var keys = bindingsByObligation.GetValueOrDefault(id);
            var count = evidence.GetValueOrDefault(id);
            return new CoverageReportEntry(id, keys is null ? "manual" : count > 0 ? "automatic" : "insufficient-evidence", count, keys ?? []);
        }).ToArray();
        var report = new CoverageReport("pt-coverage-report-1.0", manifest.TaskContractId, manifest.TraceId, entries);
        return new(report, Serialize(report), diagnostics);
    }

    private static bool IsValid(GateOutcome? outcome) => outcome is not null &&
        outcome.SchemaVersion == "1.0" && !string.IsNullOrWhiteSpace(outcome.OutcomeId) &&
        !string.IsNullOrWhiteSpace(outcome.SessionId) && !string.IsNullOrWhiteSpace(outcome.InvocationId) &&
        outcome.Sequence is >= 0 && outcome.Command is not null && outcome.ExitCode is not null &&
        outcome.Timestamp is not null && Verdicts.Contains(outcome.Verdict ?? "") &&
        outcome.SourceDigest is not null && Digest.IsMatch(outcome.SourceDigest);

    private static byte[] Serialize(CoverageReport report)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", report.SchemaVersion);
            writer.WriteString("taskContractId", report.TaskContractId);
            writer.WriteString("traceId", report.TraceId);
            writer.WriteStartArray("obligations");
            foreach (var entry in report.Obligations)
            {
                writer.WriteStartObject();
                writer.WriteString("obligationId", entry.ObligationId);
                writer.WriteString("coverageStatus", entry.CoverageStatus);
                writer.WriteNumber("gateEvidenceCount", entry.GateEvidenceCount);
                writer.WriteStartArray("gateKeys");
                foreach (var key in entry.GateKeys) writer.WriteStringValue(key);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return [.. buffer.WrittenSpan, (byte)'\n'];
    }

    private static CoverageReportGenerationResult Failure(string code, string pointer, string message) =>
        new(null, null, [new(code, pointer, message)]);
}
