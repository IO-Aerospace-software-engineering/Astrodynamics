using System;
using System.IO;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces;

public class ThermalRadiationPressureTests
{
    private static readonly DirectoryInfo SolarSystemKernelPath = new("Data/SolarSystem");

    public ThermalRadiationPressureTests()
    {
        SpiceAPI.Instance.LoadKernels(SolarSystemKernelPath);
    }

    private static CelestialBody CreateEarth(double thermalEffectiveTemperature = 254.0, double thermalEmissivity = 0.95)
    {
        return new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB,
            thermalEffectiveTemperature: thermalEffectiveTemperature, thermalEmissivity: thermalEmissivity);
    }

    private static Spacecraft CreateSpacecraft(CelestialBody observer, double mass = 1000.0,
        double sectionalArea = 10.0, double solarRadiationCoeff = 1.0)
    {
        Clock clk = new Clock("My clock", 256);
        return new Spacecraft(-1001, "MySpacecraft", mass, 10000.0, clk,
            new StateVector(new Vector3(6778136.3, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0),
                observer, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF),
            sectionalArea: sectionalArea, solarRadiationCoeff: solarRadiationCoeff);
    }

    /// <summary>
    /// Computes the expected thermal radiation pressure acceleration magnitude:
    ///   a = Cr * (A/m) * epsilon * sigma * T^4 * R^2 / (c * r^2)
    /// </summary>
    private static double ExpectedThermalAcceleration(
        double epsilon, double tEff, double rBody,
        double cr, double area, double mass, double rSc)
    {
        double t4 = tEff * tEff * tEff * tEff;
        return cr * (area / mass) * epsilon * IO.Astrodynamics.Constants.StefanBoltzmann * t4
               * rBody * rBody / (IO.Astrodynamics.Constants.C * rSc * rSc);
    }

    #region Constructor Validation

    [Fact]
    public void ConstructorThrowsOnNullSpacecraft()
    {
        var earth = CreateEarth();
        Assert.Throws<ArgumentNullException>(() => new ThermalRadiationPressure(null, earth));
    }

    [Fact]
    public void ConstructorThrowsOnNullEmittingBody()
    {
        var earth = CreateEarth();
        var spc = CreateSpacecraft(earth);
        Assert.Throws<ArgumentNullException>(() => new ThermalRadiationPressure(spc, null));
    }

    [Fact]
    public void ConstructorThrowsOnZeroEffectiveTemperature()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB,
            thermalEffectiveTemperature: 0.0, thermalEmissivity: 0.95);
        var spc = CreateSpacecraft(earth);
        Assert.Throws<ArgumentException>(() => new ThermalRadiationPressure(spc, earth));
    }

    [Fact]
    public void ConstructorThrowsOnZeroEmissivity()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB,
            thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.0);
        var spc = CreateSpacecraft(earth);
        Assert.Throws<ArgumentException>(() => new ThermalRadiationPressure(spc, earth));
    }

    #endregion

    #region Analytical Formula Tests

    [Fact]
    public void EarthLeoMatchesAnalyticalFormula()
    {
        // A=10m^2, m=1000kg, Cr=1, r=6778136.3m, T=254K, eps=0.95
        var earth = CreateEarth();
        double rSc = 6778136.3;
        var spc = CreateSpacecraft(earth);

        var state = new StateVector(new Vector3(rSc, 0.0, 0.0), new Vector3(0.0, 7656.0, 0.0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

        var force = new ThermalRadiationPressure(spc, earth);
        var acc = force.Apply(state);

        double expected = ExpectedThermalAcceleration(
            epsilon: 0.95, tEff: 254.0, rBody: earth.EquatorialRadius,
            cr: 1.0, area: 10.0, mass: 1000.0, rSc: rSc);

        Assert.True(System.Math.Abs(acc.Magnitude() - expected) / expected < 1e-10,
            $"Expected {expected:E12}, got {acc.Magnitude():E12}");
    }

    [Fact]
    public void EarthGeoMatchesAnalyticalFormula()
    {
        var earth = CreateEarth();
        double rSc = 42164136.3;
        var spc = CreateSpacecraft(earth);

        var state = new StateVector(new Vector3(rSc, 0.0, 0.0), new Vector3(0.0, 3074.0, 0.0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

        var force = new ThermalRadiationPressure(spc, earth);
        var acc = force.Apply(state);

        double expected = ExpectedThermalAcceleration(
            epsilon: 0.95, tEff: 254.0, rBody: earth.EquatorialRadius,
            cr: 1.0, area: 10.0, mass: 1000.0, rSc: rSc);

        Assert.True(System.Math.Abs(acc.Magnitude() - expected) / expected < 1e-10,
            $"Expected {expected:E12}, got {acc.Magnitude():E12}");
    }

    [Fact]
    public void DirectionIsRadiallyOutwardFromBody()
    {
        var earth = CreateEarth();
        double rSc = 6778136.3;
        var spc = CreateSpacecraft(earth);

        var scPos = new Vector3(rSc * 0.5, rSc * 0.5, rSc * 0.5).Normalize() * rSc;
        var state = new StateVector(scPos, new Vector3(0.0, 7656.0, 0.0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

        var force = new ThermalRadiationPressure(spc, earth);
        var acc = force.Apply(state);

        // Body-to-SC unit vector (body at origin since it's the observer)
        var expectedDir = scPos.Normalize();
        var accDir = acc.Normalize();
        double dot = expectedDir * accDir;

        Assert.True(System.Math.Abs(dot - 1.0) < 1e-12,
            $"Acceleration must point radially outward from body. Dot product = {dot}");
    }

    [Fact]
    public void DecaysWithInverseSquareOfDistance()
    {
        var earth = CreateEarth();
        double rLeo = 6778136.3;
        double rGeo = 42164136.3;
        var spcLeo = CreateSpacecraft(earth);
        var spcGeo = CreateSpacecraft(earth);

        var stateLeo = new StateVector(new Vector3(rLeo, 0.0, 0.0), new Vector3(0.0, 7656.0, 0.0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var stateGeo = new StateVector(new Vector3(rGeo, 0.0, 0.0), new Vector3(0.0, 3074.0, 0.0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

        var accLeo = new ThermalRadiationPressure(spcLeo, earth).Apply(stateLeo);
        var accGeo = new ThermalRadiationPressure(spcGeo, earth).Apply(stateGeo);

        double expectedRatio = (rGeo / rLeo) * (rGeo / rLeo);
        double actualRatio = accLeo.Magnitude() / accGeo.Magnitude();

        Assert.True(System.Math.Abs(actualRatio - expectedRatio) / expectedRatio < 1e-10,
            $"Expected ratio {expectedRatio:F6}, got {actualRatio:F6}");
    }

    [Fact]
    public void ScalesLinearlyWithEmissivity()
    {
        var earth1 = CreateEarth(thermalEmissivity: 0.475);
        var earth2 = CreateEarth(thermalEmissivity: 0.95);
        double rSc = 6778136.3;

        var spc1 = CreateSpacecraft(earth1);
        var spc2 = CreateSpacecraft(earth2);

        var state1 = new StateVector(new Vector3(rSc, 0.0, 0.0), new Vector3(0.0, 7656.0, 0.0),
            earth1, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var state2 = new StateVector(new Vector3(rSc, 0.0, 0.0), new Vector3(0.0, 7656.0, 0.0),
            earth2, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

        var acc1 = new ThermalRadiationPressure(spc1, earth1).Apply(state1);
        var acc2 = new ThermalRadiationPressure(spc2, earth2).Apply(state2);

        double expectedRatio = 0.95 / 0.475; // = 2.0
        double actualRatio = acc2.Magnitude() / acc1.Magnitude();

        Assert.True(System.Math.Abs(actualRatio - expectedRatio) < 1e-12,
            $"Expected ratio {expectedRatio}, got {actualRatio}");
    }

    [Fact]
    public void ScalesWithFourthPowerOfTemperature()
    {
        var earth1 = CreateEarth(thermalEffectiveTemperature: 254.0);
        var earth2 = CreateEarth(thermalEffectiveTemperature: 508.0);
        double rSc = 6778136.3;

        var spc1 = CreateSpacecraft(earth1);
        var spc2 = CreateSpacecraft(earth2);

        var state1 = new StateVector(new Vector3(rSc, 0.0, 0.0), new Vector3(0.0, 7656.0, 0.0),
            earth1, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var state2 = new StateVector(new Vector3(rSc, 0.0, 0.0), new Vector3(0.0, 7656.0, 0.0),
            earth2, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

        var acc1 = new ThermalRadiationPressure(spc1, earth1).Apply(state1);
        var acc2 = new ThermalRadiationPressure(spc2, earth2).Apply(state2);

        double expectedRatio = 16.0; // (508/254)^4 = 2^4 = 16
        double actualRatio = acc2.Magnitude() / acc1.Magnitude();

        Assert.True(System.Math.Abs(actualRatio - expectedRatio) / expectedRatio < 1e-10,
            $"Expected ratio {expectedRatio}, got {actualRatio}");
    }

    [Fact]
    public void ScalesInverselyWithMass()
    {
        var earth = CreateEarth();
        double rSc = 6778136.3;

        var spc1 = CreateSpacecraft(earth, mass: 100.0);
        var spc2 = CreateSpacecraft(earth, mass: 200.0);

        var state = new StateVector(new Vector3(rSc, 0.0, 0.0), new Vector3(0.0, 7656.0, 0.0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

        var acc1 = new ThermalRadiationPressure(spc1, earth).Apply(state);
        var acc2 = new ThermalRadiationPressure(spc2, earth).Apply(state);

        double expectedRatio = 200.0 / 100.0; // = 2.0
        double actualRatio = acc1.Magnitude() / acc2.Magnitude();

        Assert.True(System.Math.Abs(actualRatio - expectedRatio) < 1e-12,
            $"Expected ratio {expectedRatio}, got {actualRatio}");
    }

    [Fact]
    public void ScalesLinearlyWithSectionalArea()
    {
        var earth = CreateEarth();
        double rSc = 6778136.3;

        var spc1 = CreateSpacecraft(earth, sectionalArea: 1.0);
        var spc2 = CreateSpacecraft(earth, sectionalArea: 5.0);

        var state = new StateVector(new Vector3(rSc, 0.0, 0.0), new Vector3(0.0, 7656.0, 0.0),
            earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

        var acc1 = new ThermalRadiationPressure(spc1, earth).Apply(state);
        var acc2 = new ThermalRadiationPressure(spc2, earth).Apply(state);

        double expectedRatio = 5.0;
        double actualRatio = acc2.Magnitude() / acc1.Magnitude();

        Assert.True(System.Math.Abs(actualRatio - expectedRatio) < 1e-12,
            $"Expected ratio {expectedRatio}, got {actualRatio}");
    }

    [Fact]
    public void TrpVsSrpRatioAtLeo()
    {
        // Compare TRP to SRP magnitude at LEO
        // TRP ~15% of SRP is the expected ballpark
        var earth = CreateEarth();
        double rSc = 6778136.3;

        var spc = CreateSpacecraft(earth);

        // Place SC toward the Sun so SRP is at full strength
        var epoch = TimeSystem.Time.J2000TDB;
        var sunPos = Stars.SUN_BODY.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.LT)
            .ToStateVector().Position;
        var sunDir = sunPos.Normalize();
        var scPos = sunDir * rSc;
        var scVel = sunDir.Cross(Vector3.VectorZ).Normalize() * 7656.0;
        var state = new StateVector(scPos, scVel, earth, epoch, Frames.Frame.ICRF);

        var trpForce = new ThermalRadiationPressure(spc, earth);
        var srpForce = new SolarRadiationPressure(spc, [earth]);

        var trpAcc = trpForce.Apply(state);
        var srpAcc = srpForce.Apply(state);

        double ratio = trpAcc.Magnitude() / srpAcc.Magnitude();

        // TRP should be roughly 5-25% of SRP at LEO
        Assert.True(ratio > 0.05, $"TRP/SRP ratio {ratio:F4} too small (expected > 0.05)");
        Assert.True(ratio < 0.30, $"TRP/SRP ratio {ratio:F4} too large (expected < 0.30)");
    }

    #endregion
}
