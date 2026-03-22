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
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
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

        // Manually compute the same direction (default is now Aberration.None)
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(sv.Epoch, sv.Observer, sv.Frame, Aberration.None);
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

    [Fact]
    public void DefaultAberration_IsNone()
    {
        var target = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000);
        Assert.Equal(Aberration.None, target.Aberration);
    }

    [Fact]
    public void ExplicitAberration_LT_IsStored()
    {
        var target = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000, Aberration.LT);
        Assert.Equal(Aberration.LT, target.Aberration);
    }

    [Fact]
    public void GetDirection_WithNone_MatchesGeometricEphemeris()
    {
        var sv = new StateVector(
            new Vector3(6678000.0, 0.0, 0.0),
            new Vector3(0.0, 7727.0, 0.0),
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(2021, 01, 01, 13, 0, 0),
            Frames.Frame.ICRF);

        var target = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000, Aberration.None);
        var direction = target.GetDirection(sv);

        // Manually compute with Aberration.None
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(sv.Epoch, sv.Observer, sv.Frame, Aberration.None);
        var expectedDirection = (moonEphemeris.ToStateVector().Position - sv.Position).Normalize();

        Assert.True(System.Math.Abs(direction.X - expectedDirection.X) < 1e-10);
        Assert.True(System.Math.Abs(direction.Y - expectedDirection.Y) < 1e-10);
        Assert.True(System.Math.Abs(direction.Z - expectedDirection.Z) < 1e-10);
    }

    [Fact]
    public void GetDirection_LT_DiffersFromNone()
    {
        var sv = new StateVector(
            new Vector3(6678000.0, 0.0, 0.0),
            new Vector3(0.0, 7727.0, 0.0),
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(2021, 01, 01, 13, 0, 0),
            Frames.Frame.ICRF);

        var targetLT = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000, Aberration.LT);
        var targetNone = new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000, Aberration.None);

        var directionLT = targetLT.GetDirection(sv);
        var directionNone = targetNone.GetDirection(sv);

        // LT and None should produce different directions (Moon light-time is ~1.28s)
        var diff = new Vector3(
            directionLT.X - directionNone.X,
            directionLT.Y - directionNone.Y,
            directionLT.Z - directionNone.Z);

        Assert.True(diff.Magnitude() > 1e-12, "LT and None should produce measurably different directions");
    }
}
