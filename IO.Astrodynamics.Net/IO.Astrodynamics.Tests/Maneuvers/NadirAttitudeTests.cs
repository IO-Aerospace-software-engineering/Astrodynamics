using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers;

public class NadirAttitudeTests
{
    public NadirAttitudeTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    void Create()
    {
        FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
        Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);
        NadirAttitude zenithAttitude = new NadirAttitude(TestHelpers.EarthAtJ2000,new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.FromHours(1.0), eng);
        Assert.Equal(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), zenithAttitude.MinimumEpoch);
        Assert.Equal(TimeSpan.FromHours(1.0), zenithAttitude.ManeuverHoldDuration);
        Assert.NotNull(zenithAttitude.Engine);
        Assert.Equal(eng, zenithAttitude.Engine);
    }

    [Fact]
    public void Execute()
    {
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        NadirAttitude maneuver = new NadirAttitude(TestHelpers.EarthAtJ2000,new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());
        maneuver.TryExecute(orbitalParams.ToStateVector());

        Assert.Equal(new StateOrientation(new Quaternion(0.7071067811865476, 0.0, 0.0, 0.7071067811865476), Vector3.Zero, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF),
            maneuver.StateOrientation);
        Assert.Equal(0.0, maneuver.FuelBurned);
        Assert.Equal(new Window(TimeSystem.Time.J2000TDB, TimeSpan.Zero), maneuver.ManeuverWindow);
        Assert.Equal(new Window(TimeSystem.Time.J2000TDB, TimeSpan.Zero), maneuver.ThrustWindow);
    }

    /// <summary>
    /// A state after the hold of an attitude, the window set by its first execution, makes the maneuver chained after it
    /// the spacecraft's standby maneuver, not before the end of the hold; an attitude changes no velocity.
    /// </summary>
    [Fact]
    public void AStateAfterTheHold_SetsTheNextManeuverStandby()
    {
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-1107, "GenericSpacecraft1107", 1000.0, 3000.0, new Clock("GenericClk1107", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        var nadir = new ExecuteProbe(spc.Engines.First());
        var prograde = new ProgradeAttitude(TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, TimeSpan.Zero, spc.Engines.First());
        nadir.SetNextManeuver(prograde);
        spc.SetStandbyManeuver(nadir);
        var start = orbitalParams.ToStateVector();

        nadir.TryExecute(start);
        Assert.Equal(nadir, spc.StandbyManeuver);

        var afterTheHold = start.ToStateVector(TimeSystem.Time.J2000TDB.AddSeconds(20.0));
        nadir.TryExecute(afterTheHold);
        Assert.Equal(prograde, spc.StandbyManeuver);
        Assert.Contains(nadir, spc.ExecutedManeuvers);
        Assert.Equal(TimeSystem.Time.J2000TDB.AddSeconds(10.0), prograde.MinimumEpoch);
        Assert.Equal(Vector3.Zero, nadir.DeltaVOf(start));
    }

    private sealed class ExecuteProbe(Engine engine) : NadirAttitude(TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, TimeSpan.FromSeconds(10.0), engine)
    {
        public Vector3 DeltaVOf(StateVector state) => Execute(state);
    }
}
