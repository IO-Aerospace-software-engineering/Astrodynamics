// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.Exceptions;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// Value equality of orbital parameters within one concrete type, without a state computed (#475, choices 1 and 2
/// decided by S. Guillet, 2026-11-17): the values of the type, its observer's NAIF id, its epoch (an instant, #512), its
/// frame and its <see cref="OrbitalElementsType"/>; equal sets share a hash code. Every expected value is a boolean or a
/// count set by the .NET contract: CA1065 (an <c>Equals</c> and an equality operator return true or false and do not
/// throw) and <see cref="object.GetHashCode"/> (two objects that compare as equal return the same hash code), so it is
/// the same on every platform. Inputs: the sets of the issue, a = 7000 km, e = 0.1, i = 0.5 rad, Ω = 1 rad, ω = 2 rad,
/// M = 3 rad (set 1), a = 26 560 km, e = 0.02, i = 55°, Ω = 1.2 rad, ω = 0.4 rad, M = 2.5 rad (set 2), and the ISS TLE of
/// <see cref="TLETests"/>.
/// </summary>
public class OrbitalParametersEqualityTests
{
    private const string IssLine1 = "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054";
    private const string IssLine2 = "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703";

    private static readonly TimeSystem.Time Epoch = new(new DateTime(2025, 3, 28, 0, 0, 0), TimeScale.UTC);

    public OrbitalParametersEqualityTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static CelestialBody Earth => TestHelpers.EarthAtJ2000;

    private static KeplerianElements Set1(OrbitalElementsType type = OrbitalElementsType.Osculating, double a = 7000e3, double e = 0.1,
        double m = 3.0) => new(a, e, 0.5, 1.0, 2.0, m, Earth, Epoch, Frames.Frame.ICRF, elementsType: type);

    private static KeplerianElements Set2(OrbitalElementsType type = OrbitalElementsType.Osculating) =>
        new(26560e3, 0.02, 55.0 * IO.Astrodynamics.Constants.Deg2Rad, 1.2, 0.4, 2.5, Earth, Epoch, Frames.Frame.ICRF, elementsType: type);

    private static EquinoctialElements Equinoctial(OrbitalElementsType type, double p = 6930e3) =>
        new(p, 0.01, -0.02, 0.1, 0.2, 1.5, Earth, Epoch, Frames.Frame.ICRF, elementsType: type);

    private static StateVector State(double x = 6800e3) =>
        new(new Vector3(x, 1000.0, -2000.0), new Vector3(10.0, 7500.0, 300.0), Earth, Epoch, Frames.Frame.ICRF);

    private static TLE Iss(string name = "ISS") => new(name, IssLine1, IssLine2);

    // A parabola, e = 1 exactly, given its perigee radius q: a = +Infinity (#459), P = +Infinity
    private static KeplerianElements Parabola(double q) =>
        KeplerianElements.Parabolic(q, 0.5, 1.0, 2.0, 0.0, Earth, Epoch, Frames.Frame.ICRF);

    private static EquinoctialElements EquinoctialParabola(double q) =>
        new(double.PositiveInfinity, 1.0, 0.0, 0.1, 0.2, 1.5, Earth, Epoch, Frames.Frame.ICRF, perigeeRadius: q);

    /// <summary>
    /// The pairs of equal copies (x, y) and a different set z, one per type and element type.
    /// </summary>
    public static TheoryData<string> Cases => new()
    {
        "keplerian osculating", "keplerian mean", "keplerian mean set 2", "keplerian from OMM", "equinoctial osculating",
        "equinoctial mean", "state vector", "TLE", "state vector NaN", "keplerian M signed zero", "keplerian parabola",
        "equinoctial parabola"
    };

