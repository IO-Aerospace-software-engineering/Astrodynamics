using System;
using System.Linq;
using IO.Astrodynamics.TimeSystem.Frames;

namespace IO.Astrodynamics.TimeSystem;

public abstract class TimeFrame : ITimeFrame, IEquatable<TimeFrame>
{
    public static TAITimeFrame TAIFrame { get; } = new TAITimeFrame();
    public static GPSTimeFrame GPSFrame { get; } = new GPSTimeFrame();
    public static TDTTimeFrame TDTFrame { get; } = new TDTTimeFrame();
    public static UTCTimeFrame UTCFrame { get; } = new UTCTimeFrame();
    public static TDBTimeFrame TDBFrame { get; } = new TDBTimeFrame();
    public static LocalTimeFrame LocalFrame { get; } = new LocalTimeFrame();
    const double PREVIOUS_OFFSET = 9.0; //before 1972;

    internal static readonly DateTime[] LEAP_SECONDS =
    [
        new DateTime(1972, 1, 1),
        new DateTime(1972, 7, 1),
        new DateTime(1973, 1, 1),
        new DateTime(1974, 1, 1),
        new DateTime(1975, 1, 1),
        new DateTime(1976, 1, 1),
        new DateTime(1977, 1, 1),
        new DateTime(1978, 1, 1),
        new DateTime(1979, 1, 1),
        new DateTime(1980, 1, 1),
        new DateTime(1981, 7, 1),
        new DateTime(1982, 7, 1),
        new DateTime(1983, 7, 1),
        new DateTime(1985, 7, 1),
        new DateTime(1988, 1, 1),
        new DateTime(1990, 1, 1),
        new DateTime(1991, 1, 1),
        new DateTime(1992, 7, 1),
        new DateTime(1993, 7, 1),
        new DateTime(1994, 7, 1),
        new DateTime(1996, 1, 1),
        new DateTime(1997, 7, 1),
        new DateTime(1999, 1, 1),
        new DateTime(2006, 1, 1),
        new DateTime(2009, 1, 1),
        new DateTime(2012, 7, 1),
        new DateTime(2015, 7, 1),
        new DateTime(2017, 1, 1)
    ];

    public string Name { get; }

    protected TimeFrame(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// TAI - UTC at the given UTC instant.
    /// </summary>
    /// <remarks>
    /// Each entry of the leap second table is the first UTC instant of the new offset (00:00:00 UTC), so it
    /// counts from that instant onward: 2017-01-01T00:00:00 UTC already has TAI - UTC = 37 s.
    /// </remarks>
    public TimeSpan LeapSecondsFrom(Time dateTime)
    {
        return LeapSecondsAtUtc(dateTime.DateTime);
    }

    /// <summary>
    /// TAI - UTC at the given UTC instant.
    /// </summary>
    internal static TimeSpan LeapSecondsAtUtc(DateTime utc)
    {
        return TimeSpan.FromSeconds(PREVIOUS_OFFSET + LEAP_SECONDS.Count(x => x <= utc));
    }

    /// <summary>
    /// TAI - UTC at the given TAI instant.
    /// </summary>
    /// <remarks>
    /// The leap second inserted before a table entry D starts at the TAI instant D + (previous offset): from there
    /// on the new offset applies. During the inserted second, UTC 23:59:60, which <see cref="DateTime"/> cannot
    /// represent, reads as 23:59:59 a second time; D 00:00:00 UTC is reached one second later, as it should be.
    /// </remarks>
    internal static TimeSpan LeapSecondsAtTai(DateTime tai)
    {
        int count = 0;
        for (int i = 0; i < LEAP_SECONDS.Length; i++)
        {
            if (tai >= LEAP_SECONDS[i].AddSeconds(PREVIOUS_OFFSET + i))
            {
                count++;
            }
        }

        return TimeSpan.FromSeconds(PREVIOUS_OFFSET + count);
    }

    public abstract Time ConvertToTAI(Time time);
    public abstract Time ConvertFromTAI(Time time);

    #region Compare

    public bool Equals(TimeFrame other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name;
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((TimeFrame)obj);
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }

    public static bool operator ==(TimeFrame left, TimeFrame right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(TimeFrame left, TimeFrame right)
    {
        return !Equals(left, right);
    }

    #endregion

    public override string ToString()
    {
        return Name;
    }
}