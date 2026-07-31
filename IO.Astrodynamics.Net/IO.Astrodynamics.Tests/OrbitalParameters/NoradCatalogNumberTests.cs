// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.OrbitalParameters.TLE;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// Tests for the 5-character NORAD catalog number field, including the Alpha-5 convention
/// used above 99999.
/// </summary>
public class NoradCatalogNumberTests
{
    #region Format - numeric range

    [Theory]
    [InlineData(0, "00000")]
    [InlineData(1, "00001")]
    [InlineData(5, "00005")]
    [InlineData(900, "00900")]
    [InlineData(9999, "09999")]
    [InlineData(10000, "10000")]
    [InlineData(25544, "25544")] // ISS
    [InlineData(65535, "65535")] // ushort.MaxValue - the former ceiling
    [InlineData(65536, "65536")] // first value the old ushort could not hold
    [InlineData(99998, "99998")]
    [InlineData(99999, "99999")] // last purely numeric catalog number
    public void Format_NumericRange_ProducesZeroPaddedDigits(int number, string expected)
    {
        Assert.Equal(expected, NoradCatalogNumber.Format(number));
    }

    #endregion

    #region Format - Alpha-5 range

    [Theory]
    [InlineData(100000, "A0000")] // first Alpha-5 value
    [InlineData(100001, "A0001")]
    [InlineData(109999, "A9999")]
    [InlineData(110000, "B0000")]
    [InlineData(148493, "E8493")] // reference example published by the 18th SDS
    [InlineData(179999, "H9999")]
    [InlineData(180000, "J0000")] // 'I' is skipped
    [InlineData(229999, "N9999")]
    [InlineData(230000, "P0000")] // 'O' is skipped
    [InlineData(275544, "T5544")]
    [InlineData(301234, "W1234")]
    [InlineData(330000, "Z0000")]
    [InlineData(339999, "Z9999")] // largest value the TLE field can carry
    public void Format_Alpha5Range_UsesLetterPrefix(int number, string expected)
    {
        Assert.Equal(expected, NoradCatalogNumber.Format(number));
    }

    [Fact]
    public void Format_SkipsAmbiguousLetters_INotUsed()
    {
        // 'I' would sit between 'H' (17) and 'J' (18); the value 180000 must map to 'J'.
        Assert.Equal("H0000", NoradCatalogNumber.Format(170000));
        Assert.Equal("J0000", NoradCatalogNumber.Format(180000));
        Assert.DoesNotContain("I", NoradCatalogNumber.Format(180000), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_SkipsAmbiguousLetters_ONotUsed()
    {
        // 'O' would sit between 'N' (22) and 'P' (23); the value 230000 must map to 'P'.
        Assert.Equal("N0000", NoradCatalogNumber.Format(220000));
        Assert.Equal("P0000", NoradCatalogNumber.Format(230000));
        Assert.DoesNotContain("O", NoradCatalogNumber.Format(230000), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_LetterValues_MatchThePublishedAlpha5Table()
    {
        // Full reference table: letter -> value, 'A' = 10 up to 'Z' = 33, 'I' and 'O' excluded.
        var expected = new (char Letter, int Value)[]
        {
            ('A', 10), ('B', 11), ('C', 12), ('D', 13), ('E', 14), ('F', 15),
            ('G', 16), ('H', 17), ('J', 18), ('K', 19), ('L', 20), ('M', 21),
            ('N', 22), ('P', 23), ('Q', 24), ('R', 25), ('S', 26), ('T', 27),
            ('U', 28), ('V', 29), ('W', 30), ('X', 31), ('Y', 32), ('Z', 33)
        };

        foreach (var (letter, value) in expected)
        {
            Assert.Equal($"{letter}0000", NoradCatalogNumber.Format(value * 10000));
            Assert.Equal($"{letter}9999", NoradCatalogNumber.Format(value * 10000 + 9999));
            Assert.Equal(value * 10000, NoradCatalogNumber.Parse($"{letter}0000"));
        }
    }

    [Fact]
    public void Format_AlwaysProducesExactlyFiveCharacters()
    {
        // Column alignment of the TLE line depends on this invariant. Sample the whole domain.
        for (int number = 0; number <= NoradCatalogNumber.MaxValue; number += 7)
        {
            Assert.Equal(NoradCatalogNumber.FieldLength, NoradCatalogNumber.Format(number).Length);
        }

        Assert.Equal(NoradCatalogNumber.FieldLength, NoradCatalogNumber.Format(NoradCatalogNumber.MaxValue).Length);
    }

    #endregion

    #region Format - out of range

    [Theory]
    [InlineData(-1)]
    [InlineData(-25544)]
    [InlineData(int.MinValue)]
    [InlineData(340000)] // one past "Z9999"
    [InlineData(999999)]
    [InlineData(int.MaxValue)]
    public void Format_OutOfRange_ThrowsArgumentOutOfRangeException(int number)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => NoradCatalogNumber.Format(number));
        Assert.Equal("number", ex.ParamName);
    }

    #endregion

    #region Parse

    [Theory]
    [InlineData("00000", 0)]
    [InlineData("00001", 1)]
    [InlineData("25544", 25544)]
    [InlineData("65535", 65535)]
    [InlineData("65536", 65536)]
    [InlineData("99999", 99999)]
    [InlineData("A0000", 100000)]
    [InlineData("E8493", 148493)]
    [InlineData("J0000", 180000)]
    [InlineData("N0000", 220000)]
    [InlineData("P0000", 230000)]
    [InlineData("T5544", 275544)]
    [InlineData("Z9999", 339999)]
    public void Parse_ValidField_ReturnsCatalogNumber(string field, int expected)
    {
        Assert.Equal(expected, NoradCatalogNumber.Parse(field));
    }

    [Theory]
    [InlineData("  123", 123)] // space-padded numbers appear in older catalogue dumps
    [InlineData(" 4632", 4632)]
    [InlineData("25544 ", 25544)]
    [InlineData(" 5000 ", 5000)]
    public void Parse_SpacePaddedNumericField_IsTolerated(string field, int expected)
    {
        Assert.Equal(expected, NoradCatalogNumber.Parse(field));
    }

    [Theory]
    [InlineData("t5544", 275544)]
    [InlineData("e8493", 148493)]
    [InlineData("z9999", 339999)]
    public void Parse_LowercaseAlpha5_IsTolerated(string field, int expected)
    {
        Assert.Equal(expected, NoradCatalogNumber.Parse(field));
    }

    [Theory]
    [InlineData("I0000")] // ambiguous with '1' - not part of the alphabet
    [InlineData("O0000")] // ambiguous with '0' - not part of the alphabet
    [InlineData("i0000")]
    [InlineData("o0000")]
    public void Parse_AmbiguousLetters_AreRejected(string field)
    {
        Assert.Throws<FormatException>(() => NoradCatalogNumber.Parse(field));
    }

    [Theory]
    [InlineData("")]
    [InlineData("     ")]
    [InlineData("1A234")] // letter outside the leading position
    [InlineData("123A4")]
    [InlineData("1234A")]
    [InlineData("12 34")] // embedded space
    [InlineData("A 000")]
    [InlineData("A123")] // Alpha-5 requires exactly 4 trailing digits
    [InlineData("A12345")]
    [InlineData("123456")] // wider than the field
    [InlineData("-1234")]
    [InlineData("+1234")]
    [InlineData("AAAAA")]
    [InlineData("#0000")]
    public void Parse_MalformedField_ThrowsFormatException(string field)
    {
        Assert.Throws<FormatException>(() => NoradCatalogNumber.Parse(field));
    }

    [Fact]
    public void Parse_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => NoradCatalogNumber.Parse(null));
    }

