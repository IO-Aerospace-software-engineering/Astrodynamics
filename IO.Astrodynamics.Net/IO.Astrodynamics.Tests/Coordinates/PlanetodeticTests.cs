// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Coordinates;
using Xunit;

namespace IO.Astrodynamics.Tests.Coordinates;

public class PlanetodeticTests
{
    public PlanetodeticTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void Create()
    {
        var planetodetic = new Planetodetic(1.0, 2.0, 3.0);
        Assert.Equal(1.0, planetodetic.Longitude);
        Assert.Equal(2.0, planetodetic.Latitude);
        Assert.Equal(3.0, planetodetic.Altitude);
    }
    
    [Fact]
    public void ToPlanetocentric()
    {
        Planetodetic planetodetic = new Planetodetic(-116.79445837 * Astrodynamics.Constants.Deg2Rad, 35.24716450 * Astrodynamics.Constants.Deg2Rad, 1070.85);
        Planetocentric planetocentric = planetodetic.ToPlanetocentric(TestHelpers.EarthAtJ2000.Flattening, TestHelpers.EarthAtJ2000.EquatorialRadius);
        // SPICE georec_c and reclat_c on the ellipsoid of pck00011 (tools/geodetic_reference/test_values_ref.c)
        Assert.Equal(35.066018156829827, planetocentric.Latitude * Astrodynamics.Constants.Rad2Deg, 12);
        Assert.Equal(-116.79445837, planetocentric.Longitude * Astrodynamics.Constants.Rad2Deg, 12);
        Assert.Equal(6372125.096354289, planetocentric.Radius, 6);
    }
}