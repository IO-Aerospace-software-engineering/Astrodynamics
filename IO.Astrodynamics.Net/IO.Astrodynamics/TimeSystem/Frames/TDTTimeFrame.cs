using System;

namespace IO.Astrodynamics.TimeSystem.Frames;

public class TDTTimeFrame : TimeFrame
{
    internal TDTTimeFrame()
    {
        
    }
    public override Time ConvertToTAI(Time time)
    {
        return new Time(time.Add(TimeSpan.FromSeconds(-32.184)).DateTime, TAIFrame);
    }

    public override Time ConvertFromTAI(Time time)
    {
        return new Time(time.Add(TimeSpan.FromSeconds(32.184)).DateTime, this);
    }
}