using System.Text.Json;
using System.Text.RegularExpressions;

static class SchemaConformance
{
    private static readonly HashSet<string> Supported = new(StringComparer.Ordinal)
    {
        "$schema", "$id", "title", "$defs", "$ref", "type", "required", "properties",
        "additionalProperties", "items", "minItems", "minLength", "minimum", "maximum", "pattern", "const", "enum",
        "allOf", "anyOf", "if", "then", "else", "not", "uniqueItems"
    };

    public static bool Validate(byte[] instanceBytes, byte[] schemaBytes, out string error)
    {
        try
        {
            using var instance = JsonDocument.Parse(instanceBytes);
            using var schema = JsonDocument.Parse(schemaBytes);
            AssertSupported(schema.RootElement);
            return ValidateNode(instance.RootElement, schema.RootElement, schema.RootElement, "", out error);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            error = exception.Message;
            return false;
        }
    }

    private static void AssertSupported(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in node.EnumerateObject())
            {
                if (!Supported.Contains(property.Name))
                    throw new InvalidOperationException($"Unsupported schema keyword: {property.Name}");
                if (property.Name is "properties" or "$defs")
                    foreach (var child in property.Value.EnumerateObject()) AssertSupported(child.Value);
                else if (property.Name is "items" or "if" or "then" or "else" or "not") AssertSupported(property.Value);
                else if (property.Name is "allOf" or "anyOf")
                    foreach (var child in property.Value.EnumerateArray()) AssertSupported(child);
            }
        }
    }

    private static bool ValidateNode(JsonElement value, JsonElement schema, JsonElement root, string pointer, out string error)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            const string prefix = "#/$defs/";
            var text = reference.GetString()!;
            if (!text.StartsWith(prefix, StringComparison.Ordinal) ||
                !root.GetProperty("$defs").TryGetProperty(text[prefix.Length..], out schema))
                throw new InvalidOperationException($"Unsupported or unresolved schema reference: {text}");
        }
        if (schema.TryGetProperty("type", out var type))
        {
            var matches = type.ValueKind == JsonValueKind.Array
                ? type.EnumerateArray().Any(candidate => MatchesType(value, candidate.GetString()!))
                : MatchesType(value, type.GetString()!);
            if (!matches) return Fail(pointer, "type", out error);
        }
        if (schema.TryGetProperty("allOf", out var allOf))
            foreach (var child in allOf.EnumerateArray())
                if (!ValidateNode(value, child, root, pointer, out error)) return false;
        if (schema.TryGetProperty("anyOf", out var anyOf) &&
            !anyOf.EnumerateArray().Any(child => ValidateNode(value, child, root, pointer, out _)))
            return Fail(pointer, "anyOf", out error);
        if (schema.TryGetProperty("not", out var not) && ValidateNode(value, not, root, pointer, out _))
            return Fail(pointer, "not", out error);
        if (schema.TryGetProperty("if", out var condition))
        {
            var branchName = ValidateNode(value, condition, root, pointer, out _) ? "then" : "else";
            if (schema.TryGetProperty(branchName, out var branch) && !ValidateNode(value, branch, root, pointer, out error))
                return false;
        }
        if (schema.TryGetProperty("const", out var constant) && !JsonElement.DeepEquals(value, constant))
            return Fail(pointer, "const", out error);
        if (schema.TryGetProperty("enum", out var choices) &&
            !choices.EnumerateArray().Any(choice => JsonElement.DeepEquals(value, choice)))
            return Fail(pointer, "enum", out error);
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!;
            if (schema.TryGetProperty("minLength", out var minimum) && text.Length < minimum.GetInt32())
                return Fail(pointer, "minLength", out error);
            if (schema.TryGetProperty("pattern", out var pattern) && !Regex.IsMatch(text, pattern.GetString()!, RegexOptions.CultureInvariant))
                return Fail(pointer, "pattern", out error);
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            if (schema.TryGetProperty("minimum", out var minimum) && number < minimum.GetDecimal())
                return Fail(pointer, "minimum", out error);
            if (schema.TryGetProperty("maximum", out var maximum) && number > maximum.GetDecimal())
                return Fail(pointer, "maximum", out error);
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            if (schema.TryGetProperty("minItems", out var minimum) && value.GetArrayLength() < minimum.GetInt32())
                return Fail(pointer, "minItems", out error);
            if (schema.TryGetProperty("uniqueItems", out var unique) && unique.GetBoolean())
            {
                var arrayValues = value.EnumerateArray().ToList();
                for (var left = 0; left < arrayValues.Count; left++)
                    for (var right = left + 1; right < arrayValues.Count; right++)
                        if (JsonElement.DeepEquals(arrayValues[left], arrayValues[right])) return Fail(pointer, "uniqueItems", out error);
            }
            if (schema.TryGetProperty("items", out var items))
            {
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    if (!ValidateNode(item, items, root, $"{pointer}/{index}", out error)) return false;
                    index++;
                }
            }
        }
        if (value.ValueKind == JsonValueKind.Object)
        {
            var allowed = schema.TryGetProperty("properties", out var properties) ? properties : default;
            if (schema.TryGetProperty("required", out var required))
                foreach (var name in required.EnumerateArray())
                    if (!value.TryGetProperty(name.GetString()!, out _)) return Fail(pointer, "required", out error);
            foreach (var property in value.EnumerateObject())
            {
                if (allowed.ValueKind == JsonValueKind.Object && allowed.TryGetProperty(property.Name, out var child))
                {
                    if (!ValidateNode(property.Value, child, root, $"{pointer}/{Escape(property.Name)}", out error)) return false;
                }
                else if (schema.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.False)
                    return Fail($"{pointer}/{Escape(property.Name)}", "additionalProperties", out error);
            }
        }
        error = "";
        return true;
    }

    private static bool MatchesType(JsonElement value, string type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        _ => throw new InvalidOperationException($"Unsupported schema type: {type}")
    };

    private static bool Fail(string pointer, string keyword, out string error)
    {
        error = $"{pointer}: {keyword}";
        return false;
    }

    private static string Escape(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
