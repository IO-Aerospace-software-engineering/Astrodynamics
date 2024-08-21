using System;

namespace IO.Astrodynamics.Time;

public class GPSDateTime : DateTimeBase
{
    public GPSDateTime(DateTime dateTime) : base(dateTime, DateTimeKind.GPS)
    {
    }

    public override DateTimeBase ToTAI()
    {
        return new TAIDateTime(DateTime - TimeSpan.FromSeconds(19)); 
    }
}