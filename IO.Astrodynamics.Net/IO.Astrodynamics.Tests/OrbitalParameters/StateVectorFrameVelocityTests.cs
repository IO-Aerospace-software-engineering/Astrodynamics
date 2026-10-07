// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// Velocity of a state carried into or out of a rotating frame by
/// <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.ToFrame"/>, against the states SPICE computes
/// directly in each frame.
/// </summary>
/// <remarks>
/// The velocity transforms as v' = R v - (R ω) × r', with ω the angular velocity given by
/// <see cref="Frames.Frame.ToFrame(Frames.Frame, TimeSystem.Time)"/> in the source frame. The former formula crossed ω
/// with r' without bringing it into the target frame, an error of |ω| |r| times the precession-nutation angle since
/// J2000: 1.1 m/s (2000) and 34.5 m/s (2021) on the geocentric Moon in ITRF93.
/// </remarks>
public class StateVectorFrameVelocityTests
{
    // The measured agreement is a few 1e-16, the rounding of the rotation.
    private const double RelativeTolerance = 1e-12;

    public StateVectorFrameVelocityTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(2021)]
    public void ToFrame_IcrfToItrf93_MatchesSpice(int year)
    {
        // Arrange
        var epoch = new TimeSystem.Time(year, 1, 1, 12, 0, 0);
        var itrf93 = new Frames.Frame(PlanetsAndMoons.EARTH.Frame);
        var inIcrf = GeocentricMoon(epoch, Frames.Frame.ICRF);
        var expected = GeocentricMoon(epoch, itrf93);

        // Act
        var actual = inIcrf.ToFrame(itrf93).ToStateVector();

        // Assert
        AssertClose(expected.Position, actual.Position);
        AssertClose(expected.Velocity, actual.Velocity);
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(2021)]
    public void ToFrame_Itrf93ToIcrf_MatchesSpice(int year)
    {
        // Arrange
        var epoch = new TimeSystem.Time(year, 1, 1, 12, 0, 0);
        var itrf93 = new Frames.Frame(PlanetsAndMoons.EARTH.Frame);
        var inItrf93 = GeocentricMoon(epoch, itrf93);
        var expected = GeocentricMoon(epoch, Frames.Frame.ICRF);

        // Act
        var actual = inItrf93.ToFrame(Frames.Frame.ICRF).ToStateVector();

        // Assert
        AssertClose(expected.Position, actual.Position);
        AssertClose(expected.Velocity, actual.Velocity);
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(2021)]
    public void ToFrame_GroundStationFromItrf93ToIcrf_MatchesSpice(int year)
    {
        // Arrange: DSS-13 from earthstns_itrf93_201023.bsp, nearly fixed in ITRF93, about 380 m/s in ICRF.
        var epoch = new TimeSystem.Time(year, 1, 1, 12, 0, 0);
        var earth = PlanetsAndMoons.EARTH_BODY;
        var itrf93 = new Frames.Frame(PlanetsAndMoons.EARTH.Frame);
        var station = new Site(13, "DSS-13", earth);
        var inItrf93 = station.GetEphemeris(epoch, earth, itrf93, Aberration.None).ToStateVector();
        var expected = station.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();

        // Act
        var actual = inItrf93.ToFrame(Frames.Frame.ICRF).ToStateVector();

        // Assert
        AssertClose(expected.Position, actual.Position);
        AssertClose(expected.Velocity, actual.Velocity);
    }

    [Fact]
    public void ToFrame_RoundTripThroughItrf93_RestoresTheState()
    {
        // Arrange
        var epoch = new TimeSystem.Time(2021, 1, 1, 12, 0, 0);
        var itrf93 = new Frames.Frame(PlanetsAndMoons.EARTH.Frame);
        var state = GeocentricMoon(epoch, Frames.Frame.ICRF);

        // Act
        var roundTrip = state.ToFrame(itrf93).ToFrame(Frames.Frame.ICRF).ToStateVector();

        // Assert
        AssertClose(state.Position, roundTrip.Position);
        AssertClose(state.Velocity, roundTrip.Velocity);
    }

    private static StateVector GeocentricMoon(TimeSystem.Time epoch, Frames.Frame frame)
    {
        return PlanetsAndMoons.MOON_BODY.GetEphemeris(epoch, PlanetsAndMoons.EARTH_BODY, frame, Aberration.None).ToStateVector();
    }

    private static void AssertClose(Vector3 expected, Vector3 actual)
    {
        double error = (actual - expected).Magnitude();
        Assert.True(error <= RelativeTolerance * expected.Magnitude(),
            $"|actual - expected| = {error:E3}, |expected| = {expected.Magnitude():E6}");
    }
}
