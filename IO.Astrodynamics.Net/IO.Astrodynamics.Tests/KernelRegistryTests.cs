// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Kernels;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Tests.Body;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.Exceptions;
using Xunit;
using Xunit.Abstractions;
using static IO.Astrodynamics.Tests.TestKernelWriter;

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
/// <see cref="SpiceAPI.TransformFrame(TimeSystem.Time, Frames.Frame, Frames.Frame)"/>, which throws <see cref="ArgumentException"/> once
/// the frame is out of the pool (R25 of #668). A text kernel
/// is closed by SPICE after its load, so a loaded text kernel can be deleted on every platform, and SPICE keeps nothing of it
/// once unloaded. A binary kernel (the copies of the moon PCK of <c>Data/SolarSystem</c>) stays open: the scenarios that
/// delete a loaded one run in the child process <c>IO.Astrodynamics.Tests.KernelUnload</c> (<see cref="KernelUnloadChildTests"/>), because a failed reopening
/// leaves the deleted kernel open in SPICE for good, which made the IAU frames of other test classes fail (#673), and are
/// skipped on Windows, where it cannot be deleted (<see cref="NotOnWindowsTheoryAttribute"/>). The kernels are written by
/// <see cref="TestKernelWriter"/>. The frame ids and frame class ids 1999901 and up are not defined by
/// <c>Data/SolarSystem</c> (<see cref="KernelShadowing"/>), and every kernel is unloaded in <see cref="Dispose"/>.
/// </remarks>
public class KernelRegistryTests : IDisposable
{
    private static readonly TimeSystem.Time Epoch = new(2024, 1, 1, TimeScale.TDB);
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("kernel-registry-");
    private readonly ITestOutputHelper _output;

