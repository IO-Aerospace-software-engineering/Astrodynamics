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
        _solvers = new Dictionary<string, ICategorySolver>
        {
            ["pointing_triad"] = new TriadSolver(),
            ["eclipse"] = new EclipseSolver(),
            ["propagator"] = new PropagatorSolver(spiceKernelsPath),
            ["tracking_data"] = new TrackingDataSolver(conformanceTestsPath),
            ["geosynchronous"] = new GeosynchronousSolver()
        };
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
            return CompareOutputs(caseId, caseInput.Category, computed, expected.Outputs, toleranceConfig, caseInput.TolerancesOverride);
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
        Dictionary<string, TolerancePair> caseOverrides)
    {
        // Flatten nested tracking_data outputs to match solver's flat dictionary
        if (category == "tracking_data")
            golden = FlattenTrackingDataOutputs(golden);

        var deltas = new Dictionary<string, DeltaValue>();
        bool allPass = true;

        foreach (var prop in golden.EnumerateObject())
        {
            string metricName = prop.Name;

            if (!computed.ContainsKey(metricName))
            {
                deltas[metricName] = new DeltaValue { MaxAbsDelta = double.NaN, MaxRelDelta = double.NaN };
                allPass = false;
                continue;
            }

            var computedVal = computed[metricName];
            var goldenVal = prop.Value;

            bool pass;
            double maxAbsDelta, maxRelDelta;

            switch (metricName)
            {
                case "attitude_quaternion":
                    var computedQ = (double[])computedVal;
                    var goldenQ = ParseQuaternionArray(goldenVal);
                    // Canonicalize both
                    computedQ = UnitConversion.CanonicalizeQuaternion(computedQ);
                    goldenQ = UnitConversion.CanonicalizeQuaternion(goldenQ);
                    var tol = ToleranceComparer.ResolveTolerance(metricName, toleranceConfig?.Defaults, caseOverrides);
                    pass = ToleranceComparer.CompareQuaternion(computedQ, goldenQ, tol, out maxAbsDelta, out maxRelDelta);
                    break;

                case "target_in_fov":
                    var computedBool = (bool)computedVal;
                    var goldenBool = goldenVal.GetBoolean();
                    pass = ToleranceComparer.ExactMatch(computedBool, goldenBool, out maxAbsDelta, out maxRelDelta);
                    break;

                case "penumbra_entry":
                case "umbra_entry":
                case "umbra_exit":
                case "penumbra_exit":
                    var computedTimeStr = (string)computedVal;
                    var goldenTimeStr = goldenVal.GetString();
                    var computedTime = new Time(computedTimeStr);
                    var goldenTime = new Time(goldenTimeStr);
                    var timeTol = ToleranceComparer.ResolveTolerance("eclipse_time_s", toleranceConfig?.Defaults, caseOverrides);
                    pass = ToleranceComparer.CompareTime(computedTime, goldenTime, timeTol, out maxAbsDelta, out maxRelDelta);
                    break;

                case "penumbra_duration_s":
                case "umbra_duration_s":
                    var computedDur = Convert.ToDouble(computedVal);
                    var goldenDur = goldenVal.GetDouble();
                    var durTol = ToleranceComparer.ResolveTolerance(metricName, toleranceConfig?.Defaults, caseOverrides);
                    pass = ToleranceComparer.Passes(computedDur, goldenDur, durTol, out maxAbsDelta, out maxRelDelta);
                    break;

                case "final_x_km":
                case "final_y_km":
                case "final_z_km":
                case "position_x_km":
                case "position_y_km":
                case "position_z_km":
                    var posTol = ToleranceComparer.ResolveTolerance("position_km", toleranceConfig?.Defaults, caseOverrides);
                    pass = ToleranceComparer.Passes(Convert.ToDouble(computedVal), goldenVal.GetDouble(), posTol, out maxAbsDelta, out maxRelDelta);
                    deltas[metricName] = new DeltaValue { MaxAbsDelta = maxAbsDelta, MaxRelDelta = maxRelDelta, Passed = pass, GoldenValue = goldenVal.GetDouble() };
                    if (!pass) allPass = false;
                    continue;

                case "final_vx_km_s":
                case "final_vy_km_s":
                case "final_vz_km_s":
                case "velocity_x_km_s":
                case "velocity_y_km_s":
                case "velocity_z_km_s":
                    var velTol = ToleranceComparer.ResolveTolerance("velocity_km_s", toleranceConfig?.Defaults, caseOverrides);
                    pass = ToleranceComparer.Passes(Convert.ToDouble(computedVal), goldenVal.GetDouble(), velTol, out maxAbsDelta, out maxRelDelta);
                    deltas[metricName] = new DeltaValue { MaxAbsDelta = maxAbsDelta, MaxRelDelta = maxRelDelta, Passed = pass, GoldenValue = goldenVal.GetDouble() };
                    if (!pass) allPass = false;
                    continue;

                case "semi_major_axis_km":
                    var smaTol = ToleranceComparer.ResolveTolerance("semi_major_axis_km", toleranceConfig?.Defaults, caseOverrides);
                    pass = ToleranceComparer.Passes(Convert.ToDouble(computedVal), goldenVal.GetDouble(), smaTol, out maxAbsDelta, out maxRelDelta);
                    deltas[metricName] = new DeltaValue { MaxAbsDelta = maxAbsDelta, MaxRelDelta = maxRelDelta, Passed = pass, GoldenValue = goldenVal.GetDouble() };
                    if (!pass) allPass = false;
                    continue;

                case "eccentricity":
                    var eccTol = ToleranceComparer.ResolveTolerance("eccentricity", toleranceConfig?.Defaults, caseOverrides);
                    pass = ToleranceComparer.Passes(Convert.ToDouble(computedVal), goldenVal.GetDouble(), eccTol, out maxAbsDelta, out maxRelDelta);
                    break;

                case "inclination_deg":
                case "raan_deg":
                case "argp_deg":
                case "mean_anomaly_deg":
                    var angTol = ToleranceComparer.ResolveTolerance("angle_deg", toleranceConfig?.Defaults, caseOverrides);
                    pass = ToleranceComparer.Passes(Convert.ToDouble(computedVal), goldenVal.GetDouble(), angTol, out maxAbsDelta, out maxRelDelta);
                    break;

                default:
                    if (metricName == "arc_count")
                    {
                        var computedInt = Convert.ToInt32(computedVal);
                        var goldenInt = goldenVal.GetInt32();
                        pass = computedInt == goldenInt;
                        maxAbsDelta = System.Math.Abs(computedInt - goldenInt);
                        maxRelDelta = goldenInt != 0 ? maxAbsDelta / goldenInt : (maxAbsDelta == 0 ? 0.0 : double.PositiveInfinity);
                        break;
                    }

                    if (metricName.StartsWith("arc_") && (metricName.EndsWith("_rise") || metricName.EndsWith("_fall")))
                    {
                        var computedTimeStr2 = (string)computedVal;
                        var goldenTimeStr2 = goldenVal.GetString();
                        var computedTime2 = ParseTimeString(computedTimeStr2);
                        var goldenTime2 = ParseTimeString(goldenTimeStr2);
                        var arcTimeTol = ToleranceComparer.ResolveTolerance("arc_time_s", toleranceConfig?.Defaults, caseOverrides);
                        pass = ToleranceComparer.CompareTime(computedTime2, goldenTime2, arcTimeTol, out maxAbsDelta, out maxRelDelta);
                        break;
                    }

                    // Resolve tolerance: try exact metric name, then category-based fallbacks
                    var defTol = ResolveTrackingTolerance(metricName, toleranceConfig, caseOverrides);
                    pass = ToleranceComparer.Passes(Convert.ToDouble(computedVal), goldenVal.GetDouble(), defTol, out maxAbsDelta, out maxRelDelta);
                    break;
            }

            deltas[metricName] = new DeltaValue { MaxAbsDelta = maxAbsDelta, MaxRelDelta = maxRelDelta, Passed = pass };
            if (!pass) allPass = false;
        }

        return new ResultEntry
        {
            CaseId = caseId,
            Status = allPass ? "PASS" : "FAIL",
            Deltas = deltas
        };
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
                if (!metricData.TryGetValue(kv.Key, out var list))
                    metricData[kv.Key] = list = new List<(DeltaValue, bool)>();
                // If case passed, every metric in it passed; otherwise use stored Passed flag
                list.Add((kv.Value, casePassed || kv.Value.Passed));
            }
        }

        // Resolve display unit and scale factor for each metric
        static (string unit, double scale) ResolveUnit(string metric) => metric switch
        {
            "final_x_km" or "final_y_km" or "final_z_km"
                or "position_x_km" or "position_y_km" or "position_z_km"
                                                                  => ("m",   1000.0),
            "final_vx_km_s" or "final_vy_km_s" or "final_vz_km_s"
                or "velocity_x_km_s" or "velocity_y_km_s" or "velocity_z_km_s"
                                                                  => ("m/s", 1000.0),
            "semi_major_axis_km"                                  => ("m",   1000.0),
            "eccentricity"                                        => ("-",   1.0),
            "inclination_deg" or "raan_deg" or "argp_deg" or "mean_anomaly_deg"
                                                                  => ("deg", 1.0),
            "penumbra_entry" or "umbra_entry"
                or "umbra_exit" or "penumbra_exit"                  => ("s",   1.0),
            "penumbra_duration_s" or "umbra_duration_s"             => ("s",   1.0),
            "attitude_quaternion"                                   => ("rad", 1.0),
            "target_in_fov"                                         => ("-",   1.0),
            _ when metric.StartsWith("arc_") && (metric.EndsWith("_rise") || metric.EndsWith("_fall"))
                                                                    => ("s",   1.0),
            "arc_count"                                             => ("-",   1.0),
            _ when metric.StartsWith("range_") && metric.EndsWith("_km")
                                                                    => ("km",  1.0),
            _ when metric.StartsWith("doppler_") && metric.EndsWith("_km_s")
                                                                    => ("km/s", 1.0),
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

    /// <summary>
    /// Resolves tolerance for tracking data metrics, mapping flattened metric names back
    /// to case override keys (e.g., "range_mean_km" → "range (km)").
    /// </summary>
    private static TolerancePair ResolveTrackingTolerance(
        string metricName,
        ToleranceConfig toleranceConfig,
        Dictionary<string, TolerancePair> caseOverrides)
    {
        // Try exact metric name first
        var tol = ToleranceComparer.ResolveTolerance(metricName, toleranceConfig?.Defaults, caseOverrides);
        if (tol.AbsTol > 1e-12 || tol.RelTol > 1e-12)
            return tol;

        // Map flattened names back to override keys — try multiple conventions
        if (metricName.StartsWith("range_") && metricName.EndsWith("_km"))
        {
            foreach (var key in new[] { "range (km)", "position_km" })
            {
                tol = ToleranceComparer.ResolveTolerance(key, toleranceConfig?.Defaults, caseOverrides);
                if (tol.AbsTol > 1e-12 || tol.RelTol > 1e-12)
                    return tol;
            }
        }
        else if (metricName.StartsWith("doppler_") && metricName.EndsWith("_km_s"))
        {
            foreach (var key in new[] { "doppler (km/s)", "velocity_km_s" })
            {
                tol = ToleranceComparer.ResolveTolerance(key, toleranceConfig?.Defaults, caseOverrides);
                if (tol.AbsTol > 1e-12 || tol.RelTol > 1e-12)
                    return tol;
            }
        }

        return tol;
    }

    /// <summary>
    /// Parses a time string, handling "UTC" suffix that the Time(string) constructor doesn't support.
    /// </summary>
    private static Time ParseTimeString(string timeString)
    {
        if (timeString.EndsWith(" UTC", StringComparison.OrdinalIgnoreCase))
        {
            var dateStr = timeString[..^4];
            var dt = DateTime.Parse(dateStr, System.Globalization.CultureInfo.InvariantCulture);
            return new Time(dt, TimeFrame.UTCFrame);
        }

        return new Time(timeString);
    }

    private static JsonElement FlattenTrackingDataOutputs(JsonElement outputs)
    {
        var flat = new Dictionary<string, JsonElement>();

        if (outputs.TryGetProperty("arcs", out var arcs))
        {
            int i = 0;
            foreach (var arc in arcs.EnumerateArray())
            {
                flat[$"arc_{i}_rise"] = arc.GetProperty("rise");
                flat[$"arc_{i}_fall"] = arc.GetProperty("fall");
                i++;
            }
            // Add arc_count as a number
            flat["arc_count"] = JsonDocument.Parse(i.ToString()).RootElement;
        }

        if (outputs.TryGetProperty("statistics", out var stats))
        {
            if (stats.TryGetProperty("range (km)", out var range))
            {
                foreach (var prop in range.EnumerateObject())
                    flat[$"range_{prop.Name}_km"] = prop.Value;
            }

            if (stats.TryGetProperty("doppler (km/s)", out var doppler))
            {
                foreach (var prop in doppler.EnumerateObject())
                    flat[$"doppler_{prop.Name}_km_s"] = prop.Value;
            }
        }

        // Serialize back to a JsonElement
        var json = JsonSerializer.Serialize(flat);
        return JsonDocument.Parse(json).RootElement;
    }

    private static double[] ParseQuaternionArray(JsonElement element)
    {
        var arr = new double[4];
        int i = 0;
        foreach (var item in element.EnumerateArray())
        {
            arr[i++] = item.GetDouble();
        }

        return arr;
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
