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
using MathNet.Numerics.LinearAlgebra.Factorization;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// The variational equations integrated by a propagation (A1 to A5): data per segment, evaluation at any epoch,
/// events and maneuvers, chaining, and a check of Φ against finite differences of whole propagations.
/// </summary>
public class VariationalPropagationTests : IClassFixture<PartialsTestCases>
{
    private static readonly double[] Qc = { 1e-12, 2e-13, 0.0, 2e-13, 5e-13, 1e-13, 0.0, 1e-13, 2e-12 };

    private readonly PartialsTestCases _cases;
    private readonly ITestOutputHelper _output;

    public VariationalPropagationTests(PartialsTestCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    private TimeSystem.Time Start => PartialsTestCases.Epoch;

    private StateVector Leo => PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);

    private CelestialItem[] Bodies => new CelestialItem[] { _cases.Earth, _cases.Moon, _cases.Sun };

    private static VariationalOptions FullOptions => new(ForceParameters.DragCoefficient |
                                                         ForceParameters.ReflectivityCoefficient,
        new ConstantProcessNoise(Qc));

    [Fact]
    public void WithoutTheVariationalEquations_TheSolutionKeepsTheDynamicsAndTheContexts()
    {
        // Arrange
        var spacecraft = PartialsTestCases.Spacecraft(Leo);

        // Act
        var solution = spacecraft.Propagate(new TimeSystem.Window(Start, Start.AddHours(1.0)), Bodies,
            new RK78Integrator(), true, true, TimeSpan.FromSeconds(600.0), null);

        // Assert
        Assert.NotNull(solution.Dynamics);
        Assert.Null(solution.Dynamics.Options);
        Assert.Equal(0, solution.Dynamics.StageJacobianEvaluations);
        Assert.All(solution.Segments, segment =>
        {
            Assert.Equal(ForceEvaluationContext.FromSpacecraft(spacecraft), segment.Context);
            Assert.Null(segment.Variational);
        });
        var exception = Assert.Throws<InvalidOperationException>(() =>
            solution.EvaluateVariational(Start.AddHours(0.5), new double[36], Array.Empty<double>()));
        Assert.Contains("variational", exception.Message);
    }

    [Fact]
    public void WithVelocityVerlet_TheSolutionKeepsNoDynamics()
    {
        // Act
        var solution = PartialsTestCases.Spacecraft(Leo).Propagate(new TimeSystem.Window(Start, Start.AddHours(0.5)),
            Bodies, false, false, TimeSpan.FromSeconds(60.0));

        // Assert
        Assert.Null(solution.Dynamics);
        Assert.All(solution.Segments, segment => Assert.Null(segment.Context));
        var exception = Assert.Throws<InvalidOperationException>(() =>
            solution.EvaluateVariational(Start.AddMinutes(10.0), new double[36], Array.Empty<double>()));
        Assert.Contains("variational", exception.Message);
    }

    [Fact]
    public void APropagatorWithoutOrientationCache_EvaluatesWithoutPuttingOneBack()
    {
        // Arrange: a propagator of its own, with the default of PropagatorBase (no orientation cache), around a
        // point-mass Earth
        var integrator = new RK78Integrator();
        integrator.AddForce(new GravitationalAcceleration(_cases.Earth));
        var propagator = new MinimalPropagator(new TimeSystem.Window(Start, Start.AddHours(1.0)),
            PartialsTestCases.Spacecraft(Leo), integrator);
        propagator.EnableVariationalEquations(new VariationalOptions());
        var y = new double[36];

        // Act
        var solution = propagator.Propagate();
        solution.EvaluateVariational(Start.AddMinutes(17.3), y, Array.Empty<double>());

        // Assert
        Assert.NotNull(solution.Dynamics);
        Assert.Equal(13L * solution.Segments.Single().Steps.Count + 13, solution.Dynamics.StageJacobianEvaluations);
        Assert.NotEqual(0.0, y[3]);
    }

    [Fact]
    public void InterpolateAt_BeforeTheStart_ReturnsTheFirstState()
    {
        // Arrange
        var solution = PropagateLeo(Start.AddHours(1.0), null, false);
        var first = solution.Segments[0].Steps[0];

        // Act
        var (position, velocity) = solution.InterpolateAt(Start.AddSeconds(-10.0));

        // Assert
        AssertSameBits(first.StartPosition, position, "position");
        AssertSameBits(first.StartVelocity, velocity, "velocity");
    }