    public KernelRegistryTests(ITestOutputHelper output)
    {
        _output = output;
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

    // The rotation angle of the frame from ICRF, in degrees (the sign of a quaternion is free); throws once SPICE no longer
    // knows the frame
    internal static double RotationAngleDeg(string frameName)
    {
        var q = SpiceAPI.Instance.TransformFrame(Epoch, new Frames.Frame(frameName), Frames.Frame.ICRF).Rotation;
        return 2.0 * System.Math.Acos(System.Math.Min(1.0, System.Math.Abs(q.W))) * Astrodynamics.Constants.Rad2Deg;
    }

    // A frame SPICE no longer knows is a bad argument since R25 of #668 (an InvalidOperationException before 11.0.0)
    private static void AssertUnknown(string frameName)
    {
        Assert.Throws<ArgumentException>(() => RotationAngleDeg(frameName));
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
        // Arrange: <root>/k, and <root>/far/<root without its root>/k, whose path contains the first one. On Windows the colon
        // of a drive is in no component, so a full path holds another one only at its start: there the second path contains
        // the first one without its drive
        var first = new DirectoryInfo(Path.Combine(_root.FullName, "k"));
        var relative = Path.GetRelativePath(Path.GetPathRoot(first.FullName)!, first.FullName);
        var far = new DirectoryInfo(Path.Combine(_root.FullName, "far", relative));
        Assert.Contains(OperatingSystem.IsWindows() ? Path.DirectorySeparatorChar + relative : first.FullName, far.FullName);
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
            var exception = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.UnloadKernels(kept));

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
        // unloads one, from the first, and stops at D: E leaves the pool. The unload of an SPK opens the other SPKs again and
        // reads no text kernel (NAIF, Kernel Required Reading, "Unloading Kernels"; measured with CSPICE N0067)
        var gone = WriteFrameKernel(new DirectoryInfo(Path.Combine(_root.FullName, "Deleted")), "d.tf", 1999922, "IOA_F02_BATCH_D", 30.0);
        var after = WriteFrameKernel(new DirectoryInfo(Path.Combine(_root.FullName, "After")), "e.tf", 1999923, "IOA_F02_BATCH_E", 60.0);
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Batch"));
        folder.Create();
        // A folder is loaded in the order of its listing, the file system's: alphabetical on NTFS, by creation (either way) on
        // tmpfs, by a hash of the name under the file system's seed on ext4, where one fixed name can sort before every other.
        // Pairs of names are tried, each pair its own text and binary name and created in turn in either order, until the
        // binary kernel is listed last
        FileInfo text = null;
        for (int i = 0; text == null; i++)
        {
            Assert.True(i < 64, "no pair of names lists the text kernel before the binary one");
            var binary = Path.Combine(folder.FullName, $"y{i}.bsp");
            bool binaryFirst = i % 2 == 1;
            if (binaryFirst)
            {
                File.Copy("Data/UserDataTest/scn100/Sites/MySite/Ephemeris/MySite.spk", binary);
            }

            var candidate = WriteFrameKernel(folder, $"x{i}.tf", 1999924, "IOA_F02_BATCH_X", 90.0);
            if (!binaryFirst)
            {
                File.Copy("Data/UserDataTest/scn100/Sites/MySite/Ephemeris/MySite.spk", binary);
            }

            if (Directory.EnumerateFileSystemEntries(folder.FullName).SequenceEqual([candidate.FullName, binary]))
            {
                text = candidate;
            }
            else
            {
                candidate.Delete();
                File.Delete(binary);
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
            var exception = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.UnloadKernels(folder));

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
        // is why the unload of a meta-kernel reads the text kernels again (SpiceAPI.UnloadRereads)
        var gone = WriteFrameKernel(new DirectoryInfo(Path.Combine(_root.FullName, "Deleted")), "d.tf", 1999925, "IOA_F02_MKB_D", 30.0);
        var after = WriteFrameKernel(new DirectoryInfo(Path.Combine(_root.FullName, "After")), "e.tf", 1999926, "IOA_F02_MKB_E", 60.0);
        var binary = new FileInfo(Path.Combine(_root.FullName, "Binary", "y.bsp"));
        binary.Directory!.Create();
        File.Copy("Data/UserDataTest/scn100/Sites/MySite/Ephemeris/MySite.spk", binary.FullName);
        var metaKernel = WriteMetaKernel(new FileInfo(Path.Combine(_root.FullName, "Meta", "m.tm")), binary.FullName);
        SpiceAPI.Instance.LoadKernels(gone);
        SpiceAPI.Instance.LoadKernels(after);
        SpiceAPI.Instance.LoadKernels(metaKernel);
        Assert.Equal(60.0, RotationAngleDeg("IOA_F02_MKB_E"), 12);

        lock (SpiceAPI.SyncRoot)
        {
            File.Delete(gone.FullName);

            // Act and assert: the unload reads the pool again and stops at D, so E leaves the pool
            var exception = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.UnloadKernels(metaKernel));
            Assert.Contains(metaKernel.FullName, exception.Message);
            AssertUnknown("IOA_F02_MKB_E");
            SpiceAPI.Instance.UnloadKernels(gone);
        }

        Assert.Equal(60.0, RotationAngleDeg("IOA_F02_MKB_E"), 12);
        Assert.Equal([after.FullName], Under(_root).Select(k => k.FullName));
    }

    [Fact]
    public void UnloadKernels_OfAFolderOfTwoLoadedFolders_WhenTheKernelOfTheSecondIsDeleted_DoesNotThrow()
    {
        // Arrange: a folder that holds two loaded folders B and D, each its own entry, then a text kernel E loaded after them,
        // D's kernel deleted after the load. SPICE reads the text kernels again after it unloads B's, stops at D's and leaves
        // E out of the pool; the unload of D's reads them again without it, E included (measured with CSPICE N0067)
        var parent = new DirectoryInfo(Path.Combine(_root.FullName, "Parent"));
        var b = new DirectoryInfo(Path.Combine(parent.FullName, "B"));
        var d = new DirectoryInfo(Path.Combine(parent.FullName, "D"));
        WriteFrameKernel(b, "b.tf", 1999961, "IOA_F02_TWO_B", 30.0);
        var gone = WriteFrameKernel(d, "d.tf", 1999962, "IOA_F02_TWO_D", 45.0);
        var after = WriteFrameKernel(new DirectoryInfo(Path.Combine(_root.FullName, "After")), "e.tf", 1999963, "IOA_F02_TWO_E", 60.0);
        SpiceAPI.Instance.LoadKernels(b);
        SpiceAPI.Instance.LoadKernels(d);
        SpiceAPI.Instance.LoadKernels(after);

        lock (SpiceAPI.SyncRoot)
        {
            File.Delete(gone.FullName);

            // Act: before, the failure of B's entry ended the call, D's entry was neither unloaded nor taken for a repair
            SpiceAPI.Instance.UnloadKernels(parent);
        }

        // Assert
        Assert.Equal([after.FullName], Under(_root).Select(k => k.FullName));
        AssertUnknown("IOA_F02_TWO_B");
        AssertUnknown("IOA_F02_TWO_D");
        Assert.Equal(60.0, RotationAngleDeg("IOA_F02_TWO_E"), 12);
    }

