// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
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
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// The constructors of <see cref="CentralBodyPropagator"/> that take an integrator with its forces initialize it with
/// the initial state of the propagation: before, a caller who did not call <see cref="IIntegrator.Initialize"/> got a
/// <see cref="NullReferenceException"/> from the first step (no observer, frame, evaluation state or RK7(8) stages). The
/// propagator initializes it again at the start of every run (#782, decisions 22 and 23 of lot L4 of #668, decided by
/// S. Guillet, 2026-11-22), so an <see cref="IIntegrator.Initialize"/> by the caller between construction and
/// <see cref="PropagatorBase.Propagate"/> has no effect on the run, under both integrators.
/// </summary>
/// <remarks>
/// The last state, and Φ when it is propagated, are checked against the two-body closed form (<see cref="KeplerianStm"/>)
/// over the seconds of the time argument, TT about the Earth, TDB about the Sun; and bit for bit against the same run
/// with an <see cref="IIntegrator.Initialize"/> by the caller after construction, with the initial state (the pattern of
/// the tests before 11.0.0, whose results the existing goldens establish) or with another frame or observer: the run
/// without it is the reference. Before #782 a caller's Initialize after construction replaced the propagator's for the
/// first segment: with another frame or observer it changed the run (2.941e4 m over 2 h in ECLIPJ2000 under EGM2008
/// 10×10, 1.265e6 m over 2 days with an SSB-centred state, both integrators, Linux x64).
/// </remarks>
public class IntegratorInitializationTests
{
    private readonly ITestOutputHelper _output;

    public IntegratorInitializationTests(ITestOutputHelper output)
    {
        TestKernels.EnsureSolarSystem();
        _output = output;
    }

    private static readonly TimeSystem.Time Epoch = new(2021, 1, 1, 12, 0, 0, TimeScale.TDB);
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(60.0);
    private static Window OneHour => new(Epoch, Epoch.AddHours(1.0));

    private static int _nextId = -668100;
    private static int NextId() => Interlocked.Decrement(ref _nextId);

    // Thresholds twice the worst of the MEASURE lines over Linux x64, Windows x64 and macOS arm64, the same on the three
    // (CI run 43745883709, 2026-11-15), rounded up at the second significant digit. RK7(8) at 1e-10 about the Earth: 9.947e-5 m (ICRF, with and without Φ, and ECLIPJ2000); at 1e-12
    // about the Sun over a day: 1.221e-4 m; Velocity-Verlet at 1 s: 15.89 m, its truncation (1589 m at 10 s, h²)
    private const double Rk78KeplerThreshold = 2.0e-4;
    private const double SsbKeplerThreshold = 2.5e-4;
    private const double VvKeplerThreshold = 3.2e1;
    // Φ of RK7(8) at 1e-10 at the end of the hour against the two-body closed form, the worst relative 3×3 block:
    // 6.970e-11 on each of Linux x64, Windows x64 and macOS arm64 (CI run 43745883709, 2026-11-15); twice that, rounded
    // up at the second significant digit
    private const double Rk78PhiThreshold = 1.4e-10;

    private static Spacecraft NewSpacecraft(ILocalizable observer, Frames.Frame frame, Vector3 position, Vector3 velocity)
    {
        int id = NextId();
        return new Spacecraft(id, $"II{-id}", 100.0, new StateVector(position, velocity, observer, Epoch, frame), 10000.0,
            new Clock($"iiclk{-id}", 65536), 10.0, 2.2);
    }

    private static Spacecraft NewLeo(CelestialBody earth, Frames.Frame frame = null) =>
        NewSpacecraft(earth, frame ?? Frames.Frame.ICRF, new Vector3(6700000.0, 0.0, 0.0), new Vector3(0.0, 7713.0, 1000.0));

    private static Integrator NewIntegrator(string kind, CelestialItem body)
    {
        Integrator integrator = kind == "RK78"
            ? new RK78Integrator(1e-10, 1e-10)
            : new VVIntegrator(TimeSpan.FromSeconds(1.0));
        integrator.AddForce(new GravitationalAcceleration(body));
        return integrator;
    }

    private static readonly CovariancePropagationOptions StateTransitionOnly =
        new() { Mode = CovariancePropagationMode.StateTransitionOnly };

