// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using IO.Astrodynamics.Tests.Propagators.Variational;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// The states of an RK7(8) propagation between its accepted steps (#363): output states, states at any epoch, event
/// location and restart after a maneuver, against independent references (the two-body solution in universal variables,
/// a propagation that stops at the epoch).
/// </summary>
public class Rk78OutputAccuracyTests
{
    private static readonly TimeSystem.Time Start = PartialsTestCases.Epoch;

    private readonly ITestOutputHelper _output;
    private readonly CelestialBody _pointMassEarth;

    public Rk78OutputAccuracyTests(ITestOutputHelper output)
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        _output = output;
        _pointMassEarth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start);
    }

    public static TheoryData<double, double> Tolerances => new()
    {
        // Tolerance, and the bound on the worst output error relative to the worst error at the step ends (see below)
        { 1e-9, 1.5 },
        { 1e-11, 1.5 },
        { 1e-13, 1.5 }
    };

    /// <summary>
    /// The case of #363: 400 km LEO, point-mass Earth, 3 h, outputs every 10 s, against the exact two-body solution.
    /// The output states used to come from a cubic Hermite interpolation, off by up to 101 m at the default tolerance.
    /// An output is now a shortened step from the start of its accepted step, so its error is the error carried by the
    /// start of the step plus the local error of a step shorter than the accepted one: it stays at the level of the
    /// errors at the step ends.
    /// </summary>
    [Theory]
    [MemberData(nameof(Tolerances))]
    public void TwoBody_OutputStates_HaveTheAccuracyOfTheStepEnds(double tolerance, double bound)
    {
        // Arrange
        var initial = PartialsTestCases.State(PartialsTestCases.Leo400, _pointMassEarth);
        double mu = _pointMassEarth.GM;

        // Act
        var solution = PartialsTestCases.Spacecraft(initial).Propagate(new TimeSystem.Window(Start, Start.AddHours(3.0)),
            new CelestialItem[] { _pointMassEarth }, new RK78Integrator(tolerance, tolerance), false, false,
            TimeSpan.FromSeconds(10.0));

        // Assert
        var segment = solution.Segments.Single();
        double stepEndError = segment.Steps.Max(step =>
            PositionError(mu, initial, step.CumulativeTime + step.StepSize, step.EndPosition));
        double outputError = solution.StateVectors.Max(state =>
            PositionError(mu, initial, (state.Epoch - Start).TotalSeconds, state.Position));
        double outputVelocityError = solution.StateVectors.Max(state =>
            VelocityError(mu, initial, (state.Epoch - Start).TotalSeconds, state.Velocity));
        double hermiteError = solution.StateVectors.Max(state =>
        {
            double t = (state.Epoch - Start).TotalSeconds;
            return PositionError(mu, initial, t, segment.InterpolateAt(t).position);
        });
        double medianStep = segment.Steps.Select(step => step.StepSize).OrderBy(h => h).ElementAt(segment.Steps.Count / 2);
        _output.WriteLine($"tolerance {tolerance:E0}, median step {medianStep:F1} s: worst position error at the step " +
                          $"ends {stepEndError:E3} m, at the outputs {outputError:E3} m (velocity " +
                          $"{outputVelocityError:E3} m/s); cubic Hermite at the outputs {hermiteError:E3} m");
        Assert.True(outputError <= bound * stepEndError,
            $"outputs {outputError:E3} m against {stepEndError:E3} m at the step ends");
    }

    /// <summary>
    /// Inside a step, a state is a shortened step from the start of the step: the same steps, then the same last step,
    /// as a propagation whose window ends at that epoch, with a geopotential and third bodies. Bit for bit.
    /// </summary>
    [Fact]
    public void InsideAStep_TheStateIsThatOfAPropagationStoppingThere()
    {
        // Arrange: EGM2008 10×10, Moon and Sun, tolerance 1e-11
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
        var bodies = new CelestialItem[]
            { earth, new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Start), new CelestialBody(Stars.Sun) };
        var initial = PartialsTestCases.State(PartialsTestCases.Leo400, earth);
        var full = PartialsTestCases.Spacecraft(initial).Propagate(new TimeSystem.Window(Start, Start.AddHours(2.0)),
            bodies, new RK78Integrator(1e-11, 1e-11), false, false, TimeSpan.FromSeconds(60.0));
        var segment = full.Segments.Single();
        var te = Start.AddSeconds(3000.25);
        double t = (te - Start).TotalSeconds;
        var step = segment.Steps[segment.FindStepIndex(t)];
        Assert.InRange(t - step.CumulativeTime, 1.0, step.StepSize - 1.0);

        // Act
        var (position, velocity) = full.InterpolateAt(te);
        var stopped = PartialsTestCases.Spacecraft(initial).Propagate(new TimeSystem.Window(Start, te), bodies,
            new RK78Integrator(1e-11, 1e-11), false, false, TimeSpan.FromSeconds(t));

        // Assert
        var last = stopped.Segments.Single().Steps[^1];
        var (hermitePosition, _) = segment.InterpolateAt(t);
        _output.WriteLine($"cubic Hermite against the propagation stopping at te: {(hermitePosition - last.EndPosition).Magnitude():E3} m");
        AssertSameBits(last.EndPosition, position, "position");
        AssertSameBits(last.EndVelocity, velocity, "velocity");
        AssertSameBits(last.EndPosition, stopped.StateVectors[^1].Position, "last output of the stopped propagation");
    }

    /// <summary>
    /// At a step boundary, the stored state, without any evaluation of the forces; inside a step, one RK7(8) step, 13.
    /// </summary>
    [Fact]
    public void AStateCostsThirteenEvaluationsInsideAStep_AndNoneAtABoundary()
    {
        // Arrange
        var initial = PartialsTestCases.State(PartialsTestCases.Leo400, _pointMassEarth);
        var force = new CountingForce(_pointMassEarth.GM);
        var integrator = new RK78Integrator(1e-9, 1e-9);
        integrator.AddForce(force);
        integrator.Initialize(initial);
        var segment = integrator.IntegrateSegment(initial.Position, initial.Velocity, Start, 3600.0).Segment;
        segment.Context = new ForceEvaluationContext(100.0, 2.2, 1.5);
        var dynamics = new PropagationDynamics(integrator.ForceList, _pointMassEarth, Frames.Frame.ICRF, Start, null,
            null, null, null);
        var step = segment.Steps[3];

        // Act and assert
        force.Count = 0;
        var (atBoundary, _) = dynamics.StateAt(segment, step.CumulativeTime + step.StepSize);
        Assert.Equal(0, force.Count);
        AssertSameBits(step.EndPosition, atBoundary, "state at the boundary");
        dynamics.StateAt(segment, step.CumulativeTime + 0.5 * step.StepSize);
        Assert.Equal(RK78ButcherTableau.Stages, force.Count);
        force.Count = 0;
        var (beforeStart, _) = dynamics.StateAt(segment, -1.0);
        var (afterEnd, _) = dynamics.StateAt(segment, segment.Duration + 1.0);
        Assert.Equal(0, force.Count);
        AssertSameBits(initial.Position, beforeStart, "state before the segment");
        AssertSameBits(segment.Steps[^1].EndPosition, afterEnd, "state after the segment");
    }

    [Fact]
    public void StateAt_OfAnEmptySegment_Throws()
    {
        var dynamics = new PropagationDynamics(Array.Empty<ForceBase>(), _pointMassEarth, Frames.Frame.ICRF, Start,
            null, null, null, null);
        Assert.Throws<InvalidOperationException>(() => dynamics.StateAt(new PropagationSegment(Start), 1.0));
        Assert.Throws<ArgumentNullException>(() => dynamics.StateAt(null, 1.0));
    }

    /// <summary>
    /// A maneuver at perigee in the two-body problem: the event is located on the integrated trajectory, at the perigee
    /// of the Keplerian orbit, and the trajectory restarts from the integrated state at the event, plus ΔV. The state at
    /// the event is the one from which the variational values at the event come (one shortened step), so the two are
    /// the same doubles.
    /// </summary>
    [Fact]
    public void AManeuverAtPerigee_IsLocatedAndRestartedOnTheIntegratedTrajectory()
    {
        // Arrange: 400 × 1000 km LEO from apogee, apogee raised to 2000 km at the first perigee, point-mass Earth
        var orbit = new StateVector(new Vector3(-7378137.0, 0.0, 0.0), new Vector3(0.0, -5509.9, -4623.4),
            _pointMassEarth, Start, Frames.Frame.ICRF);
        var (spacecraft, maneuver) = ManeuveringSpacecraft(orbit);
        double mu = _pointMassEarth.GM;
        var end = Start.AddHours(3.0);

        // Act
        var solution = spacecraft.Propagate(new TimeSystem.Window(Start, end), new CelestialItem[] { _pointMassEarth },
            new RK78Integrator(1e-11, 1e-11), false, false, TimeSpan.FromSeconds(60.0),
            new VariationalOptions(ForceParameters.None));

        // Assert: the event at the perigee, half a period after the apogee
        Assert.Equal(2, solution.Segments.Count);
        var before = solution.Segments[0];
        var after = solution.Segments[1];
        int k = before.Steps.Count - 1;
        double te = RK78Integrator.OnEpochGrid(Start, after.BaseEpoch, before.Steps[k].CumulativeTime,
            before.Duration);
        double semiMajorAxis = 1.0 / (2.0 / orbit.Position.Magnitude() - orbit.Velocity.MagnitudeSquared() / mu);
        double halfPeriod = System.Math.PI * System.Math.Sqrt(semiMajorAxis * semiMajorAxis * semiMajorAxis / mu);
        // Measured 6.6e-8 s: the bisection tolerance (1e-10 s), then the move to the 100 ns grid of the epochs
        _output.WriteLine($"event {te - halfPeriod:E3} s from the Keplerian perigee");
        Assert.True(System.Math.Abs(te - halfPeriod) < 2e-7, $"event at {te - halfPeriod:E3} s from the perigee");

        // The restart: the Keplerian state at the event, plus ΔV
        var (keplerAtEvent, _) = KeplerianStm.Propagate(mu, orbit.Position, orbit.Velocity, te);
        var restartPosition = after.Steps[0].StartPosition;
        var restartVelocity = after.Steps[0].StartVelocity;
        double positionError = (restartPosition - Position(keplerAtEvent)).Magnitude();
        double velocityError = (restartVelocity - maneuver.DeltaV - Velocity(keplerAtEvent)).Magnitude();
        // Measured 1.1e-5 m and 1.9e-9 m/s at the tolerance 1e-11, the error of the integration over 50 min; the
        // cubic Hermite state was off by metres (#363)
        _output.WriteLine($"restart against Kepler: {positionError:E3} m, {velocityError:E3} m/s");
        Assert.True(positionError < 1e-4, $"position {positionError:E3} m");
        Assert.True(velocityError < 1e-7, $"velocity {velocityError:E3} m/s");

        // The same doubles as the shortened step of the variational values at the event
        var (rkPosition, rkVelocity) = solution.Dynamics.ShortenedStep(before, k, te - before.Steps[k].CumulativeTime,
            new double[36], Array.Empty<double>());
        AssertSameBits(rkPosition, restartPosition, "position at the event");
        AssertSameBits(rkVelocity + maneuver.DeltaV, restartVelocity, "velocity after the maneuver");

        // After the maneuver: the Keplerian solution from the state at the event, plus ΔV
        var final = solution.StateVectors[^1];
        var (keplerFinal, _) = KeplerianStm.Propagate(mu, Position(keplerAtEvent),
            Velocity(keplerAtEvent) + maneuver.DeltaV, (final.Epoch - after.BaseEpoch).TotalSeconds);
        double finalError = (final.Position - Position(keplerFinal)).Magnitude();
        // Measured 8.4e-5 m after 2 h more
        _output.WriteLine($"last output against Kepler after the maneuver: {finalError:E3} m");
        Assert.True(finalError < 1e-3, $"last output {finalError:E3} m");
    }

    /// <summary>
    /// The same maneuver with a fixed-step RK7(8): the event is located on the integrated trajectory as well.
    /// </summary>
    [Fact]
    public void AManeuverAtPerigee_WithAFixedStep_IsLocatedOnTheIntegratedTrajectory()
    {
        // Arrange
        var orbit = new StateVector(new Vector3(-7378137.0, 0.0, 0.0), new Vector3(0.0, -5509.9, -4623.4),
            _pointMassEarth, Start, Frames.Frame.ICRF);
        var (spacecraft, _) = ManeuveringSpacecraft(orbit);
        double mu = _pointMassEarth.GM;

        // Act
        var solution = spacecraft.Propagate(new TimeSystem.Window(Start, Start.AddHours(2.0)),
            new CelestialItem[] { _pointMassEarth }, new RK78Integrator(30.0), false, false, TimeSpan.FromSeconds(60.0));

        // Assert: measured 6.6e-8 s from the Keplerian perigee
        var after = solution.Segments[1];
        double te = (after.BaseEpoch - Start).TotalSeconds;
        double semiMajorAxis = 1.0 / (2.0 / orbit.Position.Magnitude() - orbit.Velocity.MagnitudeSquared() / mu);
        double halfPeriod = System.Math.PI * System.Math.Sqrt(semiMajorAxis * semiMajorAxis * semiMajorAxis / mu);
        _output.WriteLine($"fixed step: event {te - halfPeriod:E3} s from the Keplerian perigee");
        Assert.True(System.Math.Abs(te - halfPeriod) < 2e-7, $"event at {te - halfPeriod:E3} s from the perigee");
        var (keplerAtEvent, _) = KeplerianStm.Propagate(mu, orbit.Position, orbit.Velocity, te);
        Assert.True((after.Steps[0].StartPosition - Position(keplerAtEvent)).Magnitude() < 1e-3);
    }

    /// <summary>
    /// The event time is moved to the epoch the propagator restarts from, which <see cref="TimeSystem.Time"/> holds to
    /// 100 ns, so that the state at the event and the epoch of the next segment are the same instant.
    /// </summary>
    [Theory]
    [InlineData(1234.56789012345)]
    [InlineData(0.3)]
    [InlineData(3000.0000000499)]
    [InlineData(2873.1416)]
    public void OnEpochGrid_ReturnsATimeThatAddsUpToTheEpoch(double t)
    {
        var epoch = Start.AddSeconds(t);
        double onGrid = RK78Integrator.OnEpochGrid(Start, epoch, 0.0, 4000.0);
        Assert.Equal(epoch, Start.AddSeconds(onGrid));
        Assert.True(System.Math.Abs(onGrid - t) < 1e-7);
    }

    [Fact]
    public void OnEpochGrid_StaysWithinTheStep()
    {
        // The epoch of 1.00000015 s is 1.0000001 s, before the start of the step, which has the same epoch
        var epoch = Start.AddSeconds(1.00000015);
        double onGrid = RK78Integrator.OnEpochGrid(Start, epoch, 1.00000012, 2.0);
        Assert.InRange(onGrid, 1.00000012, 2.0);
        Assert.Equal(epoch, Start.AddSeconds(onGrid));

        // No time of the step reaches an epoch before it: the clamped time
        Assert.Equal(1.5, RK78Integrator.OnEpochGrid(Start, Start.AddSeconds(1.0), 1.5, 2.0));
    }

    /// <summary>
    /// Velocity-Verlet keeps the cubic Hermite interpolation: its solution keeps no dynamics.
    /// </summary>
    [Fact]
    public void VelocityVerlet_KeepsTheHermiteInterpolation()
    {
        // Arrange
        var initial = PartialsTestCases.State(PartialsTestCases.Leo400, _pointMassEarth);
        var solution = PartialsTestCases.Spacecraft(initial).Propagate(new TimeSystem.Window(Start, Start.AddHours(1.0)),
            new CelestialItem[] { _pointMassEarth }, false, false, TimeSpan.FromSeconds(7.0));
        var epoch = Start.AddSeconds(1234.5);

        // Act
        var (position, velocity) = solution.InterpolateAt(epoch);

        // Assert
        Assert.Null(solution.Dynamics);
        var (hermitePosition, hermiteVelocity) = solution.Segments.Single().InterpolateAt(1234.5);
        AssertSameBits(hermitePosition, position, "position");
        AssertSameBits(hermiteVelocity, velocity, "velocity");
    }

    private (Spacecraft Spacecraft, ApogeeHeightManeuver Maneuver) ManeuveringSpacecraft(StateVector orbit)
    {
        var spacecraft = new Spacecraft(-1971, "DENSE", 1000.0, 3000.0, new Clock("dense", 65536), orbit);
        var tank = new FuelTank("densetank", "model", "sn1", 1000.0, 1000.0);
        var engine = new Engine("denseengine", "model", "sn1", 300.0, 10.0, tank);
        spacecraft.AddFuelTank(tank);
        spacecraft.AddEngine(engine);
        var maneuver = new ApogeeHeightManeuver(_pointMassEarth, Start, TimeSpan.Zero, 8378137.0, engine);
        spacecraft.SetStandbyManeuver(maneuver);
        return (spacecraft, maneuver);
    }

    private static double PositionError(double mu, StateVector initial, double t, Vector3 position)
    {
        var (state, _) = KeplerianStm.Propagate(mu, initial.Position, initial.Velocity, t);
        return (position - Position(state)).Magnitude();
    }

    private static double VelocityError(double mu, StateVector initial, double t, Vector3 velocity)
    {
        var (state, _) = KeplerianStm.Propagate(mu, initial.Position, initial.Velocity, t);
        return (velocity - Velocity(state)).Magnitude();
    }

    private static Vector3 Position(double[] state) => new(state[0], state[1], state[2]);

    private static Vector3 Velocity(double[] state) => new(state[3], state[4], state[5]);

    private static void AssertSameBits(Vector3 expected, Vector3 actual, string what)
    {
        Assert.True(BitConverter.DoubleToInt64Bits(expected.X) == BitConverter.DoubleToInt64Bits(actual.X) &&
                    BitConverter.DoubleToInt64Bits(expected.Y) == BitConverter.DoubleToInt64Bits(actual.Y) &&
                    BitConverter.DoubleToInt64Bits(expected.Z) == BitConverter.DoubleToInt64Bits(actual.Z),
            $"{what}: expected {expected}, got {actual}");
    }

    /// <summary>Point-mass gravity that counts its evaluations.</summary>
    private sealed class CountingForce : ForceBase
    {
        private readonly double _mu;

        internal CountingForce(double mu)
        {
            _mu = mu;
        }

        internal int Count { get; set; }

        public override Vector3 Apply(StateVector stateVector)
        {
            Count++;
            double r = stateVector.Position.Magnitude();
            return stateVector.Position * (-_mu / (r * r * r));
        }
    }
}
