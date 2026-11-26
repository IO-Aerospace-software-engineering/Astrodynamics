// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.OrbitalParameters;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// The verbs of a TLE text (R21 of #668, the verb set shared with the CCSDS messages): <see cref="Tle.Parse(string)"/>,
/// <see cref="Tle.ParseAll(string)"/>, <see cref="Tle.Load"/>, <see cref="Tle.LoadAll"/>, <see cref="Tle.TryParse(string, out Tle)"/>,
/// <see cref="Tle.TryLoad"/> and <see cref="IParsable{TSelf}"/>: two- and three-line sets, CR/LF, trailing blanks, blank
/// lines, Space-Track's <c>0 </c> before a name; each set equals the set the constructor builds from its lines, and a bad set
/// throws <see cref="TleFormatException"/> at its line of the text.
/// </summary>
public class TleVerbsTests
{
    private const string Iss1 = "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054";
    private const string Iss2 = "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703";
    private const string Alpha1 = "1 T5544U 98067A   24173.50000000  .00016717  00000-0  10270-3 0  9052";
    private const string Alpha2 = "2 T5544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25701";
    private const string Vallado1 = "1 11801U          80230.29629788  .01431103  00000-0  14311-1      13";
    private const string Vallado2 = "2 11801  46.7916 230.4354 7318036  47.4722  10.4117  2.28537848    13";

    public TleVerbsTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static void AssertSame(Tle expected, Tle actual)
    {
        Assert.Equal(expected, actual);
        Assert.Equal(expected.Name, actual.Name);
    }

    [Fact]
    public void Parse_TwoAndThreeLines_EqualsTheConstructor()
    {
        AssertSame(new Tle(null, Iss1, Iss2), Tle.Parse($"{Iss1}\n{Iss2}"));
        AssertSame(new Tle("ISS (ZARYA)", Iss1, Iss2), Tle.Parse($"ISS (ZARYA)\n{Iss1}\n{Iss2}\n"));
        // CR/LF, trailing blanks, blank lines, a trimmed name, Space-Track's "0 " before it
        AssertSame(new Tle("ISS (ZARYA)", Iss1, Iss2), Tle.Parse($"\r\n0 ISS (ZARYA)   \r\n{Iss1}  \r\n\r\n{Iss2}\t\r\n"));
        AssertSame(new Tle("ISS", Iss1, Iss2), Tle.Parse(new StringReader($"  ISS  \n{Iss1}\n{Iss2}")));
        Assert.Equal("25544", Tle.Parse($"{Iss1}\n{Iss2}").Name);
    }

    [Fact]
    public void ParseAll_MixedSets_InTheirOrder()
    {
        var text = $"ISS\n{Iss1}\n{Iss2}\n\n{Alpha1}\n{Alpha2}\r\n0 11801\r\n{Vallado1}\r\n{Vallado2}";
        var all = Tle.ParseAll(text);
        Assert.Equal(3, all.Count);
        AssertSame(new Tle("ISS", Iss1, Iss2), all[0]);
        AssertSame(new Tle(null, Alpha1, Alpha2), all[1]);
        AssertSame(new Tle("11801", Vallado1, Vallado2), all[2]);
        Assert.Equal(all, Tle.ParseAll(new StringReader(text)));
        Assert.Empty(Tle.ParseAll("\n  \r\n"));
    }

