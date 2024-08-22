using System;
using System.Collections.Generic;

namespace IO.Astrodynamics.TimeSystem;

public readonly record struct Time
{
    public DateTime DateTime { get; }
    public ITimeFrame Frame { get; }

    public Time(DateTime dateTime, ITimeFrame frame)
    {
        DateTime = dateTime;
        Frame = frame;
    }

    public Time Add(in TimeSpan timeSpan)
    {
        return new Time(DateTime.Add(timeSpan), Frame);
    }
    
    public static Time operator +(Time left, TimeSpan right)
    {
        return left.Add(right);
    }
    
    public static Time operator -(Time left, TimeSpan right)
    {
        return left.Add(right.Negate());
    }
    
    public static TimeSpan operator -(Time left, Time right)
    {
        return left.DateTime.Subtract(right.DateTime);
    }
    
    public Time ConvertTo(ITimeFrame targetReference)
    {
        var taiTimeWithRef = Frame.ConvertToTAI(this);
        return targetReference.ConvertFromTAI(taiTimeWithRef);
    }

    public Time ToGPS()
    {
        return ConvertTo(TimeFrame.GPSFrame);
    }
    
    public Time ToTAI()
    {
        return ConvertTo(TimeFrame.TAIFrame);
    }
    
    public Time ToTDT()
    {
        return ConvertTo(TimeFrame.TDTFrame);
    }
    
    public Time ToUTC()
    {
        return ConvertTo(TimeFrame.UTCFrame);
    }
}