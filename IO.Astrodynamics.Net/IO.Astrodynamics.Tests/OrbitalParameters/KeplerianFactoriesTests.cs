// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// One meaning per constructor (#668, R23): <see cref="KeplerianElements"/> takes the six elements with the mean anomaly,
/// <see cref="KeplerianElements.FromTrueAnomaly"/> elements given with their true anomaly, and
/// <see cref="KeplerianElements.Parabolic"/> a parabola of the band from its perigee radius; the perigee radius of
/// <see cref="EquinoctialElements"/> goes with a non-finite P only. Before 11.0.0 the constructors took a true anomaly, a
/// period and a perigee radius stored unchecked beside the elements. Expected values are the closed forms of the conic
/// (Vallado, Fundamentals of Astrodynamics and Applications, 4th ed., algorithms 5 and 10, and Barker's equation), and
/// exception types and parameter names.
/// </summary>
public class KeplerianFactoriesTests
{
    private static readonly TimeSystem.Time Epoch = new(2026, 1, 20, 6, 0, 0, TimeScale.TDB);
    private readonly CelestialBody _earth;

    public KeplerianFactoriesTests()
    {
        TestKernels.EnsureSolarSystem();
        _earth = new CelestialBody(PlanetsAndMoons.Earth, Frames.Frame.ICRF, Epoch);
    }

    [Theory]
    [InlineData(7000000.0, 0.1, 0.7)]
    [InlineData(7000000.0, 0.1, -2.5)]
    [InlineData(24396000.0, 0.7283, 3.0)]
    [InlineData(-10000000.0, 1.7, 1.5)]
    [InlineData(-10000000.0, 1.7, -2.0)]
    public void FromTrueAnomaly_KeepsTheTrueAnomalyAndTakesItsMeanAnomaly(double a, double e, double nu)
    {
        var ke = KeplerianElements.FromTrueAnomaly(a, e, 0.5, 0.3, 0.2, nu, _earth, Epoch, Frames.Frame.ICRF);

        // The true anomaly as given, an angle in [0, 2π) as the true anomaly computed from M (#668, R23)
        Assert.Equal(nu < 0.0 ? nu + IO.Astrodynamics.Constants.TwoPi : nu, ke.TrueAnomaly());

        // M of the closed forms: tan(E/2) = √((1 − e)/(1 + e)) tan(ν/2), M = E − e sin E for an ellipse (in [0, 2π));
        // tanh(H/2) = √((e − 1)/(e + 1)) tan(ν/2), M = e sinh H − H for a hyperbola
        double expected;
        if (e < 1.0)
        {
            double eccentricAnomaly = 2.0 * System.Math.Atan(System.Math.Sqrt((1.0 - e) / (1.0 + e)) * System.Math.Tan(nu / 2.0));
            expected = eccentricAnomaly - e * System.Math.Sin(eccentricAnomaly);
            expected = (expected % IO.Astrodynamics.Constants.TwoPi + IO.Astrodynamics.Constants.TwoPi) % IO.Astrodynamics.Constants.TwoPi;
        }
        else
        {
            double hyperbolicAnomaly = 2.0 * System.Math.Atanh(System.Math.Sqrt((e - 1.0) / (e + 1.0)) * System.Math.Tan(nu / 2.0));
            expected = e * System.Math.Sinh(hyperbolicAnomaly) - hyperbolicAnomaly;
        }

        Assert.Equal(expected, ke.MeanAnomaly(), 1e-12);

        // One orbit: the state of ν is the state of the elements given by that mean anomaly (Kepler's equation solved)
        var byMeanAnomaly = new KeplerianElements(a, e, 0.5, 0.3, 0.2, ke.MeanAnomaly(), _earth, Epoch, Frames.Frame.ICRF);
        var state = ke.ToStateVector();
        var other = byMeanAnomaly.ToStateVector();
        Assert.True((state.Position - other.Position).Magnitude() < 1e-6 * state.Position.Magnitude());
        Assert.True((state.Velocity - other.Velocity).Magnitude() < 1e-6 * state.Velocity.Magnitude());

        // Its radius is p / (1 + e cos ν), the conic equation
        double p = a * (1.0 - e * e);
        Assert.Equal(p / (1.0 + e * System.Math.Cos(nu)), state.Position.Magnitude(), 1e-9 * state.Position.Magnitude());
    }