    [Fact]
    public void UnloadKernels_OfAFolderOfATextKernelWhoseCommentNamesKernelsToLoad_WhenTheOtherIsDeleted_DoesNotThrow()
    {
        // Arrange: a folder of a text kernel X whose comment names KERNELS_TO_LOAD, listed before a text kernel D deleted
        // after the load. SPICE loads X as a text kernel, not a meta-kernel (its data assigns none of KERNELS_TO_LOAD,
        // PATH_SYMBOLS, PATH_VALUES); the unload of X fails on D, and that of D reads the text kernels again without it
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Comment"));
        var gone = WriteFrameKernel(folder, "d.tf", 1999964, "IOA_F02_CMT_D", 45.0);
        ListedBefore(folder, gone, stem =>
        {
            var file = WriteFrameKernel(folder, $"{stem}.tf", 1999965, "IOA_F02_CMT_X", 30.0);
            File.AppendAllText(file.FullName, "This frame kernel is not meant for KERNELS_TO_LOAD.\n");
            return file;
        });
        SpiceAPI.Instance.LoadKernels(folder);
        Assert.Equal(30.0, RotationAngleDeg("IOA_F02_CMT_X"), 12);

        lock (SpiceAPI.SyncRoot)
        {
            File.Delete(gone.FullName);

            // Act: before, X counted as a meta-kernel, whose failure is never taken as repaired
            SpiceAPI.Instance.UnloadKernels(folder);
        }

        // Assert
        Assert.Empty(Under(_root));
        AssertUnknown("IOA_F02_CMT_X");
        AssertUnknown("IOA_F02_CMT_D");
    }

    // Text kernels as SPICE N0067 loads them, each with the type KEEPER gives it (kinfo_c, measured): a meta-kernel when its
    // data assigns KERNELS_TO_LOAD, PATH_SYMBOLS or PATH_VALUES (FURNSH watches them), a text kernel otherwise; none of
    // them loads a kernel
    public static TheoryData<string, string, bool> TextKernelTypes => new()
    {
        { "frame.tf", "KPL/FK\n\\begindata\nIOA_KT_A = 1\n\\begintext\n", false },
        { "old.ti", "\\begindata\nIOA_KT_B = 1\n", false },
        { "comment.tf", "KPL/FK\n\\begindata\nIOA_KT_C = 1\n\\begintext\nThis kernel is not meant for KERNELS_TO_LOAD.\n", false },
        { "string.tf", "KPL/FK\n\\begindata\nIOA_KT_D = ( 'KERNELS_TO_LOAD',\n 'PATH_VALUES' )\n\\begintext\n", false },
        { "names.tf", "KPL/FK\n\\begindata\npath_values = ( 'b' )\nIOA_PATH_VALUES = ( 'b' )\n", false },
        { "marker.tf", "KPL/FK\n\\begindata x\nPATH_VALUES = ( 'b' )\n\\BEGINDATA\nPATH_VALUES = ( 'b' )\n", false },
        { "empty.tf", "KPL/FK\n\\begindata\nIOA_KT_E = 1\n\\begintext\n\\begindata\n\\begintext\nPATH_VALUES = ( 'b' )\n", false },
        { "symbols.tm", "KPL/MK\n\\begindata\nPATH_SYMBOLS = ( 'A' )\nPATH_VALUES = ( 'b' )\n\\begintext\n", true },
        { "values.tm", "KPL/MK\n\\begindata\nPATH_VALUES = ( 'b' )\n\\begintext\n", true },
        { "blanks.tm", "KPL/MK\n  \t\\begindata  \nPATH_VALUES = ( 'b' )\n\\begintext\n", true },
        { "append.tm", "KPL/MK\n\\begindata\nPATH_VALUES+=( 'b' )\n", true },
        { "crlf.tm", "KPL/MK\r\n\\begindata\r\nPATH_VALUES = ( 'b' )\r\n", true },
        { "second.tm", "KPL/MK\n\\begindata\nIOA_KT_F = 1\n\\begintext\ntext\n\\begindata\nPATH_VALUES = ( 'b' )\n", true },
        { "nokpl.tm", "\\begindata\nPATH_VALUES += ( 'b' )\n", true },
    };