    [Fact]
    public void LoadAll_AndLoad_ReadAFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tle-verbs-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, $"ISS\n{Iss1}\n{Iss2}\n{Alpha1}\n{Alpha2}\n");
            var all = Tle.LoadAll(path);
            Assert.Equal(new[] { 25544, 275544 }, all.Select(t => t.NoradCatalogId));

            File.WriteAllText(path, $"ISS\r\n{Iss1}\r\n{Iss2}\r\n");
            AssertSame(new Tle("ISS", Iss1, Iss2), Tle.Load(path));
            Assert.True(Tle.TryLoad(path, out var loaded));
            AssertSame(new Tle("ISS", Iss1, Iss2), loaded);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ABadSet_ThrowsTleFormatException_AtItsLineOfTheText()
    {
        // The third set's line 2 has a wrong checksum: line 9 of the text, column 69; no partial list
        var badChecksum = Vallado2.Substring(0, 68) + "4";
        var text = $"ISS\n{Iss1}\n{Iss2}\n\n{Alpha1}\n{Alpha2}\n11801\n{Vallado1}\n{badChecksum}\n";
        var exception = Assert.Throws<TleFormatException>(() => Tle.ParseAll(text));
        Assert.Equal((9, 69), (exception.Line, exception.Column));
        Assert.StartsWith("Checksum validation failed for TLE line 2", exception.Message);

        var path = Path.Combine(Path.GetTempPath(), $"tle-verbs-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, text);
            var fromFile = Assert.Throws<TleFormatException>(() => Tle.LoadAll(path));
            Assert.Equal((9, 69), (fromFile.Line, fromFile.Column));
            Assert.False(Tle.TryLoad(path, out var none));
            Assert.Null(none);
        }
        finally
        {
            File.Delete(path);
        }

        // A field of line 1 of the second set: the epoch's day 400 at line 3, column 19
        var badEpoch = "1 25544U 98067A   21400.53488036  .00016717  00000-0  10270-3 0  9056";
        var field = Assert.Throws<TleFormatException>(() => Tle.ParseAll($"{Alpha1}\n{Alpha2}\n{badEpoch}\n{Iss2}"));
        Assert.Equal((3, 19), (field.Line, field.Column));

        // A name without its lines, at the end of the text: the line after it
        var truncated = Assert.Throws<TleFormatException>(() => Tle.ParseAll($"{Iss1}\n{Iss2}\nISS\n{Iss1}"));
        Assert.Equal((5, 1), (truncated.Line, truncated.Column));
    }

    [Fact]
    public void Parse_TakesOneSet()
    {
        var two = Assert.Throws<TleFormatException>(() => Tle.Parse($"{Iss1}\n{Iss2}\n{Alpha1}\n{Alpha2}"));
        Assert.Equal((3, 1), (two.Line, two.Column));
        Assert.Contains("ParseAll", two.Message);
        var none = Assert.Throws<TleFormatException>(() => Tle.Parse(" \n"));
        Assert.Equal((1, 1), (none.Line, none.Column));
        Assert.Throws<ArgumentNullException>(() => Tle.Parse((string)null));
    }

    [Fact]
    public void TryParse_NeverThrowsForBadText()
    {
        Assert.True(Tle.TryParse($"ISS\n{Iss1}\n{Iss2}", out var tle));
        AssertSame(new Tle("ISS", Iss1, Iss2), tle);
        Assert.False(Tle.TryParse($"ISS\n{Iss1}\n{Iss2.Substring(0, 68)}0", out var bad));
        Assert.Null(bad);
        Assert.False(Tle.TryParse(null, out _));
        Assert.False(Tle.TryParse("", out _));
        Assert.False(Tle.TryLoad(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.txt"), out _));
    }

    private static T ParseGeneric<T>(string s) where T : IParsable<T> => T.Parse(s, null);

    private static bool TryParseGeneric<T>(string s, out T result) where T : IParsable<T> => T.TryParse(s, null, out result);

    [Fact]
    public void IParsable_IsParse()
    {
        AssertSame(new Tle("ISS", Iss1, Iss2), ParseGeneric<Tle>($"ISS\n{Iss1}\n{Iss2}"));
        Assert.True(TryParseGeneric<Tle>($"{Iss1}\n{Iss2}", out var tle));
        Assert.Equal("25544", tle.Name);
        Assert.False(TryParseGeneric<Tle>("ISS", out _));
    }
}
