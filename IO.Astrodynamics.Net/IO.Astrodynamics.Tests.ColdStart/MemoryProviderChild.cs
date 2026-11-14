// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.TimeSystem;
using BodyInfo = IO.Astrodynamics.DTO.CelestialBody;

namespace IO.Astrodynamics.Tests.ColdStart;

/// <summary>
/// The mode <c>memory-provider</c> of the child process (#680): the library on a <see cref="MemoryDataProvider"/> set as
/// the global provider, which the test process never does (#631: an object another test class builds during the swap, and a
/// static frame first built during it, keep the provider for the whole process).
/// </summary>
/// <remarks>
/// <para>
/// The provider's missing data throw <see cref="InvalidOperationException"/>, as SPICE's do (#668, C10), and the library's
/// handlers of missing data catch that type: a body whose barycentre's data the provider lacks builds without an orbit and
/// with an infinite sphere of influence, as the Earth built on SPICE outside de440s, and the orientation cache of a
/// propagation over a memory frame whose entries do not reach its buffer points keeps those points unavailable. Before
/// #680 the provider threw <see cref="KeyNotFoundException"/> and <see cref="ArgumentException"/>, which nothing caught.
/// </para>
/// <para>
/// The kernels are loaded and the static frames built under SPICE first, the reference: the Earth built at 1500-01-01,
/// outside de440s, has a null orbit and an infinite sphere of influence. Exit 0 when every check holds, 1 with the failed
/// check or the exception on the standard error. The measured figures are written to the standard output as
/// <c>MEASURE</c> lines, which the test writes to its output.
/// </para>
/// </remarks>
internal static class MemoryProviderChild
{
    // The angle between the cache's orientation and the closed form at t0 + 1830 s, between two grid points: measured
    // 1.149e-16 rad on Linux x64 (#680, 2026-11-14); twice the worst, provisional until the three platforms are measured
    private const double CacheTolerance = 2.3e-16;

    private static readonly Time T0 = new(2024, 6, 21, 12, 0, 0, frame: TimeFrame.TDBFrame);

    private static readonly BodyInfo EarthInfo = new(399, 10, 3, "EARTH", new(6378137.0, 6378137.0, 6356752.0), 3.986004418e14, "ITRF93",
        13000, 1.08e-3, 0.0, 0.0);

    private static readonly BodyInfo EarthBarycenterInfo = new(3, 0, 0, "EARTH BARYCENTER", new(0.0, 0.0, 0.0), 4.0350323550225975e14, "",
        0, 0.0, 0.0, 0.0);

    private static readonly BodyInfo SsbInfo = new(0, 0, 0, "SOLAR SYSTEM BARYCENTER", new(0.0, 0.0, 0.0), 1.3289e20, "", 0, 0.0, 0.0, 0.0);

    private static readonly BodyInfo SunInfo = new(10, 0, 0, "SUN", new(696000000.0, 696000000.0, 696000000.0), 1.32712440041e20, "IAU_SUN",
        10010, 0.0, 0.0, 0.0);

    private static readonly BodyInfo MoonInfo = new(301, 399, 3, "MOON", new(1737400.0, 1737400.0, 1737400.0), 4.9e12, "IAU_MOON", 31001,
        0.0, 0.0, 0.0);