    [Theory]
    [MemberData(nameof(TextKernelTypes))]
    public void UnloadRereads_OfATextKernel_ReadsTheTextKernels_AndOfAMetaKernel_KernelsOfUnknownKinds(string name, string content, bool metaKernel)
    {
        var file = Path.Combine(Directory.CreateDirectory(Path.Combine(_root.FullName, "Text")).FullName, name);
        File.WriteAllText(file, content);

        var expected = metaKernel
            ? (KernelRereads.TextKernels, KernelRereads.TextKernels | KernelRereads.Unknown)
            : (KernelRereads.TextKernels, KernelRereads.TextKernels);
        Assert.Equal(expected, SpiceAPI.UnloadRereads(file));
    }

    /// <summary>The type KEEPER gives each text kernel of <see cref="TextKernelTypes"/> once loaded, read from CSPICE.</summary>
    [Theory]
    [MemberData(nameof(TextKernelTypes))]
    [Trait("Category", "Validation")]
    public void TextKernelTypes_AreTheTypesKeeperGives(string name, string content, bool metaKernel)
    {
        // The native library exports CSPICE on Linux only (CspiceDirect)
        if (!OperatingSystem.IsLinux()) return;

        var file = new FileInfo(Path.Combine(Directory.CreateDirectory(Path.Combine(_root.FullName, "Keeper")).FullName, name));
        File.WriteAllText(file.FullName, content);
        SpiceAPI.Instance.LoadKernels(file);

        Assert.Equal(metaKernel ? "META" : "TEXT", CspiceDirect.LoadedKernelType(file.FullName));
    }

    [Fact]
    public void UnloadRereads_OfABinaryKernel_TheKernelsOfItsKind()
    {
        // Arrange: the ID words of NAIF's Kernel Required Reading, "SPICE Kernel Type Identification"
        var folder = Directory.CreateDirectory(Path.Combine(_root.FullName, "Kinds"));
        string Write(string name, string content)
        {
            var file = Path.Combine(folder.FullName, name);
            File.WriteAllText(file, content);
            return file;
        }

        void AssertRereads(string file, KernelRereads reads, KernelRereads mayFailOn) =>
            Assert.Equal((reads, mayFailOn), SpiceAPI.UnloadRereads(file));

        // Act and assert: the binary kernels whose unload opens the others of their kind again (KEEPER's UNLOAD, SPKLEF, CKLPF,
        // PCKLOF, ZZDSKLSF, EKLEF; measured with CSPICE N0067)
        AssertRereads("Data/UserDataTest/scn100/Sites/MySite/Ephemeris/MySite.spk", KernelRereads.Spks, KernelRereads.Spks);
        AssertRereads("Data/FieldOfViewReference/fovleo.bc", KernelRereads.Cks, KernelRereads.Cks);
        AssertRereads("Data/SolarSystem/moon_pa_de421_1900-2050.bpc", KernelRereads.Pcks, KernelRereads.Pcks);
        AssertRereads(Write("dsk", "DAS/DSK "), KernelRereads.Dsks, KernelRereads.Dsks);
        AssertRereads(Write("ek", "DAS/EK  "), KernelRereads.Eks, KernelRereads.Eks);
        // A DAF of the oldest ID word, which SPICE loads as an SPK or a CK from its segments (GETFAT, ZZCKSPK): the library
        // does not tell which, so it reads no kind for its repairs, and its failure is repaired by none
        AssertRereads("Data/SolarSystem/earthstns_itrf93_201023.bsp", KernelRereads.None, KernelRereads.Spks | KernelRereads.Cks);
        AssertRereads(Write("olddaf", "NAIF/DAF"), KernelRereads.None, KernelRereads.Spks | KernelRereads.Cks);
        // An ID word SPICE loads nothing from, and a file that cannot be read
        AssertRereads(Write("olddas", "NAIF/DAS"), KernelRereads.None, KernelRereads.Unknown);
        AssertRereads(Path.Combine(folder.FullName, "missing.tf"), KernelRereads.None, KernelRereads.Unknown);
        // The data of a text kernel after the scan buffer of IsKernelFile
        const string header = "KPL/MK\n\\begindata\n";
        AssertRereads(Write("far.tm", header + new string(' ', 65536 - header.Length - 5) + "KERNELS_TO_LOAD = ( 'x' )\n"),
            KernelRereads.TextKernels, KernelRereads.TextKernels | KernelRereads.Unknown);
    }

