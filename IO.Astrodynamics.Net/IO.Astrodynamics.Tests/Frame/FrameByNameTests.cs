// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Exceptions;
using Xunit;

namespace IO.Astrodynamics.Tests.Frame;

/// <summary>
/// Frames by name on SPICE (R25 of #668, lot L1, decided by S. Guillet, 2026-11-19): a name the loaded kernels do not
/// define throws <see cref="ArgumentException"/> with SPICE's error as inner exception on every path that hands it to
/// SPICE, a coverage gap of a known frame stays <see cref="InvalidOperationException"/>, and <see cref="Frames.Frame.ITRF93"/>
/// is the frame of the Earth. The unknown names are checked by <c>namfrm_c</c> after the failed call, SPICE's message,
/// which names the frame, that of the inner exception.
/// The <see cref="DataProvider.MemoryDataProvider"/> side, which needs the global provider, runs in a child process
/// (<c>MemoryDataProviderBodyTests</c>).
/// </summary>
public class FrameByNameTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 6, 1, 12, 0, 0, TimeSystem.TimeScale.TDB);

    public FrameByNameTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static void AssertUnknownFrame(ArgumentException e, string paramName, string name)
    {
        Assert.Equal(paramName, e.ParamName);
        Assert.Contains($"'{name}' is not a frame of the loaded kernels", e.Message);
        // SPICE's message, which names the frame: sxform_c's SPICE(UNKNOWNFRAME) "was not recognized as a known reference
        // frame", spkezr_c's "The requested output frame ... is not recognized" or "The string supplied to specify the
        // reference frame ... "
        var inner = Assert.IsType<SpiceException>(e.InnerException);
        Assert.Contains(name, inner.Message);
    }

    [Theory]
    [InlineData("ITRF")]
    [InlineData("NOT_A_FRAME")]
    public void TransformFrame_OfAnUnknownName_ThrowsArgumentException_NamingTheParameter(string name)
    {
        // Before 11.0.0 an InvalidOperationException, the type of a coverage gap
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() =>
            SpiceApi.Instance.TransformFrame(Epoch, new Frames.Frame(name), Frames.Frame.ICRF)), "fromFrame", name);
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() =>
            SpiceApi.Instance.TransformFrame(Epoch, Frames.Frame.ICRF, new Frames.Frame(name))), "toFrame", name);
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() =>
            SpiceApi.Instance.TransformFrame(Epoch, Frames.Frame.ITRF93, new Frames.Frame(name))), "toFrame", name);
    }

    [Fact]
    public void ToFrame_OfAnUnknownName_ThrowsArgumentException_BothWays()
    {
        // Arrange: a LEO state about the Earth in ICRF, and the same numbers labelled with an unknown frame
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var state = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7600.0, 0.0), earth, Epoch, Frames.Frame.ICRF);
        var unknown = new StateVector(state.Position, state.Velocity, earth, Epoch, new Frames.Frame("ITRF"));

        // Act and assert
        Assert.IsType<SpiceException>(Assert.Throws<ArgumentException>(() => state.ToFrame(new Frames.Frame("ITRF"))).InnerException);
        Assert.IsType<SpiceException>(Assert.Throws<ArgumentException>(() => unknown.ToFrame(Frames.Frame.ICRF)).InnerException);
    }

    public static TheoryData<string> ManagedFrames => new() { "TEME", "CIRS", "TIRS" };

    private static Frames.Frame Managed(string name) => name switch
    {
        "TEME" => Frames.Frame.TEME,
        "CIRS" => Frames.Frame.CIRS,
        _ => Frames.Frame.TIRS
    };

    [Theory]
    [MemberData(nameof(ManagedFrames))]
    public void TransformFrame_BetweenAManagedFrameAndAnUnknownName_NamesTheParameterOfTheUnknownEnd(string managedName)
    {
        // The managed end is composed through ICRF, the unknown end read from SPICE by an inner transform: the parameter
        // named is that of the public call (review of L1: both were named fromFrame)
        var managed = Managed(managedName);
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() =>
            SpiceApi.Instance.TransformFrame(Epoch, managed, new Frames.Frame("NOT_A_FRAME"))), "toFrame", "NOT_A_FRAME");
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() =>
            SpiceApi.Instance.TransformFrame(Epoch, new Frames.Frame("NOT_A_FRAME"), managed)), "fromFrame", "NOT_A_FRAME");
    }

    [Fact]
    public void TransformFrame_BetweenASiteFrameFromCoordinatesAndAnUnknownName_NamesTheParameterOfTheUnknownEnd()
    {
        var site = new IO.Astrodynamics.Surface.Site(77, "FRAME_BY_NAME_SITE", new CelestialBody(PlanetsAndMoons.Earth),
            new Planetodetic(0.5, 0.7, 100.0));
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() =>
            SpiceApi.Instance.TransformFrame(Epoch, site.Frame, new Frames.Frame("NOT_A_FRAME"))), "toFrame", "NOT_A_FRAME");
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() =>
            SpiceApi.Instance.TransformFrame(Epoch, new Frames.Frame("NOT_A_FRAME"), site.Frame)), "fromFrame", "NOT_A_FRAME");
    }

    [Fact]
    public void ToFrame_OfAnUnknownName_NamesTheParameterOfThePublicCall_OrNone()
    {
        // Arrange: a LEO about the Earth in ICRF and in TEME, and the same numbers labelled with an unknown frame
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var state = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7600.0, 0.0), earth, Epoch, Frames.Frame.ICRF);
        var teme = new StateVector(state.Position, state.Velocity, earth, Epoch, Frames.Frame.TEME);
        var unknownFrame = new Frames.Frame("NOT_A_FRAME");
        var unknown = new StateVector(state.Position, state.Velocity, earth, Epoch, unknownFrame);

        // Act and assert: the frame argument of the call is named; the frame of the state or of the instance is no argument
        // of the call, so no parameter is named (design #671, R25, open choice 1)
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() => state.ToFrame(unknownFrame)), "frame", "NOT_A_FRAME");
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() => teme.ToFrame(unknownFrame)), "frame", "NOT_A_FRAME");
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() => unknown.ToFrame(Frames.Frame.ICRF)), null, "NOT_A_FRAME");
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() => unknown.ToEquatorial()), null, "NOT_A_FRAME");
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() => Frames.Frame.ICRF.ToFrame(unknownFrame, Epoch)), "targetFrame", "NOT_A_FRAME");
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() => Frames.Frame.TEME.ToFrame(unknownFrame, Epoch)), "targetFrame", "NOT_A_FRAME");
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() => unknownFrame.ToFrame(Frames.Frame.ICRF, Epoch)), null, "NOT_A_FRAME");
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() => new Frames.Frame("NOT_A_FRAME").GetStateOrientationToIcrf(Epoch)), null,
            "NOT_A_FRAME");
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() => new SpiceDataProvider().FrameTransformationToIcrf(Epoch, unknownFrame)),
            "source", "NOT_A_FRAME");
    }

    [Fact]
    public void CspiceWindowSearches_InAnUnknownFrame_ThrowArgumentException_NamingTheFrameParameter()
    {
        // The illumination, occultation and coordinate searches of CSPICE hand the frame to SPICE by name: an unknown name is
        // a bad argument, as on the transforms and the reads (review of L1: SpiceException before)
        var window = new TimeSystem.Window(Epoch, TimeSpan.FromDays(1.0));
        var unknown = new Frames.Frame("NOT_A_FRAME");
        var point = new Planetodetic(2.2 * IO.Astrodynamics.Constants.Deg2Rad, 48.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0);

        AssertUnknownFrame(Assert.Throws<ArgumentException>(() => SpiceApi.Instance.FindWindowsOnIlluminationConstraint(window, 10, 399,
            unknown, point, IlluminationAngle.Incidence, RelationalOperator.LessThan, System.Math.PI * 0.5, 0.0, Aberration.None,
            TimeSpan.FromHours(4.5), 10)), "fixedFrame", "NOT_A_FRAME");
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() => SpiceApi.Instance.FindWindowsOnOccultationConstraint(window, 399, 10,
            ShapeType.Ellipsoid, 301, ShapeType.Ellipsoid, OccultationType.Any, Aberration.LT, TimeSpan.FromHours(1.0), unknown, null)),
            "frontFrame", "NOT_A_FRAME");
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() => SpiceApi.Instance.FindWindowsOnOccultationConstraint(window, 399, 10,
            ShapeType.Ellipsoid, 301, ShapeType.Ellipsoid, OccultationType.Any, Aberration.LT, TimeSpan.FromHours(1.0), null, unknown)),
            "targetFrame", "NOT_A_FRAME");
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() => SpiceApi.Instance.FindWindowsOnCoordinateConstraint(window, 399, 301,
            unknown, CoordinateSystem.Rectangular, Coordinate.Z, RelationalOperator.GreaterThan, 0.0, 0.0, Aberration.None,
            TimeSpan.FromHours(1.0))), "frame", "NOT_A_FRAME");
    }

    [Theory]
    [InlineData(Aberration.None)]
    [InlineData(Aberration.LT)]
    public void Ephemeris_InAnUnknownFrame_ThrowsArgumentException(Aberration aberration)
    {
        // Arrange
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var moon = new CelestialBody(PlanetsAndMoons.Moon);
        var unknown = new Frames.Frame("ITRF");

        // Act and assert: the reads by object and by identifier, at an epoch and over a window
        Assert.IsType<SpiceException>(Assert.Throws<ArgumentException>(() => moon.GetEphemeris(Epoch, earth, unknown, aberration)).InnerException);
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() => SpiceApi.Instance.ReadEphemeris(Epoch, 399, 301, unknown, aberration)), "frame", "ITRF");
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() =>
            SpiceApi.Instance.ReadEphemeris(new TimeSystem.Window(Epoch, TimeSpan.FromHours(1.0)), 399, 301, unknown, aberration, TimeSpan.FromMinutes(10.0)).ToArray()),
            "frame", "ITRF");
    }

    [Theory]
    [InlineData("ICRF")]
    [InlineData("EME2000")]
    [InlineData("icrf")]
    public void TheNamesIcrfAndEme2000_AreOtherNames_WhoseErrorPointsToFrameIcrf(string name)
    {
        var e = Assert.Throws<ArgumentException>(() => SpiceApi.Instance.TransformFrame(Epoch, new Frames.Frame(name), Frames.Frame.ICRF));
        AssertUnknownFrame(e, "fromFrame", name);
        Assert.Contains("Use Frame.ICRF", e.Message);
        Assert.NotEqual(Frames.Frame.ICRF, new Frames.Frame(name));
    }

    [Fact]
    public void ACoverageGapOfAKnownFrame_StaysInvalidOperationException()
    {
        // ITRF93 before the Earth's binary PCK (earth_latest_high_prec.bpc starts in 2000): SPICE(FRAMEDATANOTFOUND)
        var before = new TimeSystem.Time(1950, 1, 1, 0, 0, 0, TimeSystem.TimeScale.TDB);
        var e = Assert.Throws<SpiceException>(() => SpiceApi.Instance.TransformFrame(before, Frames.Frame.ITRF93, Frames.Frame.ICRF));
        Assert.Contains("ITRF93", e.Message);
    }

    [Fact]
    public void Itrf93_IsTheFrameOfTheEarth()
    {
        // Arrange
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var byName = new Frames.Frame("ITRF93");

        // Act
        var fromStatic = Frames.Frame.ITRF93.GetStateOrientationToIcrf(Epoch);
        var fromName = byName.GetStateOrientationToIcrf(Epoch);
        var spice = SpiceApi.Instance.TransformFrame(Epoch, new Frames.Frame("ITRF93"), Frames.Frame.ICRF);

        // Assert: one frame (equality by name), the orientation of SPICE's ITRF93 bit for bit
        Assert.Equal("ITRF93", Frames.Frame.ITRF93.Name);
        Assert.Equal(byName, Frames.Frame.ITRF93);
        Assert.Equal(earth.Frame, Frames.Frame.ITRF93);
        Assert.Equal(spice.Rotation, fromStatic.Rotation);
        Assert.Equal(spice.AngularVelocity, fromStatic.AngularVelocity);
        Assert.Equal(fromName.Rotation, fromStatic.Rotation);
    }
}
