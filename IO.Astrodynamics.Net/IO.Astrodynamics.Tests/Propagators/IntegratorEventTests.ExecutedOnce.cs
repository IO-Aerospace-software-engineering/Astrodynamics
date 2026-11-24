// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Propagators;

// Inside IO.Astrodynamics.Tests the namespaces Frame and Time shadow the library types
using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

public partial class IntegratorEventTests
{
    // ------------------------------------------------------------------------------------------------------------------
    // Decision 97 of #668 (#802): a maneuver's detector executes its impulse once at most
    // ------------------------------------------------------------------------------------------------------------------

    /// <summary>The capturing integrators crossed with the member of the detector a stale call reaches.</summary>
    public static IEnumerable<object[]> CapturingIntegratorsAndMembers() =>
        from integrator in CapturingIntegrators()
        from member in new[] { "HandleEvent", "Evaluate" }
        select new[] { integrator[0], integrator[1], member };

    // The text of the exception of a detector that has executed its impulse (decision 97 of #668)
    private const string AlreadyExecuted = "already executed its impulse";

    // A state for a stale call, after the first impulse and before the second
    private static StateVector StaleState(CelestialItem earth) =>
        new(new Vector3(7e6, 0.0, 0.0), new Vector3(0.0, 7.5e3, 0.0), earth, MinimumEpochGateTests.GateAtTt(1500.0), Frame.ICRF);

    // A stale call on a detector: its HandleEvent or its Evaluate
    private static void StaleCall(IEventDetector detector, string member, StateVector state, Spacecraft spacecraft)
    {
        if (member == "HandleEvent")
        {
            detector.HandleEvent(state, spacecraft);
        }
        else
        {
            detector.Evaluate(state);
        }
    }

    /// <summary>
    /// The detector of the first impulse, kept by a caller's integrator and called (<see cref="IEventDetector.HandleEvent"/>
    /// and <see cref="IEventDetector.Evaluate"/>) before the second and the third segments, after the impulse executed:
    /// each call throws <see cref="InvalidOperationException"/> naming the maneuver, changes nothing (fuel, mass, ΔV,
    /// windows, executed maneuvers), and the run, the exceptions caught by the integrator, is bit for bit the run without
    /// the stale calls (decision 97 of #668, decided by S. Guillet, 2026-11-24; before, the third review's probe executed
    /// the first impulse a second time during the run: 6.74 kg burned, ΔV rewritten, nothing in the executed maneuvers).
    /// </summary>
    [Theory]
    [MemberData(nameof(CapturingIntegrators))]
    public void ADetectorCalledAfterItsImpulse_ThrowsAndChangesNothing(string wrapper, string inner)
    {
        // The reference: the same run without the stale calls
        var (freshSpacecraft, freshPlan, freshEarth) = TwoImpulses(-7971);
        var freshSolution = new CentralBodyPropagator(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), freshSpacecraft,
            NewCapturing(wrapper, inner, freshEarth, new CaptureHooks()), TimeSpan.FromSeconds(10.0)).Propagate();
        var fresh = Results(freshSolution, freshSpacecraft, freshPlan);

        var (spacecraft, plan, earth) = TwoImpulses(-7972);
        var messages = new List<string>();
        CaptureHooks hooks = null;
        hooks = new CaptureHooks
        {
            Before = call =>
            {
                if (call == 1)
                {
                    return;
                }

                var first = hooks.Captured[0];
                foreach (var member in new[] { "HandleEvent", "Evaluate" })
                {
                    var before = Footprint(spacecraft, plan);
                    var ex = Assert.Throws<InvalidOperationException>(() => StaleCall(first, member, StaleState(earth), spacecraft));
                    Assert.Equal(before, Footprint(spacecraft, plan));
                    messages.Add(ex.Message);
                }
            }
        };

