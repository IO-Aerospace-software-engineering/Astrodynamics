// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.OrbitalParameters.TLE;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// The text of a TLE (R21 of #668): every field checked against the format before any member reads it, a bad line throwing
/// <see cref="ArgumentException"/> (<c>line1</c> or <c>line2</c>) whose inner <see cref="TleFormatException"/> names the
/// line and the column of the field; the name of a set without one, the catalog field; the fields of line 1 and 2 the
/// properties read. The expected values are the columns of the format (CelesTrak's "NORAD Two-Line Element Set Format",
/// Vallado et al. 2006, appendix) and the text of the lines.
/// </summary>
public class TleFieldsTests
{
    private const string Line1 = "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054";
    private const string Line2 = "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703";

    public TleFieldsTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    // The line with the field at 1-based column `column` replaced by `text`, its checksum recomputed: the line passes every
    // check but that of the field
    private static string WithField(string line, int column, string text)
    {
        var body = line.Substring(0, column - 1) + text + line.Substring(column - 1 + text.Length, 68 - (column - 1 + text.Length));
        int sum = body.Sum(c => char.IsDigit(c) ? c - '0' : c == '-' ? 1 : 0);
        return body + (char)('0' + sum % 10);
    }

    public static TheoryData<int, int, string> MalformedFields => new()
    {
        // line, column of the field, the field's text
        { 1, 8, "X" }, // classification: U, C or S
        { 1, 10, "9X067A  " }, // international designator: YYNNNPPP or blank
        { 1, 19, "21X20.53488036" }, // epoch
        { 1, 19, "21000.53488036" }, // day 0
        { 1, 19, "21366.53488036" }, // day 366 of 2021
        { 1, 19, "21400.53488036" }, // day 400
        { 1, 19, "2102053488036 " }, // no decimal point
        { 1, 34, " .0001X717" }, // first derivative
        { 1, 45, " 0000X-0" }, // second derivative
        { 1, 54, " 10270*3" }, // B*
        { 1, 54, " 1027-03" }, // B*: a two-digit exponent
        { 1, 63, "X" }, // ephemeris type
        { 1, 65, " 9A5" }, // element set number
        { 2, 9, " 51.64X3" }, // inclination
        { 2, 18, "353,0312" }, // right ascension of the ascending node
        { 2, 27, "00004X3" }, // eccentricity
        { 2, 27, "0.00049" }, // eccentricity: no decimal point in the field
        { 2, 35, "320.87X5" }, // argument of perigee
        { 2, 44, " 39.23X0" }, // mean anomaly
        { 2, 53, "15.4930942X" }, // mean motion
        { 2, 64, "25X0" }, // revolution number
    };

    [Theory]
    [MemberData(nameof(MalformedFields))]
    public void AMalformedField_ThrowsArgumentException_WithTheLineAndColumn(int line, int column, string text)
    {
        string line1 = line == 1 ? WithField(Line1, column, text) : Line1;
        string line2 = line == 2 ? WithField(Line2, column, text) : Line2;

        var exception = Assert.Throws<ArgumentException>(() => new TLE("ISS", line1, line2));
        Assert.Equal(line == 1 ? "line1" : "line2", exception.ParamName);
        var format = Assert.IsType<TleFormatException>(exception.InnerException);
        Assert.Equal(line, format.Line);
        Assert.Equal(column, format.Column);
        Assert.IsAssignableFrom<FormatException>(format);
    }

    [Fact]
    public void TheDayOfALeapYear_IsAccepted()
    {
        // Day 366 of 2020, the last of a leap year
        var tle = new TLE("ISS", WithField(Line1, 19, "20366.53488036"), Line2);
        Assert.Equal(new DateTime(2020, 12, 31), tle.Epoch.DateTime.Date);
    }

