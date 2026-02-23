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
            "MERCURY" => new CelestialBody(PlanetsAndMoons.MERCURY),
            "VENUS" => new CelestialBody(PlanetsAndMoons.VENUS),
            "EARTH" => new CelestialBody(PlanetsAndMoons.EARTH),
            "MOON" => new CelestialBody(PlanetsAndMoons.MOON),
            "MARS" => new CelestialBody(PlanetsAndMoons.MARS),
            "JUPITER" => new CelestialBody(PlanetsAndMoons.JUPITER),
            "SATURN" => new CelestialBody(PlanetsAndMoons.SATURN),
            _ => throw new ArgumentException($"Unknown body: {name}")
        };
    }

    public static CelestialItem ResolveCelestialItem(string name)
    {
        return name?.ToUpperInvariant() switch
        {
            "SUN" => Stars.SUN_BODY,
            "MOON" => PlanetsAndMoons.MOON_BODY,
            "MERCURY" => new CelestialBody(PlanetsAndMoons.MERCURY),
            "VENUS" => new CelestialBody(PlanetsAndMoons.VENUS),
            "EARTH" => new CelestialBody(PlanetsAndMoons.EARTH),
            "MARS" => new CelestialBody(PlanetsAndMoons.MARS),
            "JUPITER" => new CelestialBody(PlanetsAndMoons.JUPITER),
            "SATURN" => new CelestialBody(PlanetsAndMoons.SATURN),
            "MERCURY BARYCENTER" => Barycenters.MERCURY_BARYCENTER,
            "VENUS BARYCENTER" => Barycenters.VENUS_BARYCENTER,
            "EARTH BARYCENTER" => Barycenters.EARTH_BARYCENTER,
            "MARS BARYCENTER" => Barycenters.MARS_BARYCENTER,
            "JUPITER BARYCENTER" => Barycenters.JUPITER_BARYCENTER,
            "SATURN BARYCENTER" => Barycenters.SATURN_BARYCENTER,
            "URANUS BARYCENTER" => Barycenters.URANUS_BARYCENTER,
            "NEPTUNE BARYCENTER" => Barycenters.NEPTUNE_BARYCENTER,
            "PLUTO BARYCENTER" => Barycenters.PLUTO_BARYCENTER,
            "SOLAR SYSTEM BARYCENTER" => Barycenters.SOLAR_SYSTEM_BARYCENTER,
            _ => throw new ArgumentException($"Unknown body: {name}")
        };
    }

    public static NaifObject ResolveNaif(string name)
    {
        return name?.ToUpperInvariant() switch
        {
            "SUN" => Stars.Sun,
            "MERCURY" => PlanetsAndMoons.MERCURY,
            "VENUS" => PlanetsAndMoons.VENUS,
            "EARTH" => PlanetsAndMoons.EARTH,
            "MOON" => PlanetsAndMoons.MOON,
            "MARS" => PlanetsAndMoons.MARS,
            "JUPITER" => PlanetsAndMoons.JUPITER,
            "SATURN" => PlanetsAndMoons.SATURN,
            _ => throw new ArgumentException($"Unknown body: {name}")
        };
    }
}
