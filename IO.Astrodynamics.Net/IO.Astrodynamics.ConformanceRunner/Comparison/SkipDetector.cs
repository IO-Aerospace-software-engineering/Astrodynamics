using System.Text.Json;

namespace IO.Astrodynamics.ConformanceRunner.Comparison;

public static class SkipDetector
{
    /// <summary>
    /// Returns true if any golden output value is null or "TODO", meaning the case should be skipped.
    /// </summary>
    public static bool ShouldSkip(JsonElement outputs)
    {
        foreach (var prop in outputs.EnumerateObject())
        {
            if (IsNullOrTodo(prop.Value))
                return true;
        }

        return false;
    }

    private static bool IsNullOrTodo(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return true;
            case JsonValueKind.String:
                return element.GetString() == "TODO";
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (IsNullOrTodo(item))
                        return true;
                }
                return false;
            default:
                return false;
        }
    }
}