    /// <summary>A propagator with the defaults of <see cref="PropagatorBase"/>, storing nothing.</summary>
    private sealed class MinimalPropagator : PropagatorBase
    {
        internal MinimalPropagator(in TimeSystem.Window window, Spacecraft spacecraft, RK78Integrator integrator)
            : base(window, spacecraft, integrator, TimeSpan.FromSeconds(600.0))
        {
            InitialState = spacecraft.InitialOrbitalParameters.ToStateVector();
            integrator.Initialize(InitialState);
        }

        protected override void StorePropagatedStates(StateVector[] outputStates)
        {
        }
    }

    [Fact]
    public void ShortenedStep_OfASegmentWithoutContext_Throws()
    {
        // Arrange: a segment built by hand, with variational data but no context
        var state = Leo;
        var dynamics = new PropagationDynamics(new List<ForceBase> { new GravitationalAcceleration(_cases.Earth) },
            _cases.Earth, Frames.Frame.ICRF, Start, null, null, new VariationalOptions(), null);
        var segment = new PropagationSegment(Start);
        segment.AddStep(new AcceptedStep(0.0, 60.0, state.Position, state.Velocity, state.Position, state.Velocity,
            Vector3.Zero, Vector3.Zero));
        segment.Variational = new VariationalSegmentData(36, false, 1, new double[36], Array.Empty<double>(), null);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            dynamics.ShortenedStep(segment, 0, 30.0, new double[36], Array.Empty<double>()));

