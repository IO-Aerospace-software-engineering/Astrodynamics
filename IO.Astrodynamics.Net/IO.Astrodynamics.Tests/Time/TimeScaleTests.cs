// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Time;

/// <summary>
/// The time API of R24 (D3 of #668, approved by S. Guillet, 2026-11-13, amended 2026-11-19): the scale a required argument
/// (<see cref="TimeScale"/>), one grammar for <see cref="TimeSystem.Time.Parse(string)"/> and its overloads,
/// <see cref="IFormattable"/>, TT written TT, the labels compared by <see cref="TimeSystem.Time.EqualsExact"/> and
/// <see cref="TimeSystem.Time.ExactComparer"/>. Every expected value is a definition: ISO 8601 and the CCSDS day of
/// year for the calendars, TAI − UTC = 37 s from 2017-01-01 (IERS Bulletin C), TT = TAI + 32.184 s, GPS = TAI − 19 s
/// (IS-GPS-200), the Julian date of 2000-01-01T12:00:00, 2451545.0, and 86 400 s a day.
/// </summary>
public class TimeScaleTests
{
    private static readonly DateTime Calendar = new(2024, 6, 21, 12, 0, 0);

    public static TheoryData<TimeScale> Scales => new() { TimeScale.TDB, TimeScale.TT, TimeScale.TAI, TimeScale.UTC, TimeScale.GPS };

