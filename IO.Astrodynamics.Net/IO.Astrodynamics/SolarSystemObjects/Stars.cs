// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Body;

namespace IO.Astrodynamics.SolarSystemObjects;

public static class Stars
{
    public static readonly NaifObject Sun = new(10, "SUN", "IAU_SUN");
    public static readonly CelestialBody SUN_BODY;
    
    static Stars()
    {
        SUN_BODY = new CelestialBody(Sun);
    }
}