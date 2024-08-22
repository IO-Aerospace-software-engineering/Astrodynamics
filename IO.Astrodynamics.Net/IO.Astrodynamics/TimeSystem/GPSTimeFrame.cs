using System;

namespace IO.Astrodynamics.TimeSystem;

public class GPSTimeFrame : TimeFrame
{
    internal GPSTimeFrame()
    {
        
    }
    public override Time ConvertToTAI(Time time)
    {
        return new Time(time.Add(TimeSpan.FromSeconds(-19.0)).DateTime, TAIFrame);
    }

    public override Time ConvertFromTAI(Time time)
    {
        return new Time(time.Add(TimeSpan.FromSeconds(19.0)).DateTime, this);
    }
}