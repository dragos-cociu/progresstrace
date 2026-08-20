using System.Diagnostics;
using ProgressTrace.Core.Advisory;
using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Validation;

static class AdvisoryConformance
{
    public static void Assert(string root, List<string> failures)
    {
        var directory = Path.Combine(root, "fixtures", "advisory"); var valid = Path.Combine(directory, "valid");
        var session = AgentSessionValidator.ParseAndValidate(File.ReadAllBytes(Path.Combine(valid, "session.json"))).Session!;
        var ledger = ObligationLedgerValidator.ParseAndValidatePhaseA(File.ReadAllBytes(Path.Combine(valid, "ledger.json"))).Ledger!;
        foreach (var item in new[] { ("outcomes-continue.json", "continue"), ("outcomes-stop.json", "stop-recommended"), ("outcomes-insufficient.json", "insufficient-evidence") })
        {
            var outcomes = ParseOutcomes(Path.Combine(valid, item.Item1)); var first = AdvisoryAssembler.Assemble(session, ledger, outcomes); var second = AdvisoryAssembler.Assemble(session, ledger, outcomes);
            if (!first.IsValid || first.Result!.Recommendation != item.Item2) failures.Add($"advisory {item.Item1}: expected {item.Item2}");
            var bytes = AdvisoryResultNormalizer.Normalize(first.Result!); if (!bytes.AsSpan().SequenceEqual(AdvisoryResultNormalizer.Normalize(second.Result!))) failures.Add($"advisory {item.Item1}: not reproducible");
            if (!SchemaConformance.Validate(bytes, File.ReadAllBytes(Path.Combine(root, "contracts", "advisory-result.schema.json")), out var error)) failures.Add($"advisory {item.Item1}: schema failure {error}");
            var reparsed = AdvisoryResultValidator.ParseAndValidate(bytes); if (!reparsed.IsValid || !bytes.AsSpan().SequenceEqual(AdvisoryResultNormalizer.Normalize(reparsed.AdvisoryResult!))) failures.Add($"advisory {item.Item1}: normalizer is not idempotent");
        }
        var advisory = AdvisoryAssembler.Assemble(session, ledger, ParseOutcomes(Path.Combine(valid, "outcomes-stop.json"))).Result!; var report = AdvisoryAssembler.AssembleDivergence(advisory, ledger);
        var reportBytes = AdvisoryDivergenceReportNormalizer.Normalize(report.Report!); if (!SchemaConformance.Validate(reportBytes, File.ReadAllBytes(Path.Combine(root, "contracts", "advisory-divergence-report.schema.json")), out var reportError)) failures.Add($"divergence schema failure {reportError}");
        if (!reportBytes.AsSpan().SequenceEqual(AdvisoryDivergenceReportNormalizer.Normalize(AdvisoryAssembler.AssembleDivergence(advisory, ledger).Report!))) failures.Add("divergence report is not reproducible");
        if (report.Report!.Rollup!.AllConverged) failures.Add("divergence fixture should diverge");
        var convergedLedger = ObligationLedgerValidator.ParseAndValidatePhaseA(File.ReadAllBytes(Path.Combine(valid, "ledger-converged.json"))).Ledger!;
        if (!AdvisoryAssembler.AssembleDivergence(advisory, convergedLedger).Report!.Rollup!.AllConverged) failures.Add("converged divergence fixture should converge");
        AssertCli(root, failures);
    }

    private static IReadOnlyList<ProgressTrace.Core.Models.GateOutcome> ParseOutcomes(string path)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(path)); return doc.RootElement.EnumerateArray().Select(e => GateOutcomeValidator.ParseAndValidate(System.Text.Encoding.UTF8.GetBytes(e.GetRawText())).Outcome!).ToArray();
    }

    private static void AssertCli(string root, List<string> failures)
    {
        var cli = Path.Combine(root, "src", "ProgressTrace.Cli", "bin", "Release", "net10.0", "ProgressTrace.Cli.dll"); var v = Path.Combine(root, "fixtures", "advisory", "valid"); var invalid = Path.Combine(root, "fixtures", "advisory", "invalid");
        var cases = new[] { ("PT700", $"advise --session-path {Q(Path.Combine(invalid, "session.PT700.json"))} --ledger-path {Q(Path.Combine(v, "ledger.json"))} --gate-outcomes-array-path {Q(Path.Combine(v, "outcomes-insufficient.json"))}"), ("PT701", $"advise --session-path {Q(Path.Combine(v, "session.json"))} --ledger-path {Q(Path.Combine(invalid, "ledger.PT701.json"))} --gate-outcomes-array-path {Q(Path.Combine(v, "outcomes-insufficient.json"))}"), ("PT702", $"advise --session-path {Q(Path.Combine(v, "session.json"))} --ledger-path {Q(Path.Combine(v, "ledger.json"))} --gate-outcomes-array-path {Q(Path.Combine(invalid, "outcomes.PT702.json"))}"), ("PT704", $"advise --session-path {Q(Path.Combine(invalid, "session.PT704.json"))} --ledger-path {Q(Path.Combine(v, "ledger.json"))} --gate-outcomes-array-path {Q(Path.Combine(invalid, "outcomes.PT704.json"))}") };
        foreach (var item in cases) { var run = Run(cli, item.Item2); if (run.ExitCode != 1 || !run.Error.Contains(item.Item1, StringComparison.Ordinal)) failures.Add($"advisory CLI {item.Item1}: wrong diagnostic/exit"); }
        var pt703 = Run(cli, $"advise --session-path {Q(Path.Combine(v, "session.json"))} --ledger-path {Q(Path.Combine(v, "ledger.json"))} --gate-outcomes-array-path {Q(Path.Combine(v, "outcomes-insufficient.json"))}"); if (pt703.ExitCode != 0 || !pt703.Error.Contains("PT703", StringComparison.Ordinal) || pt703.Output.Contains("PT703", StringComparison.Ordinal)) failures.Add("advisory CLI PT703 channel behavior failed");
        var pt705 = Run(cli, $"advise --divergence-report --advisory-result-path {Q(Path.Combine(invalid, "advisory.PT705.json"))} --ledger-path {Q(Path.Combine(v, "ledger.json"))}"); if (pt705.ExitCode != 1 || !pt705.Error.Contains("PT705", StringComparison.Ordinal)) failures.Add("advisory CLI PT705 behavior failed");
        var pt706 = Run(cli, $"advise --divergence-report --advisory-result-path {Q(Path.Combine(invalid, "advisory.PT706.json"))} --ledger-path {Q(Path.Combine(v, "ledger.json"))}"); if (pt706.ExitCode != 1 || !pt706.Error.Contains("PT706", StringComparison.Ordinal)) failures.Add("advisory CLI PT706 behavior failed");
    }
    private static string Q(string value) => '"' + value + '"';
    private static (int ExitCode, string Output, string Error) Run(string cli, string arguments) { using var p = Process.Start(new ProcessStartInfo("dotnet", $"{Q(cli)} {arguments}") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })!; var output = p.StandardOutput.ReadToEnd(); var error = p.StandardError.ReadToEnd(); p.WaitForExit(); return (p.ExitCode, output, error); }
}
