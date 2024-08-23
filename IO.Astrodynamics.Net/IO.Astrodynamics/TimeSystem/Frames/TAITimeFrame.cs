namespace IO.Astrodynamics.TimeSystem.Frames;

public class TAITimeFrame : TimeFrame
{
    internal TAITimeFrame(): base("TAI")
    {
        
    }
    public override Time ConvertToTAI(Time timeWithReference)
    {
        return timeWithReference;
    }

    public override Time ConvertFromTAI(Time taiTimeWithReference)
    {
        return taiTimeWithReference;
    }
}