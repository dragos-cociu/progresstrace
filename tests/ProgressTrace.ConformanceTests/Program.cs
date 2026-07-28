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
