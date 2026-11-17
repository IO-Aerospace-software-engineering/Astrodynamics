using System;

namespace IO.Astrodynamics.TimeSystem.Frames;

public class UTCTimeFrame : TimeFrame
{
    internal UTCTimeFrame() : base("UTC")
    {
    }

    public override Time ConvertToTAI(Time time)
    {
        return FromTaiTicks(TaiTicks(time));
    }

    internal override long TaiTicks(in Time time)
    {
        return time.DateTime.Ticks + LeapSecondsAtUtc(time.DateTime).Ticks;
    }

    public override Time ConvertFromTAI(Time time)
    {
        var leaps = LeapSecondsAtTai(time.DateTime);
        return new TimeSystem.Time(time.DateTime - leaps, UTCFrame);
    }

    public override string ToString()
    {
        return string.Empty;
    }
}