// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// Kepler's equation from the mean anomaly (#453): <see cref="KeplerEquation"/> against the root of the same inputs at
/// 60 digits, on the ellipse and the hyperbola, at the edges of both branches, below one turn and beyond it, and
/// through the public paths that solve it: the eccentric and true anomalies, the true longitude and the state of
/// <see cref="KeplerianElements"/> given by M, at their epoch through <c>AtEpoch</c>, the first state of a propagation
/// by RK7(8) and by Velocity-Verlet, and the eccentric anomaly of a <see cref="StateVector"/> and of
/// <see cref="EquinoctialElements"/>; its exact points, its guard, its cap and the mean anomalies too large for one
/// reduction.
/// </summary>
/// <remarks>
/// <para>
/// The reference, <c>Data/KeplerEquationReference/kepler_equation_reference.json</c>, comes from
/// <c>tools/kepler_equation/kepler_equation_reference.py</c>: the root of Kepler's equation for the double inputs, by
/// Newton's method in mpmath at 60 digits checked on its residual (below 1e-55), its true anomaly, and three floors of
/// the computation in doubles: the issue's, ε(|E| + e|sin E| + |M|) / |1 − e cos E|; the reduced floor F of the
/// equation solved for M − 2πk plus the representation of the full E, which an error of the reduction near 2πk does not
/// hide; and the floor of ν, |dν/dE| F + ½ ulp(ν). Then the states of the issue's cases and of its NaN regression at 60
/// digits, and their floors, the root sum of squares of the relative changes of the state for a relative change of
/// 2^-53 of each element and of GM. The inputs are doubles, so the reference is exact for them and measures the
/// computation alone; its values of 30 digits are read as a double and its rest (<see cref="Exact"/>), so that an error
/// is measured without the rounding of the reference.
/// </para>
/// <para>
/// Every threshold is twice the worst over Linux, Windows and macOS arm64 (CI run 40272754260, 2026-10-26), read from
/// the MEASURE #453 lines of the three logs (#453).
/// </para>
/// </remarks>
public class KeplerEquationTests
{
    private const string ReferenceFile = "Data/KeplerEquationReference/kepler_equation_reference.json";
    private const double PeriapsisRadius = 7000000.0;
    private const double Epsilon = 2.220446049250313e-16;

    // 2π − fl(2π)
    private const double TwoPiRest = 2.4492935982947064e-16;

    // Twice the worst over Linux, Windows and macOS arm64 (CI run 40272754260, 2026-10-26) of the three ellipse
    // families, rounded up to two digits (#453): 0.504 of the floor (e = 0.999, on all three), 0.451 of the reduced
    // floor (e = 0.9999, Linux and Windows; 0.414 on macOS arm64) and 2.079 of the floor of ν (M < 0, e = 0.999999,
    // on all three)
    private const double EllipseFloor = 1.1;
    private const double EllipseReducedFloor = 0.91;
    private const double EllipseTrueAnomalyFloor = 4.2;

    // Twice the worst over Linux, Windows and macOS arm64 (CI run 40272754260, 2026-10-26) of the hyperbola family,
    // rounded up to two digits (#453): 2.135 of the floor (e = 1.01, Windows and macOS arm64; 2.103 on Linux), 0.678 of
    // the reduced floor (e = 1.000002, on all three) and 2.901 of the floor of ν (e = 10, Windows; 2.837 on Linux and
    // macOS arm64)
    private const double HyperbolaFloor = 4.3;
    private const double HyperbolaReducedFloor = 1.4;
    private const double HyperbolaTrueAnomalyFloor = 5.9;

    // Twice the most iterations of every family over Linux, Windows and macOS arm64 (CI run 40272754260, 2026-10-26):
    // 9 on all three (#453; 71447 at e = 0.999999 before #453)
    private const int Iterations = 18;

    // Twice the worst over Linux, Windows and macOS arm64 (CI run 40272754260, 2026-10-26) of the error of the states
    // against the state at 60 digits, rounded up to two digits (#453): 1.652 of the floor of the position and 1.677 of
    // that of the velocity on all three, at the NaN regression, whose p = a (1 − e²) loses 4.1e-13
    private const double StatePositionFloor = 3.4;
    private const double StateVelocityFloor = 3.4;

    // Twice the worst over Linux, Windows and macOS arm64 (CI run 40272754260, 2026-10-26) of the difference of the
    // states and SPICE CONICS where CONICS resolves them, rounded up to two digits (#453): 0.841 of the floor of the
    // position and 0.863 of that of the velocity, at e = 0.99, on Linux and Windows (0.762 and 0.703 on macOS arm64)
    private const double ConicsPositionFloor = 1.7;
    private const double ConicsVelocityFloor = 1.8;

