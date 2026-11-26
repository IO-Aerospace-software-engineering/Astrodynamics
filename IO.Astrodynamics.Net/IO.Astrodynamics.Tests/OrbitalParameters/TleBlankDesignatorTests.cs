// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using IO.Astrodynamics.CCSDS.OMM;
using IO.Astrodynamics.OrbitalParameters.TLE;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// A TLE whose international designator, columns 10 to 17 of line 1, is blank (#604, decided by S. Guillet, 2026-11-18):
/// <see cref="Tle.Create"/> takes a null or blank <c>cosparId</c> and writes eight blanks there, <see cref="Tle.ToOmm"/>
/// writes <c>OBJECT_ID = UNKNOWN</c> (the element is mandatory in the ODM; CCSDS's value for an unknown designator, to
/// be verified by S. Guillet), and <see cref="Omm.ToTle"/> reads <c>UNKNOWN</c> (trimmed, in any letter case) back as the
/// blank columns. Before 11.0.0 <c>Tle.Create</c> threw <c>ArgumentException</c> (<c>cosparId</c>) and <c>Tle.ToOmm</c>
/// "Object ID is required." (<c>objectId</c>). The TLEs are 11801 and 88888 of Vallado's <c>SGP4-VER.TLE</c>, the two of
/// the 15 919 TLEs of the study of lot 5 of #457 without a designator; the expected lines are their own text, each field
/// at its columns (NORAD's Spacetrack Report No. 3).
/// </summary>
public class TleBlankDesignatorTests
{
    private readonly ITestOutputHelper _output;

    public TleBlankDesignatorTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    public static TheoryData<string, string, string> VerificationTles => new()
    {
        { "11801", "1 11801U          80230.29629788  .01431103  00000-0  14311-1      13", "2 11801  46.7916 230.4354 7318036  47.4722  10.4117  2.28537848    13" },
        { "88888", "1 88888U          80275.98708465  .00073094  13844-3  66816-4 0    87", "2 88888  72.8435 115.9689 0086731  52.6988 110.5714 16.05824518  1058" }
    };

    /// <summary>
    /// The line <see cref="Tle.Create"/> writes for the text of a TLE: the text itself, but for column 63, the ephemeris
    /// type, which <c>Tle.Create</c> always writes 0 (SGP4's) where 11801 leaves it blank; a 0 adds nothing to the checksum.
    /// </summary>
    private static string AsCreated(string line1) => line1[62] == ' ' ? line1[..62] + "0" + line1[63..] : line1;

    private static Tle Recreate(Tle tle, string cosparId)
    {
        int elementSetNumber = int.Parse(tle.Line1.Substring(64, 4), CultureInfo.InvariantCulture);
        int revolutions = int.Parse(tle.Line2.Substring(63, 5), CultureInfo.InvariantCulture);
        return Tle.Create(tle.ToMeanKeplerianElements(), tle.Name, tle.NoradCatalogId, cosparId, revolutions, TleClassification.Unclassified,
            tle.BStar, tle.MeanMotionDot, tle.MeanMotionDDot, elementSetNumber);
    }

    [Theory]
    [MemberData(nameof(VerificationTles))]
    public void Create_NullOrBlankCosparId_WritesBlankColumns(string name, string line1, string line2)
    {
        var tle = new Tle(name, line1, line2);
        Assert.Equal(new string(' ', 8), tle.Line1.Substring(9, 8));

        foreach (var cosparId in new[] { null, "", " ", "        " })
        {
            var created = Recreate(tle, cosparId);

            Assert.Equal(AsCreated(line1), created.Line1);
            Assert.Equal(line2, created.Line2);
        }
    }

    [Theory]
    [MemberData(nameof(VerificationTles))]
    public void ToOmm_BlankDesignator_WritesUnknown_AndReadsBackTheTle(string name, string line1, string line2)
    {
        var tle = new Tle(name, line1, line2);

        var omm = tle.ToOmm();
        var xml = omm.ToXml();
        var read = Omm.Parse(xml);
        var back = read.ToTle();

        Assert.Equal("UNKNOWN", omm.ObjectId);
        Assert.Contains("<OBJECT_ID>UNKNOWN</OBJECT_ID>", xml);
        Assert.Equal("UNKNOWN", read.ObjectId);
        Assert.Equal(AsCreated(line1), back.Line1);
        Assert.Equal(line2, back.Line2);
        _output.WriteLine($"MEASURE #604 {name}: {back.Line1}");
    }

