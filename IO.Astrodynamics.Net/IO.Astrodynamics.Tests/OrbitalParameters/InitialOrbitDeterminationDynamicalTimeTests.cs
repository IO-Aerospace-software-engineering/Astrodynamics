// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// The intervals of the Gauss method are the dynamical time of the centre of motion between the observations, TT about
/// the Earth, whatever the tags of their epochs, the leap seconds between UTC epochs included (#508). Before, three
/// UTC observations across the leap second of 2016-12-31 had intervals 1 s short, and gave another orbit than the same
/// observations tagged TAI.
/// </summary>
public class InitialOrbitDeterminationDynamicalTimeTests
{
    private const double Deg = IO.Astrodynamics.Constants.Deg2Rad;

    public InitialOrbitDeterminationDynamicalTimeTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    [Fact]
    public void ObservationsAcrossTheLeapSecond_GiveTheSameOrbitInUtcTaiAndTt()
    {
        // Arrange: a geosynchronous object seen from a station 2 min before, at and 2 min after 2017-01-01T00:00:30Z
        var site = new Site(80, "MyStation", TestHelpers.EarthAtJ2000, new Planetodetic(0.0, 45.0 * Deg, 0.0));
        var e2 = new TimeSystem.Time(2017, 1, 1, 0, 0, 30, frame: TimeFrame.UTCFrame);
        var epochs = new[] { e2.AddMinutes(-2.0), e2, e2.AddMinutes(2.0) };
        var orbit = new KeplerianElements(42164000.0, 0.0, 15.0 * Deg, 45.0 * Deg, 0.0, 45.0 * Deg,
            TestHelpers.EarthAtJ2000, e2, Frames.Frame.ICRF);
        var observations = Array.ConvertAll(epochs, e => orbit.AtEpoch(e).RelativeTo(site, Aberration.LT).ToEquatorial());

        // Act
        var utc = Determine(observations, site, e => e);
        var tai = Determine(observations, site, e => e.ToTAI());
        var tt = Determine(observations, site, e => e.ToTDT());

        // Assert: the intervals are the same TT seconds in the three frames, 120 s and 121 s
        Assert.Equal(tai.Position, utc.Position);
        Assert.Equal(tai.Velocity, utc.Velocity);
        Assert.Equal(tai.Position, tt.Position);
        Assert.Equal(tai.Velocity, tt.Velocity);
    }

    private static StateVector Determine(Equatorial[] observations, Site site, Func<TimeSystem.Time, TimeSystem.Time> tag)
    {
        var tagged = Array.ConvertAll(observations,
            o => new Equatorial(o.Declination, o.RightAscension, o.Distance, tag(o.Epoch)));
        return InitialOrbitDetermination.CreateFromObservation_Gauss(tagged[0], tagged[1], tagged[2], site,
            PlanetsAndMoons.EARTH_BODY, 42000000.0).OrbitalParameters.ToStateVector();
    }
}
