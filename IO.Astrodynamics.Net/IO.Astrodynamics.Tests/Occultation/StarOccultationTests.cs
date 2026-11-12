// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Equatorial = IO.Astrodynamics.Coordinates.Equatorial;
using Site = IO.Astrodynamics.Surface.Site;

namespace IO.Astrodynamics.Tests.Occultation;

/// <summary>
/// A <see cref="Star"/> is a direction, read at the epoch, in the managed occultation search and in
/// <see cref="Star.GetEphemeris"/>, whatever the correction (#533).
/// </summary>
/// <remarks>
/// <para>
/// Before, with LT or CN, the star was read at t − lt, its light time from the observer: 65.2 years at 20 pc, so its
/// proper motion over that time moved a lunar occultation by 9.6 s for 0.1″/yr (#533's measurement), and from a site the
/// Earth was read there too, before the start of de440s beyond about 54 pc, which throws since the reads of #454 (11.0.0).
/// </para>
/// <para>
/// <b>The reference</b>, <c>tools/occultation_reference/star_occultation_reference.py</c> (spiceypy, CSPICE N0067): the
/// star at its catalog position moved by its proper motion to t, minus DSS-13 from the barycenter at t (a catalog position
/// is the direction of the light that reaches the barycenter at its epoch, as SOFA's <c>iauPmsafe</c> reads it, to be
/// verified by S. Guillet; the field-of-view search reads a star so since #517, as <c>gfrfov</c> reads a ray); the Moon
/// from <c>spkpos</c> with the correction, a sphere of 1737.4 km; the entry and the exit the zeros of asin(R/d) − sep, by
/// bisection to 1e-9 s. The direction of <see cref="Star.GetEphemeris"/>: the star minus DSS-13 at the epoch, rotated by
/// <c>stelab</c> for +S.
/// </para>
/// <para>
/// <b>The case</b> (#533's): the star on the Moon's LT direction from DSS-13 at S = 2026-03-01 12:00 TDB, at 20 pc and at
/// 100 pc from DSS-13, without proper motion and with 0.1″/yr in right ascension (catalog epoch S); the search over
/// S ± 3 h, step 60 s, tolerance 1 µs, with each correction of the occultation search (None, LT, CN, XLT and XCN: before
/// #533 the star was read at t − lt or t + lt, its light time from the observer, whatever the sign).
/// </para>
/// </remarks>
public class StarOccultationTests
{
    private static readonly TimeSystem.Time S = TimeSystem.Time.CreateTDB(825638400.0);
    private static readonly Window SearchWindow = new(S.AddSeconds(-3.0 * 3600.0), S.AddSeconds(3.0 * 3600.0));
    private const double Arcsec = System.Math.PI / 180.0 / 3600.0;

    // The worst edge over the 20 cases is 2.38 µs on Linux x64, Windows x64 and macOS arm64 alike (CI run 43086661602,
    // 2026-11-11; XLT entry, 20 tick spacings of a TDB epoch near 8.3e8 s), 1.43 µs without XLT and XCN (20 pc, CN),
    // within the search's 1 µs tolerance and the light-time iteration of the Moon (#533 measured 1.4 µs with the same
    // reference); the bound is twice it, rounded up
    private const double EdgeLimit = 4.8e-6;

    // The angle (Separation) of the direction of Star.GetEphemeris to the reference: at most 1.71e-16 rad on Linux x64 and
    // Windows x64, 2.15e-16 rad on macOS arm64 (CI run 43086661602, 2026-11-11), against 3.0e-5 rad (6.2″) for 0.1″/yr at
    // 20 pc before #533; the bound is twice the worst of the three, rounded up (it was 3.0e-15 rad, twice the move of 8 ulps
    // per component, until those logs)
    private const double DirectionLimit = 4.4e-16;

    // |angle(transmission, geometric) − angle(reception, geometric)|, stlabx the mirror of stelab: 5.06e-17 rad on Linux
    // x64, Windows x64 and macOS arm64 alike (CI run 43086661602, 2026-11-11); the bound is twice it, rounded up (it was
    // 6.0e-15 rad, from 8 ulps per component, until those logs)
    private const double MirrorLimit = 1.1e-16;

    private readonly ITestOutputHelper _output;

    public StarOccultationTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    /// <summary>The right ascension, declination and parallax of the star of the reference, at <paramref name="distancePc"/>.</summary>
    private static Star NewStar(double distancePc, double properMotionArcsecPerYear)
    {
        (double ra, double dec, double parallax) = distancePc == 20.0
            ? (2.407051209225294, 0.3073668772899178, 0.04999998918876296)
            : (2.407051126402387, 0.3073669059104148, 0.009999999567550454);
        return new Star(533000 + (int)distancePc, $"STAR533_{distancePc}", 1.989e30, "G2V", 5.0, parallax, new Equatorial(dec, ra, S),
            0.0, properMotionArcsecPerYear * Arcsec, 0.0, 0.0, 0.0, 0.0, S);
    }

