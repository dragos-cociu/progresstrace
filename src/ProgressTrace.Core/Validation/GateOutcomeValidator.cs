using System.Text.Json;
using System.Text.RegularExpressions;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;

namespace ProgressTrace.Core.Validation;

public static class GateOutcomeValidator
{
    private static readonly HashSet<string> Verdicts = new(StringComparer.Ordinal) { "pass", "fail", "skipped", "error" };
    private static readonly Regex Digest = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static GateOutcomeValidationResult ParseAndValidate(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > TraceValidator.MaximumInputSizeBytes) return new(null, [new("PT510", "", "Gate outcome input exceeds the maximum size.")]);
        try
        {
            using var document = JsonDocument.Parse(bytes); var root = document.RootElement; var diagnostics = new List<Diagnostic>();
            if (root.ValueKind != JsonValueKind.Object) return new(null, [new(DiagnosticCodes.Type, "", "Gate outcome must be an object.")]);
            JsonValidationHelpers.RejectDuplicateProperties(root, "", diagnostics);
            JsonValidationHelpers.RejectUnknown(root, "", ["schemaVersion", "outcomeId", "sessionId", "invocationId", "sequence", "command", "exitCode", "timestamp", "obligationId", "verdict", "sourceDigest"], diagnostics);
            var version = AgentSessionValidator.String(root, "schemaVersion", "", diagnostics); var outcomeId = AgentSessionValidator.String(root, "outcomeId", "", diagnostics);
            var sessionId = AgentSessionValidator.String(root, "sessionId", "", diagnostics); var invocationId = AgentSessionValidator.String(root, "invocationId", "", diagnostics);
            var sequence = AgentSessionValidator.Integer(root, "sequence", "", diagnostics); var command = AgentSessionValidator.String(root, "command", "", diagnostics);
            var exitCode = AgentSessionValidator.Integer(root, "exitCode", "", diagnostics); var timestamp = AgentSessionValidator.Timestamp(root, "timestamp", "", diagnostics);
            var obligationId = AgentSessionValidator.String(root, "obligationId", "", diagnostics, optional: true); var verdict = AgentSessionValidator.String(root, "verdict", "", diagnostics);
            var sourceDigest = AgentSessionValidator.String(root, "sourceDigest", "", diagnostics);
            if (version is not null && version != "1.0") diagnostics.Add(new("PT511", "/schemaVersion", "Gate outcome schema version is not supported."));
            if (sequence is not null && sequence < 0) diagnostics.Add(new("PT512", "/sequence", "Sequence must be non-negative."));
            if (verdict is not null && !Verdicts.Contains(verdict)) diagnostics.Add(new("PT513", "/verdict", "Verdict is not supported."));
            if (sourceDigest is not null && !Digest.IsMatch(sourceDigest)) diagnostics.Add(new("PT514", "/sourceDigest", "Source digest must be a lowercase SHA-256 hex digest."));
            int? safeExitCode = null;
            if (exitCode is not null)
            {
                if (exitCode < int.MinValue || exitCode > int.MaxValue)
                    diagnostics.Add(new(DiagnosticCodes.ProjectionOverflow, "/exitCode", "Exit code is outside the supported integer range."));
                else
                    safeExitCode = (int)exitCode.Value;
            }
            var outcome = new GateOutcome(version, outcomeId, sessionId, invocationId, sequence, command, safeExitCode, timestamp, obligationId, verdict, sourceDigest);
            return new(outcome, diagnostics);
        }
        catch (JsonException) { return new(null, [new(DiagnosticCodes.InvalidJson, "", "Input is not valid JSON.")]); }
    }
}
