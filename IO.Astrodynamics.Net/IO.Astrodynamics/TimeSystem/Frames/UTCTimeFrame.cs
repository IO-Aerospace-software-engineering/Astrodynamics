namespace IO.Astrodynamics.TimeSystem.Frames;

public class UTCTimeFrame : TimeFrame
{
    internal UTCTimeFrame() : base("UTC")
    {
    }

    public override Time ConvertToTAI(Time time)
    {
        var leaps = this.LeapSecondsFrom(time);
        return new Time(time.DateTime + leaps, TAIFrame);
    }

    public override Time ConvertFromTAI(Time time)
    {
        var leaps = this.LeapSecondsFrom(time);
        return new Time(time.DateTime - leaps, UTCFrame);
    }
}