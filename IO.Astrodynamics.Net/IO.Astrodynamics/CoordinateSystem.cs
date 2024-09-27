// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics;

public enum CoordinateSystem
{
    [Description("RECTANGULAR")] Rectangular,
    [Description("RA/DEC")] RaDec,
    [Description("GEODETIC")] Geodetic,
    [Description("PLANETOGRAPHIC")] Planetographic
}