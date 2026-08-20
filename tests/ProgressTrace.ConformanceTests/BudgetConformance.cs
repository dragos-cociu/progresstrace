using System.Diagnostics;
using System.Reflection;
using ProgressTrace.Core.Budget;
using ProgressTrace.Core.Models;
using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Validation;

static class BudgetConformance
{
    public static void Assert(string root, List<string> failures)
    {
        var fixture = Path.Combine(root, "fixtures", "budget"); var valid = Path.Combine(fixture, "valid"); var invalid = Path.Combine(fixture, "invalid"); var golden = Path.Combine(fixture, "golden");
        var session = AgentSessionValidator.ParseAndValidate(File.ReadAllBytes(Path.Combine(valid, "session.json"))).Session!; var ledger = ObligationLedgerValidator.ParseAndValidatePhaseA(File.ReadAllBytes(Path.Combine(valid, "ledger.json"))).Ledger!;
        foreach (var item in new[] { ("token-usage-complete.json", "complete.json", 0), ("token-usage-missing.json", "missing.json", 1) })
        {
            var usageBytes = File.ReadAllBytes(Path.Combine(valid, item.Item1)); var usage = TokenUsageValidator.ParseAndValidate(usageBytes); if (!usage.IsValid) { failures.Add($"budget {item.Item1}: TokenUsage invalid"); continue; }
            if (!SchemaConformance.Validate(TokenUsageNormalizer.Normalize(usage.TokenUsage!), File.ReadAllBytes(Path.Combine(root, "contracts", "token-usage.schema.json")), out var usageError)) failures.Add($"budget token schema: {usageError}");
            var first = BudgetAssembler.Assemble(session, usage.TokenUsage!, ledger); var second = BudgetAssembler.Assemble(session, usage.TokenUsage!, ledger); if (!first.IsValid || first.Diagnostics.Count != item.Item3) failures.Add($"budget {item.Item1}: assembly diagnostics");
            var bytes = ObservedBudgetNormalizer.Normalize(first.Budget!); if (!bytes.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(golden, item.Item2)))) failures.Add($"budget {item.Item1}: golden mismatch");
            if (!bytes.AsSpan().SequenceEqual(ObservedBudgetNormalizer.Normalize(second.Budget!))) failures.Add($"budget {item.Item1}: not deterministic");
            if (!SchemaConformance.Validate(bytes, File.ReadAllBytes(Path.Combine(root, "contracts", "observed-budget.schema.json")), out var budgetError)) failures.Add($"budget output schema: {budgetError}");
            var reparsed = ObservedBudgetValidator.ParseAndValidate(bytes); if (!reparsed.IsValid || !bytes.AsSpan().SequenceEqual(ObservedBudgetNormalizer.Normalize(reparsed.ObservedBudget!))) failures.Add($"budget {item.Item1}: normalizer not idempotent");
        }
        AssertFailures(root, valid, invalid, failures); AssertCheckedOverflow(failures); AssertInvariant(session, ledger, failures);
    }

    private static void AssertFailures(string root, string valid, string invalid, List<string> failures)
    {
        var cli = Path.Combine(root, "src", "ProgressTrace.Cli", "bin", "Release", "net10.0", "ProgressTrace.Cli.dll"); var session = Path.Combine(valid, "session.json"); var ledger = Path.Combine(valid, "ledger.json"); var complete = Path.Combine(valid, "token-usage-complete.json");
        var cases = new[]
        {
            ("PT800", Path.Combine(invalid, "token-usage.PT800.json"), session, ledger), ("PT801", Path.Combine(invalid, "token-usage.PT801.json"), session, ledger), ("PT802", Path.Combine(invalid, "token-usage.PT802.json"), session, ledger),
            ("PT803", complete, Path.Combine(invalid, "session.PT803.json"), ledger), ("PT804", Path.Combine(invalid, "token-usage-overflow-support.json"), Path.Combine(invalid, "session.PT804.json"), ledger)
        };
        foreach (var item in cases)
        {
            var output = Path.Combine(Path.GetTempPath(), $"budget-{Guid.NewGuid():N}.json"); var run = Run(cli, $"budget --session-path {Q(item.Item3)} --token-usage-path {Q(item.Item2)} --ledger-path {Q(item.Item4)} --out {Q(output)}");
            if (run.ExitCode != 1 || !run.Error.Contains(item.Item1, StringComparison.Ordinal) || File.Exists(output)) failures.Add($"budget CLI {item.Item1}: fail-closed behavior");
        }
        var success = Run(cli, $"budget --session-path {Q(session)} --token-usage-path {Q(Path.Combine(valid, "token-usage-missing.json"))} --ledger-path {Q(ledger)}"); if (success.ExitCode != 0 || !success.Error.Contains("PT806", StringComparison.Ordinal) || success.Output.Contains("PT806", StringComparison.Ordinal)) failures.Add("budget CLI PT806 channel behavior");
        var usage = Run(cli, $"budget --session-path {Q(session)} --ledger-path {Q(ledger)}"); if (usage.ExitCode != 2) failures.Add("budget CLI usage must exit 2");
    }

    private static void AssertCheckedOverflow(List<string> failures)
    {
        var method = typeof(BudgetAssembler).GetMethod("CheckedAdd", BindingFlags.NonPublic | BindingFlags.Static)!; try { method.Invoke(null, [long.MaxValue, 1L]); failures.Add("budget token aggregation helper did not overflow"); } catch (TargetInvocationException exception) when (exception.InnerException is OverflowException) { }
    }

    private static void AssertInvariant(AgentSession session, ObligationLedger ledger, List<string> failures)
    {
        var usage = new TokenUsage("1.0", session.SessionId, session.TaskContractId, []); var malformedLedger = ledger with { Obligations = null }; var result = BudgetAssembler.Assemble(session, usage, malformedLedger); if (result.Diagnostics.SingleOrDefault()?.Code != "PT805") failures.Add("budget PT805 internal invariant");
    }

    private static string Q(string value) => '"' + value + '"';
    private static (int ExitCode, string Output, string Error) Run(string cli, string arguments) { using var process = Process.Start(new ProcessStartInfo("dotnet", $"{Q(cli)} {arguments}") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })!; var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd(); process.WaitForExit(); return (process.ExitCode, output, error); }
}
