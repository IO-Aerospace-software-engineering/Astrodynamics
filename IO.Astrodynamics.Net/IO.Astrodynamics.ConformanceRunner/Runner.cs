using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Solvers;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.TimeSystem;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace IO.Astrodynamics.ConformanceRunner;

public class Runner
{
    private readonly string _conformanceTestsPath;
    private readonly string _spiceKernelsPath;
    private readonly Dictionary<string, ICategorySolver> _solvers;
    private readonly SchemaValidator _schemaValidator;

    public Runner(string conformanceTestsPath, string spiceKernelsPath)
    {
        _conformanceTestsPath = conformanceTestsPath;
        _spiceKernelsPath = spiceKernelsPath;
        _solvers = new ICategorySolver[]
        {
            new TriadSolver(),
            new EclipseSolver(conformanceTestsPath),
            new PropagatorSolver(conformanceTestsPath),
            new StmSolver(spiceKernelsPath),
            new TrackingDataSolver(conformanceTestsPath),
            new GeosynchronousSolver(conformanceTestsPath),
            new OrbitalElementsSolver(),
            new TimeConversionSolver(),
            new FrameTransformationSolver(conformanceTestsPath)
        }.ToDictionary(solver => solver.Category);
        _schemaValidator = new SchemaValidator(conformanceTestsPath);
    }

    public RunnerReport Run()
    {
        // Load SPICE kernels
        Console.WriteLine($"Loading SPICE kernels from: {_spiceKernelsPath}");
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(_spiceKernelsPath));

        // Load tolerances
        var toleranceConfig = LoadTolerances();

        // Get conformance tests git SHA
        var commitSha = GetGitSha(_conformanceTestsPath);

        // Discover cases
        var caseDirs = DiscoverCases();
        Console.WriteLine($"Discovered {caseDirs.Count} test case(s)");

        var frameworkAssembly = typeof(SpiceAPI).Assembly;
        var informationalVersion = frameworkAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var report = new RunnerReport
        {
            ReportMeta = new ReportMeta
            {
                RunnerName = "IO.Astrodynamics.ConformanceRunner",
                RunnerVersion = "1.0.0",
                Framework = "IO.Astrodynamics.Net",
                FrameworkVersion = frameworkAssembly.GetName().Version?.ToString() ?? "unknown",
                FrameworkInformationalVersion = informationalVersion,
                FrameworkCommit = CommitFromInformationalVersion(informationalVersion),
                RunTimestamp = DateTime.UtcNow.ToString("o"),
                ConformanceTestsCommit = commitSha
            }
        };

        int passed = 0, failed = 0, skipped = 0, errors = 0;

        foreach (var caseDir in caseDirs)
        {
            var result = RunCase(caseDir, toleranceConfig);
            report.Results.Add(result);

            switch (result.Status)
            {
                case "PASS": passed++; break;
                case "FAIL": failed++; break;
                case "SKIP": skipped++; break;
                case "ERROR": errors++; break;
            }

            var statusColor = result.Status switch
            {
                "PASS" => "\u001b[32m",
                "FAIL" => "\u001b[31m",
                "SKIP" => "\u001b[33m",
                "ERROR" => "\u001b[31m",
                _ => ""
            };
            Console.WriteLine($"  {statusColor}{result.Status}\u001b[0m  {result.CaseId}{(result.Message != null ? $" — {result.Message}" : "")}");
        }

        report.Summary = new ReportSummary
        {
            Total = report.Results.Count,
            Passed = passed,
            Failed = failed,
            Skipped = skipped,
            Errors = errors,
            AccuracyByMetric = BuildAccuracySummary(report.Results, toleranceConfig)
        };

