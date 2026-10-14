// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using MathNet.Numerics.LinearAlgebra;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// The propagated covariance (C2 to C6) in a propagation: the checks of P0, P at any epoch against the Keplerian STM,
/// the RTN process noise, the consider parameters, and the execution error of a maneuver.
/// </summary>
public class CovariancePropagationTests : IClassFixture<PartialsTestCases>
{
    private readonly PartialsTestCases _cases;
    private readonly ITestOutputHelper _output;

    public CovariancePropagationTests(PartialsTestCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    private static TimeSystem.Time Start => PartialsTestCases.Epoch;

    private StateVector Leo => PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);

    private CelestialItem[] Bodies => new CelestialItem[] { _cases.Earth, _cases.Moon, _cases.Sun };

    private static VariationalOptions Covariance => new(PropagateCovariance: true);

    [Fact]
    public void WithoutAnInitialCovariance_Throws()
    {
        // Arrange
        var propagator = Propagator(PartialsTestCases.Spacecraft(Leo), Start, Start.AddHours(1.0));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            propagator.EnableVariationalEquations(Covariance));

        // Assert
        Assert.Contains("carry none", exception.Message);
    }

    [Fact]
    public void WithAnInitialCovarianceAtAnotherEpoch_Throws()
    {
        // Arrange: the window starts one minute after the epoch of the initial covariance
        var propagator = Propagator(PartialsTestCases.Spacecraft(WithCovariance(Leo)), Start.AddMinutes(1.0),
            Start.AddHours(1.0));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            propagator.EnableVariationalEquations(Covariance));

        // Assert
        Assert.Contains("does not carry a covariance from another epoch", exception.Message);
    }

    [Fact]
    public void WithAnInvalidInitialCovariance_Throws()
    {
        // Arrange: an asymmetric P0, a P0 with a negative eigenvalue, and a P0 of the wrong size
        var asymmetric = InitialCovariance(Leo);
        asymmetric.Set(0, 1, asymmetric.Get(0, 1) + 1.0);
        var indefinite = new IO.Astrodynamics.Math.Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            indefinite.Set(i, i, 1.0);
        }

        indefinite.Set(0, 1, 2.0);
        indefinite.Set(1, 0, 2.0);

        // A negative velocity variance, −1e-9 m²/s², within 1e-12 of the trace (m²) but not a covariance (H3 review of
        // #384)
        var negativeVelocityVariance = InitialCovariance(Leo);
        negativeVelocityVariance.Set(5, 5, -1e-9);

        // Act and assert
        Assert.Contains("symmetric", EnableWith(asymmetric).Message);
        Assert.Contains("positive semi-definite", EnableWith(indefinite).Message);
        Assert.Contains("positive semi-definite", EnableWith(negativeVelocityVariance).Message);
        Assert.Contains("6×6", EnableWith(new IO.Astrodynamics.Math.Matrix(3, 3)).Message);
    }

    [Fact]
    public void ConsiderParameters_NeedTheCovarianceAndTheirColumnOfPsi()
    {
        // Arrange
        var propagator = Propagator(PartialsTestCases.Spacecraft(WithCovariance(Leo)), Start, Start.AddHours(1.0));

        // Act and assert
        var withoutCovariance = Assert.Throws<ArgumentException>(() => propagator.EnableVariationalEquations(
            new VariationalOptions(ForceParameters.DragCoefficient, ConsiderParameters: new ConsiderParameters(0.1))));
        Assert.Contains("PropagateCovariance", withoutCovariance.Message);
        var withoutColumn = Assert.Throws<ArgumentException>(() => propagator.EnableVariationalEquations(
            new VariationalOptions(PropagateCovariance: true, ConsiderParameters: new ConsiderParameters(0.1))));
        Assert.Contains("column of Ψ", withoutColumn.Message);
    }

    [Fact]
    public void AtTheStart_PIsP0()
    {
        // Arrange
        var state = WithCovariance(Leo);
        var solution = Propagate(state, Start.AddHours(1.0), new VariationalOptions(ForceParameters.DragCoefficient,
            new RtnProcessNoise(1e-12, 1e-11, 1e-12), PropagateCovariance: true,
            ConsiderParameters: new ConsiderParameters(0.1)), true);
        var p = new double[36];
        var extended = new double[49];

        // Act
        solution.EvaluateCovariance(Start, p);
        solution.EvaluateExtendedCovariance(Start, extended);

        // Assert: Φ = I, Ψ = 0 and Q = 0 at the start; P0 symmetrized, as the propagation stores it
        var p0 = Symmetrized(state.Covariance.Value);
        Assert.Equal(p0, p);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                Assert.Equal(p0[6 * i + j], extended[7 * i + j]);
            }

            Assert.Equal(0.0, extended[7 * i + 6]);
        }

        Assert.Equal(0.01, extended[48], 1e-17);
    }

    [Fact]
    public void TwoBody_PMatchesTheCovarianceThroughTheKeplerianStm()
    {
        // Arrange: point-mass Earth, LEO 400 km, at times inside the steps and at the end. The reference is
        // Φ_kep P0 Φ_kepᵀ, with the closed-form Keplerian STM (universal variables, automatic differentiation).
        var state = WithCovariance(Leo);
        var end = Start.AddHours(6.0);
        var solution = PartialsTestCases.Spacecraft(state).Propagate(new TimeSystem.Window(Start, end),
            new CelestialItem[] { _cases.Earth }, new RK78Integrator(1e-12, 1e-12), false, false,
            TimeSpan.FromHours(1.0), Covariance);
        var p0 = CovarianceAssert.ToMathNet(Symmetrized(state.Covariance.Value), 6);

        foreach (double seconds in new[] { 1234.5, 7200.0, 15000.25, 21600.0 })
        {
            // Act
            var epoch = Start.AddSeconds(seconds);
            var p = new double[36];
            solution.EvaluateCovariance(epoch, p);

            // Assert: far within what the conjunction analysis needs (a covariance within 1 %); measured 4.5e-13 to
            // 1.2e-11 (Linux, .NET 10, 2026-10-14)
            var (_, phi) = KeplerianStm.Propagate(_cases.Earth.GM, state.Position, state.Velocity, seconds);
            var phiKep = CovarianceAssert.ToMathNet(phi, 6);
            var expected = CovarianceAssert.ToArray(phiKep * p0 * phiKep.Transpose());
            double error = StmMeasures.WorstBlockError(p, expected);
            _output.WriteLine($"{seconds} s: P against Φ_kep P0 Φ_kepᵀ {error:E2} (worst 3×3 block)");
            Assert.True(error < 1e-8, $"{seconds} s: {error:E2}");
            CovarianceAssert.SymmetricPositiveSemiDefinite(p, 6, $"P at {seconds} s");
        }
    }

    [Fact]
    public void WithNoiseAndConsiderParameters_EveryCovarianceIsSymmetricPositiveSemiDefinite()
    {
        // Arrange: drag and SRP, Cd and Cr as consider parameters, RTN noise; C6 on P, on the extended covariance and on Q,
        // at the output epochs and inside the steps
        var options = new VariationalOptions(ForceParameters.DragCoefficient | ForceParameters.ReflectivityCoefficient,
            new RtnProcessNoise(1e-12, 1e-11, 1e-12), PropagateCovariance: true,
            ConsiderParameters: new ConsiderParameters(0.1, 0.05));
        var solution = Propagate(WithCovariance(Leo), Start.AddHours(3.0), options, true);
        var noNoise = Propagate(WithCovariance(Leo), Start.AddHours(3.0),
            options with { ProcessNoise = null, ConsiderParameters = null }, true);

        foreach (double minutes in new[] { 17.3, 60.0, 101.7, 180.0 })
        {
            // Act
            var epoch = Start.AddMinutes(minutes);
            var p = new double[36];
            var extended = new double[64];
            var y = new double[48];
            var q = new double[21];
            var bare = new double[36];
            solution.EvaluateCovariance(epoch, p);
            solution.EvaluateExtendedCovariance(epoch, extended);
            solution.EvaluateVariational(epoch, y, q);
            noNoise.EvaluateCovariance(epoch, bare);

            // Assert: the noise and the consider parameters add covariance, Q + Ψc Pp Ψcᵀ
            CovarianceAssert.SymmetricPositiveSemiDefinite(p, 6, $"P at {minutes} min");
            CovarianceAssert.SymmetricPositiveSemiDefinite(extended, 8, $"extended covariance at {minutes} min");
            CovarianceAssert.SymmetricPositiveSemiDefinite(CovarianceAssert.Full(q), 6, $"Q at {minutes} min");
            var added = p.Zip(bare, (a, b) => a - b).ToArray();
            var symmetricAdded = CovarianceAssert.ToArray(
                (CovarianceAssert.ToMathNet(added, 6) + CovarianceAssert.ToMathNet(added, 6).Transpose()) / 2.0);
            double ratio = CovarianceAssert.SmallestCorrelationEigenvalueOverTrace(symmetricAdded, 6);
            _output.WriteLine($"{minutes} min: smallest eigenvalue of the correlation matrix of P − P(no noise, " +
                              $"no consider) {ratio:E2} of its trace");
            Assert.True(ratio >= -1e-12, $"P − P(no noise, no consider) at {minutes} min: {ratio:E2}");
            Assert.True(added[0] + added[7] + added[14] > 0.0);
        }
    }

    [Fact]
    public void WithoutTheCovariance_TheTrajectoryIsBitIdentical_AndThereIsNoCovarianceToRead()
    {
        // Arrange: an initial state carrying a covariance, propagated without the variational equations, with Φ only,
        // and with the covariance
        var state = WithCovariance(Leo);

        // Act
        var plain = Propagate(state, Start.AddHours(1.0), null, true);
        var stmOnly = Propagate(state, Start.AddHours(1.0), new VariationalOptions(), true);
        var covariance = Propagate(state, Start.AddHours(1.0),
            new VariationalOptions(ProcessNoise: new RtnProcessNoise(1e-12, 1e-11, 1e-12), PropagateCovariance: true),
            true);

        // Assert
        var steps = plain.Segments.Single().Steps;
        foreach (var other in new[] { stmOnly, covariance })
        {
            var otherSteps = other.Segments.Single().Steps;
            Assert.Equal(steps.Count, otherSteps.Count);
            for (int k = 0; k < steps.Count; k++)
            {
                Assert.Equal(steps[k].EndPosition, otherSteps[k].EndPosition);
                Assert.Equal(steps[k].EndVelocity, otherSteps[k].EndVelocity);
            }
        }

        Assert.Contains("did not propagate the covariance", Assert.Throws<InvalidOperationException>(() =>
            stmOnly.EvaluateCovariance(Start.AddMinutes(30.0), new double[36])).Message);
        Assert.Contains("did not propagate the covariance", Assert.Throws<InvalidOperationException>(() =>
            plain.EvaluateCovariance(Start.AddMinutes(30.0), new double[36])).Message);
        Assert.Null(plain.StateVectors[3].Covariance);
    }

    [Fact]
    public void EvaluateCovariance_OutsideThePropagationOrWithAWrongBuffer_Throws()
    {
        // Arrange
        var solution = Propagate(WithCovariance(Leo), Start.AddHours(1.0), Covariance, false);

        // Act and assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            solution.EvaluateCovariance(Start.AddSeconds(-1.0), new double[36]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            solution.EvaluateExtendedCovariance(Start.AddHours(1.0).AddSeconds(1.0), new double[36]));
        Assert.Equal("covariance", Assert.Throws<ArgumentException>(() =>
            solution.EvaluateCovariance(Start.AddMinutes(10.0), new double[35])).ParamName);
        Assert.Equal("extended", Assert.Throws<ArgumentException>(() =>
            solution.EvaluateExtendedCovariance(Start.AddMinutes(10.0), new double[37])).ParamName);
        Assert.Throws<InvalidOperationException>(() =>
            new PropagationSolution().EvaluateCovariance(Start, new double[36]));
        var q = new double[21];
        solution.EvaluateVariational(Start.AddMinutes(10.0), new double[36], q);
        Assert.All(q, value => Assert.Equal(0.0, value));
        Assert.Equal("q", Assert.Throws<ArgumentException>(() =>
            solution.EvaluateVariational(Start.AddMinutes(10.0), new double[36], Array.Empty<double>())).ParamName);
    }

    [Fact]
    public void EvaluateCovariance_AtAStepBoundary_AllocatesNothing()
    {
        // Arrange
        var solution = Propagate(WithCovariance(Leo), Start.AddHours(1.0),
            new VariationalOptions(ForceParameters.DragCoefficient, new RtnProcessNoise(1e-12, 1e-11, 1e-12),
                PropagateCovariance: true, ConsiderParameters: new ConsiderParameters(0.1)), true);
        var segment = solution.Segments.Single();
        var boundary = Start.AddSeconds(segment.Steps[3].CumulativeTime);
        var p = new double[36];
        var extended = new double[49];
        solution.EvaluateCovariance(boundary, p);
        solution.EvaluateExtendedCovariance(boundary, extended);

        // Act
        long before = GC.GetAllocatedBytesForCurrentThread();
        solution.EvaluateCovariance(boundary, p);
        solution.EvaluateExtendedCovariance(boundary, extended);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void LyapunovStepWithRtnNoise_AllocatesNothing()
    {
        // Arrange: a step of the variational equations with the RTN noise, after a first one
        var forces = new List<ForceBase> { new GravitationalAcceleration(_cases.Earth) };
        var equations = new VariationalEquations(new VariationalOptions(ProcessNoise:
            new RtnProcessNoise(1e-12, 1e-11, 1e-12)));
        var context = new ForceEvaluationContext(100.0, 2.2, 1.5);
        var stepper = new RK78Stepper(forces) { Context = context };
        stepper.Reset(_cases.Earth, Frames.Frame.ICRF, Start);
        stepper.Step(Leo.Position, Leo.Velocity, Start, 0.0, 30.0, out _, out _, out _, out _);
        var y0 = VariationalTestDriver.Identity(equations);
        var q0 = new double[21];
        var y1 = new double[36];
        var q1 = new double[21];
        equations.Step(stepper.StageStates, forces, context, 30.0, y0, q0, y1, q1);

        // Act
        long before = GC.GetAllocatedBytesForCurrentThread();
        equations.Step(stepper.StageStates, forces, context, 30.0, y1, q1, y0, q0);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.Equal(0, allocated);
        Assert.True(q0[VariationalEquations.CovarianceIndex(4, 4)] > 0.0);
    }

    [Fact]
    public void AtAManeuver_TheExecutionErrorIsAddedToTheVelocityBlock()
    {
        // Arrange: the maneuver case, with the covariance and without process noise, so that Q before the burn is zero
        // and the jump is the execution error alone
        var options = new VariationalOptions(PropagateCovariance: true);
        var execution = new ManeuverExecutionError(0.05, 0.01, 0.02, 0.005);
        var (spacecraft, maneuver) = ManeuverCase(execution);

        // Act
        var solution = spacecraft.Propagate(new TimeSystem.Window(Start, Start.AddHours(2.0)), new CelestialItem[]
            { _cases.Earth }, new RK78Integrator(1e-11, 1e-11), false, false, TimeSpan.FromSeconds(600.0), options);

        // Assert: P(after) − P(before) = B L_e Bᵀ at the burn (C5), L_e of the executed ΔV
        Assert.Equal(2, solution.Segments.Count);
        var before = solution.Segments[0];
        var after = solution.Segments[1];
        var expected = new double[9];
        execution.Covariance(maneuver.DeltaV, expected);
        Assert.Equal(expected, after.Variational.EntryManeuver.Value.ExecutionCovariance);
        Assert.True(maneuver.DeltaV.Magnitude() > 100.0);

        double te = RK78Integrator.OnEpochGrid(before.BaseEpoch, after.BaseEpoch, before.Steps[^1].CumulativeTime,
            before.Duration);
        var pBefore = new double[36];
        var pAfter = new double[36];
        solution.Dynamics.EvaluateCovariance(before, te, pBefore, false);
        solution.EvaluateCovariance(after.BaseEpoch, pAfter);
        var jump = new double[9];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                if (i < 3 || j < 3)
                {
                    Assert.Equal(pBefore[6 * i + j], pAfter[6 * i + j]);
                }
                else
                {
                    jump[3 * (i - 3) + j - 3] = pAfter[6 * i + j] - pBefore[6 * i + j];
                }
            }
        }

        double error = CovarianceAssert.RelativeError(jump, expected);
        _output.WriteLine($"jump of P at the burn against L_e: {error:E2}");
        Assert.True(error < 1e-12, $"{error:E2}");
    }

    [Fact]
    public void AfterAManeuver_TheExecutionErrorPropagatesThroughPhi()
    {
        // Arrange: the same propagation with and without the execution error; the trajectories are the same, and the
        // covariances differ by Φ(t, te) B L_e Bᵀ Φ(t, te)ᵀ, with Φ(t, te) the Φ of the second segment
        var options = new VariationalOptions(ProcessNoise: new RtnProcessNoise(1e-12, 1e-11, 1e-12),
            PropagateCovariance: true);
        var execution = new ManeuverExecutionError(0.05, 0.01, 0.02, 0.005);
        var window = new TimeSystem.Window(Start, Start.AddHours(2.0));
        var (withError, maneuver) = ManeuverCase(execution);
        var solution = withError.Propagate(window, new CelestialItem[] { _cases.Earth }, new RK78Integrator(1e-11, 1e-11),
            false, false, TimeSpan.FromSeconds(600.0), options);
        var (withoutError, _) = ManeuverCase(null, -1963);
        var reference = withoutError.Propagate(window, new CelestialItem[] { _cases.Earth },
            new RK78Integrator(1e-11, 1e-11), false, false, TimeSpan.FromSeconds(600.0), options);
        var after = solution.Segments[1];
        Assert.Equal(after.Steps[^1].EndPosition, reference.Segments[1].Steps[^1].EndPosition);
        var executionCovariance = new double[9];
        execution.Covariance(maneuver.DeltaV, executionCovariance);

        // Act
        var p = new double[36];
        var pReference = new double[36];
        solution.Dynamics.EvaluateCovariance(after, after.Duration, p, false);
        reference.Dynamics.EvaluateCovariance(reference.Segments[1], reference.Segments[1].Duration, pReference, false);

        // Assert
        var phiSegment = after.Variational.StepEndY(after.Variational.StepCount - 1).ToArray();
        var phi = CovarianceAssert.ToMathNet(phiSegment, 6);
        var jump = Matrix<double>.Build.Dense(6, 6);
        jump.SetSubMatrix(3, 3, CovarianceAssert.ToMathNet(executionCovariance, 3));
        var expected = CovarianceAssert.ToArray(phi * jump * phi.Transpose());
        var difference = p.Zip(pReference, (a, b) => a - b).ToArray();
        double error = StmMeasures.WorstBlockError(difference, expected);
        _output.WriteLine($"propagated execution error against Φ_seg B L_e Bᵀ Φ_segᵀ: {error:E2} (worst 3×3 block)");
        // The composition across segments of A5, 1e-10 (specification); measured 1.7e-16 (2026-10-14)
        Assert.True(error < 1e-10, $"{error:E2}");
        CovarianceAssert.SymmetricPositiveSemiDefinite(p, 6, "P after the burn");
    }

    [Fact]
    public void WithoutACarriedCovariance_TheExecutionErrorIsRecordedButNotApplied()
    {
        // Arrange: Φ only, so no Q is carried
        var (spacecraft, maneuver) = ManeuverCase(new ManeuverExecutionError(0.05, 0.01, 0.02, 0.005));

        // Act
        var solution = spacecraft.Propagate(new TimeSystem.Window(Start, Start.AddHours(2.0)),
            new CelestialItem[] { _cases.Earth }, new RK78Integrator(1e-11, 1e-11), false, false,
            TimeSpan.FromSeconds(600.0), new VariationalOptions());

        // Assert
        var after = solution.Segments[1].Variational;
        Assert.Empty(after.EntryQ);
        Assert.NotNull(after.EntryManeuver.Value.ExecutionCovariance);
        Assert.Equal(maneuver.DeltaV, after.EntryManeuver.Value.DeltaV);
    }

    // 400 × 1000 km LEO from apogee, apogee raised at the first perigee, with a covariance on the initial state
    private (Spacecraft Spacecraft, ImpulseManeuver Maneuver) ManeuverCase(ManeuverExecutionError execution,
        int naifId = -1962)
    {
        var orbit = WithCovariance(new StateVector(new Vector3(-7378137.0, 0.0, 0.0), new Vector3(0.0, -5509.9, -4623.4),
            _cases.Earth, Start, Frames.Frame.ICRF));
        var spacecraft = new Spacecraft(naifId, "COV", 1000.0, 3000.0, new Clock("cov", 65536), orbit);
        var tank = new FuelTank("covtank", "model", "sn1", 1000.0, 1000.0);
        var engine = new Engine("covengine", "model", "sn1", 300.0, 10.0, tank);
        spacecraft.AddFuelTank(tank);
        spacecraft.AddEngine(engine);
        var maneuver = new ApogeeHeightManeuver(_cases.Earth, Start, TimeSpan.Zero, 8378137.0, engine)
        {
            ExecutionError = execution
        };
        spacecraft.SetStandbyManeuver(maneuver);
        return (spacecraft, maneuver);
    }

    private InvalidOperationException EnableWith(IO.Astrodynamics.Math.Matrix covariance)
    {
        var state = new StateVector(Leo.Position, Leo.Velocity, _cases.Earth, Start, Frames.Frame.ICRF, covariance);
        var propagator = Propagator(PartialsTestCases.Spacecraft(state), Start, Start.AddHours(1.0));
        return Assert.Throws<InvalidOperationException>(() => propagator.EnableVariationalEquations(Covariance));
    }

    private CentralBodyPropagator Propagator(Spacecraft spacecraft, TimeSystem.Time start, TimeSystem.Time end)
    {
        return new CentralBodyPropagator(new TimeSystem.Window(start, end), spacecraft, new RK78Integrator(), Bodies,
            false, false, TimeSpan.FromSeconds(600.0));
    }

    private PropagationSolution Propagate(StateVector state, TimeSystem.Time end, VariationalOptions options,
        bool dragAndSrp)
    {
        return PartialsTestCases.Spacecraft(state).Propagate(new TimeSystem.Window(Start, end), Bodies,
            new RK78Integrator(1e-9, 1e-9), dragAndSrp, dragAndSrp, TimeSpan.FromSeconds(600.0), options);
    }

    /// <summary>
    /// The state with a covariance given in RTN: 50, 200 and 30 m, 0.2, 0.05 and 0.03 m/s, the radial position and
    /// transverse velocity correlated at −0.5 and the transverse position and radial velocity at −0.9.
    /// </summary>
    internal static StateVector WithCovariance(StateVector state)
    {
        return new StateVector(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame,
            InitialCovariance(state));
    }

    private static IO.Astrodynamics.Math.Matrix InitialCovariance(StateVector state)
    {
        double[] sigma = { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 };
        var rtn = new IO.Astrodynamics.Math.Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            rtn.Set(i, i, sigma[i] * sigma[i]);
        }

        rtn.Set(0, 4, -0.5 * sigma[0] * sigma[4]);
        rtn.Set(4, 0, -0.5 * sigma[0] * sigma[4]);
        rtn.Set(1, 3, -0.9 * sigma[1] * sigma[3]);
        rtn.Set(3, 1, -0.9 * sigma[1] * sigma[3]);
        return state.RotateCovarianceFromRtn(rtn);
    }

    private static double[] Symmetrized(IO.Astrodynamics.Math.Matrix matrix)
    {
        var result = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                result[6 * i + j] = 0.5 * (matrix.Get(i, j) + matrix.Get(j, i));
            }
        }

        return result;
    }
}