    /// <summary>
    /// ν is an angle (#668, R23): reduced to [0, 2π), the range of the true anomaly computed from M, so two equal elements
    /// give the same true anomaly; on a hyperbola, its reduction to (−π, π] lies inside the asymptotes. Before, ν was
    /// returned as given (7 against 0.7168 from M, −1 against 5.2832) and a hyperbola took ν = 7.
    /// </summary>
    [Theory]
    [InlineData(7000000.0, 0.1, 7.0)]
    [InlineData(7000000.0, 0.1, -1.0)]
    [InlineData(7000000.0, 0.1, 20.0)]
    [InlineData(-7000000.0, 1.5, 7.0)]
    [InlineData(-7000000.0, 1.5, -1.0)]
    [InlineData(-7000000.0, 1.5, -8.0)]
    public void FromTrueAnomaly_ReducesTheAngle(double a, double e, double nu)
    {
        var ke = KeplerianElements.FromTrueAnomaly(a, e, 0.5, 0.3, 0.2, nu, _earth, Epoch, Frames.Frame.ICRF);
        double reduced = ke.TrueAnomaly();
        Assert.InRange(reduced, 0.0, IO.Astrodynamics.Constants.TwoPi);
        Assert.True(reduced < IO.Astrodynamics.Constants.TwoPi);
        // The same direction: cos and sin of the angle given
        Assert.Equal(System.Math.Cos(nu), System.Math.Cos(reduced), 1e-14);
        Assert.Equal(System.Math.Sin(nu), System.Math.Sin(reduced), 1e-14);

        var byMeanAnomaly = new KeplerianElements(a, e, 0.5, 0.3, 0.2, ke.MeanAnomaly(), _earth, Epoch, Frames.Frame.ICRF);
        Assert.Equal(byMeanAnomaly, ke);
        Assert.Equal(byMeanAnomaly.TrueAnomaly(), reduced, 1e-12);
    }

    [Fact]
    public void FromTrueAnomaly_Refusals_NameTheParameter()
    {
        // ν not finite; ν of a hyperbola on or beyond its asymptotes, |ν| ≥ arccos(−1/e); an orbit of the band
        Assert.Equal("trueAnomaly", Assert.Throws<ArgumentException>(() =>
            KeplerianElements.FromTrueAnomaly(7e6, 0.1, 0.5, 0.3, 0.2, double.NaN, _earth, Epoch, Frames.Frame.ICRF)).ParamName);
        double asymptote = System.Math.Acos(-1.0 / 1.7);
        foreach (double nu in new[] { asymptote + 1e-9, -asymptote - 1e-9, 3.0, 3.0 + IO.Astrodynamics.Constants.TwoPi, -3.0 - IO.Astrodynamics.Constants.TwoPi })
        {
            Assert.Equal("trueAnomaly", Assert.Throws<ArgumentOutOfRangeException>(() =>
                KeplerianElements.FromTrueAnomaly(-1e7, 1.7, 0.5, 0.3, 0.2, nu, _earth, Epoch, Frames.Frame.ICRF)).ParamName);
        }

        Assert.Equal("eccentricity", Assert.Throws<ArgumentException>(() =>
            KeplerianElements.FromTrueAnomaly(7e12, 1.0 - 5e-7, 0.5, 0.3, 0.2, 0.1, _earth, Epoch, Frames.Frame.ICRF)).ParamName);
    }

    [Fact]
    public void Parabolic_IsTheParabolaOfItsPerigeeRadius()
    {
        const double q = 6800000.0;
        var parabola = KeplerianElements.Parabolic(q, 0.5, 0.3, 0.2, 0.0, _earth, Epoch, Frames.Frame.ICRF);

        // a = +Infinity at e = 1, q kept, and at M = 0 the periapsis at q with the escape speed √(2μ/q)
        Assert.Equal(1.0, parabola.Eccentricity());
        Assert.True(double.IsPositiveInfinity(parabola.SemiMajorAxis()));
        Assert.Equal(q, parabola.PerigeeRadius());
        var state = parabola.ToStateVector();
        Assert.Equal(q, state.Position.Magnitude(), 1e-9 * q);
        double escape = System.Math.Sqrt(2.0 * _earth.GM / q);
        Assert.Equal(escape, state.Velocity.Magnitude(), 1e-9 * escape);

        // Within the band, a = q / (1 − e) of the same conic
        foreach (double e in new[] { 1.0 - 5e-7, 1.0 + 5e-7 })
        {
            var band = KeplerianElements.Parabolic(q, 0.5, 0.3, 0.2, 0.1, _earth, Epoch, Frames.Frame.ICRF, e);
            Assert.Equal(q / (1.0 - e), band.SemiMajorAxis());
            Assert.Equal(q, band.PerigeeRadius());
            Assert.True(band.IsParabolic());
        }

        // Two parabolas of different perigee radii are two orbits (#475's case)
        Assert.NotEqual(parabola, KeplerianElements.Parabolic(9000000.0, 0.5, 0.3, 0.2, 0.0, _earth, Epoch, Frames.Frame.ICRF));
    }

