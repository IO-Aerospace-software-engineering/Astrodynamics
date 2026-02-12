using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Solvers;

namespace IO.Astrodynamics.ConformanceRunner;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length >= 1 && args[0] == "--smoke-test")
        {
            return RunSmokeTest(args.Length > 1 ? args[1] : null);
        }

        if (args.Length < 2)
        {
            Console.WriteLine("Usage: conformance-runner <conformance-tests-path> <spice-kernels-path> [output-report.json]");
            Console.WriteLine("       conformance-runner --smoke-test <spice-kernels-path>");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  conformance-tests-path  Path to the conformance-tests repository");
            Console.WriteLine("  spice-kernels-path      Path to SPICE kernel files directory");
            Console.WriteLine("  output-report.json      Output report file (default: conformance-report.json)");
            return 1;
        }

        var conformanceTestsPath = args[0];
        var spiceKernelsPath = args[1];
        var outputPath = args.Length > 2 ? args[2] : "conformance-report.json";

        if (!Directory.Exists(conformanceTestsPath))
        {
            Console.Error.WriteLine($"Error: Conformance tests directory not found: {conformanceTestsPath}");
            return 1;
        }

        if (!Directory.Exists(spiceKernelsPath))
        {
            Console.Error.WriteLine($"Error: SPICE kernels directory not found: {spiceKernelsPath}");
            return 1;
        }

        Console.WriteLine("IO.Astrodynamics Conformance Test Runner");
        Console.WriteLine("========================================");
        Console.WriteLine();

        var runner = new Runner(conformanceTestsPath, spiceKernelsPath);
        var report = runner.Run();

        // Write report
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        var reportJson = JsonSerializer.Serialize(report, options);
        File.WriteAllText(outputPath, reportJson);

        // Print summary
        Console.WriteLine();
        Console.WriteLine("Summary");
        Console.WriteLine("-------");
        Console.WriteLine($"  Total:   {report.Summary.Total}");
        Console.WriteLine($"  Passed:  {report.Summary.Passed}");
        Console.WriteLine($"  Failed:  {report.Summary.Failed}");
        Console.WriteLine($"  Skipped: {report.Summary.Skipped}");
        Console.WriteLine($"  Errors:  {report.Summary.Errors}");
        Console.WriteLine();
        Console.WriteLine($"Report written to: {outputPath}");

        return report.Summary.Failed + report.Summary.Errors;
    }

    private static int RunSmokeTest(string kernelsPath)
    {
        kernelsPath ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "SolarSystem");
        if (!Directory.Exists(kernelsPath))
        {
            Console.Error.WriteLine($"Error: SPICE kernels directory not found: {kernelsPath}");
            return 1;
        }

        Console.WriteLine("Smoke Test — exercising solvers without golden values");
        Console.WriteLine("=====================================================");
        Console.WriteLine();

        API.Instance.LoadKernels(new DirectoryInfo(kernelsPath));

        try
        {
            // TRIAD solver
            Console.Write("TRIAD solver... ");
            var triadInput = new CaseInput
            {
                Id = "smoke_triad",
                Category = "pointing_triad",
                Metadata = new CaseMetadata { ReferenceFrame = "ICRF", TimeScale = "TDB", EphemerisKernel = "de440s.bsp" },
                Inputs = new Dictionary<string, object>
                {
                    ["epoch"] = "2026-01-15T12:00:00.000 TDB",
                    ["orbit"] = new Dictionary<object, object>
                    {
                        ["type"] = "keplerian", ["a_km"] = 7000.0, ["e"] = 0.001,
                        ["i_deg"] = 28.5, ["raan_deg"] = 45.0, ["argp_deg"] = 0.0, ["ma_deg"] = 0.0
                    },
                    ["primary_target"] = "Moon",
                    ["secondary_target"] = "Sun",
                    ["primary_body_vector"] = new List<object> { 0.0, 0.0, 1.0 },
                    ["secondary_body_vector"] = new List<object> { 0.0, 1.0, 0.0 },
                    ["field_of_view"] = new Dictionary<object, object>
                    {
                        ["half_angle_deg"] = 15.0,
                        ["axis_body"] = new List<object> { 0.0, 0.0, 1.0 }
                    }
                }
            };
            var triadResult = new TriadSolver().Solve(triadInput);
            var q = (double[])triadResult["attitude_quaternion"];
            Console.WriteLine($"OK  q=[{q[0]:R}, {q[1]:R}, {q[2]:R}, {q[3]:R}]  fov={triadResult["target_in_fov"]}");

            // Eclipse solver
            Console.Write("Eclipse solver... ");
            var eclipseInput = new CaseInput
            {
                Id = "smoke_eclipse",
                Category = "eclipse",
                Metadata = new CaseMetadata { ReferenceFrame = "ICRF", TimeScale = "TDB", EphemerisKernel = "de440s.bsp" },
                Inputs = new Dictionary<string, object>
                {
                    ["epoch"] = "2026-03-20T00:00:00.000 TDB",
                    ["orbit"] = new Dictionary<object, object>
                    {
                        ["type"] = "keplerian", ["a_km"] = 7000.0, ["e"] = 0.001,
                        ["i_deg"] = 28.5, ["raan_deg"] = 45.0, ["argp_deg"] = 0.0, ["ma_deg"] = 0.0
                    },
                    ["search_window"] = new Dictionary<object, object>
                    {
                        ["start"] = "2026-03-20T00:00:00.000 TDB",
                        ["end"] = "2026-03-20T12:00:00.000 TDB"
                    },
                    ["occulting_body"] = "Earth",
                    ["light_source"] = "Sun"
                }
            };
            var eclipseResult = new EclipseSolver().Solve(eclipseInput);
            Console.WriteLine($"OK  penumbra=[{eclipseResult["penumbra_entry"]} → {eclipseResult["penumbra_exit"]}]  umbra=[{eclipseResult["umbra_entry"]} → {eclipseResult["umbra_exit"]}]");
            Console.WriteLine($"          penumbra_dur={eclipseResult["penumbra_duration_s"]}s  umbra_dur={eclipseResult["umbra_duration_s"]}s");

            // Propagator solver
            Console.Write("Propagator solver... ");
            var propagatorInput = new CaseInput
            {
                Id = "smoke_propagator",
                Category = "propagator",
                Metadata = new CaseMetadata { ReferenceFrame = "ICRF", TimeScale = "UTC", EphemerisKernel = "de440s.bsp" },
                Inputs = new Dictionary<string, object>
                {
                    ["epoch"] = "2025-08-25T11:55:44.000 UTC",
                    ["orbit"] = new Dictionary<object, object>
                    {
                        ["type"] = "state_vector",
                        ["position_km"] = new List<object> { 5442.1625926801835, -4068.9498468206248, -13.456851447751518 },
                        ["velocity_km_s"] = new List<object> { 2.8581975428173836, 3.8097859312745794, 6.0021266931226886 }
                    },
                    ["propagation_window"] = new Dictionary<object, object>
                    {
                        ["start"] = "2025-08-25T11:55:44.000 UTC",
                        ["end"] = "2025-08-26T11:55:44.000 UTC"
                    },
                    ["central_body"] = "Earth",
                    ["geopotential_degree"] = 10,
                    ["geopotential_model"] = "EGM2008_to70_TideFree",
                    ["perturbation_bodies"] = new List<object> { "Moon", "Sun" },
                    ["force_model"] = new Dictionary<object, object>
                    {
                        ["drag"] = false,
                        ["srp"] = false
                    },
                    ["step_size_s"] = 1.0
                }
            };
            var propagatorResult = new PropagatorSolver(kernelsPath).Solve(propagatorInput);
            Console.WriteLine($"OK  final_pos_km=({propagatorResult["final_x_km"]:F6}, {propagatorResult["final_y_km"]:F6}, {propagatorResult["final_z_km"]:F6})");

            Console.WriteLine();
            Console.WriteLine("All smoke tests PASSED!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex}");
            return 1;
        }
    }
}