    internal static int Run(string kernels)
    {
        try
        {
            SpiceAPI.Instance.LoadKernels(new DirectoryInfo(kernels));
            // The static frames capture the provider when they are built: built here, under SPICE
            GC.KeepAlive(Frame.ICRF);
            GC.KeepAlive(Frame.ECLIPTIC_J2000);

            // The reference: SPICE outside de440s
            var early = new Time(1500, 1, 1, 0, 0, 0, frame: TimeFrame.TDBFrame);
            CheckWithoutOrbit("SPICE, the Earth at 1500", new CelestialBody(399, Frame.ICRF, early));

            // The Earth's info only; then with its barycentre's and the SSB's, no state
            CheckWithoutOrbit("memory, the Earth's info only", Build(399, T0, EarthInfo));
            CheckWithoutOrbit("memory, the Earth's barycentre's info", Build(399, T0, EarthInfo, EarthBarycenterInfo));
            CheckWithoutOrbit("memory, three infos, no state", Build(399, T0, EarthInfo, EarthBarycenterInfo, SsbInfo));

            // States of the Earth, its barycentre and the Sun over one hour: built a day earlier, without an orbit; within the hour, with
            // one (the data path works, so the null orbit comes from the missing data)
            var provider = Provider(EarthInfo, EarthBarycenterInfo, SsbInfo, SunInfo);
            foreach (var t in new[] { T0, T0.AddSeconds(3600.0) })
            {
                provider.AddStateVector(3, t, new StateVector(new Vector3(1.5e11, 0.0, 0.0), new Vector3(0.0, 3.0e4, 0.0),
                    new Barycenter(0, t), t, Frame.ICRF));
                provider.AddStateVector(10, t, new StateVector(Vector3.Zero, Vector3.Zero, new Barycenter(0, t), t, Frame.ICRF));
                provider.AddStateVector(399, t, new StateVector(new Vector3(1.5e11 + 4.6e6, 0.0, 0.0), new Vector3(0.0, 3.0e4 + 12.0, 0.0),
                    new Barycenter(0, t), t, Frame.ICRF));
            }

            Configuration.Instance.SetDataProvider(provider);
            CheckWithoutOrbit("memory, a day before the stored states", new CelestialBody(399, Frame.ICRF, T0.AddDays(-1.0)));
            var within = new CelestialBody(399, Frame.ICRF, T0);
            Check("memory, at a stored state, the orbit", within.InitialOrbitalParameters != null);
            Check("memory, at a stored state, a finite sphere of influence", double.IsFinite(within.SphereOfInfluence));
            Check("memory, at a stored state, the position about the barycentre",
                within.InitialOrbitalParameters.ToStateVector().Position == new Vector3(4.6e6, 0.0, 0.0));

            // The Moon with its own info only
            CheckWithoutOrbit("memory, the Moon's info only", Build(301, T0, MoonInfo));

            CheckOrientationCache();
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"memory-provider threw: {e}");
            return 1;
        }
    }

    private static MemoryDataProvider Provider(params BodyInfo[] infos)
    {
        var provider = new MemoryDataProvider();
        provider.AddCelestialBodyInfo(infos);
        return provider;
    }

    private static CelestialBody Build(int naifId, Time epoch, params BodyInfo[] infos)
    {
        Configuration.Instance.SetDataProvider(Provider(infos));
        return new CelestialBody(naifId, Frame.ICRF, epoch);
    }

    private static void CheckWithoutOrbit(string label, CelestialBody body)
    {
        Check($"{label}, no orbit", body.InitialOrbitalParameters == null);
        Check($"{label}, infinite sphere of influence", double.IsPositiveInfinity(body.SphereOfInfluence));
    }

    // Two orientations of a frame an hour apart, rotations of 0.1 and 0.7 rad about one fixed axis: the cache over that hour
    // reads its buffer points (two grid steps before the start and after the end) outside the entries; at t0 + 1830 s,
    // between two grid points, its orientation is the rotation of 0.1 + 0.6 · 1830 / 3600 rad about the axis (the exact SLERP
    // of the provider, #548), the closed form
    private static void CheckOrientationCache()
    {
        var provider = new MemoryDataProvider();
        Configuration.Instance.SetDataProvider(provider);
        var frame = new Frame("MEMORY_CACHE_FRAME");
        var axis = new Vector3(0.3, -0.2, 0.9) / new Vector3(0.3, -0.2, 0.9).Magnitude();
        var end = T0.AddSeconds(3600.0);
        provider.AddStateOrientationToICRF(frame, T0, new StateOrientation(new Quaternion(axis, 0.1), Vector3.Zero, T0, frame));
        provider.AddStateOrientationToICRF(frame, end, new StateOrientation(new Quaternion(axis, 0.7), Vector3.Zero, end, frame));

        var cache = new PropagationFrameOrientationCache(new Window(T0, end), frame, TimeSpan.FromSeconds(60.0));

        var rotation = cache.GetOrientation(T0.AddSeconds(1830.0)).Rotation;
        var expected = new Quaternion(axis, 0.1 + 0.6 * 1830.0 / 3600.0);
        var delta = rotation * expected.Conjugate();
        double angle = 2.0 * System.Math.Atan2(delta.VectorPart.Magnitude(), System.Math.Abs(delta.W));
        Console.WriteLine($"MEASURE #680 orientation cache over a memory frame at t0 + 1830 s: {angle:E3} rad");
        Check($"the cache's orientation within {CacheTolerance:E1} rad of the closed form ({angle:E3} rad)", angle <= CacheTolerance);
    }

    private static void Check(string label, bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Failed: {label}");
        }

        Console.WriteLine($"OK {label}");
    }
}