    /// <summary>
    /// The fit of <see cref="Astrodynamics.OrbitalParameters.OrbitalParameters.ToTle"/> writes a null or blank
    /// the <c>cosparId</c> of <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.ToTle"/> as the blank columns 10 to 17, as <see cref="Tle.Create"/> does (#604): the
    /// lines are those of the same fit with a designator, but for those columns and the checksum of line 1, which the
    /// designator's digits enter (NORAD's Spacetrack Report No. 3: the sum of the digits, each minus sign 1, modulo 10).
    /// Before 11.0.0 the <c>ArgumentException</c> of the designator was caught inside the fit, which threw
    /// "TLE conversion using Equinoctial elements failed" (<c>InvalidOperationException</c>).
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToTLE_NullOrBlankCosparId_WritesBlankColumns(string cosparId)
    {
        var iss = new Tle("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var state = iss.ToOsculating();

        var withDesignator = state.ToTle("ISS", 25544, "98067A");
        var blank = state.ToTle("ISS", 25544, cosparId);

        Assert.Equal(new string(' ', 8), blank.Line1.Substring(9, 8));
        Assert.Equal(withDesignator.Line1[..9], blank.Line1[..9]);
        Assert.Equal(withDesignator.Line1[17..68], blank.Line1[17..68]);
        Assert.Equal(Checksum(blank.Line1[..68]), blank.Line1[68] - '0');
        Assert.Equal(withDesignator.Line2, blank.Line2);
        // The TLE reads back: its constructor checks the lengths, line numbers, catalog numbers and checksums (#675)
        var read = new Tle("ISS", blank.Line1, blank.Line2);
        Assert.Equal(new string(' ', 8), read.Line1.Substring(9, 8));
        _output.WriteLine($"MEASURE #604 ToTle '{cosparId}': {blank.Line1}");
    }

    /// <summary>The TLE checksum of Spacetrack Report No. 3: the sum of the digits, each minus sign 1, modulo 10.</summary>
    private static int Checksum(string columns)
    {
        int sum = 0;
        foreach (var c in columns)
        {
            if (char.IsAsciiDigit(c)) sum += c - '0';
            else if (c == '-') sum += 1;
        }

        return sum % 10;
    }

    /// <summary>
    /// <c>OBJECT_ID</c> <c>UNKNOWN</c> in any letter case and with surrounding blanks is the blank designator; any other
    /// value keeps its conversion, a designator of the OMM form to the TLE form.
    /// </summary>
    [Theory]
    [InlineData("UNKNOWN", "        ")]
    [InlineData("unknown", "        ")]
    [InlineData(" Unknown ", "        ")]
    [InlineData("1998-067A", "98067A  ")]
    [InlineData("98067A", "98067A  ")]
    public void ToTle_ObjectId_MapsToTheDesignatorColumns(string objectId, string columns)
    {
        var source = new Tle("88888", "1 88888U          80275.98708465  .00073094  13844-3  66816-4 0    87",
            "2 88888  72.8435 115.9689 0086731  52.6988 110.5714 16.05824518  1058").ToOmm();
        var omm = new Omm(source.Header, new OmmMetadata("88888", objectId, "EARTH", "TEME", "UTC", "SGP4"), source.Data);

        var tle = omm.ToTle();

        Assert.Equal(columns, tle.Line1.Substring(9, 8));
    }

    /// <summary>
    /// The internal conversion of an <c>OBJECT_ID</c> to the designator columns: <c>UNKNOWN</c> and an empty or blank value
    /// are the blank designator (null). An empty <c>OBJECT_ID</c> does not reach it through the public surface: the reader
    /// and <see cref="OmmMetadata"/> refuse it (#708, CCSDS 502.0-B-3 7.5.1: every metadata value is non-empty).
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UNKNOWN")]
    [InlineData("\tunknown\n")]
    public void ObjectIdToDesignator_UnknownOrBlank_IsBlank(string objectId)
    {
        Assert.Null(Omm.ConvertObjectIdToCosparId(objectId));
    }

    /// <summary>A designator that is neither blank nor YYNNN and one to three capital letters is refused, naming
    /// <c>cosparId</c> (the syntax of columns 10-17, R21 of #668).</summary>
    [Theory]
    [InlineData("A")]
    [InlineData("98067")]
    [InlineData("98067ABCD")]
    [InlineData("ABCDEF")]
    [InlineData("98067a")]
    public void Create_DesignatorOutsideItsSyntax_Throws(string cosparId)
    {
        var tle = new Tle("88888", "1 88888U          80275.98708465  .00073094  13844-3  66816-4 0    87",
            "2 88888  72.8435 115.9689 0086731  52.6988 110.5714 16.05824518  1058");

        var ex = Assert.Throws<ArgumentException>(() => Recreate(tle, cosparId));
        Assert.Equal("cosparId", ex.ParamName);
    }

    /// <summary>The OMM of a blank designator validates as the ODM requires: <c>OBJECT_ID</c> is present and non-empty.</summary>
    [Fact]
    public void ToOmm_BlankDesignator_HasNoObjectIdError()
    {
        var omm = new Tle("11801", "1 11801U          80230.29629788  .01431103  00000-0  14311-1      13",
            "2 11801  46.7916 230.4354 7318036  47.4722  10.4117  2.28537848    13").ToOmm();

        Assert.DoesNotContain(omm.Validate().Errors, e => e.Path == "Metadata.ObjectId");
    }
}
