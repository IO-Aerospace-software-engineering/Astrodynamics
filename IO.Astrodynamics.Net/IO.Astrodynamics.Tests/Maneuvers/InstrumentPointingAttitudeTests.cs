using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Time;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers;

public class InstrumentPointingAttitudeTests
{
    public InstrumentPointingAttitudeTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    void Create()
    {
        FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
        Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);
        var spc = TestHelpers.Spacecraft;
        spc.AddCircularInstrument(-172600, "CAM602", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.0, -System.Math.PI * 0.5, 0.0));
        InstrumentPointingToAttitude attitude =
            new InstrumentPointingToAttitude(DateTime.MinValue, TimeSpan.FromHours(1.0), spc.Instruments.First(), TestHelpers.EarthAtJ2000, eng);
        Assert.Equal(DateTime.MinValue, attitude.MinimumEpoch);
        Assert.Equal(TimeSpan.FromHours(1.0), attitude.ManeuverHoldDuration);
        Assert.NotNull(attitude.Engine);
        Assert.Equal(eng, attitude.Engine);
        Assert.Equal(spc.Instruments.First(), attitude.Instrument);
        Assert.Equal(TestHelpers.EarthAtJ2000, attitude.Target);
    }

    [Fact]
    public void Execute()
    {
        var orbitalParams = new StateVector(new Vector3(6678000.0, 0.0, 0.0), new Vector3(0.0, 7727.0, 0.0), TestHelpers.EarthAtJ2000, new DateTime(2021, 01, 01, 13, 0, 0),
            Frames.Frame.ICRF);
        var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

        spc.AddCircularInstrument(-666600, "inst", "instmodel", 1.57, Vector3.VectorZ, Vector3.VectorY, new Vector3(-Astrodynamics.Constants.PI2, 0.0, 0.0));
        InstrumentPointingToAttitude maneuver =
            new InstrumentPointingToAttitude(DateTime.MinValue, TimeSpan.FromSeconds(10.0), spc.Instruments.First(), TestHelpers.MoonAtJ2000, spc.Engines.First());
        maneuver.TryExecute(orbitalParams.ToStateVector());

        var pointingVector = spc.Instruments.First().GetBoresightInSpacecraftFrame().Rotate(maneuver.StateOrientation.Rotation).Normalize();
        Assert.Equal(new Vector3(0.6452831377480278, 0.67044862664937221, -0.36620801624490784), pointingVector, TestHelpers.VectorComparer);
        Assert.Equal(0.0, maneuver.FuelBurned);
        Assert.Equal(new Window(new DateTime(2021, 01, 01, 13, 0, 0), TimeSpan.FromSeconds(10.0)), maneuver.ManeuverWindow);
        Assert.Equal(new Window(new DateTime(2021, 01, 01, 13, 0, 0), TimeSpan.Zero), maneuver.ThrustWindow);
    }
}