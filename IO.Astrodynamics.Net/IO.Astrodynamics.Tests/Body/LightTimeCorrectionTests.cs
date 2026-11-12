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
/// site of the kernels as observer, the Moon, the Sun and the spacecraft as targets.
/// </summary>
public class LightTimeCorrectionTests
{
    private static readonly Aberration[] Flags =
    [
        Aberration.None, Aberration.LT, Aberration.LTS, Aberration.CN, Aberration.CNS, Aberration.XLT, Aberration.XLTS,
        Aberration.XCN, Aberration.XCNS
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
        _epochs = _reference.GetProperty("epochs_tdb").EnumerateArray().Select(e => TimeSystem.Time.CreateTDB(e.GetDouble())).ToArray();
        _earth = new CelestialBody(PlanetsAndMoons.EARTH);
        _moon = new CelestialBody(PlanetsAndMoons.MOON);
        _sun = new CelestialBody(Stars.Sun);
        var site = _reference.GetProperty("site");
        _site = new Site(491, "G491", _earth, new Planetodetic(site.GetProperty("longitude_deg").GetDouble() * global::IO.Astrodynamics.Constants.Deg2Rad,
            site.GetProperty("latitude_deg").GetDouble() * global::IO.Astrodynamics.Constants.Deg2Rad, site.GetProperty("altitude_m").GetDouble()));
        _dss13 = new Site(13, "DSS-13", _earth);
        var s0 = _reference.GetProperty("spacecraft_state_e0").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        _spacecraft = new Spacecraft(-1001, "S491", 100.0, 1000.0, new Clock("S491", 65536),
            new StateVector(new Vector3(s0[0], s0[1], s0[2]), new Vector3(s0[3], s0[4], s0[5]), _earth, _epochs[0], Frames.Frame.ICRF));
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
            _output.WriteLine($"{label} {flag.GetDescription(),-6} position {worst[flag].Position:E3} m, velocity {worst[flag].Velocity:E3} m/s");
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
            var state = target.GetEphemeris(_epochs[e], _site, Frames.Frame.ICRF, flag).ToStateVector();
            return [horizontal.Range, horizontal.Azimuth, horizontal.Elevation, state.Position.Normalize() * state.Velocity];
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
                delta[1] = System.Math.IEEERemainder(delta[1], global::IO.Astrodynamics.Constants._2PI);
                if (flag != Aberration.None)
                {
                    var none = Values(Aberration.None, e);
                    var noneExpected = Expected(Aberration.None, e);
                    for (int k = 0; k < 4; k++) delta[k] -= none[k] - noneExpected[k];
                    delta[1] = System.Math.IEEERemainder(delta[1], global::IO.Astrodynamics.Constants._2PI);
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
