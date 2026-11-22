// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// Cancellation by token (R28 of #668, lot L4, decisions 4 to 6, C1 and C2, decided by S. Guillet, 2026-11-22): the
/// token is the last parameter of the methods that run; a token already cancelled changes nothing; the integrators of the
/// library read it at each accepted step and the propagator before each segment, never after the last; a stopped run
/// leaves the spacecraft re-armed and partial (no stored state, no attitude record, not propagated, nothing kept for the
/// conjunction analysis; fuel, executed maneuvers and standby chain advanced up to the stop); a batch, a Monte Carlo
/// campaign and a scenario throw one <see cref="OperationCanceledException"/> carrying the caller's token.
/// </summary>
/// <remarks>
/// The stop is placed by an atmospheric model (<see cref="CancellingAtmosphere"/>) given as
/// <see cref="PropagationOptions.Atmosphere"/>, which cancels the token from inside a force evaluation once the
/// propagation reaches a given epoch, and counts the evaluations after it: the bound of one step is the number of
/// evaluations the step in progress can still make.
/// </remarks>
public class CancellationTests
{
    private readonly ITestOutputHelper _output;

    public CancellationTests(ITestOutputHelper output)
    {
        TestKernels.EnsureSolarSystem();
        _output = output;
    }

    private static readonly TimeSystem.Time Epoch = new(2021, 1, 1, 12, 0, 0, TimeScale.TDB);
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(60.0);
    private static Window TwoHours => new(Epoch, Epoch.AddHours(2.0));

    private static int _nextId = -668300;
    private static int NextId() => Interlocked.Decrement(ref _nextId);

    // An Earth of its own per test, without an atmospheric model, so that the option fills it
    private static CelestialBody NewEarth() => new(PlanetsAndMoons.EARTH);

