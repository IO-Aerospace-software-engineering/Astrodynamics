using System.Text.Json;
using System.Text.Json.Serialization;

namespace IO.Astrodynamics.ConformanceRunner.Models;

public class ExpectedResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; }

    [JsonPropertyName("case_version")]
    public string CaseVersion { get; set; }

    [JsonPropertyName("generated_by")]
    public GeneratedBy GeneratedBy { get; set; }

    [JsonPropertyName("outputs")]
    public JsonElement Outputs { get; set; }
}

public class GeneratedBy
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; }

    [JsonPropertyName("tool_version")]
    public string ToolVersion { get; set; }

    [JsonPropertyName("kernel")]
    public string Kernel { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; }
}
