// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using IO.Astrodynamics.Converters;
using IO.Astrodynamics.DTO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Kernels;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using CelestialBody = IO.Astrodynamics.DTO.CelestialBody;
using EquinoctialElements = IO.Astrodynamics.DTO.EquinoctialElements;
using Instrument = IO.Astrodynamics.Body.Spacecraft.Instrument;
using KeplerianElements = IO.Astrodynamics.DTO.KeplerianElements;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using StateOrientation = IO.Astrodynamics.DTO.StateOrientation;
using StateVector = IO.Astrodynamics.DTO.StateVector;
using Window = IO.Astrodynamics.DTO.Window;

namespace IO.Astrodynamics;

/// <summary>
///     API to communicate with IO.Astrodynamics
/// </summary>
public class SpiceAPI
{
    private static IntPtr _libHandle = IntPtr.Zero;
    private readonly List<KernelEntry> _kernels = [];

    /// <summary>A path given to <see cref="LoadKernels"/>, and the files its load handed SPICE (#673).</summary>
    private sealed record KernelEntry(FileSystemInfo Path, KernelFile[] Files);

    /// <summary>
    /// A file a load handed SPICE, the kernels SPICE reads again when it unloads it, and those whose reading may make that
    /// unload fail (<see cref="UnloadRereads"/>).
    /// </summary>
    private sealed record KernelFile(string Name, KernelRereads Reads, KernelRereads MayFailOn);

    /// <summary>
    ///     Instantiate API
    /// </summary>
    private SpiceAPI()
    {
        NativeLibrary.SetDllImportResolver(typeof(SpiceAPI).Assembly, Resolver);
    }

    public static SpiceAPI Instance { get; } = new();

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetSpiceVersionProxy();

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LoadKernelsProxy(string directoryPath);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool UnloadKernelsProxy(string directoryPath);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void FindWindowsOnDistanceConstraintProxy(Window searchWindow, int observerId,
        int targetId, string constraint, double value, string aberration, double stepSize, [In] [Out] Window[] windows);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void FindWindowsOnOccultationConstraintProxy(Window searchWindow, int observerId,
        int targetId,
        string targetFrame, string targetShape, int frontBodyId, string frontFrame, string frontShape,
        string occultationType,
        string aberration, double stepSize, [In] [Out] Window[] windows);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void FindWindowsOnCoordinateConstraintProxy(Window searchWindow, int observerId, int targetId,
        string frame, string coordinateSystem, string coordinate,
        string relationalOperator, double value, double adjustValue, string aberration, double stepSize,
        [In] [Out] Window[] windows);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void FindWindowsOnIlluminationConstraintProxy(Window searchWindow, int observerId,
        string illuminationSource, int targetBody, string fixedFrame,
        Planetodetic geodetic, string illuminationType, string relationalOperator, double value, double adjustValue,
        string aberration, double stepSize, string method, [In] [Out] Window[] windows);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void FindWindowsInFieldOfViewConstraintProxy(Window searchWindow, int observerId,
        int instrumentId, int targetId, string targetFrame, string targetShape, string aberration, double stepSize,
        [In] [Out] Window[] windows);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool ReadOrientationProxy(Window searchWindow, int spacecraftId, double tolerance,
        string frame, double stepSize, [In] [Out] StateOrientation[] stateOrientations);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool ReadEphemerisProxy(Window searchWindow, int observerId, int targetId,
        string frame, string aberration, double stepSize, [In] [Out] StateVector[] stateVectors);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetLastErrorProxy();

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool
        WriteEphemerisProxy(string filePath, int objectId, StateVector[] stateVectors, uint size);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool
        WriteOrientationProxy(string filePath, int objectId, StateOrientation[] stateOrientations, uint size);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern CelestialBody GetCelestialBodyInfoProxy(int celestialBodyId);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern FrameTransformation TransformFrameProxy(string fromFrame, string toFrame, double epoch);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern StateVector ReadEphemerisAtGivenEpochProxy(double epoch, int observerId, int targetId,
        string frame, string aberration);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern StateVector ConvertTLEToStateVectorProxy(string line1, string line2, string line3,
        double epoch);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern TLEElements GetTLEElementsProxy(string line1, string line2, string line3);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void KClearProxy();

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern DTO.KeplerianElements ConvertStateVectorToConicOrbitalElementProxy(DTO.StateVector stateVector, double mu);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern DTO.StateVector ConvertEquinoctialElementsToStateVectorProxy(DTO.EquinoctialElements equinoctialElementsDto);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern StateVector Propagate2BodiesProxy(StateVector stateVector, double mu, double dt);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern StateVector ConvertConicElementsToStateVectorProxy(KeplerianElements keplerianElements);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern StateVector ConvertConicElementsToStateVectorAtEpochProxy(KeplerianElements keplerianElements, double epoch, double gm);

    private static IntPtr Resolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        _libHandle = IntPtr.Zero;

