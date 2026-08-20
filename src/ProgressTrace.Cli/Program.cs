using System.Text.Json;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Assessment;
using ProgressTrace.Core.Comparison;
using ProgressTrace.Core.Evaluation;
using ProgressTrace.Core.Generation;
using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Validation;
using ProgressTrace.Core.Advisory;
using ProgressTrace.Core.Models;
using ProgressTrace.Core.Budget;
using ProgressTrace.Core.Shadow;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length > 0 && args[0] == "shadow-summarize") return await ShadowSummarizeAsync(args);
    if (args.Length > 0 && args[0] == "budget") return await BudgetAsync(args);
    if (args.Length > 0 && args[0] == "advise") return await AdviseAsync(args);
    if (args.Length > 0 && args[0] == "generate-ledger")
    {
        if (args.Length < 3)
        {
            WriteGenerationFailure([new(DiagnosticCodes.MissingTraceId, "", "traceId argument is missing or empty.")]);
            return 2;
        }
        if (args.Length != 5)
        {
            WriteUsage();
            return 2;
        }
        return await GenerateLedgerAsync(args[1], args[2], args[3], args[4]);
    }
    if (args.Length == 3 && (args[0] is "validate" or "normalize") && (args[1] is "session" or "gate-outcome"))
    {
        return await RunContractAsync(args[0], args[1], args[2]);
    }
    if (args.Length == 3 && args[0] == "evaluate")
    {
        return await EvaluateAsync(args[1], args[2]);
    }
    if (args.Length == 4 && args[0] == "assess")
    {
        return await AssessAsync(args[1], args[2], args[3]);
    }
    if (args.Length == 5 && args[0] == "compare")
    {
        return await CompareAsync(args[1], args[2], args[3], args[4]);
    }
    if (args.Length != 2 || args[0] is not ("validate" or "normalize"))
    {
        WriteUsage();
        return 2;
    }

    var inputResult = await ReadAsync(args[1], null);
    if (inputResult.ExitCode is { } inputExit) return inputExit;
    var input = inputResult.Bytes!;

    var result = TraceValidator.ParseAndValidate(input);
    if (!result.IsValid)
    {
        WriteJson(Console.Out, new { valid = false, diagnostics = result.Diagnostics });
        return result.Diagnostics.Any(static item =>
            item.Code is DiagnosticCodes.InvalidJson or DiagnosticCodes.InputTooLarge) ? 2 : 1;
    }

    if (args[0] == "validate")
    {
        WriteJson(Console.Out, new { valid = true, diagnostics = Array.Empty<Diagnostic>() });
    }
    else
    {
        await Console.OpenStandardOutput().WriteAsync(TraceNormalizer.Normalize(result.Envelope!));
    }
    return 0;
}

static void WriteShadowUsage() => WriteJson(Console.Error, new { error = "usage", message = "Usage: progresstrace shadow-summarize --snapshot-manifest-path <path> [--real-decision-path <path>] [--out <path>]" });

