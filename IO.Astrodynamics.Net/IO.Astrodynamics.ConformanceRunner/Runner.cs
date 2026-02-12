using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            ["propagator"] = new PropagatorSolver(spiceKernelsPath)
        };
        _schemaValidator = new SchemaValidator(conformanceTestsPath);
    }

    public RunnerReport Run()
    {
        // Load SPICE kernels
        Console.WriteLine($"Loading SPICE kernels from: {_spiceKernelsPath}");
        API.Instance.LoadKernels(new DirectoryInfo(_spiceKernelsPath));

        // Load tolerances
        var toleranceConfig = LoadTolerances();

        // Get conformance tests git SHA
        var commitSha = GetGitSha(_conformanceTestsPath);

        // Discover cases
        var caseDirs = DiscoverCases();
        Console.WriteLine($"Discovered {caseDirs.Count} test case(s)");

        var report = new RunnerReport
        {
            ReportMeta = new ReportMeta
            {
                RunnerName = "IO.Astrodynamics.ConformanceRunner",
                RunnerVersion = "1.0.0",
                Framework = "IO.Astrodynamics.Net",
                FrameworkVersion = typeof(API).Assembly.GetName().Version?.ToString() ?? "unknown",
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
            Errors = errors
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

            var expected = JsonSerializer.Deserialize<ExpectedResult>(expectedJson);

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
                    var posTol = ToleranceComparer.ResolveTolerance("position_km", toleranceConfig?.Defaults, caseOverrides);
                    pass = ToleranceComparer.Passes(Convert.ToDouble(computedVal), goldenVal.GetDouble(), posTol, out maxAbsDelta, out maxRelDelta);
                    break;

                case "final_vx_km_s":
                case "final_vy_km_s":
                case "final_vz_km_s":
                    var velTol = ToleranceComparer.ResolveTolerance("velocity_km_s", toleranceConfig?.Defaults, caseOverrides);
                    pass = ToleranceComparer.Passes(Convert.ToDouble(computedVal), goldenVal.GetDouble(), velTol, out maxAbsDelta, out maxRelDelta);
                    break;

                default:
                    // Unknown metric — try numeric comparison
                    var defTol = ToleranceComparer.ResolveTolerance(metricName, toleranceConfig?.Defaults, caseOverrides);
                    pass = ToleranceComparer.Passes(Convert.ToDouble(computedVal), goldenVal.GetDouble(), defTol, out maxAbsDelta, out maxRelDelta);
                    break;
            }

            deltas[metricName] = new DeltaValue { MaxAbsDelta = maxAbsDelta, MaxRelDelta = maxRelDelta };
            if (!pass) allPass = false;
        }

        return new ResultEntry
        {
            CaseId = caseId,
            Status = allPass ? "PASS" : "FAIL",
            Deltas = deltas
        };
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