    /// <summary>The entry and the exit of the reference, TDB seconds from J2000.</summary>
    private static (double Entry, double Exit) Reference(double distancePc, double properMotion, Aberration aberration) =>
        (distancePc, properMotion, aberration) switch
        {
            (20.0, 0.0, Aberration.None) => (825636696.6894515, 825639975.673676),
            (20.0, 0.0, Aberration.LT) => (825636730.4999154, 825640007.2576585),
            (20.0, 0.0, Aberration.CN) => (825636730.5013449, 825640007.2589674),
            (20.0, 0.0, Aberration.XLT) => (825636662.8887165, 825639944.0466852),
            (20.0, 0.0, Aberration.XCN) => (825636662.8901455, 825639944.0479977),
            (20.0, 0.1, Aberration.None) => (825636696.6894436, 825639975.6736829),
            (20.0, 0.1, Aberration.LT) => (825636730.4999075, 825640007.2576656),
            (20.0, 0.1, Aberration.CN) => (825636730.501337, 825640007.2589746),
            (20.0, 0.1, Aberration.XLT) => (825636662.8887084, 825639944.0466921),
            (20.0, 0.1, Aberration.XCN) => (825636662.8901374, 825639944.0480046),
            (100.0, 0.0, Aberration.None) => (825636696.68943, 825639975.6736946),
            (100.0, 0.0, Aberration.LT) => (825636730.4998941, 825640007.2576776),
            (100.0, 0.0, Aberration.CN) => (825636730.5013239, 825640007.2589865),
            (100.0, 0.0, Aberration.XLT) => (825636662.8886945, 825639944.0467033),
            (100.0, 0.0, Aberration.XCN) => (825636662.8901236, 825639944.0480161),
            (100.0, 0.1, Aberration.None) => (825636696.6894219, 825639975.6737015),
            (100.0, 0.1, Aberration.LT) => (825636730.4998863, 825640007.2576845),
            (100.0, 0.1, Aberration.CN) => (825636730.5013161, 825640007.2589934),
            (100.0, 0.1, Aberration.XLT) => (825636662.8886864, 825639944.0467103),
            (100.0, 0.1, Aberration.XCN) => (825636662.8901155, 825639944.048023),
            _ => throw new ArgumentOutOfRangeException()
        };

    [Theory]
    [InlineData(20.0, 0.0, Aberration.None)]
    [InlineData(20.0, 0.0, Aberration.LT)]
    [InlineData(20.0, 0.0, Aberration.CN)]
    [InlineData(20.0, 0.0, Aberration.XLT)]
    [InlineData(20.0, 0.0, Aberration.XCN)]
    [InlineData(20.0, 0.1, Aberration.None)]
    [InlineData(20.0, 0.1, Aberration.LT)]
    [InlineData(20.0, 0.1, Aberration.CN)]
    [InlineData(20.0, 0.1, Aberration.XLT)]
    [InlineData(20.0, 0.1, Aberration.XCN)]
    [InlineData(100.0, 0.0, Aberration.None)]
    [InlineData(100.0, 0.0, Aberration.LT)]
    [InlineData(100.0, 0.0, Aberration.CN)]
    [InlineData(100.0, 0.0, Aberration.XLT)]
    [InlineData(100.0, 0.0, Aberration.XCN)]
    [InlineData(100.0, 0.1, Aberration.None)]
    [InlineData(100.0, 0.1, Aberration.LT)]
    [InlineData(100.0, 0.1, Aberration.CN)]
    [InlineData(100.0, 0.1, Aberration.XLT)]
    [InlineData(100.0, 0.1, Aberration.XCN)]
    public void LunarOccultationFromASite(double distancePc, double properMotion, Aberration aberration)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var dss13 = new Site(13, "DSS-13", earth);
        var star = NewStar(distancePc, properMotion);

        var windows = star.FindWindowsOnOccultationConstraint(SearchWindow, dss13, ShapeType.Point, moon, ShapeType.Ellipsoid,
            OccultationType.Any, aberration, TimeSpan.FromSeconds(60.0)).ToArray();