    // 1 − 1e-6, the largest e of OrbitalParameters.IsElliptical, at which the constructor of KeplerianElements keeps M
    // as it is (it normalizes M below)
    private const double LargestEllipticEccentricity = 0.999999;

    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0, frame: TimeFrame.TDBFrame);

    private static readonly Lazy<KeplerReference> Reference = new(Load);

    private readonly ITestOutputHelper _output;

    public KeplerEquationTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void Ellipse_AgainstTheRootAt60Digits()
    {
        // Arrange: e of the issue and the edges 0, 1e-12 and 1e-3 of the branch (0.999999 is its largest e), M = m and
        // fl(2π) − m, which KeplerianElements keeps as they are, and down to fl(2π) itself, where the reduction by one
        // turn is singular (KeplerianElements gives it for a negative M of 1e-17)
        var earth = Earth();
        var points = Points("ellipse");

        // Act: the internal solver, and KeplerianElements given by M
        var solved = points.Select(p => Solve(p, earth)).ToList();

        // Assert: E bit for bit through both paths, and ν from the published TrueAnomaly()
        Assert.Equal(4631, points.Count);
        Assert.All(solved, s => Assert.True(s.ThroughTheElements, $"e = {s.Point.Eccentricity}, M = {s.Point.M:R}"));
        Assert.Equal(11, solved.Count(s => s.Point.M == IO.Astrodynamics.Constants._2PI));
        AssertWithin("ellipse", solved, EllipseFloor, EllipseReducedFloor, EllipseTrueAnomalyFloor);
    }

    [Fact]
    public void Ellipse_NegativeMeanAnomaly_AgainstTheRootAt60Digits()
    {
        // Arrange: M = −m and −(fl(2π) − m), negative mean anomalies, that of a StateVector before the periapsis among
        // them (KeplerianElements normalizes them, but at e = 1 − 1e-6 exactly)
        var earth = Earth();
        var points = Points("negative");

        // Act
        var solved = points.Select(p => Solve(p, earth)).ToList();

        // Assert: the root of the sign of M, ν as TrueAnomaly() computes it from E
        Assert.Equal(3971, points.Count);
        AssertThroughTheElementsAtTheLargestEccentricity(points, solved);
        Assert.All(solved, s => Assert.True(s.Anomaly < 0.0, $"e = {s.Point.Eccentricity}, M = {s.Point.M:R}"));
        AssertWithin("ellipse, M < 0", solved, EllipseFloor, EllipseReducedFloor, EllipseTrueAnomalyFloor);
    }

    [Fact]
    public void Ellipse_BeyondOneTurn_AgainstTheRootAt60Digits()
    {
        // Arrange: M = ±(k fl(2π) ± d), k = 2, 10, 11 and 1592, d from 1e-15 to π: reached by internal calls, and by
        // KeplerianElements at e = 1 − 1e-6 exactly, whose M its constructor keeps
        var earth = Earth();
        var points = Points("turns");

        // Act
        var solved = points.Select(p => Solve(p, earth)).ToList();

        // Assert: the root within e of M, as the loop before #453 found it from E = M, and to the floor of the reduced
        // equation (the reduction by 2π in two constants is exact for |M| < 2.6e7 rad)
        Assert.Equal(2016, points.Count);
        AssertThroughTheElementsAtTheLargestEccentricity(points, solved);
        Assert.All(solved, s => Assert.True(System.Math.Abs(s.Anomaly - s.Point.M) <=
                                            s.Point.Eccentricity + 4.0 * Epsilon * System.Math.Abs(s.Point.M),
            $"e = {s.Point.Eccentricity}, M = {s.Point.M:R}: E = {s.Anomaly:R}"));
        AssertWithin("ellipse beyond one turn", solved, EllipseFloor, EllipseReducedFloor, EllipseTrueAnomalyFloor);
    }

    [Fact]
    public void Hyperbola_AgainstTheRootAt60Digits()
    {
        // Arrange: e of the issue and the edges 1 + 2e-6 and 100, M = ±m from 1e-9 to 1e4
        var earth = Earth();
        var points = Points("hyperbola");

        // Act: the internal solver, and KeplerianElements given by M, which keeps it on an open orbit
        var solved = points.Select(p => Solve(p, earth)).ToList();

        // Assert: E bit for bit through both paths, of the sign of M, and ν from the published TrueAnomaly()
        Assert.Equal(3216, points.Count);
        Assert.All(solved, s => Assert.True(s.ThroughTheElements, $"e = {s.Point.Eccentricity}, M = {s.Point.M:R}"));
        Assert.All(solved, s => Assert.Equal(System.Math.Sign(s.Point.M), System.Math.Sign(s.Anomaly)));
        AssertWithin("hyperbola", solved, HyperbolaFloor, HyperbolaReducedFloor, HyperbolaTrueAnomalyFloor);
    }

    [Fact]
    public void States_FromTheMeanAnomaly_AgainstTheStateAt60Digits()
    {
        // Arrange: the issue's cases and its NaN regression, the Earth of the test kernels
        var earth = Earth();
        Assert.Equal(Reference.Value.Gm, earth.GM);
        var states = Reference.Value.States;

        // Act: the state of the elements given by M; with ν given, then at their own epoch (AtEpoch builds the
        // elements from M alone); at their epoch; and the first state of a propagation from the elements with ν given,
        // by RK7(8) and by Velocity-Verlet (both go through AtEpoch)
        double worstPosition = 0.0, worstVelocity = 0.0;
        foreach (var c in states)
        {
            var state = FromMeanAnomaly(c, earth).ToStateVector();
            var paths = new (string Path, Vector3 Position, Vector3 Velocity)[]
            {
                Path("AtEpoch", FromMeanAnomaly(c, earth, c.Nu).AtEpoch(Epoch).ToStateVector()),
                Path("ToStateVector(epoch)", FromMeanAnomaly(c, earth).ToStateVector(Epoch)),
                FirstState("RK7(8)", FromMeanAnomaly(c, earth, c.Nu), true),
                FirstState("Velocity-Verlet", FromMeanAnomaly(c, earth, c.Nu), false)
            };
            var (position, velocity) = RelativeErrors(state, c);
            worstPosition = System.Math.Max(worstPosition, position / c.PositionFloor);
            worstVelocity = System.Math.Max(worstVelocity, velocity / c.VelocityFloor);
            _output.WriteLine($"MEASURE #453 state {c.Name}: position {position / c.PositionFloor:F3}, velocity " +
                              $"{velocity / c.VelocityFloor:F3} of the state floor ({position:E2} and {velocity:E2} " +
                              "relative)");

            // Assert: every path the same state bit for bit, and TrueAnomaly(epoch) and TrueLongitude(epoch) the
            // TrueAnomaly() and TrueLongitude() of the elements
            foreach (var (path, p, v) in paths)
            {
                Assert.True(state.Position == p && state.Velocity == v, $"{c.Name}: {path} differs");
            }

            Assert.Equal(FromMeanAnomaly(c, earth).TrueAnomaly(), FromMeanAnomaly(c, earth).TrueAnomaly(Epoch));
            Assert.Equal(FromMeanAnomaly(c, earth).TrueLongitude(), FromMeanAnomaly(c, earth).TrueLongitude(Epoch));
        }

        Assert.Equal(10, states.Count);
        _output.WriteLine($"MEASURE #453 states, worst: position {worstPosition:F3}, velocity {worstVelocity:F3} of " +
                          "the state floor");
        Assert.True(worstPosition < StatePositionFloor, $"position {worstPosition:F3} of the floor");
        Assert.True(worstVelocity < StateVelocityFloor, $"velocity {worstVelocity:F3} of the floor");
    }

    [Fact]
    public void States_FromTheMeanAnomaly_AgainstSpiceConics()
    {
        // Arrange: the states where SPICE CONICS resolves the state at 60 digits (at e = 0.1 and 0.9 it is 3 to 14
        // floors away, and at e = 0.999, ν = 330° 0.32 m)
        var earth = Earth();
        var states = Reference.Value.States.Where(c => c.SpiceConics).ToList();

        // Act
        double worstPosition = 0.0, worstVelocity = 0.0;
        foreach (var c in states)
        {
            var elements = FromMeanAnomaly(c, earth);
            var state = elements.ToStateVector();
            var conics = SpiceAPI.Instance.ConvertConicElementsToStateVector(elements, Epoch, earth.GM);
            double position = (state.Position - conics.Position).Magnitude() / conics.Position.Magnitude() /
                              c.PositionFloor;
            double velocity = (state.Velocity - conics.Velocity).Magnitude() / conics.Velocity.Magnitude() /
                              c.VelocityFloor;
            var (conicsPosition, conicsVelocity) = RelativeErrors(conics, c);
            worstPosition = System.Math.Max(worstPosition, position);
            worstVelocity = System.Math.Max(worstVelocity, velocity);
            _output.WriteLine($"MEASURE #453 state {c.Name} against SPICE CONICS: position {position:F3}, velocity " +
                              $"{velocity:F3} of the state floor; CONICS against 60 digits " +
                              $"{conicsPosition / c.PositionFloor:F3} and {conicsVelocity / c.VelocityFloor:F3}");

            // Assert: the reference resolves what it measures, within a quarter of the floor of the state at 60 digits
            // (0.06 at most, 2026-10-25)
            Assert.True(conicsPosition < 0.25 * c.PositionFloor && conicsVelocity < 0.25 * c.VelocityFloor,
                $"{c.Name}: SPICE CONICS does not resolve the state");
        }

        // Assert
        Assert.Equal(6, states.Count);
        Assert.True(worstPosition < ConicsPositionFloor, $"position {worstPosition:F3} of the floor");
        Assert.True(worstVelocity < ConicsVelocityFloor, $"velocity {worstVelocity:F3} of the floor");
    }

    [Fact]
    public async Task NanRegression_GivesTheRootAndAFiniteState()
    {
        // Arrange: before #453, Newton's method from E = M left the convex region (E1 = 119.3, E2 = −24975.8, ...),
        // overflowed and returned NaN
        var earth = Earth();
        var c = Reference.Value.States.Single(s => s.Name.StartsWith("NaN regression"));
        var elements = FromMeanAnomaly(c, earth);

        // Act: a regression that does not return fails after 10 s instead of hanging the run
        var task = Task.Run(() => (elements.EccentricAnomaly(), elements.ToStateVector()));
        Assert.True(await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10.0))) == task,
            "Kepler's equation did not return");
        var (anomaly, state) = await task;

        // Assert: the root, and a finite state at the floor
        double error = System.Math.Abs(c.Anomaly.ErrorOf(anomaly)) / c.Floor;
        var (position, velocity) = RelativeErrors(state, c);
        _output.WriteLine($"MEASURE #453 NaN regression: E {error:F3} of the floor; state " +
                          $"{position / c.PositionFloor:F3} and {velocity / c.VelocityFloor:F3} of the state floor");
        Assert.True(double.IsFinite(state.Position.Magnitude()) && double.IsFinite(state.Velocity.Magnitude()));
        Assert.True(error < EllipseFloor, $"E {error:F3} of the floor");
        Assert.True(position / c.PositionFloor < StatePositionFloor, $"position {position / c.PositionFloor:F3}");
        Assert.True(velocity / c.VelocityFloor < StateVelocityFloor, $"velocity {velocity / c.VelocityFloor:F3}");
    }

    [Fact]
    public void StateVector_EccentricAnomalyOfEitherSign()
    {
        // Arrange: states of ellipses of e from 0.1 to 0.99999 at ν = ±0.01° to ±179.9° (at e = 0.999999 a state can
        // come back with e in the parabolic band) and of hyperbolas of e = 1.01, 1.5 and 3 at ν = ±0.01° to ±100°,
        // whose closed-form mean anomaly is negative before the periapsis
        var earth = Earth();
        var states = new List<StateVector>();
        var conics = new (double[] Eccentricities, double[] Degrees)[]
        {
            ([0.1, 0.5, 0.9, 0.99, 0.999, 0.9999, 0.99999], [0.01, 1.0, 5.0, 30.0, 90.0, 150.0, 179.0, 179.9]),
            ([1.01, 1.5, 3.0], [0.01, 1.0, 5.0, 30.0, 60.0, 90.0, 100.0])
        };
        foreach (var (eccentricities, degrees) in conics)
        foreach (double e in eccentricities)
        foreach (double d in degrees)
        foreach (double sign in new[] { 1.0, -1.0 })
        {
            double nu = sign * d * IO.Astrodynamics.Constants.Deg2Rad;
            var s = Elements(e, 0.0, earth, nu < 0.0 ? nu + IO.Astrodynamics.Constants._2PI : nu).ToStateVector();
            states.Add(new StateVector(s.Position, s.Velocity, earth, Epoch, Frames.Frame.ICRF));
        }

        // Act and assert: the eccentric anomaly of the state, of its equinoctial elements and of these 10 min later
        // (AtEpoch builds them from M alone) is the internal solver on their mean anomaly and eccentricity bit for bit,
        // of the sign of M; the precision of the solver for M of either sign is measured against 60 digits by the
        // families of points (the closed form E(ν) of the same state is no reference: it is farther from the root than
        // the solver, 0.36 of the floor against 0.28, #453)
        Assert.Equal(154, states.Count);
        Assert.Equal(77, states.Count(sv => sv.MeanAnomaly() < 0.0));
        Assert.Equal(42, states.Count(sv => sv.IsHyperbolic()));
        foreach (var sv in states)
        {
            var equinoctial = sv.ToEquinoctial();
            AssertSolvedByKeplerEquation(sv);
            AssertSolvedByKeplerEquation(equinoctial);
            AssertSolvedByKeplerEquation(equinoctial.AtEpoch(Epoch.AddSeconds(600.0)));
        }
    }

    // The eccentric anomaly of an ellipse or a hyperbola given by M: the internal solver on its M and e bit for bit, of
    // the sign of M
    private static void AssertSolvedByKeplerEquation(Astrodynamics.OrbitalParameters.OrbitalParameters parameters)
    {
        double m = parameters.MeanAnomaly(), e = parameters.Eccentricity();
        string inputs = FormattableString.Invariant($"{parameters.GetType().Name}, e = {e:R}, M = {m:R}");
        Assert.True(parameters.IsElliptical() || parameters.IsHyperbolic(), inputs);
        double expected = parameters.IsElliptical()
            ? KeplerEquation.EllipticAnomaly(m, e)
            : KeplerEquation.HyperbolicAnomaly(m, e);
        Assert.Equal(expected, parameters.EccentricAnomaly());
        Assert.True(System.Math.Sign(m) == System.Math.Sign(expected), inputs);
    }

    public static TheoryData<double, double> NonFiniteInputs => new()
    {
        { double.NaN, 0.5 }, { double.PositiveInfinity, 0.5 }, { double.NegativeInfinity, 0.5 }, { 1.0, double.NaN },
        { double.NaN, 1.5 }, { double.PositiveInfinity, 1.5 }, { double.NegativeInfinity, 1.5 },
        { 1.0, double.PositiveInfinity }
    };

    [Theory]
    [MemberData(nameof(NonFiniteInputs))]
    public void NonFiniteInput_Throws(double meanAnomaly, double eccentricity)
    {
        // Arrange: the ellipse below e = 1, the hyperbola otherwise, as OrbitalParameters dispatches (a NaN e is
        // neither elliptical nor parabolic)
        Func<double> solve = eccentricity < 1.0
            ? () => KeplerEquation.EllipticAnomaly(meanAnomaly, eccentricity)
            : () => KeplerEquation.HyperbolicAnomaly(meanAnomaly, eccentricity);
        var elements = Elements(eccentricity, meanAnomaly, Earth());

        // Act and assert: InvalidOperationException naming e and M, where NaN was returned before #453
        // (KeplerianElements normalizes an infinite M of an ellipse to NaN); at the epoch, through AtEpoch, whose M is
        // NaN at e = ∞ (0 s times an infinite mean motion), and from the start of a propagation by RK7(8) (which failed
        // after 100 rejected steps) and by Velocity-Verlet (which propagated NaN)
        AssertGuard(() => solve(), eccentricity, meanAnomaly);
        AssertGuard(() => elements.EccentricAnomaly(), eccentricity, elements.M);
        if (!double.IsNaN(eccentricity))
        {
            double atEpoch = elements.AtEpoch(Epoch).MeanAnomaly();
            AssertGuard(() => elements.TrueAnomaly(), eccentricity, elements.M);
            AssertGuard(() => elements.TrueLongitude(), eccentricity, elements.M);
            AssertGuard(() => elements.ToStateVector(), eccentricity, elements.M);
            AssertGuard(() => elements.ToStateVector(Epoch), eccentricity, atEpoch);
            AssertGuard(() => elements.TrueAnomaly(Epoch), eccentricity, atEpoch);
            AssertGuard(() => elements.TrueLongitude(Epoch), eccentricity, atEpoch);
            AssertGuard(() => Propagator(elements, true), eccentricity, atEpoch);
            AssertGuard(() => Propagator(elements, false), eccentricity, atEpoch);
        }
    }

    private static void AssertGuard(Func<object> call, double eccentricity, double meanAnomaly)
    {
        var exception = Assert.Throws<InvalidOperationException>(call);
        string inputs = FormattableString.Invariant($"e = {eccentricity:R}, M = {meanAnomaly:R} rad (#453).");
        Assert.Equal("Kepler's equation needs a finite mean anomaly and eccentricity: " + inputs, exception.Message);
    }

    [Theory]
    [InlineData(1e308)]
    [InlineData(-1e308)]
    public void NonFiniteIterate_Throws(double meanAnomaly)
    {
        // Act: at e = 1.5, 6m and m / (e − 1) overflow, so the start of the hyperbola is infinite (it is finite up to
        // max(3.0e307, 1.8e308 (e − 1)) rad; before #453 a finite H was returned)
        var exception =
            Assert.Throws<InvalidOperationException>(() => KeplerEquation.HyperbolicAnomaly(meanAnomaly, 1.5));

        // Assert
        Assert.Contains("reaches a non-finite iterate", exception.Message);
        Assert.True(double.IsFinite(KeplerEquation.HyperbolicAnomaly(System.Math.CopySign(1e300, meanAnomaly), 1.5)));
    }

    public static TheoryData<double, double> HugeMeanAnomalies => new()
    {
        { 77918711979062.66, 0.5 }, { -77918711979062.66, 0.999999 }, { 2.733793975257921e16, 0.999999 },
        { -2.733793975257921e16, 0.999999 }, { 1e18, 0.99 }, { 1e300, 0.9 }, { double.MaxValue, 0.99 },
        { -double.MaxValue, 0.999999 }
    };

    [Theory]
    [MemberData(nameof(HugeMeanAnomalies))]
    public void Ellipse_HugeMeanAnomaly_IsWithinEOfIt(double meanAnomaly, double eccentricity)
    {
        // Arrange: the reduced anomaly of 7.79e13 rad is 3.1459532064286577 in absolute value, above π, so Newton's
        // method rises to the root from π; from about 1.2e16 rad it can exceed 2π and is reduced again (before, the
        // solver did not converge in 50 iterations, from 2.4e16 rad at e = 0.999999); at e = 1 − 1e-6 exactly
        // KeplerianElements keeps M as it is
        var elements = Elements(eccentricity, meanAnomaly, Earth());

        // Act
        double anomaly = KeplerEquation.EllipticAnomaly(meanAnomaly, eccentricity);
        int iterations = IterationsOf(cap => KeplerEquation.EllipticAnomaly(meanAnomaly, eccentricity, cap), anomaly);
        _output.WriteLine($"MEASURE #453 ellipse, e = {eccentricity:R}, M = {meanAnomaly:R}: {iterations} iterations");

        // Assert: a finite E within e of M, as the loop before #453 returned E = M there, of the sign of M, and the
        // same E bit for bit through the elements, with a finite state
        Assert.True(double.IsFinite(anomaly));
        Assert.True(System.Math.Abs(anomaly - meanAnomaly) <=
                    eccentricity + 4.0 * Epsilon * System.Math.Abs(meanAnomaly), $"E = {anomaly:R}");
        Assert.Equal(System.Math.Sign(meanAnomaly), System.Math.Sign(anomaly));
        Assert.InRange(iterations, 1, Iterations);
        Assert.Equal(eccentricity == LargestEllipticEccentricity, elements.M == meanAnomaly);
        if (elements.M == meanAnomaly)
        {
            Assert.Equal(anomaly, elements.EccentricAnomaly());
            Assert.True(double.IsFinite(elements.ToStateVector().Position.Magnitude()));
        }
    }

    public static TheoryData<double, double> CapCases => new() { { 1.0, 0.9 }, { 1e-3, 0.999999 }, { 10.0, 1.5 } };

    [Theory]
    [MemberData(nameof(CapCases))]
    public void Cap_Throws_WithTheIterationCount(double meanAnomaly, double eccentricity)
    {
        // Arrange
        bool hyperbolic = eccentricity > 1.0;
        double Solve(int cap) => hyperbolic
            ? KeplerEquation.HyperbolicAnomaly(meanAnomaly, eccentricity, cap)
            : KeplerEquation.EllipticAnomaly(meanAnomaly, eccentricity, cap);
        double expected = hyperbolic
            ? KeplerEquation.HyperbolicAnomaly(meanAnomaly, eccentricity)
            : KeplerEquation.EllipticAnomaly(meanAnomaly, eccentricity);

        // Act
        int iterations = IterationsOf(Solve, expected);
        _output.WriteLine($"MEASURE #453 cap, e = {eccentricity:R}, M = {meanAnomaly:R}: {iterations} iterations");

        // Assert: every cap below the iterations throws, naming them; the cap of the iterations, and the default, give
        // the same E bit for bit
        Assert.InRange(iterations, 2, Iterations);
        for (int cap = 1; cap < iterations; cap++)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => Solve(cap));
            Assert.Contains($"does not converge in {cap} iterations", exception.Message);
        }

        Assert.Equal(expected, Solve(iterations));
        Assert.Equal(expected, Solve(KeplerEquation.MaximumIterations));
    }

    [Fact]
    public void ExactPoints()
    {
        // Arrange
        var earth = Earth();
        double[] eccentricities = [0.0, 1e-12, 1e-3, 0.1, 0.5, 0.9, 0.99, 0.999, 0.9999, 0.99999, 0.999999];
        double belowTwoPi = System.Math.BitDecrement(IO.Astrodynamics.Constants._2PI);

        // Act and assert: M = ±π gives ±π for every e: it is not reduced, its start is fl(π) and the first step is
        // below ½ ulp(π)
        foreach (double e in eccentricities)
        {
            Assert.Equal(System.Math.PI, KeplerEquation.EllipticAnomaly(System.Math.PI, e));
            Assert.Equal(-System.Math.PI, KeplerEquation.EllipticAnomaly(-System.Math.PI, e));
            Assert.Equal(System.Math.PI, Elements(e, System.Math.PI, earth).EccentricAnomaly());
        }

        // e = 0 gives M for any M: its start is m and its step 0, and the turns are restored through M
        foreach (double m in new[] { 0.3, 3.0, 4.0, belowTwoPi, 7.0, 100.0, 1e4, -1e4 })
        {
            Assert.Equal(m, KeplerEquation.EllipticAnomaly(m, 0.0));
            if (m is >= 0.0 and < IO.Astrodynamics.Constants._2PI)
            {
                Assert.Equal(m, Elements(0.0, m, earth).EccentricAnomaly());
            }
        }

        // M = ±0 gives ±0 on both branches: every start is 0, and so is the first step
        foreach (double zero in new[] { 0.0, -0.0 })
        {
            foreach (double e in new[] { 0.0, 0.5, 0.999999 })
            {
                AssertSignedZero(zero, KeplerEquation.EllipticAnomaly(zero, e));
            }

            foreach (double e in new[] { 1.000002, 1.5, 100.0 })
            {
                AssertSignedZero(zero, KeplerEquation.HyperbolicAnomaly(zero, e));
            }
        }
    }

    private static void AssertSignedZero(double zero, double value)
    {
        Assert.Equal(0.0, value);
        Assert.Equal(double.IsNegative(zero), double.IsNegative(value));
    }

    // The points of the internal solver: E, ν as TrueAnomaly() computes it from E, the iterations, and whether
    // KeplerianElements given by M reach the point too
    private sealed record Solved(ReferencePoint Point, double Anomaly, double TrueAnomaly, int Iterations,
        bool ThroughTheElements = false);

    // The internal solver and, where they reach the point, KeplerianElements given by M: the same E bit for bit, and ν
    // from TrueAnomaly()
    private static Solved Solve(ReferencePoint p, CelestialBody earth)
    {
        var direct = Solve(p);
        var elements = ElementsOf(p, earth);
        if (elements is null)
        {
            return direct;
        }

        Assert.Equal(direct.Anomaly, elements.EccentricAnomaly());
        if (p.Eccentricity < 1.0)
        {
            Assert.Equal(direct.TrueAnomaly, elements.TrueAnomaly());
        }

        return direct with { TrueAnomaly = elements.TrueAnomaly(), ThroughTheElements = true };
    }

    // KeplerianElements whose M is that of the point, or null: their constructor keeps M on an open orbit, in [0, 2π)
    // and at e = 1 − 1e-6 exactly, normalizes it otherwise, fl(2π) to 0, and gives fl(2π) itself for a negative M of
    // 1e-17 (SpecialFunctions.NormalizeAngle, #453)
    private static KeplerianElements ElementsOf(ReferencePoint p, CelestialBody earth)
    {
        double[] given = p.M == IO.Astrodynamics.Constants._2PI ? [p.M, -1e-17] : [p.M];
        return given.Select(m => Elements(p.Eccentricity, m, earth)).FirstOrDefault(elements => elements.M == p.M);
    }

    // The points at e = 1 − 1e-6, and those alone, reached through KeplerianElements, whose constructor keeps their M
    private static void AssertThroughTheElementsAtTheLargestEccentricity(IReadOnlyList<ReferencePoint> points,
        IReadOnlyList<Solved> solved)
    {
        int largest = points.Count(p => p.Eccentricity == LargestEllipticEccentricity);
        Assert.InRange(largest, 1, points.Count - 1);
        Assert.All(solved, s =>
            Assert.Equal(s.Point.Eccentricity == LargestEllipticEccentricity, s.ThroughTheElements));
    }

    private static Solved Solve(ReferencePoint p)
    {
        bool hyperbolic = p.Eccentricity > 1.0;
        double anomaly = hyperbolic
            ? KeplerEquation.HyperbolicAnomaly(p.M, p.Eccentricity)
            : KeplerEquation.EllipticAnomaly(p.M, p.Eccentricity);
        int iterations = IterationsOf(cap => hyperbolic
            ? KeplerEquation.HyperbolicAnomaly(p.M, p.Eccentricity, cap)
            : KeplerEquation.EllipticAnomaly(p.M, p.Eccentricity, cap), anomaly);
        return new Solved(p, anomaly, hyperbolic ? double.NaN : TrueAnomalyOf(anomaly, p.Eccentricity), iterations);
    }

    // The iterations of a solve: the smallest cap of the internal overload that converges, whose E is then the default
    // one bit for bit
    private static int IterationsOf(Func<int, double> solve, double expected)
    {
        for (int cap = 1; cap <= KeplerEquation.MaximumIterations; cap++)
        {
            double anomaly;
            try
            {
                anomaly = solve(cap);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            Assert.Equal(expected, anomaly);
            return cap;
        }

        return int.MaxValue;
    }

    // The errors of E and ν of a family, published per e, and their worst against the thresholds
    private void AssertWithin(string family, IReadOnlyList<Solved> solved, double floor, double reducedFloor,
        double trueAnomalyFloor)
    {
        (double Floor, double Reduced, double TrueAnomaly, int Iterations) Worst(IEnumerable<Solved> group)
        {
            var errors = group.Select(s => (Anomaly: System.Math.Abs(s.Point.Anomaly.ErrorOf(s.Anomaly)),
                TrueAnomaly: System.Math.Abs(AngleError(s.TrueAnomaly, s.Point.TrueAnomaly)), s.Point,
                s.Iterations)).ToList();
            return (errors.Max(x => x.Anomaly / x.Point.Floor), errors.Max(x => x.Anomaly / x.Point.ReducedFloor),
                errors.Max(x => x.TrueAnomaly / x.Point.TrueAnomalyFloor), errors.Max(x => x.Iterations));
        }

        Assert.All(solved, s => Assert.True(double.IsFinite(s.Anomaly) && double.IsFinite(s.TrueAnomaly),
            $"e = {s.Point.Eccentricity}, M = {s.Point.M:R}"));
        foreach (var group in solved.GroupBy(s => s.Point.Eccentricity))
        {
            var w = Worst(group);
            string e = group.Key.ToString("R", CultureInfo.InvariantCulture);
            _output.WriteLine($"MEASURE #453 {family} e = {e}: E {w.Floor:F3} of the floor, {w.Reduced:F3} of the " +
                              $"reduced floor, ν {w.TrueAnomaly:F3} of its floor, {w.Iterations} iterations at most " +
                              $"({group.Count()} points)");
        }

        var worst = Worst(solved);
        _output.WriteLine($"MEASURE #453 {family}, worst: E {worst.Floor:F3} of the floor, {worst.Reduced:F3} of " +
                          $"the reduced floor, ν {worst.TrueAnomaly:F3} of its floor, {worst.Iterations} iterations " +
                          $"({solved.Count} points)");
        Assert.True(worst.Floor < floor, $"{family}: E {worst.Floor:F3} of the floor");
        Assert.True(worst.Reduced < reducedFloor, $"{family}: E {worst.Reduced:F3} of the reduced floor");
        Assert.True(worst.TrueAnomaly < trueAnomalyFloor, $"{family}: ν {worst.TrueAnomaly:F3} of its floor");
        Assert.True(worst.Iterations <= Iterations, $"{family}: {worst.Iterations} iterations");
    }

    // ν from E as EllipticalTrueAnomaly and TrueAnomaly() compute it (OrbitalParameters), for the mean anomalies that
    // KeplerianElements normalizes
    private static double TrueAnomalyOf(double anomaly, double e)
    {
        double nu = 2 * System.Math.Atan(System.Math.Sqrt((1 + e) / (1 - e)) * System.Math.Tan(anomaly / 2)) %
                    IO.Astrodynamics.Constants._2PI;
        return nu < 0 ? nu + IO.Astrodynamics.Constants._2PI : nu;
    }

    // The error of an angle in [0, 2π) against an exact one, across 0 = 2π: x − fl(2π) is exact for x ≥ π
    // (Sterbenz's lemma, cited in KeplerEquation)
    private static double AngleError(double angle, Exact exact)
    {
        if (double.IsNaN(angle))
        {
            return angle;
        }

        if (angle - exact.High > System.Math.PI)
        {
            return ((angle - IO.Astrodynamics.Constants._2PI) - exact.High) - (TwoPiRest + exact.Low);
        }

        if (exact.High - angle > System.Math.PI)
        {
            return (angle - (exact.High - IO.Astrodynamics.Constants._2PI)) - (exact.Low - TwoPiRest);
        }

        return exact.ErrorOf(angle);
    }

    private static CelestialBody Earth() => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);

    // The elements of a periapsis at 7000 km given by M (an ellipse at e = 0 of radius 7000 km)
    private static KeplerianElements Elements(double e, double meanAnomaly, CelestialBody earth,
        double? trueAnomaly = null)
    {
        return new KeplerianElements(PeriapsisRadius / (1.0 - e), e, 0.7, 0.5, 0.9, meanAnomaly, earth, Epoch,
            Frames.Frame.ICRF, trueAnomaly: trueAnomaly);
    }

    private static KeplerianElements FromMeanAnomaly(ReferenceState c, CelestialBody earth, double? trueAnomaly = null)
    {
        return new KeplerianElements(c.A, c.E, c.I, c.Raan, c.Aop, c.M, earth, Epoch, Frames.Frame.ICRF,
            trueAnomaly: trueAnomaly);
    }

    private static (string, Vector3, Vector3) Path(string path, StateVector state) =>
        (path, state.Position, state.Velocity);

    // The first state of a 60 s propagation of a spacecraft whose initial orbit is the elements
    private static (string, Vector3, Vector3) FirstState(string path, KeplerianElements elements, bool rk78)
    {
        using var propagator = Propagator(elements, rk78);
        var first = propagator.Propagate().Segments[0].Steps[0];
        return (path, first.StartPosition, first.StartVelocity);
    }

    // A 60 s propagation, by RK7(8) or by Velocity-Verlet, of a spacecraft whose initial orbit is the elements
    private static CentralBodyPropagator Propagator(KeplerianElements elements, bool rk78)
    {
        var spacecraft = new Spacecraft(-1453, "KEPLER1453", 100.0, 1000.0, new Clock("kepler1453", 65536),
            elements);
        var window = new Window(Epoch, Epoch.AddSeconds(60.0));
        var bodies = new CelestialItem[] { (CelestialBody)elements.Observer };
        return rk78
            ? new CentralBodyPropagator(window, spacecraft, new RK78Integrator(1e-12, 1e-12), bodies, false, false,
                TimeSpan.FromSeconds(60.0))
            : new CentralBodyPropagator(window, spacecraft, bodies, false, false, TimeSpan.FromSeconds(10.0));
    }

    // The errors of the position and of the velocity relative to the state at 60 digits
    private static (double Position, double Velocity) RelativeErrors(StateVector state, ReferenceState c)
    {
        static double Relative(Vector3 x, Exact[] exact)
        {
            var error = new Vector3(exact[0].ErrorOf(x.X), exact[1].ErrorOf(x.Y), exact[2].ErrorOf(x.Z));
            return error.Magnitude() / new Vector3(exact[0].High, exact[1].High, exact[2].High).Magnitude();
        }

        return (Relative(state.Position, c.Position), Relative(state.Velocity, c.Velocity));
    }

    /// <summary>
    /// A reference value of 30 digits as the double nearest it and the double nearest the rest, by exact rational
    /// arithmetic, so that the error of a double x is (x − High) − Low: x − High is exact near High (Sterbenz's lemma,
    /// cited in KeplerEquation).
    /// </summary>
    private readonly record struct Exact(double High, double Low)
    {
        internal double ErrorOf(double x) => (x - High) - Low;

        internal static Exact Parse(string digits)
        {
            double high = double.Parse(digits, CultureInfo.InvariantCulture);
            var (n, d) = Rational(digits);
            var (hn, hd) = Rational(high);
            return new Exact(high, (double)(n * hd - hn * d) / (double)(d * hd));
        }

        // A decimal string as n / d
        private static (BigInteger, BigInteger) Rational(string digits)
        {
            int e = digits.IndexOfAny(['e', 'E']);
            int exponent = e < 0 ? 0 : int.Parse(digits[(e + 1)..], CultureInfo.InvariantCulture);
            string mantissa = e < 0 ? digits : digits[..e];
            int point = mantissa.IndexOf('.');
            if (point >= 0)
            {
                exponent -= mantissa.Length - point - 1;
                mantissa = mantissa.Remove(point, 1);
            }

            var n = BigInteger.Parse(mantissa, CultureInfo.InvariantCulture);
            return exponent >= 0
                ? (n * BigInteger.Pow(10, exponent), BigInteger.One)
                : (n, BigInteger.Pow(10, -exponent));
        }

        // A double as n / d, d a power of 2
        private static (BigInteger, BigInteger) Rational(double x)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);
            int biased = (int)((bits >> 52) & 0x7FF);
            long significand = biased == 0 ? bits & 0xFFFFFFFFFFFFFL : (bits & 0xFFFFFFFFFFFFFL) | (1L << 52);
            int exponent = (biased == 0 ? 1 : biased) - 1075;
            var n = new BigInteger(bits < 0 ? -significand : significand);
            return exponent >= 0 ? (n << exponent, BigInteger.One) : (n, BigInteger.One << -exponent);
        }
    }

    private sealed record ReferencePoint(string Family, double Eccentricity, double M, Exact Anomaly,
        Exact TrueAnomaly, double Floor, double ReducedFloor, double TrueAnomalyFloor);

    private sealed record ReferenceState(string Name, double A, double E, double I, double Raan, double Aop, double M,
        double Nu, Exact Anomaly, double Floor, Exact[] Position, Exact[] Velocity, double PositionFloor,
        double VelocityFloor, bool SpiceConics);

    private sealed record KeplerReference(double Gm, IReadOnlyList<ReferenceState> States,
        IReadOnlyList<ReferencePoint> Points);

    private static IReadOnlyList<ReferencePoint> Points(string family)
    {
        return Reference.Value.Points.Where(p => p.Family == family).ToList();
    }

    private static KeplerReference Load()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ReferenceFile));
        var root = document.RootElement;
        static Exact Value(JsonElement e, string name) => Exact.Parse(e.GetProperty(name).GetString()!);
        static Exact[] Vector(JsonElement e, string name) =>
            e.GetProperty(name).EnumerateArray().Select(c => Exact.Parse(c.GetString()!)).ToArray();
        var states = root.GetProperty("states").EnumerateArray().Select(s => new ReferenceState(
            s.GetProperty("name").GetString(), s.GetProperty("a").GetDouble(), s.GetProperty("e").GetDouble(),
            s.GetProperty("i").GetDouble(), s.GetProperty("raan").GetDouble(), s.GetProperty("aop").GetDouble(),
            s.GetProperty("M").GetDouble(), s.GetProperty("nu").GetDouble(), Value(s, "E"),
            s.GetProperty("floor").GetDouble(),
            Vector(s, "position"), Vector(s, "velocity"), s.GetProperty("position_floor").GetDouble(),
            s.GetProperty("velocity_floor").GetDouble(), s.GetProperty("spice_conics").GetBoolean())).ToList();
        var points = root.GetProperty("points").EnumerateArray().Select(p => new ReferencePoint(
            p.GetProperty("family").GetString(), p.GetProperty("e").GetDouble(), p.GetProperty("M").GetDouble(),
            Value(p, "E"), Value(p, "nu"), p.GetProperty("floor").GetDouble(),
            p.GetProperty("reduced_floor").GetDouble(), p.GetProperty("nu_floor").GetDouble())).ToList();
        return new KeplerReference(root.GetProperty("gm").GetDouble(), states, points);
    }
}
