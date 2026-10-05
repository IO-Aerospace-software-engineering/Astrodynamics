// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Frames;

/// <summary>
/// Provides Earth Orientation Parameters (EOP) for precise frame transformations.
/// </summary>
public interface IEarthOrientationParameters
{
    /// <summary>
    /// Returns UT1-UTC in seconds at the given UTC epoch.
    /// </summary>
    double GetDeltaUT1(Time utcEpoch);

    /// <summary>
    /// Returns polar motion x_p in radians at the given UTC epoch.
    /// </summary>
    /// <remarks>Not used by the library frames yet: the IAU chain stops at TIRS, there is no ITRS frame.</remarks>
    double GetXp(Time utcEpoch);

    /// <summary>
    /// Returns polar motion y_p in radians at the given UTC epoch.
    /// </summary>
    /// <remarks>Not used by the library frames yet: the IAU chain stops at TIRS, there is no ITRS frame.</remarks>
    double GetYp(Time utcEpoch);
}
