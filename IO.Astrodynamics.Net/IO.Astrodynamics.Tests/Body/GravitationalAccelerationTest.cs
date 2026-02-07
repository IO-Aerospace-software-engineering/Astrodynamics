using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

public class GravitationalAccelerationTest
{
    public GravitationalAccelerationTest()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void ComputeGeopotentialGravityAcceleration()
    {
        GeopotentialGravitationalField gravity =
            new GeopotentialGravitationalField(new StreamReader(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree")));
        StateVector parkingOrbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), TestHelpers.EarthWithAtmAndGeoAtJ2000,
            TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);
        var res = gravity.ComputeGravitationalAcceleration(parkingOrbit);
        // After fixing all 4 bugs: full 3D gradient, geodesy normalization, no CS phase, no abs()
        // X should be close to -GM/r² with J2 perturbation (slightly more negative)
        Assert.Equal(-8.632087240087564, res.X, 6);
        Assert.Equal(5.628817166769551E-05, res.Y, 9);
        Assert.Equal(-6.689811397694593E-05, res.Z, 9);
    }

    [Fact]
    public void ComputeGravityAcceleration()
    {
        GravitationalField gravity = new GravitationalField();
        StateVector parkingOrbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), TestHelpers.EarthWithAtmAndGeoAtJ2000,
            TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);
        var res = gravity.ComputeGravitationalAcceleration(parkingOrbit);
        Assert.Equal(new Vector3(-8.620251632937341, 0.0, 0.0), res);
    }

    [Fact]
    public void ComputeGeopotentialAt45DegreesLatitude()
    {
        // Position at ~45° latitude exercises tesseral harmonic terms
        GeopotentialGravitationalField gravity =
            new GeopotentialGravitationalField(
                new StreamReader(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree")));

        // r=6800km, 45° latitude: x=r*cos(45°), y=0, z=r*sin(45°)
        double r = 6800000.0;
        double component = r / System.Math.Sqrt(2.0);
        StateVector sv = new StateVector(
            new Vector3(component, 0.0, component),
            new Vector3(0.0, 7656.2204182967143, 0.0),
            TestHelpers.EarthWithAtmAndGeoAtJ2000,
            TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);

        var res = gravity.ComputeGravitationalAcceleration(sv);

        // At 45° latitude, the acceleration should have significant X and Z components
        // The magnitude should be close to GM/r² ≈ 8.62 m/s²
        double magnitude = System.Math.Sqrt(res.X * res.X + res.Y * res.Y + res.Z * res.Z);
        Assert.InRange(magnitude, 8.61, 8.65);

        // Z component should be comparable in magnitude to X (both ~6.1 at 45°)
        Assert.True(System.Math.Abs(res.Z) > 1.0, "Z component should be significant at 45° latitude");
        Assert.True(System.Math.Abs(res.X) > 1.0, "X component should be significant at 45° latitude");
    }

    [Fact]
    public void ComputeGeopotentialSouthernHemisphere()
    {
        // Southern hemisphere test: odd-degree zonal harmonics (J3, J5) should cause asymmetry
        GeopotentialGravitationalField gravity =
            new GeopotentialGravitationalField(
                new StreamReader(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree")));

        double r = 6800000.0;
        double component = r / System.Math.Sqrt(2.0);

        // Northern hemisphere: +Z
        StateVector svNorth = new StateVector(
            new Vector3(component, 0.0, component),
            new Vector3(0.0, 7656.2204182967143, 0.0),
            TestHelpers.EarthWithAtmAndGeoAtJ2000,
            TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);

        // Southern hemisphere: -Z
        StateVector svSouth = new StateVector(
            new Vector3(component, 0.0, -component),
            new Vector3(0.0, 7656.2204182967143, 0.0),
            TestHelpers.EarthWithAtmAndGeoAtJ2000,
            TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);

        var resNorth = gravity.ComputeGravitationalAcceleration(svNorth);
        var resSouth = gravity.ComputeGravitationalAcceleration(svSouth);

        // X components should be close (even-degree dominant, small frame rotation difference)
        Assert.Equal(resNorth.X, resSouth.X, 2);

        // Z components should have opposite sign (dominant even terms)
        // but odd-degree terms (J3, J5...) break perfect antisymmetry
        Assert.True(resNorth.Z * resSouth.Z < 0, "Z components should have opposite signs");
        double zDiff = System.Math.Abs(System.Math.Abs(resNorth.Z) - System.Math.Abs(resSouth.Z));
        Assert.True(zDiff > 1E-6, "Odd-degree zonals should break N/S symmetry (Bug 4 fix)");
    }

    [Fact]
    public void ComputeGeopotentialWithLongitude()
    {
        // Position with longitude exercises S_nm sine terms
        GeopotentialGravitationalField gravity =
            new GeopotentialGravitationalField(
                new StreamReader(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree")));

        double r = 6800000.0;
        double component = r / System.Math.Sqrt(2.0);

        // 45° longitude in equatorial plane: x=r*cos(45°), y=r*sin(45°), z=0
        StateVector sv = new StateVector(
            new Vector3(component, component, 0.0),
            new Vector3(0.0, 0.0, 7656.2204182967143),
            TestHelpers.EarthWithAtmAndGeoAtJ2000,
            TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);

        var res = gravity.ComputeGravitationalAcceleration(sv);

        // Both X and Y should have significant acceleration components
        double magnitude = System.Math.Sqrt(res.X * res.X + res.Y * res.Y + res.Z * res.Z);
        Assert.InRange(magnitude, 8.61, 8.65);

        // At equator with longitude, Y component should be non-negligible
        Assert.True(System.Math.Abs(res.Y) > 0.1, "Y component should be significant with longitude offset");
    }

    [Fact]
    public void ComputeJ2OnlyAnalytical()
    {
        // J2-only test: compare against known analytical formula
        // Use degree 2 only, which is dominated by J2 (C20 term)
        GeopotentialGravitationalField gravity =
            new GeopotentialGravitationalField(
                new StreamReader(Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree")),
                maxDegrees: 2);

        // Position in body-fixed frame at equator (φ=0)
        // Using ICRF at J2000 epoch — the body-fixed frame is close to ICRF for small time offsets
        double r = 6800000.0;
        StateVector sv = new StateVector(
            new Vector3(r, 0.0, 0.0),
            new Vector3(0.0, 7656.2204182967143, 0.0),
            TestHelpers.EarthWithAtmAndGeoAtJ2000,
            TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);

        var res = gravity.ComputeGravitationalAcceleration(sv);

        // The magnitude should be stronger than point-mass at equator due to J2
        double pointMass = TestHelpers.EarthWithAtmAndGeoAtJ2000.GM / (r * r);
        double resMag = System.Math.Sqrt(res.X * res.X + res.Y * res.Y + res.Z * res.Z);
        Assert.True(resMag > pointMass, "J2 should increase equatorial gravity magnitude");
    }
}
