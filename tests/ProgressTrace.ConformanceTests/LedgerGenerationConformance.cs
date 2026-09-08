using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProgressTrace.Core.Generation;
using ProgressTrace.Core.Validation;

static class LedgerGenerationConformance
{
    public static void Assert(string root, List<string> failures)
    {
        var fixtureRoot = Path.Combine(root, "fixtures", "ledger-generation");
        var valid = Path.Combine(fixtureRoot, "valid");
        var invalid = Path.Combine(fixtureRoot, "invalid");
        var reportSchema = File.ReadAllBytes(Path.Combine(root, "contracts", "ledger-generation-report.schema.json"));
        foreach (var path in Directory.EnumerateFiles(valid, "*.json").Order(StringComparer.Ordinal))
        {
            var bytes = File.ReadAllBytes(path);
            var first = TaskContractLedgerGenerator.Generate(bytes, Path.GetRelativePath(root, path), "synthetic-trace");
            var second = TaskContractLedgerGenerator.Generate(bytes, Path.GetRelativePath(root, path), "synthetic-trace");
            if (!first.IsValid) { failures.Add($"{Path.GetFileName(path)}: generation failed"); continue; }
            var ledgerBytes = first.LedgerBytes!;
            var reportBytes = first.ReportBytes!;
            if (!ledgerBytes.AsSpan().SequenceEqual(second.LedgerBytes) ||
                !reportBytes.AsSpan().SequenceEqual(second.ReportBytes))
                failures.Add($"{Path.GetFileName(path)}: regeneration was not byte-identical");
            if (!SchemaConformance.Validate(reportBytes, reportSchema, out var schemaError))
                failures.Add($"{Path.GetFileName(path)}: report schema rejected output ({schemaError})");
            var phaseA = ObligationLedgerValidator.ParseAndValidatePhaseA(ledgerBytes);
            if (!phaseA.IsValid) failures.Add($"{Path.GetFileName(path)}: generated ledger failed Phase A");
            var actualDigest = Convert.ToHexStringLower(SHA256.HashData(ledgerBytes));
            if (first.Report!.LedgerDigest != actualDigest) failures.Add($"{Path.GetFileName(path)}: ledger digest mismatch");
            if ((first.Ledger!.Signals ?? []).Count != 0) failures.Add($"{Path.GetFileName(path)}: generated signals were not empty");
            if ((first.Ledger.Obligations ?? []).Any(o => o.Description!.Contains("SYNTHETIC_EXCLUDED", StringComparison.Ordinal)))
                failures.Add($"{Path.GetFileName(path)}: excluded field became an obligation");
            if (Path.GetFileName(path) == "deliverables-only.json")
            {
                AssertGolden(ledgerBytes, Path.Combine(fixtureRoot, "golden", "deliverables-only.ledger.json"), "ledger", failures);
                AssertGolden(reportBytes, Path.Combine(fixtureRoot, "golden", "deliverables-only.report.json"), "report", failures);
            }
            if (Path.GetFileName(path) == "phase5-gate-bindings.json")
            {
                if (first.ManifestBytes is null) failures.Add("phase5 bindings: manifest was not derived");
                else
                {
                    AssertGolden(first.ManifestBytes, Path.Combine(fixtureRoot, "golden", "phase5-correlation-manifest.json"), "manifest", failures);
                    if (!first.ManifestBytes.AsSpan().SequenceEqual(second.ManifestBytes)) failures.Add("phase5 bindings: manifest regeneration was not byte-identical");
                    AssertManifestStructure(first.ManifestBytes, failures);
                    if (first.Manifest!.GateBindings.Count != 2 || first.Manifest.GateBindings.Any(binding => !first.Ledger.Obligations!.Any(obligation => obligation.Id == binding.ObligationId)))
                        failures.Add("phase5 bindings: gate binding did not reference a generated obligation");
                }
            }
        }

        var expectedPointers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["malformed.PT601.json"] = "",
            ["missing-id.PT602.json"] = "/id",
            ["no-candidates.PT603.json"] = "",
            ["non-string-entry.PT604.json"] = "/deliverables/1",
            ["empty-entry.PT604.json"] = "/required_contract_decisions/0",
            ["duplicate-source-field.PT606.json"] = "/deliverables/0"
        };
        foreach (var path in Directory.EnumerateFiles(invalid, "*.json").Order(StringComparer.Ordinal))
        {
            var expected = Path.GetFileNameWithoutExtension(path).Split('.').Last();
            var result = TaskContractLedgerGenerator.Generate(File.ReadAllBytes(path), path, "synthetic-trace");
            if (result.IsValid || result.Diagnostics.Count != 1 || result.Diagnostics[0].Code != expected)
                failures.Add($"{Path.GetFileName(path)}: expected only diagnostic {expected}");
            else if (result.Diagnostics[0].Pointer != expectedPointers[Path.GetFileName(path)])
                failures.Add($"{Path.GetFileName(path)}: diagnostic pointer mismatch");
        }

