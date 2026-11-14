using System;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.TimeSystem.Frames;
using Xunit;

namespace IO.Astrodynamics.Tests.Time;

public class DateTimeTests
{
    [Fact]
    public void TaiToGps()
    {
        var tai = new TimeSystem.Time(2020, 1, 1, frame: TimeFrame.TAIFrame);
        var gps = tai.ToGPS();
        Assert.Equal((tai - TimeSpan.FromSeconds(19)).DateTime, gps.DateTime);
    }

    [Fact]
    public void GpsToTai()
    {
        var gps = new TimeSystem.Time(2020, 1, 1, frame: TimeFrame.GPSFrame);
        var tai = gps.ToTAI();
        Assert.Equal((gps + TimeSpan.FromSeconds(19)).DateTime, tai.DateTime);
    }

    [Fact]
    public void TaiToTdt()
    {
        var tai = new TimeSystem.Time(2020, 1, 1, frame: TimeFrame.TAIFrame);
        var tdt = tai.ToTDT();
        Assert.Equal((tai + TimeSpan.FromSeconds(32.184)).DateTime, tdt.DateTime);
    }

    [Fact]
    public void TdtToTai()
    {
        var tdt = new TimeSystem.Time(2020, 1, 1, frame: TimeFrame.TDTFrame);
        var tai = tdt.ToTAI();
        Assert.Equal((tdt - TimeSpan.FromSeconds(32.184)).DateTime, tai.DateTime);
    }

    [Fact]
    public void TdtToGPS()
    {
        var tdt = new TimeSystem.Time(2020, 1, 1, frame: TimeFrame.TDTFrame);
        var gps = tdt.ToGPS();
        Assert.Equal((tdt + TimeSpan.FromSeconds(32.184 + 19.0).Negate()).DateTime, gps.DateTime);
    }

    [Fact]
    public void TdtConvertToGPS()
    {
        var tdt = new TimeSystem.Time(2020, 1, 1, frame: TimeFrame.TDTFrame);
        var gps = tdt.ConvertTo(TimeFrame.GPSFrame);
        Assert.Equal((tdt + TimeSpan.FromSeconds(32.184 + 19.0).Negate()).DateTime, gps.DateTime);
    }

    [Fact]
    public void TaiToUtc()
    {
        var tai = new TimeSystem.Time(2020, 1, 1, frame: TimeFrame.TAIFrame);
        var utc = tai.ToUTC();
        Assert.Equal(tai.DateTime.AddSeconds(-37), utc.DateTime);
    }

    [Fact]
    public void UTCToTai()
    {
        var utc = new TimeSystem.Time(2020, 1, 1, frame: TimeFrame.UTCFrame);
        var tai = utc.ToTAI();
        Assert.Equal(utc.DateTime.AddSeconds(37), tai.DateTime);
    }

    [Fact]
    public void UTCToTdt()
    {
        var utc = new TimeSystem.Time(2020, 1, 1, frame: TimeFrame.UTCFrame);
        var tdt = utc.ToTDT();
        Assert.Equal(new TimeSystem.Time("2020-01-01 00:01:09.184000 TDT"), tdt);
    }