    private static (Astrodynamics.OrbitalParameters.OrbitalParameters X, Astrodynamics.OrbitalParameters.OrbitalParameters Y,
        Astrodynamics.OrbitalParameters.OrbitalParameters Z) Case(string name) => name switch
    {
        "keplerian osculating" => (Set1(), Set1(), Set1(a: 7001e3)),
        "keplerian mean" => (Set1(OrbitalElementsType.Mean), Set1(OrbitalElementsType.Mean), Set1(OrbitalElementsType.Mean, a: 7001e3)),
        "keplerian mean set 2" => (Set2(OrbitalElementsType.Mean), Set2(OrbitalElementsType.Mean), Set1(OrbitalElementsType.Mean)),
        "keplerian from OMM" => (FromOmm(15.49309423), FromOmm(15.49309423), FromOmm(15.5)),
        "equinoctial osculating" => (Equinoctial(OrbitalElementsType.Osculating), Equinoctial(OrbitalElementsType.Osculating),
            Equinoctial(OrbitalElementsType.Osculating, 6931e3)),
        "equinoctial mean" => (Equinoctial(OrbitalElementsType.Mean), Equinoctial(OrbitalElementsType.Mean), Equinoctial(OrbitalElementsType.Mean, 6931e3)),
        "state vector" => (State(), State(), State(6801e3)),
        "TLE" => (Iss(), Iss(), Iss("ISS2")),
        // The elements refuse a non-finite value since #476 and #726, a state does not: NaN equals NaN, as for double.Equals,
        // and two states that differ elsewhere are different
        "state vector NaN" => (State(double.NaN), State(double.NaN),
            new StateVector(new Vector3(double.NaN, 1001.0, -2000.0), new Vector3(10.0, 7500.0, 300.0), Earth, Epoch, Frames.Frame.ICRF)),
        // double.Equals and double.GetHashCode take +0 and -0 as one value
        "keplerian M signed zero" => (Set1(m: 0.0), Set1(m: -0.0), Set1(m: double.Epsilon)),
        // Within 1e-6 of e = 1 the state is that of the perigee radius (#459), which a = +Infinity does not fix
        "keplerian parabola" => (Parabola(7000e3), Parabola(7000e3), Parabola(7500e3)),
        "equinoctial parabola" => (EquinoctialParabola(7000e3), EquinoctialParabola(7000e3), EquinoctialParabola(7500e3)),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static KeplerianElements FromOmm(double meanMotion) =>
        KeplerianElements.FromOMM(meanMotion, 0.0000493, 51.6423, 353.0312, 320.8755, 39.2360, Earth, Epoch, Frames.Frame.TEME);

    [Theory]
    [MemberData(nameof(Cases))]
    public void EqualCopies_AreEqual_OnEveryPath_WithOneHashCode(string name)
    {
        var (x, y, z) = Case(name);
        Assert.NotSame(x, y);

        // Typed as OrbitalParameters: no throw, the values decide
        Assert.True(x == y);
        Assert.False(x != y);
        Assert.True(x.Equals(y));
        Assert.True(x.Equals((object)y));
        Assert.True(y.Equals(x));
        Assert.True(EqualityComparer<Astrodynamics.OrbitalParameters.OrbitalParameters>.Default.Equals(x, y));
        Assert.Equal(x.GetHashCode(), y.GetHashCode());

        Assert.False(x == z);
        Assert.True(x != z);
        Assert.False(x.Equals(z));
        Assert.False(x.Equals((object)z));
        Assert.False(z.Equals(x));

        Assert.False(x.Equals((Astrodynamics.OrbitalParameters.OrbitalParameters)null));
        Assert.False(x.Equals((object)null));
        Assert.False(x == null);
        Assert.True(x != null);

        // The collections of the base type
        Assert.Single(new HashSet<Astrodynamics.OrbitalParameters.OrbitalParameters> { x, y });
        Assert.Equal(2, new HashSet<Astrodynamics.OrbitalParameters.OrbitalParameters> { x, z }.Count);
        Assert.Equal(1, new Dictionary<Astrodynamics.OrbitalParameters.OrbitalParameters, int> { [x] = 1 }[y]);
        Assert.Single(new[] { x, y }.Distinct());
        Assert.Contains(y, new List<Astrodynamics.OrbitalParameters.OrbitalParameters> { x });
        Assert.Equal(0, new List<Astrodynamics.OrbitalParameters.OrbitalParameters> { x }.IndexOf(y));
        Assert.True(new[] { x, z }.SequenceEqual(new[] { y, z }));
        Assert.Equal((x, 1), (y, 1));
    }

    /// <summary>
    /// The concrete types: their own operators, <see cref="IEquatable{T}"/> and hash collections agree with the base type.
    /// </summary>
    [Fact]
    public void TheConcreteTypes_AgreeWithTheBaseType()
    {
        AssertTyped(Set1(OrbitalElementsType.Mean), Set1(OrbitalElementsType.Mean), Set1(OrbitalElementsType.Mean, a: 7001e3));
        AssertTyped(Equinoctial(OrbitalElementsType.Mean), Equinoctial(OrbitalElementsType.Mean), Equinoctial(OrbitalElementsType.Mean, 6931e3));
        AssertTyped(State(), State(), State(6801e3));
        AssertTyped(Iss(), Iss(), Iss("ISS2"));

        Assert.True(Set1(OrbitalElementsType.Mean) == Set1(OrbitalElementsType.Mean));
        Assert.True(Equinoctial(OrbitalElementsType.Mean) == Equinoctial(OrbitalElementsType.Mean));
        Assert.True(State() == State());
        Assert.True(Iss() == Iss());
    }

    private static void AssertTyped<T>(T x, T y, T z) where T : Astrodynamics.OrbitalParameters.OrbitalParameters, IEquatable<T>
    {
        Assert.True(x.Equals(y));
        Assert.False(x.Equals(z));
        Assert.True(EqualityComparer<T>.Default.Equals(x, y));
        Assert.Equal(x.GetHashCode(), y.GetHashCode());
        Assert.Single(new HashSet<T> { x, y });
        Assert.Equal(1, new Dictionary<T, int> { [x] = 1 }[y]);
        // Mixed static types
        Assert.True(x == (Astrodynamics.OrbitalParameters.OrbitalParameters)y);
        Assert.True(((Astrodynamics.OrbitalParameters.OrbitalParameters)x).Equals(y));
    }

    /// <summary>
    /// Pairs of different orbits by choice 1 (a set and its own state) and choice 2 (mean and osculating sets of the same
    /// numbers, a TLE and its mean elements): unequal on every path, in both orders, without a throw.
    /// </summary>
    [Fact]
    public void ASetAndItsState_MeanAndOsculating_ATleAndItsMeanElements_AreDifferent()
    {
        var osculating = Set1();
        var tle = Iss();
        AssertDifferent(osculating, osculating.ToStateVector());
        AssertDifferent(osculating, Set1(OrbitalElementsType.Mean));
        AssertDifferent(Equinoctial(OrbitalElementsType.Osculating), Equinoctial(OrbitalElementsType.Mean));
        AssertDifferent(tle, tle.ToMeanKeplerianElements());

        Assert.False(Set1() == Set1(OrbitalElementsType.Mean));
        Assert.False(Equinoctial(OrbitalElementsType.Osculating) == Equinoctial(OrbitalElementsType.Mean));
    }

    private static void AssertDifferent(Astrodynamics.OrbitalParameters.OrbitalParameters x, Astrodynamics.OrbitalParameters.OrbitalParameters y)
    {
        foreach (var (a, b) in new[] { (x, y), (y, x) })
        {
            Assert.False(a == b);
            Assert.True(a != b);
            Assert.False(a.Equals(b));
            Assert.False(a.Equals((object)b));
            Assert.Equal(2, new HashSet<Astrodynamics.OrbitalParameters.OrbitalParameters> { a, b }.Count);
        }
    }

    /// <summary>
    /// Within 1e-6 of e = 1 the state is that of the perigee radius q, the semi-major axis that of the conic (#459), so q is
    /// a value of the set where a = +Infinity does not fix it: two parabolas that differ by q alone are different orbits, a
    /// state at the periapsis (M = 0, ν = 0) at |r| = q for the Keplerian set (the closed form of the conic), different
    /// states for the equinoctial set, and so different values (#475); the same q gives equal values, with one hash code
    /// (<see cref="EqualCopies_AreEqual_OnEveryPath_WithOneHashCode"/>, the rows "keplerian parabola" and "equinoctial
    /// parabola").
    /// </summary>
    [Fact]
    public void TwoParabolasOfDifferentPerigeeRadii_AreDifferent()
    {
        var near = Parabola(7000e3);
        var far = Parabola(7500e3);
        Assert.Equal(7000e3, near.ToStateVector().Position.Magnitude(), 1e-6);
        Assert.Equal(7500e3, far.ToStateVector().Position.Magnitude(), 1e-6);
        AssertDifferent(near, far);

        var equinoctialNear = EquinoctialParabola(7000e3);
        var equinoctialFar = EquinoctialParabola(7500e3);
        Assert.NotEqual(equinoctialNear.ToStateVector().Position, equinoctialFar.ToStateVector().Position);
        AssertDifferent(equinoctialNear, equinoctialFar);
    }

    /// <summary>
    /// The same epoch in another time frame is the same instant (#512): the sets are equal, with one hash code.
    /// </summary>
    [Fact]
    public void TheEpochInAnotherTimeFrame_IsTheSameInstant()
    {
        var utc = Set1(OrbitalElementsType.Mean);
        var tdb = new KeplerianElements(utc.SemiMajorAxis(), utc.Eccentricity(), utc.Inclination(), utc.AscendingNode(), utc.ArgumentOfPeriapsis(), utc.MeanAnomaly(), Earth, Epoch.ToTDB(), Frames.Frame.ICRF,
            elementsType: OrbitalElementsType.Mean);

        Assert.True(utc == (Astrodynamics.OrbitalParameters.OrbitalParameters)tdb);
        Assert.Equal(utc.GetHashCode(), tdb.GetHashCode());
    }

    /// <summary>
    /// Equality reads no state: two copies of a TLE whose SGP4 fails (a mean motion of 19 rev/day, below the surface,
    /// SPICE(SUBORBITAL)) are equal, while its state throws (#454). Before 11.0.0 the comparison ran SGP4 on both.
    /// </summary>
    [Fact]
    public void TleEquality_ReadsNoState()
    {
        const string line2 = "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 19.00000000 25703";
        var x = new TLE("SUB", IssLine1, line2);
        var y = new TLE("SUB", IssLine1, line2);

        Assert.True(x == y);
        Assert.True(x.Equals((Astrodynamics.OrbitalParameters.OrbitalParameters)y));
        Assert.Equal(x.GetHashCode(), y.GetHashCode());
        Assert.Throws<SpiceException>(() => x.ToStateVector());
    }

    /// <summary>
    /// A hash set of the base type fed identical mean copies keeps one and never throws: before 11.0.0 the hash codes held
    /// the identity of each instance, and the first collision of two of them called the comparison through states, which
    /// throws for mean elements (after 3 908 to 11 184 inserts in the issue's runs; 9 645 is the median of the first
    /// collision of 2^26 values).
    /// </summary>
    [Fact]
    public void AHashSetOfMeanCopies_KeepsOne()
    {
        var set = new HashSet<Astrodynamics.OrbitalParameters.OrbitalParameters>();
        for (int i = 0; i < 50_000; i++)
        {
            set.Add(Set1(OrbitalElementsType.Mean));
        }

        Assert.Single(set);
    }
}
