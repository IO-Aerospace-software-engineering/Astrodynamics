// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Occultation;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The light-time and stellar-aberration corrections of the reference-body path (#491), for the nine corrections, against
/// the CSPICE states of <c>tools/light_time_reference</c> (<c>spkezr_c</c>, <c>spkcpo_c</c>, <c>spkcpt_c</c> and
/// <c>azlcpo_c</c>): a site given by its coordinates and a spacecraft given by one state, as observers and as targets, a
/// site of the kernels as observer, the Moon, the Sun and the spacecraft as targets. A site given by its coordinates as the
/// target of an observer SPICE knows (#489), and the states in a non-inertial frame, whose orientation is that of the epoch
/// of its centre, as <c>spkezr_c</c> takes it (#593).
/// </summary>
public class LightTimeCorrectionTests
{
    private static readonly Aberration[] Flags =
    [
        Aberration.None, Aberration.LT, Aberration.LtS, Aberration.CN, Aberration.CnS, Aberration.XLT, Aberration.XltS,
        Aberration.XCN, Aberration.XcnS
    ];

    // Every bound is twice the worst value over Linux x64, Windows x64 and macOS arm64 (CI of #596 at cde5d3f5,
    // 2026-11-07), rounded up at two digits; the three platforms agree but on the Moon from the coordinate site, whose
    // geometric velocity (5.1e-13 m/s) and range (3.0e-7 m) are those of Windows and macOS. Positions in m, velocities in
    // m/s, angles in rad. The corrections (corrected minus geometric) of the
    // kernel and coordinate observers agree with CSPICE to 2.2e-5 m and 1.9e-9 m/s (the Sun with XCN), at the double of
    // a 2024 epoch (1.2e-7 s). Those of the spacecraft to 3.9e-4 m and 8.3e-7 m/s: its two-body state at t - lt is
    // OrbitalParameters.AtEpoch over TT seconds, converted on the 100 ns of a Time (7.6 km/s x 50 ns = 0.38 mm); the Sun
    // seen from it with "+S" to 2.9e-2 m and 6.4e-5 m/s, the stellar aberration (|p|/c = 500 s) of its 1.2e-4 m/s of
    // velocity difference. Its geometric state is 0.11 m and 1.2e-4 m/s from the SPK at 12 h: AtEpoch counts TT seconds
    // about the Earth (#508), prop2b TDB seconds.
    private static readonly (double Position, double Velocity) SpacecraftGeometric = (0.22, 2.5e-4);

    private readonly ITestOutputHelper _output;
    private readonly JsonElement _reference;
    private readonly TimeSystem.Time[] _epochs;
    private readonly CelestialBody _earth;
    private readonly CelestialBody _moon;
    private readonly CelestialBody _sun;
    private readonly Site _site;
    private readonly Site _dss13;
    private readonly Spacecraft _spacecraft;

