using ProgressTrace.Core.Adapters;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Models;
using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Sessions;
using ProgressTrace.Core.Validation;

static class SessionConformance
{
    public static void Assert(string root, List<string> failures)
    {
        var sessionSchema = File.ReadAllBytes(Path.Combine(root, "contracts", "agent-session.schema.json"));
        var gateSchema = File.ReadAllBytes(Path.Combine(root, "contracts", "gate-outcome.schema.json"));
        var sessionValid = Path.Combine(root, "fixtures", "session", "valid");
        var sessionInvalid = Path.Combine(root, "fixtures", "session", "invalid");
        var gateValid = Path.Combine(root, "fixtures", "gate-outcome", "valid");
        var gateInvalid = Path.Combine(root, "fixtures", "gate-outcome", "invalid");

        foreach (var path in Directory.EnumerateFiles(sessionValid, "*.json").Order(StringComparer.Ordinal))
        {
            var bytes = File.ReadAllBytes(path);
            if (!SchemaConformance.Validate(bytes, sessionSchema, out var schemaError)) failures.Add($"{Path.GetFileName(path)}: session schema rejected valid fixture ({schemaError})");
            var result = AgentSessionValidator.ParseAndValidate(bytes);
            if (!result.IsValid) { failures.Add($"{Path.GetFileName(path)}: expected valid session"); continue; }
            var first = AgentSessionNormalizer.Normalize(result.Session!);
            var reparsed = AgentSessionValidator.ParseAndValidate(first);
            if (!reparsed.IsValid || !first.AsSpan().SequenceEqual(AgentSessionNormalizer.Normalize(reparsed.Session!))) failures.Add($"{Path.GetFileName(path)}: session normalization was not byte-idempotent");
        }
        foreach (var path in Directory.EnumerateFiles(sessionInvalid, "*.json").Order(StringComparer.Ordinal))
        {
            var expected = Path.GetFileNameWithoutExtension(path).Split('.').Last();
            var result = AgentSessionValidator.ParseAndValidate(File.ReadAllBytes(path));
            if (result.IsValid || result.Diagnostics.All(item => item.Code != expected)) failures.Add($"{Path.GetFileName(path)}: expected diagnostic {expected}");
        }

        foreach (var path in Directory.EnumerateFiles(gateValid, "*.json").Order(StringComparer.Ordinal))
        {
            var bytes = File.ReadAllBytes(path);
            if (!SchemaConformance.Validate(bytes, gateSchema, out var schemaError)) failures.Add($"{Path.GetFileName(path)}: gate schema rejected valid fixture ({schemaError})");
            var result = GateOutcomeValidator.ParseAndValidate(bytes);
            if (!result.IsValid) { failures.Add($"{Path.GetFileName(path)}: expected valid gate outcome"); continue; }
            var first = GateOutcomeNormalizer.Normalize(result.Outcome!);
            var reparsed = GateOutcomeValidator.ParseAndValidate(first);
            if (!reparsed.IsValid || !first.AsSpan().SequenceEqual(GateOutcomeNormalizer.Normalize(reparsed.Outcome!))) failures.Add($"{Path.GetFileName(path)}: gate normalization was not byte-idempotent");
            var projection = GateOutcomeProjector.Project(result.Outcome!);
            if (projection.Event.Type != "gate-outcome" || projection.Event.Actor != "tool") failures.Add($"{Path.GetFileName(path)}: projection did not produce a tool gate event");
            if (result.Outcome!.ObligationId is null && projection.Signal is not null) failures.Add($"{Path.GetFileName(path)}: projection invented an obligation signal");
        }
        foreach (var path in Directory.EnumerateFiles(gateInvalid, "*.json").Order(StringComparer.Ordinal))
        {
            var expected = Path.GetFileNameWithoutExtension(path).Split('.').Last();
            var result = GateOutcomeValidator.ParseAndValidate(File.ReadAllBytes(path));
            if (result.IsValid || result.Diagnostics.All(item => item.Code != expected)) failures.Add($"{Path.GetFileName(path)}: expected diagnostic {expected}");
        }
        AssertSessionBoundary(root, failures);
    }

    private static void AssertSessionBoundary(string root, List<string> failures)
    {
        var session = AgentSessionValidator.ParseAndValidate(File.ReadAllBytes(Path.Combine(root, "fixtures", "session", "valid", "retry-session.json"))).Session!;
        var repeated = GateOutcomeValidator.ParseAndValidate(File.ReadAllBytes(Path.Combine(root, "fixtures", "gate-outcome", "valid", "repeated-attempt-fail.json"))).Outcome!;
        var evaluation = SessionEvaluator.Evaluate(session, [repeated]);
        if (evaluation.Classification != SessionEvaluator.RepeatedAttemptWithoutObligationAdvancement || evaluation.StopRequested)
            failures.Add("session boundary did not classify a retry without advancement fail-closed and without stop");

        var pass = GateOutcomeValidator.ParseAndValidate(File.ReadAllBytes(Path.Combine(root, "fixtures", "gate-outcome", "valid", "pass.json"))).Outcome!;
        var single = new AgentSession("1.0", "session-001", "phase-4-1-agent-session", [session.Invocations![0]]);
        var integration = SessionTraceBuilder.Build(single, [pass]);
        if (integration.Trace.Events!.Count != 1 || integration.Ledger.Signals!.Count != 1 || integration.Evaluation.TraceClassification != "progress")
            failures.Add("gate outcome integration did not reach trace, ledger, and evaluator");

        var duplicate = System.Text.Json.JsonSerializer.Deserialize<GateOutcome[]>(File.ReadAllBytes(Path.Combine(root, "fixtures", "gate-outcome", "integration", "duplicate-outcomes.json")), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var duplicateResult = SessionContractValidator.Validate(single with { Invocations = [session.Invocations![0], session.Invocations![1]] }, duplicate);
        if (duplicateResult.IsValid || duplicateResult.Diagnostics.All(d => d.Code != DiagnosticCodes.DuplicateOutcomeId))
            failures.Add("duplicate outcome fixture did not fail closed");
        var mismatch = GateOutcomeValidator.ParseAndValidate(File.ReadAllBytes(Path.Combine(root, "fixtures", "gate-outcome", "integration", "session-reference-mismatch.PT508.json"))).Outcome!;
        if (SessionContractValidator.Validate(single, [mismatch]).Diagnostics.All(d => d.Code != DiagnosticCodes.SessionReferenceMismatch))
            failures.Add("cross-contract session mismatch did not fail closed");
    }
}