    private static CentralBodyPropagator NewPropagator(Window window, Spacecraft spacecraft, IIntegrator integrator,
        bool stm) =>
        stm
            ? new CentralBodyPropagator(window, spacecraft, integrator, Step, StateTransitionOnly)
            : new CentralBodyPropagator(window, spacecraft, integrator, Step);

    // The distance of the state at the end of the window from the two-body orbit of the initial state after the seconds
    // of the time argument
    private static double KeplerError(StateVector initial, PropagationSolution solution, Window window, double elapsed)
    {
        var (state, _) = KeplerianStm.Propagate(((CelestialItem)initial.Observer).GM, initial.Position, initial.Velocity,
            elapsed);
        var (position, _) = solution.InterpolateAt(window.EndDate);
        return (position - new Vector3(state[0], state[1], state[2])).Magnitude();
    }

    private static void AssertSameBits(Vector3 expected, Vector3 actual, string label)
    {
        Assert.True(expected.X.Equals(actual.X) && expected.Y.Equals(actual.Y) && expected.Z.Equals(actual.Z),
            $"{label}: {expected} vs {actual}");
    }

    public static TheoryData<string, bool> Configurations() => new()
    {
        { "RK78", false },
        { "RK78", true },
        { "VV", false }
    };

    [Theory]
    [MemberData(nameof(Configurations))]
    public void WithoutAnInitializeByTheCaller_ThePropagationRuns_AndFollowsKepler(string kind, bool stm)
    {
        // Arrange: the integrator holds the point mass of the central body; the caller never initializes it
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var spacecraft = NewLeo(earth);
        var initial = (StateVector)spacecraft.InitialOrbitalParameters;
        var window = OneHour;
        var propagator = NewPropagator(window, spacecraft, NewIntegrator(kind, earth), stm);

        // Act
        var solution = propagator.Propagate();

        // Assert: about the Earth over the TT seconds of the window
        double elapsed = PropagationTimeArgument.Tt.SecondsBetween(window.StartDate, window.EndDate);
        double error = KeplerError(initial, solution, window, elapsed);
        _output.WriteLine($"MEASURE IntegratorInitializationTests {kind} stm={stm}: Kepler {error:E3} m");
        Assert.True(error <= (kind == "RK78" ? Rk78KeplerThreshold : VvKeplerThreshold), $"{error:E3} m");
        if (stm)
        {
            // Φ of the run against Φ of the two-body closed form after the same TT seconds, per 3×3 block
            var (_, expected) = KeplerianStm.Propagate(earth.GM, initial.Position, initial.Velocity, elapsed);
            double phiError = StmMeasures.WorstBlockError(StmMeasures.Elements(solution.GetStateTransitionMatrix(window.EndDate)),
                expected);
            _output.WriteLine($"MEASURE IntegratorInitializationTests {kind} stm={stm}: Φ worst block {phiError:E3}");
            Assert.True(phiError <= Rk78PhiThreshold, $"Φ: {phiError:E3}");
        }
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ACallerInitializeWithTheInitialState_ChangesNothing(string kind, bool stm)
    {
        // Arrange
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var window = OneHour;
        var implicitSpacecraft = NewLeo(earth);
        var explicitSpacecraft = NewLeo(earth);
        var explicitIntegrator = NewIntegrator(kind, earth);

        // Act
        NewPropagator(window, implicitSpacecraft, NewIntegrator(kind, earth), stm).Propagate();

        var explicitPropagator = NewPropagator(window, explicitSpacecraft, explicitIntegrator, stm);
        explicitIntegrator.Initialize(explicitSpacecraft.InitialOrbitalParameters.ToStateVector());
        explicitPropagator.Propagate();

        // Assert
        Assert.Equal(explicitSpacecraft.PropagatedStates.Length, implicitSpacecraft.PropagatedStates.Length);
        for (int i = 0; i < implicitSpacecraft.PropagatedStates.Length; i++)
        {
            AssertSameBits(explicitSpacecraft.PropagatedStates[i].Position, implicitSpacecraft.PropagatedStates[i].Position,
                $"state {i}");
            AssertSameBits(explicitSpacecraft.PropagatedStates[i].Velocity, implicitSpacecraft.PropagatedStates[i].Velocity,
                $"velocity {i}");
        }
    }

    [Fact]
    public void TheRun_IsBitForBitTheForceBuildingConstructorWithTheSameForce()
    {
        // Arrange: the force-building constructor builds the point mass of the central body alone
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var window = OneHour;
        var given = NewLeo(earth);
        var built = NewLeo(earth);

        // Act
        new CentralBodyPropagator(window, given, NewIntegrator("RK78", earth), Step).Propagate();

        new CentralBodyPropagator(window, built, new PropagationOptions { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10 }, OutputStep = Step, ThirdBodies = [earth], CovariancePropagation = CovariancePropagationOptions.None }).Propagate();

        // Assert
        Assert.Equal(built.PropagatedStates.Length, given.PropagatedStates.Length);
        AssertSameBits(built.PropagatedStates[^1].Position, given.PropagatedStates[^1].Position, "last state");
    }