    private static IntegratorOptions Integrator(string kind) => kind == "RK"
        ? RK78IntegratorOptions.Default
        : new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) };

    // The perigee at the start (6700 km, 8000 m/s), an apogee raise at the next perigee, about 1.6 h after the start
    private static Spacecraft NewSpacecraft(CelestialBody earth, bool withManeuver = true, Matrix? covariance = null)
    {
        int id = NextId();
        var state = new StateVector(new Vector3(6700000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), earth, Epoch,
            Frames.Frame.ICRF, covariance);
        var spacecraft = new Spacecraft(id, $"CX{-id}", 100.0, 10000.0, new Clock($"cxclk{-id}", 65536), state, 10.0, 2.2);
        if (withManeuver)
        {
            var tank = new FuelTank($"ft{-id}", "model", "sn", 1000.0, 1000.0);
            spacecraft.AddFuelTank(tank);
            var engine = new Engine($"eng{-id}", "model", "sn", 300.0, 10.0, tank);
            spacecraft.AddEngine(engine);
            spacecraft.SetStandbyManeuver(new ApogeeHeightManeuver(earth, Epoch.AddSeconds(600.0), TimeSpan.Zero,
                9000000.0, engine));
        }

        return spacecraft;
    }

    // The fuel left in the tanks (GetTotalFuel is the fuel at the start, R09 of #668 in lot L5)
    private static double Fuel(Spacecraft spacecraft) => spacecraft.FuelTanks.Sum(tank => tank.Quantity);

    private static PropagationOptions Options(string kind, IAtmosphericModel atmosphere = null) => new()
    {
        Integrator = Integrator(kind),
        OutputStep = Output,
        AtmosphericDrag = true,
        Atmosphere = atmosphere ?? new CancellingAtmosphere(null, Epoch),
        CovariancePropagation = CovariancePropagationOptions.None
    };

    private static void AssertSameStates(IReadOnlyList<StateVector> expected, IReadOnlyList<StateVector> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.True(expected[i].Epoch.EqualsExact(actual[i].Epoch), $"epoch {i}");
            Assert.Equal(expected[i].Position, actual[i].Position);
            Assert.Equal(expected[i].Velocity, actual[i].Velocity);
        }
    }

    // The state C2 of lot L4 describes after a stopped run, the maneuver part aside
    private static void AssertLeftAsAStoppedRun(Spacecraft spacecraft)
    {
        Assert.Empty(spacecraft.PropagatedStates);
        Assert.Empty(((SpacecraftFrame)spacecraft.Frame).GetStateOrientationsToICRF());
        Assert.False(spacecraft.IsPropagated);
        Assert.Null(spacecraft.LastPropagation);
        Assert.Null(spacecraft.LastForceModel);
    }

    #region The surface (decision 4)

    [Fact]
    public void TheToken_IsTheLastParameterOfTheMethodsThatRun()
    {
        void AssertLastToken(MethodInfo method)
        {
            var last = method.GetParameters()[^1];
            Assert.Equal(typeof(CancellationToken), last.ParameterType);
            Assert.Equal("cancellationToken", last.Name);
            Assert.True(last.IsOptional, method.ToString());
        }

        AssertLastToken(typeof(Spacecraft).GetMethod(nameof(Spacecraft.Propagate))!);
        AssertLastToken(typeof(Scenario).GetMethod(nameof(Scenario.Simulate))!);
        AssertLastToken(typeof(IPropagator).GetMethod(nameof(IPropagator.Propagate))!);
        AssertLastToken(typeof(PropagatorBase).GetMethod(nameof(PropagatorBase.Propagate))!);
        AssertLastToken(typeof(TLEPropagator).GetMethod(nameof(TLEPropagator.Propagate))!);
        var batch = typeof(BatchPropagator).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(BatchPropagator.Propagate)).ToArray();
        Assert.Equal(2, batch.Length);
        Assert.Contains(batch, m => m.GetParameters()[1].ParameterType == typeof(IProgress<BatchProgress>));
        Array.ForEach(batch, AssertLastToken);
        var monteCarlo = typeof(MonteCarloPropagator).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(MonteCarloPropagator.Propagate)).ToArray();
        Assert.Equal(2, monteCarlo.Length);
        Assert.Contains(monteCarlo, m => m.GetParameters()[1].ParameterType == typeof(IProgress<BatchProgress>));
        Array.ForEach(monteCarlo, AssertLastToken);
    }

    [Fact]
    public void NoConstructorRecordOrProperty_HoldsAToken_AndBatchOptionsHoldsTheParallelismAlone()
    {
        foreach (var type in typeof(PropagationOptions).Assembly.GetExportedTypes())
        {
            Assert.DoesNotContain(type.GetProperties(), p => p.PropertyType == typeof(CancellationToken));
            Assert.DoesNotContain(type.GetConstructors(),
                c => c.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken)));
        }

        var properties = typeof(BatchOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).ToArray();
        Assert.Equal(new[] { nameof(BatchOptions.MaxDegreeOfParallelism) }, properties);
        Assert.Equal(new BatchOptions(), BatchOptions.Default);
    }

    [Fact]
    public void TheAsynchronousMembersOverSynchronousWork_AndTheEmptyDisposables_AreRemoved()
    {
        foreach (var type in new[]
                 {
                     typeof(Spacecraft), typeof(Star), typeof(Scenario), typeof(BatchPropagator),
                     typeof(MonteCarloPropagator)
                 })
        {
            Assert.DoesNotContain(type.GetMethods(), m => m.Name is "PropagateAsync" or "SimulateAsync");
        }

        foreach (var type in new[]
                 {
                     typeof(IPropagator), typeof(PropagatorBase), typeof(CentralBodyPropagator), typeof(TLEPropagator)
                 })
        {
            Assert.False(typeof(IDisposable).IsAssignableFrom(type), type.Name);
        }
    }

    #endregion

    #region A token already cancelled (decision 6)

    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void AnAlreadyCancelledToken_ChangesNothing(string kind)
    {
        // Arrange: a spacecraft propagated once, its impulse executed
        var earth = NewEarth();
        var spacecraft = NewSpacecraft(earth);
        spacecraft.Propagate(TwoHours, Options(kind));
        var states = spacecraft.PropagatedStates;
        var attitudes = ((SpacecraftFrame)spacecraft.Frame).GetStateOrientationsToICRF().ToArray();
        var executed = spacecraft.ExecutedManeuvers.ToArray();
        double fuel = Fuel(spacecraft);
        var standby = spacecraft.StandbyManeuver;
        var lastPropagation = spacecraft.LastPropagation;
        var lastForceModel = spacecraft.LastForceModel;
        Assert.Single(executed);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var exception = Assert.Throws<OperationCanceledException>(() =>
            spacecraft.Propagate(TwoHours, Options(kind), cts.Token));

        // Assert: the caller's token, and the spacecraft as it was
        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.True(spacecraft.IsPropagated);
        AssertSameStates(states, spacecraft.PropagatedStates);
        Assert.Equal(attitudes, ((SpacecraftFrame)spacecraft.Frame).GetStateOrientationsToICRF().ToArray());
        Assert.Equal(executed, spacecraft.ExecutedManeuvers.ToArray());
        Assert.Equal(fuel, Fuel(spacecraft));
        Assert.Same(standby, spacecraft.StandbyManeuver);
        Assert.Same(lastPropagation, spacecraft.LastPropagation);
        Assert.Same(lastForceModel, spacecraft.LastForceModel);
    }

    [Fact]
    public void AnAlreadyCancelledToken_ChangesNothing_OnAPropagatorBuiltDirectly_AndOnTLEPropagator()
    {
        // Arrange
        var earth = NewEarth();
        var spacecraft = NewSpacecraft(earth);
        var propagator = new CentralBodyPropagator(TwoHours, spacecraft, Options("RK"));
        var tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        int id = NextId();
        var tleSpacecraft = new Spacecraft(id, $"CX{-id}", 100.0, 10000.0, new Clock($"cxclk{-id}", 65536), tle);
        var start = tle.Epoch;
        var tlePropagator = new TLEPropagator(new Window(start, start.AddHours(1.0)), tleSpacecraft, Output);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act and assert
        Assert.Equal(cts.Token, Assert.Throws<OperationCanceledException>(() => propagator.Propagate(cts.Token)).CancellationToken);
        Assert.Equal(cts.Token, Assert.Throws<OperationCanceledException>(() => tlePropagator.Propagate(cts.Token)).CancellationToken);
        Assert.False(spacecraft.IsPropagated);
        Assert.Equal(1000.0, Fuel(spacecraft));
        Assert.Empty(tleSpacecraft.PropagatedStates);
    }

    [Fact]
    public void AnAlreadyCancelledToken_ChangesNothing_InAScenario()
    {
        // Arrange: a scenario simulated once
        var earth = NewEarth();
        var scenario = new Scenario("Cancel", new IO.Astrodynamics.Mission.Mission("Cancel"), TwoHours);
        var spacecraft = NewSpacecraft(earth, withManeuver: false);
        var star = IO.Astrodynamics.Tests.Body.StarTests.NewStar(668300, Epoch);
        scenario.AddSpacecraft(spacecraft);
        scenario.AddStar(star);
        scenario.Simulate(Options("RK"));
        var states = spacecraft.PropagatedStates;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var exception = Assert.Throws<OperationCanceledException>(() => scenario.Simulate(Options("RK"), cts.Token));

        // Assert
        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.True(scenario.IsSimulated);
        AssertSameStates(states, spacecraft.PropagatedStates);
    }

    [Fact]
    public void AnAlreadyCancelledToken_ChangesNothing_InABatch()
    {
        // Arrange
        var earth = NewEarth();
        var tasks = Enumerable.Range(0, 3)
            .Select(_ => new PropagationTask(NewSpacecraft(earth, withManeuver: false), TwoHours, Options("RK"))).ToList();
        var reports = new List<BatchProgress>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var exception = Assert.Throws<OperationCanceledException>(() =>
            BatchPropagator.Propagate(tasks, new SynchronousProgress(reports), null, cts.Token));

        // Assert
        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.All(tasks, task => Assert.False(task.Spacecraft.IsPropagated));
        Assert.Empty(reports);
    }

    #endregion

    #region A live token (results unchanged)

    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void ALiveToken_GivesTheRunWithoutToken_BitForBit(string kind)
    {
        // Arrange
        var earth = NewEarth();
        var without = NewSpacecraft(earth);
        var with = NewSpacecraft(earth);
        using var cts = new CancellationTokenSource();

        // Act
        var expected = without.Propagate(TwoHours, Options(kind));
        var actual = with.Propagate(TwoHours, Options(kind), cts.Token);

        // Assert
        AssertSameStates(expected.StateVectors, actual.StateVectors);
        AssertSameStates(without.PropagatedStates, with.PropagatedStates);
        Assert.Equal(Fuel(without), Fuel(with));
    }

    #endregion

    #region A stop inside a run (decision 6, C1, C2)

    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void AStopInsideASegment_ThrowsWithinTheStep_AndLeavesTheSpacecraftReArmed(string kind)
    {
        // Arrange: the token cancelled 30 min into the first segment, before the impulse
        var earth = NewEarth();
        var spacecraft = NewSpacecraft(earth);
        spacecraft.Propagate(TwoHours, Options(kind));
        Assert.True(spacecraft.IsPropagated);
        using var cts = new CancellationTokenSource();
        var atmosphere = new CancellingAtmosphere(cts, Epoch.AddMinutes(30.0));

        // Act
        var exception = Assert.Throws<OperationCanceledException>(() =>
            spacecraft.Propagate(TwoHours, Options(kind, atmosphere), cts.Token));

        // Assert: within the step in progress (RK7(8): its remaining stages, at most 12 of 13 when the step in progress is
        // accepted, as measured: 2 and 4 about the Earth, 8 in SSB mode, Linux, 2026-11-22; a rejection would add one
        // attempt, at most 101 in all, C1 of lot L4; Velocity-Verlet: none, the one evaluation of the step being the one
        // that cancels), the spacecraft re-armed, its impulse not fired
        _output.WriteLine($"MEASURE {kind} density evaluations after the cancellation: {atmosphere.CallsAfterCancel}");
        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.True(atmosphere.CallsAfterCancel <= (kind == "RK" ? 12 : 0), $"{atmosphere.CallsAfterCancel}");
        AssertLeftAsAStoppedRun(spacecraft);
        Assert.Empty(spacecraft.ExecutedManeuvers);
        Assert.Equal(1000.0, Fuel(spacecraft));
        Assert.Same(spacecraft.InitialManeuver, spacecraft.StandbyManeuver);

        // Any other spacecraft than a TLE reads its initial orbit again
        var at = Epoch.AddMinutes(10.0);
        var read = spacecraft.GetGeometricStateRelativeTo(at, earth);
        var orbit = spacecraft.InitialOrbitalParameters.ToStateVector(at);
        Assert.Equal(orbit.Position, read.Position);
        Assert.Equal(orbit.Velocity, read.Velocity);

        // The next propagation re-arms it: the run of a spacecraft never propagated, bit for bit
        var twin = NewSpacecraft(earth);
        var expected = twin.Propagate(TwoHours, Options(kind));
        var actual = spacecraft.Propagate(TwoHours, Options(kind));
        AssertSameStates(expected.StateVectors, actual.StateVectors);
        Assert.Equal(Fuel(twin), Fuel(spacecraft));
    }

    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void AStopAfterTheImpulse_LeavesTheFuelTheExecutedManeuversAndTheChainAdvanced(string kind)
    {
        // Arrange: the impulse of the uncancelled run, then a run stopped 10 min after it
        var earth = NewEarth();
        var reference = NewSpacecraft(earth);
        reference.Propagate(TwoHours, Options(kind));
        var impulse = Assert.IsAssignableFrom<ImpulseManeuver>(Assert.Single(reference.ExecutedManeuvers));
        var stop = impulse.ManeuverWindow!.Value.EndDate.AddMinutes(10.0);
        Assert.True(stop < TwoHours.EndDate);
        var spacecraft = NewSpacecraft(earth);
        using var cts = new CancellationTokenSource();
        var atmosphere = new CancellingAtmosphere(cts, stop);

        // Act
        var exception = Assert.Throws<OperationCanceledException>(() =>
            spacecraft.Propagate(TwoHours, Options(kind, atmosphere), cts.Token));

        // Assert: the maneuver part advanced up to the stop, as the uncancelled run left it at its end
        _output.WriteLine($"MEASURE {kind} density evaluations after the cancellation: {atmosphere.CallsAfterCancel}");
        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.True(atmosphere.CallsAfterCancel <= (kind == "RK" ? 12 : 0), $"{atmosphere.CallsAfterCancel}");
        AssertLeftAsAStoppedRun(spacecraft);
        Assert.Single(spacecraft.ExecutedManeuvers);
        Assert.Equal(Fuel(reference), Fuel(spacecraft));
        Assert.True(Fuel(spacecraft) < 1000.0);
        Assert.Null(spacecraft.StandbyManeuver);
        Assert.Null(reference.StandbyManeuver);
    }

    [Fact]
    public void AStopInsideASegment_WithTheCovariance_LeavesTheSpacecraftReArmed()
    {
        // Arrange: Auto propagates the covariance of the initial state (RK7(8))
        var earth = NewEarth();
        var covariance = Matrix.Diagonal(100.0, 100.0, 100.0, 1e-2, 1e-2, 1e-2);
        var spacecraft = NewSpacecraft(earth, withManeuver: false, covariance: covariance);
        using var cts = new CancellationTokenSource();
        var atmosphere = new CancellingAtmosphere(cts, Epoch.AddMinutes(30.0));
        var options = Options("RK", atmosphere) with { CovariancePropagation = null };

        // Act
        var exception = Assert.Throws<OperationCanceledException>(() => spacecraft.Propagate(TwoHours, options, cts.Token));

        // Assert
        Assert.Equal(cts.Token, exception.CancellationToken);
        AssertLeftAsAStoppedRun(spacecraft);
        var twin = NewSpacecraft(earth, withManeuver: false, covariance: covariance);
        var expected = twin.Propagate(TwoHours, Options("RK") with { CovariancePropagation = null });
        var actual = spacecraft.Propagate(TwoHours, Options("RK") with { CovariancePropagation = null });
        AssertSameStates(expected.StateVectors, actual.StateVectors);
        Assert.Equal(expected.GetCovariance(TwoHours.EndDate), actual.GetCovariance(TwoHours.EndDate));
    }

    [Fact]
    public void AStopInsideASegment_InSsbMode_ThrowsWithinTheStep()
    {
        // Arrange: about the Sun, near the Earth, whose atmosphere gives the drag as a listed body
        using var cts = new CancellationTokenSource();
        var atmosphere = new CancellingAtmosphere(cts, Epoch.AddMinutes(30.0));
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, atmosphericModel: atmosphere);
        var sun = Stars.SUN_BODY;
        var earthState = earth.GetGeometricStateRelativeTo(Epoch, sun);
        int id = NextId();
        var state = new StateVector(earthState.Position + new Vector3(4.0e8, 0.0, 0.0), earthState.Velocity, sun, Epoch,
            Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(id, $"CX{-id}", 100.0, 10000.0, new Clock($"cxclk{-id}", 65536), state, 10.0, 2.2);
        var options = new PropagationOptions
        {
            OutputStep = Output, ThirdBodies = [earth], AtmosphericDrag = true,
            CovariancePropagation = CovariancePropagationOptions.None
        };

        // Act
        var exception = Assert.Throws<OperationCanceledException>(() => spacecraft.Propagate(TwoHours, options, cts.Token));

        // Assert
        _output.WriteLine($"MEASURE SSB density evaluations after the cancellation: {atmosphere.CallsAfterCancel}");
        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.True(atmosphere.CallsAfterCancel <= 12, $"{atmosphere.CallsAfterCancel}");
        AssertLeftAsAStoppedRun(spacecraft);
    }

    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void AStop_DropsTheStatesAndAttitudesAddedByHand_OfASpacecraftNeverPropagated(string kind)
    {
        // Arrange: a spacecraft never propagated that holds states (AddPropagatedStates, the second 1 km off the initial
        // orbit, so that the read can tell them apart) and an attitude (AddStateOrientationToICRF), as C2 of lot L4 covers
        var earth = NewEarth();
        var spacecraft = NewSpacecraft(earth);
        var first = spacecraft.InitialOrbitalParameters.ToStateVector(Epoch);
        var second = spacecraft.InitialOrbitalParameters.ToStateVector(Epoch.AddMinutes(1.0));
        spacecraft.AddPropagatedStates(first, new StateVector(second.Position + new Vector3(1000.0, 0.0, 0.0),
            second.Velocity, second.Observer, second.Epoch, second.Frame));
        spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(Quaternion.Identity, Vector3.Zero, Epoch,
            spacecraft.Frame));
        Assert.False(spacecraft.IsPropagated);
        Assert.Equal(2, spacecraft.PropagatedStates.Length);
        using var cts = new CancellationTokenSource();
        var atmosphere = new CancellingAtmosphere(cts, Epoch.AddMinutes(30.0));

        // Act
        var exception = Assert.Throws<OperationCanceledException>(() =>
            spacecraft.Propagate(TwoHours, Options(kind, atmosphere), cts.Token));

        // Assert: no stored state and no attitude record, and the initial orbit read again
        Assert.Equal(cts.Token, exception.CancellationToken);
        AssertLeftAsAStoppedRun(spacecraft);
        var at = Epoch.AddSeconds(30.0);
        var read = spacecraft.GetGeometricStateRelativeTo(at, earth);
        var orbit = spacecraft.InitialOrbitalParameters.ToStateVector(at);
        Assert.Equal(orbit.Position, read.Position);
        Assert.Equal(orbit.Velocity, read.Velocity);
    }

    [Fact]
    public void AStopOfATleSpacecraft_ReadsSgp4Again()
    {
        // Arrange: a TLE spacecraft propagated numerically, so that it reads its stored states
        var tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        int id = NextId();
        var spacecraft = new Spacecraft(id, $"CX{-id}", 100.0, 10000.0, new Clock($"cxclk{-id}", 65536), tle, 10.0, 2.2);
        var start = tle.Epoch;
        var window = new Window(start, start.AddHours(1.0));
        spacecraft.Propagate(window, Options("RK"));
        var at = start.AddMinutes(20.0);
        var sgp4 = tle.ToStateVector(at).ToFrame(Frames.Frame.ICRF).RelativeTo(PlanetsAndMoons.EARTH_BODY, Aberration.None)
            .ToStateVector();
        Assert.NotEqual(sgp4.Position, spacecraft.GetGeometricStateRelativeTo(at, PlanetsAndMoons.EARTH_BODY).Position);
        using var cts = new CancellationTokenSource();
        var atmosphere = new CancellingAtmosphere(cts, start.AddMinutes(30.0));

        // Act
        Assert.Throws<OperationCanceledException>(() => spacecraft.Propagate(window, Options("RK", atmosphere), cts.Token));

        // Assert
        AssertLeftAsAStoppedRun(spacecraft);
        var read = spacecraft.GetGeometricStateRelativeTo(at, PlanetsAndMoons.EARTH_BODY);
        Assert.Equal(sgp4.Position, read.Position);
        Assert.Equal(sgp4.Velocity, read.Velocity);
    }

    #endregion

    #region An integrator implemented outside the library: between the segments (decision 6)

    [Fact]
    public void AnOuterIntegrator_IsStoppedBetweenItsSegments()
    {
        // Arrange: a token cancelled inside the first segment, which ends at the impulse
        var earth = NewEarth();
        var spacecraft = NewSpacecraft(earth);
        using var cts = new CancellationTokenSource();
        var integrator = new OuterIntegrator(earth, cts);
        var propagator = new CentralBodyPropagator(TwoHours, spacecraft, integrator, Output);

        // Act
        var exception = Assert.Throws<OperationCanceledException>(() => propagator.Propagate(cts.Token));

        // Assert: the first segment ran to its event, the impulse executed, the second never started
        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.Equal(1, integrator.Segments);
        Assert.Single(spacecraft.ExecutedManeuvers);
        Assert.True(Fuel(spacecraft) < 1000.0);
        AssertLeftAsAStoppedRun(spacecraft);
    }

    [Fact]
    public void ATokenCancelledInTheLastSegment_OfAnOuterIntegrator_LetsTheRunWriteItsOutputs()
    {
        // Arrange: one segment, no maneuver
        var earth = NewEarth();
        var spacecraft = NewSpacecraft(earth, withManeuver: false);
        using var cts = new CancellationTokenSource();
        var integrator = new OuterIntegrator(earth, cts);

        // Act: not read after the last segment, so the outputs, the attitude record and the stored states are written
        var solution = new CentralBodyPropagator(TwoHours, spacecraft, integrator, Output).Propagate(cts.Token);

        // Assert
        Assert.True(cts.IsCancellationRequested);
        Assert.Equal(1, integrator.Segments);
        Assert.True(spacecraft.IsPropagated);
        Assert.Equal(solution.StateVectors.Count, spacecraft.PropagatedStates.Length);
    }

    #endregion

    #region Batch, Monte Carlo, scenario (C2)

    [Fact]
    public void ABatchStoppedInARunningTask_ThrowsOneOperationCanceledException()
    {
        // Arrange: three tasks in turn, the second stopped 30 min into its run
        var earth = NewEarth();
        using var cts = new CancellationTokenSource();
        var atmosphere = new CancellingAtmosphere(cts, Epoch.AddMinutes(30.0));
        var first = NewSpacecraft(earth, withManeuver: false);
        var second = NewSpacecraft(earth, withManeuver: false);
        var third = NewSpacecraft(earth, withManeuver: false);
        var tasks = new List<PropagationTask>
        {
            new(first, TwoHours, Options("RK")),
            new(second, TwoHours, Options("RK", atmosphere)),
            new(third, TwoHours, Options("RK"))
        };
        var reports = new List<BatchProgress>();

        // Act: one exception, never an AggregateException
        var exception = Assert.Throws<OperationCanceledException>(() => BatchPropagator.Propagate(tasks,
            new SynchronousProgress(reports), new BatchOptions { MaxDegreeOfParallelism = 1 }, cts.Token));

        // Assert: the earlier task complete, the stopped one re-armed, the later one untouched
        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.True(atmosphere.CallsAfterCancel <= 12, $"{atmosphere.CallsAfterCancel}");
        Assert.True(first.IsPropagated);
        Assert.Equal(TwoHours.EndDate, first.PropagatedStates[^1].Epoch);
        AssertLeftAsAStoppedRun(second);
        Assert.False(third.IsPropagated);
        Assert.Equal([new BatchProgress(1, 3)], reports);
    }

    [Fact]
    public void ABatchStoppedInParallelTasks_ThrowsOneOperationCanceledException()
    {
        // Arrange: four tasks run together, each stopped by the first that reaches 30 min
        var earth = NewEarth();
        using var cts = new CancellationTokenSource();
        var atmosphere = new CancellingAtmosphere(cts, Epoch.AddMinutes(30.0), afterRuns: -1);
        var spacecraft = Enumerable.Range(0, 4).Select(_ => NewSpacecraft(earth, withManeuver: false)).ToArray();
        var tasks = spacecraft.Select(s => new PropagationTask(s, TwoHours, Options("RK", atmosphere))).ToList();

        // Act: one exception of the token, never an AggregateException, whatever the number of running tasks stopped
        var exception = Assert.Throws<OperationCanceledException>(() =>
            BatchPropagator.Propagate(tasks, new BatchOptions { MaxDegreeOfParallelism = 4 }, cts.Token));

        // Assert: no task completed, each stopped one re-armed
        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.All(spacecraft, AssertLeftAsAStoppedRun);
    }

    [Fact]
    public void AMonteCarloCampaignStoppedInARun_ThrowsOneOperationCanceledException()
    {
        // Arrange
        var earth = NewEarth();
        var template = NewSpacecraft(earth, withManeuver: false,
            covariance: Matrix.Diagonal(100.0, 100.0, 100.0, 1e-2, 1e-2, 1e-2));
        using var cts = new CancellationTokenSource();
        var atmosphere = new CancellingAtmosphere(cts, Epoch.AddMinutes(30.0));
        var config = new MonteCarloConfiguration
        {
            RunCount = 3, Seed = 42, TemplateSpacecraft = template, Window = TwoHours,
            Propagation = Options("RK", atmosphere), BaseNaifId = NextId() - 100000,
            BatchOptions = new BatchOptions { MaxDegreeOfParallelism = 1 }
        };
        var reports = new List<BatchProgress>();

        // Act
        var exception = Assert.Throws<OperationCanceledException>(() =>
            MonteCarloPropagator.Propagate(config, new SynchronousProgress(reports), cts.Token));

        // Assert: stopped in the first run, no run reported
        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.True(atmosphere.CallsAfterCancel <= 12, $"{atmosphere.CallsAfterCancel}");
        Assert.Empty(reports);
    }

    [Fact]
    public void AScenarioStoppedInItsSecondSpacecraft_ReturnsNoSummary_AndIsNotSimulated()
    {
        // Arrange: a star and two spacecraft, the second stopped 30 min into its run; in Velocity-Verlet, whose outputs
        // are the cubic Hermite of its steps, so that the forces are read in the order of the epochs (an RK7(8) output is a
        // shortened step from the start of its accepted step, whose forces the model would take for a new run)
        var earth = NewEarth();
        var scenario = new Scenario("Cancel", new IO.Astrodynamics.Mission.Mission("Cancel"), TwoHours);
        var first = NewSpacecraft(earth, withManeuver: false);
        var second = NewSpacecraft(earth, withManeuver: false);
        var star = IO.Astrodynamics.Tests.Body.StarTests.NewStar(668301, Epoch);
        scenario.AddStar(star);
        scenario.AddSpacecraft(first);
        scenario.AddSpacecraft(second);
        using var cts = new CancellationTokenSource();
        var atmosphere = new CancellingAtmosphere(cts, Epoch.AddMinutes(30.0), afterRuns: 1);

        // Act
        var exception = Assert.Throws<OperationCanceledException>(() => scenario.Simulate(Options("VV", atmosphere), cts.Token));

        // Assert: the star and the first spacecraft complete, the second re-armed, the scenario not simulated
        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.Equal(0, atmosphere.CallsAfterCancel);
        Assert.True(star.IsPropagated);
        Assert.True(first.IsPropagated);
        Assert.Equal(TwoHours.EndDate, first.PropagatedStates[^1].Epoch);
        AssertLeftAsAStoppedRun(second);
        Assert.False(scenario.IsSimulated);
    }

    [Fact]
    public void ATaskRunOfPropagate_IsCancelledByItsToken()
    {
        // Arrange: the replacement of PropagateAsync, a task of the synchronous call
        var earth = NewEarth();
        var spacecraft = NewSpacecraft(earth);
        using var cts = new CancellationTokenSource();
        var atmosphere = new CancellingAtmosphere(cts, Epoch.AddMinutes(30.0));

        // Act
        var task = Task.Run(() => spacecraft.Propagate(TwoHours, Options("RK", atmosphere), cts.Token), cts.Token);

        // Assert: the task is cancelled with the token
        var exception = Assert.ThrowsAny<OperationCanceledException>(() => task.GetAwaiter().GetResult());
        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.True(task.IsCanceled);
        AssertLeftAsAStoppedRun(spacecraft);
    }

    #endregion

    /// <summary>
    /// The U.S. Standard Atmosphere 1976, which cancels <c>Source</c> at its first density read at or after
    /// <see cref="Threshold"/> once <see cref="AfterRuns"/> runs have started (a run starts when the epoch read falls back
    /// by more than 10 min; a negative count ignores the runs, for runs in parallel), and counts the reads after it.
    /// </summary>
    private sealed class CancellingAtmosphere(CancellationTokenSource source, TimeSystem.Time threshold, int afterRuns = 0)
        : IAtmosphericModel
    {
        private readonly EarthStandardAtmosphere _standard = new();
        private readonly object _lock = new();
        private TimeSystem.Time? _last;
        private int _runs;
        private bool _cancelled;

        private TimeSystem.Time Threshold => threshold;
        private int AfterRuns => afterRuns;

        internal int CallsAfterCancel { get; private set; }

        private void Read(IAtmosphericContext context)
        {
            if (source == null || context.Epoch is not { } epoch)
            {
                return;
            }

            lock (_lock)
            {
                if (_cancelled)
                {
                    CallsAfterCancel++;
                    return;
                }

                if (_last.HasValue && (_last.Value - epoch).TotalMinutes > 10.0)
                {
                    _runs++;
                }

                _last = epoch;
                if ((AfterRuns < 0 || _runs == AfterRuns) && epoch >= Threshold)
                {
                    _cancelled = true;
                    source.Cancel();
                }
            }
        }

        public IO.Astrodynamics.Atmosphere.Atmosphere GetAtmosphere(IAtmosphericContext context)
        {
            Read(context);
            return _standard.GetAtmosphere(context);
        }

        public double GetTemperature(IAtmosphericContext context) => _standard.GetTemperature(context);

        public double GetPressure(IAtmosphericContext context) => _standard.GetPressure(context);

        public double GetDensity(IAtmosphericContext context)
        {
            Read(context);
            return _standard.GetDensity(context);
        }
    }

    /// <summary>
    /// An integrator implemented outside the library, over a Velocity-Verlet at 10 s of the central gravity: it does not
    /// read the token, and cancels it at the end of its first segment.
    /// </summary>
    private sealed class OuterIntegrator : IIntegrator
    {
        private readonly VVIntegrator _inner;
        private readonly CancellationTokenSource _source;

        internal OuterIntegrator(CelestialItem body, CancellationTokenSource source)
        {
            _inner = new VVIntegrator(TimeSpan.FromSeconds(10.0));
            _inner.AddForce(new GravitationalAcceleration(body));
            _source = source;
        }

        internal int Segments { get; private set; }

        public void Initialize(StateVector initialState) => _inner.Initialize(initialState);

        public IntegrationResult IntegrateSegment(Vector3 startPosition, Vector3 startVelocity, TimeSystem.Time baseEpoch,
            double duration, IReadOnlyList<IEventDetector> eventDetectors = null)
        {
            var result = _inner.IntegrateSegment(startPosition, startVelocity, baseEpoch, duration, eventDetectors);
            Segments++;
            _source.Cancel();
            return result;
        }
    }

    // A progress that records each report on the reporting thread
    private sealed class SynchronousProgress(List<BatchProgress> reports) : IProgress<BatchProgress>
    {
        public void Report(BatchProgress value)
        {
            lock (reports)
            {
                reports.Add(value);
            }
        }
    }
}
