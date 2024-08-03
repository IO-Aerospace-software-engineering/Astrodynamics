using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

public class EquinoctialElementsTests
{
    public EquinoctialElementsTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void Create()
    {
        CelestialBody earth = new CelestialBody( PlanetsAndMoons.EARTH);
        var epoch = DateTime.UtcNow;
        EquinoctialElements equ = new EquinoctialElements(1.0, 2.0, 3.0, 4.0, 5.0, 6.0, earth, epoch, Frames.Frame.ICRF);
        Assert.Equal(1.0, equ.P);
        Assert.Equal(2.0, equ.F);
        Assert.Equal(3.0, equ.G);
        Assert.Equal(4.0, equ.H);
        Assert.Equal(5.0, equ.K);
        Assert.Equal(6.0, equ.L0);
        Assert.Equal(earth, equ.Observer);
        Assert.Equal(epoch, equ.Epoch);
        Assert.Equal(Frames.Frame.ICRF, equ.Frame);
    }

    [Fact]
    public void ToEquinoctial()
    {
        CelestialBody earth = new CelestialBody( PlanetsAndMoons.EARTH);
        KeplerianElements ke = new KeplerianElements(6800.81178582, 0.00134, 51.71 * IO.Astrodynamics.Constants.Deg2Rad, 32.57 * IO.Astrodynamics.Constants.Deg2Rad,
            105.64 * IO.Astrodynamics.Constants.Deg2Rad, 46.029 * IO.Astrodynamics.Constants.Deg2Rad, earth, DateTime.UtcNow, Frames.Frame.ICRF);
        EquinoctialElements equ = ke.ToEquinoctial();
        Assert.Equal(equ.SemiMajorAxis(), ke.A);
        Assert.Equal(equ.Eccentricity(), ke.E);
        Assert.Equal(equ.Inclination(), ke.I);
        Assert.Equal(equ.AscendingNode() * IO.Astrodynamics.Constants.Rad2Deg, ke.RAAN * IO.Astrodynamics.Constants.Rad2Deg, 12);
        Assert.Equal(equ.ArgumentOfPeriapsis(), ke.AOP, 12);
        Assert.Equal(equ.MeanAnomaly(), ke.M, 6);
    }

    [Fact]
    public void Equality()
    {
        CelestialBody earth = new CelestialBody( PlanetsAndMoons.EARTH);
        KeplerianElements ke = new KeplerianElements(6800.81178582, 0.00134, 51.71 * IO.Astrodynamics.Constants.Deg2Rad, 32.57 * IO.Astrodynamics.Constants.Deg2Rad,
            105.64 * IO.Astrodynamics.Constants.Deg2Rad, 46.029 * IO.Astrodynamics.Constants.Deg2Rad, earth, DateTime.UtcNow, Frames.Frame.ICRF);
        EquinoctialElements equ = ke.ToEquinoctial();
        EquinoctialElements equ2 = equ.ToEquinoctial();
        Assert.Equal(equ, equ2);
        Assert.True(equ == equ2);
        Assert.False(equ != equ2);
        Assert.False(equ == null);
        Assert.True(equ.Equals((object)equ2));
        Assert.True(equ.Equals((object)equ));
        Assert.False(equ.Equals((object)null));
        Assert.False(equ.Equals(null));
        Assert.True(equ.Equals(equ2));
    }
}