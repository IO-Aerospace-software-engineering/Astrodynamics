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
    private static readonly TimeSystem.Time Epoch = new(new DateTime(2026, 1, 20, 6, 0, 0), TimeFrame.TDTFrame);
    private readonly CelestialBody _earth;

    public ElementsValidationTests()
    {
        TestKernels.EnsureSolarSystem();
        _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);
    }

    private KeplerianElements Keplerian(double a = 7000000.0, double e = 0.1, double i = 0.5, double raan = 1.0,
        double aop = 2.0, double m = 3.0, double? trueAnomaly = null, double? perigeeRadius = null) =>
        new(a, e, i, raan, aop, m, _earth, Epoch, Frames.Frame.ICRF, trueAnomaly: trueAnomaly, perigeeRadius: perigeeRadius);

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
            AssertRefused(() => Keplerian(raan: x), "rigthAscendingNode");
            AssertRefused(() => Keplerian(aop: x), "argumentOfPeriapsis");
            AssertRefused(() => Keplerian(m: x), "meanAnomaly");
            AssertRefused(() => Keplerian(trueAnomaly: x), "trueAnomaly");
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
        var parabola = Keplerian(a: double.PositiveInfinity, e: 1.0, perigeeRadius: 6800000.0);
        Assert.True(double.IsPositiveInfinity(parabola.A));
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
            Keplerian(a: double.PositiveInfinity, e: 1.0, perigeeRadius: q));
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
        var parabola = Keplerian(a: double.PositiveInfinity, e: 1.0, perigeeRadius: 6800000.0).ToEquinoctial();
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
            ("Keplerian parabola", Keplerian(a: double.PositiveInfinity, e: 1.0, perigeeRadius: 6800000.0)),
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
        // Arrange: a period given to the constructor, or SPICE's (oscltx writes 0 for an open orbit), was returned without
        // the check of #663 (the review of #663: 01:00:00 and 00:00:00)
        double r = 7000000.0;
        double escape = System.Math.Sqrt(2.0 * _earth.GM / r);
        var hyperbola = new StateVector(new Vector3(r, 0.0, 0.0), new Vector3(0.0, 1.2 * escape, 0.0), _earth, Epoch,
            Frames.Frame.ICRF);
        var orbits = new (string Name, IO.Astrodynamics.OrbitalParameters.OrbitalParameters Orbit)[]
        {
            ("Keplerian hyperbola given 1 h", new KeplerianElements(-1e7, 1.7, 0.1, 0.2, 0.3, 0.4, _earth, Epoch,
                Frames.Frame.ICRF, period: TimeSpan.FromHours(1.0))),
            ("SPICE's elements of a hyperbola", SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(hyperbola)),
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
        var ke = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(sv);

        // Assert: the elements of the state, and the period's documented OverflowException, as from the state
        Assert.True(ke.IsElliptical());
        Assert.Equal(sv.SemiMajorAxis(), ke.SemiMajorAxis(), 1e-9 * sv.SemiMajorAxis());
        Assert.Throws<OverflowException>(() => ke.Period());
        Assert.Throws<OverflowException>(() => sv.Period());
    }

    [Fact]
    public void Issue663_AnEllipse_KeepsItsPeriod()
    {
        // 2π √(a³/μ), the closed form, as before
        var ke = Keplerian(a: 7000000.0, e: 0.1);
        Assert.Equal(TimeSpan.FromSeconds(IO.Astrodynamics.Constants._2PI * System.Math.Sqrt(7e6 * 7e6 * 7e6 / _earth.GM)), ke.Period());
    }

    #endregion
}
