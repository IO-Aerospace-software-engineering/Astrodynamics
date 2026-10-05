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
}