static async Task<int> ShadowSummarizeAsync(string[] args)
{
    var allowed = new HashSet<string>(["--snapshot-manifest-path", "--real-decision-path", "--out"], StringComparer.Ordinal); var values = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 1; i < args.Length; i++) if (!allowed.Contains(args[i]) || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal) || !values.TryAdd(args[i], args[++i])) { WriteShadowUsage(); return 2; }
    if (!values.ContainsKey("--snapshot-manifest-path")) { WriteShadowUsage(); return 2; }
    var manifestPath = values["--snapshot-manifest-path"]; var manifestInput = await ReadAsync(manifestPath, "snapshot-manifest"); if (manifestInput.ExitCode is { } me) return me;
    if (!TryParseShadowManifest(manifestInput.Bytes!, out var entries)) { WriteJson(Console.Error, new { error = "input", document = "snapshot-manifest", message = "Snapshot manifest is malformed." }); return 2; }
    if (entries!.Count == 0) { WriteShadowDiagnostics([new(DiagnosticCodes.ShadowAssemblyInvariant, "", "Shadow summary cannot be assembled deterministically.")]); return 1; }
    for (var i = 1; i < entries.Count; i++) if (entries[i].Sequence <= entries[i - 1].Sequence) { WriteShadowDiagnostics([new(DiagnosticCodes.ShadowSequenceInvalid, "/" + i + "/shadowSequence", "Shadow sequence must be strictly increasing.")]); return 1; }
    var inputs = new List<ShadowSnapshotInput>(); var manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
    foreach (var entry in entries)
    {
        var advisoryPath = Path.IsPathRooted(entry.Path) ? entry.Path : Path.Combine(manifestDirectory, entry.Path); var advisoryInput = await ReadAsync(advisoryPath, "advisory-result"); if (advisoryInput.ExitCode is not null) return 2;
        var advisory = AdvisoryResultValidator.ParseAndValidate(advisoryInput.Bytes!); if (!advisory.IsValid) { WriteShadowDiagnostics([new(DiagnosticCodes.ShadowAdvisoryInvalid, "", "Referenced AdvisoryResult is invalid.")]); return 1; }
        inputs.Add(new(entry.Sequence, entry.GateOutcomeId, advisory.AdvisoryResult!));
    }
    RealDecisionRecord? decision = null;
    if (values.TryGetValue("--real-decision-path", out var decisionPath))
    {
        var decisionInput = await ReadAsync(decisionPath, "real-decision"); if (decisionInput.ExitCode is not null) return 2;
        var validation = RealDecisionRecordValidator.ParseAndValidate(decisionInput.Bytes!); if (!validation.IsValid) { WriteShadowDiagnostics([new(DiagnosticCodes.ShadowRealDecisionInvalid, "", "RealDecisionRecord is invalid.")]); return 1; }
        decision = validation.RealDecisionRecord;
    }
    var assembled = ShadowAssembler.Assemble(inputs, decision); if (!assembled.IsValid) { WriteShadowDiagnostics(assembled.Diagnostics); return 1; }
    if (!await WriteAdvisoryOutput(values.GetValueOrDefault("--out"), ShadowSessionSummaryNormalizer.Normalize(assembled.Summary!))) return 2;
    if (assembled.Diagnostics.Count != 0) WriteShadowDiagnostics(assembled.Diagnostics); return 0;
}

static bool TryParseShadowManifest(byte[] bytes, out IReadOnlyList<(long Sequence, string GateOutcomeId, string Path)>? entries)
{
    entries = null;
    try
    {
        using var document = JsonDocument.Parse(bytes); if (document.RootElement.ValueKind != JsonValueKind.Array) return false; var result = new List<(long, string, string)>();
        foreach (var item in document.RootElement.EnumerateArray()) { if (item.ValueKind != JsonValueKind.Object || item.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != 3 || item.GetPropertyCount() != 3 || !item.TryGetProperty("shadowSequence", out var sequence) || !sequence.TryGetInt64(out var number) || number < 0 || !item.TryGetProperty("triggeringGateOutcomeId", out var gate) || gate.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(gate.GetString()) || !item.TryGetProperty("advisoryResultPath", out var path) || path.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(path.GetString())) return false; result.Add((number, gate.GetString()!, path.GetString()!)); }
        entries = result; return true;
    }
    catch (JsonException) { return false; }
}

static void WriteShadowDiagnostics(IReadOnlyList<Diagnostic> diagnostics) => WriteJson(Console.Error, new { diagnostics });

