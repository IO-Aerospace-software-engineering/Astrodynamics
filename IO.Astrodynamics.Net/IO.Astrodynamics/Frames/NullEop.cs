// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Frames;

/// <summary>
/// Default EOP provider that returns zero for all values.
/// Provides sub-arcsecond accuracy without external EOP data.
/// </summary>
public sealed class NullEop : IEarthOrientationParameters
{
    public static readonly NullEop Instance = new();

    private NullEop() { }

    public double GetDeltaUT1(Time utcEpoch) => 0.0;
    public double GetXp(Time utcEpoch) => 0.0;
    public double GetYp(Time utcEpoch) => 0.0;
}
