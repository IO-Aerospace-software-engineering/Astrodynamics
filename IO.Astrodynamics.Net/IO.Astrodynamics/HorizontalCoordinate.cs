// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics;

/// <summary>
/// A horizontal coordinate of a target seen from a site, in the site's topocentric frame (Z the zenith, X the north),
/// as <c>Site.GetHorizontalCoordinates</c> gives it (R15 of #668, lot L6, choice 3, decided by S. Guillet, 2026-11-19).
/// </summary>
public enum HorizontalCoordinate
{
    /// <summary>The azimuth, rad, in [0, 2π), from the north toward the east.</summary>
    Azimuth,

    /// <summary>The elevation above the horizon, rad, in [−π/2, π/2].</summary>
    Elevation,

    /// <summary>The range, m.</summary>
    Range
}
