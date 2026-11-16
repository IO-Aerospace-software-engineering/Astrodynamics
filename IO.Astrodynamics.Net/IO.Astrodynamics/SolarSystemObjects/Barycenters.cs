// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;

namespace IO.Astrodynamics.SolarSystemObjects;

public static class Barycenters
{
    public static readonly Barycenter SOLAR_SYSTEM_BARYCENTER;
    public static readonly Barycenter MERCURY_BARYCENTER;
    public static readonly Barycenter VENUS_BARYCENTER;
    public static readonly Barycenter EARTH_BARYCENTER;
    public static readonly Barycenter MARS_BARYCENTER;
    public static readonly Barycenter JUPITER_BARYCENTER;
    public static readonly Barycenter SATURN_BARYCENTER;
    public static readonly Barycenter URANUS_BARYCENTER;
    public static readonly Barycenter NEPTUNE_BARYCENTER;
    public static readonly Barycenter PLUTO_BARYCENTER;

    // The names SPICE gives the barycentres 0 to 9 (NAIF Integer ID codes, bodc2n): their objects read no provider data
    // here, but at the first read of a member that needs them, retried on failure, so that a provider without their data
    // does not leave the type unusable for the life of the process (second round of #668)
    static Barycenters()
    {
        SOLAR_SYSTEM_BARYCENTER = Barycenter.CreateDeferred(0, "SOLAR SYSTEM BARYCENTER");
        MERCURY_BARYCENTER = Barycenter.CreateDeferred(1, "MERCURY BARYCENTER");
        VENUS_BARYCENTER = Barycenter.CreateDeferred(2, "VENUS BARYCENTER");
        EARTH_BARYCENTER = Barycenter.CreateDeferred(3, "EARTH BARYCENTER");
        MARS_BARYCENTER = Barycenter.CreateDeferred(4, "MARS BARYCENTER");
        JUPITER_BARYCENTER = Barycenter.CreateDeferred(5, "JUPITER BARYCENTER");
        SATURN_BARYCENTER = Barycenter.CreateDeferred(6, "SATURN BARYCENTER");
        URANUS_BARYCENTER = Barycenter.CreateDeferred(7, "URANUS BARYCENTER");
        NEPTUNE_BARYCENTER = Barycenter.CreateDeferred(8, "NEPTUNE BARYCENTER");
        PLUTO_BARYCENTER = Barycenter.CreateDeferred(9, "PLUTO BARYCENTER");
    }
}