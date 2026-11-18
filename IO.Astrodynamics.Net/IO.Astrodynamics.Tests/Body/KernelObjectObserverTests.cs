// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The SPK of NOTO (398989, a built-in name of SPICE whose identifier has the form of a site of 398, a body SPICE does
/// not name) that the tests of <see cref="KernelObjectObserverTests"/> read: a station fixed on the Earth at 14.989° E,
/// 36.876° N, 100 m (about NOTO's place in Sicily; any fixed point would do), its states relative to the Earth in J2000
/// those of a <see cref="Site"/> built from the same coordinates, every 60 s over a day about 2024-01-01 12:00 TDB, written
/// by <see cref="SpiceAPI.WriteEphemeris"/> and loaded for the class, unloaded in <see cref="Dispose"/>. No kernel of
/// <c>Data/SolarSystem</c> holds 398989 (its SPKs hold 1 to 10, 199, 299, 301, 399, 391 to 395 and the 17 DSN stations of
/// <c>earthstns_itrf93_201023.bsp</c>, spiceypy 8.4.0 <c>spkobj</c>, 2026-11-18), which <see cref="KernelShadowing"/>
/// checks.
/// </summary>
public sealed class NotoKernel : IDisposable
{
    internal const int NotoId = 398989;
    internal static readonly TimeSystem.Time Epoch = TimeSystem.Time.CreateTDB(757425600.0); // 2024-01-01 12:00:00 TDB

    internal static Planetodetic Coordinates => new(14.989 * IO.Astrodynamics.Constants.Deg2Rad, 36.876 * IO.Astrodynamics.Constants.Deg2Rad, 100.0);

    private readonly DirectoryInfo _directory;
    internal FileInfo Spk { get; }

    public NotoKernel()
    {
        TestKernels.EnsureSolarSystem();
        _directory = Directory.CreateTempSubdirectory("noto750-");
        Spk = new FileInfo(Path.Combine(_directory.FullName, "noto750.bsp"));
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var site = new Site(989, "N750", earth, Coordinates);
        var states = site.GetEphemeris(new Window(Epoch.AddHours(-12.0), Epoch.AddHours(12.0)), earth, Frames.Frame.ICRF, Aberration.None,
            TimeSpan.FromSeconds(60.0)).Select(s => s.ToStateVector()).ToArray();
        SpiceAPI.Instance.WriteEphemeris(Spk, NotoId, states);
        Assert.Empty(KernelShadowing.Find([Spk], KernelShadowing.SolarSystem));
        SpiceAPI.Instance.LoadKernels(Spk);
    }

    public void Dispose()
    {
        SpiceAPI.Instance.UnloadKernels(Spk);
        _directory.Delete(true);
    }
}

/// <summary>
/// #750: SPICE's built-in NOTO (398989) and NEW NORCIA (398990) as observers. Their identifiers have the form of a site of
/// 398 (<see cref="SpiceAPI.IsSiteId"/>), a body SPICE does not name (<see cref="SpiceAPI.IsSiteOfNamedBody"/>), so they
/// are objects of the kernels read by their identifier (internal <see cref="KernelObject"/>): the read is
/// <c>spkezr_c</c>'s, the state with their SPK loaded and SPICE's missing-data error otherwise. Before the fix the reads by
/// identifiers built them as a site of the body 398 and threw <see cref="ArgumentException"/> "The NAIF identifier 398 has
/// no name in SPICE" (measured on f70ee32), and a frame centred on NOTO threw the same.
/// </summary>
public class KernelObjectObserverTests : IClassFixture<NotoKernel>
{
    private const int NewNorciaId = 398990;
    private readonly ITestOutputHelper _output;

    private static readonly Aberration[] Flags =
    [
        Aberration.None, Aberration.LT, Aberration.LTS, Aberration.CN, Aberration.CNS, Aberration.XLT, Aberration.XLTS,
        Aberration.XCN, Aberration.XCNS
    ];

    // The NOTO SPK against the site it was written from, the interpolation of the native writer's segment between its 60 s
    // states: 8.872e-9 m and 6.483e-13 m/s (Linux x64, measured on f70ee32 with the fix, 2026-11-18), twice the worst
    // rounded up at two digits; provisional, to be set again from the Windows x64 and macOS arm64 CI logs
    private const double SpkPositionTolerance = 1.8e-8;
    private const double SpkVelocityTolerance = 1.3e-12;

    // A frame centred on NOTO against its closed form, the same arithmetic as the library's (the light time of NOTO from the
    // site, its rate and the rotation of the frame at its epoch): 0 on Linux x64 (f70ee32 with the fix, 2026-11-18), as the
    // frame by name centred on LRO of LightTimeCorrectionTests is 0 on the three platforms; provisional
    private const double FramePositionTolerance = 0.0;
    private const double FrameVelocityTolerance = 0.0;