    /// <summary>A bare implementation of <see cref="IIntegrator"/> that records the calls of Initialize.</summary>
    private sealed class RecordingIntegrator : IIntegrator
    {
        internal RecordingIntegrator(RK78Integrator inner)
        {
            Inner = inner;
        }

        internal RK78Integrator Inner { get; }

        internal List<StateVector> Initializations { get; } = new();

        public void Initialize(StateVector initialState)
        {
            Initializations.Add(initialState);
            Inner.Initialize(initialState);
        }

        public IntegrationResult IntegrateSegment(Vector3 startPosition, Vector3 startVelocity, TimeSystem.Time baseEpoch,
            double duration, IReadOnlyList<IEventDetector> eventDetectors = null) =>
            Inner.IntegrateSegment(startPosition, startVelocity, baseEpoch, duration, eventDetectors);
    }

    [Fact]
    public void TheConstructorAndEveryRun_Initialize_WithTheCentralBodyTheFrameAndTheTdbStart()
    {
        // Arrange
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var spacecraft = NewLeo(earth);
        var recorder = new RecordingIntegrator((RK78Integrator)NewIntegrator("RK78", earth));
        // A window given in UTC: the initial state is at its TDB start
        var window = new Window(Epoch.ToUtc(), Epoch.ToUtc().AddHours(1.0));

        // Act
        var propagator = new CentralBodyPropagator(window, spacecraft, recorder, Step);
        var atConstruction = recorder.Initializations.ToArray();
        propagator.Propagate();

        // Assert: once by the constructor, once at the start of the run, and not again without an event (#782)
        Assert.Single(atConstruction);
        Assert.Equal(2, recorder.Initializations.Count);
        foreach (var state in recorder.Initializations)
        {
            Assert.Same(earth, state.Observer);
            Assert.Equal(Frames.Frame.ICRF, state.Frame);
            Assert.Equal(TimeScale.TDB, state.Epoch.Scale);
            LabelAssert.Equal(window.StartDate.ToTdb(), state.Epoch);
        }

        Assert.NotEmpty(spacecraft.PropagatedStates);
    }

    [Fact]
    public void SsbMode_InitializesAboutTheSun_AndFollowsKeplerOverTdbSeconds()
    {
        // Arrange: a heliocentric orbit given about the solar system barycenter, whose central body is the Sun
        var sun = Stars.SUN_BODY;
        var heliocentric = new StateVector(new Vector3(1.5e11, 0.0, 0.0), new Vector3(0.0, 29780.0, 1000.0), sun, Epoch,
            Frames.Frame.ICRF);
        var ssbState = heliocentric.RelativeTo(Barycenters.SOLAR_SYSTEM_BARYCENTER, Aberration.None).ToStateVector();
        int id = NextId();
        var spacecraft = new Spacecraft(id, $"II{-id}", 100.0, ssbState, 10000.0, new Clock($"iiclk{-id}", 65536), 10.0, 2.2);
        var recorder = new RecordingIntegrator(new RK78Integrator(1e-12, 1e-12));
        recorder.Inner.AddForce(new GravitationalAcceleration(sun));
        var window = new Window(Epoch, Epoch.AddDays(1.0));

        // Act
        var propagator = new CentralBodyPropagator(window, spacecraft, recorder, TimeSpan.FromMinutes(10.0));
        var solution = propagator.Propagate();

        // Assert: initialized about the Sun, as the propagator states the initial orbit, at construction and at the start
        // of the run (#782), and Kepler about the Sun over the TDB seconds of the window
        Assert.Equal(2, recorder.Initializations.Count);
        Assert.All(recorder.Initializations, state => Assert.Same(sun, state.Observer));
        var initial = ssbState.RelativeTo(sun, Aberration.None).ToStateVector();
        double elapsed = PropagationTimeArgument.Tdb.SecondsBetween(window.StartDate, window.EndDate);
        double error = KeplerError(initial, solution, window, elapsed);
        _output.WriteLine($"MEASURE IntegratorInitializationTests SSB: Kepler {error:E3} m");
        Assert.True(error <= SsbKeplerThreshold, $"{error:E3} m");
    }

