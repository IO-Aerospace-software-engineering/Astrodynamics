// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Frames;

/// <summary>
/// Default Earth orientation provider: UT1 - UTC = 0 and no polar motion.
/// </summary>
/// <remarks>
/// Without EOP, <see cref="TirsFrame"/> is off by the actual UT1 - UTC, which the IERS keeps within 0.9 s:
/// up to about 13.5 arcseconds of Earth rotation angle, about 420 m for a point fixed on the equator.
/// Polar motion (up to about 0.5 arcsecond, some 15 m on the ground) is not applied by any frame of the
/// library, whatever the provider, because the IAU chain stops at TIRS. SPICE body-fixed frames such as
/// ITRF93 with <c>earth_latest_high_prec.bpc</c> include the EOP and are not affected.
/// </remarks>
public sealed class NullEop : IEarthOrientationParameters
{
    public static readonly NullEop Instance = new();

    private NullEop() { }

    public double GetDeltaUT1(Time utcEpoch) => 0.0;
    public double GetXp(Time utcEpoch) => 0.0;
    public double GetYp(Time utcEpoch) => 0.0;
}
