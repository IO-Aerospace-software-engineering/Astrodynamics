using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Time;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers;

public class ProgradeAttitudeTests
{
    public ProgradeAttitudeTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    void Create()
    {
        FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
        Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);
        ProgradeAttitude attitude = new ProgradeAttitude(TestHelpers.EarthAtJ2000,DateTime.MinValue, TimeSpan.FromHours(1.0), eng);
        Assert.Equal(DateTime.MinValue, attitude.MinimumEpoch);
        Assert.Equal(TimeSpan.FromHours(1.0), attitude.ManeuverHoldDuration);
        Assert.NotNull(attitude.Engine);
        Assert.Equal(eng, attitude.Engine);
    }

    [Fact]
    public void Execute()
    {
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, DateTimeExtension.J2000, Frames.Frame.ICRF);
        var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        ProgradeAttitude maneuver = new ProgradeAttitude(TestHelpers.EarthAtJ2000,DateTime.MinValue, TimeSpan.Zero, spc.Engines.First());
        maneuver.TryExecute(orbitalParams.ToStateVector());

        Assert.Equal(new StateOrientation(new Quaternion(1.0, 0.0, 0.0, 0.0), Vector3.Zero, DateTimeExtension.J2000, Frames.Frame.ICRF), maneuver.StateOrientation);
        Assert.Equal(0.0, maneuver.FuelBurned);
        Assert.Equal(new Window(DateTimeExtension.J2000, TimeSpan.Zero), maneuver.ManeuverWindow);
        Assert.Equal(new Window(DateTimeExtension.J2000, TimeSpan.Zero), maneuver.ThrustWindow);
    }
}