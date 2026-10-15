// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The files <see cref="SpiceAPI.LoadKernels"/> hands to SPICE: the kernels only, so that the native loader never opens
/// the other files of a directory, such as the geopotential model it used to lock on Windows.
/// </summary>
public class KernelFilesTests : IDisposable
{
    private readonly DirectoryInfo _directory =
        Directory.CreateTempSubdirectory("kernel-files-");

    public void Dispose()
    {
        _directory.Delete(true);
    }

    [Fact]
    public void SolarSystem_AllFilesButTheGeopotentialModelAreKernels()
    {
        // Arrange: binary kernels (DAF/SPK, DAF/PCK, NAIF/DAF), text kernels with the KPL/ ID word, and earth_fixed.tf,
        // a text kernel without ID word
        var directory = Constants.SolarSystemKernelPath;

        // Act
        var kernels = SpiceAPI.KernelFiles(directory).Select(Path.GetFileName).ToArray();

        // Assert: the listing order, without the geopotential model
        var expected = Directory.EnumerateFileSystemEntries(directory.FullName).Select(Path.GetFileName)
            .Where(name => name != "EGM2008_to70_TideFree").ToArray();
        Assert.Equal(expected, kernels);
        Assert.Contains("earth_fixed.tf", kernels);
        Assert.Contains("earthstns_itrf93_201023.bsp", kernels);
        Assert.Equal(17, kernels.Length);
    }

    [Theory]
    [InlineData("DAF/SPK ", true)]
    [InlineData("DAS/EK  ", true)]
    [InlineData("NAIF/DAF", true)]
    [InlineData("NAIF/DAS", true)]
    [InlineData("KPL/FK\n", true)]
    [InlineData("KPL/MK\n", true)]
    [InlineData("  2    0  -0.484165143790815E-03\n", false)]
    [InlineData("", false)]
    [InlineData("A readme\n", false)]
    [InlineData("\\begintext\nComments only.\n", false)]
    [InlineData("Old text kernel\n\\begindata\nBODY399_RADII = ( 6378.1 6378.1 6356.8 )\n", true)]
    public void IsKernelFile_ByIdWordOrDataMarker(string content, bool expected)
    {
        // Arrange
        var file = Write("file", Encoding.ASCII.GetBytes(content));

        // Act and assert
        Assert.Equal(expected, SpiceAPI.IsKernelFile(file));
    }

    [Theory]
    [InlineData(65536 - 5)]
    [InlineData(65536)]
    [InlineData(3 * 65536 + 17)]
    public void IsKernelFile_FindsTheDataMarkerAfterLongComments_AcrossTheScanBuffers(int commentLength)
    {
        // Arrange: an old text kernel whose \begindata comes after comments longer than one scan buffer, the first case
        // putting the marker across two buffers
        var content = new string('c', commentLength) + "\\begindata\nX = 1\n";
        var file = Write("legacy.tk", Encoding.ASCII.GetBytes(content));
        var without = Write("comments.txt", Encoding.ASCII.GetBytes(new string('c', commentLength) + "\\begintext\n"));

        // Act and assert
        Assert.True(SpiceAPI.IsKernelFile(file));
        Assert.False(SpiceAPI.IsKernelFile(without));
    }

    [Fact]
    public void IsKernelFile_ReadsAFileHeldOpenForWriting()
    {
        // Arrange: another handle holds the file open for writing, sharing it
        var file = Write("held", Encoding.ASCII.GetBytes("KPL/FK\n"));
        using var holder = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        // Act and assert
        Assert.True(SpiceAPI.IsKernelFile(file));
    }

    [Fact]
    public void KernelFiles_WalksSubdirectories_AndHandsAFileAsItIs()
    {
        // Arrange
        var sub = _directory.CreateSubdirectory("sub");
        Write("readme.md", Encoding.ASCII.GetBytes("# Kernels\n"));
        var top = Write("top.tf", Encoding.ASCII.GetBytes("KPL/FK\n"));
        var nested = Path.Combine(sub.FullName, "nested.bsp");
        File.WriteAllBytes(nested, Encoding.ASCII.GetBytes("DAF/SPK "));
        var notAKernel = Write("model.txt", Encoding.ASCII.GetBytes("2 0 1.0\n"));

        // Act
        var kernels = SpiceAPI.KernelFiles(_directory).ToArray();

        // Assert: a file named explicitly is handed to SPICE, kernel or not
        Assert.Equal(new[] { nested, top }.OrderBy(x => x), kernels.OrderBy(x => x));
        Assert.Equal(new[] { notAKernel }, SpiceAPI.KernelFiles(new FileInfo(notAKernel)));
    }

