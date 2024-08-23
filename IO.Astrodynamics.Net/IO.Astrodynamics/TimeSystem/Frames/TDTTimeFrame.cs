using System;

namespace IO.Astrodynamics.TimeSystem.Frames;

public class TDTTimeFrame : TimeFrame
{
    internal TDTTimeFrame() : base("TDT")
    {
    }

    public override Time ConvertToTAI(Time time)
    {
        return new TimeSystem.Time(time.Add(TimeSpan.FromSeconds(-32.184)).DateTime, TAIFrame);
    }

    public override Time ConvertFromTAI(Time time)
    {
        return new TimeSystem.Time(time.Add(TimeSpan.FromSeconds(32.184)).DateTime, this);
    }
}