    [Fact]
    public void TryParse_MalformedField_ReturnsFalseAndZero()
    {
        Assert.False(NoradCatalogNumber.TryParse("1A234", out int number));
        Assert.Equal(0, number);

        Assert.False(NoradCatalogNumber.TryParse(null, out number));
        Assert.Equal(0, number);
    }

    [Fact]
    public void TryParse_ValidField_ReturnsTrue()
    {
        Assert.True(NoradCatalogNumber.TryParse("T5544", out int number));
        Assert.Equal(275544, number);
    }

    #endregion

    #region Round trip

    [Fact]
    public void FormatThenParse_RoundTripsOverEntireDomain()
    {
        // Exhaustive: every representable catalog number must survive encode/decode unchanged.
        for (int number = 0; number <= NoradCatalogNumber.MaxValue; number++)
        {
            var field = NoradCatalogNumber.Format(number);
            Assert.Equal(number, NoradCatalogNumber.Parse(field));
        }
    }

    [Fact]
    public void Format_IsInjectiveOverEntireDomain()
    {
        // Two distinct catalog numbers must never collapse onto the same field, otherwise
        // two objects would become indistinguishable in TLE form.
        var seen = new System.Collections.Generic.HashSet<string>(NoradCatalogNumber.MaxValue + 1);
        for (int number = 0; number <= NoradCatalogNumber.MaxValue; number++)
        {
            Assert.True(seen.Add(NoradCatalogNumber.Format(number)),
                $"Catalog number {number} produced a duplicate field.");
        }
    }

    #endregion

    #region Boundaries

    [Fact]
    public void Constants_MatchTleFieldDefinition()
    {
        Assert.Equal(5, NoradCatalogNumber.FieldLength);
        Assert.Equal(99999, NoradCatalogNumber.MaxNumericValue);
        Assert.Equal(339999, NoradCatalogNumber.MaxValue);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(65535, true)]
    [InlineData(65536, true)]
    [InlineData(99999, true)]
    [InlineData(100000, true)]
    [InlineData(339999, true)]
    [InlineData(340000, false)]
    [InlineData(-1, false)]
    public void IsValid_MatchesRepresentableRange(int number, bool expected)
    {
        Assert.Equal(expected, NoradCatalogNumber.IsValid(number));
    }

    #endregion
}
