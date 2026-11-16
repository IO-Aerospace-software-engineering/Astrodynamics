// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Body;

namespace IO.Astrodynamics.SolarSystemObjects;

public static class Stars
{
    public static readonly NaifObject Sun = new(10, "SUN", "IAU_SUN");
    public static readonly CelestialBody SUN_BODY;
    
    static Stars()
    {
        // No provider data read here: at the first read of a member that needs them, retried on failure (second round of
        // #668)
        SUN_BODY = CelestialBody.CreateDeferred(Sun);
    }
}