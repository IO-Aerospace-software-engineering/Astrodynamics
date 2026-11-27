// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Globalization;
using System.Text.RegularExpressions;
using IO.Astrodynamics.CLI.Cli;

namespace IO.Astrodynamics.CLI.Tests;

/// <summary>
/// The kernels of a command are given by <c>-K</c>, repeatable, after those of <c>ASTRO_KERNELS</c> (R29 of #668), each a
/// directory, a kernel file or a meta-kernel (#673: before, every command wrapped it in a directory, from which SPICE loaded
/// nothing for a file); none, or a path that does not exist, is exit code 3.
/// </summary>
/// <remarks>
/// The frame IOA_F02_CLI (1999931, defined by no kernel of <c>Data</c>) is rotated by 90° about z from J2000
/// (TKFRAME_ANGLES (90, 0, 0) about the axes (3, 2, 1), NAIF, Frames Required Reading, "Defining a TK Frame Using Euler
/// Angles"): its quaternion to J2000 has |W| = |Z| = cos 45° and X = Y = 0, whatever the epoch.
/// </remarks>
public class KernelsPathTests : IDisposable
{
    private const string FrameName = "IOA_F02_CLI";
    private static readonly double Cos45 = System.Math.Cos(System.Math.PI / 4.0);

    // Under the working directory, so that the meta-kernel names it by a short relative path (80 characters at most per
    // string of a text kernel)
    private readonly DirectoryInfo _folder = Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "KernelsPath673"));

    public void Dispose()
    {
        _folder.Delete(true);
    }

    private FileInfo WriteFrameKernel()
    {
        var file = new FileInfo(Path.Combine(_folder.FullName, "ioa_f02_cli.tf"));
        File.WriteAllText(file.FullName,
            "KPL/FK\n\\begindata\nFRAME_IOA_F02_CLI = 1999931\nFRAME_1999931_NAME = 'IOA_F02_CLI'\nFRAME_1999931_CLASS = 4\n" +
            "FRAME_1999931_CLASS_ID = 1999931\nFRAME_1999931_CENTER = 399\nTKFRAME_1999931_RELATIVE = 'J2000'\n" +
            "TKFRAME_1999931_SPEC = 'ANGLES'\nTKFRAME_1999931_UNITS = 'DEGREES'\nTKFRAME_1999931_AXES = ( 3, 2, 1 )\n" +
            "TKFRAME_1999931_ANGLES = ( 90.0, 0.0, 0.0 )\n\\begintext\n");
        return file;
    }

    // Runs the frame converter on the kernels path, after the leapseconds of Data, which the UTC epoch needs
    private static string Convert(string kernelsPath) =>
        CliRun.Ok("frame-converter", FrameName, "ICRF", "-K", "Data", "-K", kernelsPath, "--epoch", "2024-01-01T00:00:00Z");

    private static void AssertRotationAboutZBy90Degrees(string output)
    {
        var match = Regex.Match(output, @"W = (\S+), VectorPart = X : (\S+) Y : (\S+) Z: (\S+) \}");
        Assert.True(match.Success, output);
        double Read(int group) => double.Parse(match.Groups[group].Value, CultureInfo.CurrentCulture);
        Assert.Equal(Cos45, System.Math.Abs(Read(1)), 1e-15);
        Assert.Equal(0.0, Read(2), 1e-15);
        Assert.Equal(0.0, Read(3), 1e-15);
        Assert.Equal(Cos45, System.Math.Abs(Read(4)), 1e-15);
    }

    [Fact]
    public void FrameConverter_WithAKernelFile_LoadsIt()
    {
        using (Configuration.Lock())
        {
            var file = WriteFrameKernel();
            try
            {
                var output = Convert(file.FullName);

                AssertRotationAboutZBy90Degrees(output);
                Assert.Contains(SpiceApi.Instance.GetLoadedKernels(), k => k.FullName == file.FullName);
            }
            finally
            {
                SpiceApi.Instance.UnloadKernels(file);
            }
        }
    }

    [Fact]
    public void AMissingKernelsPath_IsAKernelError()
    {
        // Before, a kernels path that does not exist loaded nothing, and the command failed later on the unknown frame
        using (Configuration.Lock())
        {
            var missing = Path.Combine(_folder.FullName, "missing");
            var error = CliRun.Fails(3, "frame-converter", FrameName, "ICRF", "-K", "Data", "-K", missing, "--epoch", "2024-01-01T00:00:00Z");
            Assert.Equal($"Error: -K: the kernel path {missing} does not exist.{Environment.NewLine}", error);
        }
    }

    [Fact]
    public void NoKernels_IsAKernelError()
    {
        using (Configuration.Lock())
        {
            var previous = Environment.GetEnvironmentVariable(CliKernels.EnvironmentVariable);
            Environment.SetEnvironmentVariable(CliKernels.EnvironmentVariable, null);
            try
            {
                var error = CliRun.Fails(3, "celestial-body-info", "399");
                Assert.Equal($"Error: No kernels: give -K <path> (a kernel, a meta-kernel or a directory), or set ASTRO_KERNELS.{Environment.NewLine}", error);
            }
            finally
            {
                Environment.SetEnvironmentVariable(CliKernels.EnvironmentVariable, previous);
            }
        }
    }

    /// <summary>
    /// The paths of ASTRO_KERNELS, separated by the path separator of the platform, are loaded before those of -K: the
    /// frame kernel of the variable with the leapseconds of Data given by -K, or both in the variable.
    /// </summary>
    [Fact]
    public void AstroKernels_AreLoadedBeforeTheKernelsOfK()
    {
        using (Configuration.Lock())
        {
            var file = WriteFrameKernel();
            var previous = Environment.GetEnvironmentVariable(CliKernels.EnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(CliKernels.EnvironmentVariable, file.FullName);
                AssertRotationAboutZBy90Degrees(CliRun.Ok("frame-converter", FrameName, "ICRF", "-K", "Data", "--epoch", "2024-01-01T00:00:00Z"));
                Assert.Equal(new[] { file.FullName, "Data" }, CliKernels.Paths(["Data"]));

                Environment.SetEnvironmentVariable(CliKernels.EnvironmentVariable, $"Data{Path.PathSeparator}{file.FullName}{Path.PathSeparator}");
                AssertRotationAboutZBy90Degrees(CliRun.Ok("frame-converter", FrameName, "ICRF", "--epoch", "2024-01-01T00:00:00Z"));
                Assert.Equal(new[] { "Data", file.FullName }, CliKernels.Paths([]));

                Environment.SetEnvironmentVariable(CliKernels.EnvironmentVariable, Path.Combine(_folder.FullName, "missing"));
                Assert.StartsWith("Error: ASTRO_KERNELS: the kernel path", CliRun.Fails(3, "celestial-body-info", "399", "-K", "Data"));
            }
            finally
            {
                Environment.SetEnvironmentVariable(CliKernels.EnvironmentVariable, previous);
                SpiceApi.Instance.UnloadKernels(file);
            }
        }
    }

    [Fact]
    public void FrameConverter_WithAMetaKernel_LoadsItsFiles()
    {
        using (Configuration.Lock())
        {
            var file = WriteFrameKernel();
            var metaKernel = new FileInfo(Path.Combine(_folder.FullName, "ioa_f02_cli.tm"));
            var relative = Path.GetRelativePath(Environment.CurrentDirectory, file.FullName);
            Assert.True(relative.Length <= 78, relative);
            File.WriteAllText(metaKernel.FullName, $"KPL/MK\n\\begindata\nKERNELS_TO_LOAD = ( '{relative}' )\n\\begintext\n");
            try
            {
                var output = Convert(metaKernel.FullName);

                AssertRotationAboutZBy90Degrees(output);
                Assert.Contains(SpiceApi.Instance.GetLoadedKernels(), k => k.FullName == metaKernel.FullName);
            }
            finally
            {
                SpiceApi.Instance.UnloadKernels(metaKernel);
            }
        }
    }
}