    public LightTimeCorrectionTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
        _reference = JsonDocument.Parse(File.ReadAllText("Data/LightTimeReference/light_time_reference.json")).RootElement;
        _epochs = _reference.GetProperty("epochs_tdb").EnumerateArray().Select(e => TimeSystem.Time.CreateTdb(e.GetDouble())).ToArray();
        _earth = new CelestialBody(PlanetsAndMoons.EARTH);
        _moon = new CelestialBody(PlanetsAndMoons.MOON);
        _sun = new CelestialBody(Stars.Sun);
        var site = _reference.GetProperty("site");
        _site = new Site(491, "G491", _earth, new Planetodetic(site.GetProperty("longitude_deg").GetDouble() * global::IO.Astrodynamics.Constants.Deg2Rad,
            site.GetProperty("latitude_deg").GetDouble() * global::IO.Astrodynamics.Constants.Deg2Rad, site.GetProperty("altitude_m").GetDouble()));
        _dss13 = new Site(13, "DSS-13", _earth);
        var s0 = _reference.GetProperty("spacecraft_state_e0").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        _spacecraft = new Spacecraft(-1001, "S491", 100.0, new StateVector(new Vector3(s0[0], s0[1], s0[2]), new Vector3(s0[3], s0[4], s0[5]), _earth, _epochs[0], Frames.Frame.ICRF), 1000.0,
            new Clock("S491", 65536));
    }

    private IEnumerable<(Aberration Flag, int Epoch, double[] Expected)> Rows(string label)
    {
        var row = _reference.GetProperty("states").GetProperty(label);
        foreach (var flag in Flags)
        {
            var states = row.GetProperty(flag.GetDescription()).EnumerateArray().ToArray();
            for (int e = 0; e < _epochs.Length; e++)
            {
                yield return (flag, e, states[e].EnumerateArray().Select(x => x.GetDouble()).ToArray());
            }
        }
    }

    /// <summary>
    /// The worst deltas per correction of a row against CSPICE, position (m) and velocity (m/s): the geometric state for
    /// <see cref="Aberration.None"/>, the correction (the corrected state minus the geometric one) for the others, so that
    /// the difference of the two-body trajectories of the spacecraft (0.11 m in 12 h) does not mask it.
    /// </summary>
    private Dictionary<Aberration, (double Position, double Velocity)> Measure(string label, System.Func<TimeSystem.Time, Aberration, StateVector> read)
    {
        var worst = Flags.ToDictionary(f => f, _ => (Position: 0.0, Velocity: 0.0));
        var geometric = Rows(label).Where(r => r.Flag == Aberration.None).ToDictionary(r => r.Epoch, r => r.Expected);
        foreach (var (flag, e, expected) in Rows(label))
        {
            var state = read(_epochs[e], flag);
            var position = state.Position - new Vector3(expected[0], expected[1], expected[2]);
            var velocity = state.Velocity - new Vector3(expected[3], expected[4], expected[5]);
            if (flag != Aberration.None)
            {
                var none = read(_epochs[e], Aberration.None);
                var g = geometric[e];
                position -= none.Position - new Vector3(g[0], g[1], g[2]);
                velocity -= none.Velocity - new Vector3(g[3], g[4], g[5]);
            }

            double dp = position.Magnitude();
            double dv = velocity.Magnitude();
            var w = worst[flag];
            worst[flag] = (System.Math.Max(w.Position, dp), System.Math.Max(w.Velocity, dv));
        }

        foreach (var flag in Flags)
        {
            _output.WriteLine($"MEASURE {label} {flag.GetDescription(),-6} position {worst[flag].Position:E3} m, velocity {worst[flag].Velocity:E3} m/s");
        }

        return worst;
    }

    private void AssertRow(string label, System.Func<TimeSystem.Time, Aberration, StateVector> read, (double Position, double Velocity) geometric,
        (double Position, double Velocity) corrected)
    {
        foreach (var (flag, w) in Measure(label, read))
        {
            var bound = flag == Aberration.None ? geometric : corrected;
            Assert.True(w.Position <= bound.Position, $"{label} {flag}: position {w.Position:E3} m");
            Assert.True(w.Velocity <= bound.Velocity, $"{label} {flag}: velocity {w.Velocity:E3} m/s");
        }
    }

    private static StateVector Icrf(IO.Astrodynamics.OrbitalParameters.OrbitalParameters state) => state.ToStateVector();

    [Fact]
    public void MoonFromKernelSite() =>
        AssertRow("moon_from_dss13", (t, a) => Icrf(_moon.GetEphemeris(t, _dss13, Frames.Frame.ICRF, a)), (1.2e-7, 1.7e-13), (3.5e-5, 1.9e-9));

    [Fact]
    public void MoonFromCoordinateSite() =>
        AssertRow("moon_from_site", (t, a) => Icrf(_moon.GetEphemeris(t, _site, Frames.Frame.ICRF, a)), (1.4e-7, 1.1e-12), (2.8e-5, 1.9e-9));

    [Fact]
    public void SunFromCoordinateSite() =>
        AssertRow("sun_from_site", (t, a) => Icrf(_sun.GetEphemeris(t, _site, Frames.Frame.ICRF, a)), (1.6e-5, 7.6e-12), (4.4e-5, 3.8e-9));

    [Fact]
    public void SpacecraftFromCoordinateSite() =>
        AssertRow("spacecraft_from_site", (t, a) => Icrf(_spacecraft.GetEphemeris(t, _site, Frames.Frame.ICRF, a)), SpacecraftGeometric, (7.9e-4, 1.6e-6));

    [Fact]
    public void SpacecraftFromEarth() =>
        AssertRow("spacecraft_from_earth", (t, a) => Icrf(_spacecraft.GetEphemeris(t, _earth, Frames.Frame.ICRF, a)), SpacecraftGeometric, (7.5e-4, 1.7e-6));

    [Fact]
    public void MoonFromSpacecraft() =>
        AssertRow("moon_from_spacecraft", (t, a) => Icrf(_moon.GetEphemeris(t, _spacecraft, Frames.Frame.ICRF, a)), SpacecraftGeometric, (3.1e-4, 2.3e-7));

    [Fact]
    public void SunFromSpacecraft() =>
        AssertRow("sun_from_spacecraft", (t, a) => Icrf(_sun.GetEphemeris(t, _spacecraft, Frames.Frame.ICRF, a)), SpacecraftGeometric, (5.9e-2, 1.3e-4));

    [Fact]
    public void CoordinateSiteFromSpacecraft() =>
        AssertRow("site_from_spacecraft", (t, a) => Icrf(_site.GetEphemeris(t, _spacecraft, Frames.Frame.ICRF, a)), SpacecraftGeometric, (3.4e-5, 2.1e-8));

    private static StateVector InFrame(IO.Astrodynamics.OrbitalParameters.OrbitalParameters state, Frames.Frame frame)
    {
        Assert.Equal(frame, state.Frame);
        return state.ToStateVector();
    }

    private static readonly Frames.Frame Itrf93 = new("ITRF93");
    private static readonly Frames.Frame MoonMe = new("MOON_ME");

    private Site CoordinateSite(int id, double longitudeDeg, double latitudeDeg, double altitude) =>
        new(id, $"G489{id}", _earth, new Planetodetic(longitudeDeg * global::IO.Astrodynamics.Constants.Deg2Rad,
            latitudeDeg * global::IO.Astrodynamics.Constants.Deg2Rad, altitude));

    // #489: a site given by its coordinates as the target of an observer SPICE knows (spkcpt_c): before 11.0.0 the
    // correction was that of the observer seen from the site's body (82.7 km from the Moon with LT), and none from that body

    [Fact]
    public void CoordinateSiteFromMoon() =>
        AssertRow("site_from_moon", (t, a) => Icrf(_site.GetEphemeris(t, _moon, Frames.Frame.ICRF, a)), Site489Geometric, Site489Corrected);

    [Fact]
    public void CoordinateSiteFromItsBody() =>
        AssertRow("site_from_earth", (t, a) => Icrf(_site.GetEphemeris(t, _earth, Frames.Frame.ICRF, a)), Site489Geometric, Site489Corrected);

    [Fact]
    public void CoordinateSiteFromSun() =>
        AssertRow("site_from_sun", (t, a) => Icrf(_site.GetEphemeris(t, _sun, Frames.Frame.ICRF, a)), Site489SunGeometric, Site489SunCorrected);

    [Fact]
    public void CoordinateSiteFromMarsBarycenter() =>
        AssertRow("site_from_mars_barycenter", (t, a) => Icrf(_site.GetEphemeris(t, new Barycenter(4), Frames.Frame.ICRF, a)), Site489SunGeometric,
            Site489SunCorrected);

    [Fact]
    public void CoordinateSiteFromKernelSpacecraft()
    {
        TestKernels.EnsureLro();
        var lro = new Spacecraft(-85, "LRO", epoch: _epochs[0]);
        AssertRow("site_from_lro", (t, a) => Icrf(_site.GetEphemeris(t, lro, Frames.Frame.ICRF, a)), Site489Geometric, Site489Corrected);
    }

    [Theory]
    [InlineData("equator_site_from_moon", 0.0, 0.0)]
    [InlineData("north_pole_site_from_moon", 0.0, 90.0)]
    [InlineData("south_pole_site_from_moon", 0.0, -90.0)]
    public void SingularCoordinateSiteFromMoon(string label, double longitudeDeg, double latitudeDeg)
    {
        var site = CoordinateSite(489, longitudeDeg, latitudeDeg, 0.0);
        AssertRow(label, (t, a) => Icrf(site.GetEphemeris(t, _moon, Frames.Frame.ICRF, a)), Site489Geometric, Site489Corrected);
    }

    /// <summary>
    /// The window of the Site example of DEVELOPER_GUIDE.md (#483), the site within 400000 km of the Moon with LT, against
    /// the window of <c>spkcpt_c</c> (60 s samples and a bisection, <c>tools/light_time_reference</c>): before 11.0.0 about
    /// 340 s wider at each end (#489).
    /// </summary>
    [Fact]
    public void CoordinateSiteGuideWindow()
    {
        var expected = _reference.GetProperty("guide_window_lt").EnumerateArray().Select(e => TimeSystem.Time.CreateTdb(e.GetDouble())).ToArray();
        var windows = _moon.FindDistanceWindows(new TimeSystem.Window(new TimeSystem.Time(2024, 1, 1, TimeScale.TDB), new TimeSystem.Time(2024, 1, 2, TimeScale.TDB)), _site, RelationalOperator.LessThan, 400000000, new WindowSearchOptions { Step = System.TimeSpan.FromMinutes(10), Aberration = Aberration.LT }).ToArray();
        Assert.Single(windows);
        double start = (windows[0].StartDate - expected[0]).TotalSeconds;
        double end = (windows[0].EndDate - expected[1]).TotalSeconds;
        _output.WriteLine($"MEASURE guide window start {start:E3} s, end {end:E3} s");
        Assert.True(System.Math.Abs(start) <= GuideWindowBound, $"start {start:E3} s");
        Assert.True(System.Math.Abs(end) <= GuideWindowBound, $"end {end:E3} s");
    }

    // #593: a non-inertial output frame on the reference-body path, its orientation at t -+ the light time of its centre
    // (ITRF93 about the Earth, MOON_ME about the Moon, DSS-13_TOPO about DSS-13), as spkezr_c, spkcpo_c and spkcpt_c

    [Fact]
    public void MoonFromKernelSiteInItrf93() =>
        AssertRow("moon_from_dss13_itrf93", (t, a) => InFrame(_moon.GetEphemeris(t, _dss13, Itrf93, a), Itrf93), Frame593Geometric, Frame593Corrected);

    [Fact]
    public void MoonFromKernelSiteInMoonMe() =>
        AssertRow("moon_from_dss13_moon_me", (t, a) => InFrame(_moon.GetEphemeris(t, _dss13, MoonMe, a), MoonMe), Frame593Geometric, Frame593TargetCentredCorrected);

    [Fact]
    public void MoonFromKernelSiteInItsTopocentricFrame() =>
        AssertRow("moon_from_dss13_topo", (t, a) => InFrame(_moon.GetEphemeris(t, _dss13, _dss13.Frame, a), _dss13.Frame), Frame593Geometric, Frame593Corrected);

    [Fact]
    public void MoonFromCoordinateSiteInItrf93() =>
        AssertRow("moon_from_site_itrf93", (t, a) => InFrame(_moon.GetEphemeris(t, _site, Itrf93, a), Itrf93), Frame593Geometric, Frame593Corrected);

    [Fact]
    public void MoonFromCoordinateSiteInMoonMe() =>
        AssertRow("moon_from_site_moon_me", (t, a) => InFrame(_moon.GetEphemeris(t, _site, MoonMe, a), MoonMe), Frame593Geometric, Frame593TargetCentredCorrected);

    [Fact]
    public void MoonFromSpacecraftInItrf93() =>
        AssertRow("moon_from_spacecraft_itrf93", (t, a) => InFrame(_moon.GetEphemeris(t, _spacecraft, Itrf93, a), Itrf93), SpacecraftGeometric, Frame593SpacecraftCorrected);

    [Fact]
    public void MoonFromSpacecraftInMoonMe() =>
        AssertRow("moon_from_spacecraft_moon_me", (t, a) => InFrame(_moon.GetEphemeris(t, _spacecraft, MoonMe, a), MoonMe), SpacecraftGeometric, Frame593SpacecraftCorrected);

    [Fact]
    public void SpacecraftFromCoordinateSiteInItrf93() =>
        AssertRow("spacecraft_from_site_itrf93", (t, a) => InFrame(_spacecraft.GetEphemeris(t, _site, Itrf93, a), Itrf93), SpacecraftGeometric, Frame593SpacecraftCorrected);

    [Fact]
    public void SpacecraftFromCoordinateSiteInMoonMe() =>
        AssertRow("spacecraft_from_site_moon_me", (t, a) => InFrame(_spacecraft.GetEphemeris(t, _site, MoonMe, a), MoonMe), SpacecraftGeometric, Frame593SpacecraftCorrected);

    [Fact]
    public void CoordinateSiteFromSpacecraftInItrf93() =>
        AssertRow("site_from_spacecraft_itrf93", (t, a) => InFrame(_site.GetEphemeris(t, _spacecraft, Itrf93, a), Itrf93), SpacecraftGeometric, Frame593SpacecraftCorrected);

    [Fact]
    public void CoordinateSiteFromMoonInItrf93() =>
        AssertRow("site_from_moon_itrf93", (t, a) => InFrame(_site.GetEphemeris(t, _moon, Itrf93, a), Itrf93), Frame593Geometric, Frame593Corrected);

    [Fact]
    public void CoordinateSiteFromMoonInMoonMe() =>
        AssertRow("site_from_moon_moon_me", (t, a) => InFrame(_site.GetEphemeris(t, _moon, MoonMe, a), MoonMe), Frame593Geometric, Frame593Corrected);

    [Fact]
    public void SunFromSpacecraftInItrf93() =>
        AssertRow("sun_from_spacecraft_itrf93", (t, a) => InFrame(_sun.GetEphemeris(t, _spacecraft, Itrf93, a), Itrf93), SpacecraftGeometric, Frame593SunCorrected);

    [Fact]
    public void SpacecraftFromEarthInItrf93() =>
        AssertRow("spacecraft_from_earth_itrf93", (t, a) => InFrame(_spacecraft.GetEphemeris(t, _earth, Itrf93, a), Itrf93), SpacecraftGeometric, Frame593SpacecraftCorrected);

    /// <summary>
    /// #593: the centre at whose light time a non-inertial output frame is evaluated, SPICE's (<c>frinfo_c</c>) for its
    /// frames: none for an inertial frame (J2000, GCRF handed as J2000, ECLIPJ2000) or one SPICE does not know, the Earth
    /// for ITRF93 and IAU_EARTH (built-in PCK frames, Frames Required Reading), the Moon for MOON_ME (moon_080317.tf),
    /// DSS-13 for its topocentric frame (earth_topo_201023.tf); the site, the spacecraft with an attitude record and the
    /// Earth for the library's own frames, and none for the frame of a spacecraft without one, which SPICE does not know.
    /// </summary>
    [Fact]
    public void NonInertialFrameCenter()
    {
        Assert.Null(SpiceApi.NonInertialFrameCenter(Frames.Frame.ICRF));
        Assert.Null(SpiceApi.NonInertialFrameCenter(Frames.Frame.GCRF));
        Assert.Null(SpiceApi.NonInertialFrameCenter(Frames.Frame.ECLIPJ2000));
        Assert.Null(SpiceApi.NonInertialFrameCenter(new Frames.Frame("NO_SUCH_FRAME_593")));
        Assert.Equal(399, SpiceApi.NonInertialFrameCenter(Itrf93));
        Assert.Equal(399, SpiceApi.NonInertialFrameCenter(new Frames.Frame("iau_earth")));
        Assert.Equal(301, SpiceApi.NonInertialFrameCenter(MoonMe));
        Assert.Equal(399013, SpiceApi.NonInertialFrameCenter(_dss13.Frame));
        Assert.Equal(_site.NaifId, SpiceApi.NonInertialFrameCenter(_site.Frame));
        Assert.Equal(399, SpiceApi.NonInertialFrameCenter(Frames.Frame.TEME));
        Assert.Equal(399, SpiceApi.NonInertialFrameCenter(Frames.Frame.TIRS));
        var oriented = new Spacecraft(-1593, "S593", 100.0, _spacecraft.InitialOrbitalParameters, 1000.0, new Clock("S593", 65536));
        Assert.Null(SpiceApi.NonInertialFrameCenter(oriented.Frame));
        oriented.Frame.AddStateOrientationToIcrf(new StateOrientation(Quaternion.Identity, Vector3.Zero, _epochs[0], oriented.Frame));
        Assert.Equal(-1593, SpiceApi.NonInertialFrameCenter(oriented.Frame));
        Assert.Throws<System.ArgumentNullException>(() => SpiceApi.NonInertialFrameCenter(null));
    }

    // Review of #593: a non-inertial frame centred on a third object, neither the observer nor the target (the frame of a
    // site built from coordinates, of a managed spacecraft with an attitude record, or one given by name), against
    // spkezr_c, spkcpo_c and spkcpt_c in the same frame with the locus CENTER: the frame of a site is the TK frame the
    // library writes for it, centred on an SPK object of the site, and S_TK the frame of S oriented as ITRF93, which S
    // holds as an attitude record of ITRF93 sampled every 10 s (tools/light_time_reference). Before, every corrected read
    // threw ArgumentException: the site's identifier reached the kernel-site lookup, the spacecraft's CelestialItem.Create


    private Site OtherSite()
    {
        var other = _reference.GetProperty("other_site");
        return new Site(492, "H491", _earth, new Planetodetic(other.GetProperty("longitude_deg").GetDouble() * global::IO.Astrodynamics.Constants.Deg2Rad,
            other.GetProperty("latitude_deg").GetDouble() * global::IO.Astrodynamics.Constants.Deg2Rad, other.GetProperty("altitude_m").GetDouble()));
    }

    /// <summary>The frame of S oriented as ITRF93: its attitude record, ITRF93 to ICRF sampled every 10 s over ±120 s about
    /// each epoch of the reference, the samples of one law, interpolated by the exact SLERP of the record.</summary>
    private Frames.Frame SpacecraftFrameAsItrf93()
    {
        var frame = (SpacecraftFrame)_spacecraft.Frame;
        var law = new object();
        foreach (var epoch in _epochs)
        {
            for (int k = -12; k <= 12; k++)
            {
                var t = epoch.AddSeconds(10.0 * k);
                var orientation = Itrf93.ToFrame(Frames.Frame.ICRF, t);
                frame.Record(new StateOrientation(orientation.Rotation, orientation.AngularVelocity, t, Frames.Frame.ICRF), law, false);
            }
        }

        return frame;
    }

    [Fact]
    public void MoonFromSpacecraftInTheFrameOfACoordinateSite() =>
        AssertRow("moon_from_spacecraft_in_site_frame", (t, a) => InFrame(_moon.GetEphemeris(t, _spacecraft, _site.Frame, a), _site.Frame),
            SpacecraftGeometric, Frame593SpacecraftCorrected);

    [Fact]
    public void MoonFromKernelSiteInTheFrameOfACoordinateSite() =>
        AssertRow("moon_from_dss13_in_site_frame", (t, a) => InFrame(_moon.GetEphemeris(t, _dss13, _site.Frame, a), _site.Frame), Frame593Geometric,
            Frame593Corrected);

    [Fact]
    public void MoonFromCoordinateSiteInTheFrameOfAnotherSite()
    {
        var other = OtherSite();
        AssertRow("moon_from_site_in_other_site_frame", (t, a) => InFrame(_moon.GetEphemeris(t, _site, other.Frame, a), other.Frame), Frame593Geometric,
            Frame593Corrected);
    }

    [Fact]
    public void SpacecraftFromCoordinateSiteInTheFrameOfAnotherSite()
    {
        var other = OtherSite();
        AssertRow("spacecraft_from_site_in_other_site_frame", (t, a) => InFrame(_spacecraft.GetEphemeris(t, _site, other.Frame, a), other.Frame),
            SpacecraftGeometric, Frame593SpacecraftCorrected);
    }

    [Fact]
    public void CoordinateSiteFromMoonInTheFrameOfAnotherSite()
    {
        var other = OtherSite();
        AssertRow("site_from_moon_in_other_site_frame", (t, a) => InFrame(_site.GetEphemeris(t, _moon, other.Frame, a), other.Frame), Frame593Geometric,
            Frame593Corrected);
    }

    [Fact]
    public void MoonFromCoordinateSiteInTheFrameOfASpacecraft()
    {
        var frame = SpacecraftFrameAsItrf93();
        AssertRow("moon_from_site_in_spacecraft_frame", (t, a) => InFrame(_moon.GetEphemeris(t, _site, frame, a), frame), Frame593ThirdSpacecraftGeometric,
            Frame593ThirdSpacecraftCorrected);
    }

    [Fact]
    public void MoonFromKernelSiteInTheFrameOfASpacecraft()
    {
        var frame = SpacecraftFrameAsItrf93();
        AssertRow("moon_from_dss13_in_spacecraft_frame", (t, a) => InFrame(_moon.GetEphemeris(t, _dss13, frame, a), frame), Frame593ThirdSpacecraftGeometric,
            Frame593ThirdSpacecraftCorrected);
    }

    /// <summary>
    /// Review of #593: a frame given by name whose centre no object of the library holds, a TK frame of ITRF93 centred on
    /// LRO (-85, the SPK of Data/LRO) that the test loads, read through SPICE by its identifier: the Moon from the
    /// coordinate site G in it is the ICRF read rotated by ITRF93 at t − lt_c, lt_c the light time of LRO from G
    /// (|p|/c and its rate p̂·ṗ/c, the identity of <c>spkltc</c>, from the read of LRO from G, whose path the rows
    /// measure against CSPICE), the derivative of the rotation scaled by 1 − dlt_c/dt, as <c>spkezr_c</c> evaluates it.
    /// </summary>
    [Fact]
    public void MoonFromCoordinateSiteInAFrameByNameCentredOnASpacecraft()
    {
        TestKernels.EnsureLro();
        var lro = new Spacecraft(-85, "LRO", epoch: _epochs[0]);
        var directory = Directory.CreateTempSubdirectory("lt593-");
        var frameKernel = new FileInfo(Path.Combine(directory.FullName, "lt593.tf"));
        File.WriteAllText(frameKernel.FullName, "KPL/FK\n\\begindata\nFRAME_LRO593_TK = 1593085\nFRAME_1593085_NAME = 'LRO593_TK'\n" +
                                               "FRAME_1593085_CLASS = 4\nFRAME_1593085_CLASS_ID = 1593085\nFRAME_1593085_CENTER = -85\n" +
                                               "TKFRAME_1593085_SPEC = 'MATRIX'\nTKFRAME_1593085_RELATIVE = 'ITRF93'\n" +
                                               "TKFRAME_1593085_MATRIX = (1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0)\n\\begintext\n");
        Assert.Empty(KernelShadowing.Find([frameKernel], KernelShadowing.SolarSystem));
        SpiceApi.Instance.LoadKernels(frameKernel);
        try
        {
            var frame = new Frames.Frame("LRO593_TK");
            Assert.Equal(-85, SpiceApi.NonInertialFrameCenter(frame));
            double worstPosition = 0.0, worstVelocity = 0.0;
            foreach (var flag in new[] { Aberration.LT, Aberration.CN, Aberration.XLT, Aberration.XcnS })
            {
                foreach (var t in _epochs)
                {
                    var read = InFrame(_moon.GetEphemeris(t, _site, frame, flag), frame);
                    var icrf = _moon.GetEphemeris(t, _site, Frames.Frame.ICRF, flag);
                    var lightTimeOnly = flag == Aberration.XcnS ? Aberration.XCN : flag;
                    var center = lro.GetEphemeris(t, _site, Frames.Frame.ICRF, lightTimeOnly);
                    double sign = flag is Aberration.LT or Aberration.CN ? -1.0 : 1.0;
                    var expected = RotatedAtTheCentreEpoch(icrf, center, Itrf93, sign);
                    worstPosition = System.Math.Max(worstPosition, (read.Position - expected.Position).Magnitude());
                    worstVelocity = System.Math.Max(worstVelocity, (read.Velocity - expected.Velocity).Magnitude());
                }
            }

            _output.WriteLine($"MEASURE #593 review frame by name centred on LRO: position {worstPosition:E3} m, velocity {worstVelocity:E3} m/s");
            Assert.True(worstPosition <= FrameByNamePosition, $"{worstPosition:E3} m");
            Assert.True(worstVelocity <= FrameByNameVelocity, $"{worstVelocity:E3} m/s");
        }
        finally
        {
            SpiceApi.Instance.UnloadKernels(frameKernel);
            directory.Delete(true);
        }
    }

    // #739: the frames that only the library holds between two objects of the kernels, which CSPICE reads in J2000 and the
    // library rotates (SpiceApi.ToManagedFrame): the frame of a site built from coordinates and the frame of S as a record
    // of ITRF93, against spkezr_c in the same frames (tools/light_time_reference); TEME, CIRS and TIRS against the ICRF read
    // rotated at t ∓ lt of their centre, the Earth (RotatedAtTheCentreEpoch), the closed form that ITRF93 checks against
    // spkezr_c. Before the fix every correction was rotated at t (measured on f70ee32: the Earth from the Moon in TIRS
    // with LT 3.619e4 m from the rotation at t − lt).

    [Fact]
    public void MoonFromEarthInTheFrameOfACoordinateSite() =>
        AssertRow("moon_from_earth_in_site_frame", (t, a) => InFrame(_moon.GetEphemeris(t, _earth, _site.Frame, a), _site.Frame), Frame739Geometric,
            Frame739Corrected);

    [Fact]
    public void EarthFromMoonInTheFrameOfACoordinateSite() =>
        AssertRow("earth_from_moon_in_site_frame", (t, a) => InFrame(_earth.GetEphemeris(t, _moon, _site.Frame, a), _site.Frame), Frame739Geometric,
            Frame739Corrected);

    [Fact]
    public void MoonFromEarthInTheFrameOfASpacecraft()
    {
        var frame = SpacecraftFrameAsItrf93();
        AssertRow("moon_from_earth_in_spacecraft_frame", (t, a) => InFrame(_moon.GetEphemeris(t, _earth, frame, a), frame), Frame739SpacecraftGeometric,
            Frame739SpacecraftCorrected);
    }

    [Fact]
    public void EarthFromMoonInTheFrameOfASpacecraft()
    {
        var frame = SpacecraftFrameAsItrf93();
        AssertRow("earth_from_moon_in_spacecraft_frame", (t, a) => InFrame(_earth.GetEphemeris(t, _moon, frame, a), frame), Frame739SpacecraftGeometric,
            Frame739SpacecraftCorrected);
    }

    /// <summary>The light-time part of a correction, its sign (− reception, + transmission).</summary>
    private static (Aberration LightTimeOnly, double Sign) LightTimeOf(Aberration flag) => flag switch
    {
        Aberration.LtS => (Aberration.LT, -1.0),
        Aberration.CnS => (Aberration.CN, -1.0),
        Aberration.XltS => (Aberration.XLT, 1.0),
        Aberration.XcnS => (Aberration.XCN, 1.0),
        Aberration.LT or Aberration.CN => (flag, -1.0),
        _ => (flag, 1.0)
    };

    /// <summary>
    /// The worst distance of <paramref name="read"/> from the closed form of the frame-centre rule: the ICRF read of the
    /// Earth from the Moon with the correction, rotated to <paramref name="frame"/> at t ∓ lt of the Earth, lt and its rate
    /// those of the Earth's light-time corrected read from the Moon (RotatedAtTheCentreEpoch), over the epochs and the eight
    /// corrections.
    /// </summary>
    private (double Position, double Velocity) EarthFromMoonAgainstTheCentreRule(Frames.Frame frame, System.Func<TimeSystem.Time, Aberration, StateVector> read)
    {
        double worstPosition = 0.0, worstVelocity = 0.0;
        foreach (var flag in Flags.Where(f => f != Aberration.None))
        {
            var (lightTimeOnly, sign) = LightTimeOf(flag);
            foreach (var t in _epochs)
            {
                var icrf = _earth.GetEphemeris(t, _moon, Frames.Frame.ICRF, flag);
                var center = _earth.GetEphemeris(t, _moon, Frames.Frame.ICRF, lightTimeOnly);
                var expected = RotatedAtTheCentreEpoch(icrf, center, frame, sign);
                var state = read(t, flag);
                Assert.Equal(frame, state.Frame);
                worstPosition = System.Math.Max(worstPosition, (state.Position - expected.Position).Magnitude());
                worstVelocity = System.Math.Max(worstVelocity, (state.Velocity - expected.Velocity).Magnitude());
            }
        }

        return (worstPosition, worstVelocity);
    }

    /// <summary>
    /// #739, the check of the closed form: in ITRF93, which <c>spkezr_c</c> reads itself, the Earth from the Moon is the
    /// ICRF read rotated at t − lt of the Earth with the rate of its light time, for the eight corrections.
    /// </summary>
    [Fact]
    public void EarthFromMoonInItrf93_IsTheClosedFormOfTheCentreRule()
    {
        var worst = EarthFromMoonAgainstTheCentreRule(Itrf93, (t, a) => _earth.GetEphemeris(t, _moon, Itrf93, a));
        _output.WriteLine($"MEASURE #739 the Earth from the Moon in ITRF93 (spkezr_c) against the closed form: position {worst.Position:E3} m, velocity {worst.Velocity:E3} m/s");
        Assert.True(worst.Position <= Frame739ClosedForm.Position, $"{worst.Position:E3} m");
        Assert.True(worst.Velocity <= Frame739ClosedForm.Velocity, $"{worst.Velocity:E3} m/s");
    }

    public static TheoryData<string> LibraryFrames => ["TIRS", "CIRS", "TEME", "tirs"];

    private static Frames.Frame LibraryFrame(string name) => name switch
    {
        "TIRS" => Frames.Frame.TIRS,
        "CIRS" => Frames.Frame.CIRS,
        "TEME" => Frames.Frame.TEME,
        _ => new Frames.Frame(name)
    };

    /// <summary>
    /// #739: the Earth from the Moon in TIRS, CIRS and TEME, and in a frame built by the name "tirs" (#656), centred on the
    /// target, is the ICRF read rotated at t ∓ lt of the Earth, on every read: <see cref="CelestialItem.GetEphemeris(in TimeSystem.Time, ILocalizable, Frames.Frame, Aberration)"/>,
    /// <see cref="SpiceApi.ReadEphemeris(TimeSystem.Time, int, int, Frames.Frame, Aberration)"/> by identifiers and its window read.
    /// </summary>
    [Theory]
    [MemberData(nameof(LibraryFrames))]
    public void EarthFromMoonInAFrameOfTheLibrary_IsRotatedAtTheEpochOfTheEarth(string name)
    {
        var frame = LibraryFrame(name);
        var byItem = EarthFromMoonAgainstTheCentreRule(frame, (t, a) => _earth.GetEphemeris(t, _moon, frame, a));
        var byIdentifiers = EarthFromMoonAgainstTheCentreRule(frame, (t, a) => SpiceApi.Instance.ReadEphemeris(t, 301, 399, frame, a));
        var byWindow = EarthFromMoonAgainstTheCentreRule(frame, (t, a) =>
            SpiceApi.Instance.ReadEphemeris(new Window(t, t.AddSeconds(60.0)), 301, 399, frame, a, System.TimeSpan.FromSeconds(60.0)).First().ToStateVector());
        _output.WriteLine($"MEASURE #739 the Earth from the Moon in {name} against the closed form: item {byItem.Position:E3} m {byItem.Velocity:E3} m/s, " +
                          $"identifiers {byIdentifiers.Position:E3} m {byIdentifiers.Velocity:E3} m/s, window {byWindow.Position:E3} m {byWindow.Velocity:E3} m/s");
        foreach (var worst in new[] { byItem, byIdentifiers, byWindow })
        {
            Assert.True(worst.Position <= Frame739ClosedForm.Position, $"{name}: {worst.Position:E3} m");
            Assert.True(worst.Velocity <= Frame739ClosedForm.Velocity, $"{name}: {worst.Velocity:E3} m/s");
        }
    }

    /// <summary>
    /// #739: a frame centred on the observer, the Moon from the Earth in TIRS, and every geometric read stay the ICRF read
    /// rotated at t, bit for bit as before.
    /// </summary>
    [Fact]
    public void MoonFromEarthInTirs_AndGeometricReads_AreRotatedAtTheEpoch()
    {
        foreach (var t in _epochs)
        {
            foreach (var flag in Flags)
            {
                var read = _moon.GetEphemeris(t, _earth, Frames.Frame.TIRS, flag);
                var expected = _moon.GetEphemeris(t, _earth, Frames.Frame.ICRF, flag).ToFrame(Frames.Frame.TIRS).ToStateVector();
                Assert.Equal(expected.Position, read.Position);
                Assert.Equal(expected.Velocity, read.Velocity);
            }

            var geometric = _earth.GetEphemeris(t, _moon, Frames.Frame.TIRS, Aberration.None);
            var rotated = _earth.GetEphemeris(t, _moon, Frames.Frame.ICRF, Aberration.None).ToFrame(Frames.Frame.TIRS).ToStateVector();
            Assert.Equal(rotated.Position, geometric.Position);
            Assert.Equal(rotated.Velocity, geometric.Velocity);
        }
    }

    /// <summary>
    /// <paramref name="state"/>, light-time corrected in ICRF, in <paramref name="frame"/> at the epoch of its centre as
    /// <c>spkezr_c</c> takes it: lt_c = |p|/c and dlt_c = p̂·ṗ/c of the light-time corrected <paramref name="center"/>,
    /// the frame at t + sign·lt_c, carried to the exact double epoch by its angular velocity, and the derivative of the
    /// rotation scaled by 1 + sign·dlt_c.
    /// </summary>
    internal static StateVector RotatedAtTheCentreEpoch(StateVector state, StateVector center, Frames.Frame frame, double sign)
    {
        const double c = global::IO.Astrodynamics.Constants.SpeedOfLight;
        double lightTime = center.Position.Magnitude() / c;
        double rate = center.Position.Normalize().Dot(center.Velocity) / c;
        var t = state.Epoch;
        var centerEpoch = t.AddSeconds(sign * lightTime);
        double residual = (t.ToTdb().TimeSpanFromJ2000().TotalSeconds + sign * lightTime) - centerEpoch.ToTdb().TimeSpanFromJ2000().TotalSeconds;
        var orientation = Frames.Frame.ICRF.ToFrame(frame, centerEpoch);
        var angularVelocity = orientation.AngularVelocity.Rotate(orientation.Rotation);
        var position = state.Position.Rotate(orientation.Rotation);
        position -= angularVelocity.Cross(position) * residual;
        var velocity = state.Velocity.Rotate(orientation.Rotation) - angularVelocity.Cross(position) * (1.0 + sign * rate);
        return new StateVector(position, velocity, state.Observer, t, frame);
    }

    // Review of #489, item 2 of its tests: C, the copy of the kernel site DSS-13 built from its coordinates (its position
    // in ITRF93 at J2000 TDB, as the library takes them), against spkcpt_c at those coordinates, not against DSS-13, which
    // the kernel moves 0.47 m by 2024

    private Site Dss13Copy()
    {
        var copy = _reference.GetProperty("dss13_copy");
        var site = new Site(913, "DSS13COPY", _earth, new Planetodetic(copy.GetProperty("longitude_rad").GetDouble(), copy.GetProperty("latitude_rad").GetDouble(),
            copy.GetProperty("altitude_m").GetDouble()));
        var planetodetic = _dss13.Planetodetic;
        _output.WriteLine($"MEASURE DSS-13 coordinates of the library minus recgeo_c: longitude {planetodetic.Longitude - site.Planetodetic.Longitude:E3} rad, " +
                          $"latitude {planetodetic.Latitude - site.Planetodetic.Latitude:E3} rad, altitude {planetodetic.Altitude - site.Planetodetic.Altitude:E3} m");
        return site;
    }

    [Fact]
    public void KernelSiteCopyFromMoon()
    {
        var copy = Dss13Copy();
        AssertRow("dss13_copy_from_moon", (t, a) => Icrf(copy.GetEphemeris(t, _moon, Frames.Frame.ICRF, a)), Site489Geometric, Site489Corrected);
    }

    [Fact]
    public void KernelSiteCopyFromItsBody()
    {
        var copy = Dss13Copy();
        AssertRow("dss13_copy_from_earth", (t, a) => Icrf(copy.GetEphemeris(t, _earth, Frames.Frame.ICRF, a)), Site489Geometric, Site489Corrected);
    }

    /// <summary>
    /// Review of #489, item 4 of its tests: the coordinate windows of G seen from the Moon with LT, against the windows of
    /// <c>spkcpt_c</c> (60 s samples and a bisection to adjacent doubles, <c>tools/light_time_reference</c>): its range in
    /// J2000 above 405000 km, its declination in J2000 above −9.5°, and its declination in the frame of H, a site built from
    /// coordinates (#593's review: a frame centred on a third object, which threw before), above 0°.
    /// </summary>
    [Theory]
    [InlineData("range_above", Coordinate.Range, false)]
    [InlineData("declination_above", Coordinate.Declination, false)]
    [InlineData("declination_in_other_site_frame_above", Coordinate.Declination, true)]
    public void CoordinateSiteCoordinateWindows(string label, Coordinate coordinate, bool inOtherSiteFrame)
    {
        var row = _reference.GetProperty("coordinate_windows_lt").GetProperty(label);
        double value = row.GetProperty("value").GetDouble();
        value = coordinate == Coordinate.Range ? value * 1000.0 : value * global::IO.Astrodynamics.Constants.Deg2Rad;
        var expected = row.GetProperty("windows").EnumerateArray().Select(e => TimeSystem.Time.CreateTdb(e.GetDouble())).ToArray();
        var frame = inOtherSiteFrame ? OtherSite().Frame : Frames.Frame.ICRF;
        var windows = _moon.FindCoordinateWindows(new TimeSystem.Window(TimeSystem.Time.CreateTdb(757339200.0), TimeSystem.Time.CreateTdb(757425600.0)), _site, frame, CoordinateSystem.RaDec, coordinate, RelationalOperator.GreaterThan, value, new WindowSearchOptions { Step = System.TimeSpan.FromMinutes(10), Aberration = Aberration.LT }).ToArray();
        Assert.Equal(expected.Length / 2, windows.Length);
        double worst = 0.0;
        for (int k = 0; k < windows.Length; k++)
        {
            worst = System.Math.Max(worst, System.Math.Abs((windows[k].StartDate - expected[2 * k]).TotalSeconds));
            worst = System.Math.Max(worst, System.Math.Abs((windows[k].EndDate - expected[2 * k + 1]).TotalSeconds));
        }

        _output.WriteLine($"MEASURE #489 coordinate window {label}: worst bound {worst:E3} s");
        Assert.True(worst <= CoordinateWindowBound, $"{label}: {worst:E3} s");
    }

    // Bounds of #489 and #593, twice the worst over the rows of each on Linux x64, Windows x64 and macOS arm64 (the MEASURE
    // lines of each row, CI run 44225490669), rounded up at two digits. Positions in m, velocities in m/s. A site given by
    // its coordinates, as target: from the Moon, the Earth, LRO and the singular sites, geometric 6.829e-8 m on the three
    // platforms and 4.583e-13 m/s on Linux x64, 5.124e-13 m/s on Windows x64 and macOS arm64 (site_from_moon), corrected
    // 3.036e-5 m (dss13_copy_from_moon, XCN) and 3.732e-9 m/s on the three; from the Sun and the Mars barycenter, geometric
    // 6.115e-5 m (the double of 2.3e11 m) and 3.777e-12 m/s, corrected 6.835e-5 m and 3.423e-9 m/s on the three.
    private static readonly (double Position, double Velocity) Site489Geometric = (1.4e-7, 1.1e-12);
    private static readonly (double Position, double Velocity) Site489Corrected = (6.1e-5, 7.5e-9);
    private static readonly (double Position, double Velocity) Site489SunGeometric = (1.3e-4, 7.6e-12);
    private static readonly (double Position, double Velocity) Site489SunCorrected = (1.4e-4, 6.9e-9);

    // A non-inertial frame (#593), the frame of a site centred on a third object included (its rows below): geometric
    // 4.510e-7 m and 7.122e-11 m/s on Linux x64, 3.083e-7 m (moon_from_dss13_in_site_frame) and 5.719e-11 m/s
    // (site_from_moon_in_other_site_frame) on Windows x64 and macOS arm64; corrected 1.733e-5 m (moon_from_dss13_topo, LT+S) and 1.151e-8 m/s
    // (moon_from_dss13_in_site_frame) on the three, in ITRF93, the topocentric frame and MOON_ME centred on the observer;
    // MOON_ME centred on the target, 2.418e-4 m and 1.180e-9 m/s on the three, the resolution of the reference, whose
    // MOON_ME rotation changes by steps of 3.7e-4 m at 384000 km within a few ulps of its epoch (sxform_c from MOON_PA's
    // Chebyshev angles, measured with CSPICE N0067 on these kernels); the spacecraft rows 3.907e-4 m and 8.474e-7 m/s, the
    // Sun from it 2.916e-2 m and 6.437e-5 m/s, as in ICRF (above), on the three.
    private static readonly (double Position, double Velocity) Frame593Geometric = (9.1e-7, 1.5e-10);
    private static readonly (double Position, double Velocity) Frame593Corrected = (3.5e-5, 2.4e-8);
    private static readonly (double Position, double Velocity) Frame593TargetCentredCorrected = (4.9e-4, 2.4e-9);
    private static readonly (double Position, double Velocity) Frame593SpacecraftCorrected = (7.9e-4, 1.7e-6);
    private static readonly (double Position, double Velocity) Frame593SunCorrected = (5.9e-2, 1.3e-4);

    // The guide window: 0 s and 1.0e-7 s from spkcpt_c's on Linux x64, Windows x64 and macOS arm64 (CI run 44225490669),
    // twice the tick
    private const double GuideWindowBound = 2.0e-7;

    // Reviews of #593 and #489, twice the worst on Linux x64, Windows x64 and macOS arm64 alike (the MEASURE lines, CI run
    // 44225490669), rounded up at two digits. The frame of a site centred on a third object takes the bounds above. The
    // frame of S as a record of ITRF93: geometric 2.752e-7 m and 1.774e-8 m/s, the rate of the
    // record (the mean of the SLERP rates of the intervals on either side of a sample); corrected 1.666e-3 m and 1.195e-7 m/s,
    // the epoch of the centre, read on the record at its 100 ns tick (exact SLERP) where CSPICE and the residual of the
    // library take the double of a 2024 epoch, 6e-8 s apart at most: 1.7 mm at the Moon for the Earth's rotation. The frame
    // by name: the closed form of the same arithmetic, 0 on the three. The coordinate windows: 1.0e-7 s from spkcpt_c's on
    // the three, as the guide window.
    private static readonly (double Position, double Velocity) Frame593ThirdSpacecraftGeometric = (5.6e-7, 3.6e-8);
    private static readonly (double Position, double Velocity) Frame593ThirdSpacecraftCorrected = (3.4e-3, 2.4e-7);
    // #739, twice the worst over Linux x64, Windows x64 and macOS arm64 (the MEASURE lines, CI run 44395076184,
    // 2026-11-18), rounded up at two digits. The frame of the site G between the Earth and the Moon: geometric 3.292e-7 m
    // on Windows x64 and macOS arm64 (3.210e-7 m on Linux x64) and 6.395e-11 m/s on Linux x64 (4.747e-11 m/s on the two
    // others), corrected 3.116e-7 m on Windows x64 and macOS arm64 (2.731e-7 m on Linux x64; the Earth from the Moon,
    // XCN+S) and 7.648e-9 m/s on Linux x64 (7.646e-9 m/s on the two others), as the other frames of a site above. The
    // frame of S as a record of ITRF93, the same on the three: geometric 2.347e-7 m and 1.753e-8 m/s, the rate of the
    // record; corrected 1.600e-3 m and 1.208e-7 m/s (the Earth from the Moon, XLT), the epoch of the centre read on the
    // record at its 100 ns tick, as the rows of the review of #593 above. The closed form of the centre rule against
    // spkezr_c in ITRF93: 2.403e-7 m and 7.670e-9 m/s on the three, its resolution; TEME, CIRS and TIRS are 0 from it on
    // the three and take that bound
    private static readonly (double Position, double Velocity) Frame739Geometric = (6.6e-7, 1.3e-10);
    private static readonly (double Position, double Velocity) Frame739Corrected = (6.3e-7, 1.6e-8);
    private static readonly (double Position, double Velocity) Frame739SpacecraftGeometric = (4.7e-7, 3.6e-8);
    private static readonly (double Position, double Velocity) Frame739SpacecraftCorrected = (3.2e-3, 2.5e-7);
    private static readonly (double Position, double Velocity) Frame739ClosedForm = (4.9e-7, 1.6e-8);
    private const double FrameByNamePosition = 0.0;
    private const double FrameByNameVelocity = 0.0;
    private const double CoordinateWindowBound = 2.0e-7;

    /// <summary>
    /// Azimuth, elevation, range (<see cref="Site.GetHorizontalCoordinates"/>) and range rate (from the state of the target
    /// seen from the site) against <c>azlcpo_c</c>: the geometric values for <see cref="Aberration.None"/>, the corrections
    /// (corrected minus geometric) for the others, as <see cref="Measure"/>.
    /// </summary>
    private void AssertHorizontal(string label, ILocalizable target, (double Angle, double Range, double RangeRate) geometric,
        (double Angle, double Range, double RangeRate) corrected)
    {
        var row = _reference.GetProperty("horizontal").GetProperty(label);
        double[] Values(Aberration flag, int e)
        {
            var horizontal = _site.GetHorizontalCoordinates(_epochs[e], target, flag);
            var state = target.GetEphemeris(_epochs[e], _site, Frames.Frame.ICRF, flag);
            return [horizontal.Range, horizontal.Azimuth, horizontal.Elevation, state.Position.Normalize().Dot(state.Velocity)];
        }

        double[] Expected(Aberration flag, int e) =>
            row.GetProperty(flag.GetDescription()).EnumerateArray().ElementAt(e).EnumerateArray().Select(v => v.GetDouble()).ToArray();

        foreach (var flag in Flags)
        {
            double worstAngle = 0.0, worstRange = 0.0, worstRate = 0.0;
            for (int e = 0; e < _epochs.Length; e++)
            {
                var actual = Values(flag, e);
                var expected = Expected(flag, e);
                var delta = actual.Zip(expected, (a, x) => a - x).ToArray();
                delta[1] = System.Math.IEEERemainder(delta[1], global::IO.Astrodynamics.Constants.TwoPi);
                if (flag != Aberration.None)
                {
                    var none = Values(Aberration.None, e);
                    var noneExpected = Expected(Aberration.None, e);
                    for (int k = 0; k < 4; k++) delta[k] -= none[k] - noneExpected[k];
                    delta[1] = System.Math.IEEERemainder(delta[1], global::IO.Astrodynamics.Constants.TwoPi);
                }

                worstAngle = System.Math.Max(worstAngle, System.Math.Max(System.Math.Abs(delta[1] * System.Math.Cos(expected[2])), System.Math.Abs(delta[2])));
                worstRange = System.Math.Max(worstRange, System.Math.Abs(delta[0]));
                worstRate = System.Math.Max(worstRate, System.Math.Abs(delta[3]));
            }

            var bound = flag == Aberration.None ? geometric : corrected;
            _output.WriteLine($"{label} {flag.GetDescription(),-6} angle {(worstAngle * global::IO.Astrodynamics.Constants.Rad2Deg):E3} deg, range {worstRange:E3} m, range rate {worstRate:E3} m/s");
            Assert.True(worstAngle <= bound.Angle, $"{label} {flag}: angle {worstAngle:E3} rad");
            Assert.True(worstRange <= bound.Range, $"{label} {flag}: range {worstRange:E3} m");
            Assert.True(worstRate <= bound.RangeRate, $"{label} {flag}: range rate {worstRate:E3} m/s");
        }
    }

    [Fact]
    public void SpacecraftHorizontalFromCoordinateSite() => AssertHorizontal("spacecraft_from_site", _spacecraft, (1.7e-8, 7.0e-2, 1.1e-4), (1.1e-10, 3.2e-4, 1.5e-6));

    [Fact]
    public void MoonHorizontalFromCoordinateSite() => AssertHorizontal("moon_from_site", _moon, (8.9e-16, 6.0e-7, 8.2e-12), (6.8e-14, 1.3e-5, 1.4e-9));

    [Theory]
    [InlineData(0.3, 0.9)] // general
    [InlineData(0.0, 0.0)] // the target at the apex of the observer's motion, û × v = 0
    [InlineData(1.0e-7, 0.0)] // next to it
    public void StellarAberrationRate_IsTheDerivativeOfTheCorrection(double offApex, double accelerationTilt)
    {
        // The rate of zzstelab against a central difference of stelab along (p + ṗ·h, v + a·h)
        var velocity = new Vector3(29780.0, 0.0, 0.0);
        var position = new Vector3(1.5e11, 1.5e11 * offApex, 0.0);
        var positionRate = new Vector3(-12000.0, 25000.0, 3000.0);
        var acceleration = new Vector3(-0.006, 0.004 + accelerationTilt * 0.002, 0.001);
        const double h = 1.0;
        var plus = ApparentPositionReader.StellarAberration(position + positionRate * h, velocity + acceleration * h) - (position + positionRate * h);
        var minus = ApparentPositionReader.StellarAberration(position - positionRate * h, velocity - acceleration * h) - (position - positionRate * h);
        var numerical = (plus - minus) / (2.0 * h);
        var analytic = CelestialItem.StellarAberrationRate(position, positionRate, velocity, acceleration);
        _output.WriteLine($"{offApex}: |analytic| {analytic.Magnitude():E6} m/s, |analytic - numerical| {(analytic - numerical).Magnitude():E3} m/s");
        // Twice the worst over Linux x64, Windows x64 and macOS arm64 (the same on the three, CI of #596 at cde5d3f5),
        // 1.45e-5 m/s, the rounding of the central difference
        // (1.5e11 m x 1e-16 / 1 s); its truncation is far below
        Assert.True((analytic - numerical).Magnitude() < 2.9e-5, $"{(analytic - numerical).Magnitude():E3}");
    }

    [Fact]
    public void CorrectFromAberrationCB_NoneAndInvalid()
    {
        var geometric = _moon.GetGeometricStateRelativeTo(_epochs[0], _earth);
        var observer = _site.GetGeometricStateRelativeTo(_epochs[0], _earth);
        Assert.Same(geometric, CelestialItem.CorrectFromAberrationCB(_moon, _epochs[0], _site, Aberration.None, geometric, observer, _earth));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            CelestialItem.CorrectFromAberrationCB(_moon, _epochs[0], _site, (Aberration)99, geometric, observer, _earth));
        Assert.Equal(Vector3.Zero, CelestialItem.StellarAberrationRate(Vector3.Zero, Vector3.Zero, new Vector3(30000.0, 0.0, 0.0), Vector3.Zero));
    }
}