static async Task<int> GenerateLedgerAsync(string taskContractPath, string traceId, string ledgerOutputPath, string reportOutputPath)
{
    byte[] bytes;
    try
    {
        bytes = await File.ReadAllBytesAsync(taskContractPath);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        WriteGenerationFailure([new(DiagnosticCodes.TaskContractUnreadable, "", "Task contract file could not be read.")]);
        return 2;
    }

    var result = TaskContractLedgerGenerator.Generate(bytes, taskContractPath, traceId);
    if (!result.IsValid)
    {
        WriteGenerationFailure(result.Diagnostics);
        return result.Diagnostics.Any(static diagnostic => diagnostic.Code is
            DiagnosticCodes.NoObligationCandidates or
            DiagnosticCodes.InvalidSourceEntry or
            DiagnosticCodes.DuplicateGeneratedObligationId ||
            diagnostic.Code.StartsWith("PT1", StringComparison.Ordinal) ||
            diagnostic.Code.StartsWith("PT2", StringComparison.Ordinal)) ? 1 : 2;
    }

    try
    {
        WritePairAtomically(ledgerOutputPath, result.LedgerBytes!, reportOutputPath, result.ReportBytes!);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
    {
        WriteJson(Console.Error, new { error = "output", message = "Output files could not be written." });
        return 2;
    }

    WriteJson(Console.Out, new { valid = true, diagnostics = Array.Empty<Diagnostic>() });
    return 0;
}

static void WritePairAtomically(string firstPath, byte[] firstBytes, string secondPath, byte[] secondBytes)
{
    var firstFullPath = Path.GetFullPath(firstPath);
    var secondFullPath = Path.GetFullPath(secondPath);
    if (string.Equals(firstFullPath, secondFullPath, StringComparison.Ordinal))
        throw new ArgumentException("Output paths must differ.");

    var firstDirectory = Path.GetDirectoryName(firstFullPath)!;
    var secondDirectory = Path.GetDirectoryName(secondFullPath)!;
    Directory.CreateDirectory(firstDirectory);
    Directory.CreateDirectory(secondDirectory);
    var token = Guid.NewGuid().ToString("N");
    var firstTemp = Path.Combine(firstDirectory, $".{Path.GetFileName(firstFullPath)}.{token}.tmp");
    var secondTemp = Path.Combine(secondDirectory, $".{Path.GetFileName(secondFullPath)}.{token}.tmp");
    var firstBackup = firstTemp + ".backup";
    var secondBackup = secondTemp + ".backup";
    var firstExisted = File.Exists(firstFullPath);
    var secondExisted = File.Exists(secondFullPath);
    try
    {
        File.WriteAllBytes(firstTemp, firstBytes);
        File.WriteAllBytes(secondTemp, secondBytes);
        if (firstExisted) File.Move(firstFullPath, firstBackup);
        if (secondExisted) File.Move(secondFullPath, secondBackup);
        File.Move(firstTemp, firstFullPath);
        try
        {
            File.Move(secondTemp, secondFullPath);
        }
        catch
        {
            File.Delete(firstFullPath);
            throw;
        }
        if (firstExisted) File.Delete(firstBackup);
        if (secondExisted) File.Delete(secondBackup);
    }
    catch
    {
        if (File.Exists(firstFullPath) && !firstExisted) File.Delete(firstFullPath);
        if (File.Exists(secondFullPath) && !secondExisted) File.Delete(secondFullPath);
        if (File.Exists(firstBackup)) File.Move(firstBackup, firstFullPath, true);
        if (File.Exists(secondBackup)) File.Move(secondBackup, secondFullPath, true);
        throw;
    }
    finally
    {
        File.Delete(firstTemp);
        File.Delete(secondTemp);
        File.Delete(firstBackup);
        File.Delete(secondBackup);
    }
}

static void WriteGenerationFailure(IReadOnlyList<Diagnostic> diagnostics) =>
    WriteJson(Console.Out, new { valid = false, diagnostics });

static void WriteUsage() => WriteJson(Console.Error, new
{
    error = "usage",
    message = "Usage: progresstrace <validate|normalize> <path> | progresstrace evaluate <trace-path> <ledger-path> | progresstrace assess <trace-path> <ledger-path> <termination-declaration-path> | progresstrace compare <trace-path> <ledger-path> <termination-declaration-path> <baseline-definition-path> | progresstrace generate-ledger <task-contract-path> <trace-id> <ledger-output-path> <report-output-path>"
});

static void WriteBudgetUsage() => WriteJson(Console.Error, new { error = "usage", message = "Usage: progresstrace budget --session-path <path> --token-usage-path <path> --ledger-path <path> [--out <path>]" });

static async Task<int> BudgetAsync(string[] args)
{
    var allowed = new HashSet<string>(["--session-path", "--token-usage-path", "--ledger-path", "--out"], StringComparer.Ordinal); var values = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 1; i < args.Length; i++)
    {
        if (!allowed.Contains(args[i]) || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal) || !values.TryAdd(args[i], args[++i])) { WriteBudgetUsage(); return 2; }
    }
    if (new[] { "--session-path", "--token-usage-path", "--ledger-path" }.Any(key => !values.ContainsKey(key))) { WriteBudgetUsage(); return 2; }
    var sessionInput = await ReadAsync(values["--session-path"], "session"); if (sessionInput.ExitCode is { } se) return se;
    var session = AgentSessionValidator.ParseAndValidate(sessionInput.Bytes!);
    if (!session.IsValid)
    {
        var window = session.Diagnostics.Any(d => d.Code == DiagnosticCodes.InvalidInvocationWindow || d.Pointer.EndsWith("/startedAt", StringComparison.Ordinal) || d.Pointer.EndsWith("/endedAt", StringComparison.Ordinal));
        var diagnostics = window ? new[] { new Diagnostic(DiagnosticCodes.BudgetInvocationWindow, "", "Invocation window is invalid.") } : session.Diagnostics; WriteBudgetDiagnostics(diagnostics); return window ? 1 : FailureExit(diagnostics);
    }
    var usageInput = await ReadAsync(values["--token-usage-path"], "token-usage"); if (usageInput.ExitCode is { } ue) return ue;
    var usage = TokenUsageValidator.ParseAndValidate(usageInput.Bytes!); if (!usage.IsValid) { WriteBudgetDiagnostics(usage.Diagnostics); return FailureExit(usage.Diagnostics); }
    var ledgerInput = await ReadAsync(values["--ledger-path"], "ledger"); if (ledgerInput.ExitCode is { } le) return le;
    var ledger = ObligationLedgerValidator.ParseAndValidatePhaseA(ledgerInput.Bytes!); if (!ledger.IsValid) { WriteBudgetDiagnostics(ledger.Diagnostics); return FailureExit(ledger.Diagnostics); }
    var assembled = BudgetAssembler.Assemble(session.Session!, usage.TokenUsage!, ledger.Ledger!); if (!assembled.IsValid) { WriteBudgetDiagnostics(assembled.Diagnostics); return 1; }
    var bytes = ObservedBudgetNormalizer.Normalize(assembled.Budget!); if (!await WriteAdvisoryOutput(values.GetValueOrDefault("--out"), bytes)) return 2;
    if (assembled.Diagnostics.Count != 0) WriteBudgetDiagnostics(assembled.Diagnostics); return 0;
}

