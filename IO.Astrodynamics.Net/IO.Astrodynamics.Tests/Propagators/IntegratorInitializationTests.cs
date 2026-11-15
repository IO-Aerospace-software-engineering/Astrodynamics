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
/// <see cref="NullReferenceException"/> from the first step (no observer, frame, evaluation state or RK7(8) stages).
/// </summary>
/// <remarks>
/// The last state, and Φ when it is propagated, are checked against the two-body closed form (<see cref="KeplerianStm"/>)
/// over the seconds of the time argument, TT about the Earth, TDB about the Sun; and bit for bit against the same run initialized by the caller after
/// construction, the pattern of the tests before, whose results the existing goldens establish.
/// </remarks>
public class IntegratorInitializationTests
{
    private readonly ITestOutputHelper _output;

    public IntegratorInitializationTests(ITestOutputHelper output)
    {
        TestKernels.EnsureSolarSystem();
        _output = output;
    }

    private static readonly TimeSystem.Time Epoch = new(2021, 1, 1, 12, 0, 0);
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(60.0);
    private static Window OneHour => new(Epoch, Epoch.AddHours(1.0));

    private static int _nextId = -668100;
    private static int NextId() => Interlocked.Decrement(ref _nextId);

    // Provisional thresholds: twice the Linux worst of the MEASURE lines (2026-11-15), to be read again on Windows and
    // macOS arm64. RK7(8) at 1e-10 about the Earth: 9.947e-5 m (ICRF, with and without Φ, and ECLIPJ2000); at 1e-12
    // about the Sun over a day: 1.221e-4 m; Velocity-Verlet at 1 s: 15.89 m, its truncation (1589 m at 10 s, h²)
    private const double Rk78KeplerThreshold = 2.0e-4;
    private const double SsbKeplerThreshold = 2.5e-4;
    private const double VvKeplerThreshold = 3.2e1;
    // Φ of RK7(8) at 1e-10 at the end of the hour against the two-body closed form, the worst relative 3×3 block:
    // 6.970e-11 (Linux, 2026-11-15); provisional, twice that, to be read again on Windows and macOS arm64
    private const double Rk78PhiThreshold = 1.4e-10;

