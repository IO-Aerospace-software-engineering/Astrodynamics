// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Exceptions;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.TimeSystem;
using BodyInfo = IO.Astrodynamics.DataProvider.CelestialBodyInfo;

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
/// The information of an unknown identifier is a bad argument, not missing data (#680, decided by S. Guillet, 2026-11-14):
/// <see cref="MemoryDataProvider.GetCelestialBodyInfo"/>, <c>new CelestialBody(id)</c> and
/// <see cref="CelestialItem.Create"/> of an identifier the provider has no information on throw the
/// <see cref="ArgumentException"/> naming <c>naifId</c> that the SPICE provider throws for the same identifiers, checked
/// against SPICE, the reference, before the swap; a body whose barycentre's information the provider lacks still builds
/// without an orbit, as on SPICE, which names every barycentre. A Lagrange point (391 to 395) is no unknown identifier for
/// <see cref="CelestialItem.Create"/>, which reads its state and not its information: its missing state throws
/// <see cref="InvalidOperationException"/>, as SPICE for L3, whose state its kernels lack.
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
    // The angle between the cache's orientation and the closed form at t0 + 1830 s, between two grid points: 1.149e-16 rad,
    // the same on Linux x64, Windows x64 and macOS arm64 (#680, CI run 43595573884, 2026-11-14); twice the worst, rounded
    // up at the second significant digit
    private const double CacheTolerance = 2.3e-16;

    private static readonly Time T0 = new(2024, 6, 21, 12, 0, 0, TimeScale.TDB);

    private static readonly BodyInfo EarthInfo = new(399, "EARTH", 10, 3, new Vector3(6378137.0, 6378137.0, 6356752.0), 3.986004418e14, "ITRF93",
        13000, 1.08e-3, 0.0, 0.0);

    private static readonly BodyInfo EarthBarycenterInfo = new(3, "EARTH BARYCENTER", 0, 0, new Vector3(0.0, 0.0, 0.0), 4.0350323550225975e14, "",
        0, 0.0, 0.0, 0.0);

    private static readonly BodyInfo SsbInfo = new(0, "SOLAR SYSTEM BARYCENTER", 0, 0, new Vector3(0.0, 0.0, 0.0), 1.3289e20, "", 0, 0.0, 0.0, 0.0);

    private static readonly BodyInfo SunInfo = new(10, "SUN", 0, 0, new Vector3(696000000.0, 696000000.0, 696000000.0), 1.32712440041e20, "IAU_SUN",
        10010, 0.0, 0.0, 0.0);

    private static readonly BodyInfo MoonInfo = new(301, "MOON", 399, 3, new Vector3(1737400.0, 1737400.0, 1737400.0), 4.9e12, "IAU_MOON", 31001,
        0.0, 0.0, 0.0);

    internal static int Run(string kernels)
    {
        try
        {
            SpiceApi.Instance.LoadKernels(new DirectoryInfo(kernels));
            // The static frames capture the provider when they are built: built here, under SPICE
            GC.KeepAlive(Frame.ICRF);
            GC.KeepAlive(Frame.ECLIPTIC_J2000);

            // The reference: SPICE outside de440s
            var early = new Time(1500, 1, 1, 0, 0, 0, TimeScale.TDB);
            CheckWithoutOrbit("SPICE, the Earth at 1500", new CelestialBody(399, Frame.ICRF, early));
            // The reference of the unknown ids: what SPICE throws for each, through each public path
            var spiceUnknownIds = ThrownByUnknownIds(new SpiceDataProvider());
            // The reference of the Lagrange points, which CelestialItem.Create builds without reading their information
            var spiceLagrangeIds = ThrownByIds(new SpiceDataProvider(), LagrangeIds);

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

            CheckUnknownIds(spiceUnknownIds);
            CheckLagrangeIds(spiceLagrangeIds);
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

    // The ids neither provider has information on, the list of MemoryDataProviderTests.UnknownIdValues: celestial ids SPICE
    // does not name (#672), then a spacecraft's, an id from 11 to 100, and 1000
    private static readonly int[] UnknownIds = [398, 12345678, 2000003, 99999999, -12345, 50, 1000];

    // The public paths through which an id reaches the body information: the read of the provider, the global one's when it
    // is set, and the constructors that read the global provider
    private static readonly (string Path, Func<IDataProvider, int, object> Read)[] UnknownIdPaths =
    [
        ("GetCelestialBodyInfo", (provider, id) => provider.GetCelestialBodyInfo(id)),
        ("new CelestialBody", (_, id) => new CelestialBody(id)),
        ("CelestialItem.Create", (_, id) => CelestialItem.Create(id))
    ];

    // The Lagrange points L1 to L5: SPICE names none of them (Data/SolarSystem), and its kernels hold the states of all but
    // L3 (no L3_de431.bsp)
    private static readonly int[] LagrangeIds = [391, 392, 393, 394, 395];

    private const int L3 = 393;

    private static Dictionary<(int Id, string Path), Exception> ThrownByUnknownIds(IDataProvider provider) =>
        ThrownByIds(provider, UnknownIds);

    // The exception each path throws for each id, null for none; the global provider is the one given
    private static Dictionary<(int Id, string Path), Exception> ThrownByIds(IDataProvider provider, int[] ids)
    {
        var thrown = new Dictionary<(int Id, string Path), Exception>();
        foreach (int id in ids)
        {
            foreach (var (path, read) in UnknownIdPaths)
            {
                try
                {
                    read(provider, id);
                    thrown[(id, path)] = null;
                }
                catch (Exception e)
                {
                    thrown[(id, path)] = e;
                }
            }
        }

        return thrown;
    }

    private static string Describe(Exception e) => e switch
    {
        null => "no exception",
        ArgumentException argument => $"{e.GetType().Name} ({argument.ParamName})",
        _ => $"{e.GetType().Name}: {e.Message}"
    };

    // #680, decided by S. Guillet, 2026-11-14: a bad argument gives ArgumentException. On a memory provider holding the
    // Earth's, its barycentre's, the SSB's and the Sun's information, each unknown id throws through each path the
    // exception type and the parameter name SPICE throws, the reference: ArgumentException naming naifId (#672)
    private static void CheckUnknownIds(Dictionary<(int Id, string Path), Exception> spice)
    {
        var provider = Provider(EarthInfo, EarthBarycenterInfo, SsbInfo, SunInfo);
        Configuration.Instance.SetDataProvider(provider);
        var memory = ThrownByUnknownIds(provider);
        foreach (var (key, reference) in spice)
        {
            var thrown = memory[key];
            Check($"unknown id {key.Id}, {key.Path}: SPICE {Describe(reference)}",
                reference?.GetType() == typeof(ArgumentException) && ((ArgumentException)reference).ParamName == "naifId");
            Check($"unknown id {key.Id}, {key.Path}: memory {Describe(thrown)}, SPICE {Describe(reference)}",
                thrown?.GetType() == reference.GetType() && ((ArgumentException)thrown).ParamName == ((ArgumentException)reference).ParamName);
        }
    }

    // A Lagrange point is no unknown id for CelestialItem.Create, which builds a LagrangePoint from the Earth barycentre and
    // the point's state without reading the point's information: its missing state is missing data. GetCelestialBodyInfo
    // and new CelestialBody of an id SPICE does not name throw ArgumentException naming naifId on both providers; Create
    // builds the point on SPICE where its kernels hold its state, throws InvalidOperationException where they do not (L3),
    // and throws InvalidOperationException for every point on a memory provider without their states, the type of SPICE's
    // missing L3. The static barycentres were built under SPICE by the reference (#680 review, 2026-11-14)
    private static void CheckLagrangeIds(Dictionary<(int Id, string Path), Exception> spice)
    {
        var provider = Provider(EarthInfo, EarthBarycenterInfo, SsbInfo, SunInfo);
        Configuration.Instance.SetDataProvider(provider);
        var memory = ThrownByIds(provider, LagrangeIds);
        foreach (var (key, reference) in spice)
        {
            var thrown = memory[key];
            if (key.Path == "CelestialItem.Create")
            {
                // SPICE's missing state is a SpiceException, an InvalidOperationException (R03 of #668)
                var spiceExpected = key.Id == L3 ? typeof(SpiceException) : null;
                Check($"Lagrange id {key.Id}, {key.Path}: SPICE {Describe(reference)}", reference?.GetType() == spiceExpected);
                Check($"Lagrange id {key.Id}, {key.Path}: memory without its state {Describe(thrown)}, SPICE without L3's state " +
                      $"{Describe(spice[(L3, key.Path)])}",
                    thrown?.GetType() == typeof(InvalidOperationException) && spice[(L3, key.Path)] is InvalidOperationException);
                continue;
            }

            Check($"Lagrange id {key.Id}, {key.Path}: SPICE {Describe(reference)}",
                reference?.GetType() == typeof(ArgumentException) && ((ArgumentException)reference).ParamName == "naifId");
            Check($"Lagrange id {key.Id}, {key.Path}: memory {Describe(thrown)}, SPICE {Describe(reference)}",
                thrown?.GetType() == reference.GetType() && ((ArgumentException)thrown).ParamName == ((ArgumentException)reference).ParamName);
        }
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
