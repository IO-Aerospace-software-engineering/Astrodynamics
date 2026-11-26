// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// The elements the constructors of <see cref="KeplerianElements"/> and <see cref="EquinoctialElements"/> accept
/// (#476, #481, #726): no conic has a non-finite element, a negative or non-finite eccentricity, a semi-latus rectum
/// P ≤ 0 or a perigee radius q ≤ 0 (p = h²/μ &gt; 0 and q &gt; 0 for every conic; SPICE CONICS rejects rp ≤ 0 with
/// SPICE(BADPERIAPSEVALUE), EQNCPV a ≤ 0 with SPICE(BADSEMIAXIS)). The expected outcomes are exception types and
/// parameter names, from IEEE 754 (every comparison with NaN is false) and the definition of a conic; no number is
/// compared, so no threshold is set.
/// </summary>
public class ElementsValidationTests
{
    private static readonly TimeSystem.Time Epoch = new(new DateTime(2026, 1, 20, 6, 0, 0), TimeScale.TT);
    private readonly CelestialBody _earth;

    public ElementsValidationTests()
    {
        TestKernels.EnsureSolarSystem();
        _earth = new CelestialBody(PlanetsAndMoons.Earth, Frames.Frame.ICRF, Epoch);
    }

    private KeplerianElements Keplerian(double a = 7000000.0, double e = 0.1, double i = 0.5, double raan = 1.0,
        double aop = 2.0, double m = 3.0) =>
        new(a, e, i, raan, aop, m, _earth, Epoch, Frames.Frame.ICRF);

    // A parabola of the band from its perigee radius, a = q / (1 − e), +Infinity at e = 1 (#668, R23)
    private KeplerianElements Parabola(double q, double e = 1.0, double i = 0.5, double raan = 1.0, double aop = 2.0,
        double m = 3.0) =>
        KeplerianElements.Parabolic(q, i, raan, aop, m, _earth, Epoch, Frames.Frame.ICRF, e);

    private EquinoctialElements Equinoctial(double p, double f, double g, double h, double k, double l0,
        double? perigeeRadius = null) =>
        new(p, f, g, h, k, l0, _earth, Epoch, Frames.Frame.ICRF, perigeeRadius);

    private static readonly double[] NonFinite = [double.NaN, double.PositiveInfinity, double.NegativeInfinity];

