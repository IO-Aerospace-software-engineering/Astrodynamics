// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Kernels;
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
/// <see cref="SpiceAPI.TransformFrame(TimeSystem.Time, Frames.Frame, Frames.Frame)"/>, which throws once the frame is out of the pool. A text kernel
/// is closed by SPICE after its load, so a loaded text kernel can be deleted on every platform; a binary kernel (the copies
/// of the moon PCK of <c>Data/SolarSystem</c>) stays open, and the tests that delete a loaded one are skipped on Windows
/// (<see cref="NotOnWindowsFactAttribute"/>). The frame ids and frame class ids 1999901 and up are not defined by
/// <c>Data/SolarSystem</c> (<see cref="KernelShadowing"/>), and every kernel is unloaded in <see cref="Dispose"/>.
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

    // A meta-kernel that loads the given kernel. A string value of the kernel pool holds 80 characters: FURNSH cuts a longer
    // name of KERNELS_TO_LOAD there and does not load the kernel, as under the temporary folder of macOS. The name is written
    // in pieces of at most 79 characters, each but the last ended by '+', which FURNSH joins to the next one (measured with
    // CSPICE N0067)
    private static FileInfo WriteMetaKernel(FileInfo metaKernel, string kernel)
    {
        var pieces = kernel.Chunk(79).Select(x => new string(x)).ToArray();
        var values = string.Join("\n", pieces.Select((x, i) => i < pieces.Length - 1 ? $"'{x}+'" : $"'{x}'"));
        metaKernel.Directory!.Create();
        File.WriteAllText(metaKernel.FullName, $"KPL/MK\n\\begindata\nKERNELS_TO_LOAD = (\n{values}\n)\n\\begintext\n");
        return metaKernel;
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
        // unloads one, from the first, and stops at D: E leaves the pool. The unload of an SPK opens the other SPKs again and
        // reads no text kernel (NAIF, Kernel Required Reading, "Unloading Kernels"; measured with CSPICE N0067)
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
            var exception = Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.UnloadKernels(metaKernel));
            Assert.Contains(metaKernel.FullName, exception.Message);
            AssertUnknown("IOA_F02_MKB_E");
            SpiceAPI.Instance.UnloadKernels(gone);
        }

        Assert.Equal(60.0, RotationAngleDeg("IOA_F02_MKB_E"), 12);
        Assert.Equal([after.FullName], Under(_root).Select(k => k.FullName));
    }

    // A copy of the moon PCK of Data/SolarSystem whose one segment is given the frame class id classId, which
    // Data/SolarSystem does not define (KernelShadowing), and the reference frame referenceFrame (J2000, 1, in the moon
    // PCK). The layout is read from the file itself and checked: its DAF file record gives ND = 2 at byte 8, NI = 5 at
    // byte 12 and the first summary record at byte 76, which holds three doubles, then the segment's two doubles and its
    // five integers, the class id first, the reference frame second
    private static FileInfo WritePck(DirectoryInfo folder, string fileName, int classId, int referenceFrame = 1)
    {
        var bytes = File.ReadAllBytes("Data/SolarSystem/moon_pa_de421_1900-2050.bpc");
        Assert.Equal("DAF/PCK "u8.ToArray(), bytes[..8]);
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8)));
        Assert.Equal(5, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12)));
        Assert.Equal("LTL-IEEE"u8.ToArray(), bytes[88..96]);
        int summaries = (BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(76)) - 1) * 1024;
        Assert.Equal(1.0, BinaryPrimitives.ReadDoubleLittleEndian(bytes.AsSpan(summaries + 16)));
        int classIdOffset = summaries + 24 + 2 * 8;
        Assert.Equal(31006, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(classIdOffset)));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(classIdOffset), classId);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(classIdOffset + 4)));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(classIdOffset + 4), referenceFrame);
        folder.Create();
        var file = new FileInfo(Path.Combine(folder.FullName, fileName));
        File.WriteAllBytes(file.FullName, bytes);
        return file;
    }

    // Writes files named by attempt until the folder lists the first one written by write(i) before the given file: a folder
    // is loaded in the order of its listing, the file system's
    private static FileInfo ListedBefore(DirectoryInfo folder, FileInfo listedLast, Func<int, FileInfo> write)
    {
        for (int i = 0; i < 64; i++)
        {
            var candidate = write(i);
            var listing = Directory.EnumerateFileSystemEntries(folder.FullName).ToArray();
            if (listing.Length == 2 && listing[0] == candidate.FullName && listing[1] == listedLast.FullName)
            {
                return candidate;
            }

            candidate.Delete();
        }

        throw new InvalidOperationException($"no name lists a file before {listedLast.FullName}");
    }

    // A PCK frame of the class classId, centred on the Moon, and three loads: a PCK P of that class relative to J2000, a
    // folder of two PCKs of other classes (X listed before D), then a PCK Q of that class relative to ECLIPJ2000 (17), which
    // takes precedence over P (NAIF, PCK Required Reading: the PCK loaded last). Returns the folder, X, D and the rotations
    // of the frame from ICRF with P alone and with Q
    private (DirectoryInfo Folder, FileInfo X, FileInfo D, Astrodynamics.Math.Quaternion WithP, Astrodynamics.Math.Quaternion WithQ) LoadPcksAroundAFolder(int classId, string frameName)
    {
        var frame = new FileInfo(Path.Combine(_root.FullName, "Frame", "pck.tf"));
        frame.Directory!.Create();
        File.WriteAllText(frame.FullName,
            $"KPL/FK\n\\begindata\nFRAME_{frameName} = {classId}\nFRAME_{classId}_NAME = '{frameName}'\nFRAME_{classId}_CLASS = 2\n" +
            $"FRAME_{classId}_CLASS_ID = {classId}\nFRAME_{classId}_CENTER = 301\n\\begintext\n");
        var p = WritePck(new DirectoryInfo(Path.Combine(_root.FullName, "Before")), "p.bpc", classId);
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Pcks"));
        var d = WritePck(folder, "d.bpc", classId + 2);
        var x = ListedBefore(folder, d, i => WritePck(folder, $"x{i}.bpc", classId + 1));
        var q = WritePck(new DirectoryInfo(Path.Combine(_root.FullName, "After")), "q.bpc", classId, 17);
        SpiceAPI.Instance.LoadKernels(frame);
        SpiceAPI.Instance.LoadKernels(p);
        SpiceAPI.Instance.LoadKernels(folder);
        var withP = SpiceAPI.Instance.TransformFrame(Epoch, new Frames.Frame(frameName), Frames.Frame.ICRF).Rotation;
        SpiceAPI.Instance.LoadKernels(q);
        var withQ = SpiceAPI.Instance.TransformFrame(Epoch, new Frames.Frame(frameName), Frames.Frame.ICRF).Rotation;

        // The two rotations differ by the obliquity of the ecliptic, about 23.4°
        Assert.True(AngleBetweenDeg(withP, withQ) > 20.0);
        return (folder, x, d, withP, withQ);
    }

    // The angle of the rotation from a to b, in degrees (the sign of a quaternion is free)
    private static double AngleBetweenDeg(Astrodynamics.Math.Quaternion a, Astrodynamics.Math.Quaternion b)
    {
        double dot = a.W * b.W + a.VectorPart.X * b.VectorPart.X + a.VectorPart.Y * b.VectorPart.Y + a.VectorPart.Z * b.VectorPart.Z;
        return 2.0 * System.Math.Acos(System.Math.Min(1.0, System.Math.Abs(dot))) * Astrodynamics.Constants.Rad2Deg;
    }

    [NotOnWindowsFact("A loaded binary kernel cannot be deleted on Windows, where SPICE keeps it open")]
    public void UnloadKernels_OfAFolderOfTwoPcks_WhoseSecondIsDeleted_RestoresThePrecedence()
    {
        // Arrange: P, the folder [X, D] and Q, D deleted after the load. SPICE unloads a binary PCK by opening again the other
        // PCKs still loaded, in their load order (unload_c --> UNLOAD --> PCKLOF --> DAFOPR): the unload of X opens P again,
        // which then takes precedence over Q, and fails on D; the unload of D opens P and Q again, which restores the
        // precedence of Q (measured with CSPICE N0067)
        var (folder, _, d, _, withQ) = LoadPcksAroundAFolder(1999955, "IOA_F02_PCK_A");

        lock (SpiceAPI.SyncRoot)
        {
            File.Delete(d.FullName);

            // Act: the first unload fails and the second one repairs it
            SpiceAPI.Instance.UnloadKernels(folder);
        }

        // Assert: nothing of the folder is listed, and Q takes precedence over P again
        Assert.DoesNotContain(folder.FullName, Under(_root).Select(k => k.FullName));
        Assert.Equal(0.0, AngleBetweenDeg(withQ, SpiceAPI.Instance.TransformFrame(Epoch, new Frames.Frame("IOA_F02_PCK_A"), Frames.Frame.ICRF).Rotation), 9);
    }

    [NotOnWindowsFact("A loaded binary kernel cannot be deleted on Windows, where SPICE keeps it open")]
    public void UnloadKernels_OfAPck_WhenSpiceCannotOpenAnotherPckAgain_Throws_AndThePcksLoadedBeforeTakePrecedence()
    {
        // Arrange: P, the folder [X, D] and Q, D deleted after the load
        var (folder, x, d, withP, withQ) = LoadPcksAroundAFolder(1999958, "IOA_F02_PCK_B");

        lock (SpiceAPI.SyncRoot)
        {
            File.Delete(d.FullName);

            // Act: the unload of X opens P again, then fails on D, before Q
            var exception = Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.UnloadKernels(x));

            // Assert: P takes precedence over Q, loaded after it, until a PCK unload succeeds (measured with CSPICE N0067)
            Assert.Contains(x.FullName, exception.Message);
            Assert.Equal(0.0, AngleBetweenDeg(withP, SpiceAPI.Instance.TransformFrame(Epoch, new Frames.Frame("IOA_F02_PCK_B"), Frames.Frame.ICRF).Rotation), 9);
            SpiceAPI.Instance.UnloadKernels(d);
        }

        Assert.DoesNotContain(folder.FullName, Under(_root).Select(k => k.FullName));
        Assert.Equal(0.0, AngleBetweenDeg(withQ, SpiceAPI.Instance.TransformFrame(Epoch, new Frames.Frame("IOA_F02_PCK_B"), Frames.Frame.ICRF).Rotation), 9);
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
        ListedBefore(folder, gone, i =>
        {
            var file = WriteFrameKernel(folder, $"x{i}.tf", 1999965, "IOA_F02_CMT_X", 30.0);
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

    [NotOnWindowsFact("A loaded binary kernel cannot be deleted on Windows, where SPICE keeps it open")]
    public void UnloadKernels_OfAMetaKernelOfAPckThenATextKernel_WhenSpiceCannotOpenAnotherPckAgain_Throws()
    {
        // Arrange: a binary PCK D, deleted after its load, and a folder of a meta-kernel that names another PCK, listed
        // before a text kernel. The meta-kernel's unload unloads that PCK, which opens D again and fails; the text kernel's
        // unload reads the text kernels again, not the PCKs, and repairs nothing (measured with CSPICE N0067)
        var gone = WritePck(new DirectoryInfo(Path.Combine(_root.FullName, "Deleted")), "d.bpc", 1999952);
        var pck = WritePck(new DirectoryInfo(Path.Combine(_root.FullName, "Pck")), "p.bpc", 1999953);
        var folder = new DirectoryInfo(Path.Combine(_root.FullName, "Batch"));
        var text = WriteFrameKernel(folder, "x.tf", 1999954, "IOA_F02_MKP_X", 90.0);
        var metaKernel = ListedBefore(folder, text, i => WriteMetaKernel(new FileInfo(Path.Combine(folder.FullName, $"m{i}.tm")), pck.FullName));
        SpiceAPI.Instance.LoadKernels(gone);
        SpiceAPI.Instance.LoadKernels(folder);
        Assert.Equal(90.0, RotationAngleDeg("IOA_F02_MKP_X"), 12);

        lock (SpiceAPI.SyncRoot)
        {
            File.Delete(gone.FullName);

            // Act: before, the text kernel's success was taken for a repair of the meta-kernel's failure
            var exception = Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.UnloadKernels(folder));

            // Assert
            Assert.Contains(metaKernel.FullName, exception.Message);
            Assert.Equal([gone.FullName], Under(_root).Select(k => k.FullName));

            // The unload of the deleted PCK opens the others again without it
            SpiceAPI.Instance.UnloadKernels(gone);
        }

        Assert.Empty(Under(_root));
        AssertUnknown("IOA_F02_MKP_X");
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
