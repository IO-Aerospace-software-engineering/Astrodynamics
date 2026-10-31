using System;

namespace IO.Astrodynamics.TimeSystem.Frames;

public class TDBTimeFrame : TimeFrame
{
    private const double t1 = 357.528;
    private const double t2 = 35999.05;
    private const double t3 = 0.0167;

    internal TDBTimeFrame() : base("TDB")
    {
    }

    public override Time ConvertToTAI(Time time)
    {
        var tempTAI = time.Add(TimeSpan.FromSeconds(-32.184));
        var epoch = tempTAI.DateTime.Add(TimeSpan.FromSeconds(TdbMinusTt(time.Centuries())).Negate());
        return new TimeSystem.Time(epoch, TAIFrame);
    }

    public override Time ConvertFromTAI(Time time)
    {
        var tdt = time.ToTDT();
        var epoch = tdt.DateTime.Add(TimeSpan.FromSeconds(TdbMinusTt(tdt.Centuries())));
        return new TimeSystem.Time(epoch, TDBFrame);
    }

    /// <summary>
    /// TDB − TT in seconds by the series of this frame, 0.001658 sin(g + 0.0167 sin g), in double: the conversions of
    /// <see cref="Time"/> truncate it to the 100 ns tick, the time argument of the propagation (#501) does not.
    /// </summary>
    /// <param name="centuries">Julian centuries from J2000 at which g is evaluated: those of the TDB time converted to
    /// TAI (<see cref="ConvertToTAI"/>), those of the TT time converted to TDB (<see cref="ConvertFromTAI"/>).</param>
    internal static double TdbMinusTt(double centuries)
    {
        var g = Constants._2PI * (t1 + t2 * centuries) / 360.0;
        return 0.001658 * System.Math.Sin(g + t3 * System.Math.Sin(g));
    }
}