    [Fact]
    public void ANonIcrfInertialState_IsIntegratedInItsFrame()
    {
        // Arrange: the initial state in ECLIPJ2000, the frame of the integration (#529)
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var spacecraft = NewLeo(earth, Frames.Frame.ECLIPJ2000);
        var initial = (StateVector)spacecraft.InitialOrbitalParameters;
        var recorder = new RecordingIntegrator((RK78Integrator)NewIntegrator("RK78", earth));
        var window = OneHour;

        // Act
        var propagator = new CentralBodyPropagator(window, spacecraft, recorder, Step);
        var solution = propagator.Propagate();

        // Assert
        Assert.Equal(2, recorder.Initializations.Count);
        Assert.All(recorder.Initializations, state => Assert.Equal(Frames.Frame.ECLIPJ2000, state.Frame));
        double elapsed = PropagationTimeArgument.Tt.SecondsBetween(window.StartDate, window.EndDate);
        double error = KeplerError(initial, solution, window, elapsed);
        _output.WriteLine($"MEASURE IntegratorInitializationTests ECLIPJ2000: Kepler {error:E3} m");
        Assert.True(error <= Rk78KeplerThreshold, $"{error:E3} m");
    }

    [Theory]
    [InlineData("RK78")]
    [InlineData("VV")]
    public void WithAnImpulse_ACallerInitializeWithTheInitialState_ChangesNothing(string kind)
    {
        // Arrange: an apogee raise at the next perigee, about 1.6 h after the start; the integrator re-initialized after
        // the event as before
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var window = new Window(Epoch, Epoch.AddHours(2.0));

        Spacecraft WithManeuver()
        {
            var spacecraft = NewSpacecraft(earth, Frames.Frame.ICRF, new Vector3(6700000.0, 0.0, 0.0),
                new Vector3(0.0, 8000.0, 0.0));
            var tank = new FuelTank("ft", 1000.0, 1000.0, "model", "sn");
            spacecraft.AddFuelTank(tank);
            var engine = new Engine("eng", 300.0, 10.0, tank, "model", "sn");
            spacecraft.AddEngine(engine);
            spacecraft.SetManeuvers(new ApogeeHeightManeuver(9000000.0,
                engine,
                Epoch.AddSeconds(600.0),
                TimeSpan.Zero,
                earth));
            return spacecraft;
        }

        var implicitSpacecraft = WithManeuver();
        var explicitSpacecraft = WithManeuver();
        var explicitIntegrator = NewIntegrator(kind, earth);

        // Act
        new CentralBodyPropagator(window, implicitSpacecraft, NewIntegrator(kind, earth), Step).Propagate();

        var explicitPropagator = new CentralBodyPropagator(window, explicitSpacecraft, explicitIntegrator, Step);
        explicitIntegrator.Initialize(explicitSpacecraft.InitialOrbitalParameters.ToStateVector());
        explicitPropagator.Propagate();

        // Assert: the impulse fired, and every state is the same
        Assert.Single(implicitSpacecraft.ExecutedManeuvers);
        Assert.Equal(explicitSpacecraft.PropagatedStates.Length, implicitSpacecraft.PropagatedStates.Length);
        for (int i = 0; i < implicitSpacecraft.PropagatedStates.Length; i++)
        {
            AssertSameBits(explicitSpacecraft.PropagatedStates[i].Position, implicitSpacecraft.PropagatedStates[i].Position,
                $"state {i}");
        }
    }

