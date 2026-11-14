// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Tests.Body;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// What <see cref="SpiceAPI.LoadKernels"/> and <see cref="SpiceAPI.UnloadKernels"/> hand to SPICE, and what
/// <see cref="SpiceAPI.GetLoadedKernels"/> lists, on folders of TK frame kernels whose rotation is known in closed form
/// (#673): paths compared by components, not by substring, and an unload that removes what the load handed SPICE, whether
/// the path still exists or not.
/// </summary>
/// <remarks>
/// Each frame is rotated from J2000 by its angle about z (TKFRAME_ANGLES (angle, 0, 0) about the axes (3, 2, 1), NAIF,
/// Frames Required Reading, "Defining a TK Frame Using Euler Angles"), read back by SPICE through
/// <see cref="SpiceAPI.TransformFrame(TimeSystem.Time, Frames.Frame, Frames.Frame)"/>, which throws once the frame is out of the pool. The kernels are
/// text kernels, which SPICE closes after their load, so a loaded folder can be deleted on every platform. The frame ids
/// 1999901 and up are not defined by <c>Data/SolarSystem</c> (<see cref="KernelShadowing"/>), and every kernel is unloaded
/// in <see cref="Dispose"/>.
/// </remarks>
public class KernelRegistryTests : IDisposable
{
    private static readonly TimeSystem.Time Epoch = new(2024, 1, 1);
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("kernel-registry-");

    public KernelRegistryTests()
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

