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
/// Kepler's equation near the periapsis at e → 1 (#484): the solver of E from M, the reverse direction M = E − e sin E
/// (e sinh H − H), the states from a mean anomaly and the round trip state → elements → state, where x − e sin x and
/// e sinh x − x cancelled.
/// </summary>
/// <remarks>
/// The reference, <c>Data/KeplerEquationReference/kepler_periapsis_reference.json</c>, comes from
/// <c>tools/kepler_equation/kepler_periapsis_reference.py</c> (mpmath 1.4.1): the root E* of the double M at 60 digits, M*
/// of a double E at 60 digits, and the perifocal state of the root, each value a double and its rest.
/// </remarks>
public class KeplerPeriapsisTests
{
    private const string ReferenceFile = "Data/KeplerEquationReference/kepler_periapsis_reference.json";
    private static readonly TimeSystem.Time Epoch = new(new DateTime(2026, 1, 20, 6, 0, 0), TimeScale.TT);
    private static readonly Lazy<PeriapsisReference> Reference = new(Load);

    // Twice the worst over Linux x64, Windows x64 and macOS arm64 (the MEASURE lines, CI run 44395076184, 2026-11-18),
    // rounded up to two digits (#484). The solver: 1.879 ulp of E* for 0.5 ≤ e ≤ 2 (e = 1.00001, Linux x64 and macOS
    // arm64; 1.832 at e = 0.99999 on Windows x64; before, 9.3e5 ulp at e = 0.999999, 1.4e5 at 1.00001, 902 at 0.999),
    // 2.424 ulp outside it (e = 3, Linux x64; 2.005 on macOS arm64, 1.743 on Windows x64; unchanged), 6 iterations on the
    // three (10 before); the reverse direction 6.385e-16 of M (e = 1.001, Linux x64; 4.373e-16 at e = 1.00001 on
    // Windows x64 and macOS arm64; 1.9e-10 at e = 0.999999 before); the states from M within 2q, 9.669e-16 of |r| and
    // 5.224e-16 of |v| (Linux x64; 7.057e-16 and 4.213e-16 on Windows x64 and macOS arm64; 6.7e-11 and 3.3e-11 before);
    // the anomaly round trip state → elements, 2 ulp of E on Windows x64 and macOS arm64 (1 on Linux x64; 6.2e5 ulp
    // before)
    private const double SolverUlps = 3.8;
    private const double SolverUlpsOutsideTheBand = 4.9;
    private const int SolverIterations = 12;
    private const double ReverseRelative = 1.3e-15;
    private const double StatePosition = 2.0e-15;
    private const double StateVelocity = 1.1e-15;
    private const double RoundTripUlps = 4.0;

    private readonly ITestOutputHelper _output;
    private readonly CelestialBody _earth;

