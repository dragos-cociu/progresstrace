using System.Text.Json;
using ProgressTrace.Benchmarks;

var root = FindRepositoryRoot();
var failures = new List<string>();
var input = Path.Combine(root, "fixtures", "benchmarks");
var files = Directory.EnumerateFiles(input, "*.json").Order(StringComparer.Ordinal).ToList();
if (files.Count != 20) failures.Add($"expected 20 benchmark cases, found {files.Count}");

var first = BenchmarkRunner.Run(input);
var second = BenchmarkRunner.Run(input);
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var firstBytes = JsonSerializer.SerializeToUtf8Bytes(first, options);
var secondBytes = JsonSerializer.SerializeToUtf8Bytes(second, options);
if (!firstBytes.AsSpan().SequenceEqual(secondBytes)) failures.Add("runner results were not deterministic");
if (first.CaseCount != files.Count || first.Cases.Count != files.Count) failures.Add("runner case count was incorrect");
if (first.Cases.Select(item => item.CaseId).SequenceEqual(first.Cases.Select(item => item.CaseId).Order(StringComparer.Ordinal)) is false)
    failures.Add("runner case order was not ordinal");

var byId = first.Cases.ToDictionary(item => item.CaseId, StringComparer.Ordinal);
Assert(byId, "06-exact-repeat", item => item.ExactRepeat.Detected && item.ExactRepeat.Kind == "exact-repeat", failures);
Assert(byId, "07-fuzzy-repeat", item => item.FuzzyRepeatCycle.Detected && item.FuzzyRepeatCycle.Kind == "fuzzy-repeat", failures);
Assert(byId, "08-cycle-ab", item => item.FuzzyRepeatCycle.Detected && item.FuzzyRepeatCycle.Kind == "cycle", failures);
Assert(byId, "10-no-repeat", item => !item.ExactRepeat.Detected && !item.FuzzyRepeatCycle.Detected, failures);
Assert(byId, "05-max-turns", item => item.MaxTurns.Triggered && item.MaxTurns.StopRank == 2, failures);
Assert(byId, "19-short-max-turns", item => !item.MaxTurns.Triggered, failures);
Assert(byId, "01-progress", item => item.Evaluation.TraceClassification == "progress", failures);
Assert(byId, "02-insufficient", item => item.Evaluation.TraceClassification == "insufficient-evidence", failures);
Assert(byId, "03-regression", item => item.Evaluation.TraceClassification == "regression", failures);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS: {first.CaseCount} benchmark cases and deterministic baselines.");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine($"FAIL: {failure}");
return 1;

static void Assert(
    IReadOnlyDictionary<string, BenchmarkCaseResult> cases,
    string caseId,
    Func<BenchmarkCaseResult, bool> predicate,
    List<string> failures)
{
    if (!cases.TryGetValue(caseId, out var item) || !predicate(item)) failures.Add($"assertion failed for {caseId}");
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProgressTrace.slnx"))) directory = directory.Parent;
    return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
}
