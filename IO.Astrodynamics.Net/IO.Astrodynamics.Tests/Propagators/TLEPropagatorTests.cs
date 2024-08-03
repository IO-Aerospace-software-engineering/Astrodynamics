// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Time;
using Xunit;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Window = IO.Astrodynamics.Time.Window;

namespace IO.Astrodynamics.Tests.Propagators;

public class TLEPropagatorTests
{
    public TLEPropagatorTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void CheckPropagation()
    {
        Clock clk = new Clock("My clock", 256);
        TLE tle = TLE.Create("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, tle);
        var start = spc.InitialOrbitalParameters.Epoch;
        var end = start.AddDays(1.0);
        TLEPropagator propagator = new TLEPropagator(new Window(start, end), spc, TimeSpan.FromHours(1.0));
        var res = propagator.Propagate();
        Assert.Equal(tle.ToStateVector(start), res.stateVectors.First(x => x.Epoch == start));
        Assert.Equal(tle.ToStateVector(end), res.stateVectors.First(x => x.Epoch == end));
    }
}