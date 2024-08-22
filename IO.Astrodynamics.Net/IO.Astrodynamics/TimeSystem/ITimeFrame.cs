namespace IO.Astrodynamics.TimeSystem;

public interface ITimeFrame
{
    Time ConvertToTAI(Time timeWithReference);
    Time ConvertFromTAI(Time taiTimeWithReference);
}
