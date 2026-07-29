using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Evaluation;
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
    if (result.IsValid || result.Diagnostics.Count != 1 || result.Diagnostics[0].Code != expected)
    {
        failures.Add($"{Path.GetFileName(path)}: expected only diagnostic {expected}");
    }
}

AssertInputSizeBoundary(failures);
AssertDuplicateDiagnosticsAreRedacted(failures);
AssertUnknownPropertyPointerEscaping(failures);
AssertObligationFixtures(repositoryRoot, failures);
AssertLedgerStructuralBranches(repositoryRoot, failures);
CliConformance.Assert(repositoryRoot, failures);

if (failures.Count == 0)
{
    var phase1Valid = Directory.EnumerateFiles(Path.Combine(repositoryRoot, "fixtures", "obligations", "valid"), "*.json").Count();
    var phase1Invalid = Directory.EnumerateFiles(Path.Combine(repositoryRoot, "fixtures", "obligations", "invalid"), "*.json").Count();
    Console.WriteLine($"PASS: Phase 0 {Directory.EnumerateFiles(validDirectory, "*.json").Count()} valid/{Directory.EnumerateFiles(invalidDirectory, "*.json").Count()} invalid; Phase 1 {phase1Valid} valid/{phase1Invalid} invalid fixtures.");
    return 0;
}

foreach (var failure in failures)
{
    Console.Error.WriteLine($"FAIL: {failure}");
}
Console.Error.WriteLine($"FAIL: {failures.Count} conformance assertion(s).");
return 1;

static void AssertObligationFixtures(string root, List<string> failures)
{
    var trace = TraceValidator.ParseAndValidate(File.ReadAllBytes(Path.Combine(root, "fixtures", "valid", "multi-event-trace.json"))).Envelope!;
    var valid = Path.Combine(root, "fixtures", "obligations", "valid");
    var invalid = Path.Combine(root, "fixtures", "obligations", "invalid");
    var golden = Path.Combine(root, "fixtures", "obligations", "golden");
    var ledgerSchema = File.ReadAllBytes(Path.Combine(root, "contracts", "obligation-ledger.schema.json"));
    var resultSchema = File.ReadAllBytes(Path.Combine(root, "contracts", "evaluation-result.schema.json"));
    var expectedValid = new HashSet<string>(StringComparer.Ordinal)
    {
        "abandoned-ledger.json", "explicit-regressed-ledger.json", "insufficient-evidence-ledger.json",
        "no-progress-ledger.json", "partial-progress-ledger.json", "rank-decline-ledger.json",
        "repeated-status-ledger.json", "single-signal-progress-ledger.json"
    };
    var expectedInvalid = new HashSet<string>(StringComparer.Ordinal)
    {
        "abandoned-followed-by-sequence.PT204.json", "abandoned-followed-by-tie-index.PT204.json",
        "dangling-event-id.PT203.json", "dangling-obligation-id.PT202.json",
        "duplicate-obligation-id.PT200.json", "empty-obligations.PT101.json",
        "malformed-json.PT000.json", "mismatched-trace-id.PT201.json", "unsupported-version.PT100.json"
    };
    AssertDiscovery(valid, expectedValid, failures);
    AssertDiscovery(invalid, expectedInvalid, failures);
    foreach (var path in Directory.EnumerateFiles(valid, "*.json").Order(StringComparer.Ordinal))
    {
        var bytes = File.ReadAllBytes(path);
        if (!SchemaConformance.Validate(bytes, ledgerSchema, out var schemaError))
            failures.Add($"{Path.GetFileName(path)}: ledger schema rejected valid fixture ({schemaError})");
        var validation = ObligationLedgerValidator.ParseAndValidate(bytes, trace);
        if (!validation.IsValid) { failures.Add($"{Path.GetFileName(path)}: expected valid ledger"); continue; }
        var first = EvaluationResultNormalizer.Normalize(Evaluator.Evaluate(trace, validation.Ledger!));
        var second = EvaluationResultNormalizer.Normalize(Evaluator.Evaluate(trace, validation.Ledger!));
        var expected = File.ReadAllBytes(Path.Combine(golden, Path.GetFileName(path)));
        if (expected.Length == 0 || expected[^1] != (byte)'\n') expected = [.. expected, (byte)'\n'];
        if (!first.AsSpan().SequenceEqual(second)) failures.Add($"{Path.GetFileName(path)}: evaluation was not byte deterministic");
        if (!first.AsSpan().SequenceEqual(expected)) failures.Add($"{Path.GetFileName(path)}: canonical result differs from golden");
        if (!SchemaConformance.Validate(first, resultSchema, out schemaError))
            failures.Add($"{Path.GetFileName(path)}: generated result failed its schema ({schemaError})");
    }
    var pointers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["PT000"] = "",
        ["PT100"] = "/schemaVersion",
        ["PT101"] = "/obligations",
        ["PT200"] = "/obligations/1/id",
        ["PT201"] = "/traceId",
        ["PT202"] = "/signals/0/obligationId",
        ["PT203"] = "/signals/0/eventId"
    };
    foreach (var path in Directory.EnumerateFiles(invalid, "*.json").Order(StringComparer.Ordinal))
    {
        var expected = Path.GetFileNameWithoutExtension(path).Split('.').Last();
        var bytes = File.ReadAllBytes(path);
        var result = ObligationLedgerValidator.ParseAndValidate(bytes, trace);
        if (result.IsValid || result.Diagnostics.Count != 1 || result.Diagnostics[0].Code != expected)
            failures.Add($"{Path.GetFileName(path)}: expected only diagnostic {expected}");
        else
        {
            var expectedPointer = expected == "PT204"
                ? "/signals/1"
                : pointers[expected];
            if (result.Diagnostics[0].Pointer != expectedPointer)
                failures.Add($"{Path.GetFileName(path)}: expected pointer {expectedPointer}; got {result.Diagnostics[0].Pointer}");
        }

        var schemaValid = SchemaConformance.Validate(bytes, ledgerSchema, out _);
        var schemaExpressible = expected is "PT000" or "PT100" or "PT101";
        if (schemaExpressible && schemaValid)
            failures.Add($"{Path.GetFileName(path)}: schema accepted a schema-expressible invalid fixture");
        // PT200-PT204 and duplicate JSON properties require runtime identity, reference,
        // ordering, or parser semantics and are intentionally outside JSON Schema.
    }
}