        var (entry, exit) = Reference(distancePc, properMotion, aberration);
        Assert.Single(windows);
        double entryError = windows[0].StartDate.ToTDB().TimeSpanFromJ2000().TotalSeconds - entry;
        double exitError = windows[0].EndDate.ToTDB().TimeSpanFromJ2000().TotalSeconds - exit;
        _output.WriteLine($"MEASURE {distancePc} pc {properMotion}\"/yr {aberration}: entry {entryError:E3} s, exit {exitError:E3} s");
        Assert.True(System.Math.Abs(entryError) <= EdgeLimit, $"entry {entryError:E3} s");
        Assert.True(System.Math.Abs(exitError) <= EdgeLimit, $"exit {exitError:E3} s");
    }

    /// <summary>
    /// The angle between two directions, atan2(|a × b|, a · b), which resolves an angle near zero to the rounding of its
    /// components (about 1e-16 rad); <see cref="Vector3.Angle"/>, the acos of the dot product, gives 0 or at least
    /// acos(1 − 2⁻⁵³) = 1.49e-8 rad there, so it cannot measure a direction against its reference.
    /// </summary>
    private static double Separation(in Vector3 a, in Vector3 b) => System.Math.Atan2(a.Cross(b).Magnitude(), a * b);

    /// <summary>The direction of the star at S + 1 h from DSS-13, without and with the stellar aberration.</summary>
    private static Vector3 ReferenceDirection(double distancePc, double properMotion, bool stellar) =>
        (distancePc, properMotion, stellar) switch
        {
            (20.0, 0.0, false) => new Vector3(-0.7073567018437554, 0.6388348698644372, 0.302550004795922),
            (20.0, 0.0, true) => new Vector3(-0.7074207548950949, 0.638775468405415, 0.3025256625595529),
            (20.0, 0.1, false) => new Vector3(-0.7073567018790867, 0.638834869825316, 0.302550004795922),
            (20.0, 0.1, true) => new Vector3(-0.7074207549304246, 0.6387754683662894, 0.30252566255955143),
            (100.0, 0.0, false) => new Vector3(-0.7073567019335424, 0.6388348697811483, 0.3025500047618656),
            (100.0, 0.0, true) => new Vector3(-0.7074207549848778, 0.6387754683221154, 0.30252566252549157),
            (100.0, 0.1, false) => new Vector3(-0.7073567019688737, 0.6388348697420273, 0.3025500047618656),
            (100.0, 0.1, true) => new Vector3(-0.7074207550202076, 0.6387754682829898, 0.30252566252549024),
            _ => throw new ArgumentOutOfRangeException()
        };

    [Theory]
    [InlineData(20.0, 0.1, Aberration.None)]
    [InlineData(20.0, 0.1, Aberration.LT)]
    [InlineData(20.0, 0.1, Aberration.CN)]
    [InlineData(20.0, 0.1, Aberration.XLT)]
    [InlineData(20.0, 0.1, Aberration.LTS)]
    [InlineData(20.0, 0.1, Aberration.CNS)]
    [InlineData(100.0, 0.1, Aberration.LT)]
    [InlineData(100.0, 0.1, Aberration.CNS)]
    [InlineData(100.0, 0.0, Aberration.XCN)]
    [InlineData(100.0, 0.0, Aberration.XLTS)]
    public void GetEphemeris_IsTheDirectionAtTheEpoch(double distancePc, double properMotion, Aberration aberration)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var dss13 = new Site(13, "DSS-13", earth);
        var star = NewStar(distancePc, properMotion);
        TimeSystem.Time epoch = S.AddSeconds(3600.0);

        var position = star.GetEphemeris(epoch, dss13, Frames.Frame.ICRF, aberration).ToStateVector().Position;
        bool stellar = aberration is Aberration.LTS or Aberration.CNS or Aberration.XLTS or Aberration.XCNS;
        var expected = ReferenceDirection(distancePc, properMotion, stellar && aberration is Aberration.LTS or Aberration.CNS);
        if (aberration is Aberration.XLTS or Aberration.XCNS)
        {
            // Transmission: stlabx, the stellar aberration of the opposite velocity, the mirror of stelab about the
            // geometric direction to first order; checked against the geometric direction and the reception one
            var geometric = ReferenceDirection(distancePc, properMotion, false);
            var reception = ReferenceDirection(distancePc, properMotion, true);
            double toGeometric = Separation(position, geometric);
            double toReception = Separation(reception, geometric);
            _output.WriteLine($"MEASURE transmission {aberration}: {toGeometric:E3} rad from the geometric direction, reception {toReception:E3}");
            _output.WriteLine($"MEASURE transmission {aberration}: mirror difference {toGeometric - toReception:E3} rad");
            Assert.True(System.Math.Abs(toGeometric - toReception) <= MirrorLimit, $"{toGeometric:E3} against {toReception:E3}");
            Assert.True(Separation(position, reception) > 1.9 * toReception);
            return;
        }

        double angle = Separation(position, expected);
        _output.WriteLine($"MEASURE direction {distancePc} pc {properMotion}\"/yr {aberration}: {angle:E3} rad");
        Assert.True(angle <= DirectionLimit, $"{angle:E3} rad");
        // The distance is that of the star from DSS-13 at the epoch, the stellar aberration a rotation
        double distance = (star.GetEquatorialCoordinates(epoch).ToCartesian()
                           - dss13.GetGeometricStateRelativeTo(epoch, Barycenters.SOLAR_SYSTEM_BARYCENTER).Position).Magnitude();
        Assert.True(System.Math.Abs(position.Magnitude() / distance - 1.0) <= 1e-14);
    }
}
