// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Body;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// #651: the centre of motion and the barycentre of a site identifier (its body's identifier times 1000 plus its own,
/// <see cref="Surface.Site.NaifId"/>) are its body and its body's barycentre, not the Sun and the solar system barycentre
/// of an asteroid.
/// </summary>
/// <remarks>
/// The reference is SPICE's: the segments of DSS-13, DSS-14 and DSS-65 in <c>earthstns_itrf93_201023.bsp</c> of
/// <c>Data/SolarSystem</c> have the centre 399 (spiceypy 8.4.0, CSPICE N0067, <c>spksfs</c> and <c>dafus</c>,
/// 2026-11-17), whose barycentre is 3, and the NAIF rule of site identifiers that <see cref="Surface.Site.NaifId"/>
/// follows (body × 1000 + site). Before the fix both layers classed every identifier above 1000 as an asteroid:
/// <see cref="SpiceApi.GetCelestialBodyInfo"/> gave 10 and 0 for 399013 and 399014, and <see cref="CelestialItem.IsAsteroid"/>
/// was true for them (measured on a85098d). An identifier of the form of a site whose body SPICE does not name keeps that
/// classification: SPICE's built-in names include NOTO (398989) and NEW NORCIA (398990), ESA stations, but no body 398
/// (spiceypy 8.4.0, CSPICE N0067, <c>bodc2n</c> without kernels, 2026-11-17); before the review of #651 they were
/// corrected to the centre 398, which SPICE does not name, and the barycentre 3.
/// </remarks>
public class SiteIdentifierCentreTests
{
    public SiteIdentifierCentreTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    // A body from custom parameters, of the identifier given: no kernel read
    private sealed class CustomBody(int naifId) : CelestialBody(naifId, "IOA_651_" + naifId, 1.0e20, 1000.0, 1000.0);

    [Theory]
    [InlineData(399013, 399, 3)] // DSS-13
    [InlineData(399014, 399, 3)] // DSS-14
    [InlineData(399065, 399, 3)] // DSS-65
    [InlineData(301, 399, 3)] // control: the Moon
    [InlineData(399, 10, 3)] // control: the Earth
    [InlineData(398989, 10, 0)] // NOTO, a built-in name of SPICE whose form is a site's of 398, a body SPICE does not name
    [InlineData(398990, 10, 0)] // NEW NORCIA, the same
    public void GetCelestialBodyInfo_GivesTheCentreAndTheBarycentreOfSPICE(int naifId, int centre, int barycentre)
    {
        // Act
        var info = SpiceApi.Instance.GetCelestialBodyInfo(naifId);

        // Assert
        Assert.Equal(naifId, info.NaifId);
        Assert.Equal(centre, info.CenterOfMotionId);
        Assert.Equal(barycentre, info.BarycenterOfMotionId);
    }

    [Theory]
    [InlineData(399013, 399, 3, false)] // a site of the Earth
    [InlineData(301005, 301, 3, false)] // a site of the Moon
    [InlineData(499001, 499, 4, false)] // a site of Mars
    [InlineData(2000433, 10, 0, true)] // an asteroid (Eros), unchanged
    [InlineData(1000012, 10, 0, true)] // a comet, unchanged
    [InlineData(391001, 10, 0, true)] // above 1000 and not a site: the body 391 is L1, unchanged
    [InlineData(399000, 10, 0, true)] // a site number 0 is no site (Site.NaifId starts at 1), unchanged
    [InlineData(398989, 10, 0, true)] // NOTO: SPICE names no body 398, so no site, unchanged
    [InlineData(398990, 10, 0, true)] // NEW NORCIA, the same
    [InlineData(101001, 10, 0, true)] // the form of a site of 101, a body SPICE does not name, unchanged
    public void CelestialItem_OfASiteIdentifier_IsAboutItsBody(int naifId, int centre, int barycentre, bool asteroid)
    {
        // Arrange: a body from custom parameters, whose identifiers of centre the constructor does not set
        var body = new CustomBody(naifId);

        // Act: the rule the natural-body constructors apply
        int foundCentre = CelestialItem.FindCenterOfMotionId(body);
        int foundBarycentre = CelestialItem.FindBarycenterOfMotionId(body);

        // Assert
        Assert.Equal(asteroid, body.IsAsteroid);
        Assert.Equal(centre, foundCentre);
        Assert.Equal(barycentre, foundBarycentre);
    }

    [Fact]
    public void CelestialBody_OfDss13_IsAboutTheEarth()
    {
        // The body SPICE's information builds for DSS-13 (known limitation of 11.0.0: a body of GM 0 in DSS-13_TOPO)
        var body = new CelestialBody(399013);

        Assert.False(body.IsAsteroid);
        Assert.Equal(399, body.CenterOfMotionId);
        Assert.Equal(3, body.BarycenterOfMotionId);
    }
}
