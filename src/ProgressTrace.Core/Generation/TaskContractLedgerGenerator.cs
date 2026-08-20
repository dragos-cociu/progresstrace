using System.Security.Cryptography;
using System.Text.Json;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;
using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Validation;

namespace ProgressTrace.Core.Generation;

public static class TaskContractLedgerGenerator
{
    private static readonly string[] SourceFields =
        ["deliverables", "required_contract_decisions", "required_invariants"];

    public static LedgerGenerationResult Generate(
        ReadOnlyMemory<byte> taskContractBytes,
        string taskContractPath,
        string? traceId)
    {
        if (string.IsNullOrWhiteSpace(traceId))
            return Invalid(DiagnosticCodes.MissingTraceId, "", "traceId argument is missing or empty.");

        if (taskContractBytes.Length > TraceValidator.MaximumInputSizeBytes)
            return Invalid(DiagnosticCodes.TaskContractUnreadable, "", "Task contract file could not be read.");

        JsonDocument document;
        try { document = JsonDocument.Parse(taskContractBytes); }
        catch (JsonException)
        {
            return Invalid(DiagnosticCodes.TaskContractInvalidJson, "", "Task contract is not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Invalid(DiagnosticCodes.TaskContractInvalidJson, "", "Task contract is not valid JSON.");

            if (!root.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(idElement.GetString()))
                return Invalid(DiagnosticCodes.TaskContractId, "/id", "Task contract id is missing or not a string.");

            var taskContractId = idElement.GetString()!;
            var candidates = new List<Candidate>();
            var unsupported = new List<string>();
            foreach (var field in SourceFields)
            {
                var sources = root.EnumerateObject()
                    .Where(property => property.NameEquals(field) && property.Value.ValueKind == JsonValueKind.Array && property.Value.GetArrayLength() > 0)
                    .Select(property => property.Value)
                    .ToList();
                if (sources.Count == 0)
                {
                    unsupported.Add(field);
                    continue;
                }
                foreach (var source in sources)
                {
                    var index = 0;
                    foreach (var entry in source.EnumerateArray())
                    {
                        var pointer = $"/{field}/{index}";
                        if (entry.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(entry.GetString()))
                            return Invalid(DiagnosticCodes.InvalidSourceEntry, pointer, "Source array entry must be a non-empty string.");
                        candidates.Add(new(field, index, pointer, entry.GetString()!));
                        index++;
                    }
                }
            }

            if (candidates.Count == 0)
                return Invalid(DiagnosticCodes.NoObligationCandidates, "", "Task contract has no deliverables and no required_contract_decisions or required_invariants.");

            var obligations = new List<Obligation>(candidates.Count);
            var coverage = new List<GeneratedObligationCoverage>(candidates.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                var obligationId = $"{taskContractId}:{candidate.Field}:{candidate.Index}";
                if (!ids.Add(obligationId))
                    return Invalid(DiagnosticCodes.DuplicateGeneratedObligationId, candidate.Pointer, "Generated obligation id is not unique.");
                obligations.Add(new(obligationId, candidate.Description));
                coverage.Add(new(obligationId, "derived-automatic", candidate.Field, candidate.Pointer));
            }

            var ledger = new ObligationLedger("1.0", traceId, obligations, []);
            var ledgerBytes = ObligationLedgerNormalizer.Normalize(ledger);
            var validation = ObligationLedgerValidator.ParseAndValidatePhaseA(ledgerBytes);
            if (!validation.IsValid)
                return new(null, null, null, null, validation.Diagnostics);

            var ledgerDigest = Digest(ledgerBytes);
            var report = new LedgerGenerationReport(
                "1.0",
                "LedgerGenerationReport",
                new("progresstrace-ledger-generator", "1.0.0"),
                DateTimeOffset.UnixEpoch,
                taskContractId,
                NormalizePath(taskContractPath),
                Digest(taskContractBytes.Span),
                traceId,
                ledgerDigest,
                coverage,
                unsupported);
            var reportBytes = LedgerGenerationReportNormalizer.Normalize(report);
            return new(ledger, report, ledgerBytes, reportBytes, []);
        }
    }

    private static string NormalizePath(string path)
    {
        var relative = Path.IsPathRooted(path)
            ? Path.GetRelativePath(Directory.GetCurrentDirectory(), Path.GetFullPath(path))
            : path;
        return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string Digest(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static LedgerGenerationResult Invalid(string code, string pointer, string message) =>
        new(null, null, null, null, [new(code, pointer, message)]);

    private sealed record Candidate(string Field, int Index, string Pointer, string Description);
}