    [Fact]
    public void Parabolic_Refusals_NameTheParameter()
    {
        foreach (double e in new[] { 0.9, 1.0 - 2e-6, 1.0 + 2e-6, double.NaN })
        {
            Assert.Equal("eccentricity", Assert.Throws<ArgumentOutOfRangeException>(() =>
                KeplerianElements.Parabolic(6.8e6, 0.5, 0.3, 0.2, 0.0, _earth, Epoch, Frames.Frame.ICRF, e)).ParamName);
        }

        foreach (double q in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
        {
            Assert.Equal("perigeeRadius", Assert.Throws<ArgumentOutOfRangeException>(() =>
                KeplerianElements.Parabolic(q, 0.5, 0.3, 0.2, 0.0, _earth, Epoch, Frames.Frame.ICRF)).ParamName);
        }

        Assert.Equal("inclination", Assert.Throws<ArgumentException>(() =>
            KeplerianElements.Parabolic(6.8e6, 4.0, 0.3, 0.2, 0.0, _earth, Epoch, Frames.Frame.ICRF)).ParamName);
    }

    /// <summary>
    /// The constructor builds no orbit of the band: within 1e-6 of e = 1 it points to <see cref="KeplerianElements.Parabolic"/>
    /// (before 11.0.0 it asked for its optional <c>perigeeRadius</c>, naming it).
    /// </summary>
    [Theory]
    [InlineData(double.PositiveInfinity, 1.0)]
    [InlineData(14000000000000.0, 0.9999995)]
    public void Constructor_InTheBand_PointsToParabolic(double a, double e)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new KeplerianElements(a, e, 0.5, 0.3, 0.2, 0.0, _earth, Epoch, Frames.Frame.ICRF));
        Assert.Equal("eccentricity", exception.ParamName);
        Assert.Contains("Parabolic", exception.Message);
    }

    /// <summary>
    /// The public constructors take the six elements named as the methods every set shares, and no cached value: no
    /// <c>trueAnomaly</c>, <c>period</c> or <c>perigeeRadius</c>, and <c>ascendingNode</c> for <c>rigthAscendingNode</c>.
    /// </summary>
    [Fact]
    public void Constructor_TakesTheSixElementsOnly()
    {
        var constructors = typeof(KeplerianElements).GetConstructors();
        var constructor = Assert.Single(constructors);
        Assert.Equal(new[]
            {
                "semiMajorAxis", "eccentricity", "inclination", "ascendingNode", "argumentOfPeriapsis", "meanAnomaly", "observer",
                "epoch", "frame", "elementsType"
            },
            constructor.GetParameters().Select(p => p.Name));
    }

    /// <summary>
    /// The perigee radius of <see cref="EquinoctialElements"/> goes with a non-finite P only (the parabola it serves): a
    /// finite P fixes q = P / (1 + e). Before 11.0.0, P = 7000 km given 1 m returned a perigee radius of 1 m (the design's
    /// measurement).
    /// </summary>
    [Fact]
    public void Equinoctial_APerigeeRadiusWithAFiniteP_IsRefused()
    {
        foreach (double f in new[] { 0.1, 1.0 })
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                new EquinoctialElements(7000000.0, f, 0.0, 0.1, 0.2, 0.5, _earth, Epoch, Frames.Frame.ICRF, perigeeRadius: 1.0));
            Assert.Equal("perigeeRadius", exception.ParamName);
        }

        // P = +Infinity: the perigee radius of the parabola, kept; P finite at e = 1: q = P / 2
        var parabola = new EquinoctialElements(double.PositiveInfinity, 1.0, 0.0, 0.1, 0.2, 0.5, _earth, Epoch, Frames.Frame.ICRF,
            perigeeRadius: 6800000.0);
        Assert.Equal(6800000.0, parabola.PerigeeRadius());
        Assert.Equal(3500000.0, new EquinoctialElements(7000000.0, 1.0, 0.0, 0.1, 0.2, 0.5, _earth, Epoch, Frames.Frame.ICRF)
            .PerigeeRadius());

        // The library's own conversion still carries the perigee radius of a parabola with its finite P (#462)
        var equinoctial = KeplerianElements.Parabolic(6800000.0, 0.2, 0.3, 0.4, 0.0, _earth, Epoch, Frames.Frame.ICRF).ToEquinoctial();
        Assert.Equal(13600000.0, equinoctial.P);
        Assert.Equal(6800000.0, equinoctial.PerigeeRadius());
    }
}
