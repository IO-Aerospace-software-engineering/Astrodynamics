// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;

namespace IO.Astrodynamics.SolarSystemObjects;

public static class Barycenters
{
    public static Barycenter SOLAR_SYSTEM_BARYCENTER;
    public static Barycenter MERCURY_BARYCENTER;
    public static Barycenter VENUS_BARYCENTER;
    public static Barycenter EARTH_BARYCENTER;
    public static Barycenter MARS_BARYCENTER;
    public static Barycenter JUPITER_BARYCENTER;
    public static Barycenter SATURN_BARYCENTER;
    public static Barycenter URANUS_BARYCENTER;
    public static Barycenter NEPTUNE_BARYCENTER;
    public static Barycenter PLUTO_BARYCENTER;

    static Barycenters()
    {
        SOLAR_SYSTEM_BARYCENTER = new Barycenter(0);
        MERCURY_BARYCENTER = new Barycenter(1);
        VENUS_BARYCENTER = new Barycenter(2);
        EARTH_BARYCENTER = new Barycenter(3);
        MARS_BARYCENTER = new Barycenter(4);
        JUPITER_BARYCENTER = new Barycenter(5);
        SATURN_BARYCENTER = new Barycenter(6);
        URANUS_BARYCENTER = new Barycenter(7);
        NEPTUNE_BARYCENTER = new Barycenter(8);
        PLUTO_BARYCENTER = new Barycenter(9);
    }
}