static void WriteBudgetDiagnostics(IReadOnlyList<Diagnostic> diagnostics) => WriteJson(Console.Error, new { diagnostics });

static async Task<int> AdviseAsync(string[] args)
{
    var divergence = args.Skip(1).Contains("--divergence-report", StringComparer.Ordinal);
    var allowed = divergence ? new HashSet<string>(["--advisory-result-path", "--ledger-path", "--out"], StringComparer.Ordinal) : new HashSet<string>(["--session-path", "--ledger-path", "--gate-outcomes-array-path", "--out"], StringComparer.Ordinal);
    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 1; i < args.Length; i++)
    {
        if (args[i] == "--divergence-report") { if (!divergence) return 2; continue; }
        if (!allowed.Contains(args[i]) || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal) || !values.TryAdd(args[i], args[++i])) { WriteUsage(); return 2; }
    }
    var required = divergence ? new[] { "--advisory-result-path", "--ledger-path" } : ["--session-path", "--ledger-path", "--gate-outcomes-array-path"];
    if (required.Any(key => !values.ContainsKey(key)) || (!divergence && args.Contains("--divergence-report", StringComparer.Ordinal))) { WriteUsage(); return 2; }
    if (divergence) return await DivergenceAsync(values);
    var sessionInput = await ReadAsync(values["--session-path"], "session"); if (sessionInput.ExitCode is { } se) return se;
    var session = AgentSessionValidator.ParseAndValidate(sessionInput.Bytes!); if (!session.IsValid) { WriteValidationFailure("session", session.Diagnostics); return FailureExit(session.Diagnostics); }
    var ledgerInput = await ReadAsync(values["--ledger-path"], "ledger"); if (ledgerInput.ExitCode is { } le) return le;
    var ledger = ObligationLedgerValidator.ParseAndValidatePhaseA(ledgerInput.Bytes!); if (!ledger.IsValid) return ledger.Diagnostics.Any(d => d.Code is DiagnosticCodes.InvalidJson or DiagnosticCodes.InputTooLarge) ? AdvisoryRawFailure(ledger.Diagnostics[0].Code, ledger.Diagnostics[0].Message, 2) : AdvisoryFailure(DiagnosticCodes.AdvisoryLedgerInvalid, "Ledger input failed structural validation.");
    var outcomesInput = await ReadAsync(values["--gate-outcomes-array-path"], "gate-outcomes"); if (outcomesInput.ExitCode is { } oe) return oe;
    if (!TryParseOutcomes(outcomesInput.Bytes!, out var outcomes, out var malformed)) return malformed ? AdvisoryRawFailure(DiagnosticCodes.InvalidJson, "Input is not valid JSON.", 2) : AdvisoryFailure(DiagnosticCodes.AdvisoryOutcomeInvalid, "Gate outcome input failed structural validation.");
    var assembled = AdvisoryAssembler.Assemble(session.Session!, ledger.Ledger!, outcomes!);
    if (!assembled.IsValid) { WriteAdvisoryDiagnostics(assembled.Diagnostics); return 1; }
    var bytes = AdvisoryResultNormalizer.Normalize(assembled.Result!);
    if (!await WriteAdvisoryOutput(values.GetValueOrDefault("--out"), bytes)) return 2;
    if (assembled.Diagnostics.Count != 0) WriteAdvisoryDiagnostics(assembled.Diagnostics);
    return 0;
}