        if (libraryName != "IO.Astrodynamics") return _libHandle;
        string sharedLibName = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            sharedLibName = "resources/IO.Astrodynamics.dll";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) sharedLibName = "resources/libIO.Astrodynamics.so";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) sharedLibName = "resources/libIO.Astrodynamics.dylib";

        if (!string.IsNullOrEmpty(sharedLibName))
            NativeLibrary.TryLoad(sharedLibName, typeof(SpiceAPI).Assembly, DllImportSearchPath.AssemblyDirectory, out _libHandle);
        else
            throw new PlatformNotSupportedException();

        return _libHandle;
    }

    //Use the same lock for all cspice calls because it doesn't support multithreading.
    // No code under it may read a static class whose type initializer takes it (PlanetsAndMoons, Stars, Barycenters):
    // the thread would wait for the initializer while it holds the lock, and a thread running the initializer would wait
    // for the lock, forever (#623). The library's code does not; a custom IDataProvider (the bodies the element
    // conversions build, the frames of a read in TEME, CIRS or TIRS) and the enumeration of the states given to
    // WriteEphemeris or WriteOrientation also run under it.
    private static object lockObject = new object();

    /// <summary>
    /// The name SPICE knows the axes of an inertial frame by. GCRF is not a SPICE frame: it has the axes of ICRF
    /// (SPICE <c>J2000</c>), the identity rotation of <see cref="GcrfFrame"/>, so a frame named GCRF, <see cref="Frame.GCRF"/>
    /// or <c>new Frame("GCRF")</c>, is handed to SPICE as <c>J2000</c>, and what SPICE returns is labelled with the frame
    /// the caller gave (#543). The name is compared ignoring case, as SPICE compares frame names. Every other name is
    /// handed as it is.
    /// </summary>
    internal static string SpiceFrameName(Frame frame) =>
        string.Equals(frame.Name, Frame.GCRF.Name, StringComparison.OrdinalIgnoreCase) ? Frame.ICRF.Name : frame.Name;

    /// <summary>
    /// Whether the frame is a TEME frame by its name, compared ignoring case as SPICE compares frame names: a frame
    /// built by name, <c>new Frame("TEME")</c>, stands for <see cref="Frame.TEME"/>, the managed <see cref="TemeFrame"/>
    /// (#451, #562).
    /// </summary>
    internal static bool IsTeme(Frame frame) =>
        string.Equals(frame.Name, Frame.TEME.Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The managed frame whose orientation an ephemeris read in <paramref name="frame"/> rotates by, or null for a frame
    /// that SPICE knows: an instance of <see cref="TemeFrame"/>, <see cref="CirsFrame"/> or <see cref="TirsFrame"/>, and
    /// <see cref="Frame.TEME"/> for a frame named TEME (#558). SPICE knows none of these, or, for TEME, the native
    /// construction that #451 replaced.
    /// </summary>
    internal static Frame ManagedReadFrame(Frame frame) => frame switch
    {
        TemeFrame or CirsFrame or TirsFrame => frame,
        _ when IsTeme(frame) => Frame.TEME,
        _ => null
    };

    /// <summary>
    /// A state read by SPICE in J2000, expressed in the managed frame of the caller (#558): rotated by
    /// <see cref="OrbitalParameters.OrbitalParameters.ToFrame"/> at the epoch of the read, as a state in ICRF converted
    /// by the caller would be, then labelled with the frame the caller gave.
    /// </summary>
    private static OrbitalParameters.StateVector ToManagedFrame(Vector3 position, Vector3 velocity, ILocalizable observer,
        Time epoch, Frame managed, Frame frame)
    {
        var rotated = new OrbitalParameters.StateVector(position, velocity, observer, epoch, Frame.ICRF).ToFrame(managed)
            .ToStateVector();
        return new OrbitalParameters.StateVector(rotated.Position, rotated.Velocity, observer, epoch, frame);
    }

    /// <summary>
    ///     Retrieve the last error message from the native proxy layer and throw an InvalidOperationException.
    /// </summary>
    private static void ThrowLastProxyError()
    {
        var ptr = GetLastErrorProxy();
        var msg = Marshal.PtrToStringAnsi(ptr);
        throw new InvalidOperationException(string.IsNullOrEmpty(msg) ? "Native proxy call failed" : msg);
    }

    /// <summary>
    ///     Get spice toolkit version number
    /// </summary>
    /// <returns></returns>
    public string GetSpiceVersion()
    {
        lock (lockObject)
        {
            var strptr = GetSpiceVersionProxy();
            var str = Marshal.PtrToStringAnsi(strptr);
            Marshal.FreeHGlobal(strptr);
            return str;
        }
    }

    /// <summary>
    /// The kernel paths loaded by <see cref="LoadKernels"/>, as they are at the call: a copy taken under the lock, so that
    /// another thread may load or unload kernels while the caller enumerates it.
    /// </summary>
    /// <remarks>
    /// After <see cref="UnloadKernels"/> of a file or a subdirectory of a loaded directory, the directory is no longer listed
    /// but each of its kernel files SPICE still holds, in its place.
    /// </remarks>
    public IEnumerable<FileSystemInfo> GetLoadedKernels()
    {
        lock (lockObject)
        {
            return _kernels.Select(x => x.Path).ToArray();
        }
    }

    /// <summary>
    ///     Load kernel at given path
    /// </summary>
    /// <param name="path">Path where kernels are located. This could be a file path or a directory path</param>
    /// <remarks>
    /// <para>
    /// A file path is handed to SPICE as it is. For a directory, the kernel files of the directory and of its
    /// subdirectories are loaded, in the order of the directory listing; the other files, from which SPICE would load
    /// nothing (a geopotential model, a readme), are left out (<see cref="IsKernelFile"/>). A path that does not exist
    /// loads nothing.
    /// </para>
    /// <para>
    /// Paths are compared by their components (#673): a path already loaded, or inside a loaded directory, reloads that
    /// entry; a directory first unloads the entries inside it; a sibling whose name extends a loaded one is another path.
    /// SPICE stores a kernel name of at most 255 characters (FURNSH), and the full path is handed to it.
    /// </para>
    /// </remarks>
    public void LoadKernels(FileSystemInfo path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        lock (lockObject)
        {
            var containing = _kernels.Where(x => IsSameOrInside(path.FullName, x.Path.FullName)).ToArray();
            if (containing.Length > 0)
            {
                foreach (var entry in containing)
                {
                    UnloadKernels(entry.Path);
                    LoadKernels(entry.Path);
                }

                return;
            }

            foreach (var inside in _kernels.Where(x => IsSameOrInside(x.Path.FullName, path.FullName)).ToArray())
            {
                UnloadKernels(inside.Path);
            }

            // Not the cached Exists of the instance, which a reload of a stored entry would trust after a deletion
            if (Exists(path))
            {
                var files = new List<KernelFile>();
                foreach (var kernel in KernelFiles(path))
                {
                    if (!LoadKernelsProxy(kernel))
                    {
                        throw new InvalidOperationException($"Kernel {kernel} can't be loaded. You can have more details on standard output");
                    }

                    // Read once SPICE has read it, so that a file SPICE cannot read keeps the error of its load
                    var (reads, mayFailOn) = UnloadRereads(kernel);
                    files.Add(new KernelFile(kernel, reads, mayFailOn));
                }

                _kernels.Add(new KernelEntry(path, files.ToArray()));
            }
        }
    }

    /// <summary>Whether the path exists now, as a directory for a <see cref="DirectoryInfo"/>, as a file otherwise.</summary>
    private static bool Exists(FileSystemInfo path) =>
        path is DirectoryInfo ? Directory.Exists(path.FullName) : File.Exists(path.FullName);

    /// <summary>
    /// Whether <paramref name="path"/> is <paramref name="root"/> or a path inside it, compared by components on the full
    /// paths without their trailing separator, ordinal: a sibling whose name extends <paramref name="root"/>
    /// (<c>Kernels2</c> beside <c>Kernels</c>) or a path that merely contains it is not inside it (#673).
    /// </summary>
    internal static bool IsSameOrInside(string path, string root)
    {
        path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!path.StartsWith(root, StringComparison.Ordinal))
        {
            return false;
        }

        // The same path; a root that keeps its separator (/ or C:\); or a separator after the root (GetFullPath writes
        // every separator as DirectorySeparatorChar)
        return path.Length == root.Length || Path.EndsInDirectorySeparator(root) || path[root.Length] == Path.DirectorySeparatorChar;
    }

    // Length of the scan buffer of IsKernelFile, and the marker of the data of a text kernel
    private const int KernelScanBufferLength = 65536;
    private static readonly byte[] BeginData = "\\begindata"u8.ToArray();

    // The variables FURNSH watches: a text kernel whose load assigns one of them is a meta-kernel (KEEPER)
    private static readonly string[] MetaKernelVariables = ["KERNELS_TO_LOAD", "PATH_SYMBOLS", "PATH_VALUES"];

    // What ends a variable name in a line of data, around the assignment operators = and +=, and the parentheses and commas
    // of a list
    private static readonly char[] DataSeparators = [' ', '\t', '\r', '\v', '\f', '=', '+', '(', ')', ','];

    /// <summary>
    /// The files that <see cref="LoadKernels"/> hands to SPICE for <paramref name="path"/>: the file itself, or the
    /// kernel files of a directory and of its subdirectories, in the order of the directory listing, as the native
    /// loader walks it.
    /// </summary>
    /// <remarks>
    /// The other files of a directory (a geopotential model, a readme) are left out: SPICE would load nothing from
    /// them, and opening them from the native layer could lock them on Windows while the library reads them
    /// (<see cref="IsKernelFile"/>).
    /// </remarks>
    internal static IEnumerable<string> KernelFiles(FileSystemInfo path)
    {
        if (path is not DirectoryInfo && !Directory.Exists(path.FullName))
        {
            yield return path.FullName;
            yield break;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(path.FullName))
        {
            if (Directory.Exists(entry))
            {
                foreach (var kernel in KernelFiles(new DirectoryInfo(entry)))
                {
                    yield return kernel;
                }
            }
            else if (HasContent(entry) && IsKernelFile(entry))
            {
                yield return entry;
            }
        }
    }

    /// <summary>
    /// Whether an entry of a directory, its symbolic links followed, is a file that holds at least one byte. The native
    /// loader skipped what is not a regular file: a dangling link has no target, and a named pipe or a device reports no
    /// length, while reading it could block. An empty file holds no ID word and no data, so SPICE loads nothing from it.
    /// </summary>
    private static bool HasContent(string entry)
    {
        try
        {
            var file = new FileInfo(entry);
            if (file.LinkTarget != null && file.ResolveLinkTarget(true) is FileInfo target)
            {
                file = target;
            }

            return file.Exists && file.Length > 0;
        }
        catch (IOException)
        {
            // A cycle of symbolic links: no target either
            return false;
        }
    }

    /// <summary>
    /// Whether SPICE can load anything from <paramref name="file"/>: a binary kernel, whose file starts with the ID word
    /// <c>DAF/</c> or <c>DAS/</c> (<c>NAIF/DAF</c> or <c>NAIF/DAS</c> for the oldest), or a text kernel, whose ID word is
    /// <c>KPL/</c> or, for an old one without ID word, which holds a <c>\begindata</c> marker: SPICE reads the variables
    /// of a text kernel only between <c>\begindata</c> and <c>\begintext</c> (NAIF, Kernel Required Reading, "SPICE
    /// Kernel Type Identification" and "Text Kernel Specifications").
    /// </summary>
    /// <remarks>
    /// The file is read with <see cref="FileShare.ReadWrite"/>, so the check never stands in the way of another reader
    /// or writer of the file.
    /// </remarks>
    internal static bool IsKernelFile(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Span<byte> buffer = new byte[KernelScanBufferLength];
        int read = stream.ReadAtLeast(buffer, buffer.Length, false);
        var head = buffer[..read];
        if (HasBinaryIdWord(head) || head.StartsWith("KPL/"u8))
        {
            return true;
        }

        return Contains(stream, buffer, read, BeginData);
    }

    /// <summary>
    /// Whether the rest of <paramref name="stream"/>, after the <paramref name="read"/> bytes already in
    /// <paramref name="buffer"/>, holds <paramref name="marker"/>, keeping the end of each buffer so that a marker across
    /// two reads is found.
    /// </summary>
    private static bool Contains(Stream stream, Span<byte> buffer, int read, ReadOnlySpan<byte> marker)
    {
        int overlap = marker.Length - 1;
        while (read > 0)
        {
            if (buffer[..read].IndexOf(marker) >= 0)
            {
                return true;
            }

            if (read < overlap)
            {
                return false;
            }

            buffer.Slice(read - overlap, overlap).CopyTo(buffer);
            int next = stream.ReadAtLeast(buffer[overlap..], buffer.Length - overlap, false);
            if (next == 0)
            {
                return false;
            }

            read = overlap + next;
        }

        return false;
    }

    // The ID words of a binary kernel, DAF or DAS (NAIF, Kernel Required Reading, "SPICE Kernel Type Identification")
    private static bool HasBinaryIdWord(ReadOnlySpan<byte> head) =>
        head.StartsWith("DAF/"u8) || head.StartsWith("DAS/"u8) || head.StartsWith("NAIF/DAF"u8) || head.StartsWith("NAIF/DAS"u8);

    /// <summary>
    /// The kernels SPICE reads again when it unloads <paramref name="file"/> (KEEPER's UNLOAD, measured with CSPICE N0067),
    /// and those whose reading may make that unload fail, from the ID word of the file and, for a text kernel, the variables
    /// its data assigns:
    /// <list type="bullet">
    /// <item>a text kernel: the text kernels still loaded, "Text kernels are unloaded by clearing the kernel pool and then
    /// reloading the other text kernels" (NAIF, Kernel Required Reading, "Unloading Kernels"); SPICE loads as a text
    /// kernel every file without the ID word of a binary one;</item>
    /// <item>an SPK, a CK, a binary PCK, a DSK or an EK: the other loaded kernels of its kind, which SPICE opens again
    /// (<c>unload_c --&gt; UNLOAD --&gt; SPKLEF</c>, <c>CKLPF</c>, <c>PCKLOF</c>, <c>ZZDSKLSF</c> or <c>EKLEF</c>), and not
    /// the text kernels;</item>
    /// <item>a DAF of the oldest ID word (<c>NAIF/DAF</c>), which SPICE loads as an SPK or a CK from its segments
    /// (<c>GETFAT --&gt; ZZCKSPK</c>): the library does not tell which, so it reads no kind for the failures before it,
    /// and its own failure, of SPKs or of CKs, is repaired by no later unload;</item>
    /// <item>a meta-kernel (<see cref="IsMetaKernel"/>): the text kernels still loaded, even when it names binary kernels
    /// only, and, through the unload of the kernels it names, the kernels of each of their kinds, which the library does not
    /// read from the meta-kernel: its failure counts as <see cref="KernelRereads.Unknown"/>;</item>
    /// <item>a file of another ID word, from which SPICE loads nothing: nothing for its reads, <see cref="KernelRereads.Unknown"/>
    /// for its failure.</item>
    /// </list>
    /// </summary>
    /// <remarks>A file that cannot be read counts as a kernel of no known kind (#673).</remarks>
    internal static (KernelRereads Reads, KernelRereads MayFailOn) UnloadRereads(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Span<byte> head = stackalloc byte[8];
            head = head[..stream.ReadAtLeast(head, head.Length, false)];
            if (head.StartsWith("DAF/SPK "u8)) return (KernelRereads.Spks, KernelRereads.Spks);
            if (head.StartsWith("DAF/CK  "u8)) return (KernelRereads.Cks, KernelRereads.Cks);
            if (head.StartsWith("DAF/PCK "u8)) return (KernelRereads.Pcks, KernelRereads.Pcks);
            if (head.StartsWith("DAS/DSK "u8)) return (KernelRereads.Dsks, KernelRereads.Dsks);
            if (head.StartsWith("DAS/EK  "u8)) return (KernelRereads.Eks, KernelRereads.Eks);
            if (head.StartsWith("NAIF/DAF"u8)) return (KernelRereads.None, KernelRereads.Spks | KernelRereads.Cks);
            if (HasBinaryIdWord(head)) return (KernelRereads.None, KernelRereads.Unknown);
            stream.Position = 0;
            return IsMetaKernel(stream)
                ? (KernelRereads.TextKernels, KernelRereads.TextKernels | KernelRereads.Unknown)
                : (KernelRereads.TextKernels, KernelRereads.TextKernels);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (KernelRereads.None, KernelRereads.Unknown);
        }
    }

    /// <summary>
    /// Whether SPICE loads the text kernel of <paramref name="stream"/> as a meta-kernel: FURNSH watches the variables
    /// <c>KERNELS_TO_LOAD</c>, <c>PATH_SYMBOLS</c> and <c>PATH_VALUES</c> and gives the type META to a text kernel whose
    /// load assigns one of them (KEEPER), whatever its comments say. Its data is read as RDKER reads it: the lines between
    /// a line <c>\begindata</c> and a line <c>\begintext</c>, each marker alone on its line but for blanks, tabs and the
    /// carriage return of a CRLF line (NAIF, Kernel Required Reading, "Text Kernel Specifications"); a name is a word of a
    /// line of data outside its quoted strings, the names of the pool compared case-sensitively (measured with CSPICE
    /// N0067, kinfo_c).
    /// </summary>
    /// <remarks>
    /// A word that SPICE would not read as a name (a malformed line) counts as one, which can only take a text kernel for a
    /// meta-kernel, whose failure is then never taken as repaired; such a line also makes SPICE fail the load.
    /// </remarks>
    private static bool IsMetaKernel(Stream stream)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.Latin1, false, KernelScanBufferLength, true);
        bool data = false;
        var line = new System.Text.StringBuilder();
        int next;
        do
        {
            next = reader.Read();
            if (next >= 0 && next != '\n')
            {
                line.Append((char)next);
                continue;
            }

            var text = line.ToString();
            line.Clear();
            var marker = text.Trim(' ', '\t', '\r', '\v', '\f');
            if (marker == "\\begindata")
            {
                data = true;
            }
            else if (marker == "\\begintext")
            {
                data = false;
            }
            else if (data && AssignsMetaKernelVariable(text))
            {
                return true;
            }
        } while (next >= 0);

        return false;
    }

    // Whether a line of data holds one of the variables FURNSH watches as a word outside its quoted strings ('' within a
    // string is a quote, which toggles the state twice)
    private static bool AssignsMetaKernelVariable(string line)
    {
        var unquoted = new System.Text.StringBuilder(line.Length);
        bool quoted = false;
        foreach (char c in line)
        {
            if (c == '\'')
            {
                quoted = !quoted;
                unquoted.Append(' ');
            }
            else
            {
                unquoted.Append(quoted ? ' ' : c);
            }
        }

        return unquoted.ToString().Split(DataSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Any(word => Array.IndexOf(MetaKernelVariables, word) >= 0);
    }

    /// <summary>
    ///     Unload kernel at given path
    /// </summary>
    /// <param name="path">Path where kernels are located. This could be a file path or a directory path</param>
    /// <remarks>
    /// The files a load handed SPICE are unloaded by their names, whether the path still exists or not (#673): every loaded
    /// entry that is the path or inside it is unloaded and removed; an entry that holds the path (a file or a subdirectory
    /// of a loaded directory) unloads the files it recorded there and is replaced, at its place, by its remaining files. A
    /// path that matches no entry is handed to SPICE as it is (the files of a directory as it is now), which does nothing
    /// for a name SPICE does not hold.
    /// </remarks>
    /// <exception cref="InvalidOperationException">SPICE failed to unload a file and no later unload of the call, of the same
    /// entry or of another one, repaired it: the unload of a text kernel or a meta-kernel reads the text kernels still loaded
    /// again, and that of an SPK, a CK, a binary PCK, a DSK or an EK opens the other loaded kernels of its kind again; each
    /// fails on a kernel deleted since its load, until a later unload that reads the same kernels again succeeds (#673).
    /// Every matched entry is unloaded and removed before the exception.</exception>
    public void UnloadKernels(FileSystemInfo path)
    {
        if (path == null) return;
        lock (lockObject)
        {
            bool matched = false;
            var unrepaired = new List<KernelFile>();
            for (int i = 0; i < _kernels.Count; i++)
            {
                var entry = _kernels[i];
                // The registry follows SPICE's list first: SPICE drops a file from it even when it reports a failure
                if (IsSameOrInside(entry.Path.FullName, path.FullName))
                {
                    _kernels.RemoveAt(i--);
                    matched = true;
                    UnloadFiles(entry.Files, unrepaired);
                    continue;
                }

                if (!IsSameOrInside(path.FullName, entry.Path.FullName))
                {
                    continue;
                }

                var removed = entry.Files.Where(x => IsSameOrInside(x.Name, path.FullName)).ToArray();
                if (removed.Length == 0)
                {
                    continue;
                }

                var remaining = entry.Files.Except(removed).Select(x => new KernelEntry(new FileInfo(x.Name), [x])).ToArray();
                _kernels.RemoveAt(i);
                _kernels.InsertRange(i, remaining);
                i += remaining.Length - 1;
                matched = true;
                UnloadFiles(removed, unrepaired);
            }

            if (!matched && !UnloadKernelsProxy(path.FullName))
            {
                throw new InvalidOperationException($"Kernel {path.FullName} can't be unloaded. You can have more details on standard output");
            }

            if (unrepaired.Count > 0)
            {
                throw new InvalidOperationException($"Kernel {unrepaired[0].Name} can't be unloaded. You can have more details on standard output");
            }
        }
    }

    /// <summary>
    /// Unloads files by their names. After it unloads a file, SPICE reads again the kernels of <see cref="UnloadRereads"/>
    /// still loaded, in their load order, and stops at one deleted since its load (<c>SPICE(FILEOPENFAILED)</c> for a text
    /// kernel, <c>SPICE(FILENOTFOUND)</c> for a binary one, measured with CSPICE N0067): the unload fails, and the text
    /// kernels after the deleted one leave the pool, or, for SPKs, CKs and binary PCKs, those before it take precedence over
    /// those after it (each <c>SPKLEF</c>, <c>CKLPF</c> or <c>PCKLOF</c> of a loaded file closes it and loads it again
    /// at the highest priority, and the files after the deleted one are not loaded again).
    /// A later unload that succeeds has read again every kernel of its kinds, which repairs the failures that came from
    /// those kinds only (#673). The failures no later unload of the call repaired are left in <paramref name="unrepaired"/>,
    /// in their order.
    /// </summary>
    private static void UnloadFiles(KernelFile[] files, List<KernelFile> unrepaired)
    {
        foreach (var file in files)
        {
            if (!UnloadKernelsProxy(file.Name))
            {
                unrepaired.Add(file);
            }
            else
            {
                unrepaired.RemoveAll(x => (x.MayFailOn & ~file.Reads) == KernelRereads.None);
            }
        }
    }

    /// <summary>
    /// Unloads every kernel SPICE holds and clears the kernel pool (<c>kclear_c</c>), and empties
    /// <see cref="GetLoadedKernels"/>.
    /// </summary>
    /// <remarks>
    /// The files are not unloaded one by one first: SPICE reads again the text kernels still loaded after each unload of
    /// one, which fails for a text kernel deleted since its load, while <c>kclear_c</c> reads nothing.
    /// </remarks>
    public void ClearKernels()
    {
        lock (lockObject)
        {
            KClearProxy();
            _kernels.Clear();
        }
    }

    /// <summary>
    /// The windows a CSPICE search wrote, as TDB times: an end at an end of the search window, the seconds passed for it,
    /// is that end. Where an ulp of the seconds is coarser than a tick (2017 to 2028, 1971 to 1983), the seconds of a
    /// tick made a <see cref="Time"/> again are the neighbouring tick for about 16 % of ticks, which put such an end a tick
    /// off the search window's, outside it or inside (#625). A transition inside is the nearest tick of its seconds,
    /// within the search window: the rounding is monotone, and where an ulp is coarser than a tick the double after the seconds of a
    /// tick is more than half a tick past that tick (the double before, more than half a tick before it).
    /// </summary>
    private static IEnumerable<TimeSystem.Window> WithinSearchWindow(Window[] windows, TimeSystem.Window searchWindow)
    {
        var start = searchWindow.StartDate.ToTDB();
        var end = searchWindow.EndDate.ToTDB();
        double startSeconds = start.TimeSpanFromJ2000().TotalSeconds;
        double endSeconds = end.TimeSpanFromJ2000().TotalSeconds;
        return windows.Where(x => !double.IsNaN(x.Start)).Select(x => new TimeSystem.Window(Label(x.Start), Label(x.End))).ToArray();

        Time Label(double seconds) =>
            seconds <= startSeconds ? start : seconds >= endSeconds ? end : Time.Create(seconds, TimeFrame.TDBFrame);
    }

    /// <summary>
    /// The relation CSPICE's geometry finder takes for <paramref name="relationalOperator"/> (#482). CSPICE knows only
    /// <c>&gt;</c>, <c>&lt;</c>, <c>=</c>, <c>ABSMIN</c>, <c>ABSMAX</c>, <c>LOCMIN</c> and <c>LOCMAX</c>, and the native
    /// parser of the relation ends the process on any other string (a C++ exception across P/Invoke).
    /// <see cref="RelationnalOperator.GreaterOrEqual"/> and <see cref="RelationnalOperator.LowerOrEqual"/> are sent as
    /// <c>&gt;</c> and <c>&lt;</c>: CSPICE's windows are closed, so for a quantity that crosses the value they already hold
    /// the instants where it equals the value, as the managed search's windows do; the two differ only where the quantity
    /// stays equal to the value over an interval, which <c>&gt;</c> leaves out. <see cref="RelationnalOperator.NotEqual"/>
    /// is never sent: <see cref="SearchWindows"/> forms it from <c>=</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not one of the enumeration.</exception>
    private static string SpiceRelation(RelationnalOperator relationalOperator) => relationalOperator switch
    {
        RelationnalOperator.Greater or RelationnalOperator.GreaterOrEqual => ">",
        RelationnalOperator.Lower or RelationnalOperator.LowerOrEqual => "<",
        RelationnalOperator.Equal => "=",
        RelationnalOperator.AbsoluteMin or RelationnalOperator.AbsoluteMax or RelationnalOperator.LocalMin
            or RelationnalOperator.LocalMax => relationalOperator.GetDescription(),
        _ => throw new ArgumentOutOfRangeException(nameof(relationalOperator), relationalOperator,
            "CSPICE's window searches accept Greater, GreaterOrEqual, Lower, LowerOrEqual, Equal, NotEqual, AbsoluteMin, " +
            "AbsoluteMax, LocalMin and LocalMax.")
    };

    /// <summary>
    /// The windows of a CSPICE search for <paramref name="relationalOperator"/>: the search itself, or for
    /// <see cref="RelationnalOperator.NotEqual"/>, which CSPICE has not, the rest of the search window once the instants
    /// of <c>=</c> are taken out, a window from each crossing to the next, as the managed search defines it (#482).
    /// </summary>
    private static IEnumerable<TimeSystem.Window> SearchWindows(TimeSystem.Window searchWindow, RelationnalOperator relationalOperator,
        Func<string, IEnumerable<TimeSystem.Window>> search)
    {
        if (relationalOperator != RelationnalOperator.NotEqual)
        {
            return search(SpiceRelation(relationalOperator));
        }

        var end = searchWindow.EndDate.ToTDB();
        var cursor = searchWindow.StartDate.ToTDB();
        var windows = new List<TimeSystem.Window>();
        foreach (var crossing in search("=").OrderBy(x => x.StartDate))
        {
            if (crossing.StartDate > cursor)
            {
                windows.Add(new TimeSystem.Window(cursor, crossing.StartDate));
            }

            if (crossing.EndDate > cursor)
            {
                cursor = crossing.EndDate;
            }
        }

        if (end > cursor)
        {
            windows.Add(new TimeSystem.Window(cursor, end));
        }

        return windows;
    }

    /// <summary>
    /// The windows where the distance from the observer to the target satisfies the relation: CSPICE's <c>gfdist</c>.
    /// </summary>
    /// <param name="searchWindow">The search window.</param>
    /// <param name="observerId">The NAIF identifier of the observer.</param>
    /// <param name="targetId">The NAIF identifier of the target.</param>
    /// <param name="relationalOperator">The relation: every value of the enumeration; see the remarks.</param>
    /// <param name="value">The distance, m: the native layer converts it to km for <c>gfdist_c</c> (#466).</param>
    /// <param name="aberration">The aberration correction.</param>
    /// <param name="stepSize">The step of the search.</param>
    /// <returns>The windows, in TDB.</returns>
    /// <remarks>
    /// CSPICE has no <c>&gt;=</c>, <c>&lt;=</c> or <c>!=</c> (#482): <see cref="RelationnalOperator.GreaterOrEqual"/>
    /// and <see cref="RelationnalOperator.LowerOrEqual"/> are searched as <c>&gt;</c> and <c>&lt;</c>, whose windows are
    /// closed (they differ only where the distance stays equal to the value over an interval);
    /// <see cref="RelationnalOperator.NotEqual"/> is the search window less the instants of <c>=</c>, a window from each
    /// crossing to the next, as the managed search gives it.
    /// </remarks>
    public IEnumerable<TimeSystem.Window> FindWindowsOnDistanceConstraint(TimeSystem.Window searchWindow, int observerId,
        int targetId, RelationnalOperator relationalOperator, double value, Aberration aberration,
        TimeSpan stepSize)
    {
        return SearchWindows(searchWindow, relationalOperator, relation => DistanceWindows(searchWindow, observerId, targetId, relation,
            value, aberration, stepSize));
    }

    private IEnumerable<TimeSystem.Window> DistanceWindows(TimeSystem.Window searchWindow, int observerId, int targetId, string relation,
        double value, Aberration aberration, TimeSpan stepSize)
    {
        lock (lockObject)
        {
            var windows = new Window[1000];
            for (var i = 0; i < 1000; i++)
            {
                windows[i] = new Window(double.NaN, double.NaN);
            }

            FindWindowsOnDistanceConstraintProxy(searchWindow.Convert(), observerId, targetId, relation, value,
                aberration.GetDescription(), stepSize.TotalSeconds, windows);
            return WithinSearchWindow(windows, searchWindow);
        }
    }

    /// <summary>
    /// Find time windows based on occultation constraint
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="observerId"></param>
    /// <param name="targetId"></param>
    /// <param name="targetShape"></param>
    /// <param name="frontBodyId"></param>
    /// <param name="frontShape"></param>
    /// <param name="occultationType"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    public IEnumerable<TimeSystem.Window> FindWindowsOnOccultationConstraint(TimeSystem.Window searchWindow, int observerId,
        int targetId, ShapeType targetShape, int frontBodyId, ShapeType frontShape,
        OccultationType occultationType, Aberration aberration, TimeSpan stepSize)
    {
        return FindWindowsOnOccultationConstraint(searchWindow, observerId, targetId, targetShape, frontBodyId, frontShape, occultationType,
            aberration, stepSize, null, null);
    }

    /// <summary>
    /// Find time windows based on occultation constraint (CSPICE's <c>gfoclt</c>), with the body-fixed frames that
    /// orient the ellipsoids
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="observerId"></param>
    /// <param name="targetId"></param>
    /// <param name="targetShape"></param>
    /// <param name="frontBodyId"></param>
    /// <param name="frontShape"></param>
    /// <param name="occultationType"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <param name="frontFrame">The body-fixed frame of the front body's ellipsoid (<c>fframe</c>), or null for the
    /// frame the kernels associate with the body.</param>
    /// <param name="targetFrame">The body-fixed frame of the target's ellipsoid (<c>bframe</c>), or null for the frame
    /// the kernels associate with the body.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">A body modeled as an ellipsoid without a frame given is not a celestial body
    /// (<see cref="IsCelestialBodyId"/>): its frame is read from the body's information, which the native layer ended the
    /// process to read for a named spacecraft (#560).</exception>
    public IEnumerable<TimeSystem.Window> FindWindowsOnOccultationConstraint(TimeSystem.Window searchWindow, int observerId,
        int targetId, ShapeType targetShape, int frontBodyId, ShapeType frontShape,
        OccultationType occultationType, Aberration aberration, TimeSpan stepSize, Frame frontFrame, Frame targetFrame)
    {
        // The frame of an ellipsoid the kernels associate with a body is read from the body's information, which the native
        // layer ends the process to read for a named spacecraft or instrument (#560): such an ellipsoid is rejected first
        if (frontShape == ShapeType.Ellipsoid && frontFrame == null) ThrowIfNotCelestialBody(frontBodyId, nameof(frontBodyId));
        if (targetShape == ShapeType.Ellipsoid && targetFrame == null) ThrowIfNotCelestialBody(targetId, nameof(targetId));
        lock (lockObject)
        {
            string frontFrameName = frontShape == ShapeType.Ellipsoid
                ? frontFrame?.Name ?? GetCelestialBodyInfoProxy(frontBodyId).FrameName
                : string.Empty;
            string targetFrameName = targetShape == ShapeType.Ellipsoid
                ? targetFrame?.Name ?? GetCelestialBodyInfoProxy(targetId).FrameName
                : string.Empty;
            var windows = new Window[1000];
            for (var i = 0; i < 1000; i++)
            {
                windows[i] = new Window(double.NaN, double.NaN);
            }

            FindWindowsOnOccultationConstraintProxy(searchWindow.Convert(), observerId, targetId, targetFrameName, targetShape.GetDescription(),
                frontBodyId, frontFrameName, frontShape.GetDescription(), occultationType.GetDescription(), aberration.GetDescription(), stepSize.TotalSeconds, windows);
            return WithinSearchWindow(windows, searchWindow);
        }
    }

    /// <summary>
    /// Find time windows based on coordinate constraint
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="observerId"></param>
    /// <param name="targetId"></param>
    /// <param name="frame"></param>
    /// <param name="coordinateSystem"></param>
    /// <param name="coordinate"></param>
    /// <param name="relationalOperator"></param>
    /// <param name="value">The value of the coordinate, as <c>gfposc_c</c> takes it: km for a distance (X, Y, Z, range,
    /// radius, altitude), rad for an angle. Unlike <see cref="FindWindowsOnDistanceConstraint"/>, which takes metres (#466).</param>
    /// <param name="adjustValue">The adjustment of the absolute extremes, in the unit of <paramref name="value"/>.</param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    /// <remarks>
    /// CSPICE has no <c>&gt;=</c>, <c>&lt;=</c> or <c>!=</c> (#482): <see cref="RelationnalOperator.GreaterOrEqual"/>
    /// and <see cref="RelationnalOperator.LowerOrEqual"/> are searched as <c>&gt;</c> and <c>&lt;</c>, whose windows are
    /// closed (they differ only where the quantity stays equal to the value over an interval);
    /// <see cref="RelationnalOperator.NotEqual"/> is the search window less the instants of <c>=</c>, a window from each
    /// crossing to the next, as the managed search gives it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<TimeSystem.Window> FindWindowsOnCoordinateConstraint(TimeSystem.Window searchWindow, int observerId,
        int targetId, Frame frame, CoordinateSystem coordinateSystem, Coordinate coordinate,
        RelationnalOperator relationalOperator, double value, double adjustValue, Aberration aberration,
        TimeSpan stepSize)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));
        return SearchWindows(searchWindow, relationalOperator, relation =>
        {
            lock (lockObject)
            {
                var windows = new Window[1000];
                for (var i = 0; i < 1000; i++)
                {
                    windows[i] = new Window(double.NaN, double.NaN);
                }

                FindWindowsOnCoordinateConstraintProxy(searchWindow.Convert(), observerId, targetId,
                    SpiceFrameName(frame), coordinateSystem.GetDescription(),
                    coordinate.GetDescription(), relation, value, adjustValue,
                    aberration.GetDescription(), stepSize.TotalSeconds, windows);
                return WithinSearchWindow(windows, searchWindow);
            }
        });
    }

    /// <summary>
    /// Find time windows based on an illumination constraint at a surface point, CSPICE's <c>gfilum_c</c>.
    /// </summary>
    /// <param name="searchWindow">The search window.</param>
    /// <param name="observerId">The NAIF id of the observer.</param>
    /// <param name="targetBodyId">The NAIF id of the body that carries the point.</param>
    /// <param name="fixedFrame">The body-fixed frame of the body.</param>
    /// <param name="planetodetic">The point: longitude and latitude in rad, altitude in m above the ellipsoid of the body
    /// (its equatorial radius and flattening in the kernels).</param>
    /// <param name="illuminationType">The illumination angle.</param>
    /// <param name="relationalOperator">The relational operator.</param>
    /// <param name="value">The angle, rad.</param>
    /// <param name="adjustValue">The adjustment of the absolute extremes, rad.</param>
    /// <param name="aberration">The aberration correction.</param>
    /// <param name="stepSize">The step of the search.</param>
    /// <param name="illuminationSourceId">The NAIF id of the illumination source.</param>
    /// <param name="method">The computation method of <c>gfilum_c</c>.</param>
    /// <returns>The windows where the constraint holds.</returns>
    /// <remarks>
    /// CSPICE has no <c>&gt;=</c>, <c>&lt;=</c> or <c>!=</c> (#482): <see cref="RelationnalOperator.GreaterOrEqual"/>
    /// and <see cref="RelationnalOperator.LowerOrEqual"/> are searched as <c>&gt;</c> and <c>&lt;</c>, whose windows are
    /// closed (they differ only where the quantity stays equal to the value over an interval);
    /// <see cref="RelationnalOperator.NotEqual"/> is the search window less the instants of <c>=</c>, a window from each
    /// crossing to the next, as the managed search gives it.
    /// Before 11.0.0 the altitude was read in km (#488).
    /// </remarks>
    public IEnumerable<TimeSystem.Window> FindWindowsOnIlluminationConstraint(TimeSystem.Window searchWindow, int observerId,
        int targetBodyId, Frame fixedFrame, Coordinates.Planetodetic planetodetic, IlluminationAngle illuminationType, RelationnalOperator relationalOperator,
        double value, double adjustValue, Aberration aberration, TimeSpan stepSize, int illuminationSourceId,
        string method = "Ellipsoid")
    {
        if (fixedFrame == null) throw new ArgumentNullException(nameof(fixedFrame));
        if (method == null) throw new ArgumentNullException(nameof(method));
        return SearchWindows(searchWindow, relationalOperator, relation =>
        {
            lock (lockObject)
            {
                var windows = new Window[1000];
                for (var i = 0; i < 1000; i++)
                {
                    windows[i] = new Window(double.NaN, double.NaN);
                }

                FindWindowsOnIlluminationConstraintProxy(searchWindow.Convert(), observerId,
                    illuminationSourceId.ToString(), targetBodyId, fixedFrame.Name,
                    // The native layer hands the altitude to georec_c beside a radius in km (#488)
                    (planetodetic with { Altitude = planetodetic.Altitude / 1000.0 }).Convert(),
                    illuminationType.GetDescription(), relation, value, adjustValue,
                    aberration.GetDescription(), stepSize.TotalSeconds, method, windows);
                return WithinSearchWindow(windows, searchWindow);
            }
        });
    }

    /// <summary>
    /// Find time window when a target is in instrument's field of view
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="observerId"></param>
    /// <param name="instrumentId"></param>
    /// <param name="targetId"></param>
    /// <param name="targetFrame"></param>
    /// <param name="targetShape"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<TimeSystem.Window> FindWindowsInFieldOfViewConstraint(TimeSystem.Window searchWindow, int observerId,
        int instrumentId, int targetId, Frame targetFrame, ShapeType targetShape, Aberration aberration, TimeSpan stepSize)
    {
        if (targetFrame == null) throw new ArgumentNullException(nameof(targetFrame));
        lock (lockObject)
        {
            var windows = new Window[1000];
            for (var i = 0; i < 1000; i++)
            {
                windows[i] = new Window(double.NaN, double.NaN);
            }

            var searchWindowDto = searchWindow.Convert();

            FindWindowsInFieldOfViewConstraintProxy(searchWindowDto, observerId, instrumentId, targetId, targetFrame.Name, targetShape.GetDescription(),
                aberration.GetDescription(), stepSize.TotalSeconds, windows);

            return WithinSearchWindow(windows, searchWindow);
        }
    }

    /// <summary>
    /// Read object ephemeris for a given period using NAIF IDs directly.
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="observerId"></param>
    /// <param name="targetId"></param>
    /// <param name="frame"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">SPICE gives no state at one of the epochs (#454).</exception>
    public IEnumerable<OrbitalParameters.OrbitalParameters> ReadEphemeris(TimeSystem.Window searchWindow,
        int observerId, int targetId, Frame frame,
        Aberration aberration, TimeSpan stepSize)
    {
        ArgumentNullException.ThrowIfNull(frame);
        // Built before the lock (#623): the observer of a Lagrange point reads Barycenters, whose type initializer takes it
        var observer = CelestialItem.Create(observerId);
        lock (lockObject)
        {
            const int messageSize = 10000;
            List<OrbitalParameters.OrbitalParameters> orbitalParameters = [];
            int occurrences = (int)(searchWindow.Length / stepSize / messageSize);

            for (int i = 0; i <= occurrences; i++)
            {
                var start = searchWindow.StartDate + i * messageSize * stepSize;
                var end = start + messageSize * stepSize > searchWindow.EndDate ? searchWindow.EndDate : (start + messageSize * stepSize) - stepSize;
                var window = new TimeSystem.Window(start.ToTDB(), end.ToTDB());
                var stateVectors = new StateVector[messageSize];
                // A managed frame is read in J2000, then rotated (#558)
                var managed = ManagedReadFrame(frame);
                if (!ReadEphemerisProxy(window.Convert(), observerId, targetId, managed == null ? SpiceFrameName(frame) : Frame.ICRF.Name,
                    aberration.GetDescription(), stepSize.TotalSeconds,
                    stateVectors))
                    ThrowLastProxyError();
                orbitalParameters.AddRange(stateVectors.Where(x => !string.IsNullOrEmpty(x.Frame)).Select(x =>
                {
                    ThrowOnFailedRead(x);
                    var epoch = Time.CreateTDB(x.Epoch).ConvertTo(searchWindow.StartDate.Frame);
                    return managed == null
                        ? new OrbitalParameters.StateVector(x.Position.Convert(), x.Velocity.Convert(), observer, epoch, frame)
                        : ToManagedFrame(x.Position.Convert(), x.Velocity.Convert(), observer, epoch, managed, frame);
                }));
            }

            return orbitalParameters;
        }
    }

    /// <summary>
    /// Return state vector at given epoch using NAIF IDs directly.
    /// </summary>
    /// <param name="epoch"></param>
    /// <param name="observerId"></param>
    /// <param name="targetId"></param>
    /// <param name="frame"></param>
    /// <param name="aberration"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">SPICE gives no state (#454): the kernels do not cover the epoch or do
    /// not chain the target to the observer; the message is SPICE's. Before 11.0.0 the state was undefined.</exception>
    public OrbitalParameters.OrbitalParameters ReadEphemeris(Time epoch, int observerId,
        int targetId, Frame frame, Aberration aberration)
    {
        ArgumentNullException.ThrowIfNull(frame);
        // Built before the lock (#623): the observer of a Lagrange point reads Barycenters, whose type initializer takes it
        var observer = CelestialItem.Create(observerId);
        lock (lockObject)
        {
            return ReadEphemerisAtGivenEpoch(epoch, observer, targetId, frame, aberration);
        }
    }

    /// <summary>
    /// Read ephemeris at a given epoch using the caller-provided observer object.
    /// Avoids CelestialItem.Create() which can't handle spacecraft/site NAIF IDs.
    /// </summary>
    internal OrbitalParameters.StateVector ReadEphemerisRaw(Time epoch, ILocalizable observer,
        int targetId, Frame frame, Aberration aberration)
    {
        ArgumentNullException.ThrowIfNull(frame);
        lock (lockObject)
        {
            return ReadEphemerisAtGivenEpoch(epoch, observer, targetId, frame, aberration);
        }
    }

    /// <summary>
    /// The state of <paramref name="targetId"/> from <paramref name="observer"/> at <paramref name="epoch"/> (TDB label):
    /// in a frame SPICE knows, as <c>spkezr_c</c> returns it; in a managed frame, read in J2000 and rotated (#558).
    /// </summary>
    private static OrbitalParameters.StateVector ReadEphemerisAtGivenEpoch(Time epoch, ILocalizable observer, int targetId,
        Frame frame, Aberration aberration)
    {
        var managed = ManagedReadFrame(frame);
        var stateVector = ReadEphemerisAtGivenEpochProxy(epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds,
            observer.NaifId, targetId, managed == null ? SpiceFrameName(frame) : Frame.ICRF.Name, aberration.GetDescription());
        ThrowOnFailedRead(stateVector);
        // Labelled with the epoch asked, the instant SPICE read at, as SpiceDataProvider.GetEphemeris does: where an ulp
        // of the seconds is coarser than a tick (2017 to 2028, 1971 to 1983), the epoch SPICE returns, made a Time again,
        // is the neighbouring tick for about 16 % of ticks (#625)
        var tdb = epoch.ToTDB();
        return managed == null
            ? new OrbitalParameters.StateVector(stateVector.Position.Convert(), stateVector.Velocity.Convert(), observer, tdb, frame)
            : ToManagedFrame(stateVector.Position.Convert(), stateVector.Velocity.Convert(), observer, tdb, managed, frame);
    }

    // The bound of | |q| − 1 | of a rotation from the native layer, a unit quaternion to rounding; the zero quaternion of a
    // failed transformation is 1 away (#454)
    private const double UnitQuaternionTolerance = 1.0e-9;

    /// <summary>
    /// Throws the SPICE error of a failed read: <c>ReadEphemerisAtGivenEpochProxy</c> writes NaN in the state when
    /// <c>spkezr_c</c> fails and keeps the SPICE message for <c>GetLastErrorProxy</c> (#454); before, the state was the
    /// unwritten one of the stack. No valid state is non-finite.
    /// </summary>
    private static void ThrowOnFailedRead(in StateVector stateVector)
    {
        if (!double.IsFinite(stateVector.Position.X) || !double.IsFinite(stateVector.Position.Y) ||
            !double.IsFinite(stateVector.Position.Z) || !double.IsFinite(stateVector.Velocity.X) ||
            !double.IsFinite(stateVector.Velocity.Y) || !double.IsFinite(stateVector.Velocity.Z))
        {
            ThrowLastProxyError();
        }
    }

    /// <summary>
    ///     Read spacecraft orientation for a given period using a NAIF ID directly.
    ///     Use this overload to read CK kernels from external missions (NASA/ESA) where
    ///     you have only the NAIF ID, not a Spacecraft object.
    ///     Both the CK kernel and the corresponding SCLK kernel must be loaded via
    ///     LoadKernels() before calling this method.
    /// </summary>
    /// <param name="searchWindow">Time window to query</param>
    /// <param name="naifId">NAIF ID of the spacecraft (the CK bus frame ID is derived as naifId * 1000)</param>
    /// <param name="tolerance">Search tolerance — data within this duration of each step epoch will be returned</param>
    /// <param name="referenceFrame">Reference frame for the returned orientations</param>
    /// <param name="stepSize">Time step between successive query epochs</param>
    /// <returns>
    ///     The records of the CK, as <c>ckgpav_c</c> returns them: the C-matrix, the rotation from
    ///     <paramref name="referenceFrame"/> to the spacecraft frame, labelled with <paramref name="referenceFrame"/>, its
    ///     source (<c>vectorInSpacecraft = vectorInReference.Rotate(Rotation)</c>), and the angular velocity of the
    ///     spacecraft relative to <paramref name="referenceFrame"/>, expressed in <paramref name="referenceFrame"/>: the
    ///     convention of <see cref="Frame.ToFrame"/> (<c>xf2rav_c</c>) for that rotation, and that of
    ///     <see cref="Body.Spacecraft.Spacecraft.GetOrientation"/>.
    /// </returns>
    /// <remarks>
    ///     The rotation of a spacecraft's attitude in the frames' convention, spacecraft to reference, is its conjugate,
    ///     which <c>GetStateOrientationToICRF</c> of the frame of a kernel-backed spacecraft returns. On a CK written by
    ///     <see cref="Body.Spacecraft.Spacecraft.WriteOrientation"/> before 11.0.0, which holds the inverse of the
    ///     attitude (#529), this method returns that inverse.
    /// </remarks>
    public IEnumerable<OrbitalParameters.StateOrientation> ReadOrientation(TimeSystem.Window searchWindow,
        int naifId, TimeSpan tolerance,
        Frame referenceFrame, TimeSpan stepSize)
    {
        if (referenceFrame == null) throw new ArgumentNullException(nameof(referenceFrame));
        lock (lockObject)
        {
            var stateOrientations = new StateOrientation[10000];
            if (!ReadOrientationProxy(searchWindow.Convert(), naifId, tolerance.TotalSeconds,
                SpiceFrameName(referenceFrame), stepSize.TotalSeconds,
                stateOrientations))
                ThrowLastProxyError();
            // ByValTStr marshaling returns "" (not null) for zero-padded unwritten slots;
            // use IsNullOrEmpty to correctly exclude entries the proxy did not populate.
            return stateOrientations.Where(x => !string.IsNullOrEmpty(x.Frame)).Select(x => new OrbitalParameters.StateOrientation(
                x.Rotation.Convert(), x.AngularVelocity.Convert(), Time.Create(x.Epoch, TimeFrame.TDBFrame), referenceFrame));
        }
    }

    /// <summary>
    /// Writes the arcs of states of an object as an SPK of type 9, one segment per arc, through the managed writer
    /// (<see cref="Kernels.SpkWriter"/>, #540), in J2000: the native writer writes one segment per file. As the
    /// native writer, it unloads and replaces an existing file, creates its directory when needed, and leaves the new file
    /// unloaded (the native kernel unloads the file it wrote when it is destroyed, at the end of its proxy).
    /// </summary>
    /// <param name="filePath">The SPK; the caller's <see cref="FileInfo"/> is refreshed once the file is written.</param>
    /// <param name="naifId">The NAIF identifier of the object.</param>
    /// <param name="arcs">The arcs, in time order, each of at least two states in strictly increasing epoch order, in ICRF,
    /// relative to the centre of their first state.</param>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> or <paramref name="arcs"/> is null.</exception>
    /// <exception cref="ArgumentException">No arc, an arc of fewer than two states, or epochs not strictly increasing; an
    /// existing file is then left as it was.</exception>
    internal void WriteEphemerisArcs(FileInfo filePath, int naifId, IReadOnlyList<OrbitalParameters.StateVector[]> arcs)
    {
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));
        if (arcs == null) throw new ArgumentNullException(nameof(arcs));
        lock (lockObject)
        {
            // The file is built and checked first: an argument it cannot write leaves an existing file as it was
            var bytes = Kernels.SpkWriter.BuildType9(naifId, 1, arcs);

            // File.Exists, not FileInfo.Exists: the caller's FileInfo must not cache the state of the file before the write
            if (File.Exists(filePath.FullName))
            {
                UnloadKernelsProxy(filePath.FullName);
                File.Delete(filePath.FullName);
            }

            Kernels.SpkWriter.Write(filePath, bytes);
            filePath.Refresh();
        }
    }

    /// <summary>
    /// The lock of every CSPICE call, for the tests that call CSPICE directly in the process.
    /// </summary>
    internal static object SyncRoot => lockObject;

    /// <summary>
    /// Write ephemeris file using a NAIF ID directly.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="naifId"></param>
    /// <param name="stateVectors"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">The full path of <paramref name="filePath"/> is longer than the 255 characters
    /// (UTF-8 bytes) of a kernel name SPICE takes; nothing is written (#673: CSPICE wrote under the name truncated to 255,
    /// then failed).</exception>
    public bool WriteEphemeris(FileInfo filePath, int naifId,
        IEnumerable<OrbitalParameters.StateVector> stateVectors)
    {
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));
        if (stateVectors == null) throw new ArgumentNullException(nameof(stateVectors));
        Kernels.KernelPath.EnsureLoadable(filePath.FullName, nameof(filePath));
        lock (lockObject)
        {
            var enumerable = stateVectors as OrbitalParameters.StateVector[] ?? stateVectors.ToArray();
            if (!enumerable.Any())
                throw new ArgumentException("Value cannot be an empty collection.", nameof(stateVectors));
            bool res = WriteEphemerisProxy(filePath.FullName, naifId, stateVectors.Select(x => x.Convert()).ToArray(),
                (uint)enumerable.Length);
            if (res == false)
            {
                throw new InvalidOperationException(
                    "An error occurred while writing ephemeris. You can have more details on standard output.");
            }

            return true;
        }
    }

    /// <summary>
    /// Writes the given records as a type-3 CK for the spacecraft <paramref name="naifId"/> (frame
    /// <c>naifId * 1000</c>), unchanged, in one interval of one segment (<c>ckw03_c</c>).
    /// </summary>
    /// <param name="filePath">The CK to write.</param>
    /// <param name="naifId">The NAIF identifier of the spacecraft; its clock kernel must be loaded.</param>
    /// <param name="stateOrientations">
    /// The records in increasing epoch order, each the C-matrix: the rotation from its
    /// <see cref="OrbitalParameters.StateOrientation.ReferenceFrame"/> to the spacecraft frame, with the angular velocity of
    /// the spacecraft relative to that frame, expressed in it, as <see cref="ReadOrientation"/> returns them. The segment
    /// is tagged with the frame of the first record, so every record carries the same frame. Their epochs, encoded in the
    /// spacecraft clock, must be strictly increasing.
    /// </param>
    /// <returns>True when the CK is written.</returns>
    /// <remarks>
    /// <see cref="Body.Spacecraft.Spacecraft.WriteOrientation"/> writes the attitude record of a propagated spacecraft in this
    /// convention, the conjugate of the record's rotation, in J2000.
    /// </remarks>
    /// <exception cref="ArgumentException">The records are empty, or are not all in the frame of the first one: the native
    /// writer rejects them with a C++ exception that ended the process (#560, the comment on #454).</exception>
    /// <exception cref="ArgumentException">The full path of <paramref name="filePath"/> is longer than the 255 characters
    /// (UTF-8 bytes) of a kernel name SPICE takes; nothing is written (#673: CSPICE wrote under the name truncated to 255,
    /// then failed).</exception>
    public bool WriteOrientation(FileInfo filePath, int naifId, IEnumerable<OrbitalParameters.StateOrientation> stateOrientations)
    {
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));
        if (stateOrientations == null) throw new ArgumentNullException(nameof(stateOrientations));
        Kernels.KernelPath.EnsureLoadable(filePath.FullName, nameof(filePath));
        lock (lockObject)
        {
            var enumerable = stateOrientations as OrbitalParameters.StateOrientation[] ?? stateOrientations.ToArray();
            if (!enumerable.Any())
                throw new ArgumentException("Value cannot be an empty collection.", nameof(stateOrientations));
            string frame = enumerable[0].ReferenceFrame.Name;
            var other = enumerable.FirstOrDefault(x => !string.Equals(x.ReferenceFrame.Name, frame, StringComparison.Ordinal));
            if (other != null)
                throw new ArgumentException(
                    $"Every orientation must be in the frame of the first one, {frame}: the one at {other.Epoch} is in {other.ReferenceFrame.Name}.",
                    nameof(stateOrientations));
            bool res = WriteOrientationProxy(filePath.FullName, naifId, enumerable.Select(x => x.Convert()).ToArray(), (uint)enumerable.Length);
            if (res == false)
            {
                throw new InvalidOperationException(
                    "An error occurred while writing orientation. You can have more details on standard output.");
            }

            return true;
        }
    }

    /// <summary>
    /// Whether <paramref name="naifId"/> is an identifier the native layer can read as a celestial body: a barycenter (0 to
    /// 9), the Sun (10), a planet, a moon or a Lagrange point (101 to 999), or above 1000 (an asteroid, a comet, a star of
    /// the library). For any other identifier with a name (a spacecraft such as CASSINI, -82, an instrument), the native
    /// layer's search of the centre of motion throws a C++ exception across P/Invoke, which ends the process (#560).
    /// </summary>
    internal static bool IsCelestialBodyId(int naifId) => naifId is >= 0 and <= 10 or > 100 and < 1000 or > 1000;

    private static void ThrowIfNotCelestialBody(int naifId, string paramName)
    {
        if (!IsCelestialBodyId(naifId))
        {
            throw new ArgumentException(
                $"The NAIF identifier {naifId} is not a celestial body: a barycenter (0 to 9), the Sun (10), a planet, a moon or a " +
                "Lagrange point (101 to 999), or an identifier above 1000. A spacecraft or an instrument (a negative identifier) is not one.",
                paramName);
        }
    }

    /// <summary>
    ///     Get celestial celestialItem information like radius, GM, name, associated frame, ...
    /// </summary>
    /// <param name="naifId"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"><paramref name="naifId"/> is not the identifier of a celestial body
    /// (<see cref="IsCelestialBodyId"/>): a spacecraft, an instrument or a site of a negative identifier, 11 to 100, or 1000.
    /// The native layer reads the centre of motion of any named identifier and ended the process for one of them (#560).</exception>
    public CelestialBody GetCelestialBodyInfo(int naifId)
    {
        ThrowIfNotCelestialBody(naifId, nameof(naifId));
        lock (lockObject)
        {
            return GetCelestialBodyInfoProxy(naifId);
        }
    }

    /// <summary>
    /// Transform a frame to another
    /// </summary>
    /// <param name="fromFrame"></param>
    /// <param name="toFrame"></param>
    /// <param name="epoch"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <remarks>
    /// A TEME frame, <see cref="Frame.TEME"/> or a frame named TEME, is the managed <see cref="TemeFrame"/> (#451): the
    /// transform is composed through ICRF, the other frame from SPICE. SPICE must give a rotation: outside the coverage of
    /// the frame data the native layer returns the zero quaternion and drops the SPICE message, and the call throws (#454;
    /// before 11.0.0 the zero quaternion was returned, except on the TEME path).
    /// </remarks>
    /// <exception cref="InvalidOperationException">SPICE gives no rotation between the frames at this epoch: a quaternion
    /// whose norm differs from 1 by more than 1e-9.</exception>
    public OrbitalParameters.StateOrientation TransformFrame(Time epoch, Frame fromFrame, Frame toFrame)
    {
        if (fromFrame == null) throw new ArgumentNullException(nameof(fromFrame));
        if (toFrame == null) throw new ArgumentNullException(nameof(toFrame));
        if (IsTeme(fromFrame) || IsTeme(toFrame))
        {
            return TransformFrameThroughTeme(epoch, fromFrame, toFrame);
        }

        lock (lockObject)
        {
            var res = TransformFrameProxy(SpiceFrameName(fromFrame), SpiceFrameName(toFrame), epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds);

            // A failed transformation returns the zero quaternion, its SPICE message printed and dropped by the native layer
            // (#454): a rotation is a unit quaternion
            var rotation = new Quaternion(res.Rotation.W, res.Rotation.X, res.Rotation.Y, res.Rotation.Z);
            if (!(System.Math.Abs(rotation.Magnitude() - 1.0) <= UnitQuaternionTolerance))
            {
                throw new InvalidOperationException(
                    $"SPICE gives no transformation from {fromFrame.Name} to {toFrame.Name} at {epoch.ToTDB()}: the frame data (a PCK, CK or frame kernel) does not cover this epoch or is not loaded.");
            }

            return new OrbitalParameters.StateOrientation(
                rotation,
                new Vector3(res.AngularVelocity.X, res.AngularVelocity.Y, res.AngularVelocity.Z), epoch, fromFrame);
        }
    }

    /// <summary>
    /// The transform between a TEME frame and another frame, composed through ICRF as <see cref="Frame.ToFrame"/>, TEME by
    /// the managed <see cref="TemeFrame"/> and the other frame by SPICE, labelled with <paramref name="fromFrame"/>.
    /// </summary>
    private OrbitalParameters.StateOrientation TransformFrameThroughTeme(Time epoch, Frame fromFrame, Frame toFrame)
    {
        var fromToIcrf = OrientationToIcrfForTeme(epoch, fromFrame);
        var toToIcrf = OrientationToIcrfForTeme(epoch, toFrame);
        var rotation = toToIcrf.Rotation.Conjugate() * fromToIcrf.Rotation;
        var angularVelocity = toToIcrf.AngularVelocity.Inverse().Rotate(rotation.Conjugate()) + fromToIcrf.AngularVelocity;
        return new OrbitalParameters.StateOrientation(rotation, angularVelocity, epoch, fromFrame);
    }

    private OrbitalParameters.StateOrientation OrientationToIcrfForTeme(Time epoch, Frame frame)
    {
        if (IsTeme(frame))
        {
            return Frame.TEME.GetStateOrientationToICRF(epoch);
        }

        // TransformFrame throws when SPICE gives no rotation (#454)
        return TransformFrame(epoch, frame, Frame.ICRF);
    }

    public IEnumerable<OrbitalParameters.StateOrientation> TransformFrame(TimeSystem.Window window, Frame fromFrame, Frame toFrame, TimeSpan stepSize)
    {
        for (Time i = window.StartDate; i < window.EndDate; i += stepSize)
        {
            yield return TransformFrame(i, fromFrame, toFrame);
        }
    }

    /// <summary>
    /// Convert TLE to state vector at given epoch
    /// </summary>
    /// <param name="line1">The name of the object (the title line).</param>
    /// <param name="line2">Line 1 of the TLE.</param>
    /// <param name="line3">Line 2 of the TLE.</param>
    /// <param name="epoch">The instant of the state, in any time frame.</param>
    /// <returns>The SGP4 state in <see cref="Frame.TEME"/>, labelled <paramref name="epoch"/>.</returns>
    /// <remarks>
    /// <para>
    /// SGP4 is CSPICE's <c>evsgp4_c</c>: Vallado's 2006 code in the AFSPC mode with the WGS-72 "old" constants
    /// (KE = 7.43669161e-2, R = 6378.135 km, J2 = 1.082616e-3, J3 = −2.53881e-6, J4 = −1.65597e-6). tsince is the TT
    /// seconds from the TLE epoch (UTC, as the line gives it) to <paramref name="epoch"/> over 60 (#599): the call is at
    /// the epoch <c>getelm_c</c> reads plus those seconds.
    /// </para>
    /// <para>
    /// The state is in <see cref="Frame.TEME"/>, the managed TEME (#451). An SGP4 error (a decayed or suborbital orbit, an
    /// eccentricity out of range) gives a zero state from the native layer, which keeps the SPICE message on stdout only:
    /// it throws here instead (#454 on the TLE path). No valid state has r = 0.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">SGP4 failed at this epoch.</exception>
    public OrbitalParameters.OrbitalParameters ConvertTleToStateVector(string line1, string line2, string line3,
        Time epoch)
    {
        return ConvertTleToStateVector(line1, line2, line3, ReadTleEpoch(line1, line2, line3),
            OrbitalParameters.TLE.TLE.ExtractEpochFromTLE(line2), epoch);
    }

    /// <summary>
    /// The SGP4 state of a TLE at <paramref name="epoch"/>: <c>evsgp4_c</c> at <paramref name="sgp4Epoch"/>, the TLE epoch
    /// as <c>getelm_c</c> reads it, plus the TT seconds from <paramref name="tleEpoch"/>, the same epoch as a UTC
    /// <see cref="Time"/>, to <paramref name="epoch"/> (#599).
    /// </summary>
    /// <remarks>
    /// Only the native call runs under the lock; the Earth of the state is built after it is released (#623).
    /// </remarks>
    internal OrbitalParameters.OrbitalParameters ConvertTleToStateVector(string name, string line1, string line2,
        double sgp4Epoch, Time tleEpoch, Time epoch)
    {
        double et = sgp4Epoch + Propagator.PropagationTimeArgument.Tt.SecondsBetween(tleEpoch, epoch);

        // Nothing but the native call under the lock (#623): the Earth below reads PlanetsAndMoons, whose type initializer
        // takes this lock. Built under it, the Earth made this thread wait for that initializer while it held the lock,
        // and a thread running the initializer wait for the lock: both waited forever
        StateVector res;
        lock (lockObject)
        {
            res = ConvertTLEToStateVectorProxy(name, line1, line2, et);
        }

        var position = res.Position.Convert();
        if (position.Magnitude() == 0.0)
        {
            throw new InvalidOperationException(
                $"SGP4 failed for the TLE {name} ({line1}) at {epoch.ToTDB()}: the native layer returned a zero state (see the SPICE message on the standard output).");
        }

        return new OrbitalParameters.StateVector(position, res.Velocity.Convert(),
            new Body.CelestialBody(PlanetsAndMoons.EARTH, Frame.ECLIPTIC_J2000, epoch), epoch,
            Frame.TEME);
    }

    /// <summary>
    /// The epoch of a TLE as CSPICE's <c>getelm_c</c> reads it: TDB seconds from J2000 (ET), the epoch from which
    /// <c>evsgp4_c</c> counts tsince (#599).
    /// </summary>
    /// <param name="name">The name of the object.</param>
    /// <param name="line1">Line 1 of the TLE.</param>
    /// <param name="line2">Line 2 of the TLE.</param>
    internal double ReadTleEpoch(string name, string line1, string line2)
    {
        lock (lockObject)
        {
            return GetTLEElementsProxy(name, line1, line2).Epoch;
        }
    }

    public IO.Astrodynamics.OrbitalParameters.KeplerianElements ConvertStateVectorToConicOrbitalElement(IO.Astrodynamics.OrbitalParameters.StateVector stateVector)
    {
        return ConvertStateVectorToConicOrbitalElement(stateVector, stateVector.Observer.GM);
    }

    /// <summary>
    /// Convert state vector to Keplerian elements with an explicit GM value.
    /// </summary>
    /// <param name="stateVector"></param>
    /// <param name="gm">Gravitational parameter (m^3/s^2)</param>
    /// <returns></returns>
    public IO.Astrodynamics.OrbitalParameters.KeplerianElements ConvertStateVectorToConicOrbitalElement(IO.Astrodynamics.OrbitalParameters.StateVector stateVector, double gm)
    {
        lock (lockObject)
        {
            var svDto = stateVector.Convert();
            return ConvertStateVectorToConicOrbitalElementProxy(svDto, gm).Convert();
        }
    }

    public IO.Astrodynamics.OrbitalParameters.StateVector ConvertEquinoctialElementsToStateVector(IO.Astrodynamics.OrbitalParameters.EquinoctialElements equinoctialElements)
    {
        lock (lockObject)
        {
            return ConvertEquinoctialElementsToStateVectorProxy(equinoctialElements.Convert()).Convert();
        }
    }

    public IO.Astrodynamics.OrbitalParameters.StateVector ConvertConicElementsToStateVector(IO.Astrodynamics.OrbitalParameters.KeplerianElements keplerianElements)
    {
        // The native conic elements solve the elliptic Kepler equation when they are built, an iteration that may never
        // end on an open orbit (#372): an open orbit goes to SPICE CONICS at the epoch of its elements, as the other
        // overloads
        if (keplerianElements.E >= 1.0)
        {
            return ConvertConicElementsToStateVector(keplerianElements, keplerianElements.Epoch);
        }

        lock (lockObject)
        {
            return ConvertConicElementsToStateVectorProxy(keplerianElements.Convert()).Convert();
        }
    }

    public IO.Astrodynamics.OrbitalParameters.StateVector ConvertConicElementsToStateVector(IO.Astrodynamics.OrbitalParameters.KeplerianElements keplerianElements, Time epoch)
    {
        return ConvertConicElementsToStateVector(keplerianElements, epoch, keplerianElements.Observer.GM);
    }

    /// <summary>
    /// Convert Keplerian elements to state vector at a given epoch with an explicit GM value.
    /// </summary>
    /// <param name="keplerianElements"></param>
    /// <param name="epoch"></param>
    /// <param name="gm">Gravitational parameter (m^3/s^2)</param>
    /// <returns></returns>
    public IO.Astrodynamics.OrbitalParameters.StateVector ConvertConicElementsToStateVector(IO.Astrodynamics.OrbitalParameters.KeplerianElements keplerianElements, Time epoch, double gm)
    {
        lock (lockObject)
        {
            return ConvertConicElementsToStateVectorAtEpochProxy(keplerianElements.Convert(), epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds, gm)
                .Convert();
        }
    }

    public OrbitalParameters.StateVector Propagate2Bodies(OrbitalParameters.StateVector stateVector, TimeSpan dt)
    {
        return Propagate2Bodies(stateVector, stateVector.Observer.GM, dt);
    }

    /// <summary>
    /// Propagate a state vector using two-body mechanics with an explicit GM value.
    /// </summary>
    /// <param name="stateVector"></param>
    /// <param name="gm">Gravitational parameter (m^3/s^2)</param>
    /// <param name="dt"></param>
    /// <returns></returns>
    public OrbitalParameters.StateVector Propagate2Bodies(OrbitalParameters.StateVector stateVector, double gm, TimeSpan dt)
    {
        lock (lockObject)
        {
            return Propagate2BodiesProxy(stateVector.Convert(), gm, dt.TotalSeconds).Convert();
        }
    }

    public OrbitalParameters.StateVector Propagate2Bodies(OrbitalParameters.StateVector stateVector, Time targetEpoch)
    {
        return Propagate2Bodies(stateVector, targetEpoch - stateVector.Epoch);
    }
}