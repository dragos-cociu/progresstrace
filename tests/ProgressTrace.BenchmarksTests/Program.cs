using System.Text.Json;
using ProgressTrace.Benchmarks;

var root = FindRepositoryRoot();
var failures = new List<string>();
var input = Path.Combine(root, "fixtures", "benchmarks");
var files = Directory.EnumerateFiles(input, "*.json").Order(StringComparer.Ordinal).ToList();

Assert(files.Count == 60, $"expected 60 benchmark cases, found {files.Count}", failures);

var first = BenchmarkRunner.Run(input);
var second = BenchmarkRunner.Run(input);
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var firstBytes = JsonSerializer.SerializeToUtf8Bytes(first, options);
var secondBytes = JsonSerializer.SerializeToUtf8Bytes(second, options);
Assert(firstBytes.AsSpan().SequenceEqual(secondBytes), "runner results were not deterministic", failures);
Assert(first.CaseCount == 60 && first.Cases.Count == 60, "runner case count was incorrect", failures);
Assert(first.Summary is { CaseCount: 60, MatchedCaseCount: 60, MismatchedCaseCount: 0, Status: "pass" }, "aggregate summary was incorrect", failures);
Assert(first.Cases.Select(item => item.CaseId).SequenceEqual(first.Cases.Select(item => item.CaseId).Order(StringComparer.Ordinal)), "runner case order was not ordinal", failures);
Assert(first.Cases.All(item => item.Status.Overall == "match"), "a complete corpus case did not match its authored expectation", failures);
Assert(first.Cases.All(item => item.GroundTruth is not null), "a benchmark case had no ground truth", failures);

var byId = first.Cases.ToDictionary(item => item.CaseId, StringComparer.Ordinal);
Assert(byId.Values.Select(item => item.GroundTruth.TraceClassification).ToHashSet(StringComparer.Ordinal)
    .SetEquals(["progress", "insufficient-evidence", "stagnation", "regression"]), "classification categories were incomplete", failures);
Assert(byId.Values.Any(item => item.GroundTruth.MaxTurns.Triggered) && byId.Values.Any(item => !item.GroundTruth.MaxTurns.Triggered), "max-turn categories were incomplete", failures);
Assert(byId.Values.Any(item => item.GroundTruth.ExactRepeat.Kind == "exact-repeat") && byId.Values.Any(item => !item.GroundTruth.ExactRepeat.Detected), "exact-repeat categories were incomplete", failures);
Assert(byId.Values.Any(item => item.GroundTruth.FuzzyRepeatCycle.Kind == "fuzzy-repeat")
    && byId.Values.Any(item => item.GroundTruth.FuzzyRepeatCycle.Kind == "cycle")
    && byId.Values.Any(item => !item.GroundTruth.FuzzyRepeatCycle.Detected), "fuzzy-repeat/cycle categories were incomplete", failures);

AssertCase(byId, "06-exact-repeat", item => item.ExactRepeat.Detected && item.ExactRepeat.Kind == "exact-repeat", failures);
AssertCase(byId, "07-fuzzy-repeat", item => item.FuzzyRepeatCycle.Detected && item.FuzzyRepeatCycle.Kind == "fuzzy-repeat", failures);
AssertCase(byId, "08-cycle-ab", item => item.FuzzyRepeatCycle.Detected && item.FuzzyRepeatCycle.Kind == "cycle", failures);
AssertCase(byId, "10-no-repeat", item => !item.ExactRepeat.Detected && !item.FuzzyRepeatCycle.Detected, failures);
AssertCase(byId, "05-max-turns", item => item.MaxTurns.Triggered && item.MaxTurns.StopRank == 2, failures);
AssertCase(byId, "19-short-max-turns", item => !item.MaxTurns.Triggered, failures);
AssertCase(byId, "01-progress", item => item.Evaluation.TraceClassification == "progress", failures);
AssertCase(byId, "02-insufficient", item => item.Evaluation.TraceClassification == "insufficient-evidence", failures);
AssertCase(byId, "03-regression", item => item.Evaluation.TraceClassification == "regression", failures);
AssertCase(byId, "04-stagnation", item => item.Evaluation.TraceClassification == "stagnation", failures);

var temporaryRoot = Path.Combine(Path.GetTempPath(), $"progresstrace-benchmark-tests-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryRoot);
try
{
    var mismatchPath = CopyCase(input, temporaryRoot, "01-progress");
    File.WriteAllText(mismatchPath, File.ReadAllText(mismatchPath).Replace(
        "\"traceClassification\":\"progress\"", "\"traceClassification\":\"regression\"", StringComparison.Ordinal));
    var mismatch = BenchmarkRunner.Run(temporaryRoot);
    Assert(mismatch.Summary is { MatchedCaseCount: 0, MismatchedCaseCount: 1, Status: "fail" }, "mismatch did not fail the aggregate result", failures);
    Assert(mismatch.Cases.Single().Status is { Overall: "mismatch", TraceClassification: "mismatch" }, "mismatch status did not identify the classification field", failures);
    Assert(mismatch.Cases.Single().Evaluation.TraceClassification == "progress" && mismatch.Cases.Single().GroundTruth.TraceClassification == "regression", "actual and authored values were not kept distinct", failures);

    var malformedPath = Path.Combine(temporaryRoot, "malformed.json");
    File.WriteAllText(malformedPath, "{\"caseId\":\"malformed\",\"maxTurns\":1,\"groundTruth\":{}}");
    ExpectInvalidData(() => BenchmarkRunner.Run(temporaryRoot), "malformed metadata was accepted", failures);
}
finally
{
    Directory.Delete(temporaryRoot, recursive: true);
}

if (failures.Count == 0)
{
    Console.WriteLine($"PASS: {first.CaseCount} benchmark cases, authored ground truth, mismatch validation, and deterministic output.");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine($"FAIL: {failure}");
return 1;

static void Assert(bool condition, string failure, List<string> failures)
{
    if (!condition) failures.Add(failure);
}

static void AssertCase(
    IReadOnlyDictionary<string, BenchmarkCaseResult> cases,
    string caseId,
    Func<BenchmarkCaseResult, bool> predicate,
    List<string> failures)
{
    if (!cases.TryGetValue(caseId, out var item) || !predicate(item)) failures.Add($"assertion failed for {caseId}");
}

static void ExpectInvalidData(Action action, string failure, List<string> failures)
{
    try
    {
        action();
        failures.Add(failure);
    }
    catch (InvalidDataException)
    {
    }
}

static string CopyCase(string input, string output, string caseId)
{
    var source = Path.Combine(input, $"{caseId}.json");
    var target = Path.Combine(output, $"{caseId}.json");
    File.Copy(source, target);
    return target;
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProgressTrace.slnx"))) directory = directory.Parent;
    return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
}