    [Fact]
    public void UnloadKernels_OfAFileLoadedByAMetaKernel_UnloadsItByName()
    {
        // Arrange: a meta-kernel names a frame kernel of another folder, which no entry holds
        var frames = new DirectoryInfo(Path.Combine(_root.FullName, "Frames"));
        var kernel = WriteFrameKernel(frames, "a.tf", 1999919, "IOA_F02_META_A", 90.0);
        var metaKernel = WriteMetaKernel(new FileInfo(Path.Combine(_root.FullName, "Meta", "m.tm")), kernel.FullName);
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
        var metaKernel = WriteMetaKernel(new FileInfo(Path.Combine(_root.FullName, "Meta", "m.tm")), kernel.FullName);
        var gone = WriteFrameKernel(new DirectoryInfo(Path.Combine(_root.FullName, "Deleted")), "b.tf", 1999921, "IOA_F02_META_C", 45.0);
        SpiceAPI.Instance.LoadKernels(metaKernel);
        SpiceAPI.Instance.LoadKernels(gone);

        lock (SpiceAPI.SyncRoot)
        {
            File.Delete(gone.FullName);

            // Act and assert
            var exception = Assert.Throws<SpiceException>(() => SpiceAPI.Instance.UnloadKernels(new FileInfo(kernel.FullName)));
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

    // Equally spaced states of a circular orbit about the Earth in ICRF, 60 s apart from the epoch, x at the given radius
    private static StateVector[] EarthStates(int count, double x)
    {
        var earth = new Astrodynamics.Body.CelestialBody(399);
        return Enumerable.Range(0, count).Select(i => new StateVector(new Vector3(x, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), earth,
            Epoch.AddSeconds(60.0 * i), Frames.Frame.ICRF)).ToArray();
    }

    private static StateVector ReadObject(int naifId, double seconds) =>
        SpiceAPI.Instance.ReadEphemeris(Epoch.AddSeconds(seconds), 399, naifId, Frames.Frame.ICRF, Aberration.None);

    /// <summary>
    /// A file loaded through <see cref="SpiceAPI.LoadKernels"/>, then replaced by the native writer: its kernel object loaded
    /// the file once more and unloaded it once, so the caller's load stayed in SPICE, which held the replaced file, deleted
    /// but open, and read its data, while the registry listed it (#706, measured on c533a6e: the replaced states read until
    /// the file was unloaded; on Windows the removal of the open file threw across P/Invoke). The writer now unloads it first,
    /// as <see cref="SpiceAPI.UnloadKernels"/> does: no longer listed, its object not read until it is loaded again, and then
    /// read from the new file. A file of a loaded folder replaces the folder by its remaining files. References: the states
    /// written, 5 km apart from the replaced ones, and SPICE's coverage, which reads no state of an object it holds no file of.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteEphemeris_ReplacingALoadedFile_UnloadsIt(bool inAFolder)
    {
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Spks"));
        folder.Create();
        var c = new FileInfo(Path.Combine(folder.FullName, "c.bsp"));
        var other = new FileInfo(Path.Combine(folder.FullName, "other.bsp"));
        SpiceAPI.Instance.WriteEphemeris(c, -1999722, EarthStates(3, 6800000.0));
        SpiceAPI.Instance.WriteEphemeris(other, -1999723, EarthStates(3, 6800000.0));
        SpiceAPI.Instance.LoadKernels(inAFolder ? folder : c);
        Assert.Equal(6800000.0, ReadObject(-1999722, 60.0).Position.X);
        var written = EarthStates(4, 6805000.0);

        SpiceAPI.Instance.WriteEphemeris(c, -1999722, written);

        Assert.Equal(inAFolder ? [other.FullName] : Array.Empty<string>(), Under(_root).Select(k => k.FullName));
        Assert.Throws<SpiceException>(() => ReadObject(-1999722, 60.0));
        SpiceAPI.Instance.LoadKernels(c);
        var read = ReadObject(-1999722, 180.0);
        Assert.Equal(written[3].Position, read.Position);
        Assert.Equal(written[3].Velocity, read.Velocity);
    }

    // The bound of the angle between a CK record written and the one read back at its epoch: twice the worst over Linux x64,
    // Windows and macOS arm64, rounded up at two significant digits (MEASURE lines of CI run 43851568556: 0.000E+000 rad on
    // Linux and macOS arm64, 2.220E-016 rad on Windows, in process and in the child; review of #706: before, 1e-9° taken
    // from other tests)
    private const double CkRecordAngleBound = 4.5e-16;

    /// <summary>
    /// The CK analog: a CK of identity records loaded, then replaced by five records of 90° about z. Before, the load stayed
    /// in SPICE and the identity records were read (#706). References: the records written, compared as a rotation angle
    /// (2 atan2(|v|, |w|) of the quaternion between them) within <see cref="CkRecordAngleBound"/>.
    /// </summary>
    [Fact]
    public void WriteOrientation_ReplacingALoadedFile_UnloadsIt()
    {
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Cks"));
        folder.Create();
        var clock = new Clock("IOA_K1_REG_CLOCK", 65536);
        var spacecraft = new Spacecraft(-1999724, "IOA_K1_REG", 3000.0, 10000.0, clock, EarthStates(1, 6800000.0)[0]);
        var clockFile = new FileInfo(Path.Combine(folder.FullName, "clock.tsc"));
        clock.WriteAsync(clockFile).GetAwaiter().GetResult();
        SpiceAPI.Instance.LoadKernels(clockFile);
        var turned = new Quaternion(Vector3.VectorZ, System.Math.PI / 2.0);
        var c = new FileInfo(Path.Combine(folder.FullName, "c.bc"));
        SpiceAPI.Instance.WriteOrientation(c, spacecraft.NaifId, Enumerable.Range(0, 3)
            .Select(i => new StateOrientation(Quaternion.Identity, Vector3.Zero, Epoch.AddSeconds(i), Frames.Frame.ICRF)).ToArray());
        SpiceAPI.Instance.LoadKernels(c);

        SpiceAPI.Instance.WriteOrientation(c, spacecraft.NaifId, Enumerable.Range(0, 5)
            .Select(i => new StateOrientation(turned, Vector3.Zero, Epoch.AddSeconds(i), Frames.Frame.ICRF)).ToArray());

        Assert.Equal([clockFile.FullName], Under(_root).Select(k => k.FullName));
        // The CK is no longer loaded: the read throws, with SPICE's message when no other CK of the process is loaded, and
        // leaves nothing pending, so that the load below succeeds (#712; before, a second CK of the spacecraft stayed loaded
        // so that this load did not throw the CKBSS error the failed read left pending). SPICE's error is a SpiceException
        // (R03 of #668), "No orientation found" the library's InvalidOperationException
        Assert.ThrowsAny<InvalidOperationException>(() => ReadOrientation(spacecraft.NaifId, 1.0));
        SpiceAPI.Instance.LoadKernels(c);
        var q = ReadOrientation(spacecraft.NaifId, 4.0).Rotation;
        double angle = 2.0 * System.Math.Atan2((q.Conjugate() * turned).VectorPart.Magnitude(), System.Math.Abs((q.Conjugate() * turned).W));
        _output.WriteLine($"MEASURE #706 CK replaced while loaded: angle between the record written and the one read {angle:E3} rad");
        Assert.True(angle <= CkRecordAngleBound, $"angle {angle:E3} rad");
    }

    private static StateOrientation ReadOrientation(int naifId, double seconds) => SpiceAPI.Instance
        .ReadOrientation(new Window(Epoch.AddSeconds(seconds), Epoch.AddSeconds(seconds)), naifId, TimeSpan.Zero, Frames.Frame.ICRF,
            TimeSpan.FromSeconds(1.0)).Single();

    /// <summary>
    /// A spacecraft's SPK loaded, then written again by the managed writer (<c>Spacecraft.WriteEphemeris</c>): it unloaded the
    /// file in SPICE, but the registry went on listing it (#706, measured on c533a6e). It is now unloaded as
    /// <see cref="SpiceAPI.UnloadKernels"/> does: no longer listed, its object read again once it is loaded.
    /// </summary>
    [Fact]
    public void SpacecraftWriteEphemeris_ReplacingALoadedFile_NoLongerListsIt()
    {
        var spacecraft = new Spacecraft(-1999725, "IOA_K1_REG_SPC", 3000.0, 10000.0, new Clock("IOA_K1_REG_SPC_CLOCK", 65536),
            EarthStates(1, 6800000.0)[0]);
        spacecraft.AddPropagatedStates(EarthStates(3, 6800000.0));
        var c = new FileInfo(Path.Combine(_root.FullName, "managed.bsp"));
        spacecraft.WriteEphemeris(c);
        SpiceAPI.Instance.LoadKernels(c);

        spacecraft.WriteEphemeris(c);

        Assert.Empty(Under(_root));
        Assert.Throws<SpiceException>(() => ReadObject(-1999725, 60.0));
        SpiceAPI.Instance.LoadKernels(c);
        Assert.Equal(6800000.0, ReadObject(-1999725, 60.0).Position.X);
    }

    // Writes the states of an object through the native writer (SpiceAPI.WriteEphemeris) or the managed one
    // (Spacecraft.WriteEphemeris, through SpiceAPI.WriteEphemerisArcs), with a spacecraft of its own for each call
    private static void WriteStates(FileInfo file, int naifId, StateVector[] states, bool managed)
    {
        if (!managed)
        {
            SpiceAPI.Instance.WriteEphemeris(file, naifId, states);
            return;
        }

        var spacecraft = new Spacecraft(naifId, $"IOA_K1R_{-naifId}", 3000.0, 10000.0, new Clock($"IOA_K1R_CLOCK_{-naifId}", 65536), states[0]);
        spacecraft.AddPropagatedStates(states);
        spacecraft.WriteEphemeris(file);
    }

    /// <summary>
    /// A file two meta-kernels hold by its absolute path, then replaced by each writer. The native writer unloaded every load
    /// of its name (#706), but the managed one made a single unload, so the second meta-kernel's load stayed in SPICE, which
    /// read the replaced data and not the new file's (review of #706, measured on 9338a40: the state at 60 s read at the old
    /// x, the state at 180 s, which only the new file holds, not read). Both now unload every load: the meta-kernels stay
    /// listed, the object is not read until the file is loaded again, and then from the new file. References: the states
    /// written and SPICE's coverage.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Writers_ReplacingAFileTwoMetaKernelsHold_UnloadEveryLoad(bool managed)
    {
        int id = managed ? -1999726 : -1999727;
        var c = new FileInfo(Path.Combine(_root.FullName, "held.bsp"));
        WriteStates(c, id, EarthStates(3, 6800000.0), managed);
        var mk1 = WriteMetaKernel(new FileInfo(Path.Combine(_root.FullName, "mk1.tm")), c.FullName);
        var mk2 = WriteMetaKernel(new FileInfo(Path.Combine(_root.FullName, "mk2.tm")), c.FullName);
        SpiceAPI.Instance.LoadKernels(mk1);
        SpiceAPI.Instance.LoadKernels(mk2);
        Assert.Equal(6800000.0, ReadObject(id, 60.0).Position.X);
        var written = EarthStates(4, 6805000.0);

        WriteStates(c, id, written, managed);

        Assert.Equal([mk1.FullName, mk2.FullName], Under(_root).Select(k => k.FullName));
        Assert.Throws<SpiceException>(() => ReadObject(id, 60.0));
        SpiceAPI.Instance.LoadKernels(c);
        Assert.Equal(written[3].Position, ReadObject(id, 180.0).Position);
    }

    /// <summary>
    /// A file loaded under the name of a symbolic link to it, or in a folder loaded through a symbolic link, then replaced by
    /// each writer: the writers compared names as strings, so the link's load stayed in SPICE and listed, its replaced data
    /// read (review of #706, measured on 9338a40 for the native writer: the state at 60 s read at the old x, the link
    /// listed). A load whose name resolves through the replaced file is now unloaded as <see cref="SpiceAPI.UnloadKernels"/>
    /// does: the link no longer listed, the linked folder replaced by its other file, the object not read until it is loaded
    /// again, and then from the new file. References: the states written and SPICE's coverage.
    /// </summary>
    [NotOnWindowsTheory("Creating a symbolic link needs a privilege on Windows")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Writers_ReplacingAFileLoadedThroughASymbolicLink_UnloadIt(bool managed, bool linkedFolder)
    {
        int id = managed ? -1999728 : -1999729;
        var folder = Directory.CreateDirectory(Path.Combine(_root.FullName, "Real"));
        var c = new FileInfo(Path.Combine(folder.FullName, "c.bsp"));
        WriteStates(c, id, EarthStates(3, 6800000.0), managed);
        SpiceAPI.Instance.WriteEphemeris(new FileInfo(Path.Combine(folder.FullName, "other.bsp")), -1999730, EarthStates(3, 6800000.0));
        FileSystemInfo loaded = linkedFolder
            ? new DirectoryInfo(Directory.CreateSymbolicLink(Path.Combine(_root.FullName, "Linked"), folder.FullName).FullName)
            : new FileInfo(File.CreateSymbolicLink(Path.Combine(_root.FullName, "link.bsp"), c.FullName).FullName);
        SpiceAPI.Instance.LoadKernels(loaded);
        Assert.Equal(6800000.0, ReadObject(id, 60.0).Position.X);
        var written = EarthStates(4, 6805000.0);

        WriteStates(c, id, written, managed);

        Assert.Equal(linkedFolder ? [Path.Combine(loaded.FullName, "other.bsp")] : Array.Empty<string>(),
            Under(_root).Select(k => k.FullName));
        Assert.Throws<SpiceException>(() => ReadObject(id, 60.0));
        SpiceAPI.Instance.LoadKernels(c);
        Assert.Equal(written[3].Position, ReadObject(id, 180.0).Position);
    }

    /// <summary>
    /// A writer given a symbolic link replaces the link, not the file it names, as <c>File.Delete</c> and
    /// <c>std::filesystem::remove</c> do: the file stays loaded and listed, its data unchanged, and the link becomes the new
    /// file. A guard of the rule of the review of #706, which unloads the names that resolve through the replaced file only.
    /// References: the states written and the file's bytes.
    /// </summary>
    [NotOnWindowsTheory("Creating a symbolic link needs a privilege on Windows")]
    [InlineData(false)]
    [InlineData(true)]
    public void Writers_ReplacingASymbolicLink_KeepTheFileItNamedLoaded(bool managed)
    {
        int id = managed ? -1999736 : -1999737;
        var c = new FileInfo(Path.Combine(_root.FullName, "target.bsp"));
        WriteStates(c, id, EarthStates(3, 6800000.0), managed);
        var bytes = File.ReadAllBytes(c.FullName);
        var link = new FileInfo(File.CreateSymbolicLink(Path.Combine(_root.FullName, "link.bsp"), c.FullName).FullName);
        SpiceAPI.Instance.LoadKernels(c);
        var written = EarthStates(4, 6805000.0);

        WriteStates(link, id, written, managed);

        Assert.Equal([c.FullName], Under(_root).Select(k => k.FullName));
        Assert.Equal(bytes, File.ReadAllBytes(c.FullName));
        Assert.Null(new FileInfo(link.FullName).LinkTarget);
        Assert.Equal(6800000.0, ReadObject(id, 60.0).Position.X);
        SpiceAPI.Instance.LoadKernels(link);
        Assert.Equal(written[3].Position, ReadObject(id, 180.0).Position);
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
