using System.Text.Json;
using ProgressTrace.Core.Assessment;
using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Validation;

static class AssessmentConformance
{
    public static void Assert(string root, List<string> failures)
    {
        var termination = Path.Combine(root, "fixtures", "termination");
        var validDirectory = Path.Combine(termination, "valid");
        var invalidDirectory = Path.Combine(termination, "invalid");
        var goldenDirectory = Path.Combine(termination, "golden");
        var declarationSchema = File.ReadAllBytes(Path.Combine(root, "contracts", "termination-declaration.schema.json"));
        var resultSchema = File.ReadAllBytes(Path.Combine(root, "contracts", "stop-assessment-result.schema.json"));
        var multiTrace = TraceValidator.ParseAndValidate(File.ReadAllBytes(Path.Combine(root, "fixtures", "valid", "multi-event-trace.json"))).Envelope!;
        var nonContiguousTrace = TraceValidator.ParseAndValidate(File.ReadAllBytes(Path.Combine(termination, "traces", "valid", "non-contiguous-sequence-trace.json"))).Envelope!;
        var expectedValid = new HashSet<string>(StringComparer.Ordinal)
        {
            "all-stable-rollup-declaration.json", "capture-truncated-declaration.json", "late-termination-declaration.json",
            "mixed-rollup-declaration.json", "on-target-declaration.json", "regression-no-recovery-declaration.json",
            "regression-then-recovery-declaration.json", "unknown-declaration.json", "zero-signal-declaration.json"
        };
        var expectedInvalid = new HashSet<string>(StringComparer.Ordinal)
        {
            "malformed-json.PT000.json","unsupported-version.PT100.json","missing-termination-event-id.PT001.json",
            "missing-termination-kind.PT001.json","invalid-termination-kind.PT101.json","wrong-type-schema-version.PT002.json",
            "unknown-property.PT003.json","duplicate-property.PT004.json","dangling-termination-event-id.PT300.json",
            "non-terminal-termination-event-id.PT301.json","mismatched-trace-id.PT302.json","missing-declaration-source.PT001.json",
            "declaration-source-wrong-type.PT002.json","missing-producer-type.PT001.json","missing-producer-name.PT001.json",
            "missing-producer-version.PT001.json","missing-evidence-basis.PT001.json","declaration-source-unknown-property.PT003.json",
            "producer-type-wrong-type.PT002.json","producer-version-wrong-type.PT002.json","null-producer-name.PT002.json",
            "empty-producer-name.PT101.json","empty-producer-version.PT101.json","invalid-producer-type.PT101.json",
            "invalid-evidence-basis.PT101.json","coherence-agent-stop-basis.PT303.json","coherence-harness-stop-basis.PT303.json",
            "coherence-agent-producer-basis.PT303.json","coherence-operator-producer-basis.PT303.json",
            "coherence-adapter-inference-producer.PT303.json"
        };
        AssertSet(validDirectory, expectedValid, failures);
        AssertSet(goldenDirectory, expectedValid, failures);
        AssertSet(invalidDirectory, expectedInvalid, failures);

        var producerTypes = new HashSet<string>(StringComparer.Ordinal);
        var evidenceBases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in expectedValid.Order(StringComparer.Ordinal))
        {
            var trace = name is "on-target-declaration.json" or "capture-truncated-declaration.json" or
                "unknown-declaration.json" or "zero-signal-declaration.json" or "mixed-rollup-declaration.json"
                ? multiTrace : nonContiguousTrace;
            var bytes = File.ReadAllBytes(Path.Combine(validDirectory, name));
            if (!SchemaConformance.Validate(bytes, declarationSchema, out var schemaError))
                failures.Add($"{name}: declaration schema rejected fixture ({schemaError})");
            var declaration = TerminationDeclarationValidator.ParseAndValidate(bytes, trace);
            if (!declaration.IsValid) { failures.Add($"{name}: declaration validation failed"); continue; }
            var ledgerPath = LedgerPath(root, name);
            var ledger = ObligationLedgerValidator.ParseAndValidate(File.ReadAllBytes(ledgerPath), trace);
            if (!ledger.IsValid) { failures.Add($"{name}: paired ledger validation failed"); continue; }
            var result = StopAssessor.Assess(trace, ledger.Ledger!, declaration.Declaration!);
            var first = StopAssessmentResultNormalizer.Normalize(result);
            var second = StopAssessmentResultNormalizer.Normalize(result);
            var golden = File.ReadAllBytes(Path.Combine(goldenDirectory, name));
            if (!first.AsSpan().SequenceEqual(second) || !first.AsSpan().SequenceEqual(golden))
                failures.Add($"{name}: assessment is not byte deterministic or differs from golden");
            if (!SchemaConformance.Validate(first, resultSchema, out schemaError))
                failures.Add($"{name}: result schema rejected output ({schemaError})");
            producerTypes.Add(result.DeclarationSource.ProducerType!);
            evidenceBases.Add(result.DeclarationSource.EvidenceBasis!);
            if ((result.TerminationKind is "unknown" or "capture-truncated") &&
                (result.TerminationAttested || result.SafeStopRank is not null || result.TraceOverhead is not null))
                failures.Add($"{name}: non-attested suppression failed");
            if (result.TerminationKind is not ("unknown" or "capture-truncated") && !result.TerminationAttested)
                failures.Add($"{name}: concrete termination kind was not attested");
        }
        if (producerTypes.Count != 4 || evidenceBases.Count != 4)
            failures.Add("declaration source fixtures do not cover all producer and evidence enums");

