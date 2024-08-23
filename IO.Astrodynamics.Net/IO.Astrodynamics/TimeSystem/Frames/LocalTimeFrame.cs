using System;

namespace IO.Astrodynamics.TimeSystem.Frames;

public class LocalTimeFrame : TimeFrame
{
    internal LocalTimeFrame() : base(string.Empty)
    {
    }

    public override Time ConvertToTAI(Time time)
    {
        var utcEpoch = time.DateTime.ToUniversalTime();
        var leaps = this.LeapSecondsFrom(time);
        return new TimeSystem.Time(utcEpoch + leaps, TAIFrame);
    }

    public override Time ConvertFromTAI(Time time)
    {
        var leaps = this.LeapSecondsFrom(time);
        var epoch = DateTime.SpecifyKind(time.DateTime - leaps, DateTimeKind.Utc).ToLocalTime();
        return new TimeSystem.Time(epoch, UTCFrame);
    }
}