        return report;
    }

    private ResultEntry RunCase(string caseDir, ToleranceConfig toleranceConfig)
    {
        string caseId = "unknown";
        try
        {
            // Load inputs
            var inputsPath = Path.Combine(caseDir, "inputs.yaml");
            var expectedPath = Path.Combine(caseDir, "expected-result.json");
            if (!File.Exists(expectedPath))
                expectedPath = Path.Combine(caseDir, "expected-results.json");

            if (!File.Exists(inputsPath) || !File.Exists(expectedPath))
            {
                return new ResultEntry
                {
                    CaseId = Path.GetFileName(caseDir),
                    Status = "ERROR",
                    Message = "Missing inputs.yaml or expected-result.json"
                };
            }

            var inputYaml = File.ReadAllText(inputsPath);
            var expectedJson = File.ReadAllText(expectedPath);

            // Validate inputs against case schema
            var inputsError = _schemaValidator.ValidateInputs(inputYaml);
            if (inputsError != null)
            {
                return new ResultEntry
                {
                    CaseId = Path.GetFileName(caseDir),
                    Status = "ERROR",
                    Message = inputsError
                };
            }

            // Validate expected result against expected schema
            var expectedError = _schemaValidator.ValidateExpected(expectedJson);
            if (expectedError != null)
            {
                return new ResultEntry
                {
                    CaseId = Path.GetFileName(caseDir),
                    Status = "ERROR",
                    Message = expectedError
                };
            }

            var yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var caseInput = yamlDeserializer.Deserialize<CaseInput>(inputYaml);
            caseId = caseInput.Id ?? Path.GetFileName(caseDir);

            var expected = JsonSerializer.Deserialize<ExpectedResult>(expectedJson,
                new JsonSerializerOptions { AllowTrailingCommas = true });

            // Check for SKIP
            if (SkipDetector.ShouldSkip(expected.Outputs))
            {
                return new ResultEntry
                {
                    CaseId = caseId,
                    Status = "SKIP",
                    Message = "Golden values contain null or TODO"
                };
            }

            // Dispatch to solver
            if (!_solvers.TryGetValue(caseInput.Category, out var solver))
            {
                return new ResultEntry
                {
                    CaseId = caseId,
                    Status = "ERROR",
                    Message = $"No solver for category: {caseInput.Category}"
                };
            }

            var computed = solver.Solve(caseInput);

            // Compare outputs
            var result = CompareOutputs(caseId, caseInput.Category, computed, expected.Outputs, toleranceConfig, caseInput.TolerancesOverride, caseInput);
            result.Category = caseInput.Category;
            return result;
        }
        catch (Exception ex)
        {
            return new ResultEntry
            {
                CaseId = caseId,
                Status = "ERROR",
                Message = $"{ex.GetType().Name}: {ex.Message}"
            };
        }
    }

    private ResultEntry CompareOutputs(
        string caseId,
        string category,
        Dictionary<string, object> computed,
        JsonElement golden,
        ToleranceConfig toleranceConfig,
        Dictionary<string, TolerancePair> caseOverrides,
        CaseInput caseInput)
    {
        if (!MetricTable.Covers(category))
        {
            throw new InvalidOperationException($"No comparison for the category {category}.");
        }

        // The state transition matrix of the stm category, by blocks; its final state as the propagator's
        var deltas = new Dictionary<string, DeltaValue>();
        bool allPass = true;
        if (category == "stm" && golden.TryGetProperty("final_stm", out var stmGolden))
        {
            var stmTol = ToleranceComparer.ResolveTolerance(StmComparer.ToleranceKey, toleranceConfig?.Defaults, caseOverrides);
            foreach (var (block, delta) in StmComparer.Compare((double[])computed["final_stm"], stmGolden, stmTol))
            {
                deltas[block] = delta;
                allPass &= delta.Passed;
            }

            golden = Without(golden, "final_stm");
        }

        // The kinds and keys of the outputs (spec.md sections 6.1, 11 and 12), the outputs present in the golden, no
        // fallback tolerance
        var measures = OutputComparer.Compare(caseInput, computed, golden, toleranceConfig?.Defaults);
        var goldens = OutputComparer.Outputs(golden).ToDictionary(o => o.Output, o => o.Golden);
        foreach (var m in measures)
        {
            // The golden of a component of the final state, for the magnitude rows of accuracy_by_metric
            deltas[m.Output] = new DeltaValue
            {
                MaxAbsDelta = m.AbsoluteDelta, MaxRelDelta = m.RelativeDelta, Passed = m.Passed,
                GoldenValue = m.Output.StartsWith("final_") && goldens[m.Output].ValueKind == JsonValueKind.Number
                    ? goldens[m.Output].GetDouble()
                    : null
            };
            allPass &= m.Passed;
        }

        return new ResultEntry
        {
            CaseId = caseId,
            Status = allPass ? "PASS" : "FAIL",
            Deltas = deltas,
            Measures = measures
        };
    }

    // The outputs of a golden without one of them
    private static JsonElement Without(JsonElement outputs, string name)
    {
        var rest = outputs.EnumerateObject().Where(p => p.Name != name).ToDictionary(p => p.Name, p => p.Value);
        return JsonSerializer.SerializeToElement(rest);
    }

    private static List<AccuracyMetricSummary> BuildAccuracySummary(List<ResultEntry> results, ToleranceConfig toleranceConfig)
    {
        // Collect (DeltaValue, casePassed) per metric.
        // casePassed = result.Status=="PASS": if the whole case passed, all its metrics passed.
        // For FAIL cases we use DeltaValue.Passed (set during comparison) to distinguish
        // which individual metrics failed.
        var metricData = new Dictionary<string, List<(DeltaValue delta, bool casePassed)>>();

        foreach (var result in results)
        {
            if (result.Status != "PASS" && result.Status != "FAIL") continue;
            bool casePassed = result.Status == "PASS";
            foreach (var kv in result.Deltas)
            {
                if (double.IsNaN(kv.Value.MaxAbsDelta)) continue;
                // The outputs by category: semi_major_axis_km of orbital_elements is not the geosynchronous one
                var metric = MetricTable.TryGet(result.Category, kv.Key, out _) ? MetricTable.SummaryKey(result.Category, kv.Key) : kv.Key;
                if (!metricData.TryGetValue(metric, out var list))
                    metricData[metric] = list = new List<(DeltaValue, bool)>();
                // If case passed, every metric in it passed; otherwise use stored Passed flag
                list.Add((kv.Value, casePassed || kv.Value.Passed));
            }
        }

        // Resolve display unit and scale factor for each metric
        static (string unit, double scale) ResolveUnit(string metric) => metric switch
        {
            "stm_rr" or "stm_rv" or "stm_vr" or "stm_vv"            => ("-",   1.0),
            // <category>/<output>, in the units of spec.md sections 11 and 12
            _ when MetricTable.TryGetSummaryUnit(metric, out var unit)
                                                                    => (unit,  1.0),
            _                                                       => ("-",   1.0)
        };

        AccuracyMetricSummary MakeSummary(string metric, List<(DeltaValue delta, bool passed)> entries)
        {
            var (unit, scale) = ResolveUnit(metric);
            var absDeltas = entries.Select(e => e.delta.MaxAbsDelta * scale).ToList();
            var relDeltas = entries.Select(e => e.delta.MaxRelDelta)
                                   .Where(v => !double.IsInfinity(v) && !double.IsNaN(v))
                                   .DefaultIfEmpty(0.0).ToList();
            return new AccuracyMetricSummary
            {
                Metric          = metric,
                Unit            = unit,
                CasesEvaluated  = entries.Count,
                PassCount       = entries.Count(e => e.passed),
                FailCount       = entries.Count(e => !e.passed),
                MinAbsDelta     = absDeltas.Min(),
                MaxAbsDelta     = absDeltas.Max(),
                AvgAbsDelta     = absDeltas.Average(),
                MinRelDelta     = relDeltas.Min(),
                MaxRelDelta     = relDeltas.Max(),
                AvgRelDelta     = relDeltas.Average()
            };
        }

        var summaries = new List<AccuracyMetricSummary>();

        foreach (var kv in metricData.OrderBy(x => x.Key))
            summaries.Add(MakeSummary(kv.Key, kv.Value));

        // --- Synthetic vector-magnitude rows ---
        // Position magnitude: combine per-case x/y/z deltas into a single magnitude
        AddMagnitudeSummary(summaries, results,
            synthetic: "position_magnitude_km",
            unit: "m",
            scale: 1000.0,
            components: new[] { "final_x_km", "final_y_km", "final_z_km" });

        // Velocity magnitude
        AddMagnitudeSummary(summaries, results,
            synthetic: "velocity_magnitude_km_s",
            unit: "m/s",
            scale: 1000.0,
            components: new[] { "final_vx_km_s", "final_vy_km_s", "final_vz_km_s" });

        return summaries;
    }

    private static void AddMagnitudeSummary(
        List<AccuracyMetricSummary> summaries,
        List<ResultEntry> results,
        string synthetic,
        string unit,
        double scale,
        string[] components)
    {
        var magnitudes = new List<(double absDelta, double relDelta, bool passed)>();

        foreach (var result in results)
        {
            if (result.Status != "PASS" && result.Status != "FAIL") continue;
            if (!components.All(c => result.Deltas.ContainsKey(c))) continue;

            // Magnitude of the delta vector (converted to display unit)
            double absDeltaMag = scale * System.Math.Sqrt(
                components.Sum(c => System.Math.Pow(result.Deltas[c].MaxAbsDelta, 2)));

            // Magnitude of the golden vector for relative delta
            bool hasGolden = components.All(c => result.Deltas[c].GoldenValue.HasValue);
            double goldenMag = hasGolden
                ? System.Math.Sqrt(components.Sum(c => System.Math.Pow(result.Deltas[c].GoldenValue!.Value, 2)))
                : 0.0;

            double relDelta = hasGolden && goldenMag != 0.0
                ? (absDeltaMag / scale) / goldenMag   // relative is dimensionless
                : (absDeltaMag == 0.0 ? 0.0 : double.PositiveInfinity);

            // A magnitude row passes iff the whole case passed
            bool casePassed = result.Status == "PASS";

            magnitudes.Add((absDeltaMag, relDelta, casePassed));
        }

        if (magnitudes.Count == 0) return;

        var finiteRel = magnitudes.Select(m => m.relDelta)
                                  .Where(v => !double.IsInfinity(v) && !double.IsNaN(v))
                                  .DefaultIfEmpty(0.0).ToList();

        summaries.Add(new AccuracyMetricSummary
        {
            Metric         = synthetic,
            Unit           = unit,
            CasesEvaluated = magnitudes.Count,
            PassCount      = magnitudes.Count(m => m.passed),
            FailCount      = magnitudes.Count(m => !m.passed),
            MinAbsDelta    = magnitudes.Min(m => m.absDelta),
            MaxAbsDelta    = magnitudes.Max(m => m.absDelta),
            AvgAbsDelta    = magnitudes.Average(m => m.absDelta),
            MinRelDelta    = finiteRel.Min(),
            MaxRelDelta    = finiteRel.Max(),
            AvgRelDelta    = finiteRel.Average()
        });
    }

    private ToleranceConfig LoadTolerances()
    {
        var tolPath = Path.Combine(_conformanceTestsPath, "tolerances.yaml");
        if (!File.Exists(tolPath))
        {
            Console.WriteLine("Warning: tolerances.yaml not found, using tight defaults");
            return null;
        }

        var yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        return yamlDeserializer.Deserialize<ToleranceConfig>(File.ReadAllText(tolPath));
    }

    private List<string> DiscoverCases()
    {
        var casesDir = Path.Combine(_conformanceTestsPath, "cases");
        if (!Directory.Exists(casesDir))
        {
            Console.WriteLine($"Warning: cases directory not found at {casesDir}");
            return new List<string>();
        }

        var cases = new List<string>();
        foreach (var categoryDir in Directory.GetDirectories(casesDir).OrderBy(d => d))
        {
            foreach (var caseDir in Directory.GetDirectories(categoryDir).OrderBy(d => d))
            {
                var inputsFile = Path.Combine(caseDir, "inputs.yaml");
                if (File.Exists(inputsFile))
                {
                    cases.Add(caseDir);
                }
            }
        }

        return cases;
    }

    /// <summary>
    /// The SDK appends the source commit to the informational version (<c>10.1.0+&lt;sha&gt;</c>).
    /// </summary>
    internal static string CommitFromInformationalVersion(string informationalVersion)
    {
        int plus = informationalVersion.IndexOf('+');
        return plus >= 0 && plus < informationalVersion.Length - 1 ? informationalVersion[(plus + 1)..] : "unknown";
    }

    private static string GetGitSha(string repoPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-C \"{repoPath}\" rev-parse HEAD",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            var sha = proc?.StandardOutput.ReadToEnd().Trim();
            proc?.WaitForExit();
            return sha ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
