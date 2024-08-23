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
        Assert.Equal(new TimeSystem.Time("2019-12-31T23:01:09.1840000 TDT"), tdt);
    }

    [Fact]
    public void TdtToLocal()
    {
        var tdt = new TimeSystem.Time(2019, 12, 31, 23, 01, 09, 184, frame: TimeFrame.TDTFrame);
        var loc = tdt.ToLocal();
        Assert.Equal(new TimeSystem.Time("2020-01-01 00:00:00.000000+01:00"), loc);
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
}