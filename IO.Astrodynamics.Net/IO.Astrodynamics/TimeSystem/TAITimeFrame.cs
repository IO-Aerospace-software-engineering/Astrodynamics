namespace IO.Astrodynamics.TimeSystem;

public class TAITimeFrame : TimeFrame
{
    internal TAITimeFrame()
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