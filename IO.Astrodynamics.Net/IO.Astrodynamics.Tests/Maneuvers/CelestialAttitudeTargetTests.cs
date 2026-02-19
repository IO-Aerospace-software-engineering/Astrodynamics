using System;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers;

public class CelestialAttitudeTargetTests
{
    public CelestialAttitudeTargetTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void Constructor_NullTarget_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CelestialAttitudeTarget(null));
    }

    [Fact]
    public void GetDirection_MatchesManualEphemerisComputation()
    {
        var sv = new StateVector(
            new Vector3(6678000.0, 0.0, 0.0),
            new Vector3(0.0, 7727.0, 0.0),
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(2021, 01, 01, 13, 0, 0),
            Frames.Frame.ICRF);

        var target = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000);
        var direction = target.GetDirection(sv);

        // Manually compute the same direction
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(sv.Epoch, sv.Observer, sv.Frame, Aberration.LT);
        var expectedDirection = (moonEphemeris.ToStateVector().Position - sv.Position).Normalize();

        Assert.True(System.Math.Abs(direction.X - expectedDirection.X) < 1e-10);
        Assert.True(System.Math.Abs(direction.Y - expectedDirection.Y) < 1e-10);
        Assert.True(System.Math.Abs(direction.Z - expectedDirection.Z) < 1e-10);
    }

    [Fact]
    public void GetDirection_ReturnsUnitVector()
    {
        var sv = new StateVector(
            new Vector3(6678000.0, 0.0, 0.0),
            new Vector3(0.0, 7727.0, 0.0),
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(2021, 01, 01, 13, 0, 0),
            Frames.Frame.ICRF);

        var target = new CelestialAttitudeTarget(TestHelpers.Sun);
        var direction = target.GetDirection(sv);

        Assert.True(System.Math.Abs(direction.Magnitude() - 1.0) < 1e-10);
    }

    [Fact]
    public void Name_ReturnsTargetName()
    {
        var target = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000);
        Assert.Equal("MOON", target.Name);
    }

    [Fact]
    public void GetDirection_NullObserverState_ThrowsArgumentNullException()
    {
        // Arrange
        var target = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => target.GetDirection(null));
    }
}
