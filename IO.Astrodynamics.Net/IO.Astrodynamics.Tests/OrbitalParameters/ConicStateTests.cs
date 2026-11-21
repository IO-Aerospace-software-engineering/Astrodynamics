// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// The conic state of <see cref="KeplerianElements"/> given by their true anomaly, and the semi-latus rectum of
/// <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.ToEquinoctial"/>, near e = 1 outside the parabolic band
/// (#472): p = a(1 − e·e) cancelled there, and 1 + e cos ν and e + cos ν near the apoapsis and the asymptote.
/// </summary>
/// <remarks>
/// The reference, <c>Data/ConicStateReference/conic_state_reference.json</c>, comes from
/// <c>tools/conic_state/conic_state_reference.py</c> (mpmath 1.4.1): the perifocal state of the conic of the double elements
/// at 60 digits, p = a(1 − e²), r = p / (1 + e cos ν), v = √(μ/p)(−sin ν, e + cos ν), turned by R3(Ω) R1(i) R3(ω), and p
/// itself, each value a double and its rest. The inputs are doubles, so the reference is exact for them and measures the
/// computation alone.
/// </remarks>
public class ConicStateTests
{
    private const string ReferenceFile = "Data/ConicStateReference/conic_state_reference.json";
    private static readonly TimeSystem.Time Epoch = new(new DateTime(2026, 1, 20, 6, 0, 0), TimeScale.TT);
    private static readonly Lazy<ConicReference> Reference = new(Load);

    // Twice the worst over Linux x64, Windows x64 and macOS arm64 (the MEASURE lines, CI run 44395076184, 2026-11-18),
    // rounded up to two digits (#472), the worsts the same on the three. p: 1.735e-16 relative (2.2e-11 near e = 1
    // before); the state away from the apoapsis and the asymptote, 4.455e-16 in position (2.2e-11 near e = 1 before); the
    // velocity everywhere, 3.101e-16 (3.6e-11 near e = 1 before)
    private const double SemiLatusRectumBound = 3.5e-16;
    private const double PositionBound = 9.0e-16;
    private const double VelocityBound = 6.3e-16;

    // Near the apoapsis or the asymptote (|1 + e cos ν| < 0.05) near e = 1: 3.695e-15 in position, at e = 1.001 near its
    // asymptote (8.1e-11 at e = 1.000002 and 4.6e-11 at e = 0.999999 before); of the controls, 2.785e-13 at e = 1.2 near
    // its asymptote, the rounding of cos(ν/2) divided by 1 + e cos ν, which neither form lowers there (2.9e-13 before).
    // Twice the worst over Linux x64, Windows x64 and macOS arm64, the same on the three (CI run 44395076184, 2026-11-18)
    private const double ClosePositionBound = 7.4e-15;
    private const double AsymptotePositionBound = 5.6e-13;

    private readonly ITestOutputHelper _output;
    private readonly CelestialBody _earth;

