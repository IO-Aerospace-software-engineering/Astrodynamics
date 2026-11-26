// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.CCSDS;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.CCSDS.OMM;
using IO.Astrodynamics.CCSDS.OPM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS;

/// <summary>
/// The common base of the exceptions of the CCSDS readers (R03 of #668): <see cref="CcsdsFormatException"/>, a
/// <see cref="FormatException"/>, so that one catch covers a document an OMM, OPM or CDM reader cannot read, whether it
/// fails at parsing or at validation. Before 11.0.0 the five types derived from <see cref="Exception"/> alone.
/// </summary>
public class CcsdsFormatExceptionTests
{
    [Theory]
    [InlineData(typeof(OmmParseException))]
    [InlineData(typeof(OmmValidationException))]
    [InlineData(typeof(OpmParseException))]
    [InlineData(typeof(OpmValidationException))]
    [InlineData(typeof(CdmParseException))]
    public void EveryExceptionOfTheReaders_IsACcsdsFormatException(Type type)
    {
        Assert.True(typeof(CcsdsFormatException).IsAssignableFrom(type), type.Name);
        Assert.True(typeof(FormatException).IsAssignableFrom(type), type.Name);
    }

    [Fact]
    public void ADocumentThatIsNotXml_ThrowsACcsdsFormatException_ForEachMessage()
    {
        Assert.IsAssignableFrom<CcsdsFormatException>(Record.Exception(() => Omm.Parse("garbage")));
        Assert.IsAssignableFrom<CcsdsFormatException>(Record.Exception(() => Omm.Parse("garbage", new CcsdsReadOptions { ValidateSchema = false })));
        Assert.IsAssignableFrom<CcsdsFormatException>(Record.Exception(() => Opm.Parse("garbage")));
        Assert.IsAssignableFrom<CcsdsFormatException>(Record.Exception(() => Opm.Parse("garbage", new CcsdsReadOptions { ValidateSchema = false })));
        Assert.IsAssignableFrom<CcsdsFormatException>(Record.Exception(() => new CdmReader().ReadFromString("garbage")));
    }

    [Fact]
    public void TheStandardConstructors()
    {
        Assert.NotNull(new CcsdsFormatException().Message);
        Assert.Equal("m", new CcsdsFormatException("m").Message);
        var inner = new InvalidOperationException();
        var error = new CcsdsFormatException("m", inner);
        Assert.Equal("m", error.Message);
        Assert.Same(inner, error.InnerException);
    }
}
