using System.Text.Json;
using ProgressTrace.Core.Diagnostics;

namespace ProgressTrace.Core.Validation;

internal static class JsonValidationHelpers
{
    internal static ValidationResult ValidateInputSize(long length) =>
        length <= TraceValidator.MaximumInputSizeBytes
            ? new(null, [])
            : new(null, [new(DiagnosticCodes.InputTooLarge, "",
                $"Input exceeds the maximum size of {TraceValidator.MaximumInputSizeBytes} bytes.")]);

    internal static bool TryRequired(JsonElement parent, string name, string pointer, List<Diagnostic> diagnostics, out JsonElement value)
    {
        if (parent.TryGetProperty(name, out value))
        {
            return true;
        }
        diagnostics.Add(new(DiagnosticCodes.Required, Path(pointer, name), $"Required property {name} is missing."));
        return false;
    }

    internal static void RejectUnknown(JsonElement value, string pointer, string[] allowed, List<Diagnostic> diagnostics)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                diagnostics.Add(new(DiagnosticCodes.UnknownProperty, Path(pointer, property.Name), "Property is not allowed."));
            }
        }
    }

    internal static void RejectDuplicateProperties(JsonElement value, string pointer, List<Diagnostic> diagnostics)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                var child = Path(pointer, property.Name);
                if (!names.Add(property.Name))
                {
                    diagnostics.Add(new(DiagnosticCodes.DuplicateProperty, child, "JSON object property names must be unique."));
                }
                RejectDuplicateProperties(property.Value, child, diagnostics);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                RejectDuplicateProperties(item, pointer + "/" + index++, diagnostics);
            }
        }
    }

    internal static string Path(string parent, string name) =>
        parent + "/" + name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