    public ConicStateTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
        _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);
        Assert.Equal(Reference.Value.Mu, _earth.GM);
    }

    private void Measure(string what, double value) => _output.WriteLine($"MEASURE #472 {what}: {value:E3}");

    private KeplerianElements Elements(ReferenceCase c) =>
        new(c.A, c.E, Reference.Value.I, Reference.Value.Node, Reference.Value.Argument, 0.0, _earth, Epoch,
            Frames.Frame.ICRF, trueAnomaly: c.Nu);

    [Fact]
    public void SemiLatusRectum_NearEOne_IsTheConics()
    {
        // Arrange: e from 0.999 to 1.001 outside the band, and the controls from 0.1 to 3
        var cases = Reference.Value.Cases.GroupBy(c => c.E).Select(g => g.First()).ToList();

        // Act and Assert: ToEquinoctial().P, the p of ToStateVector, against a(1 − e²) at 60 digits
        double worstNear = 0.0, worstControl = 0.0;
        foreach (var c in cases)
        {
            double p = Elements(c).ToEquinoctial().P;
            double error = System.Math.Abs((p - c.P.High - c.P.Low) / c.P.High);
            Measure($"e = {c.E:R}: P relative error", error);
            if (c.Near)
            {
                worstNear = System.Math.Max(worstNear, error);
            }
            else
            {
                worstControl = System.Math.Max(worstControl, error);
            }
        }

        Measure("worst P relative error near e = 1", worstNear);
        Measure("worst P relative error of the controls", worstControl);
        Assert.Equal(19, cases.Count);
        Assert.InRange(worstNear, 0.0, SemiLatusRectumBound);
        Assert.InRange(worstControl, 0.0, SemiLatusRectumBound);
    }

    [Fact]
    public void State_NearEOne_IsTheConics()
    {
        // Arrange: near e = 1 outside the band, ν at the periapsis, at π/2, near the apoapsis (π ± 1e-1 to 1e-6, fl(π)) and
        // near the asymptote of a hyperbola (±0.9999 ν∞)
        var cases = Reference.Value.Cases.Where(c => c.Near).ToList();

        // Act
        var (position, velocity) = Worst(cases.Where(c => !NearAnApsisOrTheAsymptote(c)), "near e = 1, away from the apoapsis and the asymptote");
        var (closePosition, closeVelocity) = Worst(cases.Where(NearAnApsisOrTheAsymptote), "near e = 1, near the apoapsis or the asymptote");

        // Assert: within the rounding of the state (before: 2.2e-11 away, 8.1e-11 in position near the asymptote at
        // e = 1.000002 and 4.6e-11 near the apoapsis at e = 0.999999, 155 m at fl(π))
        Assert.Equal(128, cases.Count);
        Assert.InRange(position, 0.0, PositionBound);
        Assert.InRange(velocity, 0.0, VelocityBound);
        Assert.InRange(closePosition, 0.0, ClosePositionBound);
        Assert.InRange(closeVelocity, 0.0, VelocityBound);
    }

    [Fact]
    public void State_Controls_AreTheConics()
    {
        // Arrange: e = 0.1 to 3, inside the bands of the rewrite and outside them
        var cases = Reference.Value.Cases.Where(c => !c.Near).ToList();

        // Act
        var (position, velocity) = Worst(cases.Where(c => !NearAnApsisOrTheAsymptote(c)), "controls, away from the apoapsis and the asymptote");
        var (closePosition, closeVelocity) = Worst(cases.Where(NearAnApsisOrTheAsymptote), "controls, near the apoapsis or the asymptote");

        // Assert: away, the rounding of the state; near the asymptote of a hyperbola beyond e = 1.01 the rounding of
        // cos ν (or cos(ν/2)) divided by 1 + e cos ν, which neither form lowers there (#472: 1.2e-12 at e = 1.3)
        Assert.Equal(173, cases.Count);
        Assert.InRange(position, 0.0, PositionBound);
        Assert.InRange(velocity, 0.0, VelocityBound);
        Assert.InRange(closePosition, 0.0, AsymptotePositionBound);
        Assert.InRange(closeVelocity, 0.0, VelocityBound);
    }

    [Theory]
    [InlineData(6800000.0, 0.1)]
    [InlineData(6800000.0, 0.3)]
    [InlineData(6800000.0, 0.4999999999999999)]
    [InlineData(-3400000.0, 2.0000000000000004)]
    [InlineData(-3400000.0, 3.0)]
    [InlineData(-680000.0, 11.0)]
    public void OutsideTheBands_PAndTheStateAreUnchanged(double a, double e)
    {
        // Arrange: below e = 0.5 and above e = 2, where a(1 − e·e) is the more accurate product (#472, choice 1, decided by
        // S. Guillet, 2026-11-18)
        foreach (double nu in new[] { 0.0, 0.7, 2.5, -1.0 })
        {
            if (e > 1.0 && System.Math.Abs(nu) >= System.Math.Acos(-1.0 / e))
            {
                continue;
            }

            var ke = new KeplerianElements(a, e, 0.2, 0.3, 0.4, 0.0, _earth, Epoch, Frames.Frame.ICRF, trueAnomaly: nu);

            // Act
            var state = ke.ToStateVector();

            // Assert: p, r and v of the products before 11.0.0, bit for bit
            double p = a * (1 - e * e);
            Assert.Equal(p, ke.ToEquinoctial().P);
            var expected = PerifocalState(p, e, nu, 1 + e * System.Math.Cos(nu), e + System.Math.Cos(nu));
            Assert.Equal(expected.Position, state.Position);
            Assert.Equal(expected.Velocity, state.Velocity);
        }
    }

    // The state of the elements turned as ToStateVector turns it, from the doubles of the test: the products in the form
    // given, bit for bit
    private (Vector3 Position, Vector3 Velocity) PerifocalState(double p, double e, double nu, double denominator,
        double velocityFactor)
    {
        double r0 = p / denominator;
        double[] pos = [r0 * System.Math.Cos(nu), r0 * System.Math.Sin(nu), 0.0];
        double[] vel = [-System.Math.Sqrt(_earth.GM / p) * System.Math.Sin(nu), System.Math.Sqrt(_earth.GM / p) * velocityFactor, 0.0];
        var r = IO.Astrodynamics.Math.Matrix.CreateRotationMatrixZ(0.3) * IO.Astrodynamics.Math.Matrix.CreateRotationMatrixX(0.2) *
                IO.Astrodynamics.Math.Matrix.CreateRotationMatrixZ(0.4);
        double[] position = pos * r;
        double[] velocity = vel * r;
        return (new Vector3(position[0], position[1], position[2]), new Vector3(velocity[0], velocity[1], velocity[2]));
    }

    // |1 + e cos ν| below 0.05, the issue's "near": the apoapsis of an ellipse or the asymptote of a hyperbola
    private static bool NearAnApsisOrTheAsymptote(ReferenceCase c) => System.Math.Abs(1.0 + c.E * System.Math.Cos(c.Nu)) < 0.05;

    private (double Position, double Velocity) Worst(IEnumerable<ReferenceCase> cases, string what)
    {
        double worstPosition = 0.0, worstVelocity = 0.0;
        foreach (var c in cases)
        {
            var state = Elements(c).ToStateVector();
            double position = RelativeError(state.Position, c.R);
            double velocity = RelativeError(state.Velocity, c.V);
            _output.WriteLine($"MEASURE #472 e = {c.E:R}, ν = {c.Nu:R}: |Δr|/|r| {position:E3}, |Δv|/|v| {velocity:E3}");
            worstPosition = System.Math.Max(worstPosition, position);
            worstVelocity = System.Math.Max(worstVelocity, velocity);
        }

        Measure($"worst |Δr|/|r| {what}", worstPosition);
        Measure($"worst |Δv|/|v| {what}", worstVelocity);
        return (worstPosition, worstVelocity);
    }

    // |x − x*| / |x*|, the components of x* each a double and its rest
    private static double RelativeError(Vector3 actual, Exact[] expected)
    {
        double dx = actual.X - expected[0].High - expected[0].Low;
        double dy = actual.Y - expected[1].High - expected[1].Low;
        double dz = actual.Z - expected[2].High - expected[2].Low;
        double norm = System.Math.Sqrt(expected.Sum(c => c.High * c.High));
        return System.Math.Sqrt(dx * dx + dy * dy + dz * dz) / norm;
    }

    private readonly record struct Exact(double High, double Low);

    private sealed record ReferenceCase(double E, double A, double Nu, Exact P, Exact[] R, Exact[] V, bool Near);

    private sealed record ConicReference(double Mu, double I, double Node, double Argument, IReadOnlyList<ReferenceCase> Cases);

    private static ConicReference Load()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ReferenceFile));
        var root = document.RootElement;
        static Exact Pair(JsonElement e) => new(e[0].GetDouble(), e[1].GetDouble());
        var cases = root.GetProperty("cases").EnumerateArray().Select(c => new ReferenceCase(
            c.GetProperty("e").GetDouble(), c.GetProperty("a").GetDouble(), c.GetProperty("nu").GetDouble(),
            Pair(c.GetProperty("p")), c.GetProperty("r").EnumerateArray().Select(Pair).ToArray(),
            c.GetProperty("v").EnumerateArray().Select(Pair).ToArray(), c.GetProperty("near").GetBoolean())).ToList();
        return new ConicReference(root.GetProperty("mu").GetDouble(), root.GetProperty("i").GetDouble(),
            root.GetProperty("node").GetDouble(), root.GetProperty("argument").GetDouble(), cases);
    }
}
