using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.TimeToRadius"/> and
/// <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.TimeToSphereOfInfluence"/> (#627): the time from the
/// periapsis against the closed form of the hyperbola at 50 digits (<c>tools/time_to_radius/time_to_radius_reference.py</c>,
/// mpmath 1.4.1 and spiceypy 8.3.0 on the test kernels), and the radii an orbit never reaches.
/// </summary>
public class TimeToRadiusTests
{
    private static readonly TimeSystem.Time Epoch = new(new DateTime(2026, 1, 20, 6, 0, 0), TimeFrame.TDBFrame);
    private readonly ITestOutputHelper _output;

    public TimeToRadiusTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    // A body built from custom parameters: no heliocentric orbit, so an infinite sphere of influence
    private sealed class CustomBody : CelestialBody
    {
        internal CustomBody() : base(1000627, "CUSTOM627", 4.28e13 / IO.Astrodynamics.Constants.G, 3.0e6, 3.0e6)
        {
        }
    }

    private static KeplerianElements Orbit(double a, double e, ILocalizable observer, double meanAnomaly = 0.0) =>
        new(a, e, 0.5, 1.0, 2.0, meanAnomaly, observer, Epoch, Frames.Frame.ICRF);

    private double SecondsTo(KeplerianElements orbit, double radius)
    {
        var epoch = orbit.TimeToRadius(radius);
        Assert.Equal(TimeFrame.TDBFrame, epoch.Frame);
        return (epoch - Epoch).TotalSeconds;
    }

    [Fact]
    public void HyperbolaAboutTheSun_IsTheClosedForm()
    {
        var sun = new CelestialBody(Stars.Sun);
        Assert.Equal(1.3271244004127939e20, sun.GM);
        var orbit = Orbit(-1.5e11, 2.0, sun);

        double at1e12 = SecondsTo(orbit, 1e12) - 27139842.071728861318;
        double at1e15 = SecondsTo(orbit, 1e15) - 33580027163.50922645;
        _output.WriteLine($"1e12 m: {at1e12:E3} s, 1e15 m: {at1e15:E3} s off the closed form");
        // Measured -6.3e-8 s at 1e12 m, the same on the three platforms (CI run 42940000708), within the tick of the epoch; twice,
        // at least two ticks.
        // At 1e15 m the true anomaly is 2.6e-4 rad from the asymptote, where one ulp of nu moves the time by 5.0e-2 to
        // 6.2e-2 s: the result is the rounding of the platform's libm (Math.Acos, Tan, Atanh, Sinh; glibc, UCRT or Apple's,
        // each within one ulp, not correctly rounded). Linux and Windows measure 1.04e-2 s, macOS arm64 -2.05e-3 s
        // (CI run 42940000708); Linux's is mostly the rounding of acos (+1.89e-2 s;
        // tan -0.38e-2 s, c +0.37e-2 s); a libm within one ulp per call gives up to 3.944e-2 s
        // (tools/time_to_radius/time_to_radius_libm_envelope.py), which a bound twice the Linux figure (2.1e-2 s) did not
        // hold (the second review of #627). The bound is twice that worst value, which the three platforms are within.
        Assert.InRange(at1e12, -2e-7, 2e-7);
        Assert.InRange(at1e15, -7.9e-2, 7.9e-2);
    }