static async Task<int> DivergenceAsync(Dictionary<string, string> values)
{
    var advisoryInput = await ReadAsync(values["--advisory-result-path"], "advisory-result"); if (advisoryInput.ExitCode is { } ae) return ae;
    var advisory = AdvisoryResultValidator.ParseAndValidate(advisoryInput.Bytes!); if (!advisory.IsValid) return advisory.Diagnostics.Any(d => d.Code is DiagnosticCodes.InvalidJson or DiagnosticCodes.InputTooLarge) ? AdvisoryRawFailure(advisory.Diagnostics[0].Code, advisory.Diagnostics[0].Message, 2) : AdvisoryFailure(DiagnosticCodes.AdvisoryResultInvalid, "Advisory result input failed structural validation.");
    var ledgerInput = await ReadAsync(values["--ledger-path"], "ledger"); if (ledgerInput.ExitCode is { } le) return le;
    var ledger = ObligationLedgerValidator.ParseAndValidatePhaseA(ledgerInput.Bytes!); if (!ledger.IsValid) return ledger.Diagnostics.Any(d => d.Code is DiagnosticCodes.InvalidJson or DiagnosticCodes.InputTooLarge) ? AdvisoryRawFailure(ledger.Diagnostics[0].Code, ledger.Diagnostics[0].Message, 2) : AdvisoryFailure(DiagnosticCodes.AdvisoryLedgerInvalid, "Ledger input failed structural validation.");
    var assembled = AdvisoryAssembler.AssembleDivergence(advisory.AdvisoryResult!, ledger.Ledger!); if (!assembled.IsValid) { WriteAdvisoryDiagnostics(assembled.Diagnostics); return 1; }
    return await WriteAdvisoryOutput(values.GetValueOrDefault("--out"), AdvisoryDivergenceReportNormalizer.Normalize(assembled.Report!)) ? 0 : 2;
}

