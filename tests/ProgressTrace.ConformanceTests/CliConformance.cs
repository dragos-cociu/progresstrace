using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ProgressTrace.Core.Validation;

static class CliConformance
{
    public static void Assert(string root, List<string> failures)
    {
        var cli = Directory.EnumerateFiles(
                Path.Combine(root, "src", "ProgressTrace.Cli", "bin", "Release"),
                "ProgressTrace.Cli.dll", SearchOption.AllDirectories)
            .SingleOrDefault();
        if (cli is null)
        {
            failures.Add("cli: built CLI assembly was not found");
            return;
        }

        var temp = Path.Combine(Path.GetTempPath(), $"progresstrace-conformance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            const string marker = "SYNTHETIC_UNTRUSTED_MARKER";
            var missingTrace = Path.Combine(temp, marker + "-missing-trace.json");
            var missingLedger = Path.Combine(temp, marker + "-missing-ledger.json");
            var missingDeclaration = Path.Combine(temp, marker + "-missing-declaration.json");
            var malformedTrace = Write(temp, "malformed-trace.json", Encoding.UTF8.GetBytes("{\"x\":\"SYNTHETIC_UNTRUSTED_MARKER\""));
            var malformedLedger = Write(temp, "malformed-ledger.json", Encoding.UTF8.GetBytes("{\"x\":\"SYNTHETIC_UNTRUSTED_MARKER\""));
            var malformedDeclaration = Write(temp, "malformed-declaration.json", Encoding.UTF8.GetBytes("{\"producerName\":\"SYNTHETIC_UNTRUSTED_MARKER\""));
            var oversizedTrace = WriteOversized(temp, "oversized-trace.json");
            var oversizedLedger = WriteOversized(temp, "oversized-ledger.json");
            var oversizedDeclaration = WriteOversized(temp, "oversized-declaration.json");
            var malformedBaseline = Write(temp, "malformed-baseline.json", Encoding.UTF8.GetBytes("{\"producerName\":\"SYNTHETIC_UNTRUSTED_MARKER\""));
            var oversizedBaseline = WriteOversized(temp, "oversized-baseline.json");
            var missingBaseline = Path.Combine(temp, marker + "-missing-baseline.json");
            var boundary = Write(temp, "boundary.json", new byte[TraceValidator.MaximumInputSizeBytes]);
            var validTrace = Path.Combine(root, "fixtures", "valid", "multi-event-trace.json");
            var validLedger = Path.Combine(root, "fixtures", "obligations", "valid", "partial-progress-ledger.json");
            var invalidLedger = Path.Combine(root, "fixtures", "obligations", "invalid", "mismatched-trace-id.PT201.json");
            var assessLedger = Path.Combine(root, "fixtures", "termination", "ledgers", "valid", "on-target-ledger.json");
            var validDeclaration = Path.Combine(root, "fixtures", "termination", "valid", "on-target-declaration.json");
            var validBaseline = Path.Combine(root, "fixtures", "baseline", "definitions", "valid", "stable-attainment-baseline.json");
            var invalidBaseline = Path.Combine(root, "fixtures", "baseline", "definitions", "invalid", "trace-id-mismatch.PT401.json");

            Check("bad usage", Run(cli), 2, stdout: false, stderr: true, failures, marker);
            Check("unreadable trace", Run(cli, "evaluate", missingTrace, missingLedger), 2, false, true, failures, marker);
            CheckCode("malformed trace", Run(cli, "evaluate", malformedTrace, missingLedger), 2, "PT000", failures, marker);
            CheckCode("oversized trace", Run(cli, "evaluate", oversizedTrace, missingLedger), 2, "PT005", failures, marker);
            CheckCode("exact boundary", Run(cli, "validate", boundary), 2, "PT000", failures, marker);
            Check("unreadable ledger", Run(cli, "evaluate", validTrace, missingLedger), 2, false, true, failures, marker);
            CheckCode("malformed ledger", Run(cli, "evaluate", validTrace, malformedLedger), 2, "PT000", failures, marker);
            CheckCode("oversized ledger", Run(cli, "evaluate", validTrace, oversizedLedger), 2, "PT005", failures, marker);
            CheckCode("invalid ledger", Run(cli, "evaluate", validTrace, invalidLedger), 1, "PT201", failures, marker);
            var success = Run(cli, "evaluate", validTrace, validLedger);
            Check("valid pair", success, 0, true, false, failures, marker);
            var golden = File.ReadAllText(Path.Combine(root, "fixtures", "obligations", "golden", "partial-progress-ledger.json"));
            if (success.Stdout != golden + (golden.EndsWith('\n') ? "" : "\n"))
                failures.Add("cli valid pair: stdout did not equal the canonical golden result");

            Check("assess bad usage", Run(cli, "assess", validTrace, assessLedger), 2, false, true, failures, marker);
            Check("assess unreadable declaration", Run(cli, "assess", validTrace, assessLedger, missingDeclaration), 2, false, true, failures, marker);
            CheckCode("assess malformed declaration", Run(cli, "assess", validTrace, assessLedger, malformedDeclaration), 2, "PT000", failures, marker);
            CheckCode("assess oversized declaration", Run(cli, "assess", validTrace, assessLedger, oversizedDeclaration), 2, "PT005", failures, marker);
            foreach (var code in new[] { "PT300", "PT301", "PT302", "PT303" })
            {
                var invalid = Directory.EnumerateFiles(Path.Combine(root, "fixtures", "termination", "invalid"), $"*.{code}.json").First();
                CheckCode($"assess invalid declaration {code}", Run(cli, "assess", validTrace, assessLedger, invalid), 1, code, failures, marker);
            }
            var assessSuccess = Run(cli, "assess", validTrace, assessLedger, validDeclaration);
            Check("assess valid triple", assessSuccess, 0, true, false, failures, marker);
            var assessGolden = File.ReadAllText(Path.Combine(root, "fixtures", "termination", "golden", "on-target-declaration.json"));
            if (assessSuccess.Stdout != assessGolden + (assessGolden.EndsWith('\n') ? "" : "\n"))
                failures.Add("cli assess valid triple: stdout did not equal the canonical golden result");

            const string usageMessage = "Usage: progresstrace <validate|normalize> <path> | progresstrace evaluate <trace-path> <ledger-path> | progresstrace assess <trace-path> <ledger-path> <termination-declaration-path> | progresstrace compare <trace-path> <ledger-path> <termination-declaration-path> <baseline-definition-path>";
            var usage = JsonSerializer.Serialize(new { error = "usage", message = usageMessage });
            var compareUsage = Run(cli, "compare", validTrace, assessLedger, validDeclaration);
            Check("compare bad usage", compareUsage, 2, false, true, failures, marker);
            if (compareUsage.Stderr != usage + Environment.NewLine)
                failures.Add("cli compare bad usage: stderr did not equal the exact usage contract");

            // Each failure is paired with unreadable marker-bearing later paths. If ordering regresses,
            // the resulting input error (or marker echo) makes the asserted earlier response fail.
            CheckInputError("compare unreadable trace precedence", Run(cli, "compare", missingTrace, missingLedger, missingDeclaration, missingBaseline), "trace", failures, marker);
            CheckDiagnostic("compare malformed trace precedence", Run(cli, "compare", malformedTrace, missingLedger, missingDeclaration, missingBaseline), 2, "trace", "PT000", failures, marker);
            CheckDiagnostic("compare invalid ledger precedence", Run(cli, "compare", validTrace, invalidLedger, missingDeclaration, missingBaseline), 1, "ledger", "PT201", failures, marker);
            CheckDiagnostic("compare invalid declaration precedence", Run(cli, "compare", validTrace, assessLedger, malformedDeclaration, missingBaseline), 2, "declaration", "PT000", failures, marker);

            CheckDiagnostic("compare malformed baseline", Run(cli, "compare", validTrace, assessLedger, validDeclaration, malformedBaseline), 2, "baseline", "PT000", failures, marker);
            CheckDiagnostic("compare oversized baseline", Run(cli, "compare", validTrace, assessLedger, validDeclaration, oversizedBaseline), 2, "baseline", "PT005", failures, marker);
            CheckInputError("compare unreadable baseline", Run(cli, "compare", validTrace, assessLedger, validDeclaration, missingBaseline), "baseline", failures, marker);
            CheckDiagnostic("compare semantic baseline", Run(cli, "compare", validTrace, assessLedger, validDeclaration, invalidBaseline), 1, "baseline", "PT401", failures, marker);

            var compareSuccess = Run(cli, "compare", validTrace, assessLedger, validDeclaration, validBaseline);
            Check("compare valid quartet", compareSuccess, 0, true, false, failures, marker);
            var compareGolden = File.ReadAllText(Path.Combine(root, "fixtures", "baseline", "golden", "stable-attainment-baseline.json"));
            if (compareSuccess.Stdout != compareGolden + (compareGolden.EndsWith('\n') ? "" : "\n"))
                failures.Add("cli compare valid quartet: stdout did not equal the canonical Phase 2b golden result");
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static string Write(string directory, string name, byte[] bytes)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string WriteOversized(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        stream.SetLength(TraceValidator.MaximumInputSizeBytes + 1L);
        return path;
    }

    private static Result Run(string cli, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(cli);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("CLI process could not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            return new(-1, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult(), true);
        }
        return new(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult(), false);
    }

    private static void CheckCode(string name, Result result, int exit, string code, List<string> failures, string marker)
    {
        Check(name, result, exit, true, false, failures, marker);
        try
        {
            using var document = JsonDocument.Parse(result.Stdout);
            var diagnostics = document.RootElement.GetProperty("diagnostics");
            if (diagnostics.GetArrayLength() != 1 || diagnostics[0].GetProperty("code").GetString() != code)
                failures.Add($"cli {name}: expected only {code}");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            failures.Add($"cli {name}: stdout was not a diagnostic response");
        }
    }

    private static void CheckDiagnostic(string name, Result result, int exit, string expectedDocument, string code, List<string> failures, string marker)
    {
        Check(name, result, exit, true, false, failures, marker);
        try
        {
            using var response = JsonDocument.Parse(result.Stdout);
            var root = response.RootElement;
            var diagnostics = root.GetProperty("diagnostics");
            if (root.GetProperty("document").GetString() != expectedDocument)
                failures.Add($"cli {name}: expected document={expectedDocument}");
            if (diagnostics.GetArrayLength() != 1 || diagnostics[0].GetProperty("code").GetString() != code)
                failures.Add($"cli {name}: expected only {code}");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            failures.Add($"cli {name}: stdout was not a diagnostic response");
        }
    }

    private static void CheckInputError(string name, Result result, string expectedDocument, List<string> failures, string marker)
    {
        Check(name, result, 2, false, true, failures, marker);
        try
        {
            using var response = JsonDocument.Parse(result.Stderr);
            var root = response.RootElement;
            if (root.GetProperty("error").GetString() != "input" || root.GetProperty("document").GetString() != expectedDocument)
                failures.Add($"cli {name}: expected input error for document={expectedDocument}");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            failures.Add($"cli {name}: stderr was not an input error response");
        }
    }

    private static void Check(string name, Result result, int exit, bool stdout, bool stderr, List<string> failures, string marker)
    {
        if (result.TimedOut) failures.Add($"cli {name}: timed out");
        if (result.ExitCode != exit) failures.Add($"cli {name}: expected exit {exit}, got {result.ExitCode}");
        if (string.IsNullOrEmpty(result.Stdout) != !stdout) failures.Add($"cli {name}: stdout contract failed");
        if (string.IsNullOrEmpty(result.Stderr) != !stderr) failures.Add($"cli {name}: stderr contract failed");
        if (result.Stdout.Contains(marker, StringComparison.Ordinal) || result.Stderr.Contains(marker, StringComparison.Ordinal))
            failures.Add($"cli {name}: output echoed an untrusted path or payload");
    }

    private sealed record Result(int ExitCode, string Stdout, string Stderr, bool TimedOut);
}