static void AssertDiscovery(string directory, HashSet<string> expected, List<string> failures)
{
    var actual = Directory.EnumerateFiles(directory, "*.json").Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal)!;
    if (!actual.SetEquals(expected))
        failures.Add($"{Path.GetFileName(directory)} fixture discovery differs: expected [{string.Join(",", expected.Order())}], got [{string.Join(",", actual.Order())}]");
}

static void AssertLedgerStructuralBranches(string root, List<string> failures)
{
    var trace = TraceValidator.ParseAndValidate(File.ReadAllBytes(Path.Combine(root, "fixtures", "valid", "multi-event-trace.json"))).Envelope!;
    var schema = File.ReadAllBytes(Path.Combine(root, "contracts", "obligation-ledger.schema.json"));
    var cases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["PT001"] = """{"schemaVersion":"1.0","traceId":"synthetic-multi-event","obligations":[{"id":"o","description":""}]}""",
        ["PT002"] = """{"schemaVersion":"1.0","traceId":"synthetic-multi-event","obligations":{},"signals":[]}""",
        ["PT003"] = """{"schemaVersion":"1.0","traceId":"synthetic-multi-event","obligations":[{"id":"o","description":""}],"signals":[],"SYNTHETIC_UNTRUSTED_MARKER":1}""",
        ["PT004"] = """{"schemaVersion":"1.0","traceId":"synthetic-multi-event","obligations":[{"id":"o","description":"SYNTHETIC_UNTRUSTED_MARKER","description":"x"}],"signals":[]}"""
    };
    foreach (var item in cases)
    {
        var result = ObligationLedgerValidator.ParseAndValidate(System.Text.Encoding.UTF8.GetBytes(item.Value), trace);
        if (result.Diagnostics.Count != 1 || result.Diagnostics[0].Code != item.Key)
            failures.Add($"ledger structural {item.Key}: expected exactly one diagnostic");
        if (item.Key != "PT004" && SchemaConformance.Validate(System.Text.Encoding.UTF8.GetBytes(item.Value), schema, out _))
            failures.Add($"ledger structural {item.Key}: schema accepted the invalid input");
        if (result.Diagnostics.Any(d => d.Message.Contains("SYNTHETIC_UNTRUSTED_MARKER", StringComparison.Ordinal)))
            failures.Add($"ledger structural {item.Key}: diagnostic echoed an untrusted value");
    }
    var oversized = new byte[TraceValidator.MaximumInputSizeBytes + 1];
    var size = ObligationLedgerValidator.ParseAndValidate(oversized, trace);
    if (size.Diagnostics.Count != 1 || size.Diagnostics[0].Code != "PT005")
        failures.Add("ledger structural PT005: expected exactly one diagnostic");
}

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

static void AssertUnknownPropertyPointerEscaping(List<string> failures)
{
    var input = System.Text.Encoding.UTF8.GetBytes(
        """{"schemaVersion":"1.0","traceId":"t","source":{"name":"n","version":"v"},"createdAt":"2026-01-02T00:00:00Z","events":[],"unexpected/key~token":true}""");
    var result = TraceValidator.ParseAndValidate(input);
    var unknown = result.Diagnostics.SingleOrDefault(static item => item.Code == "PT003");
    if (unknown is null)
    {
        failures.Add("unknown-property-pointer: property containing '/' and '~' was not rejected");
    }
    else if (unknown.Pointer != "/unexpected~1key~0token")
    {
        failures.Add($"unknown-property-pointer: expected /unexpected~1key~0token; got {unknown.Pointer}");
    }
}
