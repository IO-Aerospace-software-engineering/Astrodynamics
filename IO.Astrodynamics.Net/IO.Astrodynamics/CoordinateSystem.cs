// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics;

public enum CoordinateSystem
{
    [Description("RECTANGULAR")] Rectangular,
    [Description("RA/DEC")] RaDec,
    [Description("GEODETIC")] Geodetic,
    /// <summary>
    /// CSPICE's PLANETOGRAPHIC (<c>recpgr_c</c>): the longitude in [0, 2π), positive west for a prograde body other than
    /// the Earth, the Moon and the Sun, positive east otherwise; the geodetic latitude and the altitude above the spheroid.
    /// It has no radius (#682).
    /// </summary>
    [Description("PLANETOGRAPHIC")] Planetographic
}