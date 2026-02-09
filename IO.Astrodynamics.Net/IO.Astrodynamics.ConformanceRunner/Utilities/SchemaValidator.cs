using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace IO.Astrodynamics.ConformanceRunner.Utilities;

public class SchemaValidator
{
    private readonly JsonSchema _caseSchema;
    private readonly JsonSchema _expectedSchema;

    public SchemaValidator(string conformanceTestsPath)
    {
        var caseSchemaPath = Path.Combine(conformanceTestsPath, "schemas", "v1", "case.schema.json");
        var expectedSchemaPath = Path.Combine(conformanceTestsPath, "schemas", "v1", "expected.schema.json");

        if (!File.Exists(caseSchemaPath))
            throw new FileNotFoundException($"Case schema not found: {caseSchemaPath}");
        if (!File.Exists(expectedSchemaPath))
            throw new FileNotFoundException($"Expected result schema not found: {expectedSchemaPath}");

        _caseSchema = JsonSchema.FromFile(caseSchemaPath);
        _expectedSchema = JsonSchema.FromFile(expectedSchemaPath);
    }

    /// <summary>
    /// Validates a YAML-parsed inputs document (converted to JSON) against the case schema.
    /// Returns null if valid, or an error message string if invalid.
    /// </summary>
    public string ValidateInputs(string yamlContent)
    {
        var jsonNode = YamlToJsonNode(yamlContent);
        if (jsonNode == null)
            return "Failed to convert YAML inputs to JSON";

        return EvaluateAgainstSchema(jsonNode, _caseSchema, "case input");
    }

    /// <summary>
    /// Validates an expected-result JSON file against the expected schema.
    /// Returns null if valid, or an error message string if invalid.
    /// </summary>
    public string ValidateExpected(string jsonContent)
    {
        JsonNode jsonNode;
        try
        {
            jsonNode = JsonNode.Parse(jsonContent);
        }
        catch (JsonException ex)
        {
            return $"Invalid JSON in expected result: {ex.Message}";
        }

        if (jsonNode == null)
            return "Expected result JSON is null";

        return EvaluateAgainstSchema(jsonNode, _expectedSchema, "expected result");
    }

    private static string EvaluateAgainstSchema(JsonNode jsonNode, JsonSchema schema, string documentType)
    {
        var options = new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        };

        var result = schema.Evaluate(jsonNode, options);
        if (result.IsValid)
            return null;

        var errors = CollectErrors(result);
        if (errors.Count == 0)
            return $"Schema validation failed for {documentType} (no details available)";

        return $"Schema validation failed for {documentType}: {string.Join("; ", errors.Take(5))}";
    }

    private static List<string> CollectErrors(EvaluationResults results)
    {
        var errors = new List<string>();

        if (results.HasErrors && results.Errors != null)
        {
            foreach (var kvp in results.Errors)
            {
                errors.Add($"{results.InstanceLocation}: {kvp.Key} - {kvp.Value}");
            }
        }

        if (results.HasDetails)
        {
            foreach (var detail in results.Details)
            {
                errors.AddRange(CollectErrors(detail));
            }
        }

        return errors;
    }

    /// <summary>
    /// Converts YAML content to a JsonNode by deserializing YAML to an object tree
    /// and then recursively converting to JsonNode with proper type inference.
    /// YamlDotNet deserializes all scalars as strings when targeting object, so we
    /// must detect numeric, boolean, and null values ourselves.
    /// </summary>
    private static JsonNode YamlToJsonNode(string yamlContent)
    {
        try
        {
            var yamlDeserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .Build();

            var yamlObject = yamlDeserializer.Deserialize<object>(yamlContent);
            return ConvertToJsonNode(yamlObject);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static JsonNode ConvertToJsonNode(object value)
    {
        if (value == null)
            return null;

        if (value is Dictionary<object, object> dict)
        {
            var obj = new JsonObject();
            foreach (var kvp in dict)
            {
                obj[kvp.Key.ToString()] = ConvertToJsonNode(kvp.Value);
            }
            return obj;
        }

        if (value is List<object> list)
        {
            var arr = new JsonArray();
            foreach (var item in list)
            {
                arr.Add(ConvertToJsonNode(item));
            }
            return arr;
        }

        if (value is string s)
        {
            // Try to infer the proper JSON type from the string value
            if (s == "null" || s == "~")
                return null;
            if (s == "true")
                return JsonValue.Create(true);
            if (s == "false")
                return JsonValue.Create(false);
            if (double.TryParse(s, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var d))
            {
                // Preserve integer vs float distinction
                if (s.IndexOf('.') < 0 && s.IndexOf('e') < 0 && s.IndexOf('E') < 0
                    && long.TryParse(s, out var l))
                    return JsonValue.Create(l);
                return JsonValue.Create(d);
            }
            return JsonValue.Create(s);
        }

        // Fallback for other types (int, double, bool already resolved by some YAML configs)
        if (value is bool b)
            return JsonValue.Create(b);
        if (value is int i)
            return JsonValue.Create(i);
        if (value is long lg)
            return JsonValue.Create(lg);
        if (value is double db)
            return JsonValue.Create(db);
        if (value is float f)
            return JsonValue.Create(f);

        return JsonValue.Create(value.ToString());
    }
}
