using System.Diagnostics;
using ProgressTrace.Core.Models;
using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Shadow;
using ProgressTrace.Core.Validation;

static class ShadowConformance
{
    public static void Assert(string root, List<string> failures)
    {
        var fixture = Path.Combine(root, "fixtures", "shadow-summary"); var valid = Path.Combine(fixture, "valid"); var invalid = Path.Combine(fixture, "invalid"); var golden = Path.Combine(fixture, "golden");
        var decisionBytes = File.ReadAllBytes(Path.Combine(valid, "real-decision.json")); var decision = RealDecisionRecordValidator.ParseAndValidate(decisionBytes);
        if (!decision.IsValid) failures.Add("shadow real decision fixture invalid");
        else { var normalized = RealDecisionRecordNormalizer.Normalize(decision.RealDecisionRecord!); var reparsed = RealDecisionRecordValidator.ParseAndValidate(normalized); if (!reparsed.IsValid || !normalized.AsSpan().SequenceEqual(RealDecisionRecordNormalizer.Normalize(reparsed.RealDecisionRecord!))) failures.Add("shadow real decision normalizer not idempotent"); if (!SchemaConformance.Validate(normalized, File.ReadAllBytes(Path.Combine(root, "contracts", "real-decision-record.schema.json")), out var error)) failures.Add($"shadow real decision schema: {error}"); }
        AssertAlignment(decision.RealDecisionRecord!, failures); AssertCli(root, valid, invalid, golden, failures);
    }

    private static void AssertAlignment(RealDecisionRecord decision, List<string> failures)
    {
        var advisory = AdvisoryResultValidator.ParseAndValidate(File.ReadAllBytes(Path.Combine(FindRoot(), "fixtures", "advisory", "golden", "advisory-continue.json"))).AdvisoryResult!;
        var expectations = new Dictionary<string, (bool Continue, bool Stop)>(StringComparer.Ordinal) { ["continued"] = (true, false), ["escalated"] = (true, false), ["stopped"] = (false, true), ["merged"] = (false, true), ["rejected"] = (false, true), ["abandoned"] = (false, true) };
        foreach (var item in expectations) { var real = decision with { Decision = item.Key }; var continued = ShadowAssembler.Assemble([new(1, "gate", advisory)], real); var stopped = ShadowAssembler.Assemble([new(1, "gate", advisory with { Recommendation = "stop-recommended" })], real); if (continued.Summary?.Aligned != item.Value.Continue || stopped.Summary?.Aligned != item.Value.Stop) failures.Add($"shadow alignment table {item.Key}"); }
        var insufficient = ShadowAssembler.Assemble([new(1, "gate", advisory with { Recommendation = "insufficient-evidence", Classification = "insufficient-evidence" })], decision); if (insufficient.Summary?.Aligned is not null) failures.Add("shadow insufficient alignment must be null");
        if (ShadowAssembler.Assemble([], decision).Diagnostics.SingleOrDefault()?.Code != "PT904") failures.Add("shadow PT904 internal invariant");
    }

    private static void AssertCli(string root, string valid, string invalid, string golden, List<string> failures)
    {
        var cli = Path.Combine(root, "src", "ProgressTrace.Cli", "bin", "Release", "net10.0", "ProgressTrace.Cli.dll"); var manifest = Path.Combine(valid, "manifest.json"); var decision = Path.Combine(valid, "real-decision.json");
        var success = Run(cli, "shadow-summarize", "--snapshot-manifest-path", manifest, "--real-decision-path", decision); if (success.Exit != 0 || success.Error.Length != 0 || success.Output != File.ReadAllText(Path.Combine(golden, "with-decision.json"))) failures.Add("shadow CLI golden with decision");
        var missing = Run(cli, "shadow-summarize", "--snapshot-manifest-path", Path.Combine(valid, "manifest-insufficient.json")); if (missing.Exit != 0 || !missing.Error.Contains("PT905", StringComparison.Ordinal) || missing.Output != File.ReadAllText(Path.Combine(golden, "missing-decision.json"))) failures.Add("shadow CLI PT905/golden");
        if (!SchemaConformance.Validate(System.Text.Encoding.UTF8.GetBytes(missing.Output), File.ReadAllBytes(Path.Combine(root, "contracts", "shadow-session-summary.schema.json")), out var schemaError)) failures.Add($"shadow summary schema: {schemaError}");
        var parsed = ShadowSessionSummaryValidator.ParseAndValidate(System.Text.Encoding.UTF8.GetBytes(success.Output)); if (!parsed.IsValid || !System.Text.Encoding.UTF8.GetBytes(success.Output).AsSpan().SequenceEqual(ShadowSessionSummaryNormalizer.Normalize(parsed.ShadowSessionSummary!))) failures.Add("shadow summary validator/normalizer idempotence");
        var temp = Path.Combine(Path.GetTempPath(), $"shadow-{Guid.NewGuid():N}.json"); var sentinel = System.Text.Encoding.UTF8.GetBytes("sentinel"); File.WriteAllBytes(temp, sentinel);
        var cases = new[] { ("PT900", new[] { "--snapshot-manifest-path", manifest, "--real-decision-path", Path.Combine(invalid, "real-decision.PT900.json") }), ("PT901", new[] { "--snapshot-manifest-path", Path.Combine(invalid, "manifest.PT901.json") }), ("PT902", new[] { "--snapshot-manifest-path", manifest, "--real-decision-path", Path.Combine(invalid, "real-decision.PT902.json") }), ("PT903", new[] { "--snapshot-manifest-path", Path.Combine(invalid, "manifest.PT903.json") }) };
        foreach (var item in cases) { var args = new List<string> { "shadow-summarize" }; args.AddRange(item.Item2); args.AddRange(["--out", temp]); var run = Run(cli, [.. args]); if (run.Exit != 1 || !run.Error.Contains(item.Item1, StringComparison.Ordinal) || !File.ReadAllBytes(temp).AsSpan().SequenceEqual(sentinel)) failures.Add($"shadow CLI {item.Item1} fail-closed/atomic"); }
        File.Delete(temp); var malformed = Run(cli, "shadow-summarize", "--snapshot-manifest-path", Path.Combine(invalid, "malformed-manifest.json")); if (malformed.Exit != 2) failures.Add("shadow malformed manifest exit"); var usage = Run(cli, "shadow-summarize"); if (usage.Exit != 2 || !usage.Error.Contains("Usage: progresstrace shadow-summarize", StringComparison.Ordinal)) failures.Add("shadow exact usage");
    }

    private static (int Exit, string Output, string Error) Run(string cli, params string[] arguments) { var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false }; info.ArgumentList.Add(cli); foreach (var argument in arguments) info.ArgumentList.Add(argument); using var process = Process.Start(info)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); if (!process.WaitForExit(10_000)) { process.Kill(true); process.WaitForExit(); } return (process.ExitCode, output.Result, error.Result); }
    private static string FindRoot() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProgressTrace.slnx"))) directory = directory.Parent; return directory!.FullName; }
}
