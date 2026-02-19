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
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
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
}
