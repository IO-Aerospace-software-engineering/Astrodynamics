// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
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

    // Every load of a file a writer replaces: each name KEEPER holds that resolves through it (review of #706)
    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool UnloadEveryLoadProxy(string path);

    // Whether a kernel name resolves through a file a writer replaces, the native rule of UnloadEveryLoadProxy (review of
    // #706); no SPICE call
    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool KernelNameResolvesThroughProxy(string name, string path);

    // frmnam_c: the name SPICE gives to a frame id, false when it knows none (#676)
    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool GetFrameNameProxy(int frameId, [Out] byte[] name);

    // namfrm_c and frinfo_c: the id, the centre and the class SPICE gives to a frame name, false when it knows none (#593)
    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool GetFrameInfoProxy(string frameName, out int frameId, out int centerId, out int frameClass);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool FindWindowsOnDistanceConstraintProxy(Window searchWindow, int observerId,
        int targetId, string constraint, double value, string aberration, double stepSize, [Out] Window[] windows, int capacity,
        out int count);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool FindWindowsOnOccultationConstraintProxy(Window searchWindow, int observerId,
        int targetId,
        string targetFrame, string targetShape, int frontBodyId, string frontFrame, string frontShape,
        string occultationType,
        string aberration, double stepSize, [Out] Window[] windows, int capacity, out int count);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool FindWindowsOnCoordinateConstraintProxy(Window searchWindow, int observerId, int targetId,
        string frame, string coordinateSystem, string coordinate,
        string relationalOperator, double value, double adjustValue, string aberration, double stepSize,
        [Out] Window[] windows, int capacity, out int count);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool FindWindowsOnIlluminationConstraintProxy(Window searchWindow, int observerId,
        string illuminationSource, int targetBody, string fixedFrame,
        Planetodetic geodetic, string illuminationType, string relationalOperator, double value, double adjustValue,
        string aberration, double stepSize, string method, [Out] Window[] windows, int capacity, out int count);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool FindWindowsInFieldOfViewConstraintProxy(Window searchWindow, int observerId,
        int instrumentId, int targetId, string targetFrame, string targetShape, string aberration, double stepSize,
        [Out] Window[] windows, int capacity, out int count);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool ReadOrientationProxy([In] double[] epochs, int count, int spacecraftId, double tolerance,
        string frame, [Out] StateOrientation[] stateOrientations);

    [DllImport(@"IO.Astrodynamics", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool ReadEphemerisProxy([In] double[] epochs, int count, int observerId, int targetId,
        string frame, string aberration, [Out] StateVector[] stateVectors);

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
    // for the lock, forever (#623). The library's code does not; a custom IDataProvider (the frames of a read in TEME,
    // CIRS or TIRS; no longer the bodies the element conversions built, #664, #725) also runs under it. The writers
    // enumerate the caller's states before it (#661).
    private static object lockObject = new object();

    // The generation of the kernel pool: raised, under the lock, by each LoadKernels, UnloadKernels and ClearKernels, so
    // that the static objects of Barycenters, PlanetsAndMoons and Stars read their data again after a change of the pool
    // (third round of #668)
    private static long _kernelPoolGeneration;

    // The native loads and unloads of kernels made, under the lock (LoadKernelNative, UnloadKernelNative): each first drops
    // in SPICE the deleted binary kernels (KernelsLoader), which the registry then follows (ForgetDroppedKernels). A call of
    // LoadKernels or UnloadKernels that made none, a path that does not exist or a directory without kernel files, leaves
    // the registry as it is, since SPICE dropped nothing (review of the fourth round of #668)
    private static long _nativeKernelCalls;

    /// <summary>
    /// The generation of the kernel pool, raised by each call of <see cref="LoadKernels"/>, <see cref="UnloadKernels"/> and
    /// <see cref="ClearKernels"/>, failed or not: data read from SPICE at a generation hold while it is the current one.
    /// </summary>
    internal static long KernelPoolGeneration => Interlocked.Read(ref _kernelPoolGeneration);

    /// <summary>
    /// The name SPICE knows the axes of an inertial frame by. GCRF is not a SPICE frame: it has the axes of ICRF
    /// (SPICE <c>J2000</c>), the identity rotation of <see cref="GcrfFrame"/>, so a frame named GCRF, <see cref="Frame.GCRF"/>
    /// or <c>new Frame("GCRF")</c>, is handed to SPICE as <c>J2000</c>, and what SPICE returns is labelled with the frame
    /// the caller gave (#543). The name is compared ignoring case, as SPICE compares frame names. Every other name is
    /// handed as it is.
    /// </summary>
    /// <remarks>
    /// The frame of a kernel-backed spacecraft (<see cref="Spacecraft.IsSpiceBacked"/>) is handed by the name the loaded
    /// kernels give to its id, NaifId × 1000 (<c>frmnam_c</c>), whatever the name of the spacecraft or of the mission's
    /// frame kernel, and by its own name when no loaded kernel defines that id (#676; before, always by its own name,
    /// <c>Name + "_FRAME"</c>). The lookup runs at each call, under the lock the caller holds, so a frame kernel loaded
    /// after the spacecraft is built is read. The frame of a managed spacecraft is handed by its name.
    /// The frame of a site from the kernels (<see cref="Surface.Site.IsSpiceBacked"/>) is handed the same way, by the name
    /// the loaded kernels give to its id, 1000000 + NaifId (DSS-13_TOPO for DSS-13, whatever the name of the site), and by
    /// its own name, <c>Name + "_TOPO"</c>, when no loaded kernel defines that id (#690; before, always by its own name, so
    /// that a site named otherwise than its kernel frame threw SPICE(UNKNOWNFRAME)); an ephemeris read in that frame when
    /// no loaded kernel defines it is the library's (<see cref="ManagedReadFrame"/>, third review of #690), so the name
    /// reaches SPICE only where SPICE reads the frame by name (<see cref="TransformFrame"/>, the CSPICE searches). The
    /// frame of a site built from coordinates is the library's (<see cref="ManagedReadFrame"/>).
    /// </remarks>
    internal static string SpiceFrameName(Frame frame)
    {
        if (frame is Body.Spacecraft.SpacecraftFrame { Spacecraft.IsSpiceBacked: true } or Surface.SiteFrame { Site.IsSpiceBacked: true } &&
            frame.Id is { } id)
        {
            return KernelFrameName(id) ?? frame.Name;
        }

        return string.Equals(frame.Name, Frame.GCRF.Name, StringComparison.OrdinalIgnoreCase) ? Frame.ICRF.Name : frame.Name;
    }

    /// <summary>
    /// Whether the loaded kernels define a frame of id <paramref name="frameId"/> (<c>frmnam_c</c>), read under the lock
    /// at each call, so that a frame kernel loaded or unloaded since is taken into account (#690).
    /// </summary>
    internal static bool KernelDefinesFrame(int frameId) => KernelFrameNameOf(frameId) != null;

    /// <summary>
    /// The name the loaded kernels give to the frame of id <paramref name="frameId"/> (<c>frmnam_c</c>), null when they
    /// define none; read under the lock at each call (#690).
    /// </summary>
    internal static string KernelFrameNameOf(int frameId)
    {
        lock (lockObject)
        {
            return KernelFrameName(frameId);
        }
    }

    // The name of the frame of id frameId in the loaded kernels, null when they define none; called under the lock
    private static string KernelFrameName(int frameId)
    {
        var name = new byte[33];
        if (!GetFrameNameProxy(frameId, name))
        {
            return null;
        }

        // frmnam_c writes at most 32 characters and the terminating null in the 33 bytes it is given
        return System.Text.Encoding.UTF8.GetString(name, 0, Array.IndexOf(name, (byte)0)).TrimEnd();
    }

    // The class of SPICE's inertial frames (frinfo_c; Frames Required Reading, "Frame Classes"), and the centre of TEME,
    // CIRS and TIRS, a literal: PlanetsAndMoons, whose type initializer takes the lock, is not read under it (#623)
    private const int InertialFrameClass = 1;
    private const int EarthNaifId = 399;

    /// <summary>
    /// The NAIF identifier of the centre of a non-inertial frame, at whose light time from the observer a light-time
    /// corrected state takes the orientation of the frame, as <c>spkezr_c</c> does (#593); null for an inertial frame, and
    /// for a frame SPICE does not know, whose transformation throws. The library's frames
    /// (<see cref="ManagedReadFrame"/>): the site of a <see cref="Surface.SiteFrame"/>, the spacecraft of a
    /// <see cref="Body.Spacecraft.SpacecraftFrame"/>, the Earth for TEME, CIRS and TIRS. Any other frame: SPICE's centre
    /// (<c>namfrm_c</c>, <c>frinfo_c</c>, through <c>GetFrameInfoProxy</c>), none for its inertial class (GCRF is handed as
    /// J2000, <see cref="SpiceFrameName"/>). Read under the lock at each call, so that a frame kernel loaded since is read.
    /// </summary>
    internal static int? NonInertialFrameCenter(Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        lock (lockObject)
        {
            switch (ManagedReadFrame(frame))
            {
                case Surface.SiteFrame siteFrame:
                    return siteFrame.Site.NaifId;
                case Body.Spacecraft.SpacecraftFrame spacecraftFrame:
                    return spacecraftFrame.Spacecraft.NaifId;
                case null:
                    break;
                default:
                    return EarthNaifId;
            }

            if (!GetFrameInfoProxy(SpiceFrameName(frame), out _, out int center, out int frameClass))
            {
                return null;
            }

            return frameClass == InertialFrameClass ? null : center;
        }
    }

    /// <summary>
    /// Whether the frame is a TEME frame by its name, compared ignoring case as SPICE compares frame names: a frame
    /// built by name, <c>new Frame("TEME")</c>, stands for <see cref="Frame.TEME"/>, the managed <see cref="TemeFrame"/>
    /// (#451, #562).
    /// </summary>
    internal static bool IsTeme(Frame frame) =>
        string.Equals(frame.Name, Frame.TEME.Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The managed frame of the IAU and SGP4 chains that <paramref name="frame"/> stands for, or null: an instance of
    /// <see cref="TemeFrame"/>, <see cref="CirsFrame"/> or <see cref="TirsFrame"/> as it is (its own Earth orientation
    /// parameters kept), and <see cref="Frame.TEME"/>, <see cref="Frame.CIRS"/> or <see cref="Frame.TIRS"/> for a frame of
    /// that name, compared ignoring case as SPICE compares frame names (#451, #562, #656: a frame is its name, so a frame
    /// built by name is routed as the frame it equals; before #656 CIRS and TIRS were routed by type only, and a frame built
    /// by their name reached SPICE, which does not know them). SPICE knows none of these frames.
    /// </summary>
    internal static Frame ManagedFrame(Frame frame) => frame switch
    {
        TemeFrame or CirsFrame or TirsFrame => frame,
        _ when IsTeme(frame) => Frame.TEME,
        _ when string.Equals(frame.Name, Frame.CIRS.Name, StringComparison.OrdinalIgnoreCase) => Frame.CIRS,
        _ when string.Equals(frame.Name, Frame.TIRS.Name, StringComparison.OrdinalIgnoreCase) => Frame.TIRS,
        _ => null
    };

    /// <summary>
    /// The orientation to ICRF of a frame that <see cref="ManagedFrame"/> resolves, labelled with <paramref name="source"/>,
    /// or null for any other frame: the routing of both data providers (#451, #656).
    /// </summary>
    internal static OrbitalParameters.StateOrientation ManagedOrientationToIcrf(in Time date, Frame source)
    {
        var managed = ManagedFrame(source);
        if (managed == null)
        {
            return null;
        }

        var orientation = managed.GetStateOrientationToICRF(date);
        return new OrbitalParameters.StateOrientation(orientation.Rotation, orientation.AngularVelocity, date, source);
    }

    /// <summary>
    /// The managed frame whose orientation an ephemeris read in <paramref name="frame"/> rotates by, or null for a frame
    /// that SPICE knows: the frame <see cref="ManagedFrame"/> resolves, an instance of <see cref="TemeFrame"/>,
    /// <see cref="CirsFrame"/> or <see cref="TirsFrame"/> (#558), or <see cref="Frame.TEME"/>, <see cref="Frame.CIRS"/> or
    /// <see cref="Frame.TIRS"/> for a frame of that name in any case (#656); the <see cref="Surface.SiteFrame"/> of a site built from coordinates
    /// and the <see cref="Body.Spacecraft.SpacecraftFrame"/> of a managed spacecraft whose attitude record holds an entry
    /// (#682), which only the library holds; the <see cref="Surface.SiteFrame"/> of a site of the kernels whose frame id,
    /// 1000000 + NaifId, no loaded kernel defines, the frame of its J2000 coordinates that
    /// <see cref="Surface.SiteFrame.GetStateOrientationToICRF"/> gives it (third review of #690; before, its own name,
    /// <c>Name + "_TOPO"</c>, reached <c>spkezr_c</c>, which threw SPICE(UNKNOWNFRAME), while the frame converted to ICRF
    /// and the horizontal coordinates used that orientation), decided at each read, under the lock the caller holds, so
    /// that a frame kernel loaded later is read by name. SPICE knows none of these, or, for TEME, the native construction
    /// that #451 replaced. The frame of a site or a spacecraft of the kernels is otherwise SPICE's (handed by the name the
    /// kernels give its id, <see cref="SpiceFrameName"/>; a <see cref="Surface.SiteFrame"/> of the kernels rotates by that
    /// same kernel frame, #690), and so is the frame of a managed spacecraft
    /// whose record is empty (never propagated, no orientation added, from a TLE or the TLE propagator): the library holds no
    /// attitude for it, and its name reaches SPICE as before 11.0.0, which reads the frame and C-kernels of that id when they
    /// are loaded (those the library wrote for an earlier object of the id) and rejects the frame otherwise
    /// (SPICE(UNKNOWNFRAME)), rather than the identity of the empty record. A frame built by name is SPICE's but for TEME,
    /// CIRS and TIRS (#656; before, CIRS and TIRS built by name reached <c>spkezr_c</c>, which threw SPICE(UNKNOWNFRAME)).
    /// </summary>
    /// <remarks>
    /// Called under the lock of the SPICE calls, which the lookup of a site's kernel frame (<c>frmnam_c</c>) needs.
    /// The rotation (<see cref="ToManagedFrame"/>) runs under the lock of the SPICE calls: a <see cref="Surface.SiteFrame"/> reads
    /// the frame of its body through SPICE, under the same lock, which is re-entrant, and no static class whose type
    /// initializer takes it (#623); a <see cref="Body.Spacecraft.SpacecraftFrame"/> takes the lock of its attitude record inside it, which is
    /// safe because no region of that lock calls SPICE.
    /// </remarks>
    internal static Frame ManagedReadFrame(Frame frame) => frame switch
    {
        Surface.SiteFrame { Site.IsSpiceBacked: false } => frame,
        Surface.SiteFrame { Site.IsSpiceBacked: true, Id: { } id } when KernelFrameName(id) == null => frame,
        Body.Spacecraft.SpacecraftFrame { Spacecraft.IsSpiceBacked: false, HasAttitude: true } => frame,
        _ => ManagedFrame(frame)
    };

    /// <summary>
    /// Whether <paramref name="frame"/> is one of the frames whose reads are the library's (<see cref="ManagedReadFrame"/>),
    /// decided under the lock of the SPICE calls at each call; false for a null frame. SPICE does not read these frames as
    /// the library does, or do not know them, so a CSPICE search cannot run in them as the library reads them: the
    /// coordinate searches of <see cref="Body.CelestialItem"/> and <see cref="Surface.Site"/> take the managed search in
    /// them (third review of #690; before, <c>gfposc_c</c> got the frame by name, which SPICE did not know unless a
    /// loaded kernel defined it, and its proxy dropped the error: no window).
    /// </summary>
    internal static bool IsManagedFrame(Frame frame)
    {
        if (frame == null)
        {
            return false;
        }

        lock (lockObject)
        {
            return ManagedReadFrame(frame) != null;
        }
    }

    /// <summary>
    /// Whether SPICE knows <paramref name="frame"/> by the name it is handed (<see cref="SpiceFrameName"/>), decided under
    /// the lock of the SPICE calls at each call: false for TEME, CIRS and TIRS, which only the library holds (a frame of
    /// those names included, #451, #656), and for the frame of a site or a spacecraft whose frame id no loaded kernel defines under that
    /// name; true for any other frame, whose name is handed as it is (third review of #690).
    /// </summary>
    internal static bool SpiceKnowsFrame(Frame frame)
    {
        lock (lockObject)
        {
            switch (frame)
            {
                case Surface.SiteFrame or Body.Spacecraft.SpacecraftFrame when frame.Id is { } id:
                    var kernelName = KernelFrameName(id);
                    return kernelName != null && string.Equals(kernelName, SpiceFrameName(frame), StringComparison.OrdinalIgnoreCase);
                default:
                    return ManagedFrame(frame) == null;
            }
        }
    }

    /// <summary>The one computation method of the illumination searches, CSPICE's <c>gfilum_c</c> (#682).</summary>
    internal const string EllipsoidMethod = "ELLIPSOID";

    /// <summary>
    /// The computation method of an illumination search, as <c>gfilum_c</c> takes it: "The only choice currently supported
    /// is "Ellipsoid"", "Neither case nor whitespaces are significant in `method'" (Detailed_Input <c>method</c>,
    /// https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/cspice/gfilum_c.html). ELLIPSOID is accepted whatever its case and
    /// its leading or trailing whitespace; a blank inside the word is rejected, as <c>gfilum_c</c> of N0067 rejects it
    /// (measured, SPICE(INVALIDMETHOD), #682), and so is any other method, DSK forms included, which N0067's <c>gfilum_c</c>
    /// does not support either, and the empty string (SPICE(EMPTYSTRING), Exceptions 18). Before 11.0.0 the native proxy
    /// dropped <c>gfilum_c</c>'s error (no window) and the managed search never read the method.
    /// </summary>
    /// <returns>The method as handed to <c>gfilum_c</c>: <see cref="EllipsoidMethod"/>.</returns>
    /// <exception cref="ArgumentNullException">The method is null.</exception>
    /// <exception cref="ArgumentException">The method is not ELLIPSOID.</exception>
    internal static string CheckIlluminationMethod(string method)
    {
        ArgumentNullException.ThrowIfNull(method);
        if (!string.Equals(method.Trim(), EllipsoidMethod, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"The illumination method '{method}' cannot apply: ELLIPSOID is the only method, the ellipsoid of the body's radii that the managed search models and the only one CSPICE's gfilum_c supports.",
                nameof(method));
        }

        return EllipsoidMethod;
    }

    /// <summary>
    /// A state read by SPICE in J2000, expressed in the managed frame of the caller (#558, #682): rotated by
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
    private static void ThrowLastProxyError() => throw new InvalidOperationException(LastProxyError());

    /// <summary>The last error message of the native proxy layer, or a generic message when it holds none.</summary>
    private static string LastProxyError()
    {
        var msg = Marshal.PtrToStringAnsi(GetLastErrorProxy());
        return string.IsNullOrEmpty(msg) ? "Native proxy call failed" : msg;
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
    /// <para>
    /// After <see cref="UnloadKernels"/> of a file or a subdirectory of a loaded directory, the directory is no longer listed
    /// but each of its kernel files SPICE still holds, in its place.
    /// </para>
    /// <para>
    /// An SPK, CK, binary PCK or EK whose file, named by its full path, was deleted leaves the listing at the next native load,
    /// unload or write, which closes and unloads it in SPICE (<see cref="UnloadKernels"/>): a path is listed only while SPICE
    /// holds a file of it, and a directory whose files were all dropped is no longer listed (fourth round of #668; before, the
    /// path was listed until it was unloaded). A call that hands SPICE nothing (<see cref="LoadKernels"/> of a path that does
    /// not exist or of a directory without kernel files, the unload of such a directory) leaves the listing as it is, since
    /// SPICE still holds the file; a symbolic link counts as deleted once its target is, as the native layer tests it. A text
    /// kernel, a meta-kernel or a DSK stays listed until it is unloaded.
    /// </para>
    /// <para>
    /// A file a writer replaces (<see cref="WriteEphemeris"/>, <see cref="WriteOrientation"/>, <c>Spacecraft.WriteEphemeris</c>
    /// and the writers that call them) leaves the listing before the write, unloaded as <see cref="UnloadKernels"/> unloads
    /// it, a directory being replaced by its remaining files; the new file is not loaded (#706: before, the native writers
    /// left it listed and SPICE read the replaced data, the managed one left it listed while SPICE no longer held it).
    /// </para>
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
    /// nothing (a geopotential model, a readme), are left out (<see cref="IsKernelFile"/>). The path is taken as what it
    /// is on disk, whatever its type: a <see cref="FileInfo"/> that names a directory loads the directory, a
    /// <see cref="DirectoryInfo"/> that names a file loads the file.
    /// </para>
    /// <para>
    /// Paths are compared by their components (#673): a path already loaded, or inside a loaded directory, reloads that
    /// entry; a directory first unloads the entries inside it; a sibling whose name extends a loaded one is another path.
    /// SPICE stores a kernel name of at most 255 characters (FURNSH), and the full path is handed to it.
    /// </para>
    /// <para>
    /// Each native load first closes and unloads every SPK, CK, binary PCK and EK whose file was deleted since its load, as
    /// <see cref="UnloadKernels"/> does (#668, second round); a kernel SPICE holds under a relative name, from a meta-kernel,
    /// is left to SPICE. <see cref="GetLoadedKernels"/> no longer lists a file so dropped once the call returns (fourth round
    /// of #668).
    /// </para>
    /// <para>
    /// Each call, as each <see cref="UnloadKernels"/> and <see cref="ClearKernels"/>, raises the generation of the kernel
    /// pool: the static objects of <see cref="Barycenters"/>, <see cref="PlanetsAndMoons"/> and <see cref="Stars"/> read on
    /// SPICE read their data again at their next read (third round of #668).
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">The path is neither a file nor a directory on disk: nothing is loaded, unloaded
    /// or reloaded. Before 11.0.0 such a path loaded nothing and the call returned.</exception>
    /// <exception cref="InvalidOperationException">SPICE failed to load a file, or the native file system calls rejected its
    /// path (a loop of symbolic links); the message is SPICE's or the file system's (#706: before, the message referred to
    /// the standard output, and a path the file system rejected ended the process).</exception>
    public void LoadKernels(FileSystemInfo path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        lock (lockObject)
        {
            long nativeCalls = _nativeKernelCalls;
            try
            {
                LoadKernelsLocked(path);
            }
            finally
            {
                if (_nativeKernelCalls != nativeCalls) ForgetDroppedKernels();
                Interlocked.Increment(ref _kernelPoolGeneration);
            }
        }
    }

    // LoadKernels under the lock
    private void LoadKernelsLocked(FileSystemInfo path)
    {
        // Before any unload or reload, so that a failed call changes nothing; not the cached Exists of the instance,
        // which a reload of a stored entry would trust after a deletion
        if (!Exists(path))
        {
            throw new ArgumentException($"The kernel path {path.FullName} does not exist.", nameof(path));
        }

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

        var files = new List<KernelFile>();
        foreach (var kernel in KernelFiles(path))
        {
            if (!LoadKernelNative(kernel))
            {
                throw new InvalidOperationException($"Kernel {kernel} can't be loaded: {LastProxyError()}");
            }

            // Read once SPICE has read it, so that a file SPICE cannot read keeps the error of its load
            var (reads, mayFailOn) = UnloadRereads(kernel);
            files.Add(new KernelFile(kernel, reads, mayFailOn));
        }

        _kernels.Add(new KernelEntry(path, files.ToArray()));
    }

    /// <summary>Whether the path is a file or a directory on disk now, whatever the type of <paramref name="path"/>.</summary>
    private static bool Exists(FileSystemInfo path) => File.Exists(path.FullName) || Directory.Exists(path.FullName);

    // The kinds of binary kernels the native layer closes and unloads once their files are deleted (KernelsLoader): the SPKs,
    // CKs, binary PCKs and EKs; a DAF of the oldest ID word, which KEEPER types SPK or CK, is told by WasDropped
    private const KernelRereads DroppedKinds = KernelRereads.Spks | KernelRereads.Cks | KernelRereads.Pcks | KernelRereads.Eks;

    // The native load of one kernel file, counted (_nativeKernelCalls)
    private static bool LoadKernelNative(string path)
    {
        _nativeKernelCalls++;
        return LoadKernelsProxy(path);
    }

    // The native unload of one kernel name, counted (_nativeKernelCalls)
    private static bool UnloadKernelNative(string path)
    {
        _nativeKernelCalls++;
        return UnloadKernelsProxy(path);
    }

    // The native unload of every load of a file a writer replaces, counted (_nativeKernelCalls)
    private static bool UnloadEveryLoadNative(string path)
    {
        _nativeKernelCalls++;
        return UnloadEveryLoadProxy(path);
    }

    /// <summary>
    /// Removes from the registry the files the native layer dropped on its own: each native load, unload or write first
    /// closes and unloads every SPK, CK, binary PCK and EK whose file, named by an absolute path, was deleted since its load
    /// (<c>KernelsLoader</c>, second round of #668), and tells nothing of it. The registry follows the same rule, called under
    /// the lock after each such native call, so that <see cref="GetLoadedKernels"/> never lists a file SPICE no longer holds
    /// (fourth round of #668): an entry left with no file is removed, a directory with remaining files stays listed. Text
    /// kernels, meta-kernels and DSKs are never removed. A file counts as deleted as <c>std::filesystem::exists</c> tells it:
    /// only a missing file or directory, any other error counting as existing, as the native layer keeps those kernels.
    /// </summary>
    private void ForgetDroppedKernels()
    {
        for (int i = 0; i < _kernels.Count; i++)
        {
            var entry = _kernels[i];
            var remaining = entry.Files.Where(x => !WasDropped(x)).ToArray();
            if (remaining.Length == entry.Files.Length)
            {
                continue;
            }

            if (remaining.Length == 0)
            {
                _kernels.RemoveAt(i--);
            }
            else
            {
                _kernels[i] = entry with { Files = remaining };
            }
        }
    }

    // Whether the native layer drops this file: a binary kernel of a kind it closes, whose file no longer exists
    private static bool WasDropped(KernelFile file)
    {
        bool droppedKind = (file.Reads & DroppedKinds) != KernelRereads.None ||
                           (file.Reads == KernelRereads.None && file.MayFailOn == (KernelRereads.Spks | KernelRereads.Cks));
        return droppedKind && Path.IsPathFullyQualified(file.Name) && IsMissing(file.Name);
    }

    // Mirrors std::filesystem::exists of the native layer, which follows symbolic links: only a missing file or directory
    // counts as deleted, and so does a link whose final target is missing (a dangling link, which File.GetAttributes, reading
    // the link itself, finds; review of the fourth round of #668)
    private static bool IsMissing(string file)
    {
        try
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0)
            {
                return false;
            }

            var target = File.ResolveLinkTarget(file, returnFinalTarget: true);
            return target != null && !File.Exists(target.FullName) && !Directory.Exists(target.FullName);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return true;
        }
        catch (Exception)
        {
            // An access error, a name the file system cannot test: the native layer keeps such a kernel
            return false;
        }
    }

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
    /// loader walks it. The path is walked as a directory whenever it is one on disk, whatever its type.
    /// </summary>
    /// <remarks>
    /// The other files of a directory (a geopotential model, a readme) are left out: SPICE would load nothing from
    /// them, and opening them from the native layer could lock them on Windows while the library reads them
    /// (<see cref="IsKernelFile"/>).
    /// </remarks>
    internal static IEnumerable<string> KernelFiles(FileSystemInfo path)
    {
        if (!Directory.Exists(path.FullName))
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
    /// <para>
    /// The files a load handed SPICE are unloaded by their names, whether the path still exists or not (#673): every loaded
    /// entry that is the path or inside it is unloaded and removed; an entry that holds the path (a file or a subdirectory
    /// of a loaded directory) unloads the files it recorded there and is replaced, at its place, by its remaining files. A
    /// path that matches no entry is handed to SPICE as it is (the files of a directory as it is now), which does nothing
    /// for a name SPICE does not hold.
    /// </para>
    /// <para>
    /// Each native load and unload first closes, by the handle SPICE holds, and unloads every SPK, CK, binary PCK and EK whose
    /// file was deleted since its load (#668, second round): SPICE would otherwise fail to open it again after the unload of
    /// another kernel of its kind and keep it open for the life of the process. Its data are no longer readable from then on,
    /// and <see cref="GetLoadedKernels"/> no longer lists it once the call returns: a directory whose files were all dropped
    /// leaves the listing, one with remaining files stays listed, and a later unload of the dropped path does nothing (fourth
    /// round of #668; before, the path was listed until it was unloaded). <see cref="WriteEphemeris"/>,
    /// <see cref="WriteOrientation"/> and <c>Spacecraft.WriteEphemeris</c>, when they replace a file, do the same before the
    /// unloads of their writer, and first unload the file through this method when a load of this class holds it (#706). The file is tested by the name
    /// SPICE holds: a kernel a meta-kernel names relative to the working directory of its load is left to SPICE, deleted or
    /// not, since that name no longer reaches its file once the working directory changed; name the files of a meta-kernel
    /// by absolute paths to have them covered.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">SPICE failed to unload a file and no later unload of the call, of the same
    /// entry or of another one, repaired it: the unload of a text kernel or a meta-kernel reads the text kernels still loaded
    /// again, and that of a DSK opens the other loaded DSKs again; each fails on a kernel of those kinds deleted since its
    /// load, until a later unload that reads the same kernels again succeeds (#673). A deleted SPK, CK, binary PCK or EK
    /// SPICE holds under an absolute name no longer makes an unload fail (#668, second round), but one SPICE holds under a
    /// relative name does. Every matched entry is unloaded and removed before the exception, whose message is SPICE's for
    /// the first failure no later unload repaired (#706: before, it referred to the standard output).</exception>
    public void UnloadKernels(FileSystemInfo path)
    {
        if (path == null) return;
        lock (lockObject)
        {
            long nativeCalls = _nativeKernelCalls;
            try
            {
                UnloadKernelsLocked(path);
            }
            finally
            {
                if (_nativeKernelCalls != nativeCalls) ForgetDroppedKernels();
                Interlocked.Increment(ref _kernelPoolGeneration);
            }
        }
    }

    // UnloadKernels under the lock
    private void UnloadKernelsLocked(FileSystemInfo path)
    {
        bool matched = false;
        var unrepaired = new List<(KernelFile File, string Error)>();
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

        if (!matched && !UnloadKernelNative(path.FullName))
        {
            throw new InvalidOperationException($"Kernel {path.FullName} can't be unloaded: {LastProxyError()}");
        }

        if (unrepaired.Count > 0)
        {
            throw new InvalidOperationException($"Kernel {unrepaired[0].File.Name} can't be unloaded: {unrepaired[0].Error}");
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
    /// in their order, each with SPICE's message of its failure (#706). Since the second round of #668 the native unload first
    /// closes and unloads the deleted SPKs, CKs, binary PCKs and EKs, so only text kernels, meta-kernels and DSKs still fail
    /// this way, and the binary kernels SPICE holds under a relative name.
    /// </summary>
    private static void UnloadFiles(KernelFile[] files, List<(KernelFile File, string Error)> unrepaired)
    {
        foreach (var file in files)
        {
            if (!UnloadKernelNative(file.Name))
            {
                // SPICE's message, kept by the native layer since #706, taken at the failing call
                unrepaired.Add((file, LastProxyError()));
            }
            else
            {
                unrepaired.RemoveAll(x => (x.File.MayFailOn & ~file.Reads) == KernelRereads.None);
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
            try
            {
                KClearProxy();
                _kernels.Clear();
            }
            finally
            {
                Interlocked.Increment(ref _kernelPoolGeneration);
            }
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
    private static IEnumerable<TimeSystem.Window> WithinSearchWindow(Window[] windows, int count, TimeSystem.Window searchWindow)
    {
        var start = searchWindow.StartDate.ToTDB();
        var end = searchWindow.EndDate.ToTDB();
        double startSeconds = start.TimeSpanFromJ2000().TotalSeconds;
        double endSeconds = end.TimeSpanFromJ2000().TotalSeconds;
        var result = new TimeSystem.Window[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = new TimeSystem.Window(Label(windows[i].Start), Label(windows[i].End));
        }

        return result;

        Time Label(double seconds) =>
            seconds <= startSeconds ? start : seconds >= endSeconds ? end : Time.Create(seconds, TimeFrame.TDBFrame);
    }

    /// <summary>
    /// The number of windows a CSPICE search returns at most: the result cell of the native layer, which holds 10000
    /// windows for each search (#653). A search of more throws <see cref="InvalidOperationException"/> with the long message
    /// of CSPICE's <c>SPICE(WINDOWEXCESS)</c>, "Window has size, 20000, cardinality 20000. Cannot insert an additional
    /// interval into the window." (the size counts the endpoints; the short code is not in the message).
    /// </summary>
    internal const int WindowSearchCapacity = 10000;

    private delegate bool NativeWindowSearch(Window[] windows, int capacity, out int count);

    /// <summary>
    /// A native window search into a buffer of <see cref="WindowSearchCapacity"/> windows, rented from the shared pool and
    /// returned: every window it found, labelled by <see cref="WithinSearchWindow"/>. Called under the lock. Before 11.0.0
    /// the buffer held 1000 windows and the proxies dropped the rest without an error, so that a search that found more
    /// returned its first 1000 (#653).
    /// </summary>
    /// <exception cref="InvalidOperationException">The native search failed: SPICE's message.</exception>
    private static IEnumerable<TimeSystem.Window> RunWindowSearch(TimeSystem.Window searchWindow, NativeWindowSearch search)
    {
        var windows = System.Buffers.ArrayPool<Window>.Shared.Rent(WindowSearchCapacity);
        try
        {
            if (!search(windows, WindowSearchCapacity, out int count))
            {
                ThrowLastProxyError();
            }

            return WithinSearchWindow(windows, count, searchWindow);
        }
        finally
        {
            System.Buffers.ArrayPool<Window>.Shared.Return(windows);
        }
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
    /// <exception cref="InvalidOperationException">SPICE cannot run the search: a body or an epoch without ephemeris data,
    /// an epoch outside the coverage of the frame data, a step SPICE rejects, more windows than its result cell holds (10000, #653), with
    /// SPICE's message (fourth round of #668; before 11.0.0 the native layer dropped the error and the search returned no
    /// window).</exception>
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
            return RunWindowSearch(searchWindow, (Window[] windows, int capacity, out int count) =>
                FindWindowsOnDistanceConstraintProxy(searchWindow.Convert(), observerId, targetId, relation, value,
                    aberration.GetDescription(), stepSize.TotalSeconds, windows, capacity, out count));
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
    /// <exception cref="InvalidOperationException">SPICE cannot run the search: a body or an epoch without ephemeris data,
    /// an epoch outside the coverage of the frame data, a step SPICE rejects, more windows than its result cell holds (10000, #653), a
    /// frame or radii it does not know, with SPICE's message (fourth round of #668; before 11.0.0 the native layer dropped
    /// the error and the search returned no window).</exception>
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
    /// process to read for a named spacecraft (#560); or SPICE does not name it, so it has no frame (#672).</exception>
    /// <exception cref="InvalidOperationException">SPICE cannot run the search: a body or an epoch without ephemeris data,
    /// an epoch outside the coverage of the frame data, a step SPICE rejects, more windows than its result cell holds (10000, #653), a
    /// frame or radii it does not know, with SPICE's message (fourth round of #668; before 11.0.0 the native layer dropped
    /// the error and the search returned no window).</exception>
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
                ? frontFrame?.Name ?? ReadCelestialBodyInfo(frontBodyId, nameof(frontBodyId)).FrameName
                : string.Empty;
            string targetFrameName = targetShape == ShapeType.Ellipsoid
                ? targetFrame?.Name ?? ReadCelestialBodyInfo(targetId, nameof(targetId)).FrameName
                : string.Empty;
            return RunWindowSearch(searchWindow, (Window[] windows, int capacity, out int count) =>
                FindWindowsOnOccultationConstraintProxy(searchWindow.Convert(), observerId, targetId, targetFrameName, targetShape.GetDescription(),
                    frontBodyId, frontFrameName, frontShape.GetDescription(), occultationType.GetDescription(), aberration.GetDescription(),
                    stepSize.TotalSeconds, windows, capacity, out count));
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
    /// The frame is handed to <c>gfposc_c</c> by name (<see cref="SpiceFrameName"/>). A frame of the library that SPICE
    /// does not know by that name throws (<see cref="SpiceKnowsFrame"/>: TEME, CIRS, TIRS, the frame of a site or a
    /// spacecraft whose frame id no loaded kernel defines under the name handed, third review of #690; before, the proxy
    /// dropped the error of <c>gfposc_c</c> and the search returned no window). The frame of a site built from coordinates
    /// or of a managed spacecraft whose name a loaded kernel gives its id is still searched in that kernel frame, by name.
    /// <see cref="Body.CelestialItem.FindWindowsOnCoordinateConstraint"/> and
    /// <see cref="Surface.Site.FindWindowsOnCoordinateConstraint"/> search in every frame whose reads are the library's
    /// (<see cref="ManagedReadFrame"/>) as the library reads it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException">The frame is a frame of the library that SPICE does not know by name.</exception>
    /// <exception cref="InvalidOperationException">SPICE cannot run the search: a body or an epoch without ephemeris data,
    /// an epoch outside the coverage of the frame data, a step SPICE rejects, more windows than its result cell holds (10000, #653), a
    /// frame name it does not know, with SPICE's message (fourth round of #668; before 11.0.0 the native layer dropped the
    /// error and the search returned no window).</exception>
    public IEnumerable<TimeSystem.Window> FindWindowsOnCoordinateConstraint(TimeSystem.Window searchWindow, int observerId,
        int targetId, Frame frame, CoordinateSystem coordinateSystem, Coordinate coordinate,
        RelationnalOperator relationalOperator, double value, double adjustValue, Aberration aberration,
        TimeSpan stepSize)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));
        if (!SpiceKnowsFrame(frame))
        {
            throw new ArgumentException(
                $"CSPICE cannot search in the frame {frame.Name}: SPICE does not know it (a frame of the library, or a site or spacecraft frame that no loaded kernel defines). CelestialItem.FindWindowsOnCoordinateConstraint and Site.FindWindowsOnCoordinateConstraint search in it.",
                nameof(frame));
        }

        return SearchWindows(searchWindow, relationalOperator, relation =>
        {
            lock (lockObject)
            {
                string frameName = SpiceFrameName(frame);
                return RunWindowSearch(searchWindow, (Window[] windows, int capacity, out int count) =>
                    FindWindowsOnCoordinateConstraintProxy(searchWindow.Convert(), observerId, targetId, frameName,
                        coordinateSystem.GetDescription(), coordinate.GetDescription(), relation, value, adjustValue,
                        aberration.GetDescription(), stepSize.TotalSeconds, windows, capacity, out count));
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
    /// <param name="method">The computation method of <c>gfilum_c</c>: ELLIPSOID, the only one it supports, whatever its case
    /// and its leading or trailing whitespace (#682).</param>
    /// <returns>The windows where the constraint holds.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fixedFrame"/> or <paramref name="method"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="method"/> is not ELLIPSOID; before 11.0.0 such a method gave no
    /// window, <c>gfilum_c</c>'s error dropped by the native proxy (#682).</exception>
    /// <remarks>
    /// CSPICE has no <c>&gt;=</c>, <c>&lt;=</c> or <c>!=</c> (#482): <see cref="RelationnalOperator.GreaterOrEqual"/>
    /// and <see cref="RelationnalOperator.LowerOrEqual"/> are searched as <c>&gt;</c> and <c>&lt;</c>, whose windows are
    /// closed (they differ only where the quantity stays equal to the value over an interval);
    /// <see cref="RelationnalOperator.NotEqual"/> is the search window less the instants of <c>=</c>, a window from each
    /// crossing to the next, as the managed search gives it.
    /// Before 11.0.0 the altitude was read in km (#488).
    /// </remarks>
    /// <exception cref="InvalidOperationException">SPICE cannot run the search: a body or an epoch without ephemeris data,
    /// an epoch outside the coverage of the frame data, a step SPICE rejects, more windows than its result cell holds (10000, #653), a
    /// fixed frame it does not know, with SPICE's message (fourth round of #668; before 11.0.0 the native layer dropped the
    /// error and the search returned no window).</exception>
    public IEnumerable<TimeSystem.Window> FindWindowsOnIlluminationConstraint(TimeSystem.Window searchWindow, int observerId,
        int targetBodyId, Frame fixedFrame, Coordinates.Planetodetic planetodetic, IlluminationAngle illuminationType, RelationnalOperator relationalOperator,
        double value, double adjustValue, Aberration aberration, TimeSpan stepSize, int illuminationSourceId,
        string method = "Ellipsoid")
    {
        if (fixedFrame == null) throw new ArgumentNullException(nameof(fixedFrame));
        method = CheckIlluminationMethod(method);
        return SearchWindows(searchWindow, relationalOperator, relation =>
        {
            lock (lockObject)
            {
                return RunWindowSearch(searchWindow, (Window[] windows, int capacity, out int count) =>
                    FindWindowsOnIlluminationConstraintProxy(searchWindow.Convert(), observerId,
                        illuminationSourceId.ToString(), targetBodyId, fixedFrame.Name,
                        // The native layer hands the altitude to georec_c beside a radius in km (#488)
                        (planetodetic with { Altitude = planetodetic.Altitude / 1000.0 }).Convert(),
                        illuminationType.GetDescription(), relation, value, adjustValue,
                        aberration.GetDescription(), stepSize.TotalSeconds, method, windows, capacity, out count));
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
    /// <exception cref="InvalidOperationException">SPICE cannot run the search: a body or an epoch without ephemeris data,
    /// an epoch outside the coverage of the frame data, a step SPICE rejects, more windows than its result cell holds (10000, #653), an
    /// instrument whose field of view no loaded instrument kernel defines, with SPICE's message (fourth round of #668;
    /// before 11.0.0 the native layer dropped the error and the search returned no window).</exception>
    public IEnumerable<TimeSystem.Window> FindWindowsInFieldOfViewConstraint(TimeSystem.Window searchWindow, int observerId,
        int instrumentId, int targetId, Frame targetFrame, ShapeType targetShape, Aberration aberration, TimeSpan stepSize)
    {
        if (targetFrame == null) throw new ArgumentNullException(nameof(targetFrame));
        lock (lockObject)
        {
            return RunWindowSearch(searchWindow, (Window[] windows, int capacity, out int count) =>
                FindWindowsInFieldOfViewConstraintProxy(searchWindow.Convert(), observerId, instrumentId, targetId, targetFrame.Name,
                    targetShape.GetDescription(), aberration.GetDescription(), stepSize.TotalSeconds, windows, capacity, out count));
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
    /// <param name="stepSize">The step of the epochs read, positive: start + i·step in the time frame of the window start (UTC
    /// steps for a UTC window), i from 0 while within the window, the end included when it falls on the grid; each state is the
    /// read of its epoch alone, labelled with it in that frame (#563).</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stepSize"/> is not positive (#563: before, a step of 0
    /// ran until the process was out of memory and a negative step read 10000 epochs backwards).</exception>
    /// <exception cref="InvalidOperationException">SPICE gives no state at one of the epochs (#454), or the observer is a
    /// site whose SPK does not cover J2000 TDB (see the remarks).</exception>
    /// <exception cref="ArgumentException">The observer is a site identifier (<see cref="IsSiteId"/>: body × 1000 + site)
    /// that no loaded kernel names (<c>naifId</c>, "has no name in SPICE"): its observer is a <see cref="Surface.Site"/>
    /// built from its SPICE name (#672); load a kernel that names it (NAIF_BODY_NAME and NAIF_BODY_CODE, as the frame kernel
    /// of <see cref="Surface.Site.WriteFrameAsync"/>) with its SPK. Or the observer is another identifier that is not a
    /// celestial body, or that SPICE does not name (#672).</exception>
    /// <remarks>The observer of a site identifier is <c>new Site(id % 1000, name, new CelestialBody(id / 1000))</c>, whose
    /// constructor reads the site at J2000 TDB: its SPK must cover that epoch, or the read throws
    /// <see cref="InvalidOperationException"/>. Neither condition applies to the site as target: with
    /// <see cref="Aberration.None"/>, read the site as target from the observer and negate the state (documented in the
    /// fourth round of #668).</remarks>
    public IEnumerable<OrbitalParameters.OrbitalParameters> ReadEphemeris(TimeSystem.Window searchWindow,
        int observerId, int targetId, Frame frame,
        Aberration aberration, TimeSpan stepSize)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var grid = new WindowGrid(searchWindow, stepSize);
        // Built before the lock (#623): the observer of a Lagrange point reads Barycenters, whose type initializer takes it
        var observer = CreateObserver(observerId);
        lock (lockObject)
        {
            List<OrbitalParameters.OrbitalParameters> orbitalParameters = [];
            // A managed frame is read in J2000, then rotated (#558)
            var managed = ManagedReadFrame(frame);
            string frameName = managed == null ? SpiceFrameName(frame) : Frame.ICRF.Name;
            for (long first = 0; first < grid.Count; first += WindowReadChunk)
            {
                double[] epochs = grid.Seconds(first, WindowReadChunk);
                var stateVectors = new StateVector[epochs.Length];
                if (!ReadEphemerisProxy(epochs, epochs.Length, observerId, targetId, frameName, aberration.GetDescription(), stateVectors))
                    ThrowLastProxyError();
                // Every state checked before any is converted: the rotation into a managed frame calls TransformFrameProxy,
                // which clears the message of the failed epochs (#714, review of the branch: a site frame threw "Native proxy
                // call failed" where J2000 threw SPICE's message)
                foreach (var x in stateVectors)
                {
                    ThrowOnFailedRead(x);
                }

                for (int i = 0; i < stateVectors.Length; i++)
                {
                    // Labelled with the epoch read, start + i·step in the frame of the window start (#563, #625)
                    var epoch = grid.Epoch(first + i);
                    var x = stateVectors[i];
                    orbitalParameters.Add(managed == null
                        ? new OrbitalParameters.StateVector(x.Position.Convert(), x.Velocity.Convert(), observer, epoch, frame)
                        : ToManagedFrame(x.Position.Convert(), x.Velocity.Convert(), observer, epoch, managed, frame));
                }
            }

            return orbitalParameters;
        }
    }

    // The epochs a native window read takes at most in one call: the size of the native buffers before #563
    private const int WindowReadChunk = 10000;

    /// <summary>
    /// The epochs of a window read (#563): start + i·step in the time frame of the window start, i from 0 while within the
    /// window, in exact tick arithmetic, the end of the window included when it falls on the grid; each epoch read at its TDB
    /// seconds, as the read of that epoch alone takes them. Before 11.0.0 the native proxies stepped their epoch by
    /// epoch += step in double TDB seconds, so that sample i drifted from start + i·step (1.43e-4 s after 6000 steps of
    /// 0.1 s, 4.29e-4 s after 9000 of 1.2 s) and the last epoch of the window could be dropped; a grid taken in TDB for a
    /// window of another frame still dropped it where TDB − TT made the window shorter in TDB (a UTC window of 600 s at
    /// 0.1 s from the first of May to that of October 2026: 6000 states for 6001, review of #563), and its labels were off
    /// start + i·step by up to 2 ticks.
    /// </summary>
    private readonly struct WindowGrid
    {
        private readonly Time _start;
        private readonly long _stepTicks;

        /// <summary>The number of epochs.</summary>
        internal long Count { get; }

        /// <exception cref="ArgumentOutOfRangeException"><paramref name="stepSize"/> is not positive.</exception>
        internal WindowGrid(TimeSystem.Window searchWindow, TimeSpan stepSize)
        {
            if (stepSize <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(stepSize), stepSize, "The step of a window read must be positive.");
            }

            _start = searchWindow.StartDate;
            _stepTicks = stepSize.Ticks;
            Count = (searchWindow.EndDate.ConvertTo(_start.Frame) - _start).Ticks / _stepTicks + 1;
        }

        /// <summary>Epoch <paramref name="index"/>, in the time frame of the window start.</summary>
        internal Time Epoch(long index) => _start + TimeSpan.FromTicks(_stepTicks * index);

        /// <summary>The TDB seconds past J2000 of at most <paramref name="length"/> epochs from <paramref name="first"/>, as the
        /// read of one epoch takes them.</summary>
        internal double[] Seconds(long first, int length)
        {
            var seconds = new double[(int)System.Math.Min(length, Count - first)];
            for (int i = 0; i < seconds.Length; i++)
            {
                seconds[i] = Epoch(first + i).ToTDB().TimeSpanFromJ2000().TotalSeconds;
            }

            return seconds;
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
    /// not chain the target to the observer; the message is SPICE's. Before 11.0.0 the state was undefined. Or the observer
    /// is a site whose SPK does not cover J2000 TDB (see the remarks).</exception>
    /// <exception cref="ArgumentException">The observer is a site identifier (<see cref="IsSiteId"/>: body × 1000 + site)
    /// that no loaded kernel names (<c>naifId</c>, "has no name in SPICE"): its observer is a <see cref="Surface.Site"/>
    /// built from its SPICE name (#672); load a kernel that names it (NAIF_BODY_NAME and NAIF_BODY_CODE, as the frame kernel
    /// of <see cref="Surface.Site.WriteFrameAsync"/>) with its SPK. Or the observer is another identifier that is not a
    /// celestial body, or that SPICE does not name (#672).</exception>
    /// <remarks>The observer of a site identifier is <c>new Site(id % 1000, name, new CelestialBody(id / 1000))</c>, whose
    /// constructor reads the site at J2000 TDB: its SPK must cover that epoch, or the read throws
    /// <see cref="InvalidOperationException"/>. Neither condition applies to the site as target: with
    /// <see cref="Aberration.None"/>, read the site as target from the observer and negate the state (documented in the
    /// fourth round of #668).</remarks>
    public OrbitalParameters.OrbitalParameters ReadEphemeris(Time epoch, int observerId,
        int targetId, Frame frame, Aberration aberration)
    {
        ArgumentNullException.ThrowIfNull(frame);
        // Built before the lock (#623): the observer of a Lagrange point reads Barycenters, whose type initializer takes it
        var observer = CreateObserver(observerId);
        lock (lockObject)
        {
            return ReadEphemerisAtGivenEpoch(epoch, observer, targetId, frame, aberration);
        }
    }

    /// <summary>
    /// The observer of the reads by identifiers: a <see cref="Surface.Site"/> for a site identifier (<see cref="IsSiteId"/>),
    /// from the kernels (<see cref="CreateKernelSite"/>), otherwise <see cref="CelestialItem.Create"/> (#672). Built before
    /// the lock (#623). The CLI builds its objects by identifier the same way (<c>Helpers.CreateLocalizable</c>).
    /// </summary>
    private ILocalizable CreateObserver(int observerId)
    {
        return IsSiteId(observerId) ? CreateKernelSite(observerId) : CelestialItem.Create(observerId);
    }

    /// <summary>
    /// The site of a site identifier (<see cref="IsSiteId"/>) from the kernels: <c>new Site(id % 1000, its SPICE name, new
    /// CelestialBody(id / 1000))</c>, which reads the site's state at J2000 TDB (#672). Not under the lock (#623).
    /// </summary>
    /// <param name="siteId">The NAIF identifier of the site, its body's times 1000 plus its own.</param>
    /// <exception cref="ArgumentException">The loaded kernels give the identifier no name.</exception>
    internal Surface.Site CreateKernelSite(int siteId)
    {
        string name = GetCelestialBodyInfo(siteId).Name;
        return new Surface.Site(siteId % 1000, name, new Body.CelestialBody(siteId / 1000));
    }

    /// <summary>
    /// Whether <paramref name="naifId"/> is the identifier of a site: its body's identifier times 1000 plus its own (1 to
    /// 999), the body a planet or a moon (101 to 999 without the Lagrange points 391 to 395), as <see cref="Surface.Site.NaifId"/>
    /// builds it (399013 is DSS-13 on the Earth).
    /// </summary>
    /// <remarks>
    /// The CLI follows this rule (<c>Helpers.CreateLocalizable</c> and <c>sub-point</c>, through the internals the library
    /// gives it): before 11.0.0 it kept its own, 100001 to 999998, and built the site <c>site&lt;id&gt;</c> of any of them,
    /// so that the two gave a different observer for a name, a Lagrange point or x000.
    /// </remarks>
    internal static bool IsSiteId(int naifId)
    {
        int body = naifId / 1000;
        return naifId % 1000 > 0 && body is > 100 and < 1000 && body is not (>= 391 and <= 395);
    }

    /// <summary>
    /// Whether <paramref name="naifId"/> is a site identifier (<see cref="IsSiteId"/>) whose body SPICE names, by its built-in
    /// names or the loaded kernels: the identifiers that are about their body (#651). SPICE's built-in NOTO (398989) and
    /// NEW NORCIA (398990) have the form of a site of 398, a body SPICE does not name: they are not, and keep the
    /// classification of an identifier above 1000 (the Sun as centre, the solar system barycentre).
    /// </summary>
    /// <remarks>
    /// Reads SPICE under the shared lock, for an identifier of the form of a site only: the static objects of
    /// <see cref="SolarSystemObjects"/>, whose identifiers are at most 999, never reach it (#623).
    /// </remarks>
    internal static bool IsSiteOfNamedBody(int naifId)
    {
        if (!IsSiteId(naifId))
        {
            return false;
        }

        int body = naifId / 1000;
        lock (lockObject)
        {
            // The record's Id stays -1 when bodc2n_c does not find the identifier (#672)
            return GetCelestialBodyInfoProxy(body).Id == body;
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

    // The bound of | |q| − 1 | of a rotation from the native layer, a unit quaternion to rounding; the NaN rotation of a
    // failed transformation (#714; the zero quaternion before, 1 away, #454) fails the comparison
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
    /// <param name="stepSize">Time step between successive query epochs, positive: start + i·step in the time frame of the window
    /// start (UTC steps for a UTC window), i from 0 while within the window, the end included when it falls on the grid (#563);
    /// each record is labelled with the epoch of the CK's record, TDB</param>
    /// <returns>
    ///     The records of the CK, as <c>ckgpav_c</c> returns them: the C-matrix, the rotation from
    ///     <paramref name="referenceFrame"/> to the spacecraft frame, labelled with <paramref name="referenceFrame"/>, its
    ///     source (<c>vectorInSpacecraft = vectorInReference.Rotate(Rotation)</c>), and the angular velocity of the
    ///     spacecraft relative to <paramref name="referenceFrame"/>, expressed in <paramref name="referenceFrame"/>: the
    ///     convention of <see cref="Frame.ToFrame"/> (<c>xf2rav_c</c>) for that rotation, and that of
    ///     <see cref="Body.Spacecraft.Spacecraft.GetOrientation"/>. A record read from a segment without angular velocity
    ///     (a type-1 CK, or a type-2 or type-3 CK written without rates) has a zero angular velocity: <c>ckgpav_c</c> skips
    ///     such a segment, and its pointing is read by <c>ckgp_c</c> (#714: before, the call threw "No orientation found").
    ///     Each record is that of the segment <c>ckgp_c</c> reads, the one of highest priority (the CK loaded last, the last
    ///     segment of a file) that gives a pointing within the tolerance, with <c>ckgpav_c</c>'s angular velocity when that
    ///     segment holds one and a zero angular velocity when it holds none, never the pointing or the rate of a
    ///     lower-priority segment (#724, decided by S. Guillet, 2026-11-18: before, a segment without rates loaded over one
    ///     with rates gave the record of the one with rates). The priority is that of a search in a fresh process, whatever
    ///     the calls before: the read first voids the segment CSPICE keeps from its last search of the spacecraft (its
    ///     re-use interval), which a search with angular velocity (<c>ckgpav_c</c>, or <see cref="TransformFrame(Time, Frame, Frame)"/>
    ///     to the spacecraft's CK frame through <c>sxform_c</c>) leaves over a segment without rates it skips (review of #724:
    ///     before, a read after such a search, or after an epoch of the same call in a gap of the higher-priority segment's
    ///     interpolation intervals, could take the lower-priority segment's record).
    /// </returns>
    /// <remarks>
    ///     The rotation of a spacecraft's attitude in the frames' convention, spacecraft to reference, is its conjugate,
    ///     which <c>GetStateOrientationToICRF</c> of the frame of a kernel-backed spacecraft returns. On a CK written by
    ///     <see cref="Body.Spacecraft.Spacecraft.WriteOrientation"/> before 11.0.0, which holds the inverse of the
    ///     attitude (#529), this method returns that inverse.
    ///     Each record is the read of its epoch alone, in a buffer of the epochs read, whatever their number (#563: before
    ///     11.0.0 the proxy stepped its epoch by epoch += step, so that the epochs drifted from start + i·step and the last
    ///     one could be dropped, into a buffer of 10000 records, 1.4 ms for a read of one epoch, and a window of more than
    ///     10000 steps threw).
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="referenceFrame"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stepSize"/> is not positive (#563).</exception>
    /// <exception cref="InvalidOperationException">SPICE gives no orientation at one of the epochs, with its message.</exception>
    public IEnumerable<OrbitalParameters.StateOrientation> ReadOrientation(TimeSystem.Window searchWindow,
        int naifId, TimeSpan tolerance,
        Frame referenceFrame, TimeSpan stepSize)
    {
        if (referenceFrame == null) throw new ArgumentNullException(nameof(referenceFrame));
        var grid = new WindowGrid(searchWindow, stepSize);
        lock (lockObject)
        {
            string frameName = SpiceFrameName(referenceFrame);
            List<OrbitalParameters.StateOrientation> records = [];
            for (long first = 0; first < grid.Count; first += WindowReadChunk)
            {
                double[] epochs = grid.Seconds(first, WindowReadChunk);
                // A buffer of the epochs read (#563: before, 10000 records whatever the window, 1.4 ms for one epoch)
                var stateOrientations = new StateOrientation[epochs.Length];
                if (!ReadOrientationProxy(epochs, epochs.Length, naifId, tolerance.TotalSeconds, frameName, stateOrientations))
                    ThrowLastProxyError();
                records.AddRange(stateOrientations.Select(x => new OrbitalParameters.StateOrientation(x.Rotation.Convert(),
                    x.AngularVelocity.Convert(), Time.Create(x.Epoch, TimeFrame.TDBFrame), referenceFrame)));
            }

            return records;
        }
    }

    /// <summary>
    /// Writes the arcs of states of an object as an SPK of type 9, one segment per arc, through the managed writer
    /// (<see cref="Kernels.SpkWriter"/>, #540), in J2000: the native writer writes one segment per file. As the
    /// native writer, it unloads and replaces an existing file, creates its directory when needed, and leaves the new file
    /// unloaded (the native kernel unloads the file it wrote when it is destroyed, at the end of its proxy). A file
    /// <see cref="LoadKernels"/> loaded is first unloaded as <see cref="UnloadKernels"/> does, so that
    /// <see cref="GetLoadedKernels"/> no longer lists it (#706: before, it stayed listed while SPICE no longer held it), then
    /// every other load of it SPICE holds, under the names <see cref="WriteEphemeris"/> finds (review of #706: before, a
    /// single unload, which left a second load, a meta-kernel's, holding the replaced data).
    /// </summary>
    /// <param name="filePath">The SPK; the caller's <see cref="FileInfo"/> is refreshed once the file is written.</param>
    /// <param name="naifId">The NAIF identifier of the object.</param>
    /// <param name="arcs">The arcs, in time order, each of at least two states in strictly increasing epoch order, in ICRF,
    /// relative to the centre of their first state.</param>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> or <paramref name="arcs"/> is null.</exception>
    /// <exception cref="ArgumentException">No arc, an arc of fewer than two states, or epochs not strictly increasing; an
    /// existing file is then left as it was.</exception>
    /// <exception cref="InvalidOperationException">SPICE failed to unload the file it replaces (the reopening of the other
    /// loaded SPKs, on a deleted one SPICE holds under a relative name); the message is SPICE's, and the file is left on
    /// disk as it was, no longer loaded, nothing written (#706: before, the failure was ignored and the file replaced).</exception>
    internal void WriteEphemerisArcs(FileInfo filePath, int naifId, IReadOnlyList<OrbitalParameters.StateVector[]> arcs)
    {
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));
        if (arcs == null) throw new ArgumentNullException(nameof(arcs));
        lock (lockObject)
        {
            // The file is built and checked first: an argument it cannot write leaves an existing file as it was
            var bytes = Kernels.SpkWriter.BuildType9(naifId, 1, arcs);
            UnloadReplacedKernel(filePath);

            // File.Exists, not FileInfo.Exists: the caller's FileInfo must not cache the state of the file before the write
            if (File.Exists(filePath.FullName))
            {
                // Every load SPICE holds of the file, under its name or a name that resolves through it, as the native writers
                // unload it (review of #706: a single unload left a second load, a meta-kernel's, holding the replaced file).
                // A failed unload (the reopening of the other SPKs, on a deleted one SPICE holds under a relative name) keeps
                // the file as it was, as the native writers do; before #706 its result was ignored and the file replaced
                bool unloaded = UnloadEveryLoadNative(filePath.FullName);
                ForgetDroppedKernels();
                if (!unloaded)
                {
                    throw new InvalidOperationException(
                        $"An error occurred while writing ephemeris: the file it replaces could not be unloaded: {LastProxyError()}");
                }

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
    /// <remarks>
    /// The states are enumerated once, before the SPICE lock, so that nothing of the caller's sequence runs under it
    /// (#661: before, under the lock, which a sequence that first read a static body class could deadlock, #623).
    /// The native writer unloads the file it replaces and, once written, the file itself; like every native load and unload
    /// (<see cref="UnloadKernels"/>), it first closes and unloads every SPK, CK, binary PCK and EK whose file was deleted
    /// since its load (#668, second round). Before, the unload opened the other SPKs again, failed on a deleted one, which
    /// stayed open in SPICE for good, and left the failure pending for the next kernel call. That reopening can still fail on
    /// an SPK SPICE holds under a relative name (a meta-kernel's), which the native layer leaves to SPICE, once its file was
    /// deleted or the working directory changed: the call then throws with SPICE's message and leaves nothing pending
    /// (fourth round of #668; before, it returned true and the next kernel call failed with a wrong error). A new file is
    /// written by then. A file the call replaces is unloaded before its removal, and that unload fails the same way: the
    /// file is then left on disk as it was, no longer loaded, and nothing is written (review of the fourth round of #668;
    /// before, it was removed and nothing written in its place).
    /// A file it replaces that <see cref="LoadKernels"/> loaded, by itself or in a loaded directory, is first unloaded as
    /// <see cref="UnloadKernels"/> does, and the native writer unloads every other load of the file (a meta-kernel's): SPICE
    /// no longer reads the replaced data, <see cref="GetLoadedKernels"/> no longer lists the file, and the new file is left
    /// unloaded (#706: before, the caller's load stayed in SPICE, which held the replaced file open and read its data). A
    /// load is found under the path or under an absolute name whose symbolic links, followed one at a time, resolve through
    /// it (a link to the file, the file in a directory loaded through a link; review of #706). A hard link to the file and
    /// the file a symbolic link given as <paramref name="filePath"/> names keep what they name and stay loaded; a relative
    /// name other than the path (a meta-kernel's) is not found, and SPICE goes on reading the replaced data through it.
    /// </remarks>
    /// <exception cref="ArgumentException">The full path of <paramref name="filePath"/> is longer than the 255 characters
    /// (UTF-8 bytes) of a kernel name SPICE takes; nothing is written (#673: CSPICE wrote under the name truncated to 255,
    /// then failed).</exception>
    /// <exception cref="ArgumentException">Fewer than three states, states not all in the frame of the first one (compared
    /// by name, ordinal), or a centre of motion that is not a celestial body (<see cref="IsCelestialBodyId"/>; a spacecraft or
    /// a site): the native writer rejects them or cannot read the centre; nothing is written, an existing file kept (#706:
    /// before, a C++ exception crossed P/Invoke and ended the process). Or a centre of motion SPICE does not name, neither by
    /// its built-in names nor by the loaded kernels ("has no name in SPICE"; a body built from custom parameters of an
    /// identifier no kernel names): load a kernel that names it (NAIF_BODY_NAME and NAIF_BODY_CODE); nothing is written, an
    /// existing file kept (#714: before, <see cref="InvalidOperationException"/> from the native writer).</exception>
    /// <exception cref="InvalidOperationException">SPICE failed to write the file, or to unload the file it replaces or
    /// the file once written (the reopening of the other loaded SPKs); the message is SPICE's. Or the native file system calls
    /// rejected the path (a loop of symbolic links), the message the file system's (#706: before, the process ended). Or the
    /// kernel pool has no GM for a centre SPICE names (ELARA, 507, without <c>BODY507_GM</c>): "CelestialItem id507 can't be
    /// read:" followed by SPICE's message (#714); nothing is written.</exception>
    public bool WriteEphemeris(FileInfo filePath, int naifId,
        IEnumerable<OrbitalParameters.StateVector> stateVectors)
    {
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));
        if (stateVectors == null) throw new ArgumentNullException(nameof(stateVectors));
        Kernels.KernelPath.EnsureLoadable(filePath.FullName, nameof(filePath));
        // The caller's sequence enumerated once, before the lock, so that nothing the caller supplies runs under it (#661)
        var enumerable = stateVectors as OrbitalParameters.StateVector[] ?? stateVectors.ToArray();
        if (enumerable.Length == 0)
            throw new ArgumentException("Value cannot be an empty collection.", nameof(stateVectors));
        ThrowIfNativeWriterRejects(enumerable);
        var states = enumerable.Select(x => x.Convert()).ToArray();
        var centres = enumerable.Select(x => x.Observer.NaifId).Distinct().ToArray();
        lock (lockObject)
        {
            ThrowIfCentreNotNamed(centres);
            UnloadReplacedKernel(filePath);
            bool res;
            try
            {
                res = WriteEphemerisProxy(filePath.FullName, naifId, states, (uint)states.Length);
            }
            finally
            {
                ForgetDroppedKernels();
            }

            if (res == false)
            {
                throw new InvalidOperationException($"An error occurred while writing ephemeris: {LastProxyError()}");
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
    /// The records are enumerated once, before the SPICE lock, so that nothing of the caller's sequence runs under it
    /// (#661: before, under the lock, which a sequence that first read a static body class could deadlock, #623).
    /// <see cref="Body.Spacecraft.Spacecraft.WriteOrientation"/> writes the attitude record of a propagated spacecraft in this
    /// convention, the conjugate of the record's rotation, in J2000. The native writer unloads the file it replaces and, once
    /// written, the file itself; like every native load and unload (<see cref="UnloadKernels"/>), it first closes and
    /// unloads every SPK, CK, binary PCK and EK whose file was deleted since its load (#668, second round). Before, the
    /// unload opened the other CKs again, failed on a deleted one, which stayed open in SPICE for good, and left the failure
    /// pending for the next kernel call. That reopening can still fail on a CK SPICE holds under a relative name (a
    /// meta-kernel's), which the native layer leaves to SPICE, once its file was deleted or the working directory changed:
    /// the call then throws with SPICE's message and leaves nothing pending (fourth round of #668; before, it returned true
    /// and the next kernel call failed with a wrong error). A new file is written by then. A file the call replaces is
    /// unloaded before its removal, and that unload fails the same way: the file is then left on disk as it was, no longer
    /// loaded, and nothing is written (review of the fourth round of #668; before, it was removed and nothing written in its
    /// place). A file it replaces that <see cref="LoadKernels"/> loaded is first unloaded as <see cref="UnloadKernels"/>
    /// does, and the native writer unloads every other load of the file, under the names <see cref="WriteEphemeris"/>
    /// finds (#706 and its review).
    /// </remarks>
    /// <exception cref="InvalidOperationException">SPICE failed to write the file, or to unload the file it replaces or
    /// the file once written (the reopening of the other loaded CKs); the message is SPICE's. Or the native file system calls
    /// rejected the path (a loop of symbolic links), the message the file system's (#706: before, the process ended).</exception>
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
        // The caller's sequence enumerated once, before the lock, so that nothing the caller supplies runs under it (#661)
        var enumerable = stateOrientations as OrbitalParameters.StateOrientation[] ?? stateOrientations.ToArray();
        if (enumerable.Length == 0)
            throw new ArgumentException("Value cannot be an empty collection.", nameof(stateOrientations));
        string frame = enumerable[0].ReferenceFrame.Name;
        var other = enumerable.FirstOrDefault(x => !string.Equals(x.ReferenceFrame.Name, frame, StringComparison.Ordinal));
        if (other != null)
            throw new ArgumentException(
                $"Every orientation must be in the frame of the first one, {frame}: the one at {other.Epoch} is in {other.ReferenceFrame.Name}.",
                nameof(stateOrientations));
        var orientations = enumerable.Select(x => x.Convert()).ToArray();
        lock (lockObject)
        {
            UnloadReplacedKernel(filePath);
            bool res;
            try
            {
                res = WriteOrientationProxy(filePath.FullName, naifId, orientations, (uint)orientations.Length);
            }
            finally
            {
                ForgetDroppedKernels();
            }

            if (res == false)
            {
                throw new InvalidOperationException($"An error occurred while writing orientation: {LastProxyError()}");
            }

            return true;
        }
    }

    /// <summary>
    /// Rejects the states the native writer of SPKs rejects or cannot read (#706): it writes three states or more
    /// (<c>EphemerisKernel::WriteData</c>), all in one frame, compared by name ordinally as the native layer compares them,
    /// and reads the centre of motion of each as a celestial body (<see cref="IsCelestialBodyId"/>, a barycenter, a body or
    /// a star the library builds, not a spacecraft nor a site) that SPICE names (#714). Each threw a C++ exception across
    /// P/Invoke, which ended the process, or, for a spacecraft centre, read the uninitialised GM of a failed read; a centre
    /// SPICE does not name threw <see cref="InvalidOperationException"/> from the native writer. Called before the lock
    /// (#661), the names read under it by <see cref="ThrowIfCentreNotNamed"/>.
    /// </summary>
    private static void ThrowIfNativeWriterRejects(OrbitalParameters.StateVector[] stateVectors)
    {
        if (stateVectors.Length < 3)
        {
            throw new ArgumentException(
                $"The native writer of SPKs writes three states or more; {stateVectors.Length} given.", nameof(stateVectors));
        }

        string frame = stateVectors[0].Frame.Name;
        var other = stateVectors.FirstOrDefault(x => !string.Equals(x.Frame.Name, frame, StringComparison.Ordinal));
        if (other != null)
        {
            throw new ArgumentException(
                $"Every state must be in the frame of the first one, {frame}: the one at {other.Epoch} is in {other.Frame.Name}.",
                nameof(stateVectors));
        }

        var centre = stateVectors.FirstOrDefault(x => x.Observer is not CelestialItem || !IsCelestialBodyId(x.Observer.NaifId));
        if (centre != null)
        {
            throw new ArgumentException(
                $"The centre of motion of every state must be a celestial body: the one at {centre.Epoch} is relative to " +
                $"{centre.Observer.Name} ({centre.Observer.NaifId}), which is not a celestial body: a barycenter (0 to 9), the Sun (10), " +
                "a planet, a moon or a Lagrange point (101 to 999), or an identifier above 1000, not a spacecraft nor a site.",
                nameof(stateVectors));
        }
    }

    // A centre SPICE does not name, which the native writer cannot build (#714, the last row of #655: before, it threw
    // InvalidOperationException from the native layer, "CelestialItem id<id> can't be found"); #680's rule, as the element
    // conversions check it (ReadCelestialBodyInfo). Called under the lock
    private static void ThrowIfCentreNotNamed(int[] centres)
    {
        foreach (var naifId in centres)
        {
            ReadCelestialBodyInfo(naifId, "stateVectors");
        }
    }

    /// <summary>
    /// Unloads, as <see cref="UnloadKernels"/> does, a file a writer is about to replace when it was loaded through
    /// <see cref="LoadKernels"/>, by itself or in a loaded directory, under its own name or a name whose symbolic links
    /// resolve through it (a link to the file, or the file in a directory loaded through a link; #706 and its review):
    /// <see cref="GetLoadedKernels"/> no longer lists it, a directory being replaced by its remaining files, and SPICE no
    /// longer holds it. A hard link names the replaced data, which stay on disk, and is kept, as is the file a symbolic link
    /// given as the target names: the writer replaces the link. Before, the native writers unloaded
    /// the file once after loading it once more, so the caller's load stayed in SPICE, which held the replaced file open and
    /// read its data, and the managed writer unloaded it in SPICE but left it listed. Nothing is done for a file the
    /// registry does not hold; a failed unload throws the <see cref="InvalidOperationException"/> of
    /// <see cref="UnloadKernels"/> before anything is written, the file kept on disk as it was. Called under the lock.
    /// </summary>
    private void UnloadReplacedKernel(FileInfo target)
    {
        // Each file of the registry whose name resolves through the target: the target itself, or, while it exists, a name
        // whose symbolic links reach it (KernelNameResolvesThroughProxy, the rule of the native writers' unload)
        bool exists = File.Exists(target.FullName);
        var names = _kernels.SelectMany(x => x.Files).Select(f => f.Name)
            .Where(n => IsSameOrInside(n, target.FullName) || exists && KernelNameResolvesThroughProxy(n, target.FullName))
            .Distinct(StringComparer.Ordinal).ToArray();
        foreach (var name in names)
        {
            UnloadKernels(new FileInfo(name));
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
    /// <remarks>
    /// A body the kernel pool gives no <c>BODYnnn_RADII</c> has radii 0, one it gives no <c>BODYnnn_GM</c> a GM of 0, and a
    /// barycentre has no frame (an empty <c>FrameName</c>, <c>FrameId</c> 0); before #672 a missing variable left the GM 0
    /// and the frame name undefined. A site identifier of a body SPICE names (<see cref="IsSiteOfNamedBody"/>, 399013 for
    /// DSS-13) has its body as centre of motion and its body's barycentre as barycentre (399 and 3 for DSS-13, the centre of
    /// its SPK segment): before 11.0.0 the Sun and 0, every identifier above 1000 being classed as an asteroid (#651). An
    /// identifier of that form whose body SPICE does not name keeps the Sun and 0: SPICE's NOTO (398989) and NEW NORCIA
    /// (398990), ESA stations whose identifiers do not follow the rule, 398 being no body.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="naifId"/> is not the identifier of a celestial body
    /// (<see cref="IsCelestialBodyId"/>): a spacecraft, an instrument or a site of a negative identifier, 11 to 100, or 1000.
    /// The native layer reads the centre of motion of any named identifier and ended the process for one of them (#560).
    /// Or SPICE does not name it, neither by its built-in names nor by the loaded kernels (#672: the record held the name of
    /// a previous call or undefined bytes).</exception>
    public CelestialBody GetCelestialBodyInfo(int naifId)
    {
        ThrowIfNotCelestialBody(naifId, nameof(naifId));
        lock (lockObject)
        {
            return ReadCelestialBodyInfo(naifId, nameof(naifId));
        }
    }

    // The body information of an identifier SPICE names, under the lock: the record's Id, -1 until bodc2n_c finds the
    // identifier, is the found flag (#672). A site identifier of a body SPICE names is about its body and its body's
    // barycentre, where the native layer, which classes every identifier above 1000 as an asteroid, gives the Sun and 0 (#651)
    private static CelestialBody ReadCelestialBodyInfo(int naifId, string paramName)
    {
        var info = GetCelestialBodyInfoProxy(naifId);
        if (info.Id != naifId)
        {
            throw new ArgumentException(
                $"The NAIF identifier {naifId} has no name in SPICE: neither SPICE's built-in names nor the loaded kernels name it.",
                paramName);
        }

        if (IsSiteOfNamedBody(naifId))
        {
            info.CenterOfMotionId = Body.CelestialItem.SiteBodyId(naifId);
            info.BarycenterOfMotionId = Body.CelestialItem.SiteBarycenterId(naifId);
        }

        return info;
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
    /// transform is composed through ICRF, the other frame from SPICE. So is the <see cref="Surface.SiteFrame"/> of a site
    /// built from coordinates, or of a site from the kernels whose frame id (1000000 + NaifId) no loaded kernel defines,
    /// decided at each call: its orientation is <see cref="Surface.SiteFrame.GetStateOrientationToICRF"/>, the frame the
    /// reads in it, <see cref="Frame.ToFrame"/> and the horizontal coordinates use (fourth round of #668; before, the call
    /// handed its name, <c>Name + "_TOPO"</c>, to SPICE, which does not know it, and threw). The frame of a site whose frame
    /// the kernels define is that kernel frame, read by SPICE, and the frame of a spacecraft is SPICE's by name, which reads
    /// the frame kernel and the C-kernel loaded for it. CIRS and TIRS, <see cref="Frame.CIRS"/>, <see cref="Frame.TIRS"/>,
    /// any instance of <see cref="CirsFrame"/> or <see cref="TirsFrame"/> or a frame of those names in any case, are
    /// composed through ICRF as TEME, by their managed orientation (#656; before, the call handed them to SPICE, which does
    /// not know them, and threw). SPICE must give a rotation: for a frame SPICE does not know, or outside the coverage of the frame
    /// data, the native layer returns a NaN rotation with SPICE's message, and the call throws with it (#714; before, the zero
    /// quaternion and a message of the library's that named the coverage whatever the cause, #454; before 11.0.0 the zero
    /// quaternion was returned, except on the TEME path).
    /// </remarks>
    /// <exception cref="InvalidOperationException">SPICE gives no rotation between the frames at this epoch (a quaternion
    /// whose norm differs from 1 by more than 1e-9, or NaN): the message names the frames and the epoch, then gives SPICE's,
    /// such as <c>SPICE(UNKNOWNFRAME)</c>'s "The frame NOT_A_FRAME was not recognized as a known reference frame." or
    /// <c>SPICE(FRAMEDATANOTFOUND)</c>'s "PCK data required to compute the orientation of the body-fixed frame ITRF93 for
    /// epoch ... were not found."</exception>
    public OrbitalParameters.StateOrientation TransformFrame(Time epoch, Frame fromFrame, Frame toFrame)
    {
        if (fromFrame == null) throw new ArgumentNullException(nameof(fromFrame));
        if (toFrame == null) throw new ArgumentNullException(nameof(toFrame));
        // TEME, CIRS, TIRS and the frame of a site that only the library holds (TransformedThroughIcrf) are composed through
        // ICRF, as the reads in them are rotated (fourth round of #668, #656; before, only TEME was, and the others threw)
        Frame fromManaged, toManaged;
        lock (lockObject)
        {
            fromManaged = TransformedThroughIcrf(fromFrame);
            toManaged = TransformedThroughIcrf(toFrame);
        }

        if (fromManaged != null || toManaged != null)
        {
            return TransformFrameThroughIcrf(epoch, fromFrame, fromManaged, toFrame, toManaged);
        }

        lock (lockObject)
        {
            var res = TransformFrameProxy(SpiceFrameName(fromFrame), SpiceFrameName(toFrame), epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds);

            // A failed transformation returns a NaN rotation with SPICE's message (#714; the zero quaternion with no message
            // before, #454): a rotation is a unit quaternion. Read before any other native call
            var rotation = new Quaternion(res.Rotation.W, res.Rotation.X, res.Rotation.Y, res.Rotation.Z);
            if (!(System.Math.Abs(rotation.Magnitude() - 1.0) <= UnitQuaternionTolerance))
            {
                throw new InvalidOperationException(
                    $"SPICE gives no transformation from {fromFrame.Name} to {toFrame.Name} at {epoch.ToTDB()}: {LastProxyError()}");
            }

            return new OrbitalParameters.StateOrientation(
                rotation,
                new Vector3(res.AngularVelocity.X, res.AngularVelocity.Y, res.AngularVelocity.Z), epoch, fromFrame);
        }
    }

    /// <summary>
    /// The transform between two frames one of which is managed (<see cref="TransformedThroughIcrf"/>), composed through ICRF as
    /// <see cref="Frame.ToFrame"/>: the managed frame by its own orientation, the other by SPICE; labelled with
    /// <paramref name="fromFrame"/>.
    /// </summary>
    private OrbitalParameters.StateOrientation TransformFrameThroughIcrf(Time epoch, Frame fromFrame, Frame fromManaged, Frame toFrame,
        Frame toManaged)
    {
        var fromToIcrf = OrientationToIcrf(epoch, fromFrame, fromManaged);
        var toToIcrf = OrientationToIcrf(epoch, toFrame, toManaged);
        var rotation = toToIcrf.Rotation.Conjugate() * fromToIcrf.Rotation;
        var angularVelocity = toToIcrf.AngularVelocity.Inverse().Rotate(rotation.Conjugate()) + fromToIcrf.AngularVelocity;
        return new OrbitalParameters.StateOrientation(rotation, angularVelocity, epoch, fromFrame);
    }

    // The managed frame a transform composes through ICRF, or null: TEME (#451), CIRS and TIRS (#656), by instance or name
    // (ManagedFrame), and the SiteFrame of a site from coordinates or of a site whose frame no loaded kernel defines
    // (ManagedReadFrame, decided at each call). Not the frame of a managed spacecraft, which stays SPICE's by name: it reads the frame kernel and the CK
    // the library wrote, the independent reference of its attitude record. Called under the lock
    private static Frame TransformedThroughIcrf(Frame frame) =>
        ManagedReadFrame(frame) is { } managed and not Body.Spacecraft.SpacecraftFrame ? managed : null;

    private OrbitalParameters.StateOrientation OrientationToIcrf(Time epoch, Frame frame, Frame managed)
    {
        // TransformFrame throws when SPICE gives no rotation (#454)
        return managed != null ? managed.GetStateOrientationToICRF(epoch) : TransformFrame(epoch, frame, Frame.ICRF);
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
    /// eccentricity out of range), and a read before the kernels are loaded (the Earth's <c>BODY399_GM</c> missing), give a
    /// NaN state from the native layer with SPICE's message, which this method throws (#711; before, a zero state, its
    /// message on stdout only, #454 on the TLE path). A zero state, which no valid one has, throws too.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">SGP4 failed at this epoch, or at the epoch of the TLE, or the kernels the
    /// native layer reads (the Earth's GM, the leapseconds) are not loaded; the message is SPICE's.</exception>
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

        // A failure of the native layer is a NaN state with its message (#711: the Earth's GM missing before the kernels are
        // loaded, SGP4's error); a zero state, which no valid one has, is still refused
        var position = res.Position.Convert();
        if (!IsFinite(res))
        {
            throw new InvalidOperationException($"SGP4 failed for the TLE {name} ({line1}) at {epoch.ToTDB()}: {LastProxyError()}");
        }

        if (position.Magnitude() == 0.0)
        {
            throw new InvalidOperationException(
                $"SGP4 failed for the TLE {name} ({line1}) at {epoch.ToTDB()}: the native layer returned a zero state.");
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
    /// <exception cref="InvalidOperationException">The native layer could not read the TLE: the Earth's GM missing (no kernel
    /// loaded), or SGP4 failing at the epoch of the TLE; the message is SPICE's (#711: before, the epoch <c>getelm_c</c>
    /// never wrote was returned).</exception>
    internal double ReadTleEpoch(string name, string line1, string line2)
    {
        lock (lockObject)
        {
            double epoch = GetTLEElementsProxy(name, line1, line2).Epoch;
            if (!double.IsFinite(epoch))
            {
                throw new InvalidOperationException($"The TLE {name} ({line1}) cannot be read: {LastProxyError()}");
            }

            return epoch;
        }
    }

    private static bool IsFinite(StateVector state) =>
        double.IsFinite(state.Position.X) && double.IsFinite(state.Position.Y) && double.IsFinite(state.Position.Z) &&
        double.IsFinite(state.Velocity.X) && double.IsFinite(state.Velocity.Y) && double.IsFinite(state.Velocity.Z);

    /// <summary>
    /// Rejects the centre of elements the native conversions cannot read (#711): they build it as a celestial body
    /// (<c>CelestialBody(int)</c>), which threw a C++ exception across P/Invoke, ending the process, for a centre SPICE does
    /// not name (a spacecraft whose GM a kernel gives). The centre must be a <see cref="CelestialItem"/> of
    /// <see cref="IsCelestialBodyId"/> (not a spacecraft nor a site), checked here, and an identifier SPICE names, checked
    /// under the lock by <see cref="ReadCelestialBodyInfo"/> (#680's rule).
    /// </summary>
    private static void ThrowIfNotACelestialCentre(ILocalizable observer, string paramName)
    {
        if (observer is not CelestialItem || !IsCelestialBodyId(observer.NaifId))
        {
            throw new ArgumentException(
                $"The centre of motion of the elements, {observer.Name} ({observer.NaifId}), is not a celestial body: a barycenter " +
                "(0 to 9), the Sun (10), a planet, a moon or a Lagrange point (101 to 999), or an identifier above 1000, not a " +
                "spacecraft nor a site.", paramName);
        }
    }

    // Elements that are not finite (#714): CONICS checks its domain with comparisons that NaN passes (conics.c) and the native
    // ellipse checks nothing, so that a NaN mean anomaly gave a finite state and a NaN inclination a NaN state, with no SPICE
    // error (measured with CSPICE N0067). Since #726 the constructors of the elements refuse them, naming the element, so this
    // check is no longer reached through these types; it stays, a bad argument checked before the native call. The
    // semi-major axis of a parabola is infinite: its periapsis radius is checked by ThrowIfOutsideConicsDomain
    private static void ThrowIfNotFinite(OrbitalParameters.KeplerianElements keplerianElements, string paramName)
    {
        ThrowIfNotFinite(paramName, ("e", keplerianElements.E), ("i", keplerianElements.I), ("RAAN", keplerianElements.RAAN),
            ("AOP", keplerianElements.AOP), ("M", keplerianElements.M));
    }

    private static void ThrowIfNotFinite(OrbitalParameters.EquinoctialElements equinoctialElements, string paramName)
    {
        ThrowIfNotFinite(paramName, ("p", equinoctialElements.P), ("f", equinoctialElements.F), ("g", equinoctialElements.G),
            ("h", equinoctialElements.H), ("k", equinoctialElements.K), ("L", equinoctialElements.L0));
    }

    private static void ThrowIfNotFinite(string paramName, params (string Name, double Value)[] elements)
    {
        foreach (var (name, value) in elements)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentException($"The element {name} of the elements is {value}: the elements must be finite.", paramName);
            }
        }
    }

    // The domain of EQNCPV (eqncpv.c: SPICE(BADSEMIAXIS) for a semi-major axis not above 0, SPICE(ECCOUTOFRANGE) for an
    // eccentricity sqrt(h^2 + k^2) above 0.9), checked before the native call: elements outside it are a bad argument (#711:
    // before, a zero state was returned)
    private static void ThrowIfOutsideEqncpvDomain(OrbitalParameters.EquinoctialElements equinoctialElements, string paramName)
    {
        var a = equinoctialElements.SemiMajorAxis();
        if (!(a > 0.0) || double.IsInfinity(a))
        {
            throw new ArgumentException($"The semi-major axis of the elements, {a} m, is not finite and above 0, as EQNCPV requires.", paramName);
        }

        var e = System.Math.Sqrt(equinoctialElements.G * equinoctialElements.G + equinoctialElements.F * equinoctialElements.F);
        if (!(e <= 0.9))
        {
            throw new ArgumentException($"The eccentricity of the elements, {e}, is above 0.9, the limit of EQNCPV; use EquinoctialElements.ToStateVector().",
                paramName);
        }
    }

    // The domain of CONICS (conics.c: SPICE(BADPERIAPSEVALUE) for a periapsis radius not above 0, an ellipse of negative
    // semi-major axis or a hyperbola of positive one; the eccentricity, never negative, is checked by KeplerianElements),
    // checked before the native call: elements outside it are a bad argument (#711: before, a zero state was returned)
    private static void ThrowIfOutsideConicsDomain(OrbitalParameters.KeplerianElements keplerianElements, string paramName)
    {
        var rp = keplerianElements.PerigeeRadius();
        if (!(rp > 0.0) || double.IsInfinity(rp))
        {
            throw new ArgumentException(
                $"The periapsis radius of the elements, {rp} m (a = {keplerianElements.A} m, e = {keplerianElements.E}), is not finite and above 0, as CONICS requires.",
                paramName);
        }
    }

    // The GM given to CONICS (conics.c: SPICE(BADGM) for a GM not above 0), PROP2B and OSCELT (prop2b.c, oscelt.c:
    // SPICE(NONPOSITIVEMASS)), finite: a GM outside it is a bad argument when the caller gives it (#711: before, conics_c's
    // error was left pending for the next native call; #714: prop2b_c's and oscltx_c's aborted the process or returned the
    // stack's contents)
    private static bool IsConicsGravitationalParameter(double gm) => gm > 0.0 && !double.IsInfinity(gm);

    // The GM of the observer, finite and above 0, or the InvalidOperationException of a body whose GM the kernel pool lacks
    // (#711, #714): the state of the observer, not an argument of the call
    private static double ObserverGravitationalParameter(ILocalizable observer, string subject, string routine)
    {
        var gm = observer.GM;
        if (!IsConicsGravitationalParameter(gm))
        {
            throw new InvalidOperationException(
                $"The centre of motion of {subject}, {observer.Name} ({observer.NaifId}), has a GM of {gm} m^3/s^2, where {routine} needs one finite and " +
                $"above 0: for a body read from the kernels, the kernel pool holds no BODY{observer.NaifId}_GM.");
        }

        return gm;
    }

    // A state the two-body calls give SPICE: a position and a velocity that are not finite raise no SPICE error, prop2b_c and
    // oscltx_c returning NaN (measured with CSPICE N0067), so they are a bad argument checked before the native call (#714)
    private static void ThrowIfNotFinite(OrbitalParameters.StateVector stateVector, string paramName)
    {
        var p = stateVector.Position;
        var v = stateVector.Velocity;
        if (!(double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z) && double.IsFinite(v.X) && double.IsFinite(v.Y) &&
              double.IsFinite(v.Z)))
        {
            throw new ArgumentException($"The state is not finite: position {p} m, velocity {v} m/s.", paramName);
        }
    }

    // The elements of oscltx_c, or the InvalidOperationException of its failure: the native layer writes NaN in every element
    // with SPICE's message (#714)
    private static bool IsFinite(in DTO.KeplerianElements elements) =>
        double.IsFinite(elements.PerifocalDistance) && double.IsFinite(elements.Eccentricity) && double.IsFinite(elements.Inclination) &&
        double.IsFinite(elements.AscendingNodeLongitude) && double.IsFinite(elements.PeriapsisArgument) &&
        double.IsFinite(elements.MeanAnomaly) && double.IsFinite(elements.TrueAnomaly);

    // The state of a native conversion, or the InvalidOperationException of its failure: a NaN state with SPICE's message,
    // a centre whose GM the kernel pool lacks (#711: before, the zero state, silently). Called under the lock, which holds
    // SPICE's message; the managed result is built after it, about the observer and in the frame of the input (#725, #664)
    private static StateVector CheckedState(StateVector state)
    {
        if (!IsFinite(state))
        {
            throw new InvalidOperationException($"The elements cannot be converted to a state vector: {LastProxyError()}");
        }

        return state;
    }

    /// <summary>
    /// The osculating conic elements of a state with the GM of its observer, through CSPICE's <c>oscltx_c</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="stateVector"/> is null.</exception>
    /// <exception cref="ArgumentException">The position or the velocity is not finite (#714).</exception>
    /// <exception cref="InvalidOperationException">The GM of the observer is not finite and above 0: a body whose GM the
    /// kernel pool lacks (#714: before, the elements <c>oscltx_c</c> never wrote were returned). Or <c>oscltx_c</c> failed
    /// (see <see cref="ConvertStateVectorToConicOrbitalElement(OrbitalParameters.StateVector, double)"/>).</exception>
    public IO.Astrodynamics.OrbitalParameters.KeplerianElements ConvertStateVectorToConicOrbitalElement(IO.Astrodynamics.OrbitalParameters.StateVector stateVector)
    {
        ArgumentNullException.ThrowIfNull(stateVector);
        ThrowIfNotFinite(stateVector, nameof(stateVector));
        return ConvertStateVectorToConicOrbitalElement(stateVector, ObserverGravitationalParameter(stateVector.Observer, "the state", "OSCELT"));
    }

    /// <summary>
    /// Convert state vector to Keplerian elements with an explicit GM value, through CSPICE's <c>oscltx_c</c>.
    /// </summary>
    /// <param name="stateVector"></param>
    /// <param name="gm">Gravitational parameter (m^3/s^2)</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"><paramref name="stateVector"/> is null.</exception>
    /// <exception cref="ArgumentException">The position or the velocity is not finite, which <c>oscltx_c</c> turns into NaN
    /// elements with no error (#714).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="gm"/> is not finite and above 0 (#714: before, the
    /// process ended when this was its first native call, otherwise the elements <c>oscltx_c</c> never wrote were
    /// returned).</exception>
    /// <exception cref="InvalidOperationException"><c>oscltx_c</c> failed: a zero position, a zero velocity, or a position and
    /// a velocity too close to parallel (oscelt.c, <c>SPICE(DEGENERATECASE)</c>); the message is SPICE's (#714: before, the
    /// elements it never wrote were returned).</exception>
    public IO.Astrodynamics.OrbitalParameters.KeplerianElements ConvertStateVectorToConicOrbitalElement(IO.Astrodynamics.OrbitalParameters.StateVector stateVector, double gm)
    {
        ArgumentNullException.ThrowIfNull(stateVector);
        ThrowIfNotFinite(stateVector, nameof(stateVector));
        if (!IsConicsGravitationalParameter(gm))
        {
            throw new ArgumentOutOfRangeException(nameof(gm), gm, "The GM given to OSCELT must be finite and above 0.");
        }

        var input = stateVector.Convert();
        DTO.KeplerianElements elements;
        lock (lockObject)
        {
            elements = ConvertStateVectorToConicOrbitalElementProxy(input, gm);
            if (!IsFinite(elements))
            {
                throw new InvalidOperationException($"The state cannot be converted to conic elements: {LastProxyError()}");
            }
        }

        // After the lock, about the caller's observer and frame (#725, #664)
        return elements.Convert(stateVector.Observer, stateVector.Frame);
    }

    /// <summary>
    /// The state of equinoctial elements, through CSPICE's <c>eqncpv_c</c> in the native layer, at the epoch of the
    /// elements, about their observer and in their frame (#725).
    /// </summary>
    /// <remarks>
    /// EQNCPV takes the mean longitude λ = Ω + ω + M, which the elements give by
    /// <see cref="OrbitalParameters.OrbitalParameters.MeanLongitude"/> (#450: before 11.0.0 the true longitude L0 was passed
    /// in its place, the state of another point of the orbit, 1029 km off for an ordinary LEO).
    /// </remarks>
    /// <exception cref="ArgumentException">The centre of the elements is not a celestial body SPICE names: a spacecraft, a
    /// site, or an identifier no loaded kernel names (#711: before, the native layer ended the process for a centre with a
    /// GM, and the conversion of the result threw for the others).</exception>
    /// <exception cref="ArgumentException">The elements are outside the domain of <c>EQNCPV</c>: a semi-major axis that is not
    /// above 0, or an eccentricity above 0.9 (#711: before, a zero state was returned).</exception>
    /// <exception cref="ArgumentException">An element is not finite (#714: before, the native layer returned a NaN state, which
    /// threw an unrelated message).</exception>
    /// <exception cref="InvalidOperationException">The kernel pool has no GM for the centre (#711: before, a zero state was
    /// returned); the message is SPICE's.</exception>
    public IO.Astrodynamics.OrbitalParameters.StateVector ConvertEquinoctialElementsToStateVector(IO.Astrodynamics.OrbitalParameters.EquinoctialElements equinoctialElements)
    {
        if (equinoctialElements == null) throw new ArgumentNullException(nameof(equinoctialElements));
        ThrowIfNotFinite(equinoctialElements, nameof(equinoctialElements));
        ThrowIfOutsideEqncpvDomain(equinoctialElements, nameof(equinoctialElements));
        ThrowIfNotACelestialCentre(equinoctialElements.Observer, nameof(equinoctialElements));
        var input = equinoctialElements.Convert();
        StateVector state;
        lock (lockObject)
        {
            ReadCelestialBodyInfo(equinoctialElements.Observer.NaifId, nameof(equinoctialElements));
            state = CheckedState(ConvertEquinoctialElementsToStateVectorProxy(input));
        }

        // After the lock, about the caller's observer and frame (#725, #664)
        return state.Convert(equinoctialElements.Observer, equinoctialElements.Frame);
    }

    /// <summary>
    /// The state of Keplerian elements at their epoch: an ellipse through the native conic elements, an open orbit through
    /// CSPICE's <c>conics_c</c> (<see cref="ConvertConicElementsToStateVector(OrbitalParameters.KeplerianElements, Time)"/>).
    /// </summary>
    /// <exception cref="ArgumentException">For an ellipse, the centre of the elements is not a celestial body SPICE names: a
    /// spacecraft, a site, or an identifier no loaded kernel names (#711: before, the native layer ended the process for a
    /// centre with a GM, and the conversion of the result threw for the others).</exception>
    /// <exception cref="ArgumentException">The periapsis radius of the elements is not above 0, outside the domain of
    /// <c>CONICS</c>: an ellipse of negative semi-major axis, a hyperbola of positive one (#711: before, a zero state was
    /// returned).</exception>
    /// <exception cref="ArgumentException">An element (e, i, RAAN, AOP, M) is not finite (#714: before, a NaN mean anomaly gave
    /// a finite state, silently, and a NaN inclination a NaN state, which threw an unrelated message).</exception>
    /// <exception cref="InvalidOperationException">The kernel pool has no GM for the centre (#711: before, a zero state was
    /// returned for an ellipse, and an open orbit threw an unrelated message); for an ellipse the message is SPICE's.</exception>
    public IO.Astrodynamics.OrbitalParameters.StateVector ConvertConicElementsToStateVector(IO.Astrodynamics.OrbitalParameters.KeplerianElements keplerianElements)
    {
        if (keplerianElements == null) throw new ArgumentNullException(nameof(keplerianElements));
        ThrowIfNotFinite(keplerianElements, nameof(keplerianElements));
        ThrowIfOutsideConicsDomain(keplerianElements, nameof(keplerianElements));
        // The native conic elements solve the elliptic Kepler equation when they are built, an iteration that may never
        // end on an open orbit (#372): an open orbit goes to SPICE CONICS at the epoch of its elements, as the other
        // overloads
        if (keplerianElements.E >= 1.0)
        {
            return ConvertConicElementsToStateVector(keplerianElements, keplerianElements.Epoch);
        }

        ThrowIfNotACelestialCentre(keplerianElements.Observer, nameof(keplerianElements));
        var input = keplerianElements.Convert();
        StateVector state;
        lock (lockObject)
        {
            ReadCelestialBodyInfo(keplerianElements.Observer.NaifId, nameof(keplerianElements));
            state = CheckedState(ConvertConicElementsToStateVectorProxy(input));
        }

        // After the lock, about the caller's observer and frame (#725, #664)
        return state.Convert(keplerianElements.Observer, keplerianElements.Frame);
    }

    /// <summary>
    /// The state of Keplerian elements at an epoch, through CSPICE's <c>conics_c</c> with the GM of their observer.
    /// </summary>
    /// <exception cref="ArgumentException">The periapsis radius of the elements is not above 0, outside the domain of
    /// <c>CONICS</c> (#711: before, a zero state was returned).</exception>
    /// <exception cref="ArgumentException">An element (e, i, RAAN, AOP, M) is not finite (#714: before, a NaN mean anomaly gave
    /// a finite state, silently, and a NaN inclination a NaN state, which threw an unrelated message).</exception>
    /// <exception cref="InvalidOperationException">The GM of the observer is not finite and above 0: a body whose GM the
    /// kernel pool lacks (#711: before, <c>conics_c</c>'s error was left pending and an unrelated message thrown).</exception>
    public IO.Astrodynamics.OrbitalParameters.StateVector ConvertConicElementsToStateVector(IO.Astrodynamics.OrbitalParameters.KeplerianElements keplerianElements, Time epoch)
    {
        if (keplerianElements == null) throw new ArgumentNullException(nameof(keplerianElements));
        ThrowIfNotFinite(keplerianElements, nameof(keplerianElements));
        ThrowIfOutsideConicsDomain(keplerianElements, nameof(keplerianElements));
        var gm = ObserverGravitationalParameter(keplerianElements.Observer, "the elements", "CONICS");
        return ConvertConicElementsToStateVector(keplerianElements, epoch, gm);
    }

    /// <summary>
    /// Convert Keplerian elements to state vector at a given epoch with an explicit GM value, through CSPICE's
    /// <c>conics_c</c>.
    /// </summary>
    /// <param name="keplerianElements"></param>
    /// <param name="epoch"></param>
    /// <param name="gm">Gravitational parameter (m^3/s^2)</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">The periapsis radius of the elements is not above 0, outside the domain of
    /// <c>CONICS</c> (#711: before, a zero state was returned).</exception>
    /// <exception cref="ArgumentException">An element (e, i, RAAN, AOP, M) is not finite (#714: before, a NaN mean anomaly gave
    /// a finite state, silently, and a NaN inclination a NaN state, which threw an unrelated message).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="gm"/> is not finite and above 0 (#711: before,
    /// <c>conics_c</c>'s error was left pending for the next native call and an unrelated message thrown).</exception>
    /// <exception cref="InvalidOperationException"><c>conics_c</c> failed; the message is SPICE's.</exception>
    public IO.Astrodynamics.OrbitalParameters.StateVector ConvertConicElementsToStateVector(IO.Astrodynamics.OrbitalParameters.KeplerianElements keplerianElements, Time epoch, double gm)
    {
        if (keplerianElements == null) throw new ArgumentNullException(nameof(keplerianElements));
        ThrowIfNotFinite(keplerianElements, nameof(keplerianElements));
        ThrowIfOutsideConicsDomain(keplerianElements, nameof(keplerianElements));
        if (!IsConicsGravitationalParameter(gm))
        {
            throw new ArgumentOutOfRangeException(nameof(gm), gm, "The GM given to CONICS must be finite and above 0.");
        }

        var input = keplerianElements.Convert();
        double et = epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds;
        StateVector state;
        lock (lockObject)
        {
            state = CheckedState(ConvertConicElementsToStateVectorAtEpochProxy(input, et, gm));
        }

        // After the lock, about the caller's observer and frame (#725, #664)
        return state.Convert(keplerianElements.Observer, keplerianElements.Frame);
    }

    /// <summary>
    /// The state propagated by two-body motion with the GM of its observer, through CSPICE's <c>prop2b_c</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="stateVector"/> is null.</exception>
    /// <exception cref="ArgumentException">The position or the velocity is not finite (#714).</exception>
    /// <exception cref="InvalidOperationException">The GM of the observer is not finite and above 0: a body whose GM the
    /// kernel pool lacks (#714: before, the state <c>prop2b_c</c> never wrote was returned). Or <c>prop2b_c</c> failed (see
    /// <see cref="Propagate2Bodies(OrbitalParameters.StateVector, double, TimeSpan)"/>).</exception>
    public OrbitalParameters.StateVector Propagate2Bodies(OrbitalParameters.StateVector stateVector, TimeSpan dt)
    {
        ArgumentNullException.ThrowIfNull(stateVector);
        ThrowIfNotFinite(stateVector, nameof(stateVector));
        return Propagate2Bodies(stateVector, ObserverGravitationalParameter(stateVector.Observer, "the state", "PROP2B"), dt);
    }

    /// <summary>
    /// Propagate a state vector using two-body mechanics with an explicit GM value, through CSPICE's <c>prop2b_c</c>.
    /// </summary>
    /// <param name="stateVector"></param>
    /// <param name="gm">Gravitational parameter (m^3/s^2)</param>
    /// <param name="dt"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"><paramref name="stateVector"/> is null.</exception>
    /// <exception cref="ArgumentException">The position or the velocity is not finite, which <c>prop2b_c</c> turns into a NaN
    /// state with no error (#714).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="gm"/> is not finite and above 0 (#714: before, the
    /// process ended when this was its first native call, otherwise the state <c>prop2b_c</c> never wrote was
    /// returned).</exception>
    /// <exception cref="InvalidOperationException"><c>prop2b_c</c> failed: a zero position, a zero velocity, a position
    /// parallel to the velocity (prop2b.c, <c>SPICE(ZEROPOSITION)</c>, <c>SPICE(ZEROVELOCITY)</c>,
    /// <c>SPICE(NONCONICMOTION)</c>, which carry no long message, so the message is the short one), or a
    /// <paramref name="dt"/> out of its range (<c>SPICE(DTOUTOFRANGE)</c>); the message is SPICE's (#714: before, the state
    /// it never wrote was returned).</exception>
    public OrbitalParameters.StateVector Propagate2Bodies(OrbitalParameters.StateVector stateVector, double gm, TimeSpan dt)
    {
        ArgumentNullException.ThrowIfNull(stateVector);
        ThrowIfNotFinite(stateVector, nameof(stateVector));
        if (!IsConicsGravitationalParameter(gm))
        {
            throw new ArgumentOutOfRangeException(nameof(gm), gm, "The GM given to PROP2B must be finite and above 0.");
        }

        var input = stateVector.Convert();
        StateVector state;
        lock (lockObject)
        {
            state = Propagate2BodiesProxy(input, gm, dt.TotalSeconds);
            if (!IsFinite(state))
            {
                throw new InvalidOperationException($"The state cannot be propagated by two-body motion: {LastProxyError()}");
            }
        }

        // After the lock, about the caller's observer and frame (#725, #664)
        return state.Convert(stateVector.Observer, stateVector.Frame);
    }

    /// <summary>
    /// The state propagated by two-body motion to <paramref name="targetEpoch"/> with the GM of its observer
    /// (<see cref="Propagate2Bodies(OrbitalParameters.StateVector, TimeSpan)"/>).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="stateVector"/> is null.</exception>
    public OrbitalParameters.StateVector Propagate2Bodies(OrbitalParameters.StateVector stateVector, Time targetEpoch)
    {
        ArgumentNullException.ThrowIfNull(stateVector);
        return Propagate2Bodies(stateVector, targetEpoch - stateVector.Epoch);
    }
}