    /// <summary>
    /// An <see cref="IIntegrator.Initialize"/> by the caller after construction with another frame or another observer
    /// than the propagation's has no effect on the run, under RK7(8) and Velocity-Verlet (#782, decision 23 of lot L4 of
    /// #668): the propagator initializes its integrator with its initial state at the start of every run. ECLIPJ2000 on an
    /// ICRF propagation about an EGM2008 10×10 Earth over 2 h (the options constructor); the state about the solar system
    /// barycenter of a spacecraft in SSB mode, propagated about the Sun by an integrator holding the point masses of the
    /// Sun and the Earth (the constructor of an integrator with its forces), over 2 days. Before the fix the caller's state governed the
    /// first segment, a whole run without event: 2.941e4 m over 2 h in ECLIPJ2000, 1.265e6 m over 2 days in SSB mode, under
    /// both integrators (these cases on Linux x64 at 1dc77ffc).
    /// </summary>
    [Theory]
    [InlineData("RK78", "ECLIPJ2000")]
    [InlineData("RK78", "SSB")]
    [InlineData("VV", "ECLIPJ2000")]
    [InlineData("VV", "SSB")]
    public void ACallerInitializeWithAnotherFrameOrObserver_ChangesNothing(string kind, string state)
    {
        // Arrange
        (CentralBodyPropagator Propagator, Spacecraft Spacecraft) New()
        {
            if (state == "ECLIPJ2000")
            {
                var leo = NewLeo(new CelestialBody(PlanetsAndMoons.EARTH));
                return (new CentralBodyPropagator(new Window(Epoch, Epoch.AddHours(2.0)), leo, new PropagationOptions
                {
                    Integrator = kind == "RK78"
                        ? new RK78IntegratorOptions { AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10 }
                        : new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) },
                    OutputStep = Step,
                    Geopotential = new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
                    CovariancePropagation = CovariancePropagationOptions.None
                }), leo);
            }

            var sun = Stars.SUN_BODY;
            var heliocentric = new StateVector(new Vector3(1.5e11, 0.0, 0.0), new Vector3(0.0, 29780.0, 1000.0), sun,
                Epoch, Frames.Frame.ICRF);
            int id = NextId();
            var ssb = new Spacecraft(id, $"II{-id}", 100.0, heliocentric.RelativeTo(Barycenters.SOLAR_SYSTEM_BARYCENTER, Aberration.None).ToStateVector(), 10000.0,
                new Clock($"iiclk{-id}", 65536), 10.0, 2.2);
            // The gravity of the Sun and of the Earth as point masses about the observer of the state
            Integrator integrator = kind == "RK78"
                ? new RK78Integrator(1e-12, 1e-12)
                : new VVIntegrator(TimeSpan.FromMinutes(10.0));
            integrator.AddForce(new GravitationalAcceleration(sun));
            integrator.AddForce(new GravitationalAcceleration(PlanetsAndMoons.EARTH_BODY));
            return (new CentralBodyPropagator(new Window(Epoch, Epoch.AddDays(2.0)), ssb, integrator,
                TimeSpan.FromMinutes(30.0)), ssb);
        }

        var reference = New();
        var expected = reference.Propagator.Propagate().StateVectors;
        var (propagator, spacecraft) = New();
        var callerState = state == "ECLIPJ2000"
            ? ((StateVector)spacecraft.InitialOrbitalParameters).ToFrame(Frames.Frame.ECLIPJ2000)
            : spacecraft.InitialOrbitalParameters.ToStateVector();

        // Act
        propagator.Integrator.Initialize(callerState);
        var actual = propagator.Propagate().StateVectors;

        // Assert
        Assert.Equal(expected.Count, actual.Count);
        double worst = 0.0;
        for (int i = 0; i < expected.Count; i++)
        {
            worst = System.Math.Max(worst, (expected[i].Position - actual[i].Position).Magnitude());
        }

        _output.WriteLine($"MEASURE IntegratorInitializationTests caller Initialize {kind} {state}: {worst:E3} m");
        for (int i = 0; i < expected.Count; i++)
        {
            AssertSameBits(expected[i].Position, actual[i].Position, $"state {i}");
            AssertSameBits(expected[i].Velocity, actual[i].Velocity, $"velocity {i}");
        }
    }
}
