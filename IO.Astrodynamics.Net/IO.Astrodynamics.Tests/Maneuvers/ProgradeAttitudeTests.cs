using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers;

public class ProgradeAttitudeTests
{
    public ProgradeAttitudeTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    [Fact]
    void Create()
    {
        ProgradeAttitude attitude = new ProgradeAttitude(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.FromHours(1.0), TestHelpers.EarthAtJ2000);
        LabelAssert.Equal(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), attitude.MinEpoch);
        Assert.Equal(TimeSpan.FromHours(1.0), attitude.HoldDuration);
    }

    [Fact]
    public void Execute()
    {
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, orbitalParams, 3000.0, new Clock("GenericClk", 65536));
        spc.AddFuelTank(new FuelTank("ft", 1000.0, 900.0, "ftA", "123456"));
        spc.AddEngine(new Engine("eng", 450, 50, spc.FuelTanks.First(), "engmk1", "12345"));
        ProgradeAttitude maneuver = new ProgradeAttitude(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, TestHelpers.EarthAtJ2000);
        var (_, maneuverOrientation) = maneuver.TryExecute(orbitalParams.ToStateVector());

        LabelAssert.Equal(new StateOrientation(new Quaternion(1.0, 0.0, 0.0, 0.0), Vector3.Zero, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF), maneuverOrientation);
        Assert.Equal(0.0, maneuver.FuelBurned);
        LabelAssert.Equal(new Window(TimeSystem.Time.J2000TDB, TimeSpan.Zero), maneuver.ManeuverWindow);
        LabelAssert.Equal(new Window(TimeSystem.Time.J2000TDB, TimeSpan.Zero), maneuver.ThrustWindow);
    }
}