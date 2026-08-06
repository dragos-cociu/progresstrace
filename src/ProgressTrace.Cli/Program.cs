using System.Text.Json;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Assessment;
using ProgressTrace.Core.Evaluation;
using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Validation;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 3 && args[0] == "evaluate")
    {
        return await EvaluateAsync(args[1], args[2]);
    }
    if (args.Length == 4 && args[0] == "assess")
    {
        return await AssessAsync(args[1], args[2], args[3]);
    }
    if (args.Length != 2 || args[0] is not ("validate" or "normalize"))
    {
        WriteJson(Console.Error, new
        {
            error = "usage",
            message = "Usage: progresstrace <validate|normalize> <path> | progresstrace evaluate <trace-path> <ledger-path> | progresstrace assess <trace-path> <ledger-path> <termination-declaration-path>"
        });
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
