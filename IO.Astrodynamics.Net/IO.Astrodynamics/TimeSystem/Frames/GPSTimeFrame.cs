using System;

namespace IO.Astrodynamics.TimeSystem.Frames;

public class GPSTimeFrame : TimeFrame
{
    internal GPSTimeFrame() : base("GPS")
    {
    }

    /// <summary>
    /// TAI − GPS, 19 s, in ticks.
    /// </summary>
    private static readonly long TaiMinusGpsTicks = TimeSpan.FromSeconds(19.0).Ticks;

    public override Time ConvertToTAI(Time time)
    {
        return FromTaiTicks(TaiTicks(time));
    }

    internal override long TaiTicks(in Time time)
    {
        return time.DateTime.Ticks + TaiMinusGpsTicks;
    }

    public override Time ConvertFromTAI(Time time)
    {
        return new TimeSystem.Time(time.Add(TimeSpan.FromSeconds(-19.0)).DateTime, this);
    }
}