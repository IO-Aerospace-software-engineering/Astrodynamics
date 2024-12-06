using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

public class InitialOrbitDeterminationTests
{
    public InitialOrbitDeterminationTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void GeosynchronousObject()
    {
        for (int i = 1; i <= 5; i++)
        {
            var timespan = TimeSpan.FromMinutes(i * 2);
            Site site = new Site(80, "MyStation", TestHelpers.EarthAtJ2000, new Planetodetic(0.0, 45.0 * Constants.DEG_RAD, 0.0));

            var e2 = new TimeSystem.Time(2024, 1, 2);

            var e1 = e2 - timespan;
            var e3 = e2 + timespan;
            var referenceOrbit = new KeplerianElements(
                semiMajorAxis: 42164000.0, // m
                eccentricity: 0.0,
                inclination: 15.0 * Constants.DEG_RAD,
                rigthAscendingNode: 45.0 * Constants.DEG_RAD,
                argumentOfPeriapsis: 0.0,
                meanAnomaly: 45.0 * Constants.DEG_RAD, // Décalage de 45 degrés
                observer: TestHelpers.EarthAtJ2000,
                frame: Frames.Frame.ICRF,
                epoch: e2
            );
            var obs1 = referenceOrbit.AtEpoch(e1).RelativeTo(site, Aberration.LT);
            var obs2 = referenceOrbit.AtEpoch(e2).RelativeTo(site, Aberration.LT);
            var obs3 = referenceOrbit.AtEpoch(e3).RelativeTo(site, Aberration.LT);
            var orbitalParams =
                InitialOrbitDetermination.CreateFromObservation_Gauss(obs1.ToEquatorial(), obs2.ToEquatorial(), obs3.ToEquatorial(), site,
                    PlanetsAndMoons.EARTH_BODY, 42000000.0);
            Console.WriteLine($@"Test N°{i}");
            Console.WriteLine($@"Time span between two observations : {timespan}");
            Console.WriteLine($@"Expected position : {referenceOrbit.ToStateVector().Position}");
            Console.WriteLine($@"Computed position : {orbitalParams.ToStateVector().Position}");
            Console.WriteLine($@"Delta : {orbitalParams.ToStateVector().Position - referenceOrbit.ToStateVector().Position}");
            var deltaRange = 100.0 * (orbitalParams.ToStateVector().Position - referenceOrbit.ToStateVector().Position).Magnitude() /
                             referenceOrbit.ToStateVector().Position.Magnitude();
            Console.WriteLine(
                $@"Delta range in percent = {deltaRange} %");
            Console.WriteLine($@"Delta range in meters = {(orbitalParams.ToStateVector().Position - referenceOrbit.ToStateVector().Position).Magnitude()} m");
            Console.WriteLine($@"Expected keplerian elements : {referenceOrbit.ToString()}");
            Console.WriteLine($@"computed keplerian elements : {orbitalParams.ToKeplerianElements().ToString()}");
            Console.WriteLine($@"Expected velocity : {referenceOrbit.ToStateVector().Velocity}");
            Console.WriteLine($@"Computed velocity : {orbitalParams.ToStateVector().Velocity}");
            var deltaVelocity = 100.0 * (orbitalParams.ToStateVector().Velocity - referenceOrbit.ToStateVector().Velocity).Magnitude() /
                                referenceOrbit.ToStateVector().Velocity.Magnitude();
            Console.WriteLine(
                $@"Delta velocity in percent = {deltaVelocity} %");
            Assert.True(deltaRange < 0.02);
            Assert.True(deltaVelocity < 0.02);
        }
    }

