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
        var g = this.G(time.Centuries());
        var epoch = tempTAI.DateTime.Add(TimeSpan.FromSeconds(0.001658 * System.Math.Sin(g + t3 * System.Math.Sin(g))).Negate());
        return new Time(epoch, TAIFrame);
    }

    public override Time ConvertFromTAI(Time time)
    {
        var tdt = time.ToTDT();
        var g = this.G(tdt.Centuries());
        var epoch = tdt.DateTime.Add(TimeSpan.FromSeconds(0.001658 * System.Math.Sin(g + t3 * System.Math.Sin(g))));
        return new Time(epoch, TDBFrame);
    }

    private double G(double t)
    {
        return Constants._2PI * (t1 + t2 * t) / 360.0;
    }
}