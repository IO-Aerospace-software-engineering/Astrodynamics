using System;
using IO.Astrodynamics.Time;
using Xunit;

namespace IO.Astrodynamics.Tests.Time;

public class DateTimeTests
{
    [Fact]
    public void ToTDBFromUTC()
    {
        Assert.Equal(new DateTime(1976, 12, 31, 12, 0, 47, 184, DateTimeKind.Unspecified),
            new DateTime(1976, 12, 31, 12, 0, 0, DateTimeKind.Utc).ToTDB(),TimeSpan.FromMilliseconds(1));
        Assert.Equal(new DateTime(1977, 1, 1, 12, 0, 48, 184, DateTimeKind.Unspecified),
            new DateTime(1977, 1, 1, 12, 0, 0, DateTimeKind.Utc).ToTDB(),TimeSpan.FromMilliseconds(1));
        Assert.Equal(new DateTime(2016, 12, 31, 12, 1, 8, 184, DateTimeKind.Unspecified),
            new DateTime(2016, 12, 31, 12, 0, 0, DateTimeKind.Utc).ToTDB(),TimeSpan.FromMilliseconds(1));
        Assert.Equal(new DateTime(2017, 1, 1, 12, 1, 9, 184, DateTimeKind.Unspecified),
            new DateTime(2017, 1, 1, 12, 0, 0, DateTimeKind.Utc).ToTDB(),TimeSpan.FromMilliseconds(1));
        Assert.Equal(new DateTime(2021, 12, 31, 12, 1, 9, 184, DateTimeKind.Unspecified),
            new DateTime(2021, 12, 31, 12, 0, 0, DateTimeKind.Utc).ToTDB(),TimeSpan.FromMilliseconds(1));
        Assert.Equal(new DateTime(2022, 1, 1, 12, 1, 9, 184, DateTimeKind.Unspecified),
            new DateTime(2022, 1, 1, 12, 0, 0, DateTimeKind.Utc).ToTDB(),TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void ToTDBFromLocal()
    {
        Assert.Equal(
            new DateTime(2022, 1, 1, 12, 1, 9, 184, DateTimeKind.Unspecified) -
            TimeZoneInfo.Local.GetUtcOffset(new DateTime(2022, 1, 1, 12, 0, 0, DateTimeKind.Local)),
            new DateTime(2022, 1, 1, 12, 0, 0, DateTimeKind.Local).ToTDB(),TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void ToUTCFromLocal()
    {
        Assert.Equal(
            new DateTime(2022, 1, 1, 12, 0, 0, DateTimeKind.Utc) -
            TimeZoneInfo.Local.GetUtcOffset(new DateTime(2022, 1, 1, 12, 0, 0, DateTimeKind.Local)),
            new DateTime(2022, 1, 1, 12, 0, 0, DateTimeKind.Local).ToUTC());
    }

    [Fact]
    public void ToUTCFromTDB()
    {
        Assert.Equal(new DateTime(1976, 12, 31, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(1976, 12, 31, 12, 0, 47, 184, DateTimeKind.Unspecified).ToUTC(),TimeSpan.FromMilliseconds(1));
        Assert.Equal(new DateTime(1977, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(1977, 1, 1, 12, 0, 48, 184, DateTimeKind.Unspecified).ToUTC(),TimeSpan.FromMilliseconds(1));
        Assert.Equal(new DateTime(2016, 12, 31, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2016, 12, 31, 12, 1, 8, 184, DateTimeKind.Unspecified).ToUTC(),TimeSpan.FromMilliseconds(1));
        Assert.Equal(new DateTime(2017, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2017, 1, 1, 12, 1, 9, 184, DateTimeKind.Unspecified).ToUTC(),TimeSpan.FromMilliseconds(1));
        Assert.Equal(new DateTime(2021, 12, 31, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2021, 12, 31, 12, 1, 9, 184, DateTimeKind.Unspecified).ToUTC(),TimeSpan.FromMilliseconds(1));
        Assert.Equal(new DateTime(2022, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2022, 1, 1, 12, 1, 9, 184, DateTimeKind.Unspecified).ToUTC(),TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void SecondsFromJ2000()
    {
        Assert.Equal(0.0, new DateTime(2000, 01, 01, 12, 0, 0, 0, DateTimeKind.Unspecified).SecondsFromJ2000TDB());
        Assert.Equal(-315532742.8160001, new DateTime(1990, 01, 01, 12, 0, 0, 0, DateTimeKind.Utc).SecondsFromJ2000TDB());
        Assert.Equal(631152069.1839999, new DateTime(2020, 01, 01, 12, 0, 0, 0, DateTimeKind.Utc).SecondsFromJ2000TDB());
    }

    [Fact]
    public void CreateFromEllapsedSeconds()
    {
        Assert.Equal(new DateTime(2000, 01, 01, 12, 0, 0, 0, DateTimeKind.Unspecified),
            DateTimeExtension.CreateTDB(0.0));
        Assert.Equal(new DateTime(1990, 01, 01, 12, 0, 57, 184, DateTimeKind.Utc),
            DateTimeExtension.CreateUTC(-315532742.816),TimeSpan.FromMilliseconds(1));
        Assert.Equal(new DateTime(2020, 01, 01, 12, 1, 9, 184, DateTimeKind.Utc),
            DateTimeExtension.CreateUTC(631152069.184),TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void ToFormattedString()
    {
        var utc = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var local = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Local);
        var unspecified = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal("2021-01-01T00:00:00.0000000Z", utc.ToFormattedString());
        Assert.Equal("2021-01-01T00:00:00.0000000 (TDB)", unspecified.ToFormattedString());
    }

    [Fact]
    public void ToJulian()
    {
        var jd = DateTimeExtension.J2000.ToJulianDate();
        Assert.Equal(DateTimeExtension.JULIAN_J2000, jd);
    }

    [Fact]
    public void ToJulian2()
    {
        var jd = new DateTime(1950, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(2433282.5000000000, jd.ToJulianDate());
    }

    [Fact]
    public void TDBFromJulian()
    {
        var date = DateTimeExtension.CreateTDBFromJD(DateTimeExtension.JULIAN_J2000);
        Assert.Equal(DateTimeExtension.J2000, date);
    }

    [Fact]
    public void UTCFromJulian()
    {
        var date = DateTimeExtension.CreateUTCFromJD(DateTimeExtension.JULIAN_J2000);
        Assert.Equal(new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc), date);
    }

    [Fact]
    public void FromJulian2()
    {
        var date = DateTimeExtension.CreateTDBFromJD(2433282.5000000000);
        Assert.Equal(new DateTime(1950, 1, 1, 0, 0, 0, DateTimeKind.Utc), date);
    }
}