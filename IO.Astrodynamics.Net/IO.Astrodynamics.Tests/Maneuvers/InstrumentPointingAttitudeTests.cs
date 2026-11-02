using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Maneuvers;

public class InstrumentPointingAttitudeTests
{
    private readonly ITestOutputHelper _output;

    public InstrumentPointingAttitudeTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    void Create()
    {
        FuelTank fuelTank10 = new FuelTank("My fuel tank10", "ft2021", "sn0", 4000.0, 3000.0);
        Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank10);
        var spc = TestHelpers.Spacecraft;
        spc.AddCircularInstrument(-172600, "CAM602", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, new Vector3(0.0, -System.Math.PI * 0.5, 0.0));
        InstrumentPointingToAttitude attitude =
            new InstrumentPointingToAttitude(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.FromHours(1.0), spc.Instruments.First(), TestHelpers.EarthAtJ2000, eng);
        Assert.Equal(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), attitude.MinimumEpoch);
        Assert.Equal(TimeSpan.FromHours(1.0), attitude.ManeuverHoldDuration);
        Assert.NotNull(attitude.Engine);
        Assert.Equal(eng, attitude.Engine);
        Assert.Equal(spc.Instruments.First(), attitude.Instrument);
        Assert.Equal(TestHelpers.EarthAtJ2000, attitude.Target);
    }

    [Theory]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(-System.Math.PI / 2.0, 0.0, 0.0)]
    [InlineData(0.0, System.Math.PI / 2.0, 0.0)]
    [InlineData(0.3, 0.5, 0.2)]
    [InlineData(-0.7, 1.1, 2.3)]
    public void Execute(double x, double y, double z)
    {
        var epoch = new TimeSystem.Time(2021, 01, 01, 13, 0, 0);
        var orbitalParams = new StateVector(new Vector3(6678000.0, 0.0, 0.0), new Vector3(0.0, 7727.0, 0.0), TestHelpers.EarthAtJ2000, epoch,
            Frames.Frame.ICRF);
        var spc = new Spacecraft(-666, "GenericSpacecraft", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

        spc.AddCircularInstrument(-666600, "inst", "instmodel", 1.57, Vector3.VectorZ, Vector3.VectorY, new Vector3(x, y, z));
        InstrumentPointingToAttitude maneuver =
            new InstrumentPointingToAttitude(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.FromSeconds(10.0), spc.Instruments.First(), TestHelpers.MoonAtJ2000, spc.Engines.First());
        maneuver.TryExecute(orbitalParams.ToStateVector());

        // The boresight, in the spacecraft frame by the Euler angles of the frame kernel (#524), turned by the attitude onto
        // the direction of the Moon: its geometric position from SPICE (the state is made relative to the Moon, the centre
        // of the maneuver) less the spacecraft's. Before, the attitude was the inverse rotation: the boresight 2 alpha from
        // the Moon, alpha its angle from the boresight (137.5 deg at zero orientation, reader 4 of the design of #537)
        var moon = TestHelpers.MoonAtJ2000.GetEphemeris(epoch, TestHelpers.EarthAtJ2000, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position;
        var toMoon = moon - orbitalParams.Position;
        var pointingVector = spc.Instruments.First().GetBoresightInSpacecraftFrame().Rotate(maneuver.StateOrientation.Rotation);
        double angle = System.Math.Atan2(pointingVector.Cross(toMoon).Magnitude(), pointingVector * toMoon);
        _output.WriteLine($"MEASURE InstrumentPointing ({x}, {y}, {z}): boresight from the Moon {angle:E3} rad");
        // PROVISIONAL: twice the worst over the five orientations on Linux x64 (2026-11-02: 2.57e-16 rad at (0, pi/2, 0));
        // to be set from the Windows and macOS arm64 CI logs
        Assert.True(angle <= InstrumentPointingTolerance, $"boresight {angle:E3} rad from the Moon");
        Assert.Equal(Frames.Frame.ICRF, maneuver.StateOrientation.ReferenceFrame);
        Assert.Equal(0.0, maneuver.FuelBurned);
        Assert.Equal(new Window(epoch, TimeSpan.FromSeconds(10.0)), maneuver.ManeuverWindow);
        Assert.Equal(new Window(epoch, TimeSpan.Zero), maneuver.ThrustWindow);
    }

    private const double InstrumentPointingTolerance = 5.2e-16;
}
