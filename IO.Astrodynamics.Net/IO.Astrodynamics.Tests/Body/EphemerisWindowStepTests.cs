// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The step of a window read of <c>GetEphemeris</c> is checked (R11 of #668): a step of zero or less throws
/// <see cref="ArgumentOutOfRangeException"/> naming <c>stepSize</c>, on <see cref="IO.Astrodynamics.Body.CelestialItem"/>
/// and <see cref="Site"/>, before any read. Before 11.0.0 a zero step looped without end on a site and asked a list of
/// int.MaxValue states on a body, and a negative one threw the list's "capacity" exception or returned the start alone.
/// </summary>
public class EphemerisWindowStepTests
{
    private static readonly TimeSystem.Time Start = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
    private static readonly Window OneHour = new(Start, Start.AddHours(1.0));

    public EphemerisWindowStepTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-600000000L)]
    public void CelestialItem_StepNotPositive_Throws(long ticks)
    {
        var moon = PlanetsAndMoons.MOON_BODY;
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            moon.GetEphemeris(OneHour, PlanetsAndMoons.EARTH_BODY, Frames.Frame.ICRF, Aberration.None,
                TimeSpan.FromTicks(ticks)));
        Assert.Equal("stepSize", ex.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            moon.GetEphemeris(OneHour, PlanetsAndMoons.EARTH_BODY, TimeSpan.FromTicks(ticks)));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-600000000L)]
    public void Site_StepNotPositive_Throws(long ticks)
    {
        var earth = PlanetsAndMoons.EARTH_BODY;
        var site = new Site(100, "STEPSITE", earth,
            new Planetodetic(longitude: -117.0 * IO.Astrodynamics.Constants.Deg2Rad,
                latitude: 34.0 * IO.Astrodynamics.Constants.Deg2Rad, altitude: 1000.0));
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            site.GetEphemeris(OneHour, earth, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromTicks(ticks)));
        Assert.Equal("stepSize", ex.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => site.GetEphemeris(OneHour, earth, TimeSpan.FromTicks(ticks)));
    }

    /// <summary>
    /// <see cref="SpiceAPI.TransformFrame(Window, Frames.Frame, Frames.Frame, TimeSpan)"/> checks its step at the call, not
    /// at the enumeration (decision 10 of lot L4 of #668): before 11.0.0 a zero step enumerated without end.
    /// </summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(-600000000L)]
    public void TransformFrame_StepNotPositive_ThrowsAtTheCall(long ticks)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            SpiceAPI.Instance.TransformFrame(OneHour, Frames.Frame.ICRF, Frames.Frame.ITRF93, TimeSpan.FromTicks(ticks)));
        Assert.Equal("stepSize", ex.ParamName);
    }

    /// <summary>
    /// A positive step still reads start + i·step while before the end (#781 tracks the end dropped on the grid, L6).
    /// </summary>
    [Fact]
    public void TransformFrame_PositiveStep_ReadsTheGrid()
    {
        var orientations = SpiceAPI.Instance.TransformFrame(OneHour, Frames.Frame.ICRF, Frames.Frame.ITRF93,
            TimeSpan.FromMinutes(20.0)).ToArray();
        Assert.Equal(3, orientations.Length);
        LabelAssert.Equal(Start.AddMinutes(40.0), orientations[^1].Epoch);
    }
}
