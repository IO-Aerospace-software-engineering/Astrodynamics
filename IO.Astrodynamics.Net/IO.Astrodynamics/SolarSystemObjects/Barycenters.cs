// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;

namespace IO.Astrodynamics.SolarSystemObjects;

public static class Barycenters
{
    public static readonly Barycenter SolarSystemBarycenter;
    public static readonly Barycenter MercuryBarycenter;
    public static readonly Barycenter VenusBarycenter;
    public static readonly Barycenter EarthBarycenter;
    public static readonly Barycenter MarsBarycenter;
    public static readonly Barycenter JupiterBarycenter;
    public static readonly Barycenter SaturnBarycenter;
    public static readonly Barycenter UranusBarycenter;
    public static readonly Barycenter NeptuneBarycenter;
    public static readonly Barycenter PlutoBarycenter;

    // The names SPICE gives the barycentres 0 to 9 (NAIF Integer ID codes, bodc2n): their objects read no provider data
    // here, but at the first read of a member that needs them, retried on failure, so that a provider without their data
    // does not leave the type unusable for the life of the process (second round of #668)
    static Barycenters()
    {
        SolarSystemBarycenter = Barycenter.CreateDeferred(0, "SOLAR SYSTEM BARYCENTER");
        MercuryBarycenter = Barycenter.CreateDeferred(1, "MERCURY BARYCENTER");
        VenusBarycenter = Barycenter.CreateDeferred(2, "VENUS BARYCENTER");
        EarthBarycenter = Barycenter.CreateDeferred(3, "EARTH BARYCENTER");
        MarsBarycenter = Barycenter.CreateDeferred(4, "MARS BARYCENTER");
        JupiterBarycenter = Barycenter.CreateDeferred(5, "JUPITER BARYCENTER");
        SaturnBarycenter = Barycenter.CreateDeferred(6, "SATURN BARYCENTER");
        UranusBarycenter = Barycenter.CreateDeferred(7, "URANUS BARYCENTER");
        NeptuneBarycenter = Barycenter.CreateDeferred(8, "NEPTUNE BARYCENTER");
        PlutoBarycenter = Barycenter.CreateDeferred(9, "PLUTO BARYCENTER");
    }
}