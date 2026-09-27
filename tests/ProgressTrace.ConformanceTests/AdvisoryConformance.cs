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
        AssertGolden(root, session, ledger, failures);
        AssertSeriesCases(failures);
        AssertCli(root, failures);
    }

    // The golden advisory must equal what advise emits for its fixture; before the 1.0.0 bugfix it
    // encoded F1 (a single failure reported as recovery) and was never compared.
    private static void AssertGolden(string root, ProgressTrace.Core.Models.AgentSession session, ProgressTrace.Core.Models.ObligationLedger ledger, List<string> failures)
    {
        var result = AdvisoryAssembler.Assemble(session, ledger, ParseOutcomes(Path.Combine(root, "fixtures", "advisory", "valid", "outcomes-continue.json"))).Result!;
        var golden = File.ReadAllBytes(Path.Combine(root, "fixtures", "advisory", "golden", "advisory-continue.json"));
        if (!AdvisoryResultNormalizer.Normalize(result).AsSpan().SequenceEqual(golden)) failures.Add("advisory golden advisory-continue.json differs from advise output");
    }

    // Cases observed in the Phase 5 adversarial series (ADR-0017): F5 masking and F1 labels.
    private static void AssertSeriesCases(List<string> failures)
    {
        const string ledger = """{"schemaVersion":"1.0","traceId":"t","obligations":[{"id":"o","description":"obligation"}],"signals":[]}""";
        const string session = """{"schemaVersion":"1.0","sessionId":"s","taskContractId":"c","invocations":[{"invocationId":"i1","sequence":1,"attempt":1,"traceId":"t","obligationIds":["o"],"startedAt":"2026-01-01T00:00:00Z","endedAt":"2026-01-01T00:10:00Z"},{"invocationId":"i2","sequence":2,"attempt":2,"traceId":"t","obligationIds":["o"],"startedAt":"2026-01-01T00:20:00Z","endedAt":"2026-01-01T00:30:00Z"}]}""";
        static string O(string id, string inv, int seq, string cmd, string verdict, int minute) =>
            $$"""{"schemaVersion":"1.0","outcomeId":"{{id}}","sessionId":"s","invocationId":"{{inv}}","sequence":{{seq}},"command":"{{cmd}}","exitCode":{{(verdict == "pass" ? 0 : 1)}},"timestamp":"2026-01-01T00:{{minute:00}}:00Z","obligationId":"o","verdict":"{{verdict}}","sourceDigest":"{{new string('a', 64)}}"}""";
        var cases = new (string Name, string[] Outcomes, string Status, string Classification, bool Stable, string Top, string Recommendation)[]
        {
            ("F5 failing gate then other passing gate", [O("a", "i1", 1, "gate-a", "fail", 1), O("b", "i1", 1, "gate-b", "pass", 2)], "regressed", "failed-attempt", false, "failed-attempt", "continue"),
            ("F5 failing gate not rerun, other gate passes again", [O("a", "i1", 1, "gate-a", "fail", 1), O("b", "i1", 1, "gate-b", "pass", 2), O("c", "i2", 2, "gate-b", "pass", 21)], "regressed", "failed-attempt", false, "failed-attempt", "continue"),
            ("F1 single failure is not recovery", [O("a", "i1", 1, "gate-a", "fail", 1)], "regressed", "failed-attempt", false, "failed-attempt", "continue"),
            ("F1 two passing gates in one attempt are progress", [O("a", "i1", 1, "check", "pass", 1), O("b", "i1", 1, "build", "pass", 2)], "satisfied", "progress", true, "progress", "stop-recommended"),
            ("same gate passing twice across attempts is progress", [O("a", "i1", 1, "gate-a", "pass", 1), O("b", "i2", 2, "gate-a", "pass", 21)], "satisfied", "progress", true, "progress", "stop-recommended"),
            ("recovery after failed attempt", [O("a", "i1", 1, "gate-a", "fail", 1), O("b", "i2", 2, "gate-a", "pass", 21)], "satisfied", "recovery-after-failed-attempt", true, "recovery-after-failed-attempt", "stop-recommended"),
            ("repeated attempt without advancement", [O("a", "i1", 1, "gate-a", "fail", 1), O("b", "i2", 2, "gate-a", "fail", 21)], "regressed", "repeated-attempt-without-obligation-advancement", false, "repeated-attempt-without-obligation-advancement", "continue"),
            ("skipped only is insufficient evidence", [O("a", "i1", 1, "gate-a", "skipped", 1)], "in-progress", "insufficient-evidence", false, "insufficient-evidence", "insufficient-evidence"),
        };
        var parsedSession = AgentSessionValidator.ParseAndValidate(System.Text.Encoding.UTF8.GetBytes(session)).Session!;
        var parsedLedger = ObligationLedgerValidator.ParseAndValidatePhaseA(System.Text.Encoding.UTF8.GetBytes(ledger)).Ledger!;
        foreach (var c in cases)
        {
            var outcomes = c.Outcomes.Select(o => GateOutcomeValidator.ParseAndValidate(System.Text.Encoding.UTF8.GetBytes(o)).Outcome!).ToArray();
            var r = AdvisoryAssembler.Assemble(parsedSession, parsedLedger, outcomes);
            if (r.Result is null) { failures.Add($"advisory series case '{c.Name}': no result"); continue; }
            var e = r.Result.Obligations.Single();
            if (e.Status != c.Status || e.Classification != c.Classification || e.Stable != c.Stable || r.Result.Classification != c.Top || r.Result.Recommendation != c.Recommendation)
                failures.Add($"advisory series case '{c.Name}': got {e.Status}/{e.Classification}/stable={e.Stable}/{r.Result.Classification}/{r.Result.Recommendation}");
            var reparsed = AdvisoryResultValidator.ParseAndValidate(AdvisoryResultNormalizer.Normalize(r.Result));
            if (!reparsed.IsValid) failures.Add($"advisory series case '{c.Name}': result does not validate");
        }
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
