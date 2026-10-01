using System;
using System.IO;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces;

public class AlbedoRadiationPressureTests
{
    private static readonly DirectoryInfo SolarSystemKernelPath = new("Data/SolarSystem");

    public AlbedoRadiationPressureTests()
    {
        SpiceAPI.Instance.LoadKernels(SolarSystemKernelPath);
    }

    private static CelestialBody CreateEarth(double albedo = 0.306)
    {
        return new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB, albedo: albedo);
    }

    private static Spacecraft CreateSpacecraft(CelestialBody observer, double mass = 100.0,
        double sectionalArea = 1.0, double solarRadiationCoeff = 1.0)
    {
        Clock clk = new Clock("My clock", 256);
        return new Spacecraft(-1001, "MySpacecraft", mass, 10000.0, clk,
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0),
                observer, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF),
            sectionalArea: sectionalArea, solarRadiationCoeff: solarRadiationCoeff);
    }

    /// <summary>
    /// Computes the expected albedo acceleration magnitude from the Lambertian sphere formula:
    ///   a = (L_sun / (4πc)) · α · Cr · (A/m) · (2/(3π)) · (R_body/d_sun_body)² · f(φ) / r_sc²
    /// where f(φ) = (π−φ)·cos(φ) + sin(φ)
    /// </summary>
    private static double ExpectedAlbedoAcceleration(
        double alpha, double cr, double area, double mass,
        double rBody, double dSunBody, double rSc, double phi)
    {
        double lambertian = (System.Math.PI - phi) * System.Math.Cos(phi) + System.Math.Sin(phi);
        if (lambertian <= 0.0) return 0.0;

        return (IO.Astrodynamics.Constants.SolarMeanRadiativeLuminosity / (4.0 * System.Math.PI * IO.Astrodynamics.Constants.C))
               * alpha * cr * (area / mass)
               * (2.0 / (3.0 * System.Math.PI))
               * (rBody / dSunBody) * (rBody / dSunBody)
               * lambertian
               / (rSc * rSc);
    }

    /// <summary>
    /// Creates a sub-solar state vector: spacecraft placed toward the Sun at given radius.
    /// Returns the state, the Sun distance from body, and the Sun direction unit vector.
    /// </summary>
    private static (StateVector state, double dSunBody, Vector3 sunDir) CreateSubSolarState(
        CelestialBody body, double rSc, TimeSystem.Time epoch)
    {
        var sunPos = Stars.SUN_BODY.GetEphemeris(epoch, body, Frames.Frame.ICRF, Aberration.LT)
            .ToStateVector().Position;
        var sunDir = sunPos.Normalize();
        double dSunBody = sunPos.Magnitude();

        var scPos = sunDir * rSc;
        var scVel = sunDir.Cross(Vector3.VectorZ).Normalize() * 7656.0;
        var state = new StateVector(scPos, scVel, body, epoch, Frames.Frame.ICRF);

        return (state, dSunBody, sunDir);
    }

    #region Constructor Validation

    [Fact]
    public void ConstructorThrowsOnNullSpacecraft()
    {
        var earth = CreateEarth();
        Assert.Throws<ArgumentNullException>(() => new AlbedoRadiationPressure(null, earth));
    }

    [Fact]
    public void ConstructorThrowsOnNullReflectingBody()
    {
        var earth = CreateEarth();
        var spc = CreateSpacecraft(earth);
        Assert.Throws<ArgumentNullException>(() => new AlbedoRadiationPressure(spc, null));
    }

    [Fact]
    public void ConstructorThrowsOnZeroAlbedo()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB, albedo: 0.0);
        var spc = CreateSpacecraft(earth);
        Assert.Throws<ArgumentException>(() => new AlbedoRadiationPressure(spc, earth));
    }

    [Fact]
    public void ConstructorThrowsOnNegativeAlbedo()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB, albedo: -0.1);
        var spc = CreateSpacecraft(earth);
        Assert.Throws<ArgumentException>(() => new AlbedoRadiationPressure(spc, earth));
    }

    #endregion

    #region Acceleration Behavior

    [Fact]
    public void SubSolarPointMatchesAnalyticalFormula()
    {
        // At sub-solar point (phi=0), Lambertian factor = pi
        var earth = CreateEarth();
        var epoch = TimeSystem.Time.J2000TDB;
        double rSc = 6800000.0;

        var (state, dSunBody, _) = CreateSubSolarState(earth, rSc, epoch);

        var spc = CreateSpacecraft(earth);
        var force = new AlbedoRadiationPressure(spc, earth);

        var acc = force.Apply(state);

        double expected = ExpectedAlbedoAcceleration(
            alpha: 0.306, cr: 1.0, area: 1.0, mass: 100.0,
            rBody: earth.EquatorialRadius, dSunBody: dSunBody, rSc: rSc, phi: 0.0);

        Assert.True(System.Math.Abs(acc.Magnitude() - expected) / expected < 1e-10,
            $"Expected {expected:E12}, got {acc.Magnitude():E12}");
    }

    [Fact]
    public void AntiSolarPointGivesZeroAcceleration()
    {
        // At anti-solar point (phi=pi), Lambertian factor = (pi-pi)*cos(pi) + sin(pi) = 0
        var earth = CreateEarth();
        var epoch = TimeSystem.Time.J2000TDB;

        var sunPos = Stars.SUN_BODY.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.LT)
            .ToStateVector().Position;
        var sunDir = sunPos.Normalize();

        // Place spacecraft on opposite side of Earth from Sun
        double rSc = 6800000.0;
        var scPos = sunDir * -rSc;
        var scVel = sunDir.Cross(Vector3.VectorZ).Normalize() * 7656.0;
        var state = new StateVector(scPos, scVel, earth, epoch, Frames.Frame.ICRF);

        var spc = CreateSpacecraft(earth);
        var force = new AlbedoRadiationPressure(spc, earth);

        var acc = force.Apply(state);

        // Lambertian factor is exactly 0 at phi=pi, so acceleration must be 0
        Assert.Equal(0.0, acc.Magnitude(), 15);
    }

    [Fact]
    public void DirectionIsRadiallyOutwardFromBody()
    {
        // At sub-solar point, acceleration direction must equal the body-to-SC unit vector
        var earth = CreateEarth();
        var epoch = TimeSystem.Time.J2000TDB;
        double rSc = 6800000.0;

        var (state, _, sunDir) = CreateSubSolarState(earth, rSc, epoch);

        var spc = CreateSpacecraft(earth);
        var force = new AlbedoRadiationPressure(spc, earth);

        var acc = force.Apply(state);

        // At sub-solar point, body-to-SC unit vector = sunDir
        var accUnit = acc.Normalize();
        double dot = sunDir * accUnit;
        Assert.True(System.Math.Abs(dot - 1.0) < 1e-12,
            $"Acceleration must point radially outward from body. Dot product = {dot}");
    }

    [Fact]
    public void ScalesLinearlyWithAlbedo()
    {
        // a ∝ α, so a(0.30)/a(0.15) = 2.0 exactly
        var earth1 = CreateEarth(albedo: 0.15);
        var earth2 = CreateEarth(albedo: 0.30);
        var epoch = TimeSystem.Time.J2000TDB;
        double rSc = 6800000.0;

        var (state1, _, _) = CreateSubSolarState(earth1, rSc, epoch);
        var (state2, _, _) = CreateSubSolarState(earth2, rSc, epoch);

        var spc1 = CreateSpacecraft(earth1);
        var spc2 = CreateSpacecraft(earth2);
        var acc1 = new AlbedoRadiationPressure(spc1, earth1).Apply(state1);
        var acc2 = new AlbedoRadiationPressure(spc2, earth2).Apply(state2);

        double expectedRatio = 0.30 / 0.15; // = 2.0
        double actualRatio = acc2.Magnitude() / acc1.Magnitude();
        Assert.True(System.Math.Abs(actualRatio - expectedRatio) < 1e-12,
            $"Expected ratio {expectedRatio}, got {actualRatio}");
    }

    [Fact]
    public void ScalesLinearlyWithCr()
    {
        // a ∝ Cr, so a(1.8)/a(1.0) = 1.8 exactly
        var earth = CreateEarth();
        var epoch = TimeSystem.Time.J2000TDB;
        double rSc = 6800000.0;

        var (state, _, _) = CreateSubSolarState(earth, rSc, epoch);

        var spc1 = CreateSpacecraft(earth, solarRadiationCoeff: 1.0);
        var spc2 = CreateSpacecraft(earth, solarRadiationCoeff: 1.8);
        var acc1 = new AlbedoRadiationPressure(spc1, earth).Apply(state);
        var acc2 = new AlbedoRadiationPressure(spc2, earth).Apply(state);

        double expectedRatio = 1.8;
        double actualRatio = acc2.Magnitude() / acc1.Magnitude();
        Assert.True(System.Math.Abs(actualRatio - expectedRatio) < 1e-12,
            $"Expected ratio {expectedRatio}, got {actualRatio}");
    }

    [Fact]
    public void ScalesInverselyWithMass()
    {
        // a ∝ 1/m, so a(100)/a(200) = 2.0 exactly
        var earth = CreateEarth();
        var epoch = TimeSystem.Time.J2000TDB;
        double rSc = 6800000.0;

        var (state, _, _) = CreateSubSolarState(earth, rSc, epoch);

        var spc1 = CreateSpacecraft(earth, mass: 100.0);
        var spc2 = CreateSpacecraft(earth, mass: 200.0);
        var acc1 = new AlbedoRadiationPressure(spc1, earth).Apply(state);
        var acc2 = new AlbedoRadiationPressure(spc2, earth).Apply(state);

        double expectedRatio = 200.0 / 100.0; // = 2.0
        double actualRatio = acc1.Magnitude() / acc2.Magnitude();
        Assert.True(System.Math.Abs(actualRatio - expectedRatio) < 1e-12,
            $"Expected ratio {expectedRatio}, got {actualRatio}");
    }

    [Fact]
    public void ScalesLinearlyWithSectionalArea()
    {
        // a ∝ A, so a(5)/a(1) = 5.0 exactly
        var earth = CreateEarth();
        var epoch = TimeSystem.Time.J2000TDB;
        double rSc = 6800000.0;

        var (state, _, _) = CreateSubSolarState(earth, rSc, epoch);

        var spc1 = CreateSpacecraft(earth, sectionalArea: 1.0);
        var spc2 = CreateSpacecraft(earth, sectionalArea: 5.0);
        var acc1 = new AlbedoRadiationPressure(spc1, earth).Apply(state);
        var acc2 = new AlbedoRadiationPressure(spc2, earth).Apply(state);

        double expectedRatio = 5.0;
        double actualRatio = acc2.Magnitude() / acc1.Magnitude();
        Assert.True(System.Math.Abs(actualRatio - expectedRatio) < 1e-12,
            $"Expected ratio {expectedRatio}, got {actualRatio}");
    }

    [Fact]
    public void AlbedoVsDirectSrpRatioAtSubSolarPoint()
    {
        // At sub-solar point (phi=0), the analytical ratio albedo/SRP is:
        //   ratio = alpha * (2/3) * (R_body / r_sc)^2 * (d_sun_sc / d_sun_body)^2
        // where d_sun_sc = d_sun_body - r_sc (spacecraft between Sun and Earth)
        var earth = CreateEarth();
        var epoch = TimeSystem.Time.J2000TDB;
        double rSc = 6800000.0;

        var (state, dSunBody, _) = CreateSubSolarState(earth, rSc, epoch);

        var spc = CreateSpacecraft(earth);
        var albedoForce = new AlbedoRadiationPressure(spc, earth);
        var srpForce = new SolarRadiationPressure(spc, [earth]);

        var albedoAcc = albedoForce.Apply(state);
        var srpAcc = srpForce.Apply(state);

        double actualRatio = albedoAcc.Magnitude() / srpAcc.Magnitude();

        // Analytical expected ratio at sub-solar (phi=0, Lambertian factor = pi)
        double alpha = 0.306;
        double rBody = earth.EquatorialRadius;
        double dSunSc = dSunBody - rSc;
        double expectedRatio = alpha * (2.0 / 3.0)
                               * (rBody / rSc) * (rBody / rSc)
                               * (dSunSc / dSunBody) * (dSunSc / dSunBody);

        Assert.True(System.Math.Abs(actualRatio - expectedRatio) < 1e-12,
            $"Expected ratio {expectedRatio:F6}, got {actualRatio:F6}");
    }

    [Fact]
    public void DecaysWithInverseSquareOfDistance()
    {
        // a ∝ 1/r_sc², so at same phase angle: a_leo/a_geo = (r_geo/r_leo)²
        var earth = CreateEarth();
        var epoch = TimeSystem.Time.J2000TDB;

        double rLeo = 6800000.0;
        double rGeo = 42164000.0;

        var (stateLeo, _, _) = CreateSubSolarState(earth, rLeo, epoch);
        var (stateGeo, _, _) = CreateSubSolarState(earth, rGeo, epoch);

        var spcLeo = CreateSpacecraft(earth);
        var spcGeo = CreateSpacecraft(earth);
        var accLeo = new AlbedoRadiationPressure(spcLeo, earth).Apply(stateLeo);
        var accGeo = new AlbedoRadiationPressure(spcGeo, earth).Apply(stateGeo);

        // Both at sub-solar (same direction, same phase angle), only r_sc differs
        double expectedRatio = (rGeo / rLeo) * (rGeo / rLeo);
        double actualRatio = accLeo.Magnitude() / accGeo.Magnitude();

        Assert.True(System.Math.Abs(actualRatio - expectedRatio) / expectedRatio < 1e-10,
            $"Expected ratio {expectedRatio:F6}, got {actualRatio:F6}");
    }

    [Fact]
    public void MoonAlbedoMatchesAnalyticalFormula()
    {
        // Verify full formula for Moon (albedo 0.12) at sub-solar point
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB, albedo: 0.12);
        var epoch = TimeSystem.Time.J2000TDB;
        double rSc = 1837400.0; // ~100 km above Moon surface

        var (state, dSunBody, _) = CreateSubSolarState(moon, rSc, epoch);

        Clock clk = new Clock("My clock", 256);
        var spc = new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk,
            new StateVector(new Vector3(1837400.0, 0.0, 0.0), new Vector3(0.0, 1633.0, 0.0),
                moon, epoch, Frames.Frame.ICRF));

        var force = new AlbedoRadiationPressure(spc, moon);
        var acc = force.Apply(state);

        double expected = ExpectedAlbedoAcceleration(
            alpha: 0.12, cr: 1.0, area: 1.0, mass: 100.0,
            rBody: moon.EquatorialRadius, dSunBody: dSunBody, rSc: rSc, phi: 0.0);

        Assert.True(System.Math.Abs(acc.Magnitude() - expected) / expected < 1e-10,
            $"Moon albedo: expected {expected:E12}, got {acc.Magnitude():E12}");
    }

    #endregion
}