    // The loaded entries under a folder, whose own entry included
    private static FileSystemInfo[] Under(DirectoryInfo folder) => SpiceAPI.Instance.GetLoadedKernels()
        .Where(k => k.FullName == folder.FullName || k.FullName.StartsWith(folder.FullName + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        .ToArray();

    // A TK frame kernel rotated from J2000 by angleDeg about z
    internal static FileInfo WriteFrameKernel(DirectoryInfo folder, string fileName, int id, string frameName, double angleDeg)
    {
        folder.Create();
        var file = new FileInfo(Path.Combine(folder.FullName, fileName));
        File.WriteAllText(file.FullName,
            $"KPL/FK\n\\begindata\nFRAME_{frameName} = {id}\nFRAME_{id}_NAME = '{frameName}'\nFRAME_{id}_CLASS = 4\n" +
            $"FRAME_{id}_CLASS_ID = {id}\nFRAME_{id}_CENTER = 399\nTKFRAME_{id}_RELATIVE = 'J2000'\nTKFRAME_{id}_SPEC = 'ANGLES'\n" +
            $"TKFRAME_{id}_UNITS = 'DEGREES'\nTKFRAME_{id}_AXES = ( 3, 2, 1 )\n" +
            $"TKFRAME_{id}_ANGLES = ( {angleDeg.ToString("F1", CultureInfo.InvariantCulture)}, 0.0, 0.0 )\n\\begintext\n");
        return file;
    }

    // The rotation angle of the frame from ICRF, in degrees (the sign of a quaternion is free); throws once SPICE no longer
    // knows the frame
    internal static double RotationAngleDeg(string frameName)
    {
        var q = SpiceAPI.Instance.TransformFrame(Epoch, new Frames.Frame(frameName), Frames.Frame.ICRF).Rotation;
        return 2.0 * System.Math.Acos(System.Math.Min(1.0, System.Math.Abs(q.W))) * Astrodynamics.Constants.Rad2Deg;
    }

    private static void AssertUnknown(string frameName)
    {
        Assert.Throws<InvalidOperationException>(() => RotationAngleDeg(frameName));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LoadKernels_SiblingWhoseNameExtendsALoadedFolder_LoadsBoth(bool shorterFirst)
    {
        // Arrange: Frames holds a 90° frame, Frames2, whose name extends Frames, a 45° frame
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Frames"));
        var sibling = new DirectoryInfo(Path.Combine(_root.FullName, "Frames2"));
        WriteFrameKernel(folder, "a.tf", 1999901, "IOA_F02_SIB_A", 90.0);
        WriteFrameKernel(sibling, "b.tf", 1999902, "IOA_F02_SIB_B", 45.0);

        // Act
        foreach (var path in shorterFirst ? new[] { folder, sibling } : new[] { sibling, folder })
        {
            SpiceAPI.Instance.LoadKernels(new DirectoryInfo(path.FullName));
        }

        // Assert: before #673 the later load reloaded or unloaded the other folder, whose frame SPICE no longer knew
        Assert.Equal(90.0, RotationAngleDeg("IOA_F02_SIB_A"), 12);
        Assert.Equal(45.0, RotationAngleDeg("IOA_F02_SIB_B"), 12);
        var listed = Under(_root).Select(k => k.FullName).ToArray();
        Assert.Equal(2, listed.Length);
        Assert.Contains(folder.FullName, listed);
        Assert.Contains(sibling.FullName, listed);
    }

    [Fact]
    public void UnloadKernels_OfAFolderNeverLoaded_KeepsASiblingThatExtendsItsName()
    {
        // Arrange: Frames exists on disk and is never loaded; Frames2 is loaded
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Frames"));
        var sibling = new DirectoryInfo(Path.Combine(_root.FullName, "Frames2"));
        WriteFrameKernel(folder, "a.tf", 1999903, "IOA_F02_NEV_A", 90.0);
        WriteFrameKernel(sibling, "b.tf", 1999904, "IOA_F02_NEV_B", 45.0);
        SpiceAPI.Instance.LoadKernels(sibling);

        // Act
        SpiceAPI.Instance.UnloadKernels(new DirectoryInfo(folder.FullName));

        // Assert: before #673 the unload of Frames unloaded Frames2
        Assert.Equal(45.0, RotationAngleDeg("IOA_F02_NEV_B"), 12);
        AssertUnknown("IOA_F02_NEV_A");
        Assert.Equal([sibling.FullName], Under(_root).Select(k => k.FullName));
    }

    [Fact]
    public void LoadKernels_OfAFolderWhosePathContainsALoadedPath_LoadsIt()
    {
        // Arrange: <root>/k, and <root>/far/<root without its root>/k, whose path contains the first one
        var first = new DirectoryInfo(Path.Combine(_root.FullName, "k"));
        var far = new DirectoryInfo(Path.Combine(_root.FullName, "far", Path.GetRelativePath(Path.GetPathRoot(first.FullName)!, first.FullName)));
        Assert.Contains(first.FullName, far.FullName);
        WriteFrameKernel(first, "a.tf", 1999905, "IOA_F02_FAR_A", 90.0);
        WriteFrameKernel(far, "b.tf", 1999906, "IOA_F02_FAR_B", 45.0);

        // Act
        SpiceAPI.Instance.LoadKernels(first);
        SpiceAPI.Instance.LoadKernels(far);

        // Assert: before #673 the second folder was taken for a path inside the first, which was reloaded instead
        Assert.Equal(90.0, RotationAngleDeg("IOA_F02_FAR_A"), 12);
        Assert.Equal(45.0, RotationAngleDeg("IOA_F02_FAR_B"), 12);
        Assert.Equal(2, Under(_root).Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LoadKernels_WithATrailingSeparator_IsOneEntry(bool separatorFirst)
    {
        // Arrange
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Frames"));
        WriteFrameKernel(folder, "a.tf", 1999907, "IOA_F02_SEP_A", 90.0);
        var withSeparator = new DirectoryInfo(folder.FullName + Path.DirectorySeparatorChar);
        var without = new DirectoryInfo(folder.FullName);

        // Act
        SpiceAPI.Instance.LoadKernels(separatorFirst ? withSeparator : without);
        SpiceAPI.Instance.LoadKernels(separatorFirst ? without : withSeparator);

        // Assert: one folder, one entry
        Assert.Single(Under(_root));
        Assert.Equal(90.0, RotationAngleDeg("IOA_F02_SEP_A"), 12);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnloadKernels_OfADeletedFolder_UnloadsIt(bool loadedInstance)
    {
        // Arrange: a folder of two frame kernels, loaded, then deleted
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Deleted"));
        WriteFrameKernel(folder, "a.tf", 1999908, "IOA_F02_DEL_A", 90.0);
        WriteFrameKernel(folder, "b.tf", 1999909, "IOA_F02_DEL_B", 45.0);
        var loaded = new DirectoryInfo(folder.FullName);
        SpiceAPI.Instance.LoadKernels(loaded);
        Assert.Equal(90.0, RotationAngleDeg("IOA_F02_DEL_A"), 12);
        Assert.True(loaded.Exists);

        // Act: the instance that was loaded, whose cached Exists is still true, or a new one; under the SPICE lock, so that no
        // other test unloads a text kernel while a deleted one is loaded (SPICE would fail to read it again)
        lock (SpiceAPI.SyncRoot)
        {
            Directory.Delete(folder.FullName, true);
            SpiceAPI.Instance.UnloadKernels(loadedInstance ? loaded : new DirectoryInfo(folder.FullName));
        }

        // Assert: before #673 SPICE kept both frames; a new instance also left the folder listed
        Assert.Empty(Under(_root));
        AssertUnknown("IOA_F02_DEL_A");
        AssertUnknown("IOA_F02_DEL_B");
    }

    [Fact]
    public void UnloadKernels_OfAFileOfALoadedFolder_ListsTheRemainingFiles()
    {
        // Arrange
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Pair"));
        var a = WriteFrameKernel(folder, "a.tf", 1999910, "IOA_F02_PAIR_A", 90.0);
        var b = WriteFrameKernel(folder, "b.tf", 1999911, "IOA_F02_PAIR_B", 45.0);
        SpiceAPI.Instance.LoadKernels(folder);

        // Act
        SpiceAPI.Instance.UnloadKernels(new FileInfo(a.FullName));

        // Assert: the listing names what SPICE still holds, the folder no longer (before #673 it stayed listed)
        AssertUnknown("IOA_F02_PAIR_A");
        Assert.Equal(45.0, RotationAngleDeg("IOA_F02_PAIR_B"), 12);
        Assert.Equal([b.FullName], Under(folder).Select(k => k.FullName));
        Assert.IsType<FileInfo>(Under(folder).Single());

        // Act: the folder, whose entry is now its remaining file
        SpiceAPI.Instance.UnloadKernels(folder);

        // Assert
        AssertUnknown("IOA_F02_PAIR_B");
        Assert.Empty(Under(folder));

        // Act: loaded again
        SpiceAPI.Instance.LoadKernels(folder);

        // Assert
        Assert.Equal(90.0, RotationAngleDeg("IOA_F02_PAIR_A"), 12);
        Assert.Equal(45.0, RotationAngleDeg("IOA_F02_PAIR_B"), 12);
        Assert.Equal([folder.FullName], Under(_root).Select(k => k.FullName));
    }

    [Fact]
    public void UnloadKernels_OfASubfolderWithoutKernels_KeepsTheFolder()
    {
        // Arrange: a loaded folder, and a subfolder created after its load, from which nothing was loaded
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Kept"));
        WriteFrameKernel(folder, "a.tf", 1999916, "IOA_F02_KEPT_A", 90.0);
        SpiceAPI.Instance.LoadKernels(folder);
        var subfolder = folder.CreateSubdirectory("later");

        // Act
        SpiceAPI.Instance.UnloadKernels(subfolder);

        // Assert
        Assert.Equal([folder.FullName], Under(_root).Select(k => k.FullName));
        Assert.Equal(90.0, RotationAngleDeg("IOA_F02_KEPT_A"), 12);
    }

    [Fact]
    public void UnloadKernels_WhenSpiceCannotReadAnotherTextKernelAgain_ThrowsAndListsWhatItHolds()
    {
        // Arrange: a loaded text kernel, and another one deleted after its load. SPICE reads every text kernel still loaded
        // again after it unloads one, and cannot read the deleted one
        var kept = new DirectoryInfo(Path.Combine(_root.FullName, "Kept"));
        var deleted = new DirectoryInfo(Path.Combine(_root.FullName, "Deleted"));
        WriteFrameKernel(kept, "a.tf", 1999917, "IOA_F02_FAIL_A", 90.0);
        var gone = WriteFrameKernel(deleted, "b.tf", 1999918, "IOA_F02_FAIL_B", 45.0);
        SpiceAPI.Instance.LoadKernels(kept);
        SpiceAPI.Instance.LoadKernels(new FileInfo(gone.FullName));

        // Under the SPICE lock: no other test may unload a text kernel, nor read the pool, until the deleted kernel is out
        lock (SpiceAPI.SyncRoot)
        {
            File.Delete(gone.FullName);

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.UnloadKernels(kept));

            // Assert: the failure names the file, which SPICE no longer holds, nor the listing
            Assert.Contains(Path.Combine(kept.FullName, "a.tf"), exception.Message);
            Assert.Equal([gone.FullName], Under(_root).Select(k => k.FullName));

            // The unload of the deleted kernel reads the pool again without it
            SpiceAPI.Instance.UnloadKernels(new FileInfo(gone.FullName));
        }

        Assert.Empty(Under(_root));
        AssertUnknown("IOA_F02_FAIL_A");
        AssertUnknown("IOA_F02_FAIL_B");
    }

    [Fact]
    public void UnloadKernels_OfAFolderOfATextThenABinaryKernel_WhenSpiceCannotReadAnotherTextKernelAgain_Throws()
    {
        // Arrange: a text kernel D, deleted after its load, a text kernel E loaded after it, and a folder of a text kernel
        // listed before a binary one (an SPK of scn100 copied). SPICE reads every text kernel still loaded again after it
        // unloads one, from the first, and stops at D: E leaves the pool. The unload of a binary kernel reads nothing again
        // (NAIF, Kernel Required Reading, "Unloading Kernels"; measured with CSPICE N0067)
        var gone = WriteFrameKernel(new DirectoryInfo(Path.Combine(_root.FullName, "Deleted")), "d.tf", 1999922, "IOA_F02_BATCH_D", 30.0);
        var after = WriteFrameKernel(new DirectoryInfo(Path.Combine(_root.FullName, "After")), "e.tf", 1999923, "IOA_F02_BATCH_E", 60.0);
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Batch"));
        folder.Create();
        var binary = Path.Combine(folder.FullName, "y.bsp");
        File.Copy("Data/UserDataTest/scn100/Sites/MySite/Ephemeris/MySite.spk", binary);
        // A folder is loaded in the order of its listing, the file system's: names are tried until the binary kernel is last
        FileInfo text = null;
        for (int i = 0; text == null; i++)
        {
            Assert.True(i < 64, "no name lists the text kernel before the binary one");
            var candidate = WriteFrameKernel(folder, $"x{i}.tf", 1999924, "IOA_F02_BATCH_X", 90.0);
            if (Directory.EnumerateFileSystemEntries(folder.FullName).Last() == binary)
            {
                text = candidate;
            }
            else
            {
                candidate.Delete();
            }
        }

        SpiceAPI.Instance.LoadKernels(gone);
        SpiceAPI.Instance.LoadKernels(after);
        SpiceAPI.Instance.LoadKernels(folder);
        Assert.Equal(60.0, RotationAngleDeg("IOA_F02_BATCH_E"), 12);
        Assert.Equal(90.0, RotationAngleDeg("IOA_F02_BATCH_X"), 12);

        lock (SpiceAPI.SyncRoot)
        {
            File.Delete(gone.FullName);

            // Act: the text kernel's unload fails, the binary one's succeeds; before, its success hid the failure
            var exception = Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.UnloadKernels(folder));

            // Assert: the failure names the text kernel; SPICE no longer knows E, which it still lists, nor X
            Assert.Contains(text.FullName, exception.Message);
            AssertUnknown("IOA_F02_BATCH_E");
            AssertUnknown("IOA_F02_BATCH_X");
            Assert.Equal([gone.FullName, after.FullName], Under(_root).Select(k => k.FullName));

            // The unload of the deleted kernel reads the pool again without it
            SpiceAPI.Instance.UnloadKernels(gone);
        }

        Assert.Equal(60.0, RotationAngleDeg("IOA_F02_BATCH_E"), 12);
        Assert.Equal([after.FullName], Under(_root).Select(k => k.FullName));
    }

    [Fact]
    public void UnloadKernels_OfAMetaKernelOfABinaryKernel_WhenSpiceCannotReadAnotherTextKernelAgain_Throws()
    {
        // Arrange: a meta-kernel that names a binary kernel only, loaded after a text kernel D deleted since its load and a
        // text kernel E. SPICE reads the pool again when it unloads a meta-kernel too (measured with CSPICE N0067), which
        // is why the unload of a meta-kernel counts as a reread (SpiceAPI.ReloadsPool)
        var gone = WriteFrameKernel(new DirectoryInfo(Path.Combine(_root.FullName, "Deleted")), "d.tf", 1999925, "IOA_F02_MKB_D", 30.0);
        var after = WriteFrameKernel(new DirectoryInfo(Path.Combine(_root.FullName, "After")), "e.tf", 1999926, "IOA_F02_MKB_E", 60.0);
        var binary = new FileInfo(Path.Combine(_root.FullName, "Binary", "y.bsp"));
        binary.Directory!.Create();
        File.Copy("Data/UserDataTest/scn100/Sites/MySite/Ephemeris/MySite.spk", binary.FullName);
        Assert.True(binary.FullName.Length <= 78, binary.FullName);
        var metaKernel = new FileInfo(Path.Combine(_root.FullName, "Meta", "m.tm"));
        metaKernel.Directory!.Create();
        File.WriteAllText(metaKernel.FullName, $"KPL/MK\n\\begindata\nKERNELS_TO_LOAD = ( '{binary.FullName}' )\n\\begintext\n");
        SpiceAPI.Instance.LoadKernels(gone);
        SpiceAPI.Instance.LoadKernels(after);
        SpiceAPI.Instance.LoadKernels(metaKernel);
        Assert.Equal(60.0, RotationAngleDeg("IOA_F02_MKB_E"), 12);

        lock (SpiceAPI.SyncRoot)
        {
            File.Delete(gone.FullName);

            // Act and assert: the unload reads the pool again and stops at D, so E leaves the pool
            var exception = Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.UnloadKernels(metaKernel));
            Assert.Contains(metaKernel.FullName, exception.Message);
            AssertUnknown("IOA_F02_MKB_E");
            SpiceAPI.Instance.UnloadKernels(gone);
        }

        Assert.Equal(60.0, RotationAngleDeg("IOA_F02_MKB_E"), 12);
        Assert.Equal([after.FullName], Under(_root).Select(k => k.FullName));
    }

    [Fact]
    public void ReloadsPool_TextAndMetaKernelsOnly()
    {
        // Arrange: the ID words of NAIF's Kernel Required Reading, "SPICE Kernel Type Identification"
        var folder = Directory.CreateDirectory(Path.Combine(_root.FullName, "Kinds"));
        string Write(string name, string content)
        {
            var file = Path.Combine(folder.FullName, name);
            File.WriteAllText(file, content);
            return file;
        }

        // Act and assert: SPICE loads as a text kernel every file without the ID word of a binary kernel
        Assert.True(SpiceAPI.ReloadsPool(WriteFrameKernel(folder, "a.tf", 1999927, "IOA_F02_KIND", 1.0).FullName));
        Assert.True(SpiceAPI.ReloadsPool(Write("m.tm", "KPL/MK\n\\begindata\nKERNELS_TO_LOAD = ( 'x' )\n\\begintext\n")));
        Assert.True(SpiceAPI.ReloadsPool(Write("old.ti", "\\begindata\nX = 1\n")));
        Assert.True(SpiceAPI.ReloadsPool(Write("short", "KPL")));
        Assert.False(SpiceAPI.ReloadsPool("Data/UserDataTest/scn100/Sites/MySite/Ephemeris/MySite.spk"));
        Assert.False(SpiceAPI.ReloadsPool(Write("das", "DAS/DSK ")));
        Assert.False(SpiceAPI.ReloadsPool(Write("olddaf", "NAIF/DAF")));
        Assert.False(SpiceAPI.ReloadsPool(Write("olddas", "NAIF/DAS")));
        // A file that cannot be read repairs no failure
        Assert.False(SpiceAPI.ReloadsPool(Path.Combine(folder.FullName, "missing.tf")));
    }

    [Fact]
    public void UnloadKernels_OfAFileLoadedByAMetaKernel_UnloadsItByName()
    {
        // Arrange: a meta-kernel names a frame kernel of another folder, which no entry holds
        var frames = new DirectoryInfo(Path.Combine(_root.FullName, "Frames"));
        var kernel = WriteFrameKernel(frames, "a.tf", 1999919, "IOA_F02_META_A", 90.0);
        Assert.True(kernel.FullName.Length <= 78, kernel.FullName);
        var metaKernel = new FileInfo(Path.Combine(_root.FullName, "Meta", "m.tm"));
        metaKernel.Directory!.Create();
        File.WriteAllText(metaKernel.FullName, $"KPL/MK\n\\begindata\nKERNELS_TO_LOAD = ( '{kernel.FullName}' )\n\\begintext\n");
        SpiceAPI.Instance.LoadKernels(metaKernel);
        Assert.Equal(90.0, RotationAngleDeg("IOA_F02_META_A"), 12);

        // Act: a path that matches no entry is handed to SPICE by its name
        SpiceAPI.Instance.UnloadKernels(new FileInfo(kernel.FullName));

        // Assert
        AssertUnknown("IOA_F02_META_A");
        Assert.Equal([metaKernel.FullName], Under(_root).Select(k => k.FullName));
    }

    [Fact]
    public void UnloadKernels_OfAPathOfNoEntry_WhenSpiceCannotReadAnotherTextKernelAgain_Throws()
    {
        // Arrange: a frame kernel loaded by a meta-kernel, which no entry holds, and a text kernel deleted after its load
        var kernel = WriteFrameKernel(new DirectoryInfo(Path.Combine(_root.FullName, "Frames")), "a.tf", 1999920, "IOA_F02_META_B", 90.0);
        var metaKernel = new FileInfo(Path.Combine(_root.FullName, "Meta", "m.tm"));
        metaKernel.Directory!.Create();
        File.WriteAllText(metaKernel.FullName, $"KPL/MK\n\\begindata\nKERNELS_TO_LOAD = ( '{kernel.FullName}' )\n\\begintext\n");
        var gone = WriteFrameKernel(new DirectoryInfo(Path.Combine(_root.FullName, "Deleted")), "b.tf", 1999921, "IOA_F02_META_C", 45.0);
        SpiceAPI.Instance.LoadKernels(metaKernel);
        SpiceAPI.Instance.LoadKernels(gone);

        lock (SpiceAPI.SyncRoot)
        {
            File.Delete(gone.FullName);

            // Act and assert
            var exception = Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.UnloadKernels(new FileInfo(kernel.FullName)));
            Assert.Contains(kernel.FullName, exception.Message);
            SpiceAPI.Instance.UnloadKernels(gone);
        }

        SpiceAPI.Instance.UnloadKernels(metaKernel);
        Assert.Empty(Under(_root));
        AssertUnknown("IOA_F02_META_B");
    }

    [Theory]
    [InlineData("a/b", "a", true)]
    [InlineData("a", "a", true)]
    [InlineData("a/", "a", true)]
    [InlineData("a", "a/", true)]
    [InlineData("a/b/c.tf", "a/b", true)]
    [InlineData("a2", "a", false)]
    [InlineData("a.tf.bak", "a.tf", false)]
    [InlineData("a", "a/b", false)]
    [InlineData("far/a", "a", false)]
    public void IsSameOrInside_ComparesComponents(string path, string root, bool expected)
    {
        var baseDirectory = _root.FullName;
        string Full(string relative) => Path.Combine(baseDirectory, relative.Replace('/', Path.DirectorySeparatorChar));

        Assert.Equal(expected, SpiceAPI.IsSameOrInside(Full(path), Full(root)));
    }

    [Fact]
    public void IsSameOrInside_UnderTheRootOfTheFileSystem()
    {
        var fileSystemRoot = Path.GetPathRoot(_root.FullName)!;

        Assert.True(SpiceAPI.IsSameOrInside(_root.FullName, fileSystemRoot));
        Assert.True(SpiceAPI.IsSameOrInside(fileSystemRoot, fileSystemRoot));
        Assert.False(SpiceAPI.IsSameOrInside(fileSystemRoot, _root.FullName));
    }

    /// <summary>
    /// After each scenario, the files under the temporary root that SPICE holds, read from KEEPER, are those the scenario
    /// loaded and did not unload, and those the listing names: a listed file itself, the kernel files of a listed folder.
    /// </summary>
    [Fact]
    [Trait("Category", "Validation")]
    public void Registry_EqualsTheFilesSpiceHolds()
    {
        // The native library exports CSPICE on Linux only (CspiceDirect)
        if (!OperatingSystem.IsLinux()) return;

        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Frames"));
        var sibling = new DirectoryInfo(Path.Combine(_root.FullName, "Frames2"));
        var a = WriteFrameKernel(folder, "a.tf", 1999912, "IOA_F02_REG_A", 90.0);
        var b = WriteFrameKernel(sibling, "b.tf", 1999913, "IOA_F02_REG_B", 45.0);
        var c = WriteFrameKernel(sibling, "c.tf", 1999914, "IOA_F02_REG_C", 45.0);

        // A sibling, both orders, and the unload of a folder never loaded
        SpiceAPI.Instance.LoadKernels(sibling);
        SpiceAPI.Instance.UnloadKernels(new DirectoryInfo(folder.FullName));
        AssertHeld(b, c);
        SpiceAPI.Instance.LoadKernels(folder);
        AssertHeld(a, b, c);
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(sibling.FullName));
        AssertHeld(a, b, c);

        // A path that contains a loaded one
        var far = new DirectoryInfo(Path.Combine(_root.FullName, "far", Path.GetRelativePath(Path.GetPathRoot(folder.FullName)!, folder.FullName)));
        var d = WriteFrameKernel(far, "d.tf", 1999915, "IOA_F02_REG_D", 90.0);
        SpiceAPI.Instance.LoadKernels(far);
        AssertHeld(a, b, c, d);

        // A file of a loaded folder, then the rest of the folder
        SpiceAPI.Instance.UnloadKernels(new FileInfo(b.FullName));
        AssertHeld(a, c, d);
        SpiceAPI.Instance.UnloadKernels(sibling);
        AssertHeld(a, d);

        // A deleted folder, through a new instance, then through the loaded one, each under the SPICE lock
        lock (SpiceAPI.SyncRoot)
        {
            Directory.Delete(Path.Combine(_root.FullName, "far"), true);
            SpiceAPI.Instance.UnloadKernels(new DirectoryInfo(far.FullName));
        }

        AssertHeld(a);
        lock (SpiceAPI.SyncRoot)
        {
            Directory.Delete(folder.FullName, true);
            SpiceAPI.Instance.UnloadKernels(folder);
        }

        AssertHeld();
    }

    // SPICE's files under the root, the expected ones, and the files the listing names, in any order
    private void AssertHeld(params FileInfo[] expected)
    {
        var prefix = _root.FullName + Path.DirectorySeparatorChar;
        var held = CspiceDirect.LoadedKernelFiles().Where(f => f.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
        var named = Under(_root).SelectMany(k => k is DirectoryInfo
                ? Directory.Exists(k.FullName) ? SpiceAPI.KernelFiles(k) : [k.FullName + Path.DirectorySeparatorChar + "(deleted)"]
                : [k.FullName])
            .Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected.Select(f => f.FullName).Order(StringComparer.Ordinal), held);
        Assert.Equal(held, named);
    }
}
