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
