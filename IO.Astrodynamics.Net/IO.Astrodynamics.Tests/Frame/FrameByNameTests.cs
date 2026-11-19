// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
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
    private static readonly TimeSystem.Time Epoch = new(2021, 6, 1, 12, 0, 0, frame: TimeSystem.TimeFrame.TDBFrame);

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
        var inner = Assert.IsType<InvalidOperationException>(e.InnerException);
        Assert.Contains(name, inner.Message);
    }

    [Theory]
    [InlineData("ITRF")]
    [InlineData("NOT_A_FRAME")]
    public void TransformFrame_OfAnUnknownName_ThrowsArgumentException_NamingTheParameter(string name)
    {
        // Before 11.0.0 an InvalidOperationException, the type of a coverage gap
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() =>
            SpiceAPI.Instance.TransformFrame(Epoch, new Frames.Frame(name), Frames.Frame.ICRF)), "fromFrame", name);
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() =>
            SpiceAPI.Instance.TransformFrame(Epoch, Frames.Frame.ICRF, new Frames.Frame(name))), "toFrame", name);
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() =>
            SpiceAPI.Instance.TransformFrame(Epoch, Frames.Frame.ITRF93, new Frames.Frame(name))), "toFrame", name);
    }

    [Fact]
    public void ToFrame_OfAnUnknownName_ThrowsArgumentException_BothWays()
    {
        // Arrange: a LEO state about the Earth in ICRF, and the same numbers labelled with an unknown frame
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var state = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7600.0, 0.0), earth, Epoch, Frames.Frame.ICRF);
        var unknown = new StateVector(state.Position, state.Velocity, earth, Epoch, new Frames.Frame("ITRF"));

        // Act and assert
        Assert.IsType<InvalidOperationException>(Assert.Throws<ArgumentException>(() => state.ToFrame(new Frames.Frame("ITRF"))).InnerException);
        Assert.IsType<InvalidOperationException>(Assert.Throws<ArgumentException>(() => unknown.ToFrame(Frames.Frame.ICRF)).InnerException);
    }

    [Theory]
    [InlineData(Aberration.None)]
    [InlineData(Aberration.LT)]
    public void Ephemeris_InAnUnknownFrame_ThrowsArgumentException(Aberration aberration)
    {
        // Arrange
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var unknown = new Frames.Frame("ITRF");

        // Act and assert: the reads by object and by identifier, at an epoch and over a window
        Assert.IsType<InvalidOperationException>(Assert.Throws<ArgumentException>(() => moon.GetEphemeris(Epoch, earth, unknown, aberration)).InnerException);
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.ReadEphemeris(Epoch, 399, 301, unknown, aberration)), "frame", "ITRF");
        AssertUnknownFrame(Assert.Throws<ArgumentException>(() =>
            SpiceAPI.Instance.ReadEphemeris(new TimeSystem.Window(Epoch, TimeSpan.FromHours(1.0)), 399, 301, unknown, aberration, TimeSpan.FromMinutes(10.0)).ToArray()),
            "frame", "ITRF");
    }

    [Theory]
    [InlineData("ICRF")]
    [InlineData("EME2000")]
    [InlineData("icrf")]
    public void TheNamesIcrfAndEme2000_AreOtherNames_WhoseErrorPointsToFrameIcrf(string name)
    {
        var e = Assert.Throws<ArgumentException>(() => SpiceAPI.Instance.TransformFrame(Epoch, new Frames.Frame(name), Frames.Frame.ICRF));
        AssertUnknownFrame(e, "fromFrame", name);
        Assert.Contains("Use Frame.ICRF", e.Message);
        Assert.NotEqual(Frames.Frame.ICRF, new Frames.Frame(name));
    }

    [Fact]
    public void ACoverageGapOfAKnownFrame_StaysInvalidOperationException()
    {
        // ITRF93 before the Earth's binary PCK (earth_latest_high_prec.bpc starts in 2000): SPICE(FRAMEDATANOTFOUND)
        var before = new TimeSystem.Time(1950, 1, 1, 0, 0, 0, frame: TimeSystem.TimeFrame.TDBFrame);
        var e = Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.TransformFrame(before, Frames.Frame.ITRF93, Frames.Frame.ICRF));
        Assert.Contains("ITRF93", e.Message);
    }

    [Fact]
    public void Itrf93_IsTheFrameOfTheEarth()
    {
        // Arrange
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var byName = new Frames.Frame("ITRF93");

        // Act
        var fromStatic = Frames.Frame.ITRF93.GetStateOrientationToICRF(Epoch);
        var fromName = byName.GetStateOrientationToICRF(Epoch);
        var spice = SpiceAPI.Instance.TransformFrame(Epoch, new Frames.Frame("ITRF93"), Frames.Frame.ICRF);

        // Assert: one frame (equality by name), the orientation of SPICE's ITRF93 bit for bit
        Assert.Equal("ITRF93", Frames.Frame.ITRF93.Name);
        Assert.Equal(byName, Frames.Frame.ITRF93);
        Assert.Equal(earth.Frame, Frames.Frame.ITRF93);
        Assert.Equal(spice.Rotation, fromStatic.Rotation);
        Assert.Equal(spice.AngularVelocity, fromStatic.AngularVelocity);
        Assert.Equal(fromName.Rotation, fromStatic.Rotation);
    }
}