static bool TryParseOutcomes(byte[] bytes, out IReadOnlyList<GateOutcome>? outcomes, out bool malformed)
{
    outcomes = null; malformed = false;
    try
    {
        using var document = JsonDocument.Parse(bytes); if (document.RootElement.ValueKind != JsonValueKind.Array) return false;
        var result = new List<GateOutcome>(); foreach (var item in document.RootElement.EnumerateArray()) { var validation = GateOutcomeValidator.ParseAndValidate(System.Text.Encoding.UTF8.GetBytes(item.GetRawText())); if (!validation.IsValid) { malformed = validation.Diagnostics.Any(d => d.Code == DiagnosticCodes.InvalidJson); return false; } result.Add(validation.Outcome!); }
        outcomes = result; return true;
    }
    catch (JsonException) { malformed = true; return false; }
}

static int AdvisoryFailure(string code, string message) => AdvisoryRawFailure(code, message, 1);
static int AdvisoryRawFailure(string code, string message, int exit) { WriteAdvisoryDiagnostics([new(code, "", message)]); return exit; }
static void WriteAdvisoryDiagnostics(IReadOnlyList<Diagnostic> diagnostics) => WriteJson(Console.Error, new { diagnostics });
static async Task<bool> WriteAdvisoryOutput(string? path, byte[] bytes)
{
    if (path is null) { await Console.OpenStandardOutput().WriteAsync(bytes); return true; }
    try { WriteSingleAtomically(path, bytes); return true; } catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { WriteJson(Console.Error, new { error = "output", message = "Output file could not be written." }); return false; }
}

static void WriteSingleAtomically(string path, byte[] bytes)
{
    var full = Path.GetFullPath(path); var directory = Path.GetDirectoryName(full)!; Directory.CreateDirectory(directory); var temp = Path.Combine(directory, $".{Path.GetFileName(full)}.{Guid.NewGuid():N}.tmp");
    try { File.WriteAllBytes(temp, bytes); File.Move(temp, full, true); } finally { File.Delete(temp); }
}

static async Task<int> RunContractAsync(string operation, string kind, string path)
{
    var input = await ReadAsync(path, kind);
    if (input.ExitCode is { } exit) return exit;
    if (kind == "session")
    {
        var result = AgentSessionValidator.ParseAndValidate(input.Bytes!);
        if (!result.IsValid) { WriteJson(Console.Out, new { valid = false, diagnostics = result.Diagnostics }); return FailureExit(result.Diagnostics); }
        if (operation == "validate") WriteJson(Console.Out, new { valid = true, diagnostics = Array.Empty<Diagnostic>() });
        else await Console.OpenStandardOutput().WriteAsync(AgentSessionNormalizer.Normalize(result.Session!));
        return 0;
    }
    var gate = GateOutcomeValidator.ParseAndValidate(input.Bytes!);
    if (!gate.IsValid) { WriteJson(Console.Out, new { valid = false, diagnostics = gate.Diagnostics }); return FailureExit(gate.Diagnostics); }
    if (operation == "validate") WriteJson(Console.Out, new { valid = true, diagnostics = Array.Empty<Diagnostic>() });
    else await Console.OpenStandardOutput().WriteAsync(GateOutcomeNormalizer.Normalize(gate.Outcome!));
    return 0;
}

