using System;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers;

public class OrbitalDirectionTargetTests
{
    public OrbitalDirectionTargetTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private StateVector CreateEquatorialStateVector()
    {
        // r = (+X), v = (+Y) — equatorial circular orbit
        return new StateVector(
            new Vector3(6678000.0, 0.0, 0.0),
            new Vector3(0.0, 7727.0, 0.0),
            TestHelpers.EarthAtJ2000,
            TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);
    }

    [Fact]
    public void Prograde_ReturnsVelocityDirection()
    {
        var sv = CreateEquatorialStateVector();
        var direction = OrbitalDirectionTarget.Prograde.GetDirection(sv);

        Assert.True(System.Math.Abs(direction.X) < 1e-10);
        Assert.True(System.Math.Abs(direction.Y - 1.0) < 1e-10);
        Assert.True(System.Math.Abs(direction.Z) < 1e-10);
        Assert.Equal("Prograde", OrbitalDirectionTarget.Prograde.Name);
    }

    [Fact]
    public void Retrograde_ReturnsAntiVelocityDirection()
    {
        var sv = CreateEquatorialStateVector();
        var direction = OrbitalDirectionTarget.Retrograde.GetDirection(sv);

        Assert.True(System.Math.Abs(direction.X) < 1e-10);
        Assert.True(System.Math.Abs(direction.Y + 1.0) < 1e-10);
        Assert.True(System.Math.Abs(direction.Z) < 1e-10);
    }

    [Fact]
    public void Nadir_ReturnsNegativePositionDirection()
    {
        var sv = CreateEquatorialStateVector();
        var direction = OrbitalDirectionTarget.Nadir.GetDirection(sv);

        Assert.True(System.Math.Abs(direction.X + 1.0) < 1e-10);
        Assert.True(System.Math.Abs(direction.Y) < 1e-10);
        Assert.True(System.Math.Abs(direction.Z) < 1e-10);
    }

    [Fact]
    public void Zenith_ReturnsPositionDirection()
    {
        var sv = CreateEquatorialStateVector();
        var direction = OrbitalDirectionTarget.Zenith.GetDirection(sv);

        Assert.True(System.Math.Abs(direction.X - 1.0) < 1e-10);
        Assert.True(System.Math.Abs(direction.Y) < 1e-10);
        Assert.True(System.Math.Abs(direction.Z) < 1e-10);
    }

    [Fact]
    public void Normal_ReturnsAngularMomentumDirection()
    {
        var sv = CreateEquatorialStateVector();
        var direction = OrbitalDirectionTarget.Normal.GetDirection(sv);

        // r x v = (+X) x (+Y) = +Z
        Assert.True(System.Math.Abs(direction.X) < 1e-10);
        Assert.True(System.Math.Abs(direction.Y) < 1e-10);
        Assert.True(System.Math.Abs(direction.Z - 1.0) < 1e-10);
    }

    [Fact]
    public void AntiNormal_ReturnsAntiAngularMomentumDirection()
    {
        var sv = CreateEquatorialStateVector();
        var direction = OrbitalDirectionTarget.AntiNormal.GetDirection(sv);

        Assert.True(System.Math.Abs(direction.X) < 1e-10);
        Assert.True(System.Math.Abs(direction.Y) < 1e-10);
        Assert.True(System.Math.Abs(direction.Z + 1.0) < 1e-10);
    }

    [Fact]
    public void AllDirections_ReturnUnitVectors()
    {
        var sv = CreateEquatorialStateVector();

        var targets = new[]
        {
            OrbitalDirectionTarget.Prograde,
            OrbitalDirectionTarget.Retrograde,
            OrbitalDirectionTarget.Nadir,
            OrbitalDirectionTarget.Zenith,
            OrbitalDirectionTarget.Normal,
            OrbitalDirectionTarget.AntiNormal
        };

        foreach (var target in targets)
        {
            var direction = target.GetDirection(sv);
            Assert.True(System.Math.Abs(direction.Magnitude() - 1.0) < 1e-10,
                $"{target.Name} direction should be unit vector. Magnitude: {direction.Magnitude()}");
        }
    }

