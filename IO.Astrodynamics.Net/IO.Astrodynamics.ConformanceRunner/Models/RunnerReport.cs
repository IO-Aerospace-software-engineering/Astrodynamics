using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace IO.Astrodynamics.ConformanceRunner.Models;

public class RunnerReport
{
    [JsonPropertyName("report_meta")]
    public ReportMeta ReportMeta { get; set; }

    [JsonPropertyName("results")]
    public List<ResultEntry> Results { get; set; } = new();

    [JsonPropertyName("summary")]
    public ReportSummary Summary { get; set; }
}

public class ReportMeta
{
    [JsonPropertyName("runner_name")]
    public string RunnerName { get; set; }

    [JsonPropertyName("runner_version")]
    public string RunnerVersion { get; set; }

    [JsonPropertyName("framework")]
    public string Framework { get; set; }

    [JsonPropertyName("framework_version")]
    public string FrameworkVersion { get; set; }

    [JsonPropertyName("run_timestamp")]
    public string RunTimestamp { get; set; }

    [JsonPropertyName("conformance_tests_commit")]
    public string ConformanceTestsCommit { get; set; }
}

public class ResultEntry
{
    [JsonPropertyName("case_id")]
    public string CaseId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("deltas")]
    public Dictionary<string, DeltaValue> Deltas { get; set; } = new();

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Message { get; set; }
}

public class DeltaValue
{
    [JsonPropertyName("max_abs_delta")]
    public double MaxAbsDelta { get; set; }

    [JsonPropertyName("max_rel_delta")]
    public double MaxRelDelta { get; set; }

    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    [JsonPropertyName("golden_value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? GoldenValue { get; set; }
}

public class ReportSummary
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("passed")]
    public int Passed { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; set; }

    [JsonPropertyName("errors")]
    public int Errors { get; set; }

    [JsonPropertyName("accuracy_by_metric")]
    public List<AccuracyMetricSummary> AccuracyByMetric { get; set; } = new();
}

public class AccuracyMetricSummary
{
    [JsonPropertyName("metric")]
    public string Metric { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; }

    [JsonPropertyName("cases_evaluated")]
    public int CasesEvaluated { get; set; }

    [JsonPropertyName("pass_count")]
    public int PassCount { get; set; }

    [JsonPropertyName("fail_count")]
    public int FailCount { get; set; }

    [JsonPropertyName("min_abs_delta")]
    public double MinAbsDelta { get; set; }

    [JsonPropertyName("max_abs_delta")]
    public double MaxAbsDelta { get; set; }

    [JsonPropertyName("avg_abs_delta")]
    public double AvgAbsDelta { get; set; }

    [JsonPropertyName("min_rel_delta")]
    public double MinRelDelta { get; set; }

    [JsonPropertyName("max_rel_delta")]
    public double MaxRelDelta { get; set; }

    [JsonPropertyName("avg_rel_delta")]
    public double AvgRelDelta { get; set; }
}
