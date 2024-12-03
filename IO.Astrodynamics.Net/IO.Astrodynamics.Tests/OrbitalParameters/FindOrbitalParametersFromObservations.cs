using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

public class FindOrbitalParametersFromObservations
{
    public FindOrbitalParametersFromObservations()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void GeosynchronousObject()
    {
        var timespan = TimeSpan.FromMinutes(15.0);
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
            Astrodynamics.OrbitalParameters.OrbitalParameters.CreateFromObservation_Gauss(obs1.ToEquatorial(), obs2.ToEquatorial(), obs3.ToEquatorial(), site,
                PlanetsAndMoons.EARTH_BODY, 42000000.0);
        Console.WriteLine($"Expected position : {referenceOrbit.ToStateVector().Position}");
        Console.WriteLine($"Computed position : {orbitalParams.ToStateVector().Position}");
        Console.WriteLine($"Delta : {orbitalParams.ToStateVector().Position-referenceOrbit.ToStateVector().Position}");
        Console.WriteLine($"Delta range in percent = {100.0*(orbitalParams.ToStateVector().Position-referenceOrbit.ToStateVector().Position).Magnitude()/referenceOrbit.ToStateVector().Position.Magnitude()} %");
    }
}