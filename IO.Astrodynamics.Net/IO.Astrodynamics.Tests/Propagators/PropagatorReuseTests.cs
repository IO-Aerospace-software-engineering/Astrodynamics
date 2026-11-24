// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.Propagators.Variational;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Matrix = IO.Astrodynamics.Math.Matrix;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// Every <see cref="PropagatorBase.Propagate"/> on one propagator instance is a fresh run (#782, decisions 22 to 25 of lot
/// L4 of #668, decided by S. Guillet, 2026-11-22): the propagator initializes its integrator with its initial state at the
/// start of every run, before the re-arm of the spacecraft, so runs 2 and 3 of an instance are bit for bit those of a
/// fresh spacecraft and propagator; an integrator belongs to one propagator; the solutions of the runs of one instance share
/// its dynamics lock, which each run holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect.</b> Before the fix an adaptive <see cref="RK78Integrator"/> reset its step and the history of its PI
/// controller only in <see cref="IIntegrator.Initialize"/>, which the propagator called at construction and after each
/// event, never at the start of a run: the first segment of run 2 started from the last step of run 1. Measured by the gap
/// analysis of #782 (Linux x64): 1.7e-5 m to 2.2e-3 m on a point-mass LEO of 2 to 3 h, 0.24 m (run 2) and 0.35 m (run 3)
/// with EGM2008 10×10, the Moon, the Sun, NRLMSISE-00 and SRP over 6 h, the burn up to 0.9 µs late, every element of Φ and
/// P off up to 6.8e-5 relative, 2 steps against 13 and 1.53e-4 m in SSB mode, 38 m behind a bare <see cref="IIntegrator"/>.
/// </para>
/// <para>
/// <b>The reference.</b> A spacecraft built identically and never propagated, run once by a propagator built identically:
/// the state an instance keeps between two runs is the defect, so the instance that keeps nothing is the reference.
/// Compared bit for bit, never with a tolerance: the outputs (position, velocity, epoch, covariance), the steps of every
/// segment (start, size, end state), the stored states arc by arc, the attitude record as rotation angles (exactly zero),
/// the executed maneuvers (their windows to the tick, ΔV, fuel burned), the fuel left, and Φ and P at the window end.
/// </para>
/// <para>
/// <b>Guards green before the fix</b> (stated in the test names or summaries): Velocity-Verlet and fixed-step RK7(8) keep no
/// step state; a stop past the impulse left the integrator re-initialized by the event, so the rerun was fresh by chance; a
/// token already cancelled changes nothing (decision 6); the solution of run 1 is independent of the integrator.
/// </para>
/// </remarks>
public class PropagatorReuseTests
{
    private readonly ITestOutputHelper _output;

    public PropagatorReuseTests(ITestOutputHelper output)
    {
        TestKernels.EnsureSolarSystem();
        _output = output;
    }

    private static readonly TimeSystem.Time Epoch = new(2021, 1, 1, 12, 0, 0, TimeScale.TDB);
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(60.0);
    private static Window TwoHours => new(Epoch, Epoch.AddHours(2.0));
    private static Window ThreeHours => new(Epoch, Epoch.AddHours(3.0));

    private static int _nextId = -782000;
    private static int NextId() => Interlocked.Decrement(ref _nextId);

    private static readonly RK78IntegratorOptions Rk78 = new() { AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10 };

    // ---------------------------------------------------------------------------------------------------------------------
    // The cases
    // ---------------------------------------------------------------------------------------------------------------------

    private static CelestialBody Earth => PlanetsAndMoons.EARTH_BODY;

    private static Spacecraft Leo(bool covariance = false, bool forManeuver = false)
    {
        int id = NextId();
        var state = new StateVector(new Vector3(6700000.0, 0.0, 0.0),
            forManeuver ? new Vector3(0.0, 8000.0, 0.0) : new Vector3(0.0, 7713.0, 1000.0), Earth, Epoch, Frames.Frame.ICRF);
        if (covariance)
        {
            state = CovariancePropagationTests.WithCovariance(state);
        }

        return new Spacecraft(id, $"RU{-id}", 100.0, 10000.0, new Clock($"ruclk{-id}", 65536), state, 10.0, 2.2);
    }

    // A prograde attitude at +300 s held 5 min, then an apogee raise to 9 000 km at the first perigee after +600 s
    private static Spacecraft WithChain(Spacecraft spacecraft)
    {
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        var prograde = new ProgradeAttitude(Earth, Epoch.AddSeconds(300.0), TimeSpan.FromMinutes(5.0), engine);
        spacecraft.SetManeuvers(prograde, new ApogeeHeightManeuver(Earth, Epoch.AddSeconds(600.0), TimeSpan.Zero, 9000000.0, engine));
        return spacecraft;
    }

    private static Spacecraft Heliocentric()
    {
        var sun = Stars.SUN_BODY;
        var heliocentric = new StateVector(new Vector3(1.5e11, 0.0, 0.0), new Vector3(0.0, 29780.0, 1000.0), sun, Epoch,
            Frames.Frame.ICRF);
        var ssbState = heliocentric.RelativeTo(Barycenters.SOLAR_SYSTEM_BARYCENTER, Aberration.None).ToStateVector();
        int id = NextId();
        return new Spacecraft(id, $"RU{-id}", 100.0, 10000.0, new Clock($"ruclk{-id}", 65536), ssbState, 10.0, 2.2);
    }

