using ProgressTrace.Core.Comparison;
using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Validation;

static class BaselineComparisonConformance
{
    private static readonly string[] ValidNames =
    [
        "budget-exceeded-baseline.json", "budget-exhausted-baseline.json", "dual-unused-budget-baseline.json",
        "incomplete-observation-baseline.json", "stable-attainment-baseline.json", "unused-budget-baseline.json"
    ];

    public static void Assert(string root, List<string> failures)
    {
        var definitionDirectory = Path.Combine(root, "fixtures", "baseline", "definitions");
        var validDirectory = Path.Combine(definitionDirectory, "valid");
        var invalidDirectory = Path.Combine(definitionDirectory, "invalid");
        var goldenDirectory = Path.Combine(root, "fixtures", "baseline", "golden");
        AssertNames(validDirectory, ValidNames, failures);
        AssertNames(goldenDirectory, ValidNames, failures);
        if (Directory.EnumerateFiles(invalidDirectory, "*.json").Count() != 40)
            failures.Add("baseline invalid fixture discovery: expected 40 files");
        var definitionSchema = File.ReadAllBytes(Path.Combine(root, "contracts", "baseline-definition.schema.json"));
        var resultSchema = File.ReadAllBytes(Path.Combine(root, "contracts", "baseline-comparison-result.schema.json"));

        foreach (var name in ValidNames)
        {
            var (trace, ledger, declaration) = Context(root, name);
            var bytes = File.ReadAllBytes(Path.Combine(validDirectory, name));
            if (!SchemaConformance.Validate(bytes, definitionSchema, out var schemaError)) failures.Add($"{name}: baseline schema rejected valid fixture ({schemaError})");
            var validation = BaselineDefinitionValidator.ParseAndValidate(bytes, trace, ledger);
            if (!validation.IsValid) { failures.Add($"{name}: expected valid baseline ({string.Join(',', validation.Diagnostics.Select(d => d.Code))})"); continue; }
            var result = BaselineComparator.Compare(trace, ledger, declaration, validation.BaselineDefinition!);
            var first = BaselineComparisonResultNormalizer.Normalize(result);
            var second = BaselineComparisonResultNormalizer.Normalize(result);
            var golden = File.ReadAllBytes(Path.Combine(goldenDirectory, name));
            if (!first.AsSpan().SequenceEqual(second)) failures.Add($"{name}: comparison serialization is not deterministic");
            if (!first.AsSpan().SequenceEqual(golden)) failures.Add($"{name}: comparison differs from golden");
            if (!SchemaConformance.Validate(first, resultSchema, out schemaError)) failures.Add($"{name}: result schema rejected canonical output ({schemaError})");
            if (result.ObservedEventCount != result.TerminationRank + 1) failures.Add($"{name}: observedEventCount formula failed");
            if (!Equals(result.BaselineSource, validation.BaselineDefinition!.BaselineSource)) failures.Add($"{name}: baselineSource was not copied verbatim");
            if (name is "budget-exhausted-baseline.json" or "budget-exceeded-baseline.json" && result.AuthoredEstimateFalseHaltPresent)
                failures.Add($"{name}: null/zero rollup incorrectly reported false halt");
            if (name == "dual-unused-budget-baseline.json" && (result.MaxAuthoredEstimateUnusedEventBudget != 6 || result.MaxAuthoredEstimateUnusedEventBudget >= 9))
                failures.Add("dual-unused-budget-baseline.json: maximum/non-summation rule failed");
        }

        var defaultContext = Context(root, "stable-attainment-baseline.json");
        foreach (var path in Directory.EnumerateFiles(invalidDirectory, "*.json").Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(path);
            var context = name == "missing-obligation-coverage.PT403.json" ? Context(root, "unused-budget-baseline.json") : defaultContext;
            var validation = BaselineDefinitionValidator.ParseAndValidate(File.ReadAllBytes(path), context.Trace, context.Ledger);
            var expected = Path.GetFileNameWithoutExtension(path).Split('.').Last();
            string[] expectedSequence = name switch
            {
                "coherence-agent-historical-analysis-double.PT404.json" => ["PT404", "PT404"],
                "empty-obligation-id.PT101.json" => ["PT101", "PT402", "PT403"],
                "missing-obligation-budgets.PT001.json" => ["PT001", "PT403"],
                "missing-obligation-id.PT001.json" => ["PT001", "PT403"],
                "obligation-budgets-wrong-type.PT002.json" => ["PT002", "PT403"],
                _ => [expected]
            };
            if (!validation.Diagnostics.Select(d => d.Code).SequenceEqual(expectedSequence))
                failures.Add($"{name}: expected complete diagnostic sequence {string.Join(',', expectedSequence)}; got {string.Join(',', validation.Diagnostics.Select(d => d.Code))}");
            if (validation.Diagnostics.Any(d => d.Message.Contains("SYNTHETIC", StringComparison.Ordinal) || d.Message.Contains("release-operator-role", StringComparison.Ordinal)))
                failures.Add($"{name}: diagnostic echoed untrusted source data");
            if (expected == "PT404" && validation.Diagnostics.Any(d => d.Pointer != "/baselineSource")) failures.Add($"{name}: PT404 pointer differs");
            if (expected == "PT000" ? SchemaConformance.Validate(File.ReadAllBytes(path), definitionSchema, out _) : false)
                failures.Add($"{name}: schema accepted malformed JSON");
        }
        var oversized = new byte[TraceValidator.MaximumInputSizeBytes + 1];
        var size = BaselineDefinitionValidator.ParseAndValidate(oversized, defaultContext.Trace, defaultContext.Ledger);
        if (size.Diagnostics.Count != 1 || size.Diagnostics[0].Code != "PT005") failures.Add("baseline PT005 boundary failed");
    }

    private static (ProgressTrace.Core.Models.TraceEnvelope Trace, ProgressTrace.Core.Models.ObligationLedger Ledger, ProgressTrace.Core.Models.TerminationDeclaration Declaration) Context(string root, string name)
    {
        var dual = name == "dual-unused-budget-baseline.json";
        var ledgerName = name switch { "stable-attainment-baseline.json" => "on-target-ledger.json", _ when dual => "dual-unmet-ledger.json", _ => "mixed-rollup-ledger.json" };
        var declarationName = name switch { "stable-attainment-baseline.json" => "on-target-declaration.json", "incomplete-observation-baseline.json" => "capture-truncated-declaration.json", _ when dual => "dual-unmet-declaration.json", _ => "mixed-rollup-declaration.json" };
        var tracePath = dual ? Path.Combine(root, "fixtures", "termination", "traces", "valid", "non-contiguous-sequence-trace.json") : Path.Combine(root, "fixtures", "valid", "multi-event-trace.json");
        var trace = TraceValidator.ParseAndValidate(File.ReadAllBytes(tracePath)).Envelope!;
        var ledger = ObligationLedgerValidator.ParseAndValidate(File.ReadAllBytes(Path.Combine(root, "fixtures", "termination", "ledgers", "valid", ledgerName)), trace).Ledger!;
        var declaration = TerminationDeclarationValidator.ParseAndValidate(File.ReadAllBytes(Path.Combine(root, "fixtures", "termination", "valid", declarationName)), trace).Declaration!;
        return (trace, ledger, declaration);
    }

    private static void AssertNames(string directory, IEnumerable<string> expected, List<string> failures)
    {
        var actual = Directory.EnumerateFiles(directory, "*.json").Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expected)) failures.Add($"{Path.GetFileName(directory)} fixture discovery differs");
    }
}
