﻿using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

public class LagrangeTests
{
    public LagrangeTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void Create()
    {
        var l1 = new LagrangePoint(LagrangePoints.L1);
        Assert.Equal("L1", l1.Name);
        Assert.Equal(391, l1.NaifId);
        Assert.Equal(0.0, l1.Mass);
        Assert.Equal(0.0, l1.GM);
        Assert.Equal(3, l1.InitialOrbitalParameters.Observer.NaifId);
        Assert.Equal(new Vector3(265316694.670816,  -1448527895.507656, 1706.923545571044), l1.InitialOrbitalParameters.ToStateVector().Position,TestHelpers.VectorComparer);
        Assert.Equal(new Vector3(298.1913805689489, 54.841903612497966, -0.0004202585222601307), l1.InitialOrbitalParameters.ToStateVector().Velocity,TestHelpers.VectorComparer);
        Assert.Equal(Frames.Frame.ECLIPTIC_J2000, l1.InitialOrbitalParameters.Frame);
        Assert.Equal(TimeSystem.Time.J2000TDB, l1.InitialOrbitalParameters.Epoch);
    }
}