    private static RK78Integrator CallerRk78(params ForceBase[] extra)
    {
        var integrator = new RK78Integrator(1e-10, 1e-10);
        integrator.AddForce(new GravitationalAcceleration(Earth));
        foreach (var force in extra)
        {
            integrator.AddForce(force);
        }

        return integrator;
    }

    private static readonly CovariancePropagationOptions Covariance = new() { Mode = CovariancePropagationMode.Covariance };

    private sealed record Case(PropagatorBase Propagator, Spacecraft Spacecraft, Window Window, bool Stm, bool Cov);

    /// <summary>A propagator and its spacecraft, built anew at each call.</summary>
    private static Case Make(string name)
    {
        var bodies = new CelestialItem[] { Earth, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY };
        switch (name)
        {
            case "PointMass":
            {
                var s = Leo();
                return new Case(new CentralBodyPropagator(TwoHours, s, new PropagationOptions
                {
                    Integrator = Rk78, OutputStep = Step, ThirdBodies = [Earth],
                    CovariancePropagation = CovariancePropagationOptions.None
                }), s, TwoHours, false, false);
            }
            case "FullModel":
            {
                // EGM2008 10×10, the Moon, the Sun, NRLMSISE-00 (the default atmosphere of a model-less Earth), SRP, a
                // prograde attitude and an apogee impulse
                var s = WithChain(Leo(forManeuver: true));
                return new Case(new CentralBodyPropagator(ThreeHours, s, new PropagationOptions
                {
                    Integrator = Rk78, OutputStep = Step, ThirdBodies = bodies,
                    Geopotential = new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
                    AtmosphericDrag = true, SolarRadiationPressure = true,
                    CovariancePropagation = CovariancePropagationOptions.None
                }), s, ThreeHours, false, false);
            }
            case "CovarianceExplicit":
            {
                var s = Leo(covariance: true);
                return new Case(new CentralBodyPropagator(TwoHours, s, new PropagationOptions
                {
                    Integrator = Rk78, OutputStep = Step, ThirdBodies = bodies, CovariancePropagation = Covariance
                }), s, TwoHours, true, true);
            }
            case "CovarianceAuto":
            {
                var s = Leo(covariance: true);
                return new Case(new CentralBodyPropagator(TwoHours, s, new PropagationOptions
                {
                    Integrator = Rk78, OutputStep = Step, ThirdBodies = bodies
                }), s, TwoHours, true, true);
            }
            case "CovarianceNoiseConsiderStepControl":
            {
                var s = Leo(covariance: true);
                return new Case(new CentralBodyPropagator(TwoHours, s, new PropagationOptions
                {
                    Integrator = Rk78, OutputStep = Step, ThirdBodies = bodies, AtmosphericDrag = true,
                    SolarRadiationPressure = true,
                    CovariancePropagation = new CovariancePropagationOptions
                    {
                        Mode = CovariancePropagationMode.Covariance,
                        ProcessNoise = new RtnProcessNoise(1e-12, 1e-11, 1e-12),
                        ConsiderParameters = new ConsiderParameters(dragCoefficientSigma: 0.1,
                            reflectivityCoefficientSigma: 0.05),
                        IncludeInStepControl = true
                    }
                }), s, TwoHours, true, true);
            }
            case "Ssb":
            {
                var s = Heliocentric();
                var window = new Window(Epoch, Epoch.AddDays(2.0));
                return new Case(new CentralBodyPropagator(window, s, new PropagationOptions
                {
                    Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 },
                    OutputStep = TimeSpan.FromMinutes(30.0), ThirdBodies = [Stars.SUN_BODY, Earth],
                    CovariancePropagation = CovariancePropagationOptions.None
                }), s, window, false, false);
            }
            case "CallerRk78":
            {
                var s = WithChain(Leo(forManeuver: true));
                return new Case(new CentralBodyPropagator(TwoHours, s, CallerRk78(), Step), s, TwoHours, false, false);
            }
            case "CallerRk78Stm":
            {
                var s = Leo();
                return new Case(new CentralBodyPropagator(TwoHours, s, CallerRk78(), Step,
                        new CovariancePropagationOptions { Mode = CovariancePropagationMode.StateTransitionOnly }), s,
                    TwoHours, true, false);
            }
            case "VelocityVerlet":
            {
                var s = WithChain(Leo(forManeuver: true));
                return new Case(new CentralBodyPropagator(TwoHours, s, new PropagationOptions
                {
                    Integrator = new VVIntegratorOptions { Step = Step }, OutputStep = Step, ThirdBodies = [Earth]
                }), s, TwoHours, false, false);
            }
            case "FixedRk78":
            {
                var s = WithChain(Leo(forManeuver: true));
                return new Case(new CentralBodyPropagator(TwoHours, s, new PropagationOptions
                {
                    Integrator = new RK78IntegratorOptions { FixedStep = Step }, OutputStep = Step, ThirdBodies = [Earth],
                    CovariancePropagation = CovariancePropagationOptions.None
                }), s, TwoHours, false, false);
            }
            default:
                throw new ArgumentException(name);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The snapshot of a run, compared bit for bit
    // ---------------------------------------------------------------------------------------------------------------------

    private sealed class Snapshot
    {
        public StateVector[] Outputs;
        public AcceptedStep[][] Steps;
        public StateVector[][] Arcs;
        public StateOrientation[] Attitudes;
        public (Type Type, Window? Maneuver, Window? Thrust, Vector3? DeltaV, double Fuel)[] Maneuvers;
        public double FuelLeft;
        public double Mass;
        public Matrix? PhiEnd;
        public Matrix? PEnd;
    }

    private static Snapshot Take(PropagationSolution solution, Spacecraft spacecraft, Window window, bool stm, bool cov) =>
        new()
        {
            Outputs = solution.StateVectors.ToArray(),
            Steps = solution.Segments.Select(segment => segment.Steps.ToArray()).ToArray(),
            Arcs = spacecraft.PropagatedArcs.Select(arc => arc.ToArray()).ToArray(),
            Attitudes = spacecraft.Frame.GetStateOrientationsToICRF().ToArray(),
            Maneuvers = spacecraft.ExecutedManeuvers.Select(m => (m.GetType(), m.ManeuverWindow, m.ThrustWindow,
                (m as ImpulseManeuver)?.DeltaV, m.FuelBurned)).ToArray(),
            FuelLeft = spacecraft.FuelTanks.Sum(tank => tank.Quantity),
            Mass = spacecraft.GetTotalMass(),
            PhiEnd = stm ? solution.GetStateTransitionMatrix(window.EndDate) : null,
            PEnd = cov ? solution.GetCovariance(window.EndDate) : null
        };

    private static bool Same(Vector3 a, Vector3 b) => a.X.Equals(b.X) && a.Y.Equals(b.Y) && a.Z.Equals(b.Z);

    private static bool Same(Window? a, Window? b) =>
        a.HasValue == b.HasValue &&
        (!a.HasValue || (a.Value.StartDate.EqualsExact(b.Value.StartDate) && a.Value.EndDate.EqualsExact(b.Value.EndDate)));

    private static bool Same(Matrix? a, Matrix? b) => a.HasValue == b.HasValue && (!a.HasValue || a.Value.Equals(b.Value));

    private static bool Same(StateVector a, StateVector b) =>
        Same(a.Position, b.Position) && Same(a.Velocity, b.Velocity) && a.Epoch.EqualsExact(b.Epoch) &&
        Same(a.Covariance, b.Covariance);

    // The rotation angle between two attitudes, exactly 0 for the same quaternion (the vector part of q q̄ cancels)
    private static double Angle(IO.Astrodynamics.Math.Quaternion a, IO.Astrodynamics.Math.Quaternion b)
    {
        var q = a * b.Conjugate();
        return 2.0 * System.Math.Atan2(q.VectorPart.Magnitude(), System.Math.Abs(q.W));
    }

    private static double MaxDistance(IReadOnlyList<StateVector> a, IReadOnlyList<StateVector> b)
    {
        double worst = 0.0;
        for (int i = 0; i < System.Math.Min(a.Count, b.Count); i++)
        {
            worst = System.Math.Max(worst, (a[i].Position - b[i].Position).Magnitude());
        }

        return worst;
    }

    private void AssertSame(string tag, Snapshot expected, Snapshot actual)
    {
        int outputs = expected.Outputs.Zip(actual.Outputs).Count(p => !Same(p.First, p.Second));
        int steps = expected.Steps.SelectMany(s => s).Zip(actual.Steps.SelectMany(s => s)).Count(p => p.First != p.Second);
        _output.WriteLine($"MEASURE PropagatorReuseTests {tag}: steps {string.Join("+", actual.Steps.Select(s => s.Length))} " +
                          $"against {string.Join("+", expected.Steps.Select(s => s.Length))}, {outputs}/{expected.Outputs.Length} " +
                          $"outputs differ, up to {MaxDistance(expected.Outputs, actual.Outputs):E3} m");

        Assert.Equal(expected.Steps.Select(s => s.Length), actual.Steps.Select(s => s.Length));
        Assert.True(steps == 0, $"{tag}: {steps} steps differ");
        Assert.Equal(expected.Outputs.Length, actual.Outputs.Length);
        Assert.True(outputs == 0, $"{tag}: {outputs} outputs differ");

        Assert.Equal(expected.Arcs.Select(a => a.Length), actual.Arcs.Select(a => a.Length));
        for (int arc = 0; arc < expected.Arcs.Length; arc++)
        {
            for (int i = 0; i < expected.Arcs[arc].Length; i++)
            {
                Assert.True(Same(expected.Arcs[arc][i], actual.Arcs[arc][i]), $"{tag}: stored state {i} of arc {arc}");
            }
        }

        Assert.Equal(expected.Attitudes.Length, actual.Attitudes.Length);
        for (int i = 0; i < expected.Attitudes.Length; i++)
        {
            Assert.True(expected.Attitudes[i].Epoch.EqualsExact(actual.Attitudes[i].Epoch), $"{tag}: attitude epoch {i}");
            double angle = Angle(expected.Attitudes[i].Rotation, actual.Attitudes[i].Rotation);
            Assert.True(angle == 0.0, $"{tag}: attitude {i} off by {angle:E3} rad");
        }

        Assert.Equal(expected.Maneuvers.Length, actual.Maneuvers.Length);
        for (int i = 0; i < expected.Maneuvers.Length; i++)
        {
            var (type, maneuver, thrust, deltaV, fuel) = expected.Maneuvers[i];
            Assert.Equal(type, actual.Maneuvers[i].Type);
            Assert.True(Same(maneuver, actual.Maneuvers[i].Maneuver), $"{tag}: maneuver window {i}");
            Assert.True(Same(thrust, actual.Maneuvers[i].Thrust), $"{tag}: thrust window {i}, the burn epoch");
            Assert.True(deltaV.HasValue == actual.Maneuvers[i].DeltaV.HasValue &&
                        (!deltaV.HasValue || Same(deltaV.Value, actual.Maneuvers[i].DeltaV.Value)), $"{tag}: ΔV {i}");
            Assert.Equal(fuel, actual.Maneuvers[i].Fuel);
        }

        Assert.Equal(expected.FuelLeft, actual.FuelLeft);
        Assert.Equal(expected.Mass, actual.Mass);
        Assert.True(Same(expected.PhiEnd, actual.PhiEnd), $"{tag}: Φ at the window end");
        Assert.True(Same(expected.PEnd, actual.PEnd), $"{tag}: P at the window end");
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Decision 22: runs 2 and 3 of one instance are fresh runs
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Runs 2 and 3 of one <see cref="CentralBodyPropagator"/> are bit for bit a fresh spacecraft and propagator, on every
    /// activation path of an adaptive RK7(8): the options constructor (point mass; the full model with an attitude and an
    /// impulse; the covariance asked for, under Auto, and with process noise, consider parameters and the step control;
    /// SSB mode at 1e-12) and a caller-built <see cref="RK78Integrator"/> on the <see cref="IIntegrator"/> constructors.
    /// Velocity-Verlet and fixed-step RK7(8) keep no step state: guards, green before the fix.
    /// </summary>
    [Theory]
    [InlineData("PointMass")]
    [InlineData("FullModel")]
    [InlineData("CovarianceExplicit")]
    [InlineData("CovarianceAuto")]
    [InlineData("CovarianceNoiseConsiderStepControl")]
    [InlineData("Ssb")]
    [InlineData("CallerRk78")]
    [InlineData("CallerRk78Stm")]
    [InlineData("VelocityVerlet")]
    [InlineData("FixedRk78")]
    public void RunsTwoAndThree_OfOneInstance_AreFreshRuns(string name)
    {
        // Arrange: the reference, a fresh spacecraft run once by a fresh propagator
        var fresh = Make(name);
        var expected = Take(fresh.Propagator.Propagate(), fresh.Spacecraft, fresh.Window, fresh.Stm, fresh.Cov);
        var reused = Make(name);
        var run1 = Take(reused.Propagator.Propagate(), reused.Spacecraft, reused.Window, reused.Stm, reused.Cov);

        // Act
        var run2 = Take(reused.Propagator.Propagate(), reused.Spacecraft, reused.Window, reused.Stm, reused.Cov);
        var run3 = Take(reused.Propagator.Propagate(), reused.Spacecraft, reused.Window, reused.Stm, reused.Cov);

        // Assert
        AssertSame($"{name} run 1", expected, run1);
        AssertSame($"{name} run 2", expected, run2);
        AssertSame($"{name} run 3", expected, run3);
        if (name is "FullModel" or "CallerRk78" or "VelocityVerlet" or "FixedRk78")
        {
            Assert.Equal(2, run2.Maneuvers.Length);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // After a stopped run
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>A force of zero that cancels its source after a number of calls while it is armed.</summary>
    private sealed class CancellingForce : ForceBase
    {
        private readonly CancellationTokenSource _source;
        private int _remaining;

        internal CancellingForce(CancellationTokenSource source, int calls)
        {
            _source = source;
            _remaining = calls;
        }

        internal bool Armed { get; set; } = true;

        public override Vector3 Apply(StateVector stateVector)
        {
            if (Armed && --_remaining == 0)
            {
                _source.Cancel();
            }

            return Vector3.Zero;
        }
    }

    /// <summary>
    /// A run stopped by its token, then run again on the same instance without a token, is bit for bit a fresh run: the
    /// stop inside the first segment left the step and the PI history of the stop before the fix (red); the stop past the
    /// impulse left the integrator re-initialized by the event, fresh by chance (a guard, green before the fix).
    /// </summary>
    [Theory]
    [InlineData("InsideTheFirstSegment", 100)]
    [InlineData("PastTheImpulse", 500)]
    public void AfterAStoppedRun_TheNextRunOfTheInstance_IsAFreshRun(string where, int calls)
    {
        // Arrange
        (CentralBodyPropagator Propagator, Spacecraft Spacecraft, CancellingForce Force, CancellationTokenSource Source) New()
        {
            var source = new CancellationTokenSource();
            var force = new CancellingForce(source, calls);
            var spacecraft = WithChain(Leo(forManeuver: true));
            return (new CentralBodyPropagator(TwoHours, spacecraft, CallerRk78(force), Step), spacecraft, force, source);
        }

        var fresh = New();
        fresh.Force.Armed = false;
        var expected = Take(fresh.Propagator.Propagate(), fresh.Spacecraft, TwoHours, false, false);
        var reused = New();
        Assert.Throws<OperationCanceledException>(() => reused.Propagator.Propagate(reused.Source.Token));
        bool impulseExecuted = reused.Spacecraft.ExecutedManeuvers.OfType<ImpulseManeuver>().Any();
        Assert.Equal(where == "PastTheImpulse", impulseExecuted);
        reused.Force.Armed = false;

        // Act
        var again = Take(reused.Propagator.Propagate(), reused.Spacecraft, TwoHours, false, false);

        // Assert
        AssertSame($"stopped {where}", expected, again);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // An integrator outside the library, a subclass of PropagatorBase
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>A bare <see cref="IIntegrator"/> that forwards to an RK7(8) and records the calls of Initialize.</summary>
    private sealed class RecordingIntegrator : IIntegrator
    {
        internal RecordingIntegrator(RK78Integrator inner)
        {
            Inner = inner;
        }

        private RK78Integrator Inner { get; }

        internal List<StateVector> Initializations { get; } = new();

        internal bool ThrowOnInitialize { get; set; }

        public void Initialize(StateVector initialState)
        {
            if (ThrowOnInitialize)
            {
                throw new InvalidOperationException("Initialize refused by the test.");
            }

            Initializations.Add(initialState);
            Inner.Initialize(initialState);
        }

        public IntegrationResult IntegrateSegment(Vector3 startPosition, Vector3 startVelocity, TimeSystem.Time baseEpoch,
            double duration, IReadOnlyList<IEventDetector> eventDetectors = null) =>
            Inner.IntegrateSegment(startPosition, startVelocity, baseEpoch, duration, eventDetectors);
    }

    private static StateVector InitialStateOf(PropagatorBase propagator) =>
        (StateVector)typeof(PropagatorBase).GetProperty("InitialState", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(propagator);

    /// <summary>
    /// A bare <see cref="IIntegrator"/> is initialized once at construction and once at the start of every run, each time
    /// with the initial state of the propagation (observer, frame, epoch, position and velocity); run 2 is a fresh run.
    /// Before the fix it was initialized at construction only, and run 2 was 38 m off (its outputs, a cubic Hermite of its
    /// steps, on another step grid).
    /// </summary>
    [Fact]
    public void ABareIIntegrator_IsInitializedAtTheStartOfEveryRun_AndRunTwoIsAFreshRun()
    {
        // Arrange
        var freshSpacecraft = Leo();
        var expected = Take(new CentralBodyPropagator(TwoHours, freshSpacecraft, new RecordingIntegrator(CallerRk78()), Step)
            .Propagate(), freshSpacecraft, TwoHours, false, false);
        var spacecraft = Leo();
        var recorder = new RecordingIntegrator(CallerRk78());
        var propagator = new CentralBodyPropagator(TwoHours, spacecraft, recorder, Step);
        propagator.Propagate();

        // Act
        var run2 = Take(propagator.Propagate(), spacecraft, TwoHours, false, false);

        // Assert: construction and two runs, no event
        var initial = InitialStateOf(propagator);
        Assert.Equal(3, recorder.Initializations.Count);
        foreach (var state in recorder.Initializations)
        {
            Assert.Same(initial.Observer, state.Observer);
            Assert.Equal(initial.Frame, state.Frame);
            Assert.True(initial.Epoch.EqualsExact(state.Epoch));
            Assert.True(Same(initial.Position, state.Position) && Same(initial.Velocity, state.Velocity));
        }

        AssertSame("bare IIntegrator run 2", expected, run2);
    }

    /// <summary>A propagator derived by a user (the pattern of <c>VariationalPropagationTests.MinimalPropagator</c>).</summary>
    private sealed class UserPropagator : PropagatorBase
    {
        internal UserPropagator(in Window window, Spacecraft spacecraft, IIntegrator integrator)
            : base(window, spacecraft, integrator, Step)
        {
            InitialState = spacecraft.InitialOrbitalParameters.ToStateVector();
            integrator.Initialize(InitialState);
        }

        protected override void StorePropagatedStates(StateVector[] outputStates)
        {
        }
    }

    /// <summary>
    /// Run 2 of a <see cref="PropagatorBase"/> subclass that sets <c>InitialState</c> in its constructor is a fresh run: the
    /// base initializes the integrator with it at the start of every run.
    /// </summary>
    [Fact]
    public void APropagatorBaseSubclass_RunTwoIsAFreshRun()
    {
        // Arrange
        var freshSpacecraft = Leo();
        var expected = Take(new UserPropagator(TwoHours, freshSpacecraft, CallerRk78()).Propagate(), freshSpacecraft,
            TwoHours, false, false);
        var spacecraft = Leo();
        var propagator = new UserPropagator(TwoHours, spacecraft, CallerRk78());
        propagator.Propagate();

        // Act
        var run2 = Take(propagator.Propagate(), spacecraft, TwoHours, false, false);

        // Assert
        AssertSame("PropagatorBase subclass run 2", expected, run2);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The placement of the initialization: after the token check, before the re-arm
    // ---------------------------------------------------------------------------------------------------------------------

    private static object Field(object instance, Type type, string name) =>
        type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance);

    private static (double Step, object PreviousError, object HasPreviousError, object EvalState) StateOf(
        RK78Integrator integrator)
    {
        var controller = Field(integrator, typeof(RK78Integrator), "_controller");
        var evalState = typeof(Integrator).GetProperty("EvalState", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(integrator);
        return ((double)Field(integrator, typeof(RK78Integrator), "_currentH"),
            Field(controller, controller.GetType(), "_prevError"), Field(controller, controller.GetType(), "_hasPreviousError"),
            evalState);
    }

    /// <summary>
    /// A token already cancelled leaves the integrator as the previous run left it (decision 6: the token is read before
    /// the initialization of the run): its step, the history of its controller and its evaluation state. A guard, green
    /// before the fix.
    /// </summary>
    [Fact]
    public void AnAlreadyCancelledToken_LeavesTheIntegratorUntouched()
    {
        // Arrange
        var reused = Make("PointMass");
        reused.Propagator.Propagate();
        var integrator = (RK78Integrator)reused.Propagator.Integrator;
        var before = StateOf(integrator);

        // Act
        Assert.Throws<OperationCanceledException>(() => reused.Propagator.Propagate(new CancellationToken(true)));

        // Assert
        var after = StateOf(integrator);
        Assert.Equal(before.Step, after.Step);
        Assert.Equal(before.PreviousError, after.PreviousError);
        Assert.Equal(before.HasPreviousError, after.HasPreviousError);
        Assert.Same(before.EvalState, after.EvalState);
    }

    /// <summary>
    /// An <see cref="IIntegrator.Initialize"/> that throws at the start of a run leaves the spacecraft as the previous run
    /// left it (the initialization comes before the re-arm): its stored states, attitude record, fuel and executed
    /// maneuvers (an integrator outside the library keeps no solution on the spacecraft). Before the fix no run initialized the integrator at its start: the run re-armed the spacecraft
    /// and threw at the re-initialization after the impulse, the spacecraft refuelled and half propagated.
    /// </summary>
    [Fact]
    public void AnInitializeThatThrows_LeavesTheSpacecraftUntouched()
    {
        // Arrange
        var spacecraft = WithChain(Leo(forManeuver: true));
        var recorder = new RecordingIntegrator(CallerRk78());
        var propagator = new CentralBodyPropagator(TwoHours, spacecraft, recorder, Step);
        var solution = propagator.Propagate();
        var before = Take(solution, spacecraft, TwoHours, false, false);
        var executed = spacecraft.ExecutedManeuvers.ToArray();
        recorder.ThrowOnInitialize = true;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => propagator.Propagate());

        // Assert
        Assert.Equal("Initialize refused by the test.", exception.Message);
        Assert.True(spacecraft.IsPropagated);
        Assert.Equal(executed, spacecraft.ExecutedManeuvers.ToArray());
        AssertSame("after an Initialize that throws", before, Take(solution, spacecraft, TwoHours, false, false));
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Decision 24: an integrator belongs to one propagator
    // ---------------------------------------------------------------------------------------------------------------------

    private static Spacecraft AboutTheMoon()
    {
        int id = NextId();
        var state = new StateVector(new Vector3(2000000.0, 0.0, 0.0), new Vector3(0.0, 1600.0, 0.0),
            PlanetsAndMoons.MOON_BODY, Epoch, Frames.Frame.ICRF);
        return new Spacecraft(id, $"RU{-id}", 100.0, 10000.0, new Clock($"ruclk{-id}", 65536), state, 10.0, 2.2);
    }

    /// <summary>
    /// A second propagator given an integrator already given to a propagator throws <see cref="ArgumentException"/> naming
    /// <c>integrator</c>, on each constructor that takes an integrator, before it changes the integrator: the first
    /// propagator, about the Earth (TT), runs bit for bit a fresh one although the second is about the Moon (TDB). Before
    /// the fix the second constructor re-initialized the integrator about the Moon and set its time argument to TDB, and
    /// the Earth run was 5.16e7 m off.
    /// </summary>
    [Theory]
    [InlineData("IIntegrator")]
    [InlineData("IIntegratorCovariance")]
    [InlineData("PropagatorBase")]
    [InlineData("Avoidance")]
    public void AnIntegratorGivenToAPropagator_IsRefusedByASecond(string constructor)
    {
        // Arrange
        var freshSpacecraft = Leo();
        var expected = Take(new CentralBodyPropagator(TwoHours, freshSpacecraft, CallerRk78(), Step).Propagate(),
            freshSpacecraft, TwoHours, false, false);
        var spacecraft = Leo();
        var integrator = CallerRk78();
        var first = new CentralBodyPropagator(TwoHours, spacecraft, integrator, Step);
        var other = AboutTheMoon();

        // Act
        var exception = Assert.Throws<ArgumentException>(() => constructor switch
        {
            "IIntegrator" => new CentralBodyPropagator(TwoHours, other, integrator, Step),
            "IIntegratorCovariance" => new CentralBodyPropagator(TwoHours, other, integrator, Step,
                new CovariancePropagationOptions { Mode = CovariancePropagationMode.StateTransitionOnly }),
            "PropagatorBase" => new UserPropagator(TwoHours, other, integrator),
            _ => (PropagatorBase)new CentralBodyPropagator(TwoHours, other, integrator, Step, null, null, new object())
        });

        // Assert
        Assert.Equal("integrator", exception.ParamName);
        AssertClaimMessage(exception);
        Assert.Same(PropagationTimeArgument.Tt, integrator.TimeArgument);
        Assert.Same(integrator, first.Integrator);
        AssertSame($"first propagator after the refusal of {constructor}", expected,
            Take(first.Propagate(), spacecraft, TwoHours, false, false));
    }

    // ELARA (507): named by SPICE, no BODY507_GM in gm_de440.tpc, so its kernel GM is 0 and a propagation about it is
    // refused (R03 of #668), after the claim of the integrator
    private static Spacecraft AboutABodyWithoutGm()
    {
        var elara = new CelestialBody(507);
        Assert.Equal(0.0, elara.GM);
        int id = NextId();
        var state = new StateVector(new Vector3(100000.0, 0.0, 0.0), new Vector3(0.0, 10.0, 0.0), elara, Epoch,
            Frames.Frame.ICRF);
        return new Spacecraft(id, $"RU{-id}", 100.0, 10000.0, new Clock($"ruclk{-id}", 65536), state, 10.0, 2.2);
    }

    /// <summary>
    /// A constructor that takes an integrator and is rejected by a check made after its claim, the GM of the central body
    /// or the covariance options, leaves the integrator claimed (decision 27 of lot L4 of #668, decided by S. Guillet,
    /// 2026-11-22): the claim is never released, as <c>ClaimForForces</c>, so a second propagator given that integrator
    /// throws <see cref="ArgumentException"/> naming <c>integrator</c>, and the caller builds a new integrator.
    /// </summary>
    [Theory]
    [InlineData("GM 0")]
    [InlineData("covariance options")]
    public void AConstructorRejectedAfterTheClaim_LeavesTheIntegratorClaimed(string check)
    {
        // Arrange
        var integrator = CallerRk78();

        // Act
        if (check == "GM 0")
        {
            var gm = Assert.Throws<ArgumentException>(() =>
                new CentralBodyPropagator(TwoHours, AboutABodyWithoutGm(), integrator, Step));
            Assert.Equal("spacecraft", gm.ParamName);
            Assert.Contains("BODY507_GM", gm.Message);
        }
        else
        {
            // The covariance is asked for, and the initial orbital parameters carry none
            Assert.Throws<InvalidOperationException>(() =>
                new CentralBodyPropagator(TwoHours, Leo(), integrator, Step, Covariance));
        }

        var exception = Assert.Throws<ArgumentException>(() => new CentralBodyPropagator(TwoHours, Leo(), integrator, Step));

        // Assert
        Assert.Equal("integrator", exception.ParamName);
        AssertClaimMessage(exception);
    }

    // The message of the claim names both cases, a propagator and a constructor that threw after the claim, and says the
    // claim is never released (decision 29 of lot L4 of #668, decided by S. Guillet, 2026-11-22)
    private static void AssertClaimMessage(ArgumentException exception)
    {
        Assert.Contains("already given to another propagator", exception.Message);
        Assert.Contains("or to a propagator constructor that threw after claiming it", exception.Message);
        Assert.Contains("the claim is never released", exception.Message);
        Assert.Contains("build a new integrator", exception.Message);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Decision 25: one dynamics lock per propagator instance
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>A force of zero that records whether the thread holds a lock at each call while it watches.</summary>
    private sealed class LockProbeForce : ForceBase
    {
        internal object Lock { get; set; }

        internal int Calls { get; private set; }

        internal int CallsWithoutTheLock { get; private set; }

        public override Vector3 Apply(StateVector stateVector)
        {
            if (Lock != null)
            {
                Calls++;
                if (!Monitor.IsEntered(Lock))
                {
                    CallsWithoutTheLock++;
                }
            }

            return Vector3.Zero;
        }
    }

    /// <summary>
    /// The solutions of the runs of one instance share one dynamics lock, that of their force model too, under RK7(8) and
    /// Velocity-Verlet, and each run holds it while it evaluates the forces, so the evaluations of the solution of an
    /// earlier run and a later run serialize. Before the fix each run took a new lock.
    /// </summary>
    [Theory]
    [InlineData("RK78")]
    [InlineData("VV")]
    public void TheRunsOfOneInstance_ShareOneLock_AndHoldIt(string kind)
    {
        // Arrange
        var probe = new LockProbeForce();
        Integrator integrator = kind == "RK78" ? CallerRk78(probe) : new VVIntegrator(Step);
        if (kind == "VV")
        {
            integrator.AddForce(new GravitationalAcceleration(Earth));
            integrator.AddForce(probe);
        }

        var propagator = new CentralBodyPropagator(TwoHours, Leo(), integrator, Step);
        var solution1 = propagator.Propagate();
        probe.Lock = solution1.ForceModel.SyncRoot;

        // Act
        var solution2 = propagator.Propagate();

        // Assert
        Assert.Same(solution1.ForceModel.SyncRoot, solution2.ForceModel.SyncRoot);
        if (kind == "RK78")
        {
            Assert.Same(solution1.ForceModel.SyncRoot, solution1.Dynamics.SyncRoot);
            Assert.Same(solution1.Dynamics.SyncRoot, solution2.Dynamics.SyncRoot);
        }

        Assert.True(probe.Calls > 0);
        Assert.Equal(0, probe.CallsWithoutTheLock);
    }

    /// <summary>
    /// The solution of run 1, evaluated after run 2 of its instance, gives bit for bit the values it gave before: the state,
    /// Φ and the covariance at mid-window. A guard: each solution has its own stepper, green before the fix.
    /// </summary>
    [Fact]
    public void TheSolutionOfRunOne_AfterRunTwo_GivesTheSameValues()
    {
        // Arrange
        var reused = Make("CovarianceExplicit");
        var solution1 = reused.Propagator.Propagate();
        var mid = Epoch.AddMinutes(47.3);
        var (position, velocity) = solution1.InterpolateAt(mid);
        var phi = solution1.GetStateTransitionMatrix(mid);
        var covariance = solution1.GetCovariance(mid);

        // Act
        reused.Propagator.Propagate();

        // Assert
        var (positionAfter, velocityAfter) = solution1.InterpolateAt(mid);
        Assert.True(Same(position, positionAfter) && Same(velocity, velocityAfter));
        Assert.Equal(phi, solution1.GetStateTransitionMatrix(mid));
        Assert.Equal(covariance, solution1.GetCovariance(mid));
    }

    /// <summary>
    /// A force of zero that, on the thread it watches, counts its calls and holds the first one until it is released, so
    /// that a run of the propagator stops inside its dynamics, the lock of the instance held.
    /// </summary>
    private sealed class GateForce : ForceBase
    {
        private int _watchedThread = -1;
        private int _callsOnWatchedThread;

        internal ManualResetEventSlim Entered { get; } = new();

        internal ManualResetEventSlim Released { get; } = new();

        internal int CallsOnWatchedThread => Volatile.Read(ref _callsOnWatchedThread);

        internal void WatchCurrentThread() => Volatile.Write(ref _watchedThread, Environment.CurrentManagedThreadId);

        public override Vector3 Apply(StateVector stateVector)
        {
            if (Environment.CurrentManagedThreadId == Volatile.Read(ref _watchedThread))
            {
                Interlocked.Increment(ref _callsOnWatchedThread);
                Entered.Set();
                Released.Wait();
            }

            return Vector3.Zero;
        }
    }

    /// <summary>
    /// The lock of an instance is held for the whole run, its forces included (decision 28 of lot L4 of #668, decided by
    /// S. Guillet, 2026-11-22): an evaluation of the solution of run 1 on another thread, asked while run 2 is held inside
    /// a force, returns only after run 2 has evaluated its last force, and gives the values it gave before run 2. Before
    /// the fix (1dc77ffc) each run took a new lock, and the evaluation returned while run 2 was held.
    /// </summary>
    [Fact]
    public void AnEvaluationOfRunOne_WaitsForTheEndOfRunTwo()
    {
        // Arrange
        var gate = new GateForce();
        var propagator = new CentralBodyPropagator(TwoHours, Leo(), CallerRk78(gate), Step);
        var solution1 = propagator.Propagate();
        var mid = Epoch.AddMinutes(47.3);
        var (position, velocity) = solution1.InterpolateAt(mid);
        var wait = TimeSpan.FromSeconds(60.0);

        // Act
        var run2 = new Thread(() =>
        {
            gate.WatchCurrentThread();
            propagator.Propagate();
        });
        run2.Start();
        Assert.True(gate.Entered.Wait(wait), "run 2 never reached its forces");

        bool releasedWhenReturned = false;
        int run2CallsWhenReturned = -1;
        Vector3 positionDuring = default, velocityDuring = default;
        var evaluationAsked = new ManualResetEventSlim();
        var evaluation = new Thread(() =>
        {
            evaluationAsked.Set();
            (positionDuring, velocityDuring) = solution1.InterpolateAt(mid);
            releasedWhenReturned = gate.Released.IsSet;
            run2CallsWhenReturned = gate.CallsOnWatchedThread;
        });
        evaluation.Start();
        Assert.True(evaluationAsked.Wait(wait));

        // Long enough for an evaluation that does not wait to return, which takes milliseconds
        bool returnedWhileHeld = evaluation.Join(TimeSpan.FromSeconds(1.0));
        gate.Released.Set();
        Assert.True(run2.Join(wait), "run 2 did not end");
        Assert.True(evaluation.Join(wait), "the evaluation did not return");

        // Assert
        Assert.False(returnedWhileHeld);
        Assert.True(releasedWhenReturned);
        Assert.True(gate.CallsOnWatchedThread > 1);
        Assert.Equal(gate.CallsOnWatchedThread, run2CallsWhenReturned);
        Assert.True(Same(position, positionDuring) && Same(velocity, velocityDuring));
    }
}
