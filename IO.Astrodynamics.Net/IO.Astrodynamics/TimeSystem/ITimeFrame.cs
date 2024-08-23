namespace IO.Astrodynamics.TimeSystem;

public interface ITimeFrame
{
    public string Name { get; }
    Time ConvertToTAI(Time timeWithReference);
    Time ConvertFromTAI(Time taiTimeWithReference);
}
