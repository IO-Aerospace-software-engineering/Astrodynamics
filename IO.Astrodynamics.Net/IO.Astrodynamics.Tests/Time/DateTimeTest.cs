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
        var tai = new TimeSystem.Time(new Time(2020, 1, 1), TimeFrame.TAIFrame);
        var gps = tai.ToGPS();
        Assert.Equal((tai - TimeSpan.FromSeconds(19)).Time, gps.Time);
    }

    [Fact]
    public void GpsToTai()
    {
        var gps = new TimeSystem.Time(new Time(2020, 1, 1), TimeFrame.GPSFrame);
        var tai = gps.ToTAI();
        Assert.Equal((gps + TimeSpan.FromSeconds(19)).Time, tai.Time);
    }

    [Fact]
    public void TaiToTdt()
    {
        var tai = new TimeSystem.Time(new Time(2020, 1, 1), TimeFrame.TAIFrame);
        var tdt = tai.ToTDT();
        Assert.Equal((tai + TimeSpan.FromSeconds(32.184)).Time, tdt.Time);
    }

    [Fact]
    public void TdtToTai()
    {
        var tdt = new TimeSystem.Time(new Time(2020, 1, 1), TimeFrame.TDTFrame);
        var tai = tdt.ToTAI();
        Assert.Equal((tdt - TimeSpan.FromSeconds(32.184)).Time, tai.Time);
    }

    [Fact]
    public void TdtToGPS()
    {
        var tdt = new TimeSystem.Time(new Time(2020, 1, 1), TimeFrame.TDTFrame);
        var gps = tdt.ToGPS();
        Assert.Equal((tdt + TimeSpan.FromSeconds(32.184 + 19.0).Negate()).Time, gps.Time);
    }

    [Fact]
    public void TdtConvertToGPS()
    {
        var tdt = new TimeSystem.Time(new Time(2020, 1, 1), TimeFrame.TDTFrame);
        var gps = tdt.ConvertTo(TimeFrame.GPSFrame);
        Assert.Equal((tdt + TimeSpan.FromSeconds(32.184 + 19.0).Negate()).Time, gps.Time);
    }

    [Fact]
    public void TaiToUtc()
    {
        var tai = new TimeSystem.Time(new Time(2020, 1, 1), TimeFrame.TAIFrame);
        var utc = tai.ToUTC();
        Assert.Equal(tai.Time.AddSeconds(-37), utc.Time);
    }

    [Fact]
    public void UTCToTai()
    {
        var utc = new TimeSystem.Time(new Time(2020, 1, 1), TimeFrame.UTCFrame);
        var tai = utc.ToTAI();
        Assert.Equal(utc.Time.AddSeconds(37), tai.Time);
    }

    [Fact]
    public void UTCToTdt()
    {
        var utc = new TimeSystem.Time(new Time(2020, 1, 1), TimeFrame.UTCFrame);
        var tdt = utc.ToTDT();
        Assert.Equal(Time.Parse("2020-01-01 00:01:09.184000"), tdt.Time);
    }

    [Fact]
    public void ToJulianDate()
    {
        var utc = new TimeSystem.Time(new Time(2020, 1, 1), TimeFrame.UTCFrame);
        var jl = utc.ToJulianDate();
        Assert.Equal(2458849.5, jl);
    }

    [Fact]
    public void ToTDBFromUTC()
    {
        Assert.Equal(new TimeSystem.Time(new Time(1976, 12, 31, 12, 0, 47, 183, 926, DateTimeKind.Unspecified), TimeFrame.TDBFrame),
            new TimeSystem.Time(new Time(1976, 12, 31, 12, 0, 0, DateTimeKind.Utc), TimeFrame.UTCFrame).ToTDB(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(new Time(1977, 1, 1, 12, 0, 48, 184, DateTimeKind.Unspecified), TimeFrame.TDBFrame),
            new TimeSystem.Time(new Time(1977, 1, 1, 12, 0, 0, DateTimeKind.Utc), TimeFrame.UTCFrame).ToTDB(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(new Time(2016, 12, 31, 12, 1, 8, 184, DateTimeKind.Unspecified), TimeFrame.TDBFrame),
            new TimeSystem.Time(new Time(2016, 12, 31, 12, 0, 0, DateTimeKind.Utc), TimeFrame.UTCFrame).ToTDB(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(new Time(2017, 1, 1, 12, 1, 9, 184, DateTimeKind.Unspecified), TimeFrame.TDBFrame),
            new TimeSystem.Time(new Time(2017, 1, 1, 12, 0, 0, DateTimeKind.Utc), TimeFrame.UTCFrame).ToTDB(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(new Time(2021, 12, 31, 12, 1, 9, 184, DateTimeKind.Unspecified), TimeFrame.TDBFrame),
            new TimeSystem.Time(new Time(2021, 12, 31, 12, 0, 0, DateTimeKind.Utc), TimeFrame.UTCFrame).ToTDB(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(new Time(2022, 1, 1, 12, 1, 9, 184, DateTimeKind.Unspecified), TimeFrame.TDBFrame),
            new TimeSystem.Time(new Time(2022, 1, 1, 12, 0, 0, DateTimeKind.Utc), TimeFrame.UTCFrame).ToTDB(), TestHelpers.TimeComparer);
    }

    [Fact]
    public void ToUTCFromTDB()
    {
        Assert.Equal(new TimeSystem.Time(new Time(1976, 12, 31, 12, 0, 0, DateTimeKind.Utc), TimeFrame.UTCFrame),
            new TimeSystem.Time(new Time(1976, 12, 31, 12, 0, 47, 184, DateTimeKind.Unspecified), TimeFrame.TDBFrame).ToUTC(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(new Time(1977, 1, 1, 12, 0, 0, DateTimeKind.Utc), TimeFrame.UTCFrame),
            new TimeSystem.Time(new Time(1977, 1, 1, 12, 0, 48, 184, DateTimeKind.Unspecified), TimeFrame.TDBFrame).ToUTC(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(new Time(2016, 12, 31, 12, 0, 0, DateTimeKind.Utc), TimeFrame.UTCFrame),
            new TimeSystem.Time(new Time(2016, 12, 31, 12, 1, 8, 184, DateTimeKind.Unspecified), TimeFrame.TDBFrame).ToUTC(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(new Time(2017, 1, 1, 12, 0, 0, DateTimeKind.Utc), TimeFrame.UTCFrame),
            new TimeSystem.Time(new Time(2017, 1, 1, 12, 1, 9, 184, DateTimeKind.Unspecified), TimeFrame.TDBFrame).ToUTC(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(new Time(2021, 12, 31, 12, 0, 0, DateTimeKind.Utc), TimeFrame.UTCFrame),
            new TimeSystem.Time(new Time(2021, 12, 31, 12, 1, 9, 184, DateTimeKind.Unspecified), TimeFrame.TDBFrame).ToUTC(), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(new Time(2022, 1, 1, 12, 0, 0, DateTimeKind.Utc), TimeFrame.UTCFrame),
            new TimeSystem.Time(new Time(2022, 1, 1, 12, 1, 9, 184, DateTimeKind.Unspecified), TimeFrame.TDBFrame).ToUTC(), TestHelpers.TimeComparer);
    }

    [Fact]
    public void SecondsFromJ2000()
    {
        Assert.Equal(0.0, new TimeSystem.Time(new Time(2000, 01, 01, 12, 0, 0, 0, DateTimeKind.Unspecified), TimeFrame.TDBFrame).TimeSpanFromJ2000().TotalSeconds);
        Assert.Equal(-315532800.000000, new TimeSystem.Time(new Time(1990, 01, 01, 12, 0, 0, 0, DateTimeKind.Utc), TimeFrame.UTCFrame).TimeSpanFromJ2000().TotalSeconds);
        Assert.Equal(631152000.0, new TimeSystem.Time(new Time(2020, 01, 01, 12, 0, 0, 0, DateTimeKind.Utc), TimeFrame.UTCFrame).TimeSpanFromJ2000().TotalSeconds);
    }

    [Fact]
    public void CreateFromEllapsedSeconds()
    {
        Assert.Equal(new TimeSystem.Time(new Time(2000, 01, 01, 12, 0, 0, 0, DateTimeKind.Unspecified), TimeFrame.TDBFrame),
            TimeSystem.Time.Create(0.0, TimeFrame.TDBFrame));
        Assert.Equal(new TimeSystem.Time(new Time(1990, 01, 01, 12, 0, 57, 184, DateTimeKind.Utc), TimeFrame.UTCFrame),
            TimeSystem.Time.Create(-315532742.816, TimeFrame.UTCFrame), TestHelpers.TimeComparer);
        Assert.Equal(new TimeSystem.Time(new Time(2020, 01, 01, 12, 1, 9, 184, DateTimeKind.Utc), TimeFrame.UTCFrame),
            TimeSystem.Time.Create(631152069.184, TimeFrame.UTCFrame), TestHelpers.TimeComparer);
    }

    [Fact]
    public void ToFormattedString()
    {
        var utc = TimeSystem.Time.Create(0.0, TimeFrame.UTCFrame);
        var tdb = TimeSystem.Time.Create(0.0, TimeFrame.TDBFrame);

        Assert.Equal("2000-01-01T12:00:00.0000000Z UTC", utc.ToString());
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
        var jd = new Time(1950, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var date = new TimeSystem.Time(jd, TimeFrame.UTCFrame);
        Assert.Equal(2433282.5000000000, date.ToJulianDate());
    }

    [Fact]
    public void TDBFromJulian()
    {
        var date = TimeSystem.Time.CreateFromJD(TimeSystem.Time.JULIAN_J2000, TimeFrame.TDBFrame);
        Assert.Equal(new TimeSystem.Time(TimeSystem.Time.J2000,TimeFrame.TDBFrame), date);
    }

    [Fact]
    public void UTCFromJulian()
    {
        var date = TimeSystem.Time.CreateFromJD(TimeSystem.Time.JULIAN_J2000, TimeFrame.UTCFrame);
        Assert.Equal(new TimeSystem.Time(TimeSystem.Time.J2000,TimeFrame.UTCFrame), date);
    }

    [Fact]
    public void FromJulian2()
    {
        var date = TimeSystem.Time.CreateFromJD(2433282.5000000000,TimeFrame.TDBFrame);
        Assert.Equal(new TimeSystem.Time(new Time(1950, 1, 1, 0, 0, 0, DateTimeKind.Utc),TimeFrame.TDBFrame), date);
    }
}