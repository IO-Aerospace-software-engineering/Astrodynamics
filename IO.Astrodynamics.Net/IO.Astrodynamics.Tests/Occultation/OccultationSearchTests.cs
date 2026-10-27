using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Occultation;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Occultation;

/// <summary>
/// The rules of the occultation searches (#277) beyond the validation of the ellipsoids: point bodies against
/// <c>gfoclt</c>, <see cref="OccultationType.None"/>, the types a point cannot have, the CSPICE path, the instant test
/// <see cref="CelestialItem.IsOcculted(CelestialItem, OrbitalParameters.OrbitalParameters, Aberration)"/> and the
/// arguments.
/// </summary>
public class OccultationSearchTests
{
    // Provisional, twice the worst value on Linux x64 (2026-10-27): the managed search and gfoclt both at 1e-6 s
    private const double GfocltThreshold = 1.4e-6;

    private readonly ITestOutputHelper _output;

    public OccultationSearchTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/OccultationReference"));
    }

    private static Window Eclipse2005Window => new(new TimeSystem.Time("2005-10-03 00:00:00"), new TimeSystem.Time("2005-10-04 00:00:00"));

    private static Window Eclipse001Window => new(new TimeSystem.Time("2026-03-20T00:00:00.0000000 TDB"), new TimeSystem.Time("2026-03-20T12:00:00.0000000 TDB"));

    private static double WorstEdge(Window[] actual, Window[] expected)
    {
        Assert.Equal(expected.Length, actual.Length);
        return expected.Select((e, i) => System.Math.Max(System.Math.Abs((actual[i].StartDate - e.StartDate).TotalSeconds),
            System.Math.Abs((actual[i].EndDate - e.EndDate).TotalSeconds))).DefaultIfEmpty(0.0).Max();
    }

    [Theory]
    [InlineData(ShapeType.Point, ShapeType.Ellipsoid, OccultationType.Any, "dss65_2005_point_back ANY NONE IAU_MOON")]
    [InlineData(ShapeType.Point, ShapeType.Ellipsoid, OccultationType.Full, "dss65_2005_point_back ANY NONE IAU_MOON")]
    [InlineData(ShapeType.Ellipsoid, ShapeType.Point, OccultationType.Any, "dss65_2005_point_front ANY NONE POINT")]
    [InlineData(ShapeType.Ellipsoid, ShapeType.Point, OccultationType.Annular, "dss65_2005_point_front ANY NONE POINT")]
    public void PointBody_MatchesGfocltAny(ShapeType targetShape, ShapeType frontShape, OccultationType type, string reference)
    {
        // The annular eclipse of 2005-10-03 from DSS-65: the Sun's center hidden by the Moon, the Moon's center across the
        // Sun's disc. A point back body is hidden (Full is Any), a point front body transits (Annular is Any). The
        // reference is gfoclt at 1e-9 s (tools/occultation_reference): the native proxy passes ELLIPSOID for every front
        // body, so gfoclt cannot be run in the process with a point front body
        var site = new Site(65, "DSS-65", TestHelpers.EarthAtJ2000);
        var managed = site.FindWindowsOnOccultationConstraint(Eclipse2005Window, TestHelpers.Sun, targetShape, TestHelpers.MoonAtJ2000, frontShape, type,
            Aberration.None, TimeSpan.FromSeconds(360.0)).ToArray();
        var expected = JsonSerializer.Deserialize<Dictionary<string, double[][]>>(File.ReadAllText("Data/OccultationReference/occultation_reference.json"))[reference]
            .Select(w => new Window(TimeSystem.Time.CreateTDB(w[0]), TimeSystem.Time.CreateTDB(w[1]))).ToArray();
        double worst = WorstEdge(managed, expected);
        _output.WriteLine($"MEASURE #277 dss65_2005 point {(targetShape == ShapeType.Point ? "back" : "front")} {type}: {expected.Length} windows, worst {worst:E2} s");
        Assert.Single(expected);
        Assert.True(worst < GfocltThreshold, $"{worst:E2} s");
    }

    [Theory]
    [InlineData(ShapeType.Point, ShapeType.Ellipsoid, OccultationType.Partial)]
    [InlineData(ShapeType.Point, ShapeType.Ellipsoid, OccultationType.Annular)]
    [InlineData(ShapeType.Ellipsoid, ShapeType.Point, OccultationType.Partial)]
    [InlineData(ShapeType.Ellipsoid, ShapeType.Point, OccultationType.Full)]
    public void PointBody_TypesThatCannotHappen_GiveNoWindow(ShapeType targetShape, ShapeType frontShape, OccultationType type)
    {
        var site = new Site(65, "DSS-65", TestHelpers.EarthAtJ2000);
        Assert.Empty(site.FindWindowsOnOccultationConstraint(Eclipse2005Window, TestHelpers.Sun, targetShape, TestHelpers.MoonAtJ2000, frontShape, type,
            Aberration.None, TimeSpan.FromSeconds(360.0)));

        // The CSPICE path, a spacecraft of the kernels, applies the same rule
        var spacecraft = new Spacecraft(-470001, "eclipse_001", epoch: Eclipse001Window.StartDate);
        var earthType = type == OccultationType.Annular ? OccultationType.Partial : type;
        Assert.Empty(TestHelpers.Sun.FindWindowsOnOccultationConstraint(Eclipse001Window, spacecraft, targetShape, TestHelpers.EarthAtJ2000, frontShape, earthType,
            Aberration.None, TimeSpan.FromSeconds(60.0)));
    }

    [Fact]
    public void SpicePath_PointBackBody_FullIsAny()
    {
        var spacecraft = new Spacecraft(-470001, "eclipse_001", epoch: Eclipse001Window.StartDate);
        var full = TestHelpers.Sun.FindWindowsOnOccultationConstraint(Eclipse001Window, spacecraft, ShapeType.Point, TestHelpers.EarthAtJ2000, ShapeType.Ellipsoid,
            OccultationType.Full, Aberration.None, TimeSpan.FromSeconds(60.0)).ToArray();
        var any = SpiceAPI.Instance.FindWindowsOnOccultationConstraint(Eclipse001Window, -470001, 10, ShapeType.Point, 399, ShapeType.Ellipsoid, OccultationType.Any,
            Aberration.None, TimeSpan.FromSeconds(60.0)).ToArray();
        Assert.Equal(8, any.Length);
        Assert.Equal(any, full);
    }

    [Fact]
    public void None_IsTheComplementOfAny_OnBothPaths()
    {
        var spacecraft = new Spacecraft(-470001, "eclipse_001", epoch: Eclipse001Window.StartDate);
        var any = TestHelpers.Sun.FindWindowsOnOccultationConstraint(Eclipse001Window, spacecraft, ShapeType.Ellipsoid, TestHelpers.EarthAtJ2000, ShapeType.Ellipsoid,
            OccultationType.Any, Aberration.None, TimeSpan.FromSeconds(60.0)).ToArray();
        var none = TestHelpers.Sun.FindWindowsOnOccultationConstraint(Eclipse001Window, spacecraft, ShapeType.Ellipsoid, TestHelpers.EarthAtJ2000, ShapeType.Ellipsoid,
            OccultationType.None, Aberration.None, TimeSpan.FromSeconds(60.0)).ToArray();
        var managedNone = OccultationSearch.Managed(Eclipse001Window, spacecraft, TestHelpers.Sun, ShapeType.Ellipsoid, TestHelpers.EarthAtJ2000, ShapeType.Ellipsoid,
            OccultationType.None, Aberration.None, TimeSpan.FromSeconds(60.0), null).ToArray();

        // Any holds until the end of the search: its last window ends there, and None has one window before each of Any's
        Assert.Equal(8, any.Length);
        Assert.Equal(Eclipse001Window.EndDate, any[^1].EndDate);
        Assert.Equal(8, none.Length);
        Assert.Equal(Eclipse001Window.StartDate, none[0].StartDate);
        for (int i = 0; i < any.Length; i++)
        {
            Assert.Equal(any[i].StartDate, none[i].EndDate);
        }

        Assert.True(WorstEdge(managedNone, none) < GfocltThreshold);
    }

    [Fact]
    public void IsOcculted_TypesOfTheAnnularEclipseFromDss65()
    {
        // gfoclt from DSS-65 (tools/occultation_reference): partial from 181597306.9, annular from 181601839.1 to
        // 181602081.8, partial until 181607086.1 TDB seconds past J2000
        var site = new Site(65, "DSS-65", TestHelpers.EarthAtJ2000);
        var sun = TestHelpers.Sun;
        var moon = TestHelpers.MoonAtJ2000;

        OccultationType? At(double seconds, CelestialItem target, CelestialItem by)
        {
            var epoch = TimeSystem.Time.CreateTDB(seconds);
            var from = site.GetEphemeris(epoch, TestHelpers.EarthAtJ2000, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            return target.IsOcculted(by, from, Aberration.None);
        }

        Assert.Equal(OccultationType.None, At(181590000.0, sun, moon));
        Assert.Equal(OccultationType.Partial, At(181599000.0, sun, moon));
        Assert.Equal(OccultationType.Annular, At(181601960.0, sun, moon));
        // The Sun behind the Moon is no occultation of the Moon by the Sun
        Assert.Equal(OccultationType.None, At(181601960.0, moon, sun));
    }

    [Fact]
    public void IsOcculted_FullEclipseOfASpacecraft()
    {
        // In the middle of the first umbra of eclipse 001 (gfoclt: 827237996.05 to 827240060.05 TDB seconds)
        var epoch = TimeSystem.Time.CreateTDB(827239000.0);
        var spacecraft = new Spacecraft(-470001, "eclipse_001", epoch: epoch);
        var from = spacecraft.GetEphemeris(epoch, TestHelpers.EarthAtJ2000, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        Assert.Equal(OccultationType.Full, TestHelpers.Sun.IsOcculted(TestHelpers.EarthAtJ2000, from, Aberration.LT));
        var later = TimeSystem.Time.CreateTDB(827242000.0);
        var fromLater = spacecraft.GetEphemeris(later, TestHelpers.EarthAtJ2000, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        Assert.Equal(OccultationType.None, TestHelpers.Sun.IsOcculted(TestHelpers.EarthAtJ2000, fromLater, Aberration.None));
    }

    [Fact]
    public void Arguments_AreChecked()
    {
        var site = new Site(65, "DSS-65", TestHelpers.EarthAtJ2000);
        var sun = TestHelpers.Sun;
        var moon = TestHelpers.MoonAtJ2000;
        var step = TimeSpan.FromSeconds(360.0);
        Assert.Throws<ArgumentException>(() => site.FindWindowsOnOccultationConstraint(Eclipse2005Window, sun, ShapeType.Point, moon, ShapeType.Point,
            OccultationType.Any, Aberration.None, step).ToArray());
        Assert.Throws<ArgumentException>(() => OccultationSearch.Spice(Eclipse2005Window, 399065, 10, ShapeType.Point, 301, ShapeType.Point, OccultationType.Any,
            Aberration.None, step, null));
        Assert.Throws<ArgumentException>(() => site.FindWindowsOnOccultationConstraint(Eclipse2005Window, sun, ShapeType.Ellipsoid, sun, ShapeType.Ellipsoid,
            OccultationType.Any, Aberration.None, step).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => site.FindWindowsOnOccultationConstraint(Eclipse2005Window, sun, ShapeType.Ellipsoid, moon, ShapeType.Ellipsoid,
            OccultationType.Any, Aberration.None, TimeSpan.Zero).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => new OccultationSearchOptions { ConvergenceTolerance = TimeSpan.Zero });
        Assert.Throws<ArgumentNullException>(() => site.FindWindowsOnOccultationConstraint(Eclipse2005Window, null, ShapeType.Ellipsoid, moon, ShapeType.Ellipsoid,
            OccultationType.Any, Aberration.None, step));

        // A spacecraft is no ellipsoid
        var spacecraft = new Spacecraft(-470001, "eclipse_001", epoch: Eclipse001Window.StartDate);
        Assert.Throws<ArgumentException>(() => sun.FindWindowsOnOccultationConstraint(Eclipse001Window, TestHelpers.EarthAtJ2000, ShapeType.Ellipsoid, spacecraft,
            ShapeType.Ellipsoid, OccultationType.Any, Aberration.None, step).ToArray());

        // An observer inside a body
        var center = new StateVector(Vector3.Zero, Vector3.Zero, TestHelpers.EarthAtJ2000, Eclipse001Window.StartDate, Frames.Frame.ICRF);
        Assert.Throws<InvalidOperationException>(() => sun.IsOcculted(TestHelpers.EarthAtJ2000, center, Aberration.None));
    }

    [Fact]
    public void Brent_ChecksItsBracket()
    {
        Assert.Throws<ArgumentException>(() => Brent.FindRoot(x => x, 1.0, 2.0, 1.0, 2.0, 1e-9));
        Assert.Throws<ArgumentException>(() => Brent.FindRoot(x => x, -1.0, 2.0, -1.0, 2.0, 0.0));
        Assert.Equal(-1.0, Brent.FindRoot(x => x + 1.0, -1.0, 2.0, 0.0, 3.0, 1e-9));
        Assert.Equal(2.0, Brent.FindRoot(x => x - 2.0, -1.0, 2.0, -3.0, 0.0, 1e-9));
        Assert.Equal(System.Math.Sqrt(2.0), Brent.FindRoot(x => x * x - 2.0, 0.0, 2.0, -2.0, 2.0, 1e-12), 11);
        Assert.Throws<ArgumentException>(() => Brent.Minimize(x => x, 1.0, 1.0, 1e-9, out _));
        Assert.Throws<ArgumentException>(() => Brent.Minimize(x => x, 0.0, 1.0, 0.0, out _));
        Assert.Equal(0.3, Brent.Minimize(x => (x - 0.3) * (x - 0.3), 0.0, 1.0, 1e-10, out double minimum), 6);
        Assert.True(minimum < 1e-11);
    }
}
