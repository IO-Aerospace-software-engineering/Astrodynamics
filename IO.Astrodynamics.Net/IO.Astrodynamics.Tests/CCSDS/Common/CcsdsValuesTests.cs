using System;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OMM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.Common;

/// <summary>
/// The internal helper through which the OMM and OPM readers build their data model (fourth round of #668), on the paths
/// the readers cannot reach: every <see cref="ArgumentException"/> a reader's constructor can throw names a parameter
/// mapped to a keyword, the others being prevented by the reader's earlier checks, so the fallback message naming the
/// block, an exception without parameter and an exception that is not an <see cref="ArgumentException"/> are checked here.
/// </summary>
public class CcsdsValuesTests
{
    private static Exception Parse(string message, Exception inner) => new OmmParseException(message, inner);

    private static readonly CcsdsKeyword Eccentricity = new("eccentricity", "ECCENTRICITY", "-0.5");

    [Fact]
    public void Construct_ReturnsTheObjectBuilt()
    {
        var value = CcsdsValues.Construct(() => 42, "meanElements", Parse, Eccentricity);

        Assert.Equal(42, value);
    }

    [Fact]
    public void Construct_MappedParameter_NamesKeywordTextAndBlock()
    {
        var inner = new ArgumentOutOfRangeException("eccentricity", "Eccentricity must be non-negative.");

        var ex = Assert.Throws<OmmParseException>(() =>
            CcsdsValues.Construct<int>(() => throw inner, "meanElements", Parse, Eccentricity));

        Assert.Equal("ECCENTRICITY '-0.5' in meanElements is invalid: Eccentricity must be non-negative.", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void Construct_MappedParameterWithAnotherKeyword_NamesBoth()
    {
        var inner = new ArgumentOutOfRangeException("semiMajorAxis", "Semi-major axis must be positive.");
        var keyword = new CcsdsKeyword("semiMajorAxis", "SEMI_MAJOR_AXIS", "-7000", "ECCENTRICITY '0.1'");

        var ex = Assert.Throws<OmmParseException>(() =>
            CcsdsValues.Construct<int>(() => throw inner, "keplerianElements", Parse, Eccentricity, keyword));

        Assert.Equal("SEMI_MAJOR_AXIS '-7000' in keplerianElements with ECCENTRICITY '0.1' is invalid: "
                     + "Semi-major axis must be positive.", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void Construct_UnmappedParameter_NamesTheBlock()
    {
        var inner = new ArgumentException("Use mean motion or semi-major axis.", "useMeanMotion");

        var ex = Assert.Throws<OmmParseException>(() =>
            CcsdsValues.Construct<int>(() => throw inner, "meanElements", Parse, Eccentricity));

        Assert.Equal("The meanElements is invalid: Use mean motion or semi-major axis.", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void Construct_ExceptionWithoutParameter_KeepsItsMessage()
    {
        var inner = new ArgumentException("Inconsistent values.");

        var ex = Assert.Throws<OmmParseException>(() =>
            CcsdsValues.Construct<int>(() => throw inner, "meanElements", Parse, Eccentricity));

        Assert.Equal("The meanElements is invalid: Inconsistent values.", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void Construct_NullArgument_NamesTheKeyword()
    {
        var inner = new ArgumentNullException("eccentricity");

        var ex = Assert.Throws<OmmParseException>(() =>
            CcsdsValues.Construct<int>(() => throw inner, "meanElements", Parse, Eccentricity));

        Assert.StartsWith("ECCENTRICITY '-0.5' in meanElements is invalid: ", ex.Message);
        Assert.DoesNotContain("(Parameter", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void Construct_OtherException_Propagates()
    {
        var inner = new InvalidOperationException("Not an argument check.");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CcsdsValues.Construct<int>(() => throw inner, "meanElements", Parse, Eccentricity));

        Assert.Same(inner, ex);
    }
}
