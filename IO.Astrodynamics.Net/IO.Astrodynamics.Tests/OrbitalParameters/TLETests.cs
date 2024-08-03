// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Time;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

public class TLETests
{
    public TLETests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void Create()
    {
        TLE tle = TLE.Create("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        Assert.Throws<ArgumentException>(() => TLE.Create("",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"));
        Assert.Throws<ArgumentException>(() => TLE.Create("ISS",
            "",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"));
        Assert.Throws<ArgumentException>(() => TLE.Create("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            ""));

        Assert.Equal(6803376.2159598358, tle.SemiMajorAxis(), 6);
        Assert.Equal(4.9299999999999999e-05, tle.Eccentricity(), 6);
        Assert.Equal(0.9013281683026676, tle.Inclination(), 6);
        Assert.Equal(6.1615568022666061, tle.AscendingNode(), 6);
        Assert.Equal(5.6003339639830649, tle.ArgumentOfPeriapsis(), 6);
        Assert.Equal(0.68479738531249512, tle.MeanAnomaly(), 6);
        Assert.Equal(664419082.84759104, tle.Epoch.SecondsFromJ2000TDB(), 6);
        Assert.Equal(5.06539394194257e-10, tle.BalisticCoefficient, 6);
        Assert.Equal(0.0001027, tle.DragTerm, 6);
        Assert.Equal(0.0, tle.SecondDerivativeMeanMotion, 6);
    }

    [Fact]
    public void ToStateVector()
    {
        TLE tle = TLE.Create("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        DateTime epoch = DateTimeExtension.CreateTDB(664440682.84760022);
        var stateVector = tle.AtEpoch(epoch).ToStateVector();

        Assert.Equal(stateVector, tle.ToStateVector(epoch));
        Assert.Equal(4363669.261337338, stateVector.Position.X, 3);
        Assert.Equal(-3627809.912410662, stateVector.Position.Y, 3);
        Assert.Equal(-3747415.4653566754, stateVector.Position.Z, 3);
        Assert.Equal(5805.8241824895995, stateVector.Velocity.X, 3);
        Assert.Equal(2575.722643716163, stateVector.Velocity.Y, 3);
        Assert.Equal(4271.5974622410786, stateVector.Velocity.Z, 3);
        Assert.Equal("J2000", stateVector.Frame.Name);
        Assert.Equal(399, stateVector.Observer.NaifId);
        Assert.Equal(664440682.84760022, stateVector.Epoch.SecondsFromJ2000TDB(),3);
    }

    [Fact]
    public void Equality()
    {
        TLE tle = TLE.Create("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        TLE tle2 = TLE.Create("ISS",
            "1 25544U 98067A   21021.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        Assert.NotEqual(tle, tle2);
        Assert.True(tle != tle2);
        Assert.False(tle == tle2);
        Assert.False(tle.Equals(tle2));
        Assert.False(tle.Equals(null));
        Assert.True(tle.Equals(tle));
        Assert.False(tle.Equals((object)tle2));
        Assert.False(tle.Equals((object)null));
        Assert.True(tle.Equals((object)tle));
    }
}