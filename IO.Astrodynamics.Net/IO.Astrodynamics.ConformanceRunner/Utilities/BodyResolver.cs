using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.SolarSystemObjects;

namespace IO.Astrodynamics.ConformanceRunner.Utilities;

public static class BodyResolver
{
    public static CelestialBody Resolve(string name)
    {
        return name?.ToUpperInvariant() switch
        {
            "SUN" => new CelestialBody(Stars.Sun),
            "MERCURY" => new CelestialBody(PlanetsAndMoons.Mercury),
            "VENUS" => new CelestialBody(PlanetsAndMoons.Venus),
            "EARTH" => new CelestialBody(PlanetsAndMoons.Earth),
            "MOON" => new CelestialBody(PlanetsAndMoons.Moon),
            "MARS" => new CelestialBody(PlanetsAndMoons.Mars),
            "JUPITER" => new CelestialBody(PlanetsAndMoons.Jupiter),
            "SATURN" => new CelestialBody(PlanetsAndMoons.Saturn),
            _ => throw new ArgumentException($"Unknown body: {name}")
        };
    }

    public static CelestialItem ResolveCelestialItem(string name)
    {
        return name?.ToUpperInvariant() switch
        {
            "SUN" => Stars.SunBody,
            "MOON" => PlanetsAndMoons.MoonBody,
            "MERCURY" => new CelestialBody(PlanetsAndMoons.Mercury),
            "VENUS" => new CelestialBody(PlanetsAndMoons.Venus),
            "EARTH" => new CelestialBody(PlanetsAndMoons.Earth),
            "MARS" => new CelestialBody(PlanetsAndMoons.Mars),
            "JUPITER" => new CelestialBody(PlanetsAndMoons.Jupiter),
            "SATURN" => new CelestialBody(PlanetsAndMoons.Saturn),
            "MERCURY BARYCENTER" => Barycenters.MercuryBarycenter,
            "VENUS BARYCENTER" => Barycenters.VenusBarycenter,
            "EARTH BARYCENTER" => Barycenters.EarthBarycenter,
            "MARS BARYCENTER" => Barycenters.MarsBarycenter,
            "JUPITER BARYCENTER" => Barycenters.JupiterBarycenter,
            "SATURN BARYCENTER" => Barycenters.SaturnBarycenter,
            "URANUS BARYCENTER" => Barycenters.UranusBarycenter,
            "NEPTUNE BARYCENTER" => Barycenters.NeptuneBarycenter,
            "PLUTO BARYCENTER" => Barycenters.PlutoBarycenter,
            "SOLAR SYSTEM BARYCENTER" => Barycenters.SolarSystemBarycenter,
            _ => throw new ArgumentException($"Unknown body: {name}")
        };
    }

    public static NaifObject ResolveNaif(string name)
    {
        return name?.ToUpperInvariant() switch
        {
            "SUN" => Stars.Sun,
            "MERCURY" => PlanetsAndMoons.Mercury,
            "VENUS" => PlanetsAndMoons.Venus,
            "EARTH" => PlanetsAndMoons.Earth,
            "MOON" => PlanetsAndMoons.Moon,
            "MARS" => PlanetsAndMoons.Mars,
            "JUPITER" => PlanetsAndMoons.Jupiter,
            "SATURN" => PlanetsAndMoons.Saturn,
            _ => throw new ArgumentException($"Unknown body: {name}")
        };
    }
}
