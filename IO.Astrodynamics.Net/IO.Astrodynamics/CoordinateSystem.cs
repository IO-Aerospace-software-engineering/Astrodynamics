// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics;

public enum CoordinateSystem
{
    [Description("RECTANGULAR")] Rectangular,
    [Description("LATITUDINAL")] Latitudinal,
    [Description("RA/DEC")] RaDec,
    [Description("SPHERICAL")] Spherical,
    [Description("CYLINDRICAL")] Cylindrical,
    [Description("GEODETIC")] Geodetic,
    [Description("PLANETOGRAPHIC")] Planetographic
}