    private static Spacecraft NewSpacecraft(ILocalizable observer, Frames.Frame frame, Vector3 position, Vector3 velocity)
    {
        int id = NextId();
        return new Spacecraft(id, $"II{-id}", 100.0, 10000.0, new Clock($"iiclk{-id}", 65536),
            new StateVector(position, velocity, observer, Epoch, frame), 10.0, 2.2);
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
        using var propagator = NewPropagator(window, spacecraft, NewIntegrator(kind, earth), stm);

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
    public void TheRun_IsBitForBitTheOneTheCallerInitializedAfterConstruction(string kind, bool stm)
    {
        // Arrange
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var window = OneHour;
        var implicitSpacecraft = NewLeo(earth);
        var explicitSpacecraft = NewLeo(earth);
        var explicitIntegrator = NewIntegrator(kind, earth);

        // Act
        using (var implicitPropagator = NewPropagator(window, implicitSpacecraft, NewIntegrator(kind, earth), stm))
        {
            implicitPropagator.Propagate();
        }

        using (var explicitPropagator = NewPropagator(window, explicitSpacecraft, explicitIntegrator, stm))
        {
            explicitIntegrator.Initialize(explicitSpacecraft.InitialOrbitalParameters.ToStateVector());
            explicitPropagator.Propagate();
        }

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
        using (var p = new CentralBodyPropagator(window, given, NewIntegrator("RK78", earth), Step))
        {
            p.Propagate();
        }

        using (var p = new CentralBodyPropagator(window, built, new RK78Integrator(1e-10, 1e-10), [earth], false, false,
                   Step))
        {
            p.Propagate();
        }

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
    public void TheConstructor_InitializesOnce_WithTheCentralBodyTheFrameAndTheTdbStart()
    {
        // Arrange
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var spacecraft = NewLeo(earth);
        var recorder = new RecordingIntegrator((RK78Integrator)NewIntegrator("RK78", earth));
        // A window given in UTC: the initial state is at its TDB start
        var window = new Window(Epoch.ToUTC(), Epoch.ToUTC().AddHours(1.0));

        // Act
        using var propagator = new CentralBodyPropagator(window, spacecraft, recorder, Step);
        var atConstruction = recorder.Initializations.ToArray();
        propagator.Propagate();

        // Assert: once, by the constructor, and not again without an event
        var state = Assert.Single(atConstruction);
        Assert.Single(recorder.Initializations);
        Assert.Same(earth, state.Observer);
        Assert.Equal(Frames.Frame.ICRF, state.Frame);
        Assert.Equal(TimeFrame.TDBFrame, state.Epoch.Frame);
        Assert.Equal(window.StartDate.ToTDB(), state.Epoch);
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
        var spacecraft = new Spacecraft(id, $"II{-id}", 100.0, 10000.0, new Clock($"iiclk{-id}", 65536), ssbState, 10.0, 2.2);
        var recorder = new RecordingIntegrator(new RK78Integrator(1e-12, 1e-12));
        recorder.Inner.AddForce(new GravitationalAcceleration(sun));
        var window = new Window(Epoch, Epoch.AddDays(1.0));

        // Act
        using var propagator = new CentralBodyPropagator(window, spacecraft, recorder, TimeSpan.FromMinutes(10.0));
        var solution = propagator.Propagate();

        // Assert: initialized about the Sun, as the propagator states the initial orbit, and Kepler about the Sun over
        // the TDB seconds of the window
        var state = Assert.Single(recorder.Initializations);
        Assert.Same(sun, state.Observer);
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
        var spacecraft = NewLeo(earth, Frames.Frame.ECLIPTIC_J2000);
        var initial = (StateVector)spacecraft.InitialOrbitalParameters;
        var recorder = new RecordingIntegrator((RK78Integrator)NewIntegrator("RK78", earth));
        var window = OneHour;

        // Act
        using var propagator = new CentralBodyPropagator(window, spacecraft, recorder, Step);
        var solution = propagator.Propagate();

        // Assert
        Assert.Equal(Frames.Frame.ECLIPTIC_J2000, Assert.Single(recorder.Initializations).Frame);
        double elapsed = PropagationTimeArgument.Tt.SecondsBetween(window.StartDate, window.EndDate);
        double error = KeplerError(initial, solution, window, elapsed);
        _output.WriteLine($"MEASURE IntegratorInitializationTests ECLIPJ2000: Kepler {error:E3} m");
        Assert.True(error <= Rk78KeplerThreshold, $"{error:E3} m");
    }

    [Theory]
    [InlineData("RK78")]
    [InlineData("VV")]
    public void WithAnImpulse_TheRunIsBitForBitTheOneTheCallerInitialized(string kind)
    {
        // Arrange: an apogee raise at the next perigee, about 1.6 h after the start; the integrator re-initialized after
        // the event as before
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var window = new Window(Epoch, Epoch.AddHours(2.0));

        Spacecraft WithManeuver()
        {
            var spacecraft = NewSpacecraft(earth, Frames.Frame.ICRF, new Vector3(6700000.0, 0.0, 0.0),
                new Vector3(0.0, 8000.0, 0.0));
            var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
            spacecraft.AddFuelTank(tank);
            var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
            spacecraft.AddEngine(engine);
            spacecraft.SetStandbyManeuver(new ApogeeHeightManeuver(earth, Epoch.AddSeconds(600.0), TimeSpan.Zero, 9000000.0,
                engine));
            return spacecraft;
        }

        var implicitSpacecraft = WithManeuver();
        var explicitSpacecraft = WithManeuver();
        var explicitIntegrator = NewIntegrator(kind, earth);

        // Act
        using (var p = new CentralBodyPropagator(window, implicitSpacecraft, NewIntegrator(kind, earth), Step))
        {
            p.Propagate();
        }

        using (var p = new CentralBodyPropagator(window, explicitSpacecraft, explicitIntegrator, Step))
        {
            explicitIntegrator.Initialize(explicitSpacecraft.InitialOrbitalParameters.ToStateVector());
            p.Propagate();
        }

        // Assert: the impulse fired, and every state is the same
        Assert.Single(implicitSpacecraft.ExecutedManeuvers);
        Assert.Equal(explicitSpacecraft.PropagatedStates.Length, implicitSpacecraft.PropagatedStates.Length);
        for (int i = 0; i < implicitSpacecraft.PropagatedStates.Length; i++)
        {
            AssertSameBits(explicitSpacecraft.PropagatedStates[i].Position, implicitSpacecraft.PropagatedStates[i].Position,
                $"state {i}");
        }
    }
}
