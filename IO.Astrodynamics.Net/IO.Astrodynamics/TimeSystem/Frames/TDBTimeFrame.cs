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
        return FromTaiTicks(TaiTicks(time));
    }

    /// <summary>
    /// The TAI ticks of a TDB time: its ticks less TT − TAI and less TDB − TT at its own centuries, rounded to the tick.
    /// Not one to one: where the rounded TDB − TT gains a tick, two consecutive TDB ticks give one TAI tick (about every
    /// 300 s near its steepest), and where it loses one a TAI tick has no TDB tick; since TDB − TT changes by less than
    /// 3.4e-10 s per second, two TDB ticks two or more apart never give one TAI tick nor change their order (#512).
    /// </summary>
    internal override long TaiTicks(in Time time)
    {
        return time.DateTime.Ticks - TDTTimeFrame.TtMinusTaiTicks - Time.SecondsToTicks(TdbMinusTt(time.Centuries()));
    }

    public override Time ConvertFromTAI(Time time)
    {
        var tdt = time.ToTDT();
        var epoch = tdt.DateTime.AddTicks(Time.SecondsToTicks(TdbMinusTt(tdt.Centuries())));
        return new TimeSystem.Time(epoch, TDBFrame);
    }

    /// <summary>
    /// TDB − TT in seconds by the series of this frame, 0.001658 sin(g + 0.0167 sin g), in double: the conversions of
    /// <see cref="Time"/> round it to the nearest 100 ns tick (<see cref="Time.SecondsToTicks"/>, #625; they truncated it
    /// toward zero before), the time argument of the propagation (#501) does not.
    /// </summary>
    /// <param name="centuries">Julian centuries from J2000 at which g is evaluated: those of the TDB time converted to
    /// TAI (<see cref="ConvertToTAI"/>), those of the TT time converted to TDB (<see cref="ConvertFromTAI"/>).</param>
    internal static double TdbMinusTt(double centuries)
    {
        var g = Constants._2PI * (t1 + t2 * centuries) / 360.0;
        return 0.001658 * System.Math.Sin(g + t3 * System.Math.Sin(g));
    }
}
