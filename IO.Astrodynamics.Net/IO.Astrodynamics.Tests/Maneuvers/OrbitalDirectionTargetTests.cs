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
}