    [Fact]
    public void Constructor_CustomDirection()
    {
        var target = new OrbitalDirectionTarget(OrbitalDirection.Normal);
        Assert.Equal(OrbitalDirection.Normal, target.Direction);
        Assert.Equal("Normal", target.Name);
    }

    [Fact]
    public void GetDirection_NullObserverState_ThrowsArgumentNullException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => OrbitalDirectionTarget.Prograde.GetDirection(null));
        Assert.Throws<ArgumentNullException>(() => OrbitalDirectionTarget.Normal.GetDirection(null));
        Assert.Throws<ArgumentNullException>(() => OrbitalDirectionTarget.Nadir.GetDirection(null));
    }

    [Fact]
    public void GetDirection_InclinedOrbit_AllDirectionsCorrect()
    {
        // r = (3,4,0)*1e6 m, v = (-4,3,0)*7500 m/s — roughly circular orbit rotated 53° in XY plane.
        // This verifies no component-swap bugs: directions are NOT axis-aligned.
        var sv = new StateVector(
            new Vector3(3.0e6, 4.0e6, 0.0),
            new Vector3(-4.0 * 7500.0, 3.0 * 7500.0, 0.0),
            TestHelpers.EarthAtJ2000,
            TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);

        // Prograde: in direction of velocity
        var progradeDir = OrbitalDirectionTarget.Prograde.GetDirection(sv);
        var velocityDir = sv.Velocity.Normalize();
        double progradeAlignment = progradeDir * velocityDir;
        Assert.True(progradeAlignment > 0.9999,
            $"Prograde should align with velocity direction. Alignment: {progradeAlignment}");

        // Nadir: in direction of -position
        var nadirDir = OrbitalDirectionTarget.Nadir.GetDirection(sv);
        var negPositionDir = sv.Position.Inverse().Normalize();
        double nadirAlignment = nadirDir * negPositionDir;
        Assert.True(nadirAlignment > 0.9999,
            $"Nadir should align with -position direction. Alignment: {nadirAlignment}");

        // Normal: in direction of h = r x v
        var normalDir = OrbitalDirectionTarget.Normal.GetDirection(sv);
        var hDir = sv.Position.Cross(sv.Velocity).Normalize();
        double normalAlignment = normalDir * hDir;
        Assert.True(normalAlignment > 0.9999,
            $"Normal should align with angular momentum direction. Alignment: {normalAlignment}");

        // Verify none of the directions are purely axis-aligned (to catch component-swap bugs)
        Assert.True(System.Math.Abs(progradeDir.X) > 0.01 && System.Math.Abs(progradeDir.Y) > 0.01,
            "Prograde should have both X and Y components in this inclined orbit");
        Assert.True(System.Math.Abs(nadirDir.X) > 0.01 && System.Math.Abs(nadirDir.Y) > 0.01,
            "Nadir should have both X and Y components in this inclined orbit");

        // Verify dot product relationships hold
        // Prograde * Velocity > 0: velocity in same half-space as prograde
        Assert.True(progradeDir * sv.Velocity > 0.0, "Prograde dot Velocity should be positive");
        // Nadir * Position < 0: nadir points away from position vector
        Assert.True(nadirDir * sv.Position < 0.0, "Nadir dot Position should be negative");
        // Normal * (r x v) > 0: normal aligns with angular momentum
        Assert.True(normalDir * sv.Position.Cross(sv.Velocity) > 0.0, "Normal dot (r x v) should be positive");
    }

    [Fact]
    public void Normal_DegenerateOrbit_ThrowsInvalidOperationException()
    {
        // r and v are parallel — h = r x v = 0, so Normal direction is undefined
        var sv = new StateVector(
            new Vector3(6678000.0, 0.0, 0.0),
            new Vector3(1000.0, 0.0, 0.0), // parallel to r
            TestHelpers.EarthAtJ2000,
            TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => OrbitalDirectionTarget.Normal.GetDirection(sv));
        Assert.Throws<InvalidOperationException>(() => OrbitalDirectionTarget.AntiNormal.GetDirection(sv));
    }
}