    #region #476: the eccentricity of KeplerianElements

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.1)]
    [InlineData(-1e-300)]
    public void Issue476_AnEccentricityNotFiniteOrNegative_IsRefused(double e)
    {
        // Before: NaN and +∞ accepted (the message of an empty Nullable later, or a NaN state), the negative ones refused by
        // an ArgumentException without a parameter name
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Keplerian(e: e));
        Assert.Equal("eccentricity", exception.ParamName);
        Assert.Contains("finite and non-negative", exception.Message);
        var fromOmm = Assert.Throws<ArgumentOutOfRangeException>(() =>
            KeplerianElements.FromOMM(15.5, e, 51.64, 120.0, 80.0, 30.0, _earth, Epoch, Frames.Frame.TEME));
        Assert.Equal("eccentricity", fromOmm.ParamName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.0)]
    public void Issue476_AZeroEccentricity_IsAccepted(double e)
    {
        var ke = Keplerian(e: e);
        Assert.True(ke.IsElliptical());
        Assert.Equal(3.0, ke.TrueAnomaly(), 15);
    }

    // Orbital parameters whose eccentricity is NaN: a subclass reaches the conic branches of the base class, which no
    // constructor of the library now does
    private sealed class NaNEccentricityOrbit : IO.Astrodynamics.OrbitalParameters.OrbitalParameters
    {
        internal NaNEccentricityOrbit(ILocalizable observer) : base(observer, ElementsValidationTests.Epoch, Frames.Frame.ICRF)
        {
        }

        public override double SemiMajorAxis() => 7000000.0;
        public override double Eccentricity() => double.NaN;
        public override double Inclination() => 0.5;
        public override double AscendingNode() => 1.0;
        public override double ArgumentOfPeriapsis() => 2.0;
        public override double MeanAnomaly() => 3.0;
    }

    [Fact]
    public void Issue476_ATrueAnomalyWithoutAConicBranch_NamesTheEccentricity()
    {
        // Before: "Nullable object must have a value."
        var exception = Assert.Throws<InvalidOperationException>(() => new NaNEccentricityOrbit(_earth).TrueAnomaly());
        Assert.Contains("e = NaN", exception.Message);
    }

    public static TheoryData<double[], double[]> NonFiniteStates => new()
    {
        { [double.NaN, double.NaN, double.NaN], [0.0, 7500.0, 0.0] },
        { [double.PositiveInfinity, 0.0, 0.0], [0.0, 7500.0, 0.0] },
        { [7000000.0, 0.0, 0.0], [0.0, double.PositiveInfinity, 0.0] },
        { [7000000.0, 0.0, 0.0], [double.NaN, double.NaN, double.NaN] },
        { [1e308, 0.0, 0.0], [0.0, 1e308, 1e308] },
    };

    [Theory]
    [MemberData(nameof(NonFiniteStates))]
    public void Issue476_ANonFiniteState_ThrowsWhereItNeedsAConic(double[] r, double[] v)
    {
        // Arrange: a state no constructor checks (choice 3 (a) of the issue: the object stays representable)
        var sv = new StateVector(new Vector3(r[0], r[1], r[2]), new Vector3(v[0], v[1], v[2]), _earth, Epoch,
            Frames.Frame.ICRF);

        // Act, assert: InvalidOperationException from every call that needs a conic branch (#687 for most; the last state,
        // whose h and r pass the rectilinear check, the message of an empty Nullable before #476), NaN from TrueAnomaly()
        Assert.Throws<InvalidOperationException>(() => sv.EccentricAnomaly());
        Assert.Throws<InvalidOperationException>(() => sv.MeanAnomaly());
        Assert.Throws<InvalidOperationException>(() => sv.ToKeplerianElements());
        Assert.Throws<InvalidOperationException>(() => sv.ToStateVector(Epoch.AddSeconds(60.0)));
        Assert.Throws<InvalidOperationException>(() => sv.ToStateVector(1.0));
        Assert.True(double.IsNaN(sv.TrueAnomaly()));
    }

    [Fact]
    public void Issue476_AStateOfNaNEccentricity_NamesItsEccentricity()
    {
        // Arrange: h and r pass the rectilinear check of #687, e = NaN (|v|² overflows)
        var sv = new StateVector(new Vector3(1e308, 0.0, 0.0), new Vector3(0.0, 1e308, 1e308), _earth, Epoch,
            Frames.Frame.ICRF);

        // Act, assert: the message names e (before #476: "Nullable object must have a value.", the same exception type)
        foreach (Action call in new Action[]
                 {
                     () => sv.MeanAnomaly(), () => sv.EccentricAnomaly(), () => sv.ToKeplerianElements(),
                     () => sv.ToStateVector(Epoch.AddSeconds(60.0)), () => sv.ToStateVector(1.0),
                 })
        {
            var exception = Assert.Throws<InvalidOperationException>(call);
            Assert.Contains("e = NaN", exception.Message);
        }
    }

    #endregion

    #region #726: every element finite

    [Fact]
    public void Issue726_KeplerianElementsNotFinite_AreRefused()
    {
        // Before: stored, reaching the periods, anomalies and conversions done in .NET
        foreach (double x in NonFinite)
        {
            AssertRefused(() => Keplerian(i: x), "inclination");
            AssertRefused(() => Keplerian(raan: x), "ascendingNode");
            AssertRefused(() => Keplerian(aop: x), "argumentOfPeriapsis");
            AssertRefused(() => Keplerian(m: x), "meanAnomaly");
            AssertRefused(() => KeplerianElements.FromTrueAnomaly(7000000.0, 0.1, 0.5, 1.0, 2.0, x, _earth, Epoch,
                Frames.Frame.ICRF), "trueAnomaly");
        }

        // A semi-major axis not finite: NaN, −∞, and +∞ outside the parabolic band
        AssertRefused(() => Keplerian(a: double.NaN), "semiMajorAxis");
        AssertRefused(() => Keplerian(a: double.NegativeInfinity), "semiMajorAxis");
        AssertRefused(() => Keplerian(a: double.PositiveInfinity), "semiMajorAxis");
        AssertRefused(() => Keplerian(a: double.PositiveInfinity, e: 2.0), "semiMajorAxis");
    }

    [Fact]
    public void Issue726_TheParabola_KeepsItsInfiniteSemiMajorAxis()
    {
        // The one legitimate infinity: a = +∞ with e = 1 and a finite perigee radius (#373)
        var parabola = Parabola(6800000.0);
        Assert.True(double.IsPositiveInfinity(parabola.SemiMajorAxis()));
        Assert.Equal(6800000.0, parabola.PerigeeRadius());
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0.0)]
    [InlineData(-7000000.0)]
    public void Issue726_APerigeeRadiusNotFiniteAndPositive_IsRefused(double q)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Parabola(q));
        Assert.Equal("perigeeRadius", exception.ParamName);
    }

    [Fact]
    public void Issue726_MeanElementsFromOmmNotFinite_AreRefused()
    {
        foreach (double x in NonFinite)
        {
            AssertRefused(() => KeplerianElements.FromOMM(15.5, 0.0007, x, 120.0, 80.0, 30.0, _earth, Epoch, Frames.Frame.TEME), "inclination");
            AssertRefused(() => KeplerianElements.FromOMM(15.5, 0.0007, 51.64, x, 80.0, 30.0, _earth, Epoch, Frames.Frame.TEME), "raan");
            AssertRefused(() => KeplerianElements.FromOMM(15.5, 0.0007, 51.64, 120.0, x, 30.0, _earth, Epoch, Frames.Frame.TEME), "argumentOfPeriapsis");
            AssertRefused(() => KeplerianElements.FromOMM(15.5, 0.0007, 51.64, 120.0, 80.0, x, _earth, Epoch, Frames.Frame.TEME), "meanAnomaly");
        }

        // A mean motion finite and above 0: n = 0 gave a = +∞, a negative n the a of |n|
        foreach (double n in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0.0, -15.5 })
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                KeplerianElements.FromOMM(n, 0.0007, 51.64, 120.0, 80.0, 30.0, _earth, Epoch, Frames.Frame.TEME));
            Assert.Equal("meanMotion", exception.ParamName);
        }
    }

    [Fact]
    public void Issue726_EquinoctialElementsNotFinite_AreRefused()
    {
        // Before: accepted, a NaN F giving e = NaN and a state of NaN
        foreach (double x in NonFinite)
        {
            AssertRefused(() => Equinoctial(7000000.0, x, 0.05, 0.2, 0.1, 1.0), "f");
            AssertRefused(() => Equinoctial(7000000.0, 0.1, x, 0.2, 0.1, 1.0), "g");
            AssertRefused(() => Equinoctial(7000000.0, 0.1, 0.05, x, 0.1, 1.0), "h");
            AssertRefused(() => Equinoctial(7000000.0, 0.1, 0.05, 0.2, x, 1.0), "k");
            AssertRefused(() => Equinoctial(7000000.0, 0.1, 0.05, 0.2, 0.1, x), "l0");
        }

        // F and G finite whose eccentricity √(F² + G²) overflows
        AssertRefused(() => Equinoctial(7000000.0, 1e200, 0.0, 0.2, 0.1, 1.0), "f");
    }

    private static void AssertRefused(Func<object> build, string paramName)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(build);
        Assert.Equal(paramName, exception.ParamName);
    }

    #endregion

    #region #481: the semi-latus rectum and the perigee radius of EquinoctialElements

    public static TheoryData<double, double, double, double, double> Shapes => new()
    {
        // F, G, H, K, L0: an ellipse, a hyperbola, a circular equatorial orbit
        { 0.1, 0.05, 0.2, 0.1, 1.0 }, { 1.5, 0.0, 0.2, 0.1, 0.5 }, { 0.0, 0.0, 0.0, 0.0, 0.0 }
    };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Issue481_ASemiLatusRectumNotPositive_IsRefused(double f, double g, double h, double k, double l0)
    {
        // Before: a finite position mirrored through the centre with a NaN velocity, a negative a, NaN states
        foreach (double p in new[] { -7000000.0, -1.0, -5e-324, -0.0, 0.0 })
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Equinoctial(p, f, g, h, k, l0));
            Assert.Equal("p", exception.ParamName);
        }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Issue481_ASemiLatusRectumNotFiniteOutsideTheParabolicBand_IsRefused(double f, double g, double h, double k,
        double l0)
    {
        foreach (double p in NonFinite)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Equinoctial(p, f, g, h, k, l0));
            Assert.Equal("p", exception.ParamName);
        }
    }

    [Theory]
    [InlineData(-7000000.0)]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Issue481_APerigeeRadiusNotFiniteAndPositive_IsRefused(double q)
    {
        // The parabola of the issue, P = +∞: its state is the conic of p = q (1 + e), which SPICE CONICS refuses for rp ≤ 0
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Equinoctial(double.PositiveInfinity, 1.0, 0.0, 0.2, 0.1, 0.5, q));
        Assert.Equal("perigeeRadius", exception.ParamName);
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]
    public void Issue481_AParabolaWithANonFiniteSemiLatusRectumAndAPerigeeRadius_IsAccepted(double p)
    {
        // A non-finite P with a perigee radius, the way #452 documents to give a parabola: its state is that of
        // p = q (1 + e) = 14 000 km at e = 1, the same arithmetic as the finite P
        var parabola = Equinoctial(p, 1.0, 0.0, 0.2, 0.1, 0.5, 7000000.0);
        var finite = Equinoctial(14000000.0, 1.0, 0.0, 0.2, 0.1, 0.5);
        Assert.Equal(finite.ToStateVector().Position, parabola.ToStateVector().Position);
        Assert.Equal(finite.ToStateVector().Velocity, parabola.ToStateVector().Velocity);
    }

    [Fact]
    public void Issue481_TheElementsTheLibraryBuilds_AreAccepted()
    {
        // ToEquinoctial of a parabola (P finite since #462) and of a state of the band
        var parabola = Parabola(6800000.0).ToEquinoctial();
        Assert.Equal(13600000.0, parabola.P);
        var state = new StateVector(new Vector3(7000000.0, 0.0, 0.0), new Vector3(0.0, 10671.729487052813, 0.0), _earth,
            Epoch, Frames.Frame.ICRF);
        Assert.True(state.ToEquinoctial().P > 0.0);

        // An ordinary hyperbola, P = 1 m (EquinoctialElementsTests)
        Assert.True(Equinoctial(1.0, 3.0, 2.0, 0.1, 0.2, 0.3).IsHyperbolic());
    }

    #endregion

    #region #663: no period for e ≥ 1

    [Fact]
    public void Issue663_AnOpenOrbit_HasNoPeriod()
    {
        // Arrange: r = (7000 km, 0, 0) about the Earth, v at escape speed (e one ulp above 1 in doubles) and 1.2 times it,
        // and the Keplerian and equinoctial elements of a hyperbola and of a parabola
        double r = 7000000.0;
        double escape = System.Math.Sqrt(2.0 * _earth.GM / r);
        var orbits = new (string Name, IO.Astrodynamics.OrbitalParameters.OrbitalParameters Orbit)[]
        {
            ("state at escape speed", new StateVector(new Vector3(r, 0.0, 0.0), new Vector3(0.0, escape, 0.0), _earth, Epoch, Frames.Frame.ICRF)),
            ("hyperbolic state", new StateVector(new Vector3(r, 0.0, 0.0), new Vector3(0.0, 1.2 * escape, 0.0), _earth, Epoch, Frames.Frame.ICRF)),
            ("Keplerian hyperbola", Keplerian(a: -1e7, e: 1.7)),
            ("Keplerian parabola", Parabola(6800000.0)),
            ("equinoctial hyperbola", Equinoctial(7000000.0, 1.5, 0.0, 0.2, 0.1, 0.5)),
            ("equinoctial parabola", Equinoctial(14000000.0, 1.0, 0.0, 0.2, 0.1, 0.5)),
        };

        foreach (var (name, orbit) in orbits)
        {
            // Act, assert: InvalidOperationException naming e and a (before: TimeSpan's NaN ArgumentException for a
            // hyperbola, its OverflowException for a parabola)
            var exception = Assert.Throws<InvalidOperationException>(() => orbit.Period());
            Assert.Contains("no period", exception.Message);
            Assert.Contains("e = ", exception.Message);
            Assert.Contains("a = ", exception.Message);
            Assert.True(orbit.Eccentricity() >= 1.0, name);
        }
    }

    [Fact]
    public void Issue663_AnOpenOrbitGivenAPeriod_HasNoPeriod()
    {
        // Arrange: SPICE's period (oscltx writes 0 for an open orbit; a period given to the constructor, removed by #668's
        // R23, was the other source) was returned without
        // the check of #663 (the review of #663: 01:00:00 and 00:00:00)
        double r = 7000000.0;
        double escape = System.Math.Sqrt(2.0 * _earth.GM / r);
        var hyperbola = new StateVector(new Vector3(r, 0.0, 0.0), new Vector3(0.0, 1.2 * escape, 0.0), _earth, Epoch,
            Frames.Frame.ICRF);
        var orbits = new (string Name, IO.Astrodynamics.OrbitalParameters.OrbitalParameters Orbit)[]
        {
            ("SPICE's elements of a hyperbola", SpiceApi.Instance.ConvertStateVectorToConicOrbitalElement(hyperbola)),
        };

        foreach (var (name, orbit) in orbits)
        {
            // Act, assert
            var exception = Assert.Throws<InvalidOperationException>(() => orbit.Period());
            Assert.Contains("no period", exception.Message);
            Assert.True(orbit.Eccentricity() > 1.0, name);
        }
    }

    [Fact]
    public void Issue663_SpicesElementsOfAnEllipseWithoutATimeSpanPeriod_AreBuilt()
    {
        // Arrange: e = 1 − 2e-6, outside the band, a = 3.5e12 m: SPICE's period, 2.1e12 s, exceeds TimeSpan.MaxValue
        // (9.2e11 s), and the conversion passed it to TimeSpan.FromSeconds
        double r = 7000000.0;
        double vy = System.Math.Sqrt(_earth.GM * (2.0 - 2e-6) / r);
        var sv = new StateVector(new Vector3(r, 0.0, 0.0), new Vector3(0.0, vy, 0.0), _earth, Epoch, Frames.Frame.ICRF);

        // Act
        var ke = SpiceApi.Instance.ConvertStateVectorToConicOrbitalElement(sv);

        // Assert: the elements of the state, and the period's documented OverflowException, as from the state
        Assert.True(ke.IsElliptical());
        Assert.Equal(sv.SemiMajorAxis(), ke.SemiMajorAxis(), 1e-9 * sv.SemiMajorAxis());
        Assert.Throws<OverflowException>(() => ke.Period());
        Assert.Throws<OverflowException>(() => sv.Period());
    }

    [Fact]
    public void Issue663_AnEllipse_KeepsItsPeriod()
    {
        // 2π √(a³/μ), the closed form, to the nearest tick (#637; ExactTicks, the exact integer reference)
        var ke = Keplerian(a: 7000000.0, e: 0.1);
        Assert.Equal(IO.Astrodynamics.Tests.Time.ExactTicks.Nearest(IO.Astrodynamics.Constants.TwoPi * System.Math.Sqrt(7e6 * 7e6 * 7e6 / _earth.GM)),
            ke.Period().Ticks);
    }

    #endregion

    #region #737: the apsides of open orbits

    [Fact]
    public void Issue737_TheParabola_HasItsPeriapsis()
    {
        // Arrange: an exact parabola, a = +Infinity, q = 7000 km (the issue's), at its periapsis
        var ke = Parabola(7.0e6, i: 0.5, raan: 0.3, aop: 0.2, m: 0.0);

        // Act
        var periapsis = ke.PerigeeVector();
        double speed = ke.PerigeeVelocity();

        // Assert: q P̂, P̂ the perifocal axis (Vallado, algorithm 10 COE2RV at ν = 0), and the speed of vis-viva at q of a
        // parabola, √(2μ/q), 10 671.73 m/s (before: NaN, a(1 − e) = ∞ × 0); to the rounding of P̂ and of the square root
        Assert.Equal(ke.PeriapsisDirection() * 7.0e6, periapsis);
        Assert.Equal(7.0e6, periapsis.Magnitude(), 4e-16 * 7.0e6);
        double visViva = System.Math.Sqrt(2.0 * _earth.GM / 7.0e6);
        Assert.Equal(visViva, speed, 4e-16 * visViva);
        Assert.Equal(visViva, ke.ToStateVector().Velocity.Magnitude(), 4e-16 * visViva);
        Assert.Equal(periapsis.Magnitude(), ke.ToStateVector().Position.Magnitude(), 4e-16 * 7.0e6);
    }

    [Theory]
    [InlineData(0.9999995)]
    [InlineData(1.0000005)]
    public void Issue737_InTheBand_ThePeriapsisIsAtThePerigeeRadius(double e)
    {
        // Arrange: the conic's a of the band, q / (1 − e)
        const double q = 7.0e6;
        var ke = Parabola(q, e);

        // Act, assert: q P̂, the radius of the state at ν = 0, and vis-viva there
        Assert.Equal(ke.PeriapsisDirection() * q, ke.PerigeeVector());
        double visViva = System.Math.Sqrt(_earth.GM * (2.0 / q - 1.0 / ke.SemiMajorAxis()));
        Assert.Equal(visViva, ke.PerigeeVelocity(), 4e-16 * visViva);
    }

    [Theory]
    [InlineData(7.0e6, 0.1)]
    [InlineData(-1e7, 1.7)]
    public void Issue737_OutsideTheBand_ThePeriapsisIsUnchanged(double a, double e)
    {
        // a (1 − e) P̂ in the order of the products before 11.0.0, bit for bit
        var ke = Keplerian(a: a, e: e);
        Assert.Equal(ke.PeriapsisDirection() * a * (1.0 - e), ke.PerigeeVector());
        Assert.Equal(System.Math.Sqrt(_earth.GM * (2 / ke.PerigeeVector().Magnitude() - 1.0 / a)), ke.PerigeeVelocity());
    }

    [Fact]
    public void Issue737_AnOpenOrbit_HasNoApoapsis()
    {
        // Arrange: a hyperbola and a parabola as elements and as states (e one ulp above 1 at escape speed)
        double r = 7000000.0;
        double escape = System.Math.Sqrt(2.0 * _earth.GM / r);
        var orbits = new IO.Astrodynamics.OrbitalParameters.OrbitalParameters[]
        {
            Keplerian(a: -1e7, e: 1.7),
            Parabola(7.0e6),
            Parabola(7.0e6, 1.0000005),
            new StateVector(new Vector3(r, 0.0, 0.0), new Vector3(0.0, escape, 0.0), _earth, Epoch, Frames.Frame.ICRF),
            new StateVector(new Vector3(r, 0.0, 0.0), new Vector3(0.0, 1.2 * escape, 0.0), _earth, Epoch, Frames.Frame.ICRF),
            Equinoctial(7000000.0, 1.5, 0.0, 0.2, 0.1, 0.5),
        };

        foreach (var orbit in orbits)
        {
            // Act, assert: InvalidOperationException naming e and a, as Period() (before: a point of the other branch for a
            // hyperbola, ±Infinity for a parabola, and an apoapsis speed of 0 or NaN)
            var vector = Assert.Throws<InvalidOperationException>(() => orbit.ApogeeVector());
            var speed = Assert.Throws<InvalidOperationException>(() => orbit.ApogeeVelocity());
            Assert.Contains("no apoapsis", vector.Message);
            Assert.Contains("e = ", speed.Message);
            Assert.Contains("a = ", speed.Message);
        }
    }

    [Fact]
    public void Issue737_AnEllipseOfTheBand_KeepsItsApoapsis()
    {
        // The conic of a finite a above 0, e = 1 − 5e-7: its apoapsis −a (1 + e) P̂ and vis-viva there
        const double q = 7.0e6;
        const double e = 0.9999995;
        var ke = Parabola(q, e);
        double a = ke.SemiMajorAxis();
        Assert.Equal(ke.PeriapsisDirection().Inverse() * a * (1.0 + e), ke.ApogeeVector());
        Assert.Equal(System.Math.Sqrt(_earth.GM * (2 / ke.ApogeeVector().Magnitude() - 1.0 / a)), ke.ApogeeVelocity());
    }

    [Fact]
    public void Issue737_AnApogeeManeuverToAnOpenOrbit_IsRefused()
    {
        // Arrange: a target orbit without an apoapsis, and an engine on a spacecraft
        var clock = new IO.Astrodynamics.Body.Spacecraft.Clock("clk737", 256);
        var spacecraft = new IO.Astrodynamics.Body.Spacecraft.Spacecraft(-737, "S737", 1000.0, Keplerian(a: 7.0e6, e: 0.1), 10000.0,
            clock);
        var tank = new IO.Astrodynamics.Body.Spacecraft.FuelTank("ft737", 1000.0, 900.0, "model", "sn737");
        spacecraft.AddFuelTank(tank);
        var engine = new IO.Astrodynamics.Body.Spacecraft.Engine("eng737", 450.0, 50.0, tank, "model", "sn737");
        spacecraft.AddEngine(engine);

        // Act, assert: a bad argument (before: the apogee radius of the other branch, or +Infinity)
        var exception = Assert.Throws<ArgumentException>(() =>
            new IO.Astrodynamics.Maneuver.ApogeeHeightManeuver(Keplerian(a: -1e7, e: 1.7), engine, Epoch, TimeSpan.Zero));
        Assert.Equal("targetOrbit", exception.ParamName);
    }

    #endregion

    #region #738: the sign of the semi-major axis

    [Theory]
    [InlineData(-7.0e6, 0.1)]
    [InlineData(7.0e6, 1.5)]
    [InlineData(0.0, 0.1)]
    [InlineData(0.0, 0.0)]
    [InlineData(-0.0, 1.5)]
    [InlineData(-7.0e6, 0.0)]
    [InlineData(7.0e12, 1.0000011)]
    [InlineData(-7.0e12, 0.9999989)]
    public void Issue738_ASemiMajorAxisOfTheWrongSign_IsRefused(double a, double e)
    {
        // A conic has p = a (1 − e²) > 0: a > 0 for e < 1, a < 0 for e > 1, a ≠ 0 (the energy −μ/(2a) negative for an
        // ellipse, positive for a hyperbola); before, a NaN velocity without an exception
        var exception = Assert.Throws<ArgumentException>(() => Keplerian(a: a, e: e));
        Assert.Equal("semiMajorAxis", exception.ParamName);
        Assert.Contains($"a = {a:R}", exception.Message);
        Assert.Contains($"e = {e:R}", exception.Message);
    }

    [Theory]
    [InlineData(7.0e6, 0.0)]
    [InlineData(7.0e6, 0.1)]
    [InlineData(7.0e12, 0.999999)]
    [InlineData(-7.0e12, 1.0000011)]
    [InlineData(-1e7, 1.7)]
    public void Issue738_ASemiMajorAxisOfTheSignOfTheConic_IsAccepted(double a, double e)
    {
        var state = Keplerian(a: a, e: e).ToStateVector();
        Assert.True(double.IsFinite(state.Velocity.Magnitude()) && double.IsFinite(state.Position.Magnitude()));
    }

    #endregion
}