        AssertDiagnostic(TaskContractLedgerGenerator.Generate(Encoding.UTF8.GetBytes("{}"), "x", "synthetic-trace"), "PT602", "/id", failures);
        AssertDiagnostic(TaskContractLedgerGenerator.Generate(Encoding.UTF8.GetBytes("{}"), "x", " "), "PT605", "", failures);
        AssertDiagnostic(TaskContractLedgerGenerator.Generate(new byte[TraceValidator.MaximumInputSizeBytes + 1], "x", "synthetic-trace"), "PT600", "", failures);
        AssertBindingFailures(failures);
        AssertConditionalSchema(reportSchema, failures);
    }

    private static void AssertManifestStructure(byte[] manifestBytes, List<string> failures)
    {
        try
        {
            using var document = JsonDocument.Parse(manifestBytes);
            var root = document.RootElement;
            var rootFields = new HashSet<string>(["schemaVersion", "taskContractId", "traceId", "gateBindings"], StringComparer.Ordinal);
            if (root.ValueKind != JsonValueKind.Object ||
                root.EnumerateObject().Any(property => !rootFields.Remove(property.Name)) || rootFields.Count != 0 ||
                root.GetProperty("schemaVersion").ValueKind != JsonValueKind.String || root.GetProperty("schemaVersion").GetString() != "pt-shadow-correlation-1.0" ||
                !IsNonEmptyString(root.GetProperty("taskContractId")) || !IsNonEmptyString(root.GetProperty("traceId")) ||
                root.GetProperty("gateBindings").ValueKind != JsonValueKind.Array || root.GetProperty("gateBindings").GetArrayLength() != 2)
            {
                failures.Add("phase5 bindings: manifest root structure was invalid");
                return;
            }

            var gateKeys = new HashSet<string>(StringComparer.Ordinal);
            var obligationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in root.GetProperty("gateBindings").EnumerateArray())
            {
                var bindingFields = new HashSet<string>(["gateKey", "obligationId", "passPredicate", "match"], StringComparer.Ordinal);
                if (binding.ValueKind != JsonValueKind.Object ||
                    binding.EnumerateObject().Any(property => !bindingFields.Remove(property.Name)) || bindingFields.Count != 0 ||
                    !IsNonEmptyString(binding.GetProperty("gateKey")) || !gateKeys.Add(binding.GetProperty("gateKey").GetString()!) ||
                    !IsNonEmptyString(binding.GetProperty("obligationId")) || !obligationIds.Add(binding.GetProperty("obligationId").GetString()!) ||
                    binding.GetProperty("passPredicate").ValueKind != JsonValueKind.String || binding.GetProperty("passPredicate").GetString() != "exit-code-zero")
                {
                    failures.Add("phase5 bindings: manifest gate binding structure was invalid");
                    return;
                }

                var match = binding.GetProperty("match");
                var matchFields = new HashSet<string>(["type", "value"], StringComparer.Ordinal);
                if (match.ValueKind != JsonValueKind.Object ||
                    match.EnumerateObject().Any(property => !matchFields.Remove(property.Name)) || matchFields.Count != 0 ||
                    match.GetProperty("type").ValueKind != JsonValueKind.String || match.GetProperty("type").GetString() != "envelope-field" ||
                    !IsNonEmptyString(match.GetProperty("value")))
                {
                    failures.Add("phase5 bindings: manifest match structure was invalid");
                    return;
                }
            }
        }
        catch (JsonException)
        {
            failures.Add("phase5 bindings: manifest was not valid JSON");
        }
    }

    private static bool IsNonEmptyString(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(value.GetString());

    private static void AssertBindingFailures(List<string> failures)
    {
        const string prefix = "{\"id\":\"x\",\"deliverables\":[\"one\"],\"acceptance_commands\":";
        var malformed = new[]
        {
            "{}", "[{\"command\":[\"ok\"],\"gateKey\":\"\",\"obligationRef\":{\"sourceField\":\"deliverables\",\"index\":0}}]",
            "[{\"command\":[\"ok\"],\"gateKey\":\"g\",\"obligationRef\":{\"sourceField\":\"other\",\"index\":0}}]",
            "[{\"command\":[\"ok\"],\"gateKey\":\"g\",\"obligationRef\":{\"sourceField\":\"deliverables\",\"index\":1}}]",
            "[{\"command\":[\"ok\"],\"gateKey\":\"g\",\"obligationRef\":{\"sourceField\":\"deliverables\",\"index\":0}},{\"command\":[\"ok\"],\"gateKey\":\"g\",\"obligationRef\":{\"sourceField\":\"deliverables\",\"index\":0}}]"
        };
        foreach (var commands in malformed)
        {
            var result = TaskContractLedgerGenerator.Generate(Encoding.UTF8.GetBytes(prefix + commands + "}"), "x", "trace");
            if (result.IsValid || result.Diagnostics.Count != 1 || result.Diagnostics[0].Code != "PT607" || result.LedgerBytes is not null || result.ReportBytes is not null || result.ManifestBytes is not null)
                failures.Add("phase5 bindings: malformed binding did not fail closed");
        }
        var legacy = TaskContractLedgerGenerator.Generate(Encoding.UTF8.GetBytes(prefix + "[[\"git\",\"diff\"]]}"), "x", "trace");
        if (!legacy.IsValid || legacy.ManifestBytes is not null) failures.Add("phase5 bindings: legacy argv was not valid and unbound");
    }

    private static void AssertDiagnostic(LedgerGenerationResult result, string code, string pointer, List<string> failures)
    {
        if (result.Diagnostics.Count != 1 || result.Diagnostics[0].Code != code || result.Diagnostics[0].Pointer != pointer)
            failures.Add($"generation diagnostic {code}: code or pointer mismatch");
    }

    private static void AssertGolden(byte[] actual, string path, string document, List<string> failures)
    {
        var expected = File.ReadAllBytes(path);
        if (expected.Length == 0 || expected[^1] != (byte)'\n') expected = [.. expected, (byte)'\n'];
        if (!actual.AsSpan().SequenceEqual(expected)) failures.Add($"generation {document} differed from golden");
    }

    private static void AssertConditionalSchema(byte[] schema, List<string> failures)
    {
        var invalidDerived = Encoding.UTF8.GetBytes("""{"schemaVersion":"1.0","reportType":"LedgerGenerationReport","generator":{"name":"progresstrace-ledger-generator","version":"1"},"generatedAt":"1970-01-01T00:00:00.0000000Z","taskContractId":"x","taskContractPath":"x","taskContractDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","traceId":"t","ledgerDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","obligations":[{"obligationId":"o","coverageStatus":"derived-automatic"}],"unsupportedSourceFields":[]}""");
        var invalidManual = Encoding.UTF8.GetBytes("""{"schemaVersion":"1.0","reportType":"LedgerGenerationReport","generator":{"name":"progresstrace-ledger-generator","version":"1"},"generatedAt":"1970-01-01T00:00:00.0000000Z","taskContractId":"x","taskContractPath":"x","taskContractDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","traceId":"t","ledgerDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","obligations":[{"obligationId":"o","coverageStatus":"manual","sourceField":"deliverables"}],"unsupportedSourceFields":[]}""");
        if (SchemaConformance.Validate(invalidDerived, schema, out _)) failures.Add("report schema accepted derived coverage without provenance");
        if (SchemaConformance.Validate(invalidManual, schema, out _)) failures.Add("report schema accepted manual coverage with provenance");
    }
}
