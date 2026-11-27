using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using ZenithAttitude = IO.Astrodynamics.Maneuver.ZenithAttitude;

namespace IO.Astrodynamics.Tests.Maneuvers;

public class ZenithAttitudeTests
{
    public ZenithAttitudeTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    [Fact]
    void Create()
    {
        ZenithAttitude zenithAttitude = new ZenithAttitude(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.FromHours(1.0), TestHelpers.EarthAtJ2000);
        LabelAssert.Equal(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), zenithAttitude.MinEpoch);
        Assert.Equal(TimeSpan.FromHours(1.0), zenithAttitude.HoldDuration);
    }

    [Fact]
    public void Execute()
    {
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000Tdb, Frames.Frame.ICRF);
        var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, orbitalParams, 3000.0, new Clock("GenericClk", 65536));
        spc.AddFuelTank(new FuelTank("ft", 1000.0, 900.0, "ftA", "123456"));
        spc.AddEngine(new Engine("eng", 450, 50, spc.FuelTanks.First(), "engmk1", "12345"));
        ZenithAttitude maneuver = new ZenithAttitude(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, TestHelpers.EarthAtJ2000);
        var (_, maneuverOrientation) = maneuver.TryExecute(orbitalParams.ToStateVector());

        LabelAssert.Equal(new StateOrientation(new Quaternion(0.7071067811865476, 0.0, 0.0, -0.7071067811865476), Vector3.Zero, TimeSystem.Time.J2000Tdb, Frames.Frame.ICRF),
            maneuverOrientation);
        Assert.Equal(0.0, maneuver.FuelBurned);
        LabelAssert.Equal(new Window(TimeSystem.Time.J2000Tdb, TimeSpan.Zero), maneuver.ManeuverWindow);
        LabelAssert.Equal(new Window(TimeSystem.Time.J2000Tdb, TimeSpan.Zero), maneuver.ThrustWindow);
    }
}