static async Task<int> CompareAsync(string tracePath, string ledgerPath, string declarationPath, string baselinePath)
{
    var traceInput = await ReadAsync(tracePath, "trace"); if (traceInput.ExitCode is { } traceExit) return traceExit;
    var traceResult = TraceValidator.ParseAndValidate(traceInput.Bytes!);
    if (!traceResult.IsValid) { WriteJson(Console.Out, new { valid = false, document = "trace", diagnostics = traceResult.Diagnostics }); return FailureExit(traceResult.Diagnostics); }
    var ledgerInput = await ReadAsync(ledgerPath, "ledger"); if (ledgerInput.ExitCode is { } ledgerExit) return ledgerExit;
    var ledgerResult = ObligationLedgerValidator.ParseAndValidate(ledgerInput.Bytes!, traceResult.Envelope!);
    if (!ledgerResult.IsValid) { WriteJson(Console.Out, new { valid = false, document = "ledger", diagnostics = ledgerResult.Diagnostics }); return FailureExit(ledgerResult.Diagnostics); }
    var declarationInput = await ReadAsync(declarationPath, "declaration"); if (declarationInput.ExitCode is { } declarationExit) return declarationExit;
    var declarationResult = TerminationDeclarationValidator.ParseAndValidate(declarationInput.Bytes!, traceResult.Envelope!);
    if (!declarationResult.IsValid) { WriteJson(Console.Out, new { valid = false, document = "declaration", diagnostics = declarationResult.Diagnostics }); return FailureExit(declarationResult.Diagnostics); }
    var baselineInput = await ReadAsync(baselinePath, "baseline"); if (baselineInput.ExitCode is { } baselineExit) return baselineExit;
    var baselineResult = BaselineDefinitionValidator.ParseAndValidate(baselineInput.Bytes!, traceResult.Envelope!, ledgerResult.Ledger!);
    if (!baselineResult.IsValid) { WriteJson(Console.Out, new { valid = false, document = "baseline", diagnostics = baselineResult.Diagnostics }); return FailureExit(baselineResult.Diagnostics); }
    var result = BaselineComparator.Compare(traceResult.Envelope!, ledgerResult.Ledger!, declarationResult.Declaration!, baselineResult.BaselineDefinition!);
    await Console.OpenStandardOutput().WriteAsync(BaselineComparisonResultNormalizer.Normalize(result)); return 0;
}

static int FailureExit(IReadOnlyList<Diagnostic> diagnostics) =>
    diagnostics.Any(d => d.Code is DiagnosticCodes.InvalidJson or DiagnosticCodes.InputTooLarge) ? 2 : 1;

