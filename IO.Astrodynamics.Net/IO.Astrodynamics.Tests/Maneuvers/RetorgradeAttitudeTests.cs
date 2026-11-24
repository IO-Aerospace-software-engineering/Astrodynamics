using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers;

public class RetrogradeAttitudeTests
{
    public RetrogradeAttitudeTests()
    {
        TestKernels.EnsureSolarSystem();
    }
    [Fact]
    void Create()
    {
        FuelTank fuelTank10 = new FuelTank("My fuel tank10", 4000.0,3000.0, "ft2021","sn0");
        Engine eng = new Engine("My engine", 350.0, 50.0, fuelTank10, "model 1","sn1");
        RetrogradeAttitude attitude = new RetrogradeAttitude(TestHelpers.EarthAtJ2000,new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.FromHours(1.0), eng);
        LabelAssert.Equal(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),attitude.MinimumEpoch);
        Assert.Equal(TimeSpan.FromHours(1.0),attitude.ManeuverHoldDuration);
        Assert.NotNull(attitude.Engine);
        Assert.Equal(eng,attitude.Engine);
    }
    
    [Fact]
    public void Execute()
    {
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, orbitalParams, 3000.0, new Clock("GenericClk", 65536));
        spc.AddFuelTank(new FuelTank("ft", 1000.0, 900.0, "ftA", "123456"));
        spc.AddEngine(new Engine("eng", 450, 50, spc.FuelTanks.First(), "engmk1", "12345"));
        RetrogradeAttitude maneuver = new RetrogradeAttitude(TestHelpers.EarthAtJ2000,new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, spc.Engines.First());
        var (_, maneuverOrientation) = maneuver.TryExecute(orbitalParams.ToStateVector());

        LabelAssert.Equal(new StateOrientation(new Quaternion(0.0, -1.0, 0.0, 0.0), Vector3.Zero, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF), maneuverOrientation);
        Assert.Equal(0.0, maneuver.FuelBurned);
        LabelAssert.Equal(new Window(TimeSystem.Time.J2000TDB, TimeSpan.Zero), maneuver.ManeuverWindow);
        LabelAssert.Equal(new Window(TimeSystem.Time.J2000TDB, TimeSpan.Zero), maneuver.ThrustWindow);
    }
}