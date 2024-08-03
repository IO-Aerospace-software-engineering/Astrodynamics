// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using Xunit;

namespace IO.Astrodynamics.Tests.Coordinates;

public class PlanetocentricTests
{
    public PlanetocentricTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void Create()
    {
        var planetocentric = new Planetocentric(1.0, 2.0, 3.0);
        Assert.Equal(1.0, planetocentric.Longitude);
        Assert.Equal(2.0, planetocentric.Latitude);
        Assert.Equal(3.0, planetocentric.Radius);
    }

    [Fact]
    public void ToCartesian()
    {
        var plan = new Planetocentric(-98.34959789 * Astrodynamics.Constants.Deg2Rad, -18.26566077 * Astrodynamics.Constants.Deg2Rad, 403626339.12495);
        var res = plan.ToCartesianCoordinates();
        Assert.Equal(new Vector3(-55658443.24257991, -379226329.314363, -126505930.63558689), res);
    }

    [Fact]
    public void ToPlanetodetic()
    {
        var plan = new Planetocentric(-116.79445837 * Astrodynamics.Constants.Deg2Rad, 35.06601815 * Astrodynamics.Constants.Deg2Rad, 6372125.09695);
        var res = plan.ToPlanetodetic(TestHelpers.EarthAtJ2000.Flattening, TestHelpers.EarthAtJ2000.EquatorialRadius);
        Assert.Equal(new Planetodetic(-116.79445837000002, 35.24719500924553, 1073.2434613695368),
            new Planetodetic(res.Longitude * Astrodynamics.Constants.Rad2Deg, res.Latitude * Astrodynamics.Constants.Rad2Deg, res.Altitude));
    }

    [Fact]
    public void Radius()
    {
        var plan = new Planetocentric(-116.79445837 * Astrodynamics.Constants.Deg2Rad, 35.06601815 * Astrodynamics.Constants.Deg2Rad, 6372125.09695);
        var radius = plan.RadiusFromPlanetocentricLatitude(TestHelpers.EarthAtJ2000.EquatorialRadius, TestHelpers.EarthAtJ2000.Flattening);
        Assert.Equal(6371054.241003811, radius);
    }
}