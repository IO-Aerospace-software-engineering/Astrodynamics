using System;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces;

public class SolarRadiationPressureTests
{
    public SolarRadiationPressureTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void ComputeAcceleration()
    {
        var earth = new CelestialBody(399, new GeopotentialModelParameters(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree")));
        Clock clk = new Clock("My clock", 256);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk,
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
        SolarRadiationPressure solarRadiationPressure = new SolarRadiationPressure(spc, [earth]);

        StateVector parkingOrbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);
        var res = solarRadiationPressure.Apply(parkingOrbit);
        Assert.Equal(new Vector3(-8.456677063948622E-09, 4.237795744348693E-08, 1.8372880484798083E-08), res, TestHelpers.VectorComparer);
    }

    [Fact]
    public void ComputeAccelerationWithCr()
    {
        // Cr = 1.8 should scale the SRP by 1.8x compared to Cr = 1.0
        var earth = new CelestialBody(399, new GeopotentialModelParameters(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree")));
        Clock clk = new Clock("My clock", 256);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk,
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF),
            solarRadiationCoeff: 1.8);
        SolarRadiationPressure solarRadiationPressure = new SolarRadiationPressure(spc, [earth]);

        StateVector parkingOrbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);
        var res = solarRadiationPressure.Apply(parkingOrbit);

        // Expected = base result * 1.8
        var expected = new Vector3(-8.456677063948622E-09 * 1.8, 4.237795744348693E-08 * 1.8, 1.8372880484798083E-08 * 1.8);
        Assert.Equal(expected, res, TestHelpers.VectorComparer);
    }

    [Fact]
    public void ComputeAccelerationWithDynamicMass()
    {
        // Spacecraft with fuel tank — GetTotalMass() = dry mass + fuel = 100 + 50 = 150 kg
        // areaMassRatio = 1.0 / 150 instead of 1.0 / 100
        // So result should be 100/150 = 2/3 of the base result
        var earth = new CelestialBody(399, new GeopotentialModelParameters(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree")));
        Clock clk = new Clock("My clock", 256);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk,
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
        var fuelTank = new FuelTank("tank1", "model1", "sn001", 100.0, 50.0);
        spc.AddFuelTank(fuelTank);

        SolarRadiationPressure solarRadiationPressure = new SolarRadiationPressure(spc, [earth]);

        StateVector parkingOrbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);
        var res = solarRadiationPressure.Apply(parkingOrbit);

        // Expected = base result * (100/150)
        double ratio = 100.0 / 150.0;
        var expected = new Vector3(-8.456677063948622E-09 * ratio, 4.237795744348693E-08 * ratio, 1.8372880484798083E-08 * ratio);
        Assert.Equal(expected, res, TestHelpers.VectorComparer);
    }

    [Fact]
    public void ConstructorThrowsOnNullSpacecraft()
    {
        var earth = new CelestialBody(399);
        Assert.Throws<ArgumentNullException>(() => new SolarRadiationPressure(null, [earth]));
    }

    [Fact]
    public void ConstructorThrowsOnNullOccultingBodies()
    {
        var earth = new CelestialBody(399);
        Clock clk = new Clock("My clock", 256);
        Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk,
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
        Assert.Throws<ArgumentNullException>(() => new SolarRadiationPressure(spc, null));
    }
}
