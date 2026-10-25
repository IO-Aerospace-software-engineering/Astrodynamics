// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Frames;
using Xunit;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The frames of spec.md section 2.1, and nothing else.
/// </summary>
public class FrameCatalogTests
{
    [Theory]
    [InlineData("ICRF", "J2000")]
    [InlineData("ECLIPJ2000", "ECLIPJ2000")]
    [InlineData("ITRF93", "ITRF93")]
    [InlineData("IAU_EARTH", "IAU_EARTH")]
    [InlineData("IAU_MOON", "IAU_MOON")]
    [InlineData("MOON_PA", "MOON_PA")]
    [InlineData("MOON_ME", "MOON_ME")]
    public void TheSevenFrames_AreTheSpiceFramesOfTheirName(string name, string spiceName)
    {
        Assert.Equal(spiceName, FrameCatalog.Resolve(name).Name);
        Assert.Contains(name, FrameCatalog.Names);
    }

    [Fact]
    public void IcrfAndEcliptic_AreTheFrameworkFrames()
    {
        Assert.Same(Frame.ICRF, FrameCatalog.Resolve("ICRF"));
        Assert.Same(Frame.ECLIPTIC_J2000, FrameCatalog.Resolve("ECLIPJ2000"));
        Assert.Equal(7, FrameCatalog.Names.Count);
    }

    [Theory]
    [InlineData("EME2000")] // GMAT's EarthMJ2000Eq is not EME2000 (conformance-tests#13)
    [InlineData("TIRS")]
    [InlineData("TEME")]
    [InlineData("J2000")]
    [InlineData("icrf")]
    [InlineData("")]
    [InlineData(null)]
    public void AnyOtherName_IsRejected(string name)
    {
        var exception = Assert.Throws<ArgumentException>(() => FrameCatalog.Resolve(name));

        Assert.Contains("section 2.1", exception.Message);
    }
}
