using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces;

public class ThirdBodyPerturbationTests
{
    public ThirdBodyPerturbationTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Theory]
    [InlineData(1e-12)]
    [InlineData(1e-8)]
    [InlineData(1e-4)]
    [InlineData(0.01)]
    [InlineData(0.1)]
    [InlineData(0.5)]
    public void BattinFMatchesDirectFormula(double q)
    {
        // Battin's f(q) should match the direct formula: (1+q)^(3/2) - 1 expanded for numerical stability
        // Direct check: f(q) = q * (3 + 3q + q^2) / (1 + (1+q)^(3/2))
        double fq = ThirdBodyPerturbation.BattinF(q);

        // Verify against the algebraic identity:
        // The standard third-body formula gives: a = -mu/|d|^3 * ((r - d)/|r-d|^3 * |d|^3 + d)
        // Battin's f(q) satisfies: f(q) * d = ((1+q)^(-3/2) - 1) * d, which cancels the near-equal terms
        // We verify: (1+q)^(-3/2) = 1 - f(q) * (|d|^2 / (r . (r - 2d))) ... indirectly
        // Direct numerical check of the formula itself:
        double numerator = q * (3.0 + 3.0 * q + q * q);
        double denominator = 1.0 + System.Math.Pow(1.0 + q, 1.5);
        double expected = numerator / denominator;

        Assert.Equal(expected, fq, 15);
    }

    [Fact]
    public void BattinFAtZeroIsZero()
    {
        Assert.Equal(0.0, ThirdBodyPerturbation.BattinF(0.0), 15);
    }

    [Fact]
    public void SunThirdBodyAccelerationOnLeoSatellite()
    {
        // Sun's tidal acceleration on a LEO satellite at ~400 km altitude
        // Analytical estimate: ~5.6e-7 m/s^2 (Montenbruck & Gill, Table 3.1)
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var sun = Stars.SUN_BODY;

        var thirdBody = new ThirdBodyPerturbation(sun, earth);

        // LEO satellite at 400 km altitude, on the Earth-Sun line (worst case for tidal acceleration)
        var epoch = TimeSystem.Time.J2000TDB;
        // Get Sun direction from Earth to place satellite on Earth-Sun line
        var sunFromEarth = sun.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position;
        var sunDir = sunFromEarth * (1.0 / sunFromEarth.Magnitude());

        // Satellite at 6778 km from Earth center, along the Earth-Sun line
        double r = 6778000.0; // meters
        var satPosition = sunDir * r;
        var satVelocity = new Vector3(0, 7500, 0); // velocity doesn't matter for gravitational acceleration

        var sv = new StateVector(satPosition, satVelocity, earth, epoch, Frames.Frame.ICRF);
        var acceleration = thirdBody.Apply(sv);
        var accMag = acceleration.Magnitude();

        // Sun's tidal acceleration on LEO should be ~5.6e-7 m/s^2 (order of magnitude check)
        Assert.True(accMag > 1e-7, $"Sun tidal acceleration {accMag:E3} m/s^2 is too small");
        Assert.True(accMag < 1e-5, $"Sun tidal acceleration {accMag:E3} m/s^2 is too large");
    }

    [Fact]
    public void MoonThirdBodyAccelerationOnLeoSatellite()
    {
        // Moon's tidal acceleration on LEO is ~1.1e-6 m/s^2 (larger than Sun's despite lower mass)
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var moon = PlanetsAndMoons.MOON_BODY;

        var thirdBody = new ThirdBodyPerturbation(moon, earth);

        var epoch = TimeSystem.Time.J2000TDB;
        var moonFromEarth = moon.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position;
        var moonDir = moonFromEarth * (1.0 / moonFromEarth.Magnitude());

        double r = 6778000.0;
        var satPosition = moonDir * r;
        var satVelocity = new Vector3(0, 7500, 0);

        var sv = new StateVector(satPosition, satVelocity, earth, epoch, Frames.Frame.ICRF);
        var acceleration = thirdBody.Apply(sv);
        var accMag = acceleration.Magnitude();

        // Moon's tidal acceleration on LEO should be ~1.1e-6 m/s^2
        Assert.True(accMag > 1e-7, $"Moon tidal acceleration {accMag:E3} m/s^2 is too small");
        Assert.True(accMag < 1e-5, $"Moon tidal acceleration {accMag:E3} m/s^2 is too large");
    }

    [Fact]
    public void ThirdBodyAccelerationMatchesDirectFormula()
    {
        // Verify Battin's formula matches the direct (but less numerically stable) formula
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var sun = Stars.SUN_BODY;

        var thirdBody = new ThirdBodyPerturbation(sun, earth);

        var epoch = TimeSystem.Time.J2000TDB;

        // Use a position not on the Earth-Sun line for a more general test
        double r = 6778000.0;
        var satPosition = new Vector3(r, 0, 0);
        var satVelocity = new Vector3(0, 7500, 0);

        var sv = new StateVector(satPosition, satVelocity, earth, epoch, Frames.Frame.ICRF);

        // Battin's result
        var battinResult = thirdBody.Apply(sv);

        // Direct formula: a = -mu * ((r - d)/|r - d|^3 + d/|d|^3)
        var dj = sun.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position;
        var rMinusDj = satPosition - dj;
        double rMinusDjMag = rMinusDj.Magnitude();
        double djMag = dj.Magnitude();
        var directResult = (rMinusDj * (1.0 / (rMinusDjMag * rMinusDjMag * rMinusDjMag)) + dj * (1.0 / (djMag * djMag * djMag))) * (-sun.GM);

        // For LEO vs Sun, q is tiny (~10^-8), so both formulas should agree closely.
        // The direct formula loses ~5 significant digits from catastrophic cancellation,
        // but the result should still match to ~1e-5 relative error.
        var diff = (battinResult - directResult).Magnitude();
        var relError = diff / battinResult.Magnitude();

        Assert.True(relError < 1e-4,
            $"Battin vs direct relative error: {relError:E3}. Battin: {battinResult.Magnitude():E6}, Direct: {directResult.Magnitude():E6}");
    }
}
