// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators;

public class IntegratorTests
{
    public IntegratorTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void IntegrateWithPerturbations()
    {
        var sun = TestHelpers.Sun;
        var moon = TestHelpers.MoonAtJ2000;
        var earth = TestHelpers.EarthWithAtmAndGeoAtJ2000;
        Clock clk = new Clock("My clock", 256);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk,
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));

        List<ForceBase> forces = new List<ForceBase>();
        forces.Add(new GravitationalAcceleration(sun));
        forces.Add(new GravitationalAcceleration(moon));
        forces.Add(new GravitationalAcceleration(earth));
        forces.Add(new AtmosphericDrag(spc, earth));
        forces.Add(new SolarRadiationPressure(spc, [earth]));
        VVIntegrator vvIntegrator = new VVIntegrator(forces, TimeSpan.FromSeconds(1.0), spc.InitialOrbitalParameters.ToStateVector());
        Dictionary<TimeSystem.Time, StateVector> data = new Dictionary<TimeSystem.Time, StateVector>(2);
        data[TimeSystem.Time.J2000TDB] = spc.InitialOrbitalParameters.ToStateVector();
        data[TimeSystem.Time.J2000TDB.AddSeconds(1)] = spc.InitialOrbitalParameters.ToStateVector();
        vvIntegrator.Integrate(data, 1);
        Assert.Equal(new Vector3(6799995.689837336, 7656.217641039796, -0.0012025735770054711), data[TimeSystem.Time.J2000TDB.AddSeconds(1)].Position, TestHelpers.VectorComparer);
        Assert.Equal(new Vector3(-8.620322602729967, 7656.210010299539, -0.002405146120787803), data[TimeSystem.Time.J2000TDB.AddSeconds(1)].Velocity, TestHelpers.VectorComparer);
    }
}