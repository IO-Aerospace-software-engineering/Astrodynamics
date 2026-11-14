// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Linq;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The kernels the kernel registry tests write (#673): TK frame kernels, meta-kernels, copies of the moon PCK of
/// <c>Data/SolarSystem</c> given frame class ids of their own, and the order of a folder's listing. Shared by
/// <c>KernelRegistryTests</c> and its child process <c>IO.Astrodynamics.Tests.KernelUnload</c>, which links this file; it
/// uses no test framework, and throws <see cref="InvalidDataException"/> where a file is not laid out as expected.
/// </summary>
internal static class TestKernelWriter
{
    /// <summary>A TK frame kernel rotated from J2000 by <paramref name="angleDeg"/> about z.</summary>
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

    /// <summary>
    /// A meta-kernel that loads the given kernel. A string value of the kernel pool holds 80 characters: FURNSH cuts a longer
    /// name of KERNELS_TO_LOAD there and does not load the kernel, as under the temporary folder of macOS. The name is written
    /// in pieces of at most 79 characters, each but the last ended by '+', which FURNSH joins to the next one (measured with
    /// CSPICE N0067).
    /// </summary>
    internal static FileInfo WriteMetaKernel(FileInfo metaKernel, string kernel)
    {
        var pieces = kernel.Chunk(79).Select(x => new string(x)).ToArray();
        var values = string.Join("\n", pieces.Select((x, i) => i < pieces.Length - 1 ? $"'{x}+'" : $"'{x}'"));
        metaKernel.Directory!.Create();
        File.WriteAllText(metaKernel.FullName, $"KPL/MK\n\\begindata\nKERNELS_TO_LOAD = (\n{values}\n)\n\\begintext\n");
        return metaKernel;
    }

    /// <summary>
    /// A copy of the moon PCK of <c>Data/SolarSystem</c> (<paramref name="moonPck"/>) whose one segment is given the frame
    /// class id <paramref name="classId"/>, which <c>Data/SolarSystem</c> does not define (<c>KernelShadowing</c>), and the
    /// reference frame <paramref name="referenceFrame"/> (J2000, 1, in the moon PCK). The layout is read from the file itself
    /// and checked: its DAF file record gives ND = 2 at byte 8, NI = 5 at byte 12 and the first summary record at byte 76,
    /// which holds three doubles, then the segment's two doubles and its five integers, the class id first, the reference
    /// frame second.
    /// </summary>
    internal static FileInfo WritePck(string moonPck, DirectoryInfo folder, string fileName, int classId, int referenceFrame = 1)
    {
        var bytes = File.ReadAllBytes(moonPck);
        Expect(bytes.AsSpan(0, 8).SequenceEqual("DAF/PCK "u8), "ID word DAF/PCK");
        Expect(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8)) == 2, "ND = 2");
        Expect(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12)) == 5, "NI = 5");
        Expect(bytes.AsSpan(88, 8).SequenceEqual("LTL-IEEE"u8), "LTL-IEEE");
        int summaries = (BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(76)) - 1) * 1024;
        Expect(BinaryPrimitives.ReadDoubleLittleEndian(bytes.AsSpan(summaries + 16)) == 1.0, "one summary");
        int classIdOffset = summaries + 24 + 2 * 8;
        Expect(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(classIdOffset)) == 31006, "class id 31006");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(classIdOffset), classId);
        Expect(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(classIdOffset + 4)) == 1, "reference frame J2000");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(classIdOffset + 4), referenceFrame);
        folder.Create();
        var file = new FileInfo(Path.Combine(folder.FullName, fileName));
        File.WriteAllBytes(file.FullName, bytes);
        return file;
    }

    /// <summary>
    /// Writes files until the folder lists the one <paramref name="write"/> wrote before <paramref name="listedLast"/>, its
    /// only other file: a folder is loaded in the order of its listing (<see cref="SpiceAPI.KernelFiles"/>,
    /// <see cref="Directory.EnumerateFileSystemEntries(string)"/>), which is the file system's, alphabetical on NTFS, by
    /// hash or by creation on others. <paramref name="write"/> is given the stem of the name to write: a letter, from a to
    /// z in turn, then the attempt, so that one of the first stems sorts before any name and others fall anywhere in a hash
    /// order.
    /// </summary>
    internal static FileInfo ListedBefore(DirectoryInfo folder, FileInfo listedLast, Func<string, FileInfo> write)
    {
        for (int i = 0; i < 64; i++)
        {
            var candidate = write($"{(char)('a' + i % 26)}{i}");
            var listing = SpiceAPI.KernelFiles(folder).ToArray();
            if (listing.Length == 2 && listing[0] == candidate.FullName && listing[1] == listedLast.FullName)
            {
                return candidate;
            }

            candidate.Delete();
        }

        throw new InvalidOperationException($"no name lists a file before {listedLast.FullName}");
    }

    private static void Expect(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidDataException($"The moon PCK is not laid out as expected: {what}");
        }
    }
}