    [Fact]
    public void TheDefaultTime_IsMidnightOfYearOneInTdb()
    {
        var time = default(TimeSystem.Time);
        Assert.Equal(TimeScale.TDB, time.Scale);
        Assert.Equal(DateTime.MinValue, time.DateTime);
        Assert.Equal("0001-01-01T00:00:00.0000000 TDB", time.ToString());
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void EachCalendarConstructor_IsTheCalendarInItsScale(TimeScale scale)
    {
        LabelAssert.Equal(new TimeSystem.Time(new DateTime(2024, 6, 21), scale), new TimeSystem.Time(2024, 6, 21, scale));
        LabelAssert.Equal(new TimeSystem.Time(Calendar, scale), new TimeSystem.Time(2024, 6, 21, 12, 0, 0, scale));
        LabelAssert.Equal(new TimeSystem.Time(Calendar.AddMilliseconds(250), scale), new TimeSystem.Time(2024, 6, 21, 12, 0, 0, 250, scale));
        LabelAssert.Equal(new TimeSystem.Time(Calendar.AddTicks(2500010), scale),
            new TimeSystem.Time(2024, 6, 21, 12, 0, 0, 250, 1, scale));
        Assert.Equal(scale == TimeScale.UTC ? DateTimeKind.Utc : DateTimeKind.Unspecified,
            new TimeSystem.Time(2024, 6, 21, scale).DateTime.Kind);
    }

    [Fact]
    public void ALocalDateTime_Throws()
    {
        // A local time is not a scale (D3.2): its instant depends on the zone of the machine
        var local = DateTime.SpecifyKind(Calendar, DateTimeKind.Local);
        foreach (var scale in new[] { TimeScale.UTC, TimeScale.TDB })
        {
            var exception = Assert.Throws<ArgumentException>(() => new TimeSystem.Time(local, scale));
            Assert.Equal("dateTime", exception.ParamName);
            Assert.Contains("ToUniversalTime()", exception.Message);
        }
    }

    [Theory]
    [InlineData(TimeScale.TDB)]
    [InlineData(TimeScale.TT)]
    [InlineData(TimeScale.TAI)]
    [InlineData(TimeScale.GPS)]
    public void AUtcDateTimeInAnotherScale_Throws(TimeScale scale)
    {
        // Before 11.0.0 the frame replaced the kind, so a UTC calendar was read as a TDB one, 69 s away, silently
        var exception = Assert.Throws<ArgumentException>(() => new TimeSystem.Time(DateTime.SpecifyKind(Calendar, DateTimeKind.Utc), scale));
        Assert.Equal("dateTime", exception.ParamName);
        LabelAssert.Equal(new TimeSystem.Time(Calendar, TimeScale.UTC),
            new TimeSystem.Time(DateTime.SpecifyKind(Calendar, DateTimeKind.Utc), TimeScale.UTC));
    }

    [Fact]
    public void AScaleOutsideTheEnumeration_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeSystem.Time(Calendar, (TimeScale)5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeSystem.Time(2024, 1, 1, (TimeScale)(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeSystem.Time.J2000TDB.ConvertTo((TimeScale)5));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeSystem.Time.Parse("2024-01-01", (TimeScale)5));
        Assert.False(TimeSystem.Time.TryParse("2024-01-01", (TimeScale)5, out _));
    }

    [Fact]
    public void UtcNow_IsTheClockOfTheMachineInUtc()
    {
        var before = DateTime.UtcNow;
        var now = TimeSystem.Time.UtcNow;
        var after = DateTime.UtcNow;
        Assert.Equal(TimeScale.UTC, now.Scale);
        Assert.InRange(now.DateTime, before, after);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void FromDayOfYear_IsTheCalendarOfThatDay(TimeScale scale)
    {
        // Day 173 of the leap year 2024 is 21 June; 43 200 s is noon
        LabelAssert.Equal(new TimeSystem.Time(Calendar, scale), TimeSystem.Time.FromDayOfYear(2024, 173, 43200.0, scale));
        LabelAssert.Equal(new TimeSystem.Time(2024, 12, 31, scale), TimeSystem.Time.FromDayOfYear(2024, 366, 0.0, scale));
    }

    [Theory]
    [InlineData(0, 1, 0.0, "year")]
    [InlineData(10000, 1, 0.0, "year")]
    [InlineData(2024, 0, 0.0, "dayOfYear")]
    [InlineData(2024, 367, 0.0, "dayOfYear")]
    [InlineData(2023, 366, 0.0, "dayOfYear")]
    [InlineData(2024, 400, 0.0, "dayOfYear")]
    [InlineData(2024, 1, -1.0, "seconds")]
    [InlineData(2024, 1, 86400.0, "seconds")]
    [InlineData(2024, 1, double.NaN, "seconds")]
    public void FromDayOfYear_OutOfRange_ThrowsNamingTheArgument(int year, int dayOfYear, double seconds, string parameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            TimeSystem.Time.FromDayOfYear(year, dayOfYear, seconds, TimeScale.UTC));
        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void ToString_WritesTtAsTt()
    {
        // TT was written TDT before 11.0.0 (D3.5); TDT is still read
        var tt = new TimeSystem.Time(Calendar, TimeScale.TT);
        Assert.Equal("2024-06-21T12:00:00.0000000 TT", tt.ToString());
        LabelAssert.Equal(tt, TimeSystem.Time.Parse("2024-06-21T12:00:00 TDT"));
        Assert.Equal("2024-06-21T12:00:00.0000000Z", new TimeSystem.Time(Calendar, TimeScale.UTC).ToString());
        Assert.Equal("2024-06-21T12:00:00.0000000 TDB", new TimeSystem.Time(Calendar, TimeScale.TDB).ToString());
        Assert.Equal("2024-06-21T12:00:00.0000000 TAI", new TimeSystem.Time(Calendar, TimeScale.TAI).ToString());
        Assert.Equal("2024-06-21T12:00:00.0000000 GPS", new TimeSystem.Time(Calendar, TimeScale.GPS).ToString());
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void Parse_OfToString_IsTheSameLabel(TimeScale scale)
    {
        foreach (long ticks in new[] { 0L, 1L, 1234567L, DateTime.MaxValue.Ticks - Calendar.Ticks })
        {
            var time = new TimeSystem.Time(Calendar.AddTicks(ticks), scale);
            Assert.True(TimeSystem.Time.Parse(time.ToString()).EqualsExact(time), time.ToString());
            Assert.True(TimeSystem.Time.Parse(time.ToString(), CultureInfo.InvariantCulture).EqualsExact(time));
            Assert.True(TimeSystem.Time.Parse(time.ToString().AsSpan(), null).EqualsExact(time));
        }
    }

    /// <summary>
    /// The forms of the grammar, each the calendar or the Julian date it writes in the scale it names.
    /// </summary>
    public static TheoryData<string, DateTime, TimeScale> AcceptedForms => new()
    {
        { "2024-06-21T12:00:00Z", Calendar, TimeScale.UTC },
        { "2024-06-21t12:00:00z", Calendar, TimeScale.UTC },
        { "2024-06-21T12:00:00 UTC", Calendar, TimeScale.UTC },
        { "2024-06-21 12:00:00 utc", Calendar, TimeScale.UTC },
        { "  2024-06-21T12:00:00   Utc  ", Calendar, TimeScale.UTC },
        { "2024-06-21T14:30:00+02:30", Calendar, TimeScale.UTC },
        { "2024-06-21T07:00:00-05:00", Calendar, TimeScale.UTC },
        { "2024-06-21T12:00:00TDB", Calendar, TimeScale.TDB },
        { "2024-06-21T12:00 TDB", Calendar, TimeScale.TDB },
        { "2024-06-21 TDB", new DateTime(2024, 6, 21), TimeScale.TDB },
        { "2024-06-21T12:00:00.1234567 TT", Calendar.AddTicks(1234567), TimeScale.TT },
        { "2024-06-21T12:00:00.5 TAI", Calendar.AddMilliseconds(500), TimeScale.TAI },
        { "2024-06-21T12:00:00 GPS", Calendar, TimeScale.GPS },
        { "2024-173T12:00:00 UTC", Calendar, TimeScale.UTC },
        { "2024-173T12:00:00Z", Calendar, TimeScale.UTC },
        { "2024-366T00:00:00 TDB", new DateTime(2024, 12, 31), TimeScale.TDB },
        { "JD 2451545.0 TDB", new DateTime(2000, 1, 1, 12, 0, 0), TimeScale.TDB },
        { "2451545 JD TT", new DateTime(2000, 1, 1, 12, 0, 0), TimeScale.TT },
        { "2460310.5 JD TT", new DateTime(2024, 1, 1), TimeScale.TT },
        { "jd 2460482.75 utc", new DateTime(2024, 6, 21, 6, 0, 0), TimeScale.UTC },
        // 1e-7 day is 8.64 ms exactly, 86 400 ticks; the digits are read exactly, not through a double
        { "JD 2451545.0000001 TAI", new DateTime(2000, 1, 1, 12, 0, 0).AddTicks(86400), TimeScale.TAI },
        // 1e-10 day is 86.4 ticks, the nearest 86: below the 4.7e-10 day a double resolves at this Julian date
        { "JD 2451545.0000000001 TDB", new DateTime(2000, 1, 1, 12, 0, 0).AddTicks(86), TimeScale.TDB },
    };

    [Theory]
    [MemberData(nameof(AcceptedForms))]
    public void Parse_ReadsEachForm(string text, DateTime calendar, TimeScale scale)
    {
        var expected = new TimeSystem.Time(calendar, scale);
        LabelAssert.Equal(expected, TimeSystem.Time.Parse(text));
        Assert.True(TimeSystem.Time.TryParse(text, out var parsed));
        LabelAssert.Equal(expected, parsed);
        // The scale given for untagged strings agrees with a tag, a Z or an offset of the same scale
        LabelAssert.Equal(expected, TimeSystem.Time.Parse(text, scale));
        Assert.True(TimeSystem.Time.TryParse(text, scale, out parsed));
        LabelAssert.Equal(expected, parsed);
    }

    /// <summary>
    /// D3.4 of #668: <c>Parse(s, scale)</c> gives its scale to an untagged string, and a tag, a <c>Z</c> or an offset of
    /// another scale contradicts it: <see cref="FormatException"/>, and false from <c>TryParse(s, scale)</c>.
    /// </summary>
    [Theory]
    [InlineData("2024-01-01T00:00:00 TT", TimeScale.TDB)]
    [InlineData("2024-01-01T00:00:00 TDT", TimeScale.UTC)]
    [InlineData("2024-01-01T00:00:00Z", TimeScale.TDB)]
    [InlineData("2024-01-01T00:00:00 UTC", TimeScale.TAI)]
    [InlineData("2024-01-01T02:00:00+02:00", TimeScale.GPS)]
    [InlineData("2024-001T00:00:00 TDB", TimeScale.TT)]
    [InlineData("JD 2460310.5 TAI", TimeScale.TDB)]
    [InlineData("2460310.5 JD GPS", TimeScale.UTC)]
    public void Parse_WithAScale_RefusesAContradictingTag(string text, TimeScale scale)
    {
        var exception = Assert.Throws<FormatException>(() => TimeSystem.Time.Parse(text, scale));
        Assert.Contains(scale.ToString(), exception.Message);
        Assert.False(TimeSystem.Time.TryParse(text, scale, out var result));
        LabelAssert.Equal(default, result);
        // The string alone names its scale
        Assert.True(TimeSystem.Time.TryParse(text, out _));
    }

    [Fact]
    public void Parse_WithAScale_AcceptsTdtForTt()
    {
        LabelAssert.Equal(new TimeSystem.Time(2024, 1, 1, TimeScale.TT), TimeSystem.Time.Parse("2024-01-01T00:00:00 TDT", TimeScale.TT));
    }

    /// <summary>
    /// D3.4 of #668: <c>Parse</c> throws <see cref="FormatException"/> only and <c>TryParse</c> never throws. A Julian date
    /// of 17 or more integer digits overflowed the decimal product with the ticks of a day (7.9e28 at most) before the range
    /// check: <see cref="OverflowException"/> from both.
    /// </summary>
    [Theory]
    [InlineData("JD 100000000000000000000 TDB")]
    [InlineData("JD 79228162514264337593543950335 TDB")]
    [InlineData("JD -79228162514264337593543950335 TT")]
    [InlineData("99999999999999999 JD UTC")]
    [InlineData("JD 5373484.5 TDB")]
    [InlineData("JD 1721425.4 TDB")]
    public void Parse_OfAJulianDateOutOfRange_ThrowsAFormatException_AndTryParseReturnsFalse(string text)
    {
        Assert.Throws<FormatException>(() => TimeSystem.Time.Parse(text));
        Assert.Throws<FormatException>(() => TimeSystem.Time.Parse(text, TimeScale.TDB));
        Assert.False(TimeSystem.Time.TryParse(text, out _));
        Assert.False(TimeSystem.Time.TryParse(text.AsSpan(), null, out _));
        Assert.False(TimeSystem.Time.TryParse(text, TimeScale.TDB, out _));
    }

    /// <summary>
    /// The forms refused: no tag, a tag after a zone, a second 60, a field out of range, the lenient forms of
    /// <see cref="DateTime.TryParse(string, out DateTime)"/> that <c>Time(string)</c> read before 11.0.0.
    /// </summary>
    [Theory]
    [InlineData("2024-06-21T12:00:00")]
    [InlineData("2451545.0 JD")]
    [InlineData("2024-06-21T12:00:00Z UTC")]
    [InlineData("2024-06-21T12:00:00+02:00 TDB")]
    [InlineData("2016-12-31T23:59:60 TAI")]
    [InlineData("2023-366T00:00:00 UTC")]
    [InlineData("2024-000T00:00:00 UTC")]
    [InlineData("2024-02-30T00:00:00 UTC")]
    [InlineData("2024-06-21T24:00:00 UTC")]
    [InlineData("2024-06-21T12:00:00+15:00")]
    [InlineData("0000-01-01T00:00:00 TDB")]
    [InlineData("06/21/2024 12:00:00 UTC")]
    [InlineData("Fri, 21 Jun 2024 12:00:00 GMT")]
    [InlineData("2024-06-21T12:00:00 GMT")]
    [InlineData("2024-06-21T12:00:00 TCG")]
    [InlineData("2024-6-21T12:00:00 UTC")]
    [InlineData("")]
    [InlineData("UTC")]
    [InlineData("JD TDB")]
    public void Parse_RefusesWithAFormatException_AndTryParseReturnsFalse(string text)
    {
        Assert.Throws<FormatException>(() => TimeSystem.Time.Parse(text));
        Assert.False(TimeSystem.Time.TryParse(text, out var result));
        LabelAssert.Equal(default, result);
        Assert.False(TimeSystem.Time.TryParse(text.AsSpan(), null, out _));
    }

    [Fact]
    public void Parse_GivesItsScaleToAnUntaggedString()
    {
        LabelAssert.Equal(new TimeSystem.Time(Calendar, TimeScale.TDB), TimeSystem.Time.Parse("2024-06-21T12:00:00", TimeScale.TDB));
        LabelAssert.Equal(new TimeSystem.Time(Calendar, TimeScale.UTC), TimeSystem.Time.Parse("2024-06-21T12:00:00", TimeScale.UTC));
        LabelAssert.Equal(new TimeSystem.Time(2000, 1, 1, 12, 0, 0, TimeScale.TT), TimeSystem.Time.Parse("2451545.0 JD", TimeScale.TT));
        Assert.True(TimeSystem.Time.TryParse("2024-173T12:00:00", TimeScale.GPS, out var gps));
        LabelAssert.Equal(new TimeSystem.Time(Calendar, TimeScale.GPS), gps);
    }

    [Fact]
    public void Parse_OfNull_ThrowsArgumentNull_AndTryParseReturnsFalse()
    {
        Assert.Throws<ArgumentNullException>(() => TimeSystem.Time.Parse(null));
        Assert.Throws<ArgumentNullException>(() => TimeSystem.Time.Parse(null, TimeScale.TDB));
        Assert.False(TimeSystem.Time.TryParse(null, out _));
        Assert.False(TimeSystem.Time.TryParse(null, TimeScale.TDB, out _));
        Assert.False(TimeSystem.Time.TryParse((string)null, CultureInfo.InvariantCulture, out _));
    }

    [Fact]
    public void Parse_OfASecondSixty_SaysSo()
    {
        var exception = Assert.Throws<FormatException>(() => TimeSystem.Time.Parse("2016-12-31T23:59:60Z"));
        Assert.Contains("second 60", exception.Message);
    }

    [Fact]
    public void Time_IsSpanParsable()
    {
        LabelAssert.Equal(new TimeSystem.Time(Calendar, TimeScale.UTC), ParseGeneric<TimeSystem.Time>("2024-06-21T12:00:00Z"));
    }

    private static T ParseGeneric<T>(string text) where T : ISpanParsable<T> => T.Parse(text.AsSpan(), CultureInfo.InvariantCulture);

    [Fact]
    public void Format_IsADateTimeFormatThenTheTag()
    {
        var tdb = new TimeSystem.Time(Calendar, TimeScale.TDB);
        var utc = new TimeSystem.Time(Calendar, TimeScale.UTC);
        Assert.Equal("2024-06-21 TDB", tdb.ToString("yyyy-MM-dd", null));
        Assert.Equal("2024-06-21Z", utc.ToString("yyyy-MM-dd", null));
        Assert.Equal("2024-06-21 12:00 TT", $"{new TimeSystem.Time(Calendar, TimeScale.TT):yyyy-MM-dd HH:mm}");
        foreach (var format in new[] { null, "", "G", "O", "o" })
        {
            Assert.Equal(tdb.ToString(), tdb.ToString(format, CultureInfo.InvariantCulture));
            Assert.Equal(utc.ToString(), utc.ToString(format, null));
        }

        Assert.Equal(tdb.ToString(), $"{tdb}");
        Span<char> buffer = stackalloc char[64];
        Assert.True(tdb.TryFormat(buffer, out int written, "yyyy", null));
        Assert.Equal("2024 TDB", buffer[..written].ToString());
        Assert.False(tdb.TryFormat(buffer[..3], out written, "yyyy", null));
        Assert.Equal(0, written);
    }

    /// <summary>
    /// D3.5 of #668: a format other than null, G and O is a custom format of <see cref="DateTime"/>. A standard format (one
    /// letter: <c>u</c> and <c>R</c> write their own UTC designator before the tag) and a zone specifier (<c>z</c>, <c>zz</c>,
    /// <c>zzz</c>, <c>K</c>: the machine's offset for a calendar of kind Unspecified, a second Z after a UTC one) are
    /// refused with <see cref="FormatException"/>; a quoted or escaped letter is a literal.
    /// </summary>
    [Theory]
    [InlineData("u")]
    [InlineData("R")]
    [InlineData("r")]
    [InlineData("s")]
    [InlineData("d")]
    [InlineData("U")]
    [InlineData("yyyy-MM-ddTHH:mm:sszzz")]
    [InlineData("yyyy-MM-ddTHH:mm:ssK")]
    [InlineData("HH:mm z")]
    [InlineData("HH:mm 'h' zz")]
    [InlineData("%K")]
    [InlineData("%z")]
    public void Format_RefusesAStandardFormatAndAZoneSpecifier(string format)
    {
        foreach (var scale in new[] { TimeScale.TDB, TimeScale.TT, TimeScale.TAI, TimeScale.UTC, TimeScale.GPS })
        {
            var time = new TimeSystem.Time(Calendar, scale);
            Assert.Throws<FormatException>(() => time.ToString(format, CultureInfo.InvariantCulture));
            Assert.Throws<FormatException>(() => time.TryFormat(new char[64], out _, format, null));
        }
    }

    [Fact]
    public void Format_ReadsQuotedAndEscapedZoneLettersAsLiterals()
    {
        var tdb = new TimeSystem.Time(Calendar, TimeScale.TDB);
        Assert.Equal("12:00 z TDB", tdb.ToString("HH:mm 'z'", null));
        Assert.Equal("12:00 K TDB", tdb.ToString("HH:mm \\K", null));
        Assert.Equal("12:00 zzz TDB", tdb.ToString("HH:mm \"zzz\"", null));
        Assert.Equal("2024-06-21T12:00:00 TT", new TimeSystem.Time(Calendar, TimeScale.TT).ToString("yyyy-MM-ddTHH:mm:ss", null));
        Assert.Equal("21 TDB", tdb.ToString("%d", null));
        LabelAssert.Equal(tdb, TimeSystem.Time.Parse(tdb.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", null)));
    }

    [Fact]
    public void EqualsExact_ComparesTheLabels_EqualsTheInstants()
    {
        // 2017-01-01T00:00:00Z is TAI 00:00:37 (TAI − UTC = 37 s)
        var utc = new TimeSystem.Time(2017, 1, 1, TimeScale.UTC);
        var tai = new TimeSystem.Time(2017, 1, 1, 0, 0, 37, TimeScale.TAI);
        Assert.True(utc.Equals(tai));
        Assert.False(utc.EqualsExact(tai));
        Assert.True(utc.EqualsExact(new TimeSystem.Time(2017, 1, 1, TimeScale.UTC)));
        Assert.False(utc.EqualsExact(utc.AddTicks(1)));

        var byInstant = new HashSet<TimeSystem.Time> { utc, tai };
        var byLabel = new HashSet<TimeSystem.Time>(TimeSystem.Time.ExactComparer) { utc, tai, tai.ToUTC() };
        Assert.Single(byInstant);
        Assert.Equal(2, byLabel.Count);
        Assert.True(TimeSystem.Time.ExactComparer.Equals(utc, tai.ToUTC()));
        Assert.Equal(TimeSystem.Time.ExactComparer.GetHashCode(utc), TimeSystem.Time.ExactComparer.GetHashCode(tai.ToUTC()));
    }

    [Fact]
    public void ToTT_IsTaiPlus32Point184Seconds()
    {
        var tai = new TimeSystem.Time(Calendar, TimeScale.TAI);
        LabelAssert.Equal(new TimeSystem.Time(Calendar.AddTicks(321840000), TimeScale.TT), tai.ToTT());
        LabelAssert.Equal(tai.ToTT(), tai.ConvertTo(TimeScale.TT));
    }

    [Fact]
    public void TwoPiAndHalfPi_AreTheirDoubles()
    {
        Assert.Equal(System.Math.Tau, IO.Astrodynamics.Constants.TwoPi);
        Assert.Equal(System.Math.PI / 2.0, IO.Astrodynamics.Constants.HalfPi);
    }
}
