using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers;

public class NormalAttitudeTests
{
    public NormalAttitudeTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    void Create()
    {
        FuelTank fuelTank = new FuelTank("My fuel tank", "ft2021", "sn0", 4000.0, 3000.0);
        Engine eng = new Engine("My engine", "model 1", "sn1", 350.0, 50.0, fuelTank);
        NormalAttitude attitude = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.FromHours(1.0), eng);
        Assert.Equal(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), attitude.MinimumEpoch);
        Assert.Equal(TimeSpan.FromHours(1.0), attitude.ManeuverHoldDuration);
        Assert.NotNull(attitude.Engine);
        Assert.Equal(eng, attitude.Engine);
    }

    [Fact]
    public void Execute_EquatorialOrbit_NormalIsPositiveZ()
    {
        // For r=(+X, 0, 0) and v=(0, +Y, 0): h = r x v = +Z
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-668, "NormalTestSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        NormalAttitude maneuver = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());

        var sv = orbitalParams.ToStateVector();
        maneuver.TryExecute(sv);

        // The body front (+Y) should be rotated to align with +Z (normal direction)
        var rotatedFront = Spacecraft.Front.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        var normal = sv.Position.Cross(sv.Velocity).Normalize();

        // Verify the rotated front aligns with orbital normal
        double alignment = rotatedFront * normal;
        Assert.True(alignment > 0.9999, $"Front should align with orbital normal (+Z). Alignment: {alignment}");
    }

    [Fact]
    public void Execute_InclinedOrbit_NormalIsOffAxis()
    {
        // Inclined orbit: normal should have X and Z components
        var orbitalParams = new KeplerianElements(7000000.0, 0.0, 45.0 * Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, 0.0,
            TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-669, "InclinedTestSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        NormalAttitude maneuver = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());

        var sv = orbitalParams.ToStateVector();
        maneuver.TryExecute(sv);

        var rotatedFront = Spacecraft.Front.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        var normal = sv.Position.Cross(sv.Velocity).Normalize();

        double alignment = rotatedFront * normal;
        Assert.True(alignment > 0.9999, $"Front should align with inclined orbital normal. Alignment: {alignment}");
    }

    [Fact]
    public void Execute_FuelNotBurned()
    {
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-670, "TestSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        NormalAttitude maneuver = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());
        maneuver.TryExecute(orbitalParams.ToStateVector());

        Assert.Equal(0.0, maneuver.FuelBurned);
    }

    [Fact]
    public void Execute_WithCustomBodyAxes()
    {
        // Spacecraft with +X as front
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-671, "CustomAxesSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams,
            bodyFront: Vector3.VectorX, bodyRight: Vector3.VectorY.Inverse(), bodyUp: Vector3.VectorZ);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        NormalAttitude maneuver = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());

        var sv = orbitalParams.ToStateVector();
        maneuver.TryExecute(sv);

        // The custom body front (+X) should align with orbital normal
        var rotatedFront = spc.BodyFront.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        var normal = sv.Position.Cross(sv.Velocity).Normalize();

        double alignment = rotatedFront * normal;
        Assert.True(alignment > 0.9999, $"Custom body front (+X) should align with orbital normal. Alignment: {alignment}");
    }

    [Fact]
    public void Execute_PolarOrbit_NormalIsInEquatorialPlane()
    {
        // Polar orbit (i=90°, RAAN=0°): h = r x v lies in the equatorial plane.
        // At trueAnomaly=0: r = (a,0,0), v = (0,0,vCirc) → h = r x v = (0*vCirc - 0*0, 0*0 - a*vCirc, 0) = (0,-a*vCirc,0)
        // So h direction = (0,-1,0) — purely in equatorial plane with zero Z component.
        var orbitalParams = new KeplerianElements(
            7000000.0, 0.0,
            90.0 * Astrodynamics.Constants.Deg2Rad, // polar orbit
            0.0, 0.0, 0.0,
            TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-676, "PolarSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        NormalAttitude maneuver = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());

        var sv = orbitalParams.ToStateVector();
        maneuver.TryExecute(sv);

        // Compute expected normal: r x v, normalized
        var normal = sv.Position.Cross(sv.Velocity).Normalize();

        // The normal should have near-zero Z component (lies in equatorial plane)
        Assert.True(System.Math.Abs(normal.Z) < 1e-10,
            $"Polar orbit normal Z should be ~0 (equatorial plane). Got: {normal.Z}");

        // The rotated front should align with the orbital normal
        var rotatedFront = Spacecraft.Front.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        double alignment = rotatedFront * normal;
        Assert.True(alignment > 0.9999,
            $"Front should align with polar orbit normal. Alignment: {alignment}");
    }

    [Fact]
    public void Execute_RetrogradeOrbit_NormalPointsNegativeY()
    {
        // Retrograde equatorial orbit (i=180°): at trueAnomaly=0, r=(a,0,0), v=(0,-vCirc,0)
        // h = r x v = (a,0,0) x (0,-vCirc,0) = (0*0-0*(-vCirc), 0*0-a*0, a*(-vCirc)-0*0)
        //           = (0, 0, -a*vCirc) → normalized: (0,0,-1)
        // So normal = -Z for retrograde equatorial orbit
        var orbitalParams = new KeplerianElements(
            7000000.0, 0.0,
            180.0 * Astrodynamics.Constants.Deg2Rad, // retrograde
            0.0, 0.0, 0.0,
            TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-677, "RetrogradeSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        NormalAttitude maneuver = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());

        var sv = orbitalParams.ToStateVector();
        maneuver.TryExecute(sv);

        var normal = sv.Position.Cross(sv.Velocity).Normalize();

        // For retrograde equatorial, normal should point in -Z direction
        Assert.True(normal.Z < -0.9999,
            $"Retrograde equatorial orbit normal should be -Z. Got: ({normal.X:F6},{normal.Y:F6},{normal.Z:F6})");

        var rotatedFront = Spacecraft.Front.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        double alignment = rotatedFront * normal;
        Assert.True(alignment > 0.9999,
            $"Front should align with retrograde orbit normal (-Z). Alignment: {alignment}");
    }

    [Fact]
    public void Execute_EquatorialOrbit_ExactQuaternion()
    {
        // For equatorial circular orbit, orbital normal = +Z.
        // Default body front = +Y. The shortest rotation from +Y to +Z:
        //   Rotation axis: +Y x +Z = +X (unit)
        //   Angle: acos(+Y · +Z) = acos(0) = +90°
        //   Quaternion: (cos(45°), sin(45°)*+X̂) = (√2/2, √2/2, 0, 0)
        var orbitalParams = new KeplerianElements(42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0,
            TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-678, "ExactQSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        NormalAttitude maneuver = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());

        var sv = orbitalParams.ToStateVector();
        maneuver.TryExecute(sv);

        // Primary check: rotated body front should align with orbital normal (+Z)
        var rotatedFront = Spacecraft.Front.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        Assert.True(System.Math.Abs(rotatedFront.X) < 1e-6,
            $"Rotated front X should be ~0. Got: {rotatedFront.X}");
        Assert.True(System.Math.Abs(rotatedFront.Y) < 1e-6,
            $"Rotated front Y should be ~0. Got: {rotatedFront.Y}");
        Assert.True(System.Math.Abs(rotatedFront.Z - 1.0) < 1e-6,
            $"Rotated front Z should be ~1. Got: {rotatedFront.Z}");

        // Quaternion check: rotation of +Y to +Z is +90° around +X axis.
        // q = (cos(45°), sin(45°), 0, 0) = (√2/2, +√2/2, 0, 0)
        double expectedW = System.Math.Sqrt(0.5);  // cos(45°)
        double expectedX = System.Math.Sqrt(0.5);  // sin(45°) * axis.X = sin(45°) * 1
        var q = maneuver.StateOrientation.Rotation;

        // Account for quaternion double-cover: q and -q represent the same rotation.
        // Canonical form: pick the representative that matches the expected sign pattern.
        double dotQ = q.W * expectedW + q.VectorPart.X * expectedX;
        double sign = dotQ >= 0 ? 1.0 : -1.0;

        Assert.True(System.Math.Abs(sign * q.W - expectedW) < 1e-6,
            $"Quaternion W should be {expectedW:F6}. Got: {sign * q.W:F6}");
        Assert.True(System.Math.Abs(sign * q.VectorPart.X - expectedX) < 1e-6,
            $"Quaternion X should be {expectedX:F6}. Got: {sign * q.VectorPart.X:F6}");
        Assert.True(System.Math.Abs(q.VectorPart.Y) < 1e-6,
            $"Quaternion Y should be 0. Got: {q.VectorPart.Y:F6}");
        Assert.True(System.Math.Abs(q.VectorPart.Z) < 1e-6,
            $"Quaternion Z should be 0. Got: {q.VectorPart.Z:F6}");
    }

    [Fact]
    public void Execute_DegenerateOrbit_ThrowsInvalidOperationException()
    {
        // r and v are parallel (radial trajectory) — h = r x v = 0
        var sv = new StateVector(
            new Vector3(6678000.0, 0.0, 0.0),
            new Vector3(1000.0, 0.0, 0.0), // parallel to r
            TestHelpers.EarthAtJ2000,
            TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);

        var spc = new Spacecraft(-679, "DegenerateSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), sv);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        NormalAttitude maneuver = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => maneuver.TryExecute(sv));
    }

    [Fact]
    public void Execute_HighEccentricityOrbit_NormalAligned()
    {
        // Highly eccentric orbit (e=0.9): normal direction should still be h = r x v, normalized
        var orbitalParams = new KeplerianElements(
            42164000.0, 0.9, // high eccentricity
            45.0 * Astrodynamics.Constants.Deg2Rad,
            30.0 * Astrodynamics.Constants.Deg2Rad,
            60.0 * Astrodynamics.Constants.Deg2Rad,
            0.0, // at periapsis
            TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spc = new Spacecraft(-680, "HighEccSC", 1000.0, 3000.0, new Clock("GenericClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
        NormalAttitude maneuver = new NormalAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, spc.Engines.First());

        var sv = orbitalParams.ToStateVector();
        maneuver.TryExecute(sv);

        var normal = sv.Position.Cross(sv.Velocity).Normalize();
        var rotatedFront = Spacecraft.Front.Rotate(maneuver.StateOrientation.Rotation).Normalize();

        double alignment = rotatedFront * normal;
        Assert.True(alignment > 0.9999,
            $"Front should align with high-eccentricity orbit normal. Alignment: {alignment}");
    }
}
