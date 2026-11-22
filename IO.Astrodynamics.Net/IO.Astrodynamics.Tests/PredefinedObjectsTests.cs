// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Reflection;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The predefined objects of 11.0.0 (R02 of #668, lot L1): a catalogue entry is an <see cref="INaifObject"/> without a
/// frame, a site of the kernels is built from its <see cref="GroundStations"/> entry, and the data provider of the process
/// is readable (structuring choice 1, decided by S. Guillet, 2026-11-19).
/// </summary>
public class PredefinedObjectsTests
{
    public PredefinedObjectsTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    [Fact]
    public void NaifObject_IsAnINaifObject_WithoutFrame()
    {
        // Before 11.0.0 NaifObject held a Frame the library read nowhere, and was no INaifObject
        Assert.True(typeof(INaifObject).IsAssignableFrom(typeof(NaifObject)));
        Assert.Null(typeof(NaifObject).GetProperty("Frame"));
        Assert.Equal([typeof(int), typeof(string)], typeof(NaifObject).GetConstructors().Single(c => c.GetParameters().Length > 0)
            .GetParameters().Select(p => p.ParameterType));

        // An entry is used where an INaifObject is asked: its identifier and name, no kernel read
        INaifObject[] entries = [PlanetsAndMoons.EARTH, PlanetsAndMoons.MOON, Stars.Sun, GroundStations.DSS_13, LagrangePoints.L1];
        Assert.Equal([399, 301, 10, 399013, 391], entries.Select(x => x.NaifId));
        Assert.Equal(["EARTH", "MOON", "SUN", "DSS-13", "L1"], entries.Select(x => x.Name));
    }

    [Fact]
    public void Site_FromAGroundStation_IsTheSiteOfItsIdentifier()
    {
        // Arrange
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);

        // Act
        var site = new Site(GroundStations.DSS_13, earth);

        // Assert: the site 13 of the Earth, its position at J2000 TDB the one SPICE reads for 399013 (spkezr_c in the
        // Earth's frame, the kernel site's own path: the same state bit for bit)
        Assert.Equal(13, site.Id);
        Assert.Equal(399013, site.NaifId);
        Assert.Equal("DSS-13", site.Name);
        Assert.True(site.IsSpiceBacked);
        var expected = SpiceAPI.Instance.ReadEphemeris(TimeSystem.Time.J2000TDB, 399, 399013, earth.Frame, Aberration.None);
        Assert.Equal(expected.Position, site.InitialOrbitalParameters.ToStateVector().Position);
        Assert.Equal(new Site(13, "DSS-13", earth).Planetodetic, site.Planetodetic);
    }

    [Fact]
    public void Site_FromAStationOfAnotherBody_Throws()
    {
        // Arrange
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);

        // Act and assert: 399013 is no site of the Moon (301001 to 301999); NOTO (398989) and NEW NORCIA (398990) are of no
        // body 398 and no site of the Earth (399001 to 399999)
        var other = Assert.Throws<ArgumentException>(() => new Site(GroundStations.DSS_13, moon));
        Assert.Equal("station", other.ParamName);
        Assert.Contains("301001", other.Message);
        Assert.Equal("station", Assert.Throws<ArgumentException>(() => new Site(GroundStations.NOTO, earth)).ParamName);
        Assert.Equal("station", Assert.Throws<ArgumentException>(() => new Site(GroundStations.NORCIA, earth)).ParamName);
        Assert.Equal("station", Assert.Throws<ArgumentException>(() => new Site(new NaifObject(399000, "NONE"), earth)).ParamName);
        Assert.Equal("station", Assert.Throws<ArgumentException>(() => new Site(new NaifObject(400000, "NONE"), earth)).ParamName);
        Assert.Equal("station", Assert.Throws<ArgumentException>(() => new Site(new NaifObject(399013, ""), earth)).ParamName);
        Assert.Equal("celestialItem", Assert.Throws<ArgumentNullException>(() => new Site(GroundStations.DSS_13, null)).ParamName);
    }

    [Fact]
    public void Configuration_DataProvider_IsPublicAndReadOnly()
    {
        // Before 11.0.0 the getter was internal (structuring choice 1: a public getter, no scoped provider)
        var property = typeof(Configuration).GetProperty(nameof(Configuration.DataProvider), BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        Assert.True(property.GetMethod!.IsPublic);
        Assert.False(property.SetMethod?.IsPublic ?? false);

        // The test process keeps the SPICE provider (no test of it sets the global provider)
        Assert.IsType<SpiceDataProvider>(Configuration.Instance.DataProvider);
    }
}