    [Fact]
    public void HyperbolaAboutTheMoon_ReachesItsSphereOfInfluence()
    {
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Epoch);
        Assert.Equal(4902800118457.549, moon.GM);
        double soi = moon.SphereOfInfluence - 67393946.13153864;
        var orbit = Orbit(-5e6, 2.0, moon);
        double toSoi = (orbit.TimeToSphereOfInfluence() - Epoch).TotalSeconds - 58936.224724040151512;
        _output.WriteLine($"SOI {soi:E3} m, time to it {toSoi:E3} s off the reference");
        // Measured, the same on the three platforms (CI run 42940000708): the sphere of influence one ulp (1.5e-8 m) from spiceypy's, the time 4.0e-8 s
        // from the closed form, within the tick of the epoch; twice each, the time at least two ticks
        Assert.InRange(soi, -3e-8, 3e-8);
        Assert.InRange(toSoi, -2e-7, 2e-7);
        Assert.Equal(orbit.TimeToRadius(moon.SphereOfInfluence), orbit.TimeToSphereOfInfluence());
    }

    [Fact]
    public void AnInfiniteSphereOfInfluence_Throws()
    {
        var sun = new CelestialBody(Stars.Sun);
        var custom = new CustomBody();
        var mars = new CelestialBody(PlanetsAndMoons.MARS, Frames.Frame.ICRF, Epoch);

        // de440s does not chain Mars to its barycenter: no initial orbital parameters (#454)
        Assert.Null(mars.InitialOrbitalParameters);
        foreach (var body in new CelestialBody[] { sun, custom, mars })
        {
            Assert.Equal(double.PositiveInfinity, body.SphereOfInfluence);
            foreach (var e in new[] { 0.5, 2.0, 5.0 })
            {
                var orbit = Orbit(e < 1.0 ? 1.5e11 : -5e6, e, body);
                var ex = Assert.Throws<InvalidOperationException>(() => orbit.TimeToSphereOfInfluence());
                Assert.Equal($"{body.Name} has no finite sphere of influence; an orbit about it never leaves it.", ex.Message);
            }
        }
    }

    [Fact]
    public void AnObserverThatIsNotACelestialBody_Throws()
    {
        var orbit = Orbit(-5e6, 2.0, new Barycenter(0));
        Assert.Throws<InvalidOperationException>(() => orbit.TimeToSphereOfInfluence());
    }

    [Fact]
    public void AnEllipseInsideAFiniteSphereOfInfluence_Throws()
    {
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Epoch);
        var orbit = Orbit(5e6, 0.5, moon);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => orbit.TimeToSphereOfInfluence());
        Assert.Equal("radius", ex.ParamName);
        Assert.Equal(moon.SphereOfInfluence, ex.ActualValue);
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]
    [InlineData(1e12)] // below the periapsis, 1.35e12 m
    [InlineData(0.0)]
    [InlineData(-1e13)]
    public void ARadiusAHyperbolaNeverReaches_Throws(double radius)
    {
        var orbit = Orbit(-1.5e11, 10.0, new CelestialBody(Stars.Sun));
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => orbit.TimeToRadius(radius));
        Assert.Equal("radius", ex.ParamName);
        Assert.Equal(radius, ex.ActualValue);
        Assert.Contains($"from its periapsis, {orbit.SemiLatusRectum() / 11.0:R} m, to infinity", ex.Message);
    }

    [Theory]
    [InlineData(3e11)] // above the apoapsis, 2.25e11 m
    [InlineData(7e10)] // below the periapsis, 7.5e10 m
    [InlineData(double.PositiveInfinity)]
    public void ARadiusAnEllipseNeverReaches_Throws(double radius)
    {
        var orbit = Orbit(1.5e11, 0.5, new CelestialBody(Stars.Sun));
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => orbit.TimeToRadius(radius));
        Assert.Equal("radius", ex.ParamName);
        double p = orbit.SemiLatusRectum();
        Assert.Contains($"from its periapsis, {p / 1.5:R} m, to its apoapsis, {p / 0.5:R} m.", ex.Message);
    }

    [Fact]
    public void TheApsidesOfAnEllipse_AreReached()
    {
        var sun = new CelestialBody(Stars.Sun);
        var orbit = Orbit(1.5e11, 0.5, sun);
        double p = orbit.SemiLatusRectum();
        double halfPeriod = orbit.Period().TotalSeconds / 2.0;

        // The apoapsis of the orbit is p / (1 - e) = 225000000000.00015 m; its other expressions give 2.25e11 m, 6.8e-16
        // below it, which the orbit reaches before the apoapsis by sqrt(2 (r_a - r) / |r''_a|), |r''_a| = mu (r_a - p) /
        // r_a^3 (0.478 s, the closed form near an extreme of r(t)); the acos of the true anomaly resolves it to some 10 %
        double ra = p / 0.5;
        double rDotDot = sun.GM * (ra - p) / (ra * ra * ra);
        foreach (var apoapsis in new[] { ra, orbit.ApogeeVector().Magnitude(), 1.5e11 * 1.5 })
        {
            double expected = System.Math.Sqrt(2.0 * (ra - apoapsis) / rDotDot);
            double early = halfPeriod - SecondsTo(orbit, apoapsis);
            _output.WriteLine($"apoapsis {apoapsis:R}: {early:E3} s before it, the closed form {expected:E3} s");
            Assert.InRange(early, -1e-6, 2.0 * expected + 1e-6);
        }

        // At the periapsis the mean anomaly is 0 whatever the rounding of the radius (6.1e-16 below p / (1 + e))
        foreach (var periapsis in new[] { p / 1.5, orbit.PerigeeVector().Magnitude(), orbit.PerigeeRadius() })
        {
            Assert.Equal(0.0, SecondsTo(orbit, periapsis));
        }
    }

    [Theory]
    [InlineData(1e18)] // 3.4e13 s, beyond Time
    [InlineData(1e300)] // the mean anomaly overflows
    public void ARadiusReachedAfterTheLastEpochOfTime_Throws(double radius)
    {
        var orbit = Orbit(-1.5e11, 2.0, new CelestialBody(Stars.Sun));
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => orbit.TimeToRadius(radius));
        Assert.Equal("radius", ex.ParamName);
        Assert.Contains($"a radius of {radius:R} m outside the epochs a Time holds, after the last.", ex.Message);
    }

    [Fact]
    public void ARadiusCrossedBeforeTheFirstEpochOfTime_Throws()
    {
        // At M0 = 1e7 rad the outbound crossing of 1e12 m (M = 5.37 rad) is (5.37 - 1e7) / n, about -5e13 s: before year 1
        var orbit = Orbit(-1.5e11, 2.0, new CelestialBody(Stars.Sun), 1e7);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => orbit.TimeToRadius(1e12));
        Assert.Equal("radius", ex.ParamName);
        Assert.Contains("a radius of 1000000000000 m outside the epochs a Time holds, before the first.", ex.Message);
    }

    [Fact]
    public void AnOpenOrbitPastTheRadius_GivesItsOutboundCrossing_BeforeItsEpoch()
    {
        // The one outbound crossing of an open orbit, already past at M0 > M(r): (M(r) - M0) / n, the closed form
        var sun = new CelestialBody(Stars.Sun);
        double past = SecondsTo(Orbit(-1.5e11, 2.0, sun, 10.0), 1e12) - -23289240.982338788371;
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Epoch);
        double pastSoi = (Orbit(-5e6, 2.0, moon, 1e3).TimeToSphereOfInfluence() - Epoch).TotalSeconds -
                         -4990383.9801409249097;
        _output.WriteLine($"past 1e12 m: {past:E3} s, past the SOI: {pastSoi:E3} s off the closed form");
        // Measured, the same on the three platforms (CI run 42940000708): -1.12e-7 s past 1e12 m (the -6.3e-8 s of the time from the periapsis, above,
        // and the rounding of M(r) - M0 and of the tick), 2.4e-8 s past the SOI; twice each, at least two ticks
        Assert.InRange(past, -2.3e-7, 2.3e-7);
        Assert.InRange(pastSoi, -2e-7, 2e-7);
    }

    [Theory]
    [InlineData(7.0e6)]
    [InlineData(7.2e6)]
    [InlineData(4.2164e7)]
    public void ACircularOrbit_IsAtItsRadiusAtItsEpoch(double a)
    {
        // e = 0: the orbit is at its radius at every instant; (p / r - 1) / e is 0 / 0 at r = p and ±1 / 0 at r = a, an
        // ulp or two from p
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);
        var orbit = Orbit(a, 0.0, earth);
        double p = orbit.SemiLatusRectum();
        foreach (var radius in new[] { p, a, System.Math.BitIncrement(p), System.Math.BitDecrement(p) })
        {
            Assert.Equal(Epoch, orbit.TimeToRadius(radius));
        }

        // Any other radius is never reached
        foreach (var radius in new[] { p * (1.0 + 1e-9), p * (1.0 - 1e-9) })
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => orbit.TimeToRadius(radius));
            Assert.Contains($"from its periapsis, {p:R} m, to its apoapsis, {p:R} m.", ex.Message);
        }
    }
}