    public KernelObjectObserverTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static TimeSystem.Time[] Epochs => [NotoKernel.Epoch, NotoKernel.Epoch.AddSeconds(7000.0), NotoKernel.Epoch.AddSeconds(-31000.0)];

    [Fact]
    public void ReadEphemeris_FromNoto_IsTheReadOfItsIdentifier()
    {
        foreach (var epoch in Epochs)
        {
            // Act
            var fromNoto = SpiceAPI.Instance.ReadEphemeris(epoch, NotoKernel.NotoId, 399, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            var toNoto = SpiceAPI.Instance.ReadEphemeris(epoch, 399, NotoKernel.NotoId, Frames.Frame.ICRF, Aberration.None).ToStateVector();

            // Assert: the observer is NOTO, read by its identifier; the geometric state is the negation of NOTO from the Earth
            Assert.IsType<KernelObject>(fromNoto.Observer);
            Assert.Equal(NotoKernel.NotoId, fromNoto.Observer.NaifId);
            Assert.Equal("NOTO", fromNoto.Observer.Name);
            Assert.True(fromNoto.Observer.IsSpiceBacked);
            Assert.Equal(toNoto.Position.Inverse(), fromNoto.Position);
            Assert.Equal(toNoto.Velocity.Inverse(), fromNoto.Velocity);
        }
    }

    /// <summary>NOTO as target, the control: its SPK holds the states of the site it was written from.</summary>
    [Fact]
    public void ReadEphemeris_OfNoto_IsTheSiteItWasWrittenFrom()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var site = new Site(989, "N750", earth, NotoKernel.Coordinates);
        double worstPosition = 0.0, worstVelocity = 0.0;
        for (int k = 0; k <= 48; k++)
        {
            var epoch = NotoKernel.Epoch.AddSeconds(-11.0 * 3600.0 + 1650.5 * k);
            var read = SpiceAPI.Instance.ReadEphemeris(epoch, 399, NotoKernel.NotoId, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            var expected = site.GetGeometricStateRelativeTo(epoch, earth);
            worstPosition = System.Math.Max(worstPosition, (read.Position - expected.Position).Magnitude());
            worstVelocity = System.Math.Max(worstVelocity, (read.Velocity - expected.Velocity).Magnitude());
        }

        _output.WriteLine($"MEASURE #750 NOTO SPK against its site: position {worstPosition:E3} m, velocity {worstVelocity:E3} m/s");
        Assert.True(worstPosition <= SpkPositionTolerance, $"{worstPosition:E3} m");
        Assert.True(worstVelocity <= SpkVelocityTolerance, $"{worstVelocity:E3} m/s");
    }

    [Fact]
    public void ReadEphemeris_WindowFromNoto_IsTheReadOfEachEpoch()
    {
        // Act
        var window = new Window(NotoKernel.Epoch, NotoKernel.Epoch.AddSeconds(600.0));
        var states = SpiceAPI.Instance.ReadEphemeris(window, NotoKernel.NotoId, 301, Frames.Frame.ICRF, Aberration.LT, TimeSpan.FromSeconds(120.0))
            .Select(s => s.ToStateVector()).ToArray();

        // Assert
        Assert.Equal(6, states.Length);
        foreach (var state in states)
        {
            var single = SpiceAPI.Instance.ReadEphemeris(state.Epoch, NotoKernel.NotoId, 301, Frames.Frame.ICRF, Aberration.LT).ToStateVector();
            Assert.Equal(NotoKernel.NotoId, state.Observer.NaifId);
            Assert.Equal(single.Position, state.Position);
            Assert.Equal(single.Velocity, state.Velocity);
        }
    }

    /// <summary>NEW NORCIA without its SPK: SPICE's missing-data error, an <see cref="InvalidOperationException"/> that names
    /// it, where the read threw <see cref="ArgumentException"/> for the body 398 the caller never gave.</summary>
    [Fact]
    public void ReadEphemeris_FromNewNorciaWithoutItsSpk_IsSpicesMissingData()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SpiceAPI.Instance.ReadEphemeris(NotoKernel.Epoch, NewNorciaId, 399, Frames.Frame.ICRF, Aberration.None));
        Assert.Contains("398990", exception.Message);
        Assert.DoesNotContain("398 has no name", exception.Message);
        var window = new Window(NotoKernel.Epoch, NotoKernel.Epoch.AddSeconds(60.0));
        Assert.Throws<InvalidOperationException>(() =>
            SpiceAPI.Instance.ReadEphemeris(window, NewNorciaId, 399, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromSeconds(60.0)));
    }

    /// <summary>
    /// NOTO as observer against CSPICE's <c>spkezr_c</c> with "NOTO" as observer, the nine corrections, the Moon in J2000 and
    /// in ITRF93: the same read.
    /// </summary>
    [Fact]
    [Trait("Category", "Validation")]
    public void ReadEphemeris_FromNoto_IsSpkezrs()
    {
        double worstPosition = 0.0, worstVelocity = 0.0;
        foreach (var frame in new[] { Frames.Frame.ICRF, new Frames.Frame("ITRF93") })
        {
            foreach (var flag in Flags)
            {
                foreach (var epoch in Epochs)
                {
                    var read = SpiceAPI.Instance.ReadEphemeris(epoch, NotoKernel.NotoId, 301, frame, flag).ToStateVector();
                    var expected = CspiceDirect.Spkezr("MOON", epoch.TimeSpanFromJ2000().TotalSeconds, frame == Frames.Frame.ICRF ? "J2000" : "ITRF93",
                        flag.GetDescription(), "NOTO");
                    worstPosition = System.Math.Max(worstPosition, (read.Position - new Vector3(expected[0], expected[1], expected[2]) * 1000.0).Magnitude());
                    worstVelocity = System.Math.Max(worstVelocity, (read.Velocity - new Vector3(expected[3], expected[4], expected[5]) * 1000.0).Magnitude());
                }
            }
        }

        _output.WriteLine($"MEASURE #750 NOTO observer against spkezr_c: position {worstPosition:E3} m, velocity {worstVelocity:E3} m/s");
        Assert.Equal(0.0, worstPosition);
        Assert.Equal(0.0, worstVelocity);
    }

    /// <summary>
    /// A frame centred on NOTO, a TK frame of ITRF93 the test loads, on the reference-body path: the Moon from a site built
    /// from coordinates in it is the ICRF read rotated by ITRF93 at t ∓ lt_c, lt_c the light time of NOTO from the site
    /// (|p|/c and its rate p̂·ṗ/c, the identity of <c>spkltc</c>, from the read of NOTO from the site), the derivative of the
    /// rotation scaled by 1 ∓ dlt_c/dt, as <c>spkezr_c</c> evaluates it (#593). Before the fix the frame centre was built
    /// as a site of 398 and every corrected read threw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void FrameCentredOnNoto_RotatesAtTheEpochOfNoto()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var site = new Site(750, "G750", earth, new Planetodetic(-117.0 * IO.Astrodynamics.Constants.Deg2Rad, 34.0 * IO.Astrodynamics.Constants.Deg2Rad, 1000.0));
        var noto = SpiceAPI.CreateKernelObject(NotoKernel.NotoId);
        var itrf93 = new Frames.Frame("ITRF93");
        var directory = Directory.CreateTempSubdirectory("noto750tk-");
        var frameKernel = new FileInfo(Path.Combine(directory.FullName, "noto750.tf"));
        File.WriteAllText(frameKernel.FullName, "KPL/FK\n\\begindata\nFRAME_NOTO750_TK = 1750989\nFRAME_1750989_NAME = 'NOTO750_TK'\n" +
                                               "FRAME_1750989_CLASS = 4\nFRAME_1750989_CLASS_ID = 1750989\nFRAME_1750989_CENTER = 398989\n" +
                                               "TKFRAME_1750989_SPEC = 'MATRIX'\nTKFRAME_1750989_RELATIVE = 'ITRF93'\n" +
                                               "TKFRAME_1750989_MATRIX = (1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0)\n\\begintext\n");
        Assert.Empty(KernelShadowing.Find([frameKernel], KernelShadowing.SolarSystem));
        SpiceAPI.Instance.LoadKernels(frameKernel);
        try
        {
            var frame = new Frames.Frame("NOTO750_TK");
            Assert.Equal(NotoKernel.NotoId, SpiceAPI.NonInertialFrameCenter(frame));
            double worstPosition = 0.0, worstVelocity = 0.0;
            foreach (var flag in new[] { Aberration.LT, Aberration.CN, Aberration.XLT, Aberration.XCNS })
            {
                foreach (var t in Epochs)
                {
                    var read = moon.GetEphemeris(t, site, frame, flag).ToStateVector();
                    var icrf = moon.GetEphemeris(t, site, Frames.Frame.ICRF, flag).ToStateVector();
                    var lightTimeOnly = flag == Aberration.XCNS ? Aberration.XCN : flag;
                    var center = noto.GetEphemeris(t, site, Frames.Frame.ICRF, lightTimeOnly).ToStateVector();
                    double sign = flag is Aberration.LT or Aberration.CN ? -1.0 : 1.0;
                    var expected = LightTimeCorrectionTests.RotatedAtTheCentreEpoch(icrf, center, itrf93, sign);
                    worstPosition = System.Math.Max(worstPosition, (read.Position - expected.Position).Magnitude());
                    worstVelocity = System.Math.Max(worstVelocity, (read.Velocity - expected.Velocity).Magnitude());
                }
            }

            _output.WriteLine($"MEASURE #750 frame centred on NOTO: position {worstPosition:E3} m, velocity {worstVelocity:E3} m/s");
            Assert.True(worstPosition <= FramePositionTolerance, $"{worstPosition:E3} m");
            Assert.True(worstVelocity <= FrameVelocityTolerance, $"{worstVelocity:E3} m/s");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(frameKernel);
            directory.Delete(true);
        }
    }
}