        // Assert
        Assert.Contains("context", exception.Message);
        Assert.Throws<ArgumentNullException>(() =>
            dynamics.ShortenedStep(null, 0, 30.0, new double[36], Array.Empty<double>()));
    }

    [Fact]
    public void BeginVariationalSegment_WithoutTheVariationalEquations_Throws()
    {
        // Arrange
        var integrator = new RK78Integrator();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            integrator.BeginVariationalSegment(new ForceEvaluationContext(1.0, 2.2, 1.5), new double[36],
                Array.Empty<double>(), null));

        // Assert
        Assert.Contains("not enabled", exception.Message);
        Assert.Null(integrator.VariationalEquations);
    }

    [Fact]
    public void AtTheStart_PhiIsTheIdentity_AndPsiAndQAreZero()
    {
        // Arrange
        var solution = PropagateLeo(Start.AddHours(1.0), FullOptions, true);
        var y = Enumerable.Repeat(9.0, 48).ToArray();
        var q = Enumerable.Repeat(9.0, VariationalEquations.CovarianceLength).ToArray();

        // Act
        solution.EvaluateVariational(Start, y, q);

        // Assert
        for (int i = 0; i < 6; i++)
        {
            for (int c = 0; c < 8; c++)
            {
                Assert.Equal(i == c ? 1.0 : 0.0, y[i * 8 + c]);
            }
        }

        Assert.All(q, value => Assert.Equal(0.0, value));
    }

    [Fact]
    public void AFullShortenedStep_ReproducesTheStoredValuesAndState_BitForBit()
    {
        // Arrange: drag and SRP read the mass, which the evaluation takes from the context of the segment
        var solution = PropagateLeo(Start.AddHours(1.0), FullOptions, true);
        var segment = solution.Segments.Single();
        var data = segment.Variational;
        var y = new double[48];
        var q = new double[VariationalEquations.CovarianceLength];
        int count = segment.Steps.Count;

        foreach (int k in new[] { 0, 1, count / 2, count - 1 })
        {
            // Act
            var (position, velocity) = solution.Dynamics.ShortenedStep(segment, k, segment.Steps[k].StepSize, y, q);

            // Assert
            AssertSameBits(data.StepEndY(k), y, $"Y, step {k}");
            AssertSameBits(data.StepEndQ(k), q, $"Q, step {k}");
            AssertSameBits(segment.Steps[k].EndPosition, position, $"position, step {k}");
            AssertSameBits(segment.Steps[k].EndVelocity, velocity, $"velocity, step {k}");
        }
    }

    [Fact]
    public void InsideAStep_MatchesAPropagationThatStopsThere()
    {
        // Arrange: a propagation that stops at te takes the same steps up to te, then a last step of te − t_k, the same
        // as the shortened step from t_k.
        var te = Start.AddSeconds(3000.25);
        var full = PropagateLeo(Start.AddHours(2.0), FullOptions, true);
        var segment = full.Segments.Single();
        double t = (te - Start).TotalSeconds;
        var step = segment.Steps[segment.FindStepIndex(t)];
        Assert.InRange(t - step.CumulativeTime, 1.0, step.StepSize - 1.0);
        var stopped = PropagateLeo(te, FullOptions, true, TimeSpan.FromSeconds(t));

        // Act
        var y = new double[48];
        var q = new double[VariationalEquations.CovarianceLength];
        full.EvaluateVariational(te, y, q);
        var expectedY = new double[48];
        var expectedQ = new double[VariationalEquations.CovarianceLength];
        stopped.EvaluateVariational(te, expectedY, expectedQ);

        // Assert
        double yError = RiddersDerivative.RelativeFrobeniusError(y, expectedY);
        double qError = RiddersDerivative.RelativeFrobeniusError(q, expectedQ);
        _output.WriteLine($"shortened step against a propagation stopping at te: Y {yError:E2}, Q {qError:E2}");
        Assert.True(yError < 1e-12, $"Y: {yError:E2}");
        Assert.True(qError < 1e-12, $"Q: {qError:E2}");
    }

    [Fact]
    public void Composition_MatchesAnIndependentPropagationFromAnIntermediateEpoch()
    {
        // Arrange: Φ(t2, t0) against Φ(t2, t1) Φ(t1, t0), and Q(t2) against Φ(t2, t1) Q(t1) Φ(t2, t1)ᵀ + Q(t2, t1), where
        // the propagation from t1 starts from the RK state at t1 of a propagation stopping there. Point mass and third
        // bodies, at tight tolerances.
        var options = new VariationalOptions(ProcessNoise: new ConstantProcessNoise(Qc));
        var t1 = Start.AddHours(1.0);
        var t2 = Start.AddHours(2.0);
        var whole = PropagateLeo(t2, options, false, tolerance: 1e-13);
        var first = PropagateLeo(t1, options, false, TimeSpan.FromHours(1.0), 1e-13);
        var lastStep = first.Segments.Single().Steps[^1];
        var middle = new StateVector(lastStep.EndPosition, lastStep.EndVelocity, _cases.Earth, t1, Frames.Frame.ICRF);
        var second = PartialsTestCases.Spacecraft(middle).Propagate(new TimeSystem.Window(t1, t2), Bodies,
            new RK78Integrator(1e-13, 1e-13), false, false, TimeSpan.FromHours(1.0), options);

        // Act
        var (phi20, q20) = Evaluate(whole, t2, 6);
        var (phi10, q10) = Evaluate(first, t1, 6);
        var (phi21, q21) = Evaluate(second, t2, 6);

        // Assert
        var composedPhi = Multiply(phi21, phi10);
        var composedQ = Add(Multiply(Multiply(phi21, Full(q10)), Transpose(phi21)), Full(q21));
        double phiError = WorstBlockError(composedPhi, phi20);
        double qError = WorstBlockError(composedQ, Full(q20));
        _output.WriteLine($"composition: Φ {phiError:E2}, Q {qError:E2} (worst 3×3 block)");
        Assert.True(phiError < 1e-10, $"Φ: {phiError:E2}");
        Assert.True(qError < 1e-10, $"Q: {qError:E2}");
    }

    [Fact]
    public void AtAManeuver_PhiIsContinuous_AndTheBurnIsRecorded()
    {
        // Arrange: the maneuver case, with drag, so that Ψ is not zero on either side of the burn
        var (spacecraft, maneuver) = ManeuverCase();
        double initialMass = spacecraft.GetTotalMass();

        // Act
        var solution = spacecraft.Propagate(new TimeSystem.Window(Start, Start.AddHours(2.0)), Bodies,
            new RK78Integrator(1e-11, 1e-11), true, false, TimeSpan.FromSeconds(60.0), FullOptions);

        // Assert: two segments, the burn recorded at the start of the second
        Assert.Equal(2, solution.Segments.Count);
        var before = solution.Segments[0];
        var after = solution.Segments[1];
        var entry = after.Variational.EntryManeuver;
        Assert.NotNull(entry);
        Assert.Equal(after.BaseEpoch, entry.Value.Epoch);
        AssertSameBits(maneuver.DeltaV, entry.Value.DeltaV, "ΔV");
        Assert.Null(before.Variational.EntryManeuver);
        Assert.Equal(initialMass, before.Context.Value.TotalMass);
        Assert.Equal(spacecraft.GetTotalMass(), after.Context.Value.TotalMass);
        Assert.True(after.Context.Value.TotalMass < initialMass);

        // Φ, Ψ and Q are continuous: the values of the first segment at te are the entry values of the second, and the
        // solution returns those at te. The event time te is on the 100 ns grid of the epochs, so the second segment
        // starts at the instant of the values of the first: the same doubles (#363; 1e-9 before, when the epoch carried
        // te to 100 ns only).
        int k = before.Steps.Count - 1;
        double te = RK78Integrator.OnEpochGrid(before.BaseEpoch, after.BaseEpoch, before.Steps[k].CumulativeTime,
            before.Duration);
        var yBefore = new double[48];
        var qBefore = new double[VariationalEquations.CovarianceLength];
        solution.Dynamics.Evaluate(before, te, yBefore, qBefore);
        AssertSameBits(yBefore, after.Variational.EntryY, "Y at the maneuver");
        AssertSameBits(qBefore, after.Variational.EntryQ, "Q at the maneuver");
        var y = new double[48];
        var q = new double[VariationalEquations.CovarianceLength];
        solution.EvaluateVariational(after.BaseEpoch, y, q);
        AssertSameBits(after.Variational.EntryY, y, "Y returned at te");
        AssertSameBits(after.Variational.EntryQ, q, "Q returned at te");

        // The trajectory restarts from the state of the shortened step from which Y(te) comes, plus ΔV
        var (rkPosition, rkVelocity) = solution.Dynamics.ShortenedStep(before, k, te - before.Steps[k].CumulativeTime,
            new double[48], new double[VariationalEquations.CovarianceLength]);
        AssertSameBits(rkPosition, after.Steps[0].StartPosition, "position at te");
        AssertSameBits(rkVelocity + maneuver.DeltaV, after.Steps[0].StartVelocity, "velocity after the maneuver");
    }

    [Fact]
    public void AfterAManeuver_PhiAndPsiMatchAnIndependentPropagationFromThePostBurnState()
    {
        // Arrange: the maneuver case with drag, at tight tolerances. A second propagation starts from the post-burn state
        // at te, with the post-burn mass, and integrates its own Φ₂ and Ψ₂ from (I, 0); open-loop burn, so
        // Φ(t2, t0) = Φ₂ Φ(te, t0) and Ψ(t2, t0) = Φ₂ Ψ(te, t0) + Ψ₂. Ψ goes through the post-burn ∂a/∂Cd, which depends
        // on the mass, and through the term Φ₂ Ψ(te, t0) of the composition.
        var options = new VariationalOptions(ForceParameters.DragCoefficient);
        var (spacecraft, _) = ManeuverCase();
        var t2 = Start.AddHours(2.0);
        var whole = spacecraft.Propagate(new TimeSystem.Window(Start, t2), Bodies, new RK78Integrator(1e-13, 1e-13), true,
            false, TimeSpan.FromSeconds(60.0), options);
        Assert.Equal(2, whole.Segments.Count);
        var after = whole.Segments[1];
        var te = after.BaseEpoch;
        var postBurn = new StateVector(after.Steps[0].StartPosition, after.Steps[0].StartVelocity, _cases.Earth, te,
            Frames.Frame.ICRF);
        var (independent, _) = ManeuverCase(-1963, postBurn, spacecraft.GetTotalMass() - 1000.0, false);
        using var propagator = new ExactStartPropagator(new TimeSystem.Window(te, t2), independent,
            new RK78Integrator(1e-13, 1e-13), Bodies, TimeSpan.FromSeconds(60.0), postBurn);
        propagator.EnableVariationalEquations(options);
        var second = propagator.Propagate();
        AssertSameBits(postBurn.Position, second.Segments[0].Steps[0].StartPosition, "start position");
        AssertSameBits(postBurn.Velocity, second.Segments[0].Steps[0].StartVelocity, "start velocity");
        Assert.Single(second.Segments);
        Assert.Equal(after.Context.Value.TotalMass, second.Segments[0].Context.Value.TotalMass);

        // Act
        var (phi20, psi20) = PhiAndPsi(whole, t2);
        var (phiE0, psiE0) = PhiAndPsi(whole, te);
        var (phi2, psi2) = PhiAndPsi(second, t2);

        // Assert: the threshold of the specification (A5); measured Φ 3.0e-15 and Ψ 3.8e-12 (Linux, .NET 10, 2026-10-10)
        var composedPhi = Multiply(phi2, phiE0);
        var composedPsi = new double[6];
        for (int i = 0; i < 6; i++)
        {
            double sum = psi2[i];
            for (int k = 0; k < 6; k++)
            {
                sum += phi2[6 * i + k] * psiE0[k];
            }

            composedPsi[i] = sum;
        }

        double phiError = WorstBlockError(composedPhi, phi20);
        double psiPositionError = RiddersDerivative.RelativeFrobeniusError(composedPsi[..3], psi20[..3]);
        double psiVelocityError = RiddersDerivative.RelativeFrobeniusError(composedPsi[3..], psi20[3..]);
        _output.WriteLine($"composition across the maneuver: Φ {phiError:E2} (worst 3×3 block), Ψ {psiPositionError:E2} " +
                          $"(position), {psiVelocityError:E2} (velocity); |Ψ(te)| = {Norm(psiE0[..3]):E2} m, " +
                          $"|Ψ(t2)| = {Norm(psi20[..3]):E2} m");
        Assert.True(Norm(psiE0[..3]) > 0.0, "Ψ is zero before the burn");
        Assert.True(phiError < 1e-10, $"Φ: {phiError:E2}");
        Assert.True(psiPositionError < 1e-10, $"Ψ, position: {psiPositionError:E2}");
        Assert.True(psiVelocityError < 1e-10, $"Ψ, velocity: {psiVelocityError:E2}");
    }

    [Fact]
    public void TwoBody_PhiMatchesCentralDifferencesOfWholePropagations()
    {
        // Arrange: point-mass Earth, a fixed 60 s step over about one orbit, so that every perturbed propagation takes
        // the same steps and the differences see the discrete map itself. Steps of 1 m and 1 mm/s: the truncation is
        // of order (δ/r)², and the rounding of the final state ε |r| / δ ≈ 1e-9 relative.
        const double duration = 5520.0;
        var end = Start.AddSeconds(duration);
        var options = new VariationalOptions();
        var (phi, _) = Evaluate(PropagateTwoBody(Leo, end, options), end, 6);

        // Act
        var expected = new double[36];
        for (int column = 0; column < 6; column++)
        {
            double delta = column < 3 ? 1.0 : 1e-3;
            var plus = FinalState(PropagateTwoBody(Perturbed(Leo, column, delta), end, null));
            var minus = FinalState(PropagateTwoBody(Perturbed(Leo, column, -delta), end, null));
            for (int row = 0; row < 6; row++)
            {
                expected[row * 6 + column] = (plus[row] - minus[row]) / (2.0 * delta);
            }
        }

        // Assert
        double error = WorstBlockError(phi, expected);
        _output.WriteLine($"Φ against central differences of propagations: {error:E2} (worst 3×3 block)");
        Assert.True(error < 1e-7, $"Φ: {error:E2}");
    }

    [Fact]
    public void Counter_CountsThirteenStageJacobiansPerAcceptedAndShortenedStep()
    {
        // Arrange
        var solution = PropagateLeo(Start.AddHours(1.0), new VariationalOptions(), false);
        var segment = solution.Segments.Single();
        long afterPropagation = solution.Dynamics.StageJacobianEvaluations;
        var y = new double[36];

        // Act: a step boundary, and the end of the propagation, read the stored values; an epoch inside a step needs a
        // shortened step. The boundary is given in seconds from the segment start: an epoch goes through the 100 ns ticks
        // of TimeSpan and lands next to it.
        solution.Dynamics.Evaluate(segment, segment.Steps[3].CumulativeTime, y, Array.Empty<double>());
        solution.EvaluateVariational(Start.AddHours(1.0), y, Array.Empty<double>());
        long afterBoundary = solution.Dynamics.StageJacobianEvaluations;
        solution.EvaluateVariational(
            segment.BaseEpoch.AddSeconds(segment.Steps[3].CumulativeTime + 0.5 * segment.Steps[3].StepSize), y,
            Array.Empty<double>());

        // Assert
        Assert.Equal(13L * segment.Steps.Count, afterPropagation);
        Assert.Equal(afterPropagation, afterBoundary);
        Assert.Equal(afterPropagation + 13, solution.Dynamics.StageJacobianEvaluations);
    }

    [Fact]
    public void EvaluateVariational_OutsideThePropagation_Throws()
    {
        // Arrange
        var solution = PropagateLeo(Start.AddHours(1.0), new VariationalOptions(), false);
        var y = new double[36];

        // Act and assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            solution.EvaluateVariational(Start.AddSeconds(-1.0), y, Array.Empty<double>()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            solution.EvaluateVariational(Start.AddHours(1.0).AddSeconds(1.0), y, Array.Empty<double>()));
        Assert.Throws<InvalidOperationException>(() =>
            new PropagationSolution().EvaluateVariational(Start, y, Array.Empty<double>()));
    }

    [Fact]
    public void EvaluateVariational_WithBuffersOfTheWrongLength_Throws()
    {
        // Arrange: Y holds 6 × 8 values with Cd and Cr, Q 21 with process noise and none without
        var epoch = Start.AddMinutes(30.0);
        var withNoise = PropagateLeo(Start.AddHours(1.0), FullOptions, true);
        var withoutNoise = PropagateLeo(Start.AddHours(1.0), new VariationalOptions(), false);
        var q = new double[VariationalEquations.CovarianceLength];

        // Act and assert
        Assert.Equal("y", Assert.Throws<ArgumentException>(() =>
            withNoise.EvaluateVariational(epoch, new double[36], q)).ParamName);
        Assert.Equal("y", Assert.Throws<ArgumentException>(() =>
            withNoise.EvaluateVariational(epoch, new double[49], q)).ParamName);
        Assert.Equal("q", Assert.Throws<ArgumentException>(() =>
            withNoise.EvaluateVariational(epoch, new double[48], Array.Empty<double>())).ParamName);
        Assert.Equal("q", Assert.Throws<ArgumentException>(() =>
            withNoise.EvaluateVariational(epoch, new double[48], new double[20])).ParamName);
        Assert.Equal("q", Assert.Throws<ArgumentException>(() =>
            withoutNoise.EvaluateVariational(epoch, new double[36], q)).ParamName);
        withNoise.EvaluateVariational(epoch, new double[48], q);
        withoutNoise.EvaluateVariational(epoch, new double[36], Array.Empty<double>());
    }

    [Fact]
    public void WithTheVariationalEquations_TheTrajectoryIsBitIdentical()
    {
        // Act
        var without = PropagateLeo(Start.AddHours(1.0), null, true);
        var with = PropagateLeo(Start.AddHours(1.0), FullOptions, true);

        // Assert
        var stepsWithout = without.Segments.Single().Steps;
        var stepsWith = with.Segments.Single().Steps;
        Assert.Equal(stepsWithout.Count, stepsWith.Count);
        for (int k = 0; k < stepsWith.Count; k++)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(stepsWithout[k].StepSize),
                BitConverter.DoubleToInt64Bits(stepsWith[k].StepSize));
            AssertSameBits(stepsWithout[k].EndPosition, stepsWith[k].EndPosition, $"position, step {k}");
            AssertSameBits(stepsWithout[k].EndVelocity, stepsWith[k].EndVelocity, $"velocity, step {k}");
        }
    }

    [Fact]
    public void IncludeInStepControl_AddsTheErrorOfYToTheAdaptiveControlOnly()
    {
        // Arrange
        var controlled = new VariationalOptions(IncludeInStepControl: true);

        // Act
        var adaptive = PropagateLeo(Start.AddHours(1.0), new VariationalOptions(), false, tolerance: 1e-6);
        var adaptiveControlled = PropagateLeo(Start.AddHours(1.0), controlled, false, tolerance: 1e-6);
        var fixedStep = PropagateTwoBody(Leo, Start.AddHours(1.0), new VariationalOptions());
        var fixedControlled = PropagateTwoBody(Leo, Start.AddHours(1.0), controlled);

        // Assert: the error of Y can only shorten the adaptive steps; the fixed step ignores the option
        int steps = adaptive.Segments.Single().Steps.Count;
        int controlledSteps = adaptiveControlled.Segments.Single().Steps.Count;
        _output.WriteLine($"adaptive steps: {steps} on the state, {controlledSteps} with Y");
        Assert.True(controlledSteps >= steps);
        var (phi, _) = Evaluate(fixedStep, Start.AddHours(1.0), 6);
        var (phiControlled, _) = Evaluate(fixedControlled, Start.AddHours(1.0), 6);
        AssertSameBits(phi, phiControlled, "fixed step");
    }

    [Fact]
    public void SegmentData_AppendsWithinItsCapacityWithoutAllocating_AndGrows()
    {
        // Arrange
        var data = new VariationalSegmentData(36, true, 4, new double[36], new double[21], null);
        var y = Enumerable.Range(0, 36).Select(i => (double)i).ToArray();
        var q = Enumerable.Range(0, 21).Select(i => -(double)i).ToArray();
        data.Append(y, q);

        // Act
        long before = GC.GetAllocatedBytesForCurrentThread();
        data.Append(y, q);
        data.Append(y, q);
        data.Append(y, q);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        data.Append(y, q);

        // Assert
        Assert.Equal(0, allocated);
        Assert.Equal(5, data.StepCount);
        Assert.Equal(y, data.StepEndY(4).ToArray());
        Assert.Equal(q, data.StepEndQ(4).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => data.StepEndY(5).ToArray());
        Assert.Throws<ArgumentException>(() => new VariationalSegmentData(36, false, 4, new double[35],
            Array.Empty<double>(), null));
        Assert.Throws<ArgumentException>(() => new VariationalSegmentData(36, true, 4, new double[36],
            new double[20], null));
    }

    // 400 × 1000 km LEO from apogee, apogee raised at the first perigee (the maneuver case of the golden, with a
    // point-mass Earth); without the maneuver, the same spacecraft from another state and with another fuel quantity
    private (Spacecraft Spacecraft, ImpulseManeuver Maneuver) ManeuverCase(int naifId = -1962, StateVector orbit = null,
        double fuel = 1000.0, bool withManeuver = true)
    {
        orbit ??= new StateVector(new Vector3(-7378137.0, 0.0, 0.0), new Vector3(0.0, -5509.9, -4623.4), _cases.Earth,
            Start, Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(naifId, "STM", 1000.0, 3000.0, new Clock("stm", 65536), orbit);
        var tank = new FuelTank("stmtank", "model", "sn1", 1000.0, fuel);
        var engine = new Engine("stmengine", "model", "sn1", 300.0, 10.0, tank);
        spacecraft.AddFuelTank(tank);
        spacecraft.AddEngine(engine);
        if (!withManeuver)
        {
            return (spacecraft, null);
        }

        var maneuver = new ApogeeHeightManeuver(_cases.Earth, Start, TimeSpan.Zero, 8378137.0, engine);
        spacecraft.SetStandbyManeuver(maneuver);
        return (spacecraft, maneuver);
    }

    // A propagation with drag that starts from exactly the given state: CentralBodyPropagator takes its initial state
    // from OrbitalParameters.AtEpoch, which goes through the Keplerian elements even at the same epoch and moves a state
    // at an apsis by about 1e-8 of its radius (0.1 m at the post-burn perigee of the maneuver case)
    private sealed class ExactStartPropagator : CentralBodyPropagator
    {
        internal ExactStartPropagator(TimeSystem.Window window, Spacecraft spacecraft, Integrator integrator,
            IEnumerable<CelestialItem> bodies, TimeSpan deltaT, StateVector start)
            : base(window, spacecraft, integrator, bodies, true, false, deltaT)
        {
            InitialState = start;
        }
    }

    // Φ (6×6) and Ψ (6×1) at the epoch, for a solution with one parameter and no process noise
    private static (double[] Phi, double[] Psi) PhiAndPsi(PropagationSolution solution, TimeSystem.Time epoch)
    {
        var y = new double[42];
        solution.EvaluateVariational(epoch, y, Array.Empty<double>());
        var phi = new double[36];
        var psi = new double[6];
        for (int i = 0; i < 6; i++)
        {
            Array.Copy(y, 7 * i, phi, 6 * i, 6);
            psi[i] = y[7 * i + 6];
        }

        return (phi, psi);
    }

    private static double Norm(double[] v) => System.Math.Sqrt(v.Sum(x => x * x));

    private PropagationSolution PropagateLeo(TimeSystem.Time end, VariationalOptions options, bool dragAndSrp,
        TimeSpan? outputStep = null, double tolerance = 1e-9)
    {
        return PartialsTestCases.Spacecraft(Leo).Propagate(new TimeSystem.Window(Start, end), Bodies,
            new RK78Integrator(tolerance, tolerance), dragAndSrp, dragAndSrp, outputStep ?? TimeSpan.FromSeconds(600.0),
            options);
    }

    private PropagationSolution PropagateTwoBody(StateVector start, TimeSystem.Time end, VariationalOptions options)
    {
        return PartialsTestCases.Spacecraft(start).Propagate(new TimeSystem.Window(Start, end),
            new CelestialItem[] { _cases.Earth }, new RK78Integrator(60.0), false, false, TimeSpan.FromSeconds(60.0),
            options);
    }

    private static double[] FinalState(PropagationSolution solution)
    {
        var step = solution.Segments[^1].Steps[^1];
        return new[]
        {
            step.EndPosition.X, step.EndPosition.Y, step.EndPosition.Z,
            step.EndVelocity.X, step.EndVelocity.Y, step.EndVelocity.Z
        };
    }

    private static StateVector Perturbed(StateVector state, int component, double delta)
    {
        var p = state.Position;
        var v = state.Velocity;
        var position = component switch
        {
            0 => new Vector3(p.X + delta, p.Y, p.Z),
            1 => new Vector3(p.X, p.Y + delta, p.Z),
            2 => new Vector3(p.X, p.Y, p.Z + delta),
            _ => p
        };
        var velocity = component switch
        {
            3 => new Vector3(v.X + delta, v.Y, v.Z),
            4 => new Vector3(v.X, v.Y + delta, v.Z),
            5 => new Vector3(v.X, v.Y, v.Z + delta),
            _ => v
        };
        return new StateVector(position, velocity, state.Observer, state.Epoch, state.Frame);
    }

    // Φ (6×6, the first 6 columns of Y) and the full Q (6×6, zero without process noise) at the epoch
    private static (double[] Phi, double[] Q) Evaluate(PropagationSolution solution, TimeSystem.Time epoch,
        int columns)
    {
        bool hasQ = solution.Dynamics.Options.ProcessNoise != null;
        int n = 6 + (solution.Dynamics.Options.Parameters == ForceParameters.None
            ? 0
            : solution.Dynamics.Options.Parameters ==
              (ForceParameters.DragCoefficient | ForceParameters.ReflectivityCoefficient)
                ? 2
                : 1);
        var y = new double[6 * n];
        var q = new double[hasQ ? VariationalEquations.CovarianceLength : 0];
        solution.EvaluateVariational(epoch, y, q);
        var phi = new double[6 * columns];
        for (int i = 0; i < 6; i++)
        {
            for (int c = 0; c < columns; c++)
            {
                phi[i * columns + c] = y[i * n + c];
            }
        }

        return (phi, q);
    }

    private static double[] Full(double[] packed)
    {
        var full = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                full[6 * i + j] = packed[VariationalEquations.CovarianceIndex(System.Math.Min(i, j),
                    System.Math.Max(i, j))];
            }
        }

        return full;
    }

    private static double[] Multiply(double[] a, double[] b)
    {
        var result = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < 6; k++)
                {
                    sum += a[6 * i + k] * b[6 * k + j];
                }

                result[6 * i + j] = sum;
            }
        }

        return result;
    }

    private static double[] Transpose(double[] a)
    {
        var result = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                result[6 * j + i] = a[6 * i + j];
            }
        }

        return result;
    }

    private static double[] Add(double[] a, double[] b)
    {
        return a.Zip(b, (x, y) => x + y).ToArray();
    }

    // The worst relative error of the four 3×3 blocks of a 6×6 matrix, each against its own norm: the blocks have
    // different units.
    private static double WorstBlockError(double[] actual, double[] expected)
    {
        double worst = 0.0;
        for (int block = 0; block < 4; block++)
        {
            int rowOffset = block < 2 ? 0 : 3;
            int columnOffset = block % 2 == 0 ? 0 : 3;
            var a = new double[9];
            var e = new double[9];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    a[3 * i + j] = actual[6 * (rowOffset + i) + columnOffset + j];
                    e[3 * i + j] = expected[6 * (rowOffset + i) + columnOffset + j];
                }
            }

            worst = System.Math.Max(worst, RiddersDerivative.RelativeFrobeniusError(a, e));
        }

        return worst;
    }

    private static void AssertSameBits(ReadOnlySpan<double> expected, ReadOnlySpan<double> actual, string what)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(BitConverter.DoubleToInt64Bits(expected[i]) == BitConverter.DoubleToInt64Bits(actual[i]),
                $"{what}: element {i}, {expected[i]:R} against {actual[i]:R}");
        }
    }

    private static void AssertSameBits(in Vector3 expected, in Vector3 actual, string what)
    {
        AssertSameBits(new[] { expected.X, expected.Y, expected.Z }, new[] { actual.X, actual.Y, actual.Z }, what);
    }
}
