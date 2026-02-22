using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers;

public class AntiNormalAttitudeTests
{
    public AntiNormalAttitudeTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    void Create()
    {
        FuelTank fuelTank = new FuelTank("My fuel tank", "ft2021", "sn0", 4000.0, 3000.0);
        Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank);
        AntiNormalAttitude attitude = new AntiNormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.FromHours(1.0), eng);
        Assert.Equal(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), attitude.MinimumEpoch);
        Assert.Equal(TimeSpan.FromHours(1.0), attitude.ManeuverHoldDuration);
        Assert.NotNull(attitude.Engine);
        Assert.Equal(eng, attitude.Engine);
    }

    [Fact]
    public void Execute_EquatorialOrbit_AntiNormalIsNegativeZ()
    {
        // For r=(+X, 0, 0) and v=(0, +Y, 0): h = r x v = +Z, so anti-normal = -Z
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-672, "AntiNormalTestSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        AntiNormalAttitude maneuver = new AntiNormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());

        var sv = orbitalParams.ToStateVector();
        maneuver.TryExecute(sv);

        var rotatedFront = Spacecraft.Front.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        var antiNormal = sv.Position.Cross(sv.Velocity).Inverse().Normalize();

        double alignment = rotatedFront * antiNormal;
        Assert.True(alignment > 0.9999, $"Front should align with anti-normal (-Z). Alignment: {alignment}");
    }

    [Fact]
    public void Execute_OppositeToNormal()
    {
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-673, "TestSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

        var sv = orbitalParams.ToStateVector();

        var normalManeuver = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());
        normalManeuver.TryExecute(sv);
        var normalPointing = Spacecraft.Front.Rotate(normalManeuver.StateOrientation.Rotation).Normalize();

        var antiNormalManeuver = new AntiNormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());
        antiNormalManeuver.TryExecute(sv);
        var antiNormalPointing = Spacecraft.Front.Rotate(antiNormalManeuver.StateOrientation.Rotation).Normalize();

        // Normal and anti-normal should be opposite (dot product close to -1)
        double dotProduct = normalPointing * antiNormalPointing;
        Assert.True(dotProduct < -0.9999, $"Normal and anti-normal should be opposite. Dot product: {dotProduct}");
    }

    [Fact]
    public void Execute_FuelNotBurned()
    {
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-674, "TestSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        AntiNormalAttitude maneuver = new AntiNormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());
        maneuver.TryExecute(orbitalParams.ToStateVector());

        Assert.Equal(0.0, maneuver.FuelBurned);
    }

    [Fact]
    public void Execute_PolarOrbit_AntiNormalIsInEquatorialPlane()
    {
        // Polar orbit (i=90°, RAAN=0°, trueAnomaly=0): h = (0,-1,0)*|h|
        // Anti-normal = -h direction = (0,+1,0) — in equatorial plane with zero Z.
        var orbitalParams = new KeplerianElements(
            7000000.0, 0.0,
            90.0 * Astrodynamics.Constants.Deg2Rad,
            0.0, 0.0, 0.0,
            TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-681, "PolarAntiNormalSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        AntiNormalAttitude maneuver = new AntiNormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());

        var sv = orbitalParams.ToStateVector();
        maneuver.TryExecute(sv);

        // Anti-normal = -(r x v) / |r x v|
        var antiNormal = sv.Position.Cross(sv.Velocity).Inverse().Normalize();

        // Anti-normal should have near-zero Z component (equatorial plane)
        Assert.True(System.Math.Abs(antiNormal.Z) < 1e-10,
            $"Polar orbit anti-normal Z should be ~0. Got: {antiNormal.Z}");

        // Rotated front should align with anti-normal
        var rotatedFront = Spacecraft.Front.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        double alignment = rotatedFront * antiNormal;
        Assert.True(alignment > 0.9999,
            $"Front should align with anti-normal. Alignment: {alignment}");

        // Anti-normal must be opposite to normal
        var normal = sv.Position.Cross(sv.Velocity).Normalize();
        double oppositeness = antiNormal * normal;
        Assert.True(oppositeness < -0.9999,
            $"Anti-normal should be opposite to normal. Dot product: {oppositeness}");
    }

    [Fact]
    public void Execute_DegenerateOrbit_ThrowsInvalidOperationException()
    {
        // r and v are parallel — h = r x v = 0, so anti-normal is undefined
        var sv = new StateVector(
            new Vector3(6678000.0, 0.0, 0.0),
            new Vector3(1000.0, 0.0, 0.0), // parallel to r
            TestHelpers.EarthAtJ2000,
            TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);

        var spc = new Spacecraft(-682, "DegAntiNormalSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), sv);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        AntiNormalAttitude maneuver = new AntiNormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => maneuver.TryExecute(sv));
    }

    [Fact]
    public void Execute_WithCustomBodyAxes()
    {
        // Mirror of NormalAttitudeTests.Execute_WithCustomBodyAxes but for anti-normal.
        // Spacecraft with +X as front; for equatorial orbit anti-normal = -Z.
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-683, "AntiNormalCustomAxesSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams,
            bodyFront: Vector3.VectorX, bodyRight: Vector3.VectorY.Inverse(), bodyUp: Vector3.VectorZ);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        AntiNormalAttitude maneuver = new AntiNormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());

        var sv = orbitalParams.ToStateVector();
        maneuver.TryExecute(sv);

        // The custom body front (+X) should align with the anti-normal direction
        var rotatedFront = spc.BodyFront.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        var antiNormal = sv.Position.Cross(sv.Velocity).Inverse().Normalize();

        double alignment = rotatedFront * antiNormal;
        Assert.True(alignment > 0.9999,
            $"Custom body front (+X) should align with anti-normal. Alignment: {alignment}");
    }
}