    public KeplerPeriapsisTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
        _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);
        Assert.Equal(Reference.Value.Mu, _earth.GM);
    }

    private void Measure(string what, double value) => _output.WriteLine($"MEASURE #484 {what}: {value:E3}");

    [Fact]
    public void Solver_NearThePeriapsis_IsWithinUlpsOfTheRoot()
    {
        // Arrange: e = 0.3 to 3, m from 1e-15 to π (10 on the hyperbola), M = ±m
        var points = Reference.Value.Solver;

        // Act: the internal solver, and the eccentric anomaly of KeplerianElements given by M, which keep it on an open
        // orbit and for 0 < M ≤ π on an ellipse
        var worst = new SortedDictionary<double, (double Ulps, int Iterations)>();
        foreach (var p in points)
        {
            bool hyperbolic = p.E > 1.0;
            double anomaly = hyperbolic ? KeplerEquation.HyperbolicAnomaly(p.M, p.E) : KeplerEquation.EllipticAnomaly(p.M, p.E);
            if (hyperbolic || p.M > 0.0)
            {
                Assert.Equal(anomaly, Elements(p.E, p.M).EccentricAnomaly());
            }

            double ulps = System.Math.Abs(p.Anomaly.ErrorOf(anomaly)) / Ulp(p.Anomaly.High);
            int iterations = Iterations(p, anomaly);
            var (u, n) = worst.GetValueOrDefault(p.E);
            worst[p.E] = (System.Math.Max(u, ulps), System.Math.Max(n, iterations));
        }

        // Assert: within ulps of E* in a few iterations, at every e
        Assert.Equal(4422, points.Count);
        foreach (var (e, (ulps, iterations)) in worst)
        {
            Measure($"solver e = {e:R}: worst |E − E*| in ulp of E*", ulps);
            _output.WriteLine($"MEASURE #484 solver e = {e:R}: most iterations {iterations}");
        }

        Assert.All(worst, w => Assert.InRange(w.Value.Ulps, 0.0, w.Key is >= 0.5 and <= 2.0 ? SolverUlps : SolverUlpsOutsideTheBand));
        Assert.All(worst, w => Assert.InRange(w.Value.Iterations, 1, SolverIterations));
    }

    [Fact]
    public void Reverse_NearThePeriapsis_IsTheMeanAnomalyOfE()
    {
        // Arrange: a double E from 1e-7 to 3 rad, e = 0.3 to 3
        var points = Reference.Value.Reverse;

        // Act: TrueAnomalyToMeanAnomaly, the reverse direction of EquinoctialElements.MeanAnomaly(), MeanAnomaly(ν),
        // ToStateVector(ν) and TimeToRadius; StateVector.MeanAnomaly() takes the same function
        var worst = new SortedDictionary<double, double>();
        foreach (var p in points)
        {
            double nu = p.E < 1.0
                ? 2.0 * System.Math.Atan(System.Math.Sqrt((1.0 + p.E) / (1.0 - p.E)) * System.Math.Tan(p.Anomaly / 2.0))
                : 2.0 * System.Math.Atan(System.Math.Sqrt((p.E + 1.0) / (p.E - 1.0)) * System.Math.Tanh(p.Anomaly / 2.0));
            double m = IO.Astrodynamics.OrbitalParameters.OrbitalParameters.TrueAnomalyToMeanAnomaly(nu, p.E, p.Anomaly);
            double error = System.Math.Abs(p.M.ErrorOf(m)) / System.Math.Abs(p.M.High);
            worst[p.E] = System.Math.Max(worst.GetValueOrDefault(p.E), error);
        }

        // Assert
        Assert.Equal(2211, points.Count);
        foreach (var (e, error) in worst)
        {
            Measure($"reverse e = {e:R}: worst |M − M*| / M*", error);
        }

        Assert.All(worst, w => Assert.InRange(w.Value, 0.0, ReverseRelative));
    }

    [Fact]
    public void States_FromAMeanAnomalyNearThePeriapsis_AreTheConics()
    {
        // Arrange: q = 7000 km, e = 0.999 to 1.001, ν from 0.5° to 90° (and before the periapsis on the hyperbola), r < 2q
        var states = Reference.Value.States;

        // Act
        double worstPosition = 0.0, worstVelocity = 0.0;
        foreach (var s in states)
        {
            var state = new KeplerianElements(s.A, s.E, Reference.Value.I, Reference.Value.Node, Reference.Value.Argument, s.M,
                _earth, Epoch, Frames.Frame.ICRF).ToStateVector();
            double position = RelativeError(state.Position, s.R);
            double velocity = RelativeError(state.Velocity, s.V);
            _output.WriteLine($"MEASURE #484 state e = {s.E:R}, ν = {s.NuDegrees:R}°: |Δr|/|r| {position:E3} ({position * s.Radius:E3} m), |Δv|/|v| {velocity:E3}");
            worstPosition = System.Math.Max(worstPosition, position);
            worstVelocity = System.Math.Max(worstVelocity, velocity);
        }

        // Assert
        Measure("states, worst |Δr|/|r|", worstPosition);
        Measure("states, worst |Δv|/|v|", worstVelocity);
        Assert.Equal(70, states.Count);
        Assert.InRange(worstPosition, 0.0, StatePosition);
        Assert.InRange(worstVelocity, 0.0, StateVelocity);
    }

    [Fact]
    public void RoundTrip_StateToElements_NearThePeriapsis_KeepsTheAnomaly()
    {
        // Arrange: the states of the previous test rounded to doubles; their elements pass the mean anomaly, not ν
        // (StateVector.MeanAnomaly(), the reverse direction), whose eccentric anomaly the solver finds again, so the
        // round trip goes through both directions. The state of the elements carries besides the rounding of the
        // semi-major axis from the energy, ε v²/(2|ξ|), which this issue does not change (#462): the anomaly is compared
        var states = Reference.Value.States;

        // Act
        double worst = 0.0;
        int count = 0;
        foreach (var s in states)
        {
            var sv = new StateVector(new Vector3(s.RDouble[0], s.RDouble[1], s.RDouble[2]),
                new Vector3(s.VDouble[0], s.VDouble[1], s.VDouble[2]), _earth, Epoch, Frames.Frame.ICRF);
            if (sv.IsParabolic())
            {
                continue;
            }

            Assert.Equal(KeplerEquationMeanAnomaly(sv), sv.MeanAnomaly());
            double anomaly = sv.EccentricAnomaly(sv.TrueAnomaly());
            double back = sv.ToKeplerianElements().EccentricAnomaly();
            double ulps = System.Math.Abs(back - anomaly) / Ulp(anomaly);
            _output.WriteLine($"MEASURE #484 round trip e = {s.E:R}, ν = {s.NuDegrees:R}°: |E − E0| {ulps:E3} ulp of E0");
            worst = System.Math.Max(worst, ulps);
            count++;
        }

        // Assert
        Measure("round trips, worst |E − E0| in ulp of E0", worst);
        // 7 of the 10 states at e = 0.999999 come back in the band |e − 1| < 1e-6 from their double state, whose anomalies
        // are Barker's: the 63 others are measured
        Assert.Equal(63, count);
        Assert.InRange(worst, 0.0, RoundTripUlps);
    }

    // The mean anomaly of the state's own eccentric anomaly by the library's function of Kepler's equation
    private static double KeplerEquationMeanAnomaly(StateVector sv)
    {
        double e = sv.Eccentricity();
        double anomaly = sv.EccentricAnomaly(sv.TrueAnomaly());
        return e < 1.0 ? KeplerEquation.EllipticMeanAnomaly(anomaly, e) : KeplerEquation.HyperbolicMeanAnomaly(anomaly, e);
    }

    private KeplerianElements Elements(double e, double m) =>
        new(Reference.Value.Q / (1.0 - e), e, 0.2, 0.3, 0.4, m, _earth, Epoch, Frames.Frame.ICRF);

    private static double Ulp(double x) => System.Math.BitIncrement(System.Math.Abs(x)) - System.Math.Abs(x);

    // The iterations of a solve: the smallest cap of the internal overload that converges, to the same E
    private static int Iterations(SolverPoint p, double expected)
    {
        for (int cap = 1; cap <= KeplerEquation.MaximumIterations; cap++)
        {
            try
            {
                double anomaly = p.E > 1.0 ? KeplerEquation.HyperbolicAnomaly(p.M, p.E, cap) : KeplerEquation.EllipticAnomaly(p.M, p.E, cap);
                Assert.Equal(expected, anomaly);
                return cap;
            }
            catch (InvalidOperationException)
            {
            }
        }

        return int.MaxValue;
    }

    private static double RelativeError(Vector3 actual, Exact[] expected)
    {
        var error = new Vector3(expected[0].ErrorOf(actual.X), expected[1].ErrorOf(actual.Y), expected[2].ErrorOf(actual.Z));
        return error.Magnitude() / new Vector3(expected[0].High, expected[1].High, expected[2].High).Magnitude();
    }

    private readonly record struct Exact(double High, double Low)
    {
        internal double ErrorOf(double x) => (x - High) - Low;
    }

    private sealed record SolverPoint(double E, double M, Exact Anomaly);

    private sealed record ReversePoint(double E, double Anomaly, Exact M);

    private sealed record ReferenceState(double E, double A, double NuDegrees, double M, Exact[] R, Exact[] V, double[] RDouble,
        double[] VDouble, double Radius);

    private sealed record PeriapsisReference(double Mu, double Q, double I, double Node, double Argument,
        IReadOnlyList<SolverPoint> Solver, IReadOnlyList<ReversePoint> Reverse, IReadOnlyList<ReferenceState> States);

    private static PeriapsisReference Load()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ReferenceFile));
        var root = document.RootElement;
        static Exact Pair(JsonElement e) => new(e[0].GetDouble(), e[1].GetDouble());
        static double[] Doubles(JsonElement e) => e.EnumerateArray().Select(c => c.GetDouble()).ToArray();
        var solver = root.GetProperty("solver").EnumerateArray().Select(p => new SolverPoint(p.GetProperty("e").GetDouble(),
            p.GetProperty("M").GetDouble(), Pair(p.GetProperty("E")))).ToList();
        var reverse = root.GetProperty("reverse").EnumerateArray().Select(p => new ReversePoint(p.GetProperty("e").GetDouble(),
            p.GetProperty("E").GetDouble(), Pair(p.GetProperty("M")))).ToList();
        var states = root.GetProperty("states").EnumerateArray().Select(s => new ReferenceState(s.GetProperty("e").GetDouble(),
            s.GetProperty("a").GetDouble(), s.GetProperty("nuDegrees").GetDouble(), s.GetProperty("M").GetDouble(),
            s.GetProperty("r").EnumerateArray().Select(Pair).ToArray(), s.GetProperty("v").EnumerateArray().Select(Pair).ToArray(),
            Doubles(s.GetProperty("rDouble")), Doubles(s.GetProperty("vDouble")), s.GetProperty("radius").GetDouble())).ToList();
        return new PeriapsisReference(root.GetProperty("mu").GetDouble(), root.GetProperty("q").GetDouble(),
            root.GetProperty("i").GetDouble(), root.GetProperty("node").GetDouble(), root.GetProperty("argument").GetDouble(),
            solver, reverse, states);
    }
}