        var solution = new CentralBodyPropagator(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), spacecraft,
            NewCapturing(wrapper, inner, earth, hooks), TimeSpan.FromSeconds(10.0)).Propagate();

        Assert.Equal(3, hooks.Calls);
        Assert.Equal(4, messages.Count);
        _output.WriteLine($"executed {wrapper} {inner}: {messages[0]}");
        Assert.All(messages, m =>
        {
            Assert.Contains(AlreadyExecuted, m);
            Assert.Contains(nameof(TimeLikeImpulse), m);
            Assert.DoesNotContain("finished run", m);
        });
        Assert.Equal(fresh, Results(solution, spacecraft, plan));
        Assert.Equal(2, spacecraft.ExecutedManeuvers.Count);
    }

    /// <summary>
    /// The same stale call, not caught by the caller's integrator: the run throws <see cref="InvalidOperationException"/>
    /// before the second segment, the first impulse burned once (decision 97 of #668).
    /// </summary>
    [Theory]
    [MemberData(nameof(CapturingIntegratorsAndMembers))]
    public void ADetectorCalledAfterItsImpulse_UncaughtThrowsTheRun(string wrapper, string inner, string member)
    {
        var (freshSpacecraft, freshPlan, freshEarth) = TwoImpulses(-7973);
        new CentralBodyPropagator(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), freshSpacecraft,
            NewCapturing(wrapper, inner, freshEarth, new CaptureHooks()), TimeSpan.FromSeconds(10.0)).Propagate();
        double oneBurn = freshPlan[0].FuelBurned;

        var (spacecraft, plan, earth) = TwoImpulses(-7974);
        CaptureHooks hooks = null;
        hooks = new CaptureHooks
        {
            Before = call =>
            {
                if (call == 2) StaleCall(hooks.Captured[0], member, StaleState(earth), spacecraft);
            }
        };
        var propagator = new CentralBodyPropagator(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), spacecraft,
            NewCapturing(wrapper, inner, earth, hooks), TimeSpan.FromSeconds(10.0));

        var ex = Assert.Throws<InvalidOperationException>(() => propagator.Propagate());
        Assert.Contains(AlreadyExecuted, ex.Message);
        Assert.Equal(1000.0 - oneBurn, spacecraft.GetRemainingFuel());
        Assert.Equal(oneBurn, plan[0].FuelBurned);
        Assert.Single(spacecraft.ExecutedManeuvers);
    }

    /// <summary>
    /// A caller's integrator that calls <see cref="IEventDetector.HandleEvent"/> itself on the detector of the event it
    /// returns: the impulse executes in that call, and the propagation's own <see cref="IEventDetector.HandleEvent"/>
    /// throws <see cref="InvalidOperationException"/>, so the run throws rather than burning twice; the fuel is that of one
    /// burn (decision 97 of #668).
    /// </summary>
    [Theory]
    [MemberData(nameof(CapturingIntegrators))]
    public void AnIntegratorThatHandlesItsEvent_ThrowsTheRunAndBurnsOnce(string wrapper, string inner)
    {
        var (freshSpacecraft, freshPlan, freshEarth) = TwoImpulses(-7975);
        new CentralBodyPropagator(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), freshSpacecraft,
            NewCapturing(wrapper, inner, freshEarth, new CaptureHooks()), TimeSpan.FromSeconds(10.0)).Propagate();
        double oneBurn = freshPlan[0].FuelBurned;

        var (spacecraft, plan, earth) = TwoImpulses(-7976);
        int handled = 0;
        var hooks = new CaptureHooks
        {
            Returning = (result, detectors, baseEpoch) =>
            {
                if (result.Event is not { } evt) return;
                var state = new StateVector(evt.EventPosition, evt.EventVelocity, earth, baseEpoch.AddSeconds(evt.EventTime), Frame.ICRF);
                detectors[evt.DetectorIndex].HandleEvent(state, spacecraft);
                handled++;
            }
        };
        var propagator = new CentralBodyPropagator(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), spacecraft,
            NewCapturing(wrapper, inner, earth, hooks), TimeSpan.FromSeconds(10.0));

        var ex = Assert.Throws<InvalidOperationException>(() => propagator.Propagate());
        _output.WriteLine($"handled by the integrator {wrapper} {inner}: {ex.Message}");
        Assert.Contains(AlreadyExecuted, ex.Message);
        Assert.Equal(1, handled);
        Assert.Equal(1000.0 - oneBurn, spacecraft.GetRemainingFuel());
        Assert.Equal(oneBurn, plan[0].FuelBurned);
    }

    /// <summary>
    /// A caller's integrator that calls <see cref="IEventDetector.HandleEvent"/> itself on the detector of its event and
    /// returns no event breaks the contract: the propagation takes a result without an event for the end of its window,
    /// and the run returns, its solution stopped at that event, the first impulse burned once, in the executed maneuvers of
    /// the spacecraft but not in the maneuvers of the solution, the second never run (the fourth review of L5a; the docs of decision 97 of #668).
    /// </summary>
    [Theory]
    [MemberData(nameof(CapturingIntegrators))]
    public void AnIntegratorThatHandlesAndHidesItsEvent_ReturnsAnIncompleteRun(string wrapper, string inner)
    {
        var (freshSpacecraft, freshPlan, freshEarth) = TwoImpulses(-7947);
        new CentralBodyPropagator(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), freshSpacecraft,
            NewCapturing(wrapper, inner, freshEarth, new CaptureHooks()), TimeSpan.FromSeconds(10.0)).Propagate();
        double oneBurn = freshPlan[0].FuelBurned;

        var (spacecraft, plan, earth) = TwoImpulses(-7948);
        int handled = 0;
        var hooks = new CaptureHooks
        {
            Returning = (result, detectors, baseEpoch) =>
            {
                if (result.Event is not { } evt) return;
                var state = new StateVector(evt.EventPosition, evt.EventVelocity, earth, baseEpoch.AddSeconds(evt.EventTime), Frame.ICRF);
                detectors[evt.DetectorIndex].HandleEvent(state, spacecraft);
                handled++;
            },
            Map = result => result with { Event = null }
        };

        var solution = new CentralBodyPropagator(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), spacecraft,
            NewCapturing(wrapper, inner, earth, hooks), TimeSpan.FromSeconds(10.0)).Propagate();

        Assert.Equal(1, handled);
        Assert.Single(solution.Segments);
        Assert.Empty(solution.Maneuvers);
        Assert.Same(plan[0], Assert.Single(spacecraft.ExecutedManeuvers));
        Assert.Equal(oneBurn, plan[0].FuelBurned);
        Assert.Equal(0.0, plan[1].FuelBurned);
        Assert.Equal(1000.0 - oneBurn, spacecraft.GetRemainingFuel());
    }

    // A plan of one time-like impulse at TT 1 100 s from the start, on an engine whose fuel flow of 1e-12 kg/s makes the
    // burn of its 10 m/s last past the range of a TimeSpan: its execution throws OverflowException after the fuel burned
    private static (Spacecraft Spacecraft, ImpulseManeuver Maneuver, CelestialBody Earth) SlowBurn(int naifId)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var spacecraft = new Spacecraft(naifId, $"IE{-naifId}", 1000.0, MinimumEpochGateTests.Orbit(earth, System.Math.PI), 3000.0,
            new Clock($"IE{-naifId}_CLK", 65536), cosparId: "2026-001A");
        var tank = new FuelTank("ft", 1000.0, 1000.0, "model", "sn");
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", 300.0, 1e-12, tank, "model", "sn");
        spacecraft.AddEngine(engine);
        var maneuver = new TimeLikeImpulse(earth, S, engine, MinimumEpochGateTests.GateAtTt(1100.0), 1.0);
        spacecraft.SetManeuvers(maneuver);
        return (spacecraft, maneuver, earth);
    }

    /// <summary>
    /// A detector whose execution throws after the fuel burned (a burn past the range of a <see cref="TimeSpan"/>) is
    /// disarmed by that call: a later <see cref="IEventDetector.HandleEvent"/>, <see cref="IEventDetector.Evaluate"/> or
    /// gated evaluation throws <see cref="InvalidOperationException"/> and changes nothing, the fuel that of one burn
    /// (decision 97 of #668; before, each call burned again: 1 000, 993.213, 986.450, 979.709 kg in the fourth review's
    /// probe).
    /// </summary>
    [Fact]
    public void ADetectorWhoseExecutionThrows_IsDisarmed()
    {
        var (spacecraft, maneuver, earth) = SlowBurn(-7940);
        var detector = new ManeuverEventDetector(maneuver);
        var state = StaleState(earth);

        var overflow = Assert.Throws<OverflowException>(() => detector.HandleEvent(state, spacecraft));
        _output.WriteLine($"first call: {overflow.Message}");
        double oneBurn = maneuver.FuelBurned;
        Assert.True(oneBurn > 0.0);

        var before = Footprint(spacecraft, [maneuver]);
        var handle = Assert.Throws<InvalidOperationException>(() => detector.HandleEvent(state, spacecraft));
        var evaluate = Assert.Throws<InvalidOperationException>(() => detector.Evaluate(state));
        var open = Assert.Throws<InvalidOperationException>(() => ((IGatedEventDetector)detector).EvaluateOpen(state));
        Assert.Equal(before, Footprint(spacecraft, [maneuver]));
        Assert.Contains(AlreadyExecuted, handle.Message);
        Assert.Equal(handle.Message, evaluate.Message);
        Assert.Equal(handle.Message, open.Message);
        Assert.Equal(1000.0 - oneBurn, spacecraft.GetRemainingFuel());
        Assert.Empty(spacecraft.ExecutedManeuvers);
    }

    /// <summary>
    /// The same execution in a run, a caller's integrator calling <see cref="IEventDetector.HandleEvent"/> twice on the
    /// detector of the event it returns and catching what they throw: the first call burns and throws
    /// <see cref="OverflowException"/>, the second and the propagation's own call throw
    /// <see cref="InvalidOperationException"/>, so the run throws, the fuel that of one burn (decision 97 of #668; before,
    /// three burns, 979.709 kg left, in the fourth review's probe).
    /// </summary>
    [Theory]
    [MemberData(nameof(CapturingIntegrators))]
    public void ADetectorWhoseExecutionThrowsInARun_BurnsOnce(string wrapper, string inner)
    {
        var (spacecraft, maneuver, earth) = SlowBurn(wrapper == "Bare" ? (inner == "VV" ? -7943 : -7944) : (inner == "VV" ? -7945 : -7946));
        var thrown = new List<Exception>();
        var hooks = new CaptureHooks
        {
            Returning = (result, detectors, baseEpoch) =>
            {
                if (result.Event is not { } evt) return;
                var state = new StateVector(evt.EventPosition, evt.EventVelocity, earth, baseEpoch.AddSeconds(evt.EventTime), Frame.ICRF);
                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        detectors[evt.DetectorIndex].HandleEvent(state, spacecraft);
                    }
                    catch (Exception ex)
                    {
                        thrown.Add(ex);
                    }
                }
            }
        };
        var propagator = new CentralBodyPropagator(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), spacecraft,
            NewCapturing(wrapper, inner, earth, hooks), TimeSpan.FromSeconds(10.0));

        var ex = Assert.Throws<InvalidOperationException>(() => propagator.Propagate());
        _output.WriteLine($"slow burn {wrapper} {inner}: {ex.Message}");
        Assert.Contains(AlreadyExecuted, ex.Message);
        Assert.Equal(2, thrown.Count);
        Assert.IsType<OverflowException>(thrown[0]);
        Assert.Contains(AlreadyExecuted, Assert.IsType<InvalidOperationException>(thrown[1]).Message);
        Assert.True(maneuver.FuelBurned > 0.0);
        Assert.Equal(1000.0 - maneuver.FuelBurned, spacecraft.GetRemainingFuel());
        Assert.Empty(spacecraft.ExecutedManeuvers);
    }

    /// <summary>
    /// An impulse whose g-function, the product (t − r1)(t − r2)(t − r3)(t − r4) of TT seconds, crosses from negative to
    /// positive at r2 and r4, and whose precondition fails at its first <c>skips</c> crossings.
    /// </summary>
    private sealed class TwoCrossingImpulse : ImpulseManeuver
    {
        private readonly Time[] _roots;
        private readonly int _skips;

        internal TwoCrossingImpulse(CelestialItem center, Engine engine, int skips, params Time[] roots) : base(center, S, TimeSpan.Zero, engine)
        {
            _roots = roots;
            _skips = skips;
        }

        internal int Checks { get; private set; }

        public override double ComputeEventValue(StateVector localState) =>
            _roots.Aggregate(1.0, (g, root) => g * (localState.Epoch - root).TotalSeconds);

        public override CrossingDirection EventCrossingDirection => CrossingDirection.NegativeToPositive;

        public override bool CheckPreconditions(StateVector state) => ++Checks > _skips;

        protected override Vector3 Execute(StateVector vector) => vector.Velocity.Normalize() * 10.0;
    }

    private static Time[] TwoCrossingRoots() =>
        [MinimumEpochGateTests.GateAtTt(1000.0), MinimumEpochGateTests.GateAtTt(1100.0), MinimumEpochGateTests.GateAtTt(1500.0), MinimumEpochGateTests.GateAtTt(2000.0)];

    /// <summary>
    /// A detector whose <see cref="IEventDetector.HandleEvent"/> skipped an occurrence (its precondition failed,
    /// <see cref="EventAction.Continue"/>) stays armed: its <see cref="IEventDetector.Evaluate"/>, its gated evaluation and
    /// its next <see cref="IEventDetector.HandleEvent"/> run, the last one executing the impulse; then each throws
    /// <see cref="InvalidOperationException"/> and changes nothing (decision 97 of #668).
    /// </summary>
    [Fact]
    public void ADetectorThatSkippedAnOccurrence_StaysArmedUntilItExecutes()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var (spacecraft, engine) = NewSpacecraft(-7977, MinimumEpochGateTests.Orbit(earth, System.Math.PI));
        var maneuver = new TwoCrossingImpulse(earth, engine, 1, TwoCrossingRoots());
        spacecraft.SetManeuvers(maneuver);
        var detector = new ManeuverEventDetector(maneuver);
        var state = StaleState(earth);

        var skipped = detector.HandleEvent(state, spacecraft);
        Assert.Equal(EventAction.Continue, skipped.Action);
        Assert.Same(detector, skipped.NextDetector);
        Assert.Equal(maneuver.ComputeEventValue(state), detector.Evaluate(state));
        Assert.Equal(maneuver.ComputeEventValue(state), ((IGatedEventDetector)detector).EvaluateOpen(state));

        var executed = detector.HandleEvent(state, spacecraft);
        Assert.Equal(EventAction.StopAndRestart, executed.Action);
        Assert.True(maneuver.FuelBurned > 0.0);

        var before = Footprint(spacecraft, [maneuver]);
        var handle = Assert.Throws<InvalidOperationException>(() => detector.HandleEvent(state, spacecraft));
        var evaluate = Assert.Throws<InvalidOperationException>(() => detector.Evaluate(state));
        var open = Assert.Throws<InvalidOperationException>(() => ((IGatedEventDetector)detector).EvaluateOpen(state));
        Assert.Equal(before, Footprint(spacecraft, [maneuver]));
        Assert.Equal(2, maneuver.Checks);
        Assert.Contains(AlreadyExecuted, handle.Message);
        Assert.Contains(nameof(TwoCrossingImpulse), handle.Message);
        Assert.Equal(handle.Message, evaluate.Message);
        Assert.Equal(handle.Message, open.Message);
    }

    /// <summary>
    /// In a run, a detector whose precondition failed at the first crossing (TT 1 100 s) stays armed and executes its
    /// impulse at the later crossing (TT 2 000 s), Velocity-Verlet and fixed-step RK7(8) at 10 s (decision 97 of #668).
    /// </summary>
    [Theory]
    [InlineData("VV")]
    [InlineData("RK")]
    public void ADetectorThatSkippedACrossing_ExecutesAtTheLaterOne(string inner)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var (spacecraft, engine) = NewSpacecraft(inner == "VV" ? -7978 : -7979, MinimumEpochGateTests.Orbit(earth, System.Math.PI));
        var maneuver = new TwoCrossingImpulse(earth, engine, 1, TwoCrossingRoots());
        spacecraft.SetManeuvers(maneuver);
        Integrator integrator = inner == "VV" ? new VVIntegrator(TimeSpan.FromSeconds(10.0)) : new RK78Integrator(10.0);
        integrator.AddForce(new GravitationalAcceleration(earth));

        var solution = new CentralBodyPropagator(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), spacecraft, integrator,
            TimeSpan.FromSeconds(10.0)).Propagate();

        Assert.Equal(2, maneuver.Checks);
        var record = Assert.Single(solution.Maneuvers);
        LabelAssert.Equal(MinimumEpochGateTests.GateAtTt(2000.0), record.Epoch);
        Assert.Equal(3, solution.Segments.Count);
    }
}
