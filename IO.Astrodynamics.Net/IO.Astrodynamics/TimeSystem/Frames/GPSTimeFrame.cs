using System;

namespace IO.Astrodynamics.TimeSystem.Frames;

public class GPSTimeFrame : TimeFrame
{
    internal GPSTimeFrame() : base("GPS")
    {
    }

    public override Time ConvertToTAI(Time time)
    {
        return new TimeSystem.Time(time.Add(TimeSpan.FromSeconds(19.0)).DateTime, TAIFrame);
    }

    public override Time ConvertFromTAI(Time time)
    {
        return new TimeSystem.Time(time.Add(TimeSpan.FromSeconds(-19.0)).DateTime, this);
    }
}