    [Fact]
    public void LocalToTdt()
    {
        var loc = new TimeSystem.Time(2020, 1, 1, frame: TimeFrame.LocalFrame);
        var tdt = loc.ToTDT();
        // Calculating expected TDT based on the system's local offset at 2020-01-01
        var localDateTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Local);
        var offset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);
        // UTC = Local - offset
        var utcDateTime = localDateTime - offset;
        // TDT = UTC + 69.184s (37 leap seconds + 32.184)
        var tdtDateTime = utcDateTime.AddSeconds(37 + 32.184);
        var expectedTdt = new TimeSystem.Time(tdtDateTime, TimeFrame.TDTFrame);
        Assert.Equal(expectedTdt.DateTime, tdt.DateTime,TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void TdtToLocal()
    {
        var tdt = new TimeSystem.Time(2019, 12, 31, 23, 01, 09, 184, frame: TimeFrame.TDTFrame);
        var utcDateTime = tdt.DateTime.AddSeconds(-69.184);
        var localOffset = TimeZoneInfo.Local.GetUtcOffset(utcDateTime);
        var localDateTime = utcDateTime + localOffset;
        var expectedLocal = new TimeSystem.Time(localDateTime, TimeFrame.LocalFrame);
        Assert.Equal(expectedLocal.DateTime, tdt.ToLocal().DateTime, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void ToJulianDate()
    {
        var utc = new TimeSystem.Time(2020, 1, 1, frame: TimeFrame.UTCFrame);
        var jl = utc.ToJulianDate();
        Assert.Equal(2458849.5, jl);
    }

    [Fact]
    public void ToTDBFromUTC()
    {
        Assert.Equal(new TimeSystem.Time(1976, 12, 31, 12, 0, 47, 183, 926, TimeFrame.TDBFrame),
            new TimeSystem.Time(1976, 12, 31, 12, 0, 0, frame: TimeFrame.UTCFrame).ToTDB(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(1977, 1, 1, 12, 0, 48, 184),
            new TimeSystem.Time(1977, 1, 1, 12, 0, 0, frame: TimeFrame.UTCFrame).ToTDB(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(2016, 12, 31, 12, 1, 8, 184),
            new TimeSystem.Time(2016, 12, 31, 12, 0, 0, frame: TimeFrame.UTCFrame).ToTDB(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(2017, 1, 1, 12, 1, 9, 184),
            new TimeSystem.Time(2017, 1, 1, 12, 0, 0, frame: TimeFrame.UTCFrame).ToTDB(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(2021, 12, 31, 12, 1, 9, 184),
            new TimeSystem.Time(2021, 12, 31, 12, 0, 0, frame: TimeFrame.UTCFrame).ToTDB(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(2022, 1, 1, 12, 1, 9, 184),
            new TimeSystem.Time(2022, 1, 1, 12, 0, 0, frame: TimeFrame.UTCFrame).ToTDB(), TestHelpers.TimeComparer);
    }

    [Fact]
    public void ToUTCFromTDB()
    {
        Assert.Equal(new TimeSystem.Time(1976, 12, 31, 12, 0, 0, frame: TimeFrame.UTCFrame),
            new TimeSystem.Time(1976, 12, 31, 12, 0, 47, 184).ToUTC(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(1977, 1, 1, 12, 0, 0, frame: TimeFrame.UTCFrame),
            new TimeSystem.Time(1977, 1, 1, 12, 0, 48, 184).ToUTC(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(2016, 12, 31, 12, 0, 0, frame: TimeFrame.UTCFrame),
            new TimeSystem.Time(2016, 12, 31, 12, 1, 8, 184).ToUTC(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(2017, 1, 1, 12, 0, 0, frame: TimeFrame.UTCFrame),
            new TimeSystem.Time(2017, 1, 1, 12, 1, 9, 184).ToUTC(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(2021, 12, 31, 12, 0, 0, frame: TimeFrame.UTCFrame),
            new TimeSystem.Time(2021, 12, 31, 12, 1, 9, 184).ToUTC(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(2022, 1, 1, 12, 0, 0, frame: TimeFrame.UTCFrame),
            new TimeSystem.Time(2022, 1, 1, 12, 1, 9, 184).ToUTC(), TestHelpers.TimeComparer);
    }

    [Fact]
    public void SecondsFromJ2000()
    {
        Assert.Equal(0.0, new TimeSystem.Time(2000, 01, 01, 12, 0, 0, 0).TimeSpanFromJ2000().TotalSeconds);
        Assert.Equal(-315532800.000000, new TimeSystem.Time(1990, 01, 01, 12, 0, 0, 0, frame: TimeFrame.UTCFrame).TimeSpanFromJ2000().TotalSeconds);
        Assert.Equal(631152000.0, new TimeSystem.Time(2020, 01, 01, 12, 0, 0, 0, frame: TimeFrame.UTCFrame).TimeSpanFromJ2000().TotalSeconds);
    }

    [Fact]
    public void CreateFromEllapsedSeconds()
    {
        Assert.Equal(new TimeSystem.Time(2000, 01, 01, 12),
            TimeSystem.Time.Create(0.0, TimeFrame.TDBFrame));
        Assert.Equal(new TimeSystem.Time(1990, 01, 01, 12, 0, 57, 184, frame: TimeFrame.UTCFrame),
            TimeSystem.Time.Create(-315532742.816, TimeFrame.UTCFrame), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(2020, 01, 01, 12, 1, 9, 184, frame: TimeFrame.UTCFrame),
            TimeSystem.Time.Create(631152069.184, TimeFrame.UTCFrame), TestHelpers.TimeComparer);
    }

    [Fact]
    public void EvaluateToString()
    {
        var utc = TimeSystem.Time.Create(0.0, TimeFrame.UTCFrame);
        var tdb = TimeSystem.Time.Create(0.0, TimeFrame.TDBFrame);

        Assert.Equal("2000-01-01T12:00:00.0000000Z", utc.ToString());
        Assert.Equal("2000-01-01T12:00:00.0000000 TDB", tdb.ToString());
    }

    [Fact]
    public void ToJulian()
    {
        var jd = TimeSystem.Time.Create(0.0, TimeFrame.TDBFrame).ToJulianDate();
        Assert.Equal(TimeSystem.Time.JULIAN_J2000, jd);
    }

    [Fact]
    public void ToJulian2()
    {
        var jd = new TimeSystem.Time(1950, 1, 1, 0, 0, 0, frame: TimeFrame.UTCFrame);
        Assert.Equal(2433282.5000000000, jd.ToJulianDate());
    }

    [Fact]
    public void TDBFromJulian()
    {
        var date = TimeSystem.Time.CreateFromJD(TimeSystem.Time.JULIAN_J2000, TimeFrame.TDBFrame);
        Assert.Equal(TimeSystem.Time.J2000TDB, date);
    }

    [Fact]
    public void UTCFromJulian()
    {
        var date = TimeSystem.Time.CreateFromJD(TimeSystem.Time.JULIAN_J2000, TimeFrame.UTCFrame);
        Assert.Equal(TimeSystem.Time.J2000UTC, date);
    }

    [Fact]
    public void FromJulian2()
    {
        var date = TimeSystem.Time.CreateFromJD(2433282.5000000000, TimeFrame.TDBFrame);
        Assert.Equal(new TimeSystem.Time(1950, 1, 1, 0, 0, 0, frame: TimeFrame.TDBFrame), date);
    }

    [Fact]
    public void CreateTDTFromString()
    {
        var tdt = new TimeSystem.Time("2000-01-01T12:00:00.0000000 TDT");
        var expectedTdt = new TimeSystem.Time(2000, 1, 1, 12, frame: TimeFrame.TDTFrame);
        Assert.Equal(expectedTdt, tdt);
    }
    
    [Fact]
    public void CreateTDBFromString()
    {
        var source = new TimeSystem.Time("2000-01-01T12:00:00.0000000 TDB");
        var expected = new TimeSystem.Time(2000, 1, 1, 12, frame: TimeFrame.TDBFrame);
        Assert.Equal(expected, source);
    }
    
    [Fact]
    public void CreateTAIFromString()
    {
        var source = new TimeSystem.Time("2000-01-01T12:00:00.0000000 TAI");
        var expected = new TimeSystem.Time(2000, 1, 1, 12, frame: TimeFrame.TAIFrame);
        Assert.Equal(expected, source);
    }
    
    [Fact]
    public void CreateUTCFromString()
    {
        var source = new TimeSystem.Time("2000-01-01T12:00:00.0000000Z");
        var expected = new TimeSystem.Time(2000, 1, 1, 12, frame: TimeFrame.UTCFrame);
        Assert.Equal(expected, source);
    }
    
    [Fact]
    public void CreateGPSFromString()
    {
        var source = new TimeSystem.Time("2000-01-01T12:00:00.0000000 GPS");
        var expected = new TimeSystem.Time(2000, 1, 1, 12, frame: TimeFrame.GPSFrame);
        Assert.Equal(expected, source);
    }

    /// <summary>
    /// The frame names a time string can end with (#455), in the case written: 2024-06-21T12:00:00 of each frame is
    /// 772243200 s from J2000 in its own calendar (8938 days), and its TAI instant is that calendar plus TAI − UTC = 37 s
    /// (IERS Bulletin C, from 2017-01-01; the NAIF LSK), minus TT − TAI = 32.184 s, or plus TAI − GPS = 19 s.
    /// </summary>
    public static TheoryData<string, string, long?> FrameSuffixes() => new()
    {
        { "2024-06-21T12:00:00.000 UTC", "UTC", 370000000L },
        { "2024-06-21T12:00:00.000 utc", "UTC", 370000000L },
        { "2024-06-21T12:00:00.000 TT", "TDT", -321840000L },
        { "2024-06-21T12:00:00.000 tt", "TDT", -321840000L },
        { "2024-06-21T12:00:00.000 TDT", "TDT", -321840000L },
        { "2024-06-21T12:00:00.000 TAI", "TAI", 0L },
        { "2024-06-21T12:00:00.000 GPS", "GPS", 190000000L },
        { "2024-06-21 12:00:00 Tdb", "TDB", null },
        { "2024-06-21T12:00:00.000\tTDB  ", "TDB", null }
    };

    [Theory]
    [MemberData(nameof(FrameSuffixes))]
    public void CreateFromStringWithEachFrameName(string timeString, string frameName, long? taiMinusCalendarTicks)
    {
        var frame = frameName switch
        {
            "UTC" => (ITimeFrame)TimeFrame.UTCFrame,
            "TDT" => TimeFrame.TDTFrame,
            "TAI" => TimeFrame.TAIFrame,
            "GPS" => TimeFrame.GPSFrame,
            _ => TimeFrame.TDBFrame
        };

        var parsed = new TimeSystem.Time(timeString);

        Assert.Same(frame, parsed.Frame);
        Assert.Equal(new TimeSystem.Time(new DateTime(2024, 6, 21, 12, 0, 0), frame), parsed);
        Assert.Equal(TimeSystem.Time.Create(772243200.0, frame), parsed);
        Assert.Equal(frame is UTCTimeFrame ? DateTimeKind.Utc : DateTimeKind.Unspecified, parsed.DateTime.Kind);
        if (frame is UTCTimeFrame)
        {
            Assert.Equal(TimeSystem.Time.CreateUTC(772243200.0), parsed);
        }
        else if (frame is TDBTimeFrame)
        {
            Assert.Equal(TimeSystem.Time.CreateTDB(772243200.0), parsed);
        }

        if (taiMinusCalendarTicks.HasValue)
        {
            Assert.Equal(new DateTime(2024, 6, 21, 12, 0, 0).AddTicks(taiMinusCalendarTicks.Value), parsed.ToTAI().DateTime);
        }
    }

    [Fact]
    public void TheUtcExampleOfTheDocumentationIsTheUtcInstant()
    {
        // docs/reference/time-and-windows.md and DEVELOPER_GUIDE.md: the same instant as its Z form
        Assert.Equal(new TimeSystem.Time("2024-06-21T12:00:00.000Z"), new TimeSystem.Time("2024-06-21T12:00:00.000 UTC"));
    }

    [Fact]
    public void ToStringReadsBackInEveryFrame()
    {
        var calendar = new DateTime(2024, 6, 21, 12, 0, 0).AddTicks(1234567);
        foreach (var frame in new ITimeFrame[] { TimeFrame.UTCFrame, TimeFrame.TDBFrame, TimeFrame.TDTFrame, TimeFrame.TAIFrame, TimeFrame.GPSFrame })
        {
            var time = new TimeSystem.Time(calendar, frame);
            var read = new TimeSystem.Time(time.ToString());
            Assert.Same(frame, read.Frame);
            Assert.Equal(time, read);
        }
    }

    /// <summary>
    /// A frame name right after the calendar, with no space, as before #455, which cut the calendar one character short
    /// (12:05TDB read 12:00): the calendar as written, in the frame named.
    /// </summary>
    [Theory]
    [InlineData("2024-06-21T12:00:00.000TDB", "TDB", 2024, 6, 21, 12, 0, 0, 0)]
    [InlineData("2024-06-21T12:00:00.120TAI", "TAI", 2024, 6, 21, 12, 0, 0, 120)]
    [InlineData("2024-06-21T12:00:00.00GPS", "GPS", 2024, 6, 21, 12, 0, 0, 0)]
    [InlineData("2024-06-21 12:00:00TDB", "TDB", 2024, 6, 21, 12, 0, 0, 0)]
    [InlineData("2024-06-21T12:00:05TDB", "TDB", 2024, 6, 21, 12, 0, 5, 0)]
    [InlineData("2024-06-21 12:05TDB", "TDB", 2024, 6, 21, 12, 5, 0, 0)]
    [InlineData("2024-06-21T12:00:00.000UTC", "UTC", 2024, 6, 21, 12, 0, 0, 0)]
    [InlineData("2024-06-21T12:00:00tt", "TDT", 2024, 6, 21, 12, 0, 0, 0)]
    [InlineData("2024-06-21T12:00:00TDT", "TDT", 2024, 6, 21, 12, 0, 0, 0)]
    public void CreateFromStringWithAnUnspacedFrameName(string timeString, string frameName, int year, int month, int day,
        int hour, int minute, int second, int millisecond)
    {
        var frame = frameName switch
        {
            "UTC" => (ITimeFrame)TimeFrame.UTCFrame,
            "TDT" => TimeFrame.TDTFrame,
            "TAI" => TimeFrame.TAIFrame,
            "GPS" => TimeFrame.GPSFrame,
            _ => TimeFrame.TDBFrame
        };

        var parsed = new TimeSystem.Time(timeString);

        Assert.Same(frame, parsed.Frame);
        Assert.Equal(new TimeSystem.Time(new DateTime(year, month, day, hour, minute, second, millisecond), frame), parsed);
    }

    [Theory]
    [InlineData("2024-06-21T12:00:00.000 GMT")]
    [InlineData("2024-06-21T12:00:00.000 TTT")]
    [InlineData("2024-06-21T12:00:00.000 XTDB")]
    [InlineData("TDB")]
    [InlineData(" TDB")]
    public void CreateFromStringWithoutAFrameNameThrows(string timeString)
    {
        var exception = Assert.Throws<ArgumentException>(() => new TimeSystem.Time(timeString));
        Assert.StartsWith("Value is not a valid time frame.", exception.Message);
    }

    /// <summary>
    /// A Z or an offset before a frame name: DateTime converted the calendar to the machine's local time, which the frame
    /// then labelled, an instant that depended on the time zone of the machine (#455's review). It throws in every zone.
    /// </summary>
    [Theory]
    [InlineData("2024-06-21T12:00:00Z UTC")]
    [InlineData("2024-06-21T12:00:00.000ZUTC")]
    [InlineData("2024-06-21T12:00:00+02:00 UTC")]
    [InlineData("2024-06-21T12:00:00Z TDB")]
    [InlineData("2024-06-21T12:00:00z tai")]
    [InlineData("2024-06-21T12:00:00-05:00 GPS")]
    [InlineData("2024-06-21T12:00:00+00:00TT")]
    public void CreateFromStringWithAZoneAndAFrameNameThrows(string timeString)
    {
        var exception = Assert.Throws<ArgumentException>(() => new TimeSystem.Time(timeString));
        Assert.StartsWith("A calendar with a Z or an offset takes no frame name.", exception.Message);
    }

    [Theory]
    [InlineData("2016-12-31T23:59:60Z")]
    [InlineData("2016-12-31T23:59:60.5 UTC")]
    [InlineData("2016-12-31T23:59:60 TAI")]
    public void CreateFromStringWithSecondSixtyThrowsSaying(string timeString)
    {
        var exception = Assert.Throws<ArgumentException>(() => new TimeSystem.Time(timeString));
        Assert.StartsWith("Second 60 cannot be represented", exception.Message);
    }

    /// <summary>
    /// Every leap second of the table, with its index: TAI - UTC is 9 + index s just before it and 10 + index s
    /// from its first instant on (10 s on 1972-01-01, 37 s on 2017-01-01).
    /// </summary>
    public static TheoryData<DateTime, int> LeapSeconds()
    {
        var data = new TheoryData<DateTime, int>();
        for (int i = 0; i < TimeFrame.LEAP_SECONDS.Length; i++)
        {
            data.Add(TimeFrame.LEAP_SECONDS[i], i);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(LeapSeconds))]
    public void UtcToTaiUsesTheNewOffsetFromMidnightOfTheLeapSecondDate(DateTime leapDate, int index)
    {
        var midnight = new TimeSystem.Time(leapDate, TimeFrame.UTCFrame);
        Assert.Equal(leapDate.AddSeconds(10 + index), midnight.ToTAI().DateTime);

        var justBefore = new TimeSystem.Time(leapDate.AddSeconds(-0.5), TimeFrame.UTCFrame);
        Assert.Equal(leapDate.AddSeconds(-0.5 + 9 + index), justBefore.ToTAI().DateTime);
    }

    [Theory]
    [MemberData(nameof(LeapSeconds))]
    public void UtcRoundTripsThroughTaiAroundEveryLeapSecond(DateTime leapDate, int index)
    {
        _ = index;
        foreach (var offset in new[] { -1.5, -1.0, -0.5, 0.0, 0.5, 1.0, 1.5 })
        {
            var utc = new TimeSystem.Time(leapDate.AddSeconds(offset), TimeFrame.UTCFrame);
            Assert.Equal(utc.DateTime, utc.ToTAI().ToUTC().DateTime);
        }
    }

    [Theory]
    [MemberData(nameof(LeapSeconds))]
    public void TaiToUtcSwitchesOffsetAtTheInsertedSecond(DateTime leapDate, int index)
    {
        // Just after midnight TAI, UTC is still on the previous day with the previous offset.
        var shortlyAfterMidnightTai = new TimeSystem.Time(leapDate.AddSeconds(5), TimeFrame.TAIFrame);
        Assert.Equal(leapDate.AddSeconds(5 - 9 - index), shortlyAfterMidnightTai.ToUTC().DateTime);

        // The inserted second 23:59:60 cannot be represented and reads as 23:59:59 a second time.
        var insertedSecond = new TimeSystem.Time(leapDate.AddSeconds(9 + index + 0.5), TimeFrame.TAIFrame);
        Assert.Equal(leapDate.AddSeconds(-0.5), insertedSecond.ToUTC().DateTime);

        // One second later, UTC reaches midnight with the new offset.
        var newOffset = new TimeSystem.Time(leapDate.AddSeconds(10 + index), TimeFrame.TAIFrame);
        Assert.Equal(leapDate, newOffset.ToUTC().DateTime);
    }

    [Fact]
    public void UtcToTdbAtTheLastLeapSecondMidnight()
    {
        // 2017-01-01T00:00:00 UTC = 00:00:37 TAI = 00:01:09.184 TT; TDB differs from TT by less than 2 ms.
        var utc = new TimeSystem.Time(2017, 1, 1, frame: TimeFrame.UTCFrame);
        var tdt = utc.ToTDT();
        Assert.Equal(new DateTime(2017, 1, 1, 0, 1, 9, 184), tdt.DateTime);
        Assert.Equal(tdt.DateTime, utc.ToTDB().DateTime, TimeSpan.FromMilliseconds(2));
    }

    [Fact]
    public void JulianDateKeepsSubMillisecondTime()
    {
        // 0.4 ms apart: the former OADate-based conversion truncated both to the same millisecond.
        var first = new TimeSystem.Time(new DateTime(2024, 1, 1, 6, 30, 15).AddTicks(1234567), TimeFrame.UTCFrame);
        var second = first.Add(TimeSpan.FromTicks(4000));

        // About 8766 days from J2000, a double resolves ~2e-12 day (0.2 µs).
        double elapsedDays = second.DaysFromJ2000() - first.DaysFromJ2000();
        Assert.Equal(4000.0 / TimeSpan.TicksPerDay, elapsedDays, 2e-12);
        Assert.Equal(TimeSystem.Time.JULIAN_J2000 + first.DaysFromJ2000(), first.ToJulianDate());
        Assert.Equal(first.DaysFromJ2000() / 36525.0, first.Centuries());
        Assert.NotEqual(first.ToJulianDate(), second.ToJulianDate());
    }
    // #680: a non-finite span has no instant. AddSeconds, AddMilliseconds and AddMicroseconds already throw (#625); DateTime's
    // AddDays/AddHours/AddMinutes returned the epoch unchanged for NaN (0 ticks) and named their own parameter, value
    public static TheoryData<string, double, bool> NonFiniteSpans()
    {
        var data = new TheoryData<string, double, bool>();
        foreach (var method in new[] { "days", "hours", "minutes", "julianDate" })
        {
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                data.Add(method, value, true);
                data.Add(method, value, false);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(NonFiniteSpans))]
    public void AddDaysHoursMinutesAndCreateFromJD_RejectANonFiniteArgument(string parameter, double value, bool utc)
    {
        ITimeFrame frame = utc ? TimeFrame.UTCFrame : TimeFrame.TDBFrame;
        var epoch = new TimeSystem.Time(2024, 6, 21, 12, 0, 0, frame: frame);
        Func<TimeSystem.Time> call = parameter switch
        {
            "days" => () => epoch.AddDays(value),
            "hours" => () => epoch.AddHours(value),
            "minutes" => () => epoch.AddMinutes(value),
            _ => () => TimeSystem.Time.CreateFromJD(value, frame)
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => call());
        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void AddDaysHoursMinutes_FiniteArguments_Unchanged()
    {
        // A non-regression check of the guard, not a correctness reference: the result is DateTime's arithmetic, bit for bit
        // (its truncation is #637's, not this guard's), for 1e5 random spans within ±1e5 days in each unit
        var random = new Random(680);
        var baseDate = new DateTime(2024, 6, 21, 12, 0, 0);
        var tdb = new TimeSystem.Time(baseDate, TimeFrame.TDBFrame);
        var utc = new TimeSystem.Time(DateTime.SpecifyKind(baseDate, DateTimeKind.Utc), TimeFrame.UTCFrame);
        for (int i = 0; i < 100000; i++)
        {
            double days = (random.NextDouble() * 2.0 - 1.0) * 1.0e5;
            Assert.Equal(baseDate.AddDays(days).Ticks, tdb.AddDays(days).DateTime.Ticks);
            Assert.Equal(baseDate.AddHours(days * 24.0).Ticks, tdb.AddHours(days * 24.0).DateTime.Ticks);
            Assert.Equal(baseDate.AddMinutes(days * 1440.0).Ticks, utc.AddMinutes(days * 1440.0).DateTime.Ticks);
            Assert.Equal(TimeFrame.TDBFrame, tdb.AddDays(days).Frame);
            Assert.Equal(TimeFrame.UTCFrame, utc.AddMinutes(days * 1440.0).Frame);

            double julianDate = TimeSystem.Time.JULIAN_J2000 + days;
            Assert.Equal(TimeSystem.Time.J2000.AddDays(julianDate - TimeSystem.Time.JULIAN_J2000).Ticks,
                TimeSystem.Time.CreateFromJD(julianDate, TimeFrame.TDBFrame).DateTime.Ticks);
        }
    }

    // #680: the day of year counts the days of its year, 365 or 366 by the Gregorian rule (a multiple of 4, not of 100 unless
    // of 400), and the seconds those of a day without leap second, [0, 86400); year 0 was read as 2000
    [Theory]
    [InlineData(0, 1, 0.0, "year")]
    [InlineData(-1, 1, 0.0, "year")]
    [InlineData(10000, 1, 0.0, "year")]
    [InlineData(2024, 0, 0.0, "doy")]
    [InlineData(2024, -5, 0.0, "doy")]
    [InlineData(2024, 367, 0.0, "doy")]
    [InlineData(2023, 366, 0.0, "doy")]
    [InlineData(1900, 366, 0.0, "doy")]
    [InlineData(2024, 400, 0.0, "doy")]
    [InlineData(2024, 1, -1.0, "sec")]
    [InlineData(2024, 1, -1.0e-7, "sec")]
    [InlineData(2024, 1, 86400.0, "sec")]
    [InlineData(2024, 1, 90000.0, "sec")]
    [InlineData(2024, 1, double.NaN, "sec")]
    [InlineData(2024, 1, double.PositiveInfinity, "sec")]
    [InlineData(2024, 1, double.NegativeInfinity, "sec")]
    public void CreateFromDayOfYear_RejectsOutOfRangeInputs(int year, int doy, double sec, string parameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => TimeSystem.Time.Create(year, doy, sec));
        Assert.Equal(parameter, exception.ParamName);
    }

    [Theory]
    // The last day of a leap year and of a common year
    [InlineData(2024, 366, 0.0, 2024, 12, 31, 0L)]
    [InlineData(2023, 365, 86399.9999999, 2023, 12, 31, 863999999999L)]
    // The nearest tick of 86399.99999996 s is 86400 s: the next day's midnight, the nearest instant
    [InlineData(2024, 1, 86399.99999996, 2024, 1, 2, 0L)]
    // Day 60: 29 February in a leap year (2000, a multiple of 400), 1 March otherwise (1900, a multiple of 100)
    [InlineData(2000, 60, 0.0, 2000, 2, 29, 0L)]
    [InlineData(1900, 60, 0.0, 1900, 3, 1, 0L)]
    [InlineData(1, 1, 0.0, 1, 1, 1, 0L)]
    [InlineData(9999, 365, 0.0, 9999, 12, 31, 0L)]
    [InlineData(2024, 1, 43200.5, 2024, 1, 1, 432005000000L)]
    public void CreateFromDayOfYear_ValidBounds(int year, int doy, double sec, int expectedYear, int month, int day, long ticksOfDay)
    {
        var time = TimeSystem.Time.Create(year, doy, sec);
        Assert.Equal(TimeFrame.UTCFrame, time.Frame);
        Assert.Equal(new DateTime(expectedYear, month, day, 0, 0, 0, DateTimeKind.Utc).AddTicks(ticksOfDay), time.DateTime);
    }
}