    [Fact]
    public void KernelFiles_SkipsWhatIsNotARegularFile_AndFollowsLinksToKernels()
    {
        // Arrange: an empty file everywhere; on Linux and macOS, as the native loader saw them, a dangling symbolic link,
        // a cycle of links, a link to a kernel and a named pipe (H3 review of #384: the dangling link used to throw, the
        // pipe to block the read)
        var kernel = Write("kernel.tf", Encoding.ASCII.GetBytes("KPL/FK\n"));
        Write("empty", Array.Empty<byte>());
        var expected = new[] { kernel }.ToList();
        if (!OperatingSystem.IsWindows())
        {
            File.CreateSymbolicLink(Path.Combine(_directory.FullName, "dangling"), "missing");
            File.CreateSymbolicLink(Path.Combine(_directory.FullName, "cycle1"), "cycle2");
            File.CreateSymbolicLink(Path.Combine(_directory.FullName, "cycle2"), "cycle1");
            expected.Add(File.CreateSymbolicLink(Path.Combine(_directory.FullName, "link.tf"), kernel).FullName);
            using var mkfifo = System.Diagnostics.Process.Start("mkfifo", Path.Combine(_directory.FullName, "pipe"));
            mkfifo.WaitForExit();
            Assert.Equal(0, mkfifo.ExitCode);
        }

        // Act
        var kernels = SpiceAPI.KernelFiles(_directory).ToArray();

        // Assert
        Assert.Equal(expected.OrderBy(x => x), kernels.OrderBy(x => x));
    }

    [Fact]
    public void LoadKernels_OfADirectoryWithoutKernels_LoadsNothingAndRecordsIt()
    {
        // Arrange
        Write("model.txt", Encoding.ASCII.GetBytes("2 0 1.0\n"));

        // Act
        SpiceAPI.Instance.LoadKernels(_directory);

        // Assert
        Assert.Contains(SpiceAPI.Instance.GetLoadedKernels(), kernel => kernel.FullName == _directory.FullName);
        SpiceAPI.Instance.UnloadKernels(_directory);
        Assert.DoesNotContain(SpiceAPI.Instance.GetLoadedKernels(), kernel => kernel.FullName == _directory.FullName);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetLoadedKernels_WhileAnotherThreadLoadsKernels_GivesASnapshot()
    {
        // Arrange: a directory whose load and unload change the list of loaded kernels without touching SPICE
        Write("model.txt", Encoding.ASCII.GetBytes("2 0 1.0\n"));
        using var stop = new System.Threading.CancellationTokenSource();
        var loader = System.Threading.Tasks.Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                SpiceAPI.Instance.LoadKernels(_directory);
                SpiceAPI.Instance.UnloadKernels(_directory);
            }
        });

        // Act: enumerate the loaded kernels while the other thread changes them (the list itself was returned, outside
        // the lock, so the enumeration threw "Collection was modified"; Windows CI of #394)
        var exception = Record.Exception(() =>
        {
            var deadline = DateTime.UtcNow.AddSeconds(2.0);
            while (DateTime.UtcNow < deadline)
            {
                foreach (var kernel in SpiceAPI.Instance.GetLoadedKernels())
                {
                    _ = kernel.FullName;
                }
            }
        });
        stop.Cancel();
        await loader;

        // Assert
        Assert.Null(exception);
        Assert.DoesNotContain(SpiceAPI.Instance.GetLoadedKernels(), kernel => kernel.FullName == _directory.FullName);
    }

    private string Write(string name, byte[] content)
    {
        var path = Path.Combine(_directory.FullName, name);
        File.WriteAllBytes(path, content);
        return path;
    }
}
