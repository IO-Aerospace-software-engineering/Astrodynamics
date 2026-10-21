// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using Xunit;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The data files a case takes from the conformance suite, which keeps them in Git LFS (#425): a file left as an LFS
/// pointer makes the case an ERROR naming the file and the command, instead of a FAIL.
/// </summary>
public sealed class SuiteDataFileTests : IDisposable
{
    private const string KernelsPath = "Data/SolarSystem";
    private const string Pointer = SuiteDataFile.LfsPointerHeader + "v1\noid sha256:5e3e4188e4\nsize 7658496\n";

    private readonly string _directory;

    public SuiteDataFileTests()
    {
        _directory = Directory.CreateTempSubdirectory("suite-data-").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void GitLfsPointer_Throws_NamingTheFileAndTheCommand()
    {
        // Arrange
        var path = Path.Combine(_directory, "orbit.bsp");
        File.WriteAllText(path, Pointer);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => SuiteDataFile.EnsureContent(path));

        // Assert
        Assert.Contains(path, exception.Message);
        Assert.Contains("git lfs install && git lfs pull", exception.Message);
    }

    [Fact]
    public void MissingFile_Throws()
    {
        var path = Path.Combine(_directory, "missing.bsp");
        var exception = Assert.Throws<InvalidOperationException>(() => SuiteDataFile.EnsureContent(path));
        Assert.Contains(path, exception.Message);
    }

    [Fact]
    public void Kernel_AndFileShorterThanTheHeader_Pass()
    {
        // Arrange: a binary kernel, a text kernel, the geopotential model and a file of a few bytes
        var shortFile = Path.Combine(_directory, "short.txt");
        File.WriteAllText(shortFile, "version");

        // Act and assert: nothing thrown
        SuiteDataFile.EnsureContent(Path.Combine(KernelsPath, "de440s.bsp"));
        SuiteDataFile.EnsureContent(Path.Combine(KernelsPath, "latest_leapseconds.tls"));
        SuiteDataFile.EnsureContent(Path.Combine(KernelsPath, "EGM2008_to70_TideFree"));
        SuiteDataFile.EnsureContent(shortFile);
    }

    [Fact]
    public void TrackingCaseWithItsKernelLeftAsAGitLfsPointer_IsAnError()
    {
        // Arrange: a suite with the schemas of the fixture and one tracking case, whose kernel is an LFS pointer
        Directory.CreateDirectory(Path.Combine(_directory, "schemas", "v1"));
        foreach (var schema in Directory.GetFiles(Path.Combine("Fixture", "schemas", "v1")))
        {
            File.Copy(schema, Path.Combine(_directory, "schemas", "v1", Path.GetFileName(schema)));
        }

        File.Copy(Path.Combine("Fixture", "tolerances.yaml"), Path.Combine(_directory, "tolerances.yaml"));
        Directory.CreateDirectory(Path.Combine(_directory, "data"));
        var kernel = Path.Combine(_directory, "data", "orbit.bsp");
        File.WriteAllText(kernel, Pointer);
        var caseDirectory = Path.Combine(_directory, "cases", "tracking_data", "001");
        Directory.CreateDirectory(caseDirectory);
        File.WriteAllText(Path.Combine(caseDirectory, "inputs.yaml"), """
                                                                      id: "tracking_data_lfs_pointer"
                                                                      schema_version: "v1"
                                                                      version: "0.1.0"
                                                                      category: "tracking_data"
                                                                      created: "2026-10-21"
                                                                      description: "A tracking case whose kernel is a Git LFS pointer."
                                                                      metadata:
                                                                        ephemeris_kernel: "de440s.bsp"
                                                                        reference_frame: "ICRF"
                                                                        time_scale: "TDB"
                                                                      inputs:
                                                                        epoch: "2023-12-15T00:00:00.000 UTC"
                                                                        orbit:
                                                                          type: "bsp_source"
                                                                          naif_id: -85
                                                                          filename: data/orbit.bsp
                                                                          aberration: null
                                                                        search_window:
                                                                          start: "2023-12-15T00:00:00.000 UTC"
                                                                          end: "2023-12-15T12:00:00.000 UTC"
                                                                          step_size_s: 60
                                                                        occulting_body: "Moon"
                                                                        location:
                                                                          reference_frame: ITRF93
                                                                          latitude_deg: -29.0
                                                                          longitude_deg: 115.3
                                                                          height_km: 0.25
                                                                      """);
        File.WriteAllText(Path.Combine(caseDirectory, "expected-result.json"), """
            {
              "id": "tracking_data_lfs_pointer",
              "category": "tracking_data",
              "case_version": "0.1.0",
              "generated_by": { "tool": "none", "tool_version": "0", "kernel": "de440s.bsp", "date": "2026-10-21" },
              "outputs": {
                "arcs": [ { "rise": "2023-12-15T00:25:00 UTC", "fall": "2023-12-15T01:36:00 UTC" } ],
                "statistics": { "range (km)": { "mean": 368000.0 } }
              }
            }
            """);

        // Act
        var report = new Runner(_directory, KernelsPath).Run();

        // Assert: an ERROR naming the kernel and the command, counted among the errors, and the pointer never handed
        // to SPICE
        var result = report.Results.Single();
        Assert.Equal("ERROR", result.Status);
        Assert.Contains(Path.Combine(_directory, "data/orbit.bsp"), result.Message);
        Assert.Contains("git lfs pull", result.Message);
        Assert.Equal(1, report.Summary.Errors);
        Assert.Equal(0, report.Summary.Failed);
        Assert.DoesNotContain(SpiceAPI.Instance.GetLoadedKernels(), k => k.FullName == new FileInfo(kernel).FullName);
    }
}
