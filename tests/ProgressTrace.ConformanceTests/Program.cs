using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Validation;

var failures = new List<string>();
var repositoryRoot = FindRepositoryRoot();
var validDirectory = Path.Combine(repositoryRoot, "fixtures", "valid");
var invalidDirectory = Path.Combine(repositoryRoot, "fixtures", "invalid");

foreach (var path in Directory.EnumerateFiles(validDirectory, "*.json").Order(StringComparer.Ordinal))
{
    var result = TraceValidator.ParseAndValidate(File.ReadAllBytes(path));
    if (!result.IsValid)
    {
        failures.Add($"{Path.GetFileName(path)}: expected valid; got {string.Join(",", result.Diagnostics.Select(static item => item.Code))}");
        continue;
    }

    var first = TraceNormalizer.Normalize(result.Envelope!);
    var reparsed = TraceValidator.ParseAndValidate(first);
    if (!reparsed.IsValid)
    {
        failures.Add($"{Path.GetFileName(path)}: normalized output did not validate");
        continue;
    }

    var second = TraceNormalizer.Normalize(reparsed.Envelope!);
    if (!first.AsSpan().SequenceEqual(second))
    {
        failures.Add($"{Path.GetFileName(path)}: normalization was not byte-idempotent");
    }
}

foreach (var path in Directory.EnumerateFiles(invalidDirectory, "*.json").Order(StringComparer.Ordinal))
{
    var expected = Path.GetFileNameWithoutExtension(path).Split('.').Last();
    var result = TraceValidator.ParseAndValidate(File.ReadAllBytes(path));
    if (result.IsValid || !result.Diagnostics.Any(item => item.Code == expected))
    {
        failures.Add($"{Path.GetFileName(path)}: expected diagnostic {expected}");
    }
}

AssertInputSizeBoundary(failures);
AssertDuplicateDiagnosticsAreRedacted(failures);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS: {Directory.EnumerateFiles(validDirectory, "*.json").Count()} valid and {Directory.EnumerateFiles(invalidDirectory, "*.json").Count()} invalid fixtures.");
    return 0;
}

foreach (var failure in failures)
{
    Console.Error.WriteLine($"FAIL: {failure}");
}
Console.Error.WriteLine($"FAIL: {failures.Count} conformance assertion(s).");
return 1;

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProgressTrace.slnx")))
    {
        directory = directory.Parent;
    }
    return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
}

static void AssertInputSizeBoundary(List<string> failures)
{
    var atLimit = new byte[TraceValidator.MaximumInputSizeBytes];
    var atLimitResult = TraceValidator.ParseAndValidate(atLimit);
    if (atLimitResult.Diagnostics.Any(static item => item.Code == "PT005"))
    {
        failures.Add("input-size: input at the 16 MiB boundary was rejected as too large");
    }

    var overLimit = new byte[TraceValidator.MaximumInputSizeBytes + 1];
    overLimit.AsSpan().Fill((byte)'x');
    var overLimitResult = TraceValidator.ParseAndValidate(overLimit);
    if (overLimitResult.Diagnostics.Count != 1 ||
        overLimitResult.Diagnostics[0].Code != "PT005")
    {
        failures.Add("input-size: input above the 16 MiB boundary did not fail with only PT005");
    }
}

static void AssertDuplicateDiagnosticsAreRedacted(List<string> failures)
{
    const string secretValue = "SYNTHETIC_SECRET_VALUE";
    var input = System.Text.Encoding.UTF8.GetBytes(
        """{"schemaVersion":"1.0","traceId":"t","source":{"name":"n","version":"v"},"createdAt":"2026-01-02T00:00:00Z","events":[{"id":"e","sequence":0,"timestamp":"2026-01-02T00:00:00Z","type":"t","actor":"other","payload":{"nested":{"key":"SYNTHETIC_SECRET_VALUE","key":"other"}},"provenance":{"sourceEventId":"s"}}]}""");
    var result = TraceValidator.ParseAndValidate(input);
    var duplicate = result.Diagnostics.SingleOrDefault(static item => item.Code == "PT004");
    if (duplicate is null)
    {
        failures.Add("duplicate-property: nested payload duplicate was not rejected");
    }
    else if (duplicate.Message.Contains(secretValue, StringComparison.Ordinal))
    {
        failures.Add("duplicate-property: diagnostic echoed a payload value");
    }
}