    [Fact]
    public void PlanetObject()
    {
        for (int i = 0; i <= 12; i++)
        {
            var timespan = TimeSpan.FromDays(10);
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);

            //var site = TestHelpers.EarthAtJ2000;
            var e2 = new TimeSystem.Time(2023, 1, 1).AddMonths(i);
            Console.WriteLine($@"Test at epoch {e2}");
            Console.WriteLine($@"Time span between two observations : {timespan} days");
            var e1 = e2 - timespan;
            var e3 = e2 + timespan;
            var target = new Barycenter(4);
            var obs1 = target.GetEphemeris(e1, site, Frames.Frame.ICRF, Aberration.LT);
            var obs2 = target.GetEphemeris(e2, site, Frames.Frame.ICRF, Aberration.LT);
            var obs3 = target.GetEphemeris(e3, site, Frames.Frame.ICRF, Aberration.LT);
            var referenceOrbit = target.GetEphemeris(e2, TestHelpers.Sun, Frames.Frame.ICRF, Aberration.LT);
            var orbitalParams =
                InitialOrbitDetermination.CreateFromObservation_Gauss(obs1.ToEquatorial(), obs2.ToEquatorial(), obs3.ToEquatorial(), site,
                    Stars.SUN_BODY, 350000000000.58063);
            
            // Console.WriteLine($@"Expected position : {referenceOrbit.ToStateVector().Position}");
            // Console.WriteLine($@"Computed position : {orbitalParams.ToStateVector().Position}");
            // Console.WriteLine($@"Delta : {orbitalParams.ToStateVector().Position - referenceOrbit.ToStateVector().Position}");
            var deltaRange = 100.0 * (orbitalParams.ToStateVector().Position - referenceOrbit.ToStateVector().Position).Magnitude() /
                             referenceOrbit.ToStateVector().Position.Magnitude();
            Console.WriteLine(
                $@"Delta range in percent = {deltaRange} %");
            // Console.WriteLine($@"Delta range in meters = {(orbitalParams.ToStateVector().Position - referenceOrbit.ToStateVector().Position).Magnitude()} m");
            // Console.WriteLine($@"Expected keplerian elements : {referenceOrbit.ToString()}");
            // Console.WriteLine($@"computed keplerian elements : {orbitalParams.ToKeplerianElements().ToString()}");
            // Console.WriteLine($@"Expected velocity : {referenceOrbit.ToStateVector().Velocity}");
            // Console.WriteLine($@"Computed velocity : {orbitalParams.ToStateVector().Velocity}");
            var deltaVelocity = 100.0 * (orbitalParams.ToStateVector().Velocity - referenceOrbit.ToStateVector().Velocity).Magnitude() /
                                referenceOrbit.ToStateVector().Velocity.Magnitude();
            Console.WriteLine(
                $@"Delta velocity in percent = {deltaVelocity} %");
            Console.WriteLine(Environment.NewLine);
            //Assert.True(deltaRange < 0.02);
            //Assert.True(deltaVelocity < 0.02);
        }
    }

    [Fact]
    public void COBE()
    {
        var ut1 = new TimeSystem.Time(2000, 11, 6, 22, 31, 29, 0, 0, TimeSystem.TimeFrame.UTCFrame);
        var ut2 = new TimeSystem.Time(2000, 11, 6, 22, 34, 30, 0, 0, TimeSystem.TimeFrame.UTCFrame);
        var ut3 = new TimeSystem.Time(2000, 11, 6, 22, 37, 30, 0, 0, TimeSystem.TimeFrame.UTCFrame);
        var site = new Site(99, "Maryland university", TestHelpers.EarthAtJ2000, new Planetodetic(-76.95667 * Constants.DEG_RAD, 39.00167 * Constants.DEG_RAD, 53.0));
        var obs1 = new Equatorial(-16.3 * Astrodynamics.Constants.Deg2Rad, 327.0 * Astrodynamics.Constants.Deg2Rad, 7250000.0, ut1);
        var obs2 = new Equatorial(46.9 * Astrodynamics.Constants.Deg2Rad, 318.5 * Astrodynamics.Constants.Deg2Rad, 7250000.0, ut2);
        var obs3 = new Equatorial(76.1 * Astrodynamics.Constants.Deg2Rad, 165.75 * Astrodynamics.Constants.Deg2Rad, 7250000.0, ut3);
        var orbitalParams =
            InitialOrbitDetermination.CreateFromObservation_Gauss(obs1, obs2, obs3, site, PlanetsAndMoons.EARTH_BODY, 7_250_000.0);
        StateVector referenceOrbit = new(new Vector3(3520477.0118993367, -4310404.2221997585, 4645118.5764311915),
            new Vector3(-4026.2486947722973, 2615.67401249017, 5468.113004203227),
            TestHelpers.EarthAtJ2000, ut2, Frames.Frame.ICRF);
        Assert.Equal(referenceOrbit, orbitalParams.ToStateVector(), TestHelpers.StateVectorComparer);
        var deltaRange = 100.0 * (orbitalParams.ToStateVector().Position - referenceOrbit.ToStateVector().Position).Magnitude() /
                         referenceOrbit.ToStateVector().Position.Magnitude();
    }
}