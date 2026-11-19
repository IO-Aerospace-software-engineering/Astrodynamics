// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using Xunit;
using static IO.Astrodynamics.Tests.TestKernelWriter;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The kernel loading rules of 11.0.0 (R01 of #668, structuring choice 9, decided by S. Guillet, 2026-11-19): a repeated
/// <see cref="SpiceAPI.LoadKernels(FileSystemInfo)"/> furnishes only the files of its path that SPICE does not hold from a
/// load of this class and raises the generation of the kernel pool only when it hands SPICE a file;
/// <see cref="SpiceAPI.ReloadKernels(FileSystemInfo)"/> reads a path again, on top; the files of a folder load in ordinal
/// order, component by component, so that the later file wins on every platform.
/// </summary>
/// <remarks>
/// The kernels are TK frame kernels whose rotation is known in closed form: a frame rotated from J2000 by its angle about z
/// (TKFRAME_ANGLES (angle, 0, 0) about the axes (3, 2, 1), NAIF, Frames Required Reading, "Defining a TK Frame Using Euler
/// Angles") has that rotation angle from ICRF. Two text kernels that assign the same frame give it the angle of the one
/// SPICE read last: a text kernel's assignment replaces the value in the pool (NAIF, Kernel Required Reading, "Kernel
/// Precedence Rule"). The frame ids 1999989 to 1999999 are not defined by <c>Data/SolarSystem</c>
/// (<see cref="KernelShadowing"/>), and every kernel is unloaded in <see cref="Dispose"/>.
/// </remarks>
public class KernelLoadingTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("kernel-loading-");

    public KernelLoadingTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    public void Dispose()
    {
        foreach (var kernel in Under(_root))
        {
            SpiceAPI.Instance.UnloadKernels(kernel);
        }

        if (_root.Exists)
        {
            _root.Delete(true);
        }
    }

    private static FileSystemInfo[] Under(DirectoryInfo folder) => SpiceAPI.Instance.GetLoadedKernels()
        .Where(k => SpiceAPI.IsSameOrInside(k.FullName, folder.FullName))
        .ToArray();

    private static double AngleDeg(string frameName) => KernelRegistryTests.RotationAngleDeg(frameName);

    [Fact]
    public void LoadKernels_OfALoadedFile_FurnishesNothing_AndKeepsThePrecedence()
    {
        // Arrange: f gives the frame 10°, z gives it 20°
        var f = WriteFrameKernel(_root, "f.tf", 1999989, "IOA_L1_PREC", 10.0);
        var z = WriteFrameKernel(_root, "z.tf", 1999989, "IOA_L1_PREC", 20.0);
        lock (SpiceAPI.SyncRoot)
        {
            SpiceAPI.Instance.LoadKernels(f);
            SpiceAPI.Instance.LoadKernels(z);
            Assert.Equal(20.0, AngleDeg("IOA_L1_PREC"), 12);
            long generation = SpiceAPI.KernelPoolGeneration;

            // Act: f again
            SpiceAPI.Instance.LoadKernels(new FileInfo(f.FullName));

            // Assert: before 11.0.0 the load unloaded f and read it again, on top, so f's 10° won; and it raised the generation
            Assert.Equal(20.0, AngleDeg("IOA_L1_PREC"), 12);
            Assert.Equal(generation, SpiceAPI.KernelPoolGeneration);
        }

        Assert.Equal([f.FullName, z.FullName], Under(_root).Select(k => k.FullName));
    }

    [Fact]
    public void LoadKernels_OfALoadedFolder_FurnishesOnlyItsNewFiles()
    {
        // Arrange: the folder D holds a.tf (frame A at 10°), loaded; z.tf, outside it, gives A 30°; then b.tf (frame B at 40°)
        // is written into D
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "D"));
        WriteFrameKernel(folder, "a.tf", 1999990, "IOA_L1_NEW_A", 10.0);
        SpiceAPI.Instance.LoadKernels(folder);
        var z = WriteFrameKernel(_root, "z.tf", 1999990, "IOA_L1_NEW_A", 30.0);
        SpiceAPI.Instance.LoadKernels(z);
        WriteFrameKernel(folder, "b.tf", 1999991, "IOA_L1_NEW_B", 40.0);

        // Act
        SpiceAPI.Instance.LoadKernels(folder);

        // Assert: b.tf is read, a.tf is not read again (before 11.0.0 the folder was unloaded and read again, so A was 10°),
        // and the folder is listed once, at its place
        Assert.Equal(30.0, AngleDeg("IOA_L1_NEW_A"), 12);
        Assert.Equal(40.0, AngleDeg("IOA_L1_NEW_B"), 12);
        Assert.Equal([folder.FullName, z.FullName], Under(_root).Select(k => k.FullName));
    }

    [Fact]
    public void LoadKernels_OfAFileInsideALoadedFolder_FurnishesNothing_AndKeepsTheFolder()
    {
        // Arrange
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "In"));
        var a = WriteFrameKernel(folder, "a.tf", 1999992, "IOA_L1_IN", 10.0);
        SpiceAPI.Instance.LoadKernels(folder);
        var z = WriteFrameKernel(_root, "z.tf", 1999992, "IOA_L1_IN", 20.0);
        SpiceAPI.Instance.LoadKernels(z);
        lock (SpiceAPI.SyncRoot)
        {
            long generation = SpiceAPI.KernelPoolGeneration;

            // Act
            SpiceAPI.Instance.LoadKernels(a);

            // Assert: before 11.0.0 the folder was read again, on top (10°), and moved after z.tf
            Assert.Equal(20.0, AngleDeg("IOA_L1_IN"), 12);
            Assert.Equal(generation, SpiceAPI.KernelPoolGeneration);
        }

        Assert.Equal([folder.FullName, z.FullName], Under(_root).Select(k => k.FullName));
    }

    [Fact]
    public void LoadKernels_OfAFolderHoldingALoadedFile_FurnishesTheOthers_AndListsTheFolder()
    {
        // Arrange: a.tf of the folder loaded alone, then overridden by z.tf; b.tf beside it
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Holder"));
        var a = WriteFrameKernel(folder, "a.tf", 1999993, "IOA_L1_HOLD_A", 10.0);
        WriteFrameKernel(folder, "b.tf", 1999994, "IOA_L1_HOLD_B", 45.0);
        SpiceAPI.Instance.LoadKernels(a);
        var z = WriteFrameKernel(_root, "z.tf", 1999993, "IOA_L1_HOLD_A", 20.0);
        SpiceAPI.Instance.LoadKernels(z);

        // Act
        SpiceAPI.Instance.LoadKernels(folder);

        // Assert: a.tf is not read again (before 11.0.0 it was unloaded and the folder loaded, so A was 10°); the folder
        // replaces the entry of its file, listed last
        Assert.Equal(20.0, AngleDeg("IOA_L1_HOLD_A"), 12);
        Assert.Equal(45.0, AngleDeg("IOA_L1_HOLD_B"), 12);
        Assert.Equal([z.FullName, folder.FullName], Under(_root).Select(k => k.FullName));

        // Act: the folder unloads both of its files
        SpiceAPI.Instance.UnloadKernels(folder);

        // Assert
        Assert.Equal(20.0, AngleDeg("IOA_L1_HOLD_A"), 12);
        Assert.Throws<InvalidOperationException>(() => AngleDeg("IOA_L1_HOLD_B"));
    }

    [Fact]
    public void LoadKernels_OfAMissingPath_DoesNotRaiseTheGeneration()
    {
        // Arrange
        var missing = new FileInfo(Path.Combine(_root.FullName, "missing.tf"));
        lock (SpiceAPI.SyncRoot)
        {
            long generation = SpiceAPI.KernelPoolGeneration;

            // Act
            Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.LoadKernels(missing));

            // Assert: before 11.0.0 every call raised it, failed or not, so the predefined bodies read their data again
            Assert.Equal(generation, SpiceAPI.KernelPoolGeneration);
        }
    }

    [Fact]
    public void LoadKernels_OfAKernel_RaisesTheGeneration()
    {
        // Arrange
        var f = WriteFrameKernel(_root, "g.tf", 1999995, "IOA_L1_GEN", 10.0);
        lock (SpiceAPI.SyncRoot)
        {
            long generation = SpiceAPI.KernelPoolGeneration;

            // Act
            SpiceAPI.Instance.LoadKernels(f);

            // Assert
            Assert.Equal(generation + 1, SpiceAPI.KernelPoolGeneration);
        }
    }

    [Fact]
    public void LoadKernels_OfAFolder_LoadsItsFilesInOrdinalOrderComponentByComponent()
    {
        // Arrange: twenty files k00 to k19 that each give the frame its index in degrees, written in their ordinal order (the
        // order a tmpfs lists in reverse, ext4 in the order of its hash); x/y.tf gives 30°, x0.tf 31°: x sorts before x0.tf
        // component by component, whereas the whole paths sort x0.tf first where the separator is '\' (0x5C) and x/y.tf first
        // where it is '/' (0x2F); B.tf (32°) sorts before a.tf (33°), capitals first
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Order"));
        for (int i = 0; i < 20; i++)
        {
            WriteFrameKernel(folder, $"k{i:00}.tf", 1999996, "IOA_L1_ORDER", i);
        }

        var sub = new DirectoryInfo(Path.Combine(folder.FullName, "x"));
        WriteFrameKernel(sub, "y.tf", 1999997, "IOA_L1_ORDER_X", 30.0);
        WriteFrameKernel(folder, "x0.tf", 1999997, "IOA_L1_ORDER_X", 31.0);
        WriteFrameKernel(folder, "a.tf", 1999998, "IOA_L1_ORDER_CASE", 33.0);
        WriteFrameKernel(folder, "B.tf", 1999998, "IOA_L1_ORDER_CASE", 32.0);

        // Act
        var files = SpiceAPI.KernelFiles(folder).Select(x => Path.GetRelativePath(folder.FullName, x)).ToArray();
        SpiceAPI.Instance.LoadKernels(folder);

        // Assert: the later file wins (before 11.0.0 the files loaded in the order of the directory listing)
        string[] expected = ["B.tf", "a.tf", .. Enumerable.Range(0, 20).Select(i => $"k{i:00}.tf"), Path.Combine("x", "y.tf"), "x0.tf"];
        Assert.Equal(expected, files);
        Assert.Equal(19.0, AngleDeg("IOA_L1_ORDER"), 12);
        Assert.Equal(31.0, AngleDeg("IOA_L1_ORDER_X"), 12);
        Assert.Equal(33.0, AngleDeg("IOA_L1_ORDER_CASE"), 12);
    }

    [Fact]
    public void LoadKernels_OfAFolderWhoseLaterFileFails_ListsTheFilesItFurnished()
    {
        // Arrange: a.tf is a frame kernel, b.tf a text kernel SPICE cannot read: a variable name of more than 32 characters
        // (Kernel Required Reading, "Variable Name Rules"; SPICE(BADVARNAME))
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Fails"));
        WriteFrameKernel(folder, "a.tf", 1999999, "IOA_L1_FAIL", 10.0);
        var b = new FileInfo(Path.Combine(folder.FullName, "b.tf"));
        File.WriteAllText(b.FullName, "KPL/FK\n\\begindata\nIOA_L1_FAIL_A_NAME_LONGER_THAN_32_CHARACTERS = 1\n\\begintext\n");

        // Act
        Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.LoadKernels(folder));

        // Assert: SPICE holds a.tf, so the folder is listed, to be unloaded (before 11.0.0 a failed load listed nothing, and
        // a later load furnished a.tf a second time)
        Assert.Equal(10.0, AngleDeg("IOA_L1_FAIL"), 12);
        Assert.Equal([folder.FullName], Under(_root).Select(k => k.FullName));

        // Act: b.tf repaired, the folder loaded again
        File.WriteAllText(b.FullName, "KPL/FK\n\\begindata\nIOA_L1_FAIL_SHORT = 1\n\\begintext\n");
        SpiceAPI.Instance.LoadKernels(folder);
        SpiceAPI.Instance.UnloadKernels(folder);

        // Assert: one unload removes a.tf (a second load of it would have needed a second unload)
        Assert.Throws<InvalidOperationException>(() => AngleDeg("IOA_L1_FAIL"));
        Assert.Empty(Under(_root));
    }

    [Fact]
    public void UnloadKernels_OfNull_Throws()
    {
        // Before 11.0.0 it returned, while LoadKernels(null) threw
        var e = Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.UnloadKernels((FileSystemInfo)null));
        Assert.Equal("path", e.ParamName);
    }

    [Fact]
    [Trait("Category", "Validation")]
    public void LoadKernels_Again_HandsSpiceNothing_KeeperList()
    {
        // KEEPER's list of the files SPICE holds, in load order (CspiceDirect, ktotal_c and kdata_c, Linux): f, z, then f
        // again adds no file and keeps f before z
        if (!OperatingSystem.IsLinux()) return;
        var f = WriteFrameKernel(_root, "kf.tf", 1999989, "IOA_L1_PREC", 10.0);
        var z = WriteFrameKernel(_root, "kz.tf", 1999989, "IOA_L1_PREC", 20.0);
        lock (SpiceAPI.SyncRoot)
        {
            SpiceAPI.Instance.LoadKernels(f);
            SpiceAPI.Instance.LoadKernels(z);
            var before = Body.CspiceDirect.LoadedKernelFiles();

            // Act
            SpiceAPI.Instance.LoadKernels(f);

            // Assert: before 11.0.0 KEEPER listed z, then f
            var after = Body.CspiceDirect.LoadedKernelFiles();
            Assert.Equal(before, after);
            Assert.True(after.IndexOf(f.FullName) < after.IndexOf(z.FullName));
        }
    }

    [Fact]
    public void ReloadKernels_OfALoadedFile_PutsItBackOnTop()
    {
        // Arrange
        var f = WriteFrameKernel(_root, "f.tf", 1999989, "IOA_L1_PREC", 10.0);
        var z = WriteFrameKernel(_root, "z.tf", 1999989, "IOA_L1_PREC", 20.0);
        SpiceAPI.Instance.LoadKernels(f);
        SpiceAPI.Instance.LoadKernels(z);
        lock (SpiceAPI.SyncRoot)
        {
            long generation = SpiceAPI.KernelPoolGeneration;

            // Act
            SpiceAPI.Instance.ReloadKernels(f);

            // Assert: what a repeated LoadKernels did before 11.0.0
            Assert.Equal(10.0, AngleDeg("IOA_L1_PREC"), 12);
            Assert.True(SpiceAPI.KernelPoolGeneration > generation);
        }

        Assert.Equal([z.FullName, f.FullName], Under(_root).Select(k => k.FullName));
    }

    [Fact]
    public void ReloadKernels_OfARewrittenFile_ReadsItsNewContent()
    {
        // Arrange: f gives the frame 10°, then is rewritten at 50°
        var f = WriteFrameKernel(_root, "f.tf", 1999990, "IOA_L1_REWRITE", 10.0);
        SpiceAPI.Instance.LoadKernels(f);
        WriteFrameKernel(_root, "f.tf", 1999990, "IOA_L1_REWRITE", 50.0);

        // Act and assert: a load reads nothing, a reload the new content
        SpiceAPI.Instance.LoadKernels(f);
        Assert.Equal(10.0, AngleDeg("IOA_L1_REWRITE"), 12);
        SpiceAPI.Instance.ReloadKernels(f);
        Assert.Equal(50.0, AngleDeg("IOA_L1_REWRITE"), 12);
        Assert.Equal([f.FullName], Under(_root).Select(k => k.FullName));
    }

    [Fact]
    public void ReloadKernels_OfAFileInsideALoadedFolder_ReadsItAgain_AndKeepsTheFolder()
    {
        // Arrange: the folder D of a.tf (A at 10°) and b.tf (B at 20°), loaded, then z.tf; a.tf rewritten at 60°
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "D"));
        var a = WriteFrameKernel(folder, "a.tf", 1999991, "IOA_L1_RIN_A", 10.0);
        WriteFrameKernel(folder, "b.tf", 1999992, "IOA_L1_RIN_B", 20.0);
        SpiceAPI.Instance.LoadKernels(folder);
        var z = WriteFrameKernel(_root, "z.tf", 1999992, "IOA_L1_RIN_B", 30.0);
        SpiceAPI.Instance.LoadKernels(z);
        WriteFrameKernel(folder, "a.tf", 1999991, "IOA_L1_RIN_A", 60.0);

        // Act
        SpiceAPI.Instance.ReloadKernels(a);

        // Assert: a.tf alone is read again, b.tf keeps its place under z.tf, and the folder stays listed at its place
        Assert.Equal(60.0, AngleDeg("IOA_L1_RIN_A"), 12);
        Assert.Equal(30.0, AngleDeg("IOA_L1_RIN_B"), 12);
        Assert.Equal([folder.FullName, z.FullName], Under(_root).Select(k => k.FullName));

        // Act: the folder unloads both of its files
        SpiceAPI.Instance.UnloadKernels(folder);

        // Assert
        Assert.Throws<InvalidOperationException>(() => AngleDeg("IOA_L1_RIN_A"));
        Assert.Equal(30.0, AngleDeg("IOA_L1_RIN_B"), 12);
    }

    [Fact]
    public void ReloadKernels_OfAPathNeverLoaded_LoadsIt()
    {
        var f = WriteFrameKernel(_root, "n.tf", 1999993, "IOA_L1_NEVER", 15.0);

        SpiceAPI.Instance.ReloadKernels(f);

        Assert.Equal(15.0, AngleDeg("IOA_L1_NEVER"), 12);
        Assert.Equal([f.FullName], Under(_root).Select(k => k.FullName));
    }

    [Fact]
    public void ReloadKernels_OfAMissingPath_Throws_AndUnloadsNothing()
    {
        // Arrange
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Kept"));
        WriteFrameKernel(folder, "a.tf", 1999994, "IOA_L1_MISSING", 15.0);
        SpiceAPI.Instance.LoadKernels(folder);
        var missing = new FileInfo(Path.Combine(folder.FullName, "missing.tf"));

        // Act
        var e = Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.ReloadKernels(missing));

        // Assert
        Assert.Equal("path", e.ParamName);
        Assert.Equal(15.0, AngleDeg("IOA_L1_MISSING"), 12);
        Assert.Equal([folder.FullName], Under(_root).Select(k => k.FullName));
    }

    [Fact]
    public void KernelCalls_ByString_ActAsByFileSystemInfo()
    {
        // Arrange: a folder and a file, named by strings
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Text"));
        WriteFrameKernel(folder, "a.tf", 1999995, "IOA_L1_STR_A", 10.0);
        var file = WriteFrameKernel(_root, "b.tf", 1999996, "IOA_L1_STR_B", 20.0);

        // Act
        SpiceAPI.Instance.LoadKernels(folder.FullName);
        SpiceAPI.Instance.LoadKernels(file.FullName);

        // Assert: a directory is listed as a DirectoryInfo, a file as a FileInfo
        var listed = Under(_root);
        Assert.Equal([folder.FullName, file.FullName], listed.Select(k => k.FullName));
        Assert.IsType<DirectoryInfo>(listed[0]);
        Assert.IsType<FileInfo>(listed[1]);
        Assert.Equal(10.0, AngleDeg("IOA_L1_STR_A"), 12);

        // Act: rewritten and reloaded by string, then unloaded by string
        WriteFrameKernel(_root, "b.tf", 1999996, "IOA_L1_STR_B", 25.0);
        SpiceAPI.Instance.ReloadKernels(file.FullName);
        Assert.Equal(25.0, AngleDeg("IOA_L1_STR_B"), 12);
        SpiceAPI.Instance.UnloadKernels(folder.FullName);
        SpiceAPI.Instance.UnloadKernels(file.FullName);

        // Assert
        Assert.Empty(Under(_root));
        Assert.Throws<InvalidOperationException>(() => AngleDeg("IOA_L1_STR_A"));
        Assert.Throws<InvalidOperationException>(() => AngleDeg("IOA_L1_STR_B"));
    }

    [Fact]
    public void KernelCalls_ByString_RefuseNullEmptyAndMissing()
    {
        var missing = Path.Combine(_root.FullName, "missing");
        Assert.Equal("path", Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.LoadKernels((string)null)).ParamName);
        Assert.Equal("path", Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.ReloadKernels((string)null)).ParamName);
        Assert.Equal("path", Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.UnloadKernels((string)null)).ParamName);
        Assert.Equal("path", Assert.Throws<ArgumentNullException>(() => SpiceAPI.Instance.ReloadKernels((FileSystemInfo)null)).ParamName);
        Assert.Equal("path", Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.LoadKernels(string.Empty)).ParamName);
        Assert.Equal("path", Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.LoadKernels(missing)).ParamName);
        Assert.Equal("path", Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.ReloadKernels(missing)).ParamName);
    }
}
