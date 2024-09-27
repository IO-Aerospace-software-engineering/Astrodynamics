// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Tests.Propagators;

public class SpacecraftPropagatorTests
{
    public SpacecraftPropagatorTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void CheckSymplecticProperty()
    {
        Clock clk = new Clock("My clock", 256);
        var orbit = new KeplerianElements(150000000000.0, 0, 0, 0, 0, 0, new Barycenter(0), TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, orbit);
        Propagator.SpacecraftPropagator spacecraftPropagator = new Propagator.SpacecraftPropagator(new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(30)), spc,
            [new Barycenter(0)], false, false, TimeSpan.FromSeconds(100.0));
        spacecraftPropagator.Propagate();
        var energy = spc.StateVectorsRelativeToICRF.Values.Select(x => x.SpecificOrbitalEnergy()).ToArray();
        var min = energy.Min();
        var max = energy.Max();
        var diff = max - min;
        Assert.True(diff < 9.8E-05);
    }

    [Fact]
    public void CheckNonFittingStepSize()
    {
        Clock clk = new Clock("My clock", 256);
        var orbit = new KeplerianElements(150000000000.0, 0, 0, 0, 0, 0, new Barycenter(0), TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk, orbit);
        Propagator.SpacecraftPropagator spacecraftPropagator = new Propagator.SpacecraftPropagator(new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddSeconds(5)),
            spc,
            [new Barycenter(0)], false, false, TimeSpan.FromSeconds(2.0));

        spacecraftPropagator.Propagate();
        var state = spc.StateVectorsRelativeToICRF.Values.ElementAt(0);
        var state1 = spc.StateVectorsRelativeToICRF.Values.ElementAt(1);
        var state2 = spc.StateVectorsRelativeToICRF.Values.ElementAt(2);
        var state3 = spc.StateVectorsRelativeToICRF.Values.ElementAt(3);
    }
}