using System;

namespace IO.Astrodynamics.Time;

public class TAIDateTime : DateTimeBase
{
    public TAIDateTime(DateTime dateTime) : base(dateTime, DateTimeKind.TAI)
    {
    }

    public override DateTimeBase ToTAI()
    {
        return this;
    }
}