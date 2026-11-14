// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;

namespace IO.Astrodynamics.CLI.Tests;

/// <summary>
/// The kernels path of a command is a directory, a kernel file or a meta-kernel (#673): before, every command wrapped it
/// in a directory, from which SPICE loaded nothing for a file.
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
    private static string Convert(string kernelsPath)
    {
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data"));
        var command = new FrameConverterCommand();
        var sb = new StringBuilder();
        Console.SetOut(new StringWriter(sb));
        command.Convert(kernelsPath, FrameName, "ICRF", new EpochParameters { Epoch = "2024-01-01T00:00:00Z" });
        return sb.ToString();
    }

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
                Assert.Contains(SpiceAPI.Instance.GetLoadedKernels(), k => k.FullName == file.FullName);
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(file);
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
                Assert.Contains(SpiceAPI.Instance.GetLoadedKernels(), k => k.FullName == metaKernel.FullName);
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(metaKernel);
            }
        }
    }
}
