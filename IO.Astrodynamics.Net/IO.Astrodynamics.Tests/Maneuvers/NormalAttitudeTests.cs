using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers;

public class NormalAttitudeTests
{
    public NormalAttitudeTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    void Create()
    {
        FuelTank fuelTank = new FuelTank("My fuel tank", "ft2021", "sn0", 4000.0, 3000.0);
        Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank);
        NormalAttitude attitude = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.FromHours(1.0), eng);
        Assert.Equal(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), attitude.MinimumEpoch);
        Assert.Equal(TimeSpan.FromHours(1.0), attitude.ManeuverHoldDuration);
        Assert.NotNull(attitude.Engine);
        Assert.Equal(eng, attitude.Engine);
    }

    [Fact]
    public void Execute_EquatorialOrbit_NormalIsPositiveZ()
    {
        // For r=(+X, 0, 0) and v=(0, +Y, 0): h = r x v = +Z
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-668, "NormalTestSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        NormalAttitude maneuver = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());

        var sv = orbitalParams.ToStateVector();
        maneuver.TryExecute(sv);

        // The body front (+Y) should be rotated to align with +Z (normal direction)
        var rotatedFront = Spacecraft.Front.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        var normal = sv.Position.Cross(sv.Velocity).Normalize();

        // Verify the rotated front aligns with orbital normal
        double alignment = rotatedFront * normal;
        Assert.True(alignment > 0.9999, $"Front should align with orbital normal (+Z). Alignment: {alignment}");
    }

    [Fact]
    public void Execute_InclinedOrbit_NormalIsOffAxis()
    {
        // Inclined orbit: normal should have X and Z components
        var orbitalParams = new KeplerianElements(7000000.0, 0.0, 45.0 * Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, 0.0,
            TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-669, "InclinedTestSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        NormalAttitude maneuver = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());

        var sv = orbitalParams.ToStateVector();
        maneuver.TryExecute(sv);

        var rotatedFront = Spacecraft.Front.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        var normal = sv.Position.Cross(sv.Velocity).Normalize();

        double alignment = rotatedFront * normal;
        Assert.True(alignment > 0.9999, $"Front should align with inclined orbital normal. Alignment: {alignment}");
    }

    [Fact]
    public void Execute_FuelNotBurned()
    {
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-670, "TestSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        NormalAttitude maneuver = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());
        maneuver.TryExecute(orbitalParams.ToStateVector());

        Assert.Equal(0.0, maneuver.FuelBurned);
    }

    [Fact]
    public void Execute_WithCustomBodyAxes()
    {
        // Spacecraft with +X as front
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-671, "CustomAxesSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams,
            bodyFront: Vector3.VectorX, bodyRight: Vector3.VectorY.Inverse(), bodyUp: Vector3.VectorZ);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        NormalAttitude maneuver = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());

        var sv = orbitalParams.ToStateVector();
        maneuver.TryExecute(sv);

        // The custom body front (+X) should align with orbital normal
        var rotatedFront = spc.BodyFront.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        var normal = sv.Position.Cross(sv.Velocity).Normalize();

        double alignment = rotatedFront * normal;
        Assert.True(alignment > 0.9999, $"Custom body front (+X) should align with orbital normal. Alignment: {alignment}");
    }
}
