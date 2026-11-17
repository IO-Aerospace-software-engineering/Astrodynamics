using System;

namespace IO.Astrodynamics.TimeSystem.Frames;

public class TDTTimeFrame : TimeFrame
{
    internal TDTTimeFrame() : base("TDT")
    {
    }

    /// <summary>
    /// TT − TAI, 32.184 s, in ticks.
    /// </summary>
    internal static readonly long TtMinusTaiTicks = TimeSpan.FromSeconds(32.184).Ticks;

    public override Time ConvertToTAI(Time time)
    {
        return FromTaiTicks(TaiTicks(time));
    }

    internal override long TaiTicks(in Time time)
    {
        return time.DateTime.Ticks - TtMinusTaiTicks;
    }

    public override Time ConvertFromTAI(Time time)
    {
        return new TimeSystem.Time(time.Add(TimeSpan.FromSeconds(32.184)).DateTime, this);
    }
}