using System.Text.Json;
using ProgressTrace.Core.Diagnostics;
using ProgressTrace.Core.Normalization;
using ProgressTrace.Core.Validation;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length != 2 || args[0] is not ("validate" or "normalize"))
    {
        WriteJson(Console.Error, new
        {
            error = "usage",
            message = "Usage: progresstrace <validate|normalize> <path>"
        });
        return 2;
    }

    byte[] input;
    try
    {
        var sizeValidation = TraceValidator.ValidateInputSize(new FileInfo(args[1]).Length);
        if (!sizeValidation.IsValid)
        {
            WriteJson(Console.Out, new { valid = false, diagnostics = sizeValidation.Diagnostics });
            return 2;
        }
        input = await File.ReadAllBytesAsync(args[1]);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        WriteJson(Console.Error, new { error = "input", message = "Input file could not be read." });
        return 2;
    }

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

static void WriteJson(TextWriter output, object value)
{
    output.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    }));
}
