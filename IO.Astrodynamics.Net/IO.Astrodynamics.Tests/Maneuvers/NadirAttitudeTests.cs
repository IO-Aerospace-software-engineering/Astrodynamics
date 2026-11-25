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
        TestKernels.EnsureSolarSystem();
    }

    [Fact]
    void Create()
    {
        NadirAttitude zenithAttitude = new NadirAttitude(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.FromHours(1.0), TestHelpers.EarthAtJ2000);
        LabelAssert.Equal(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), zenithAttitude.MinEpoch);
        Assert.Equal(TimeSpan.FromHours(1.0), zenithAttitude.HoldDuration);
    }

    [Fact]
    public void Execute()
    {
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, orbitalParams, 3000.0, new Clock("GenericClk", 65536));
        spc.AddFuelTank(new FuelTank("ft", 1000.0, 900.0, "ftA", "123456"));
        spc.AddEngine(new Engine("eng", 450, 50, spc.FuelTanks.First(), "engmk1", "12345"));
        NadirAttitude maneuver = new NadirAttitude(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, TestHelpers.EarthAtJ2000);
        var (_, maneuverOrientation) = maneuver.TryExecute(orbitalParams.ToStateVector());

        LabelAssert.Equal(new StateOrientation(new Quaternion(0.7071067811865476, 0.0, 0.0, 0.7071067811865476), Vector3.Zero, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF),
            maneuverOrientation);
        Assert.Equal(0.0, maneuver.FuelBurned);
        LabelAssert.Equal(new Window(TimeSystem.Time.J2000TDB, TimeSpan.Zero), maneuver.ManeuverWindow);
        LabelAssert.Equal(new Window(TimeSystem.Time.J2000TDB, TimeSpan.Zero), maneuver.ThrustWindow);
    }

    /// <summary>
    /// An attitude changes no velocity: its execution returns the state it was given, and moves no chain (the run advances
    /// the chain on its attitude timeline; the branch of the execution that set the next maneuver in standby, after the
    /// hold, was dead in a run and is removed in lot L5b with the engine of the attitude, decisions 48 and 93 of #668).
    /// </summary>
    [Fact]
    public void TheExecution_ReturnsTheStateItWasGiven_AndMovesNoChain()
    {
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-1107, "GenericSpacecraft1107", 1000.0, orbitalParams, 3000.0, new Clock("GenericClk1107", 65536));
        var nadir = new NadirAttitude(TimeSystem.Time.J2000TDB, TimeSpan.FromSeconds(10.0), TestHelpers.EarthAtJ2000);
        var prograde = new ProgradeAttitude(TimeSystem.Time.J2000TDB, TimeSpan.Zero, TestHelpers.EarthAtJ2000);
        spc.SetManeuvers(nadir, prograde);
        var start = orbitalParams.ToStateVector();

        nadir.TryExecute(start);
        var afterTheHold = start.ToStateVector(TimeSystem.Time.J2000TDB.AddSeconds(20.0));

        Assert.Same(afterTheHold, nadir.TryExecute(afterTheHold).sv);
        Assert.Equal(nadir, spc.StandbyManeuver);
        Assert.Empty(spc.ExecutedManeuvers);
        LabelAssert.Equal(TimeSystem.Time.J2000TDB, prograde.MinEpoch);
    }
}
