using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.CLI.Tests;

public class TimeTests
{
    [Fact]
    public void DateToJDUTC()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Args("1950-01-01T00:00:00Z", false, true, false, true, false, false));

            Assert.Equal($"2433282.5 JD UTC{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void DateToSecondsFromJ2000()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Args("2020-01-01T12:00:00Z", true, false, false, false, true, false));

            // TDB − TT rounded to the nearest tick (#625): tools/seconds_to_ticks/time_conversion_reference.py, an exact
            // replica of the conversions (.1839218 when they truncated)
            Assert.Equal($"631152069.1839217 TDB{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void DateToDateTDB()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Args("2020-01-01T12:00:00Z", true, false, false, false, false, true));

            Assert.Equal($"2020-01-01T12:01:09.1839217 TDB{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void J2000ToDateUTC()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Args("631152069.1839273 TDB", false, true, false, false, false, true));

            // The nearest tick of the seconds and of TDB − TT (#625; .0000054 when they truncated)
            Assert.Equal($"2020-01-01T12:00:00.0000056Z{Environment.NewLine}", res);
        }
    }

    // [Fact]
    internal void J2000ToLocal()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Args("631152069.184 TDB", false, false, true, false, false, true));

            Assert.Equal($"2020-01-01T13:00:00.0000001+01:00 {Environment.NewLine}", res);
        }
    }

    [Fact]
    public void LocalToJ2000()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Args("2020-01-01T13:00:00.0000001+01:00", true, false, false, false, true, false));

            // The nearest tick of TDB − TT (#625; .1839219 when it was truncated)
            Assert.Equal($"631152069.1839218 TDB{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void J2000ToJulianTDB()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Args("0.0 TDB", true, false, false, true, false, false));

            Assert.Equal($"{Time.JULIAN_J2000} JD TDB{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void J2000ToJ2000UTC()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Args("0.0 TDB", false, true, false, false, true, false));

            Assert.Equal($"-64.1839273 UTC{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void JulianToJ2000TDB()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Args($"{Time.JULIAN_J2000} JD TDB", true, false, false, false, true, false));

            Assert.Equal($"0 TDB{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void JulianToDateTDB()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Args($"{Time.JULIAN_J2000} JD TDB", true, false, false, false, false, true));

            Assert.Equal($"2000-01-01T12:00:00.0000000 TDB{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void JulianToJulianUTC()
    {
        using (Configuration.Lock())
        {
            var res = CliRun.Ok(Args($"{Time.JULIAN_J2000} JD TDB", false, true, false, true, false, false));

            // J2000 TDB is 11:58:55.816074 UTC (TDB - TT is about -74 µs at J2000). The value used to be
            // truncated to the millisecond (...2571296).
            Assert.Equal($"2451544.9992571305 JD UTC{Environment.NewLine}", res);
        }
    }

    [Fact]
    public void Exceptions()
    {
        using (Configuration.Lock())
        {
            const string epoch = "2020-01-01T12:00:00Z";
            // Two formats, two scales, local time in another format than ISO (D3.7), each an input error
            Assert.StartsWith("Error: Two formats", CliRun.Fails(2, Args(epoch, false, false, false, true, false, true)));
            Assert.StartsWith("Error: Two scales", CliRun.Fails(2, Args(epoch, true, true, false, true, false, true)));
            Assert.StartsWith("Error: Local time is written in ISO only", CliRun.Fails(2, Args(epoch, false, false, true, true, false, false)));
            Assert.StartsWith("Error: Two formats", CliRun.Fails(2, Args(epoch, true, false, false, true, true, false)));
            Assert.StartsWith("Error: Two formats", CliRun.Fails(2, Args(epoch, true, false, false, true, false, true)));
            Assert.StartsWith("Error: Two formats", CliRun.Fails(2, Args(epoch, true, false, false, false, true, true)));
        }
    }

    /// <summary>The arguments of time-converter with its aliases -t, -u, -l, -j, -e and -d.</summary>
    private static string[] Args(string epoch, bool toTdb, bool toUtc, bool toLocal, bool toJulian, bool toSeconds, bool toDateTime)
    {
        var args = new List<string> { "time-converter", epoch };
        if (toTdb) args.Add("-t");
        if (toUtc) args.Add("-u");
        if (toLocal) args.Add("-l");
        if (toJulian) args.Add("-j");
        if (toSeconds) args.Add("-e");
        if (toDateTime) args.Add("-d");
        return args.ToArray();
    }

    [Fact]
    public void Compare()
    {
        // J2000TDB and J2000UTC represent the same calendar date but different physical instants
        // When converted to TAI, they differ, so they are not equal
        Assert.Equal(-1, Time.J2000TDB.CompareTo(Time.J2000UTC));
        Assert.Equal(0, Time.J2000TDB.CompareTo(Time.J2000TDB));
        Assert.Equal(1, Time.J2000TDB.CompareTo(Time.J2000TDB.AddMonths(-1)));
        Assert.Equal(1, Time.J2000TDB.CompareTo(Time.J2000TDB.AddMilliseconds(-1)));
        Assert.Equal(-1, Time.J2000TDB.CompareTo(Time.J2000TDB.AddMicroseconds(1)));
        Assert.Equal(1, Time.J2000TDB.CompareTo(null));
        Assert.Equal(1, Time.J2000TDB.CompareTo((object)Time.J2000TDB.AddMonths(-1)));
        Assert.Equal(1, Time.J2000TDB.CompareTo((object)Time.J2000TDB.AddMilliseconds(-1)));
        Assert.Equal(-1, Time.J2000TDB.CompareTo((object)Time.J2000TDB.AddMicroseconds(1)));
    }
}