        foreach (var path in Directory.EnumerateFiles(invalidDirectory, "*.json").Order(StringComparer.Ordinal))
        {
            var expected = Path.GetFileNameWithoutExtension(path).Split('.').Last();
            var result = TerminationDeclarationValidator.ParseAndValidate(File.ReadAllBytes(path), multiTrace);
            if (result.IsValid || result.Diagnostics.Count != 1 || result.Diagnostics[0].Code != expected)
                failures.Add($"{Path.GetFileName(path)}: expected only diagnostic {expected}, got {string.Join(",", result.Diagnostics.Select(d => d.Code))}");
            if (result.Diagnostics.Any(d => d.Message.Contains("SYNTHETIC_UNTRUSTED_MARKER", StringComparison.Ordinal)))
                failures.Add($"{Path.GetFileName(path)}: diagnostic echoed producerName");
            if (result.Diagnostics.Count == 1)
            {
                var pointer = ExpectedPointer(Path.GetFileName(path), expected);
                if (pointer is not null && result.Diagnostics[0].Pointer != pointer)
                    failures.Add($"{Path.GetFileName(path)}: expected pointer {pointer}, got {result.Diagnostics[0].Pointer}");
            }
            if (expected is not ("PT004" or "PT300" or "PT301" or "PT302" or "PT303") &&
                SchemaConformance.Validate(File.ReadAllBytes(path), declarationSchema, out _))
                failures.Add($"{Path.GetFileName(path)}: schema accepted a schema-expressible invalid declaration");
        }
        var oversized = TerminationDeclarationValidator.ParseAndValidate(
            new byte[TraceValidator.MaximumInputSizeBytes + 1], multiTrace);
        if (oversized.Diagnostics.Count != 1 || oversized.Diagnostics[0].Code != "PT005")
            failures.Add("declaration structural PT005: expected exactly one diagnostic");
        var precedence = TerminationDeclarationValidator.ParseAndValidate(
            System.Text.Encoding.UTF8.GetBytes(
                """{"schemaVersion":"1.0","traceId":"other","terminationEventId":"event-002","terminationKind":"agent-self-reported-stop","declarationSource":{"producerType":"harness","producerName":"p","producerVersion":"1","evidenceBasis":"harness-lifecycle"}}"""),
            multiTrace);
        if (!precedence.Diagnostics.Select(d => d.Code).SequenceEqual(["PT303", "PT302"]))
            failures.Add("declaration validation did not order Phase A PT303 before Phase B PT302");

        var allStable = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(goldenDirectory, "all-stable-rollup-declaration.json")));
        var obligationResults = allStable.RootElement.GetProperty("obligationResults");
        var sum = obligationResults.EnumerateArray().Sum(x => x.GetProperty("overhead").GetInt32());
        var traceOverhead = allStable.RootElement.GetProperty("traceOverhead").GetInt32();
        if (sum != 4 || traceOverhead != 1 || traceOverhead >= sum)
            failures.Add("all-stable rollup did not prove max-based non-summed overhead");
    }

    private static string LedgerPath(string root, string name)
    {
        if (name == "zero-signal-declaration.json")
            return Path.Combine(root, "fixtures", "obligations", "valid", "insufficient-evidence-ledger.json");
        var ledgerName = name.Replace("-declaration.json", "-ledger.json", StringComparison.Ordinal);
        if (name is "capture-truncated-declaration.json" or "unknown-declaration.json")
            ledgerName = "on-target-ledger.json";
        return Path.Combine(root, "fixtures", "termination", "ledgers", "valid", ledgerName);
    }

    private static string? ExpectedPointer(string name, string code) => code switch
    {
        "PT000" => "",
        "PT100" => "/schemaVersion",
        "PT300" or "PT301" => "/terminationEventId",
        "PT302" => "/traceId",
        "PT303" when name.Contains("agent-stop", StringComparison.Ordinal) || name.Contains("harness-stop", StringComparison.Ordinal)
            => "/declarationSource/evidenceBasis",
        "PT303" => "/declarationSource",
        _ when name.Contains("declaration-source-wrong", StringComparison.Ordinal) => "/declarationSource",
        _ when name.Contains("producer-type", StringComparison.Ordinal) => "/declarationSource/producerType",
        _ when name.Contains("producer-name", StringComparison.Ordinal) => "/declarationSource/producerName",
        _ when name.Contains("producer-version", StringComparison.Ordinal) => "/declarationSource/producerVersion",
        _ when name.Contains("evidence-basis", StringComparison.Ordinal) => "/declarationSource/evidenceBasis",
        _ => null
    };

    private static void AssertSet(string directory, HashSet<string> expected, List<string> failures)
    {
        var actual = Directory.EnumerateFiles(directory, "*.json").Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal)!;
        if (!actual.SetEquals(expected))
            failures.Add($"{directory}: fixture discovery differs");
    }
}