static async Task<int> AssessAsync(string tracePath, string ledgerPath, string declarationPath)
{
    var traceInput = await ReadAsync(tracePath, "trace");
    if (traceInput.ExitCode is { } traceExit) return traceExit;
    var traceResult = TraceValidator.ParseAndValidate(traceInput.Bytes!);
    if (!traceResult.IsValid)
    {
        WriteJson(Console.Out, new { valid = false, document = "trace", diagnostics = traceResult.Diagnostics });
        return traceResult.Diagnostics.Any(d => d.Code is DiagnosticCodes.InvalidJson or DiagnosticCodes.InputTooLarge) ? 2 : 1;
    }
    var ledgerInput = await ReadAsync(ledgerPath, "ledger");
    if (ledgerInput.ExitCode is { } ledgerExit) return ledgerExit;
    var ledgerResult = ObligationLedgerValidator.ParseAndValidate(ledgerInput.Bytes!, traceResult.Envelope!);
    if (!ledgerResult.IsValid)
    {
        WriteJson(Console.Out, new { valid = false, document = "ledger", diagnostics = ledgerResult.Diagnostics });
        return ledgerResult.Diagnostics.Any(d => d.Code is DiagnosticCodes.InvalidJson or DiagnosticCodes.InputTooLarge) ? 2 : 1;
    }
    var declarationInput = await ReadAsync(declarationPath, "declaration");
    if (declarationInput.ExitCode is { } declarationExit) return declarationExit;
    var declarationResult = TerminationDeclarationValidator.ParseAndValidate(declarationInput.Bytes!, traceResult.Envelope!);
    if (!declarationResult.IsValid)
    {
        WriteJson(Console.Out, new { valid = false, document = "declaration", diagnostics = declarationResult.Diagnostics });
        return declarationResult.Diagnostics.Any(d => d.Code is DiagnosticCodes.InvalidJson or DiagnosticCodes.InputTooLarge) ? 2 : 1;
    }
    var result = StopAssessor.Assess(traceResult.Envelope!, ledgerResult.Ledger!, declarationResult.Declaration!);
    await Console.OpenStandardOutput().WriteAsync(StopAssessmentResultNormalizer.Normalize(result));
    return 0;
}

static async Task<int> EvaluateAsync(string tracePath, string ledgerPath)
{
    var traceInput = await ReadAsync(tracePath, "trace");
    if (traceInput.ExitCode is { } traceExit) return traceExit;
    var traceResult = TraceValidator.ParseAndValidate(traceInput.Bytes!);
    if (!traceResult.IsValid)
    {
        WriteJson(Console.Out, new { valid = false, document = "trace", diagnostics = traceResult.Diagnostics });
        return traceResult.Diagnostics.Any(d => d.Code == DiagnosticCodes.InvalidJson) ? 2 : 1;
    }
    var ledgerInput = await ReadAsync(ledgerPath, "ledger");
    if (ledgerInput.ExitCode is { } ledgerExit) return ledgerExit;
    var ledgerResult = ObligationLedgerValidator.ParseAndValidate(ledgerInput.Bytes!, traceResult.Envelope!);
    if (!ledgerResult.IsValid)
    {
        WriteJson(Console.Out, new { valid = false, document = "ledger", diagnostics = ledgerResult.Diagnostics });
        return ledgerResult.Diagnostics.Any(d => d.Code is DiagnosticCodes.InvalidJson or DiagnosticCodes.InputTooLarge) ? 2 : 1;
    }
    var result = Evaluator.Evaluate(traceResult.Envelope!, ledgerResult.Ledger!);
    await Console.OpenStandardOutput().WriteAsync(EvaluationResultNormalizer.Normalize(result));
    return 0;
}

static async Task<(byte[]? Bytes, int? ExitCode)> ReadAsync(string path, string? document)
{
    try
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var content = new MemoryStream();
        var buffer = new byte[64 * 1024];
        var remaining = TraceValidator.MaximumInputSizeBytes + 1;
        while (remaining > 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)));
            if (read == 0) break;
            content.Write(buffer, 0, read);
            remaining -= read;
        }
        if (content.Length > TraceValidator.MaximumInputSizeBytes)
        {
            var size = TraceValidator.ValidateInputSize(content.Length);
            WriteValidationFailure(document, size.Diagnostics);
            return (null, 2);
        }
        return (content.ToArray(), null);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        if (document is null)
            WriteJson(Console.Error, new { error = "input", message = "Input file could not be read." });
        else
            WriteJson(Console.Error, new { error = "input", document, message = "Input file could not be read." });
        return (null, 2);
    }
}

static void WriteValidationFailure(string? document, IReadOnlyList<Diagnostic> diagnostics)
{
    if (document is null)
        WriteJson(Console.Out, new { valid = false, diagnostics });
    else
        WriteJson(Console.Out, new { valid = false, document, diagnostics });
}

static void WriteJson(TextWriter output, object value)
{
    output.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    }));
}