    [Fact]
    public void TheChecksLeadingTheFields_GiveArgumentException_WithTheLineAndColumn()
    {
        // The checks of #675 (length, line number, catalog number, the catalog of both lines, checksum), ArgumentException
        // since R21 of #668 (the checksum and the catalog mismatch threw InvalidOperationException before)
        Expect(Line1.Substring(0, 60), Line2, "line1", 1, 61, "TLE line 1 must be exactly 69 characters long");
        Expect(Line1, Line2 + " ", "line2", 2, 70, "TLE line 2 must be exactly 69 characters long");
        Expect("3" + Line1.Substring(1), Line2, "line1", 1, 1, "TLE line 1 must start with '1'");
        Expect(Line1, "1" + Line2.Substring(1), "line2", 2, 1, "TLE line 2 must start with '2'");
        Expect(WithField(Line1, 3, "2I544"), Line2, "line1", 1, 3, "Invalid NORAD catalog number");
        Expect(Line1, WithField(Line2, 3, "25545"), "line2", 2, 3, "NORAD catalog numbers differ");
        Expect(Line1.Substring(0, 68) + "5", Line2, "line1", 1, 69, "Checksum validation failed for TLE line 1");
        Expect(Line1, Line2.Substring(0, 68) + "X", "line2", 2, 69, "Invalid checksum character in line 2");
    }

    private static void Expect(string line1, string line2, string parameter, int line, int column, string message)
    {
        var exception = Assert.Throws<ArgumentException>(() => new TLE("ISS", line1, line2));
        Assert.Equal(parameter, exception.ParamName);
        Assert.StartsWith(message, exception.Message);
        var format = Assert.IsType<TleFormatException>(exception.InnerException);
        Assert.Equal((line, column), (format.Line, format.Column));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithoutAName_TheNameIsTheCatalogField(string name)
    {
        Assert.Equal("25544", new TLE(name, Line1, Line2).Name);
        Assert.Equal("T5544", new TLE(name, "1 T5544U 98067A   24173.50000000  .00016717  00000-0  10270-3 0  9052",
            "2 T5544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25701").Name);
    }

    [Fact]
    public void TheFieldsOfTheLines_AreProperties()
    {
        var tle = new TLE("ISS", Line1, Line2);
        Assert.Equal("98067A", tle.CosparId);
        Assert.Equal(905, tle.ElementSetNumber);
        Assert.Equal(2570, tle.RevolutionNumberAtEpoch);
        Assert.Equal(Classification.Unclassified, tle.Classification);
        Assert.Equal(Classification.Classified, new TLE("C", WithField(Line1, 8, "C"), Line2).Classification);
        Assert.Equal(Classification.Secret, new TLE("S", WithField(Line1, 8, "S"), Line2).Classification);

        // Vallado's 11801: no designator, a blank ephemeris type, an element set number and a revolution number of 1
        var vallado = new TLE("11801", "1 11801U          80230.29629788  .01431103  00000-0  14311-1      13",
            "2 11801  46.7916 230.4354 7318036  47.4722  10.4117  2.28537848    13");
        Assert.Null(vallado.CosparId);
        Assert.Equal(1, vallado.ElementSetNumber);
        Assert.Equal(1, vallado.RevolutionNumberAtEpoch);

        // Blank count columns read 0
        var blank = new TLE("ISS", WithField(Line1, 65, "    "), WithField(Line2, 64, "     "));
        Assert.Equal(0, blank.ElementSetNumber);
        Assert.Equal(0, blank.RevolutionNumberAtEpoch);
    }

    [Fact]
    public void Create_FromTheProperties_RebuildsTheLines()
    {
        var tle = new TLE("ISS", Line1, Line2);
        var again = TLE.Create(tle.ToMeanKeplerianElements(), tle.Name, tle.NoradCatalogId, tle.CosparId, tle.RevolutionNumberAtEpoch,
            tle.Classification, tle.BallisticCoefficient, tle.FirstDerivationMeanMotion, tle.SecondDerivativeMeanMotion, tle.ElementSetNumber);
        Assert.Equal(Line1, again.Line1);
        Assert.Equal(Line2, again.Line2);
    }
}
