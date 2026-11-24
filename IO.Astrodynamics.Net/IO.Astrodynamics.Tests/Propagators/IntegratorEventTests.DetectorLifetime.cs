// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    // Decision 95 of #668 (#802): a maneuver's detector serves the run that built it only
    // ------------------------------------------------------------------------------------------------------------------

    /// <summary>The integrators a caller gives the propagator: an <see cref="IIntegrator"/> implemented outside the
    /// library and a subclass of <see cref="Integrator"/>, each over Velocity-Verlet and fixed-step RK7(8) at 10 s.</summary>
    public static IEnumerable<object[]> CapturingIntegrators() =>
    [
        ["Bare", "VV"], ["Bare", "RK"], ["Subclass", "VV"], ["Subclass", "RK"]
    ];

    // The calls of a capturing integrator: before each segment (its number from 1) and after it
    private sealed class CaptureHooks
    {
        internal Action<int> Before { get; init; } = _ => { };
        internal Action<int> After { get; init; } = _ => { };
        internal List<IEventDetector> Captured { get; } = new();
        private int _calls;

        internal IntegrationResult Integrate(IIntegrator inner, Vector3 p, Vector3 v, Time b, double d, IReadOnlyList<IEventDetector> e)
        {
            int call = ++_calls;
            foreach (var detector in e ?? [])
            {
                if (!Captured.Contains(detector)) Captured.Add(detector);
            }

            Before(call);
            var result = inner.IntegrateSegment(p, v, b, d, e);
            After(call);
            return result;
        }
    }

    // An IIntegrator implemented outside the library, which keeps the detectors it receives
    private sealed class BareCapture(IIntegrator inner, CaptureHooks hooks) : IIntegrator
    {
        public void Initialize(StateVector initialState) => inner.Initialize(initialState);

        public IntegrationResult IntegrateSegment(Vector3 p, Vector3 v, Time b, double d, IReadOnlyList<IEventDetector> e = null) =>
            hooks.Integrate(inner, p, v, b, d, e);
    }

    // A subclass of Integrator, which keeps the detectors it receives
    private sealed class SubclassCapture(IIntegrator inner, CaptureHooks hooks) : Integrator
    {
        public override void Initialize(StateVector initialState)
        {
            base.Initialize(initialState);
            inner.Initialize(initialState);
        }

        public override IntegrationResult IntegrateSegment(Vector3 p, Vector3 v, Time b, double d, IReadOnlyList<IEventDetector> e = null) =>
            hooks.Integrate(inner, p, v, b, d, e);
    }

    private static IIntegrator NewCapturing(string wrapper, string inner, CelestialItem earth, CaptureHooks hooks)
    {
        Integrator integrator = inner == "VV" ? new VVIntegrator(TimeSpan.FromSeconds(10.0)) : new RK78Integrator(10.0);
        integrator.AddForce(new GravitationalAcceleration(earth));
        return wrapper == "Bare" ? new BareCapture(integrator, hooks) : new SubclassCapture(integrator, hooks);
    }

    // What a detector could change: the spacecraft's fuel and executed maneuvers, each maneuver's ΔV, burn and windows
    private static string Footprint(Spacecraft spacecraft, IEnumerable<ImpulseManeuver> maneuvers) =>
        $"fuel {spacecraft.GetRemainingFuel():R}, mass {spacecraft.GetTotalMass():R}, executed {spacecraft.ExecutedManeuvers.Count}; " +
        string.Join("; ", maneuvers.Select(m => $"dv {m.DeltaV}, burned {m.FuelBurned:R}, thrust {m.ThrustWindow}, maneuver {m.ManeuverWindow}, min {m.MinimumEpoch}"));

    // A plan of two time-like impulses, at TT 1 100 s and 2 000 s from the start, on a 3 000 s window
    private (Spacecraft Spacecraft, ImpulseManeuver[] Plan, CelestialBody Earth) TwoImpulses(int naifId)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var (spacecraft, engine) = NewSpacecraft(naifId, MinimumEpochGateTests.Orbit(earth, System.Math.PI));
        var m1 = new TimeLikeImpulse(earth, S, engine, MinimumEpochGateTests.GateAtTt(1100.0), 1.0);
        var m2 = new TimeLikeImpulse(earth, S, engine, MinimumEpochGateTests.GateAtTt(2000.0), 1.0);
        spacecraft.SetManeuvers(m1, m2);
        return (spacecraft, [m1, m2], earth);
    }

    // Both members of a detector a finished run built throw, and change nothing
    private void AssertDisarmed(IReadOnlyList<IEventDetector> detectors, Spacecraft spacecraft, ImpulseManeuver[] plan,
        CelestialItem earth, string what)
    {
        Assert.NotEmpty(detectors);
        var before = Footprint(spacecraft, plan);
        var state = new StateVector(new Vector3(7e6, 0.0, 0.0), new Vector3(0.0, 7.5e3, 0.0), earth,
            MinimumEpochGateTests.GateAtTt(2500.0), Frame.ICRF);
        foreach (var detector in detectors)
        {
            var handle = Assert.Throws<InvalidOperationException>(() => detector.HandleEvent(state, spacecraft));
            var evaluate = Assert.Throws<InvalidOperationException>(() => detector.Evaluate(state));
            _output.WriteLine($"{what}: {handle.Message}");
            Assert.Contains("finished run", handle.Message);
            Assert.Contains(nameof(TimeLikeImpulse), handle.Message);
            Assert.Equal(handle.Message, evaluate.Message);
        }

        Assert.Equal(before, Footprint(spacecraft, plan));
    }

    /// <summary>
    /// The detectors a caller's integrator kept from a completed run (the review of L5a's measure: a kept detector burned
    /// 472 kg of fuel and rewrote ΔV after the run): <see cref="IEventDetector.HandleEvent"/> and
    /// <see cref="IEventDetector.Evaluate"/> throw <see cref="InvalidOperationException"/> and change nothing, the
    /// detector of the first impulse (built at the start of the run) and that of the second (built at the first impulse)
    /// (decision 95 of #668, decided by S. Guillet, 2026-11-24).
    /// </summary>
    [Theory]
    [MemberData(nameof(CapturingIntegrators))]
    public void ADetectorKeptPastACompletedRun_Throws(string wrapper, string inner)
    {
        var (spacecraft, plan, earth) = TwoImpulses(-7961);
        var hooks = new CaptureHooks();
        var propagator = new CentralBodyPropagator(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), spacecraft,
            NewCapturing(wrapper, inner, earth, hooks), TimeSpan.FromSeconds(10.0));
        var solution = propagator.Propagate();

        Assert.Equal(2, solution.Maneuvers.Count);
        Assert.Equal(2, hooks.Captured.Count);
        AssertDisarmed(hooks.Captured, spacecraft, plan, earth, $"completed {wrapper} {inner}");
    }

    /// <summary>
    /// The detectors a caller's integrator kept from a run its token stopped (after the first impulse, before the second
    /// segment) throw and change nothing (decision 95 of #668).
    /// </summary>
    [Theory]
    [MemberData(nameof(CapturingIntegrators))]
    public void ADetectorKeptPastAStoppedRun_Throws(string wrapper, string inner)
    {
        var (spacecraft, plan, earth) = TwoImpulses(-7962);
        using var source = new CancellationTokenSource();
        var hooks = new CaptureHooks { After = call => { if (call == 1) source.Cancel(); } };
        var propagator = new CentralBodyPropagator(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), spacecraft,
            NewCapturing(wrapper, inner, earth, hooks), TimeSpan.FromSeconds(10.0));

        Assert.ThrowsAny<OperationCanceledException>(() => propagator.Propagate(source.Token));
        Assert.Single(hooks.Captured);
        AssertDisarmed(hooks.Captured, spacecraft, plan, earth, $"stopped {wrapper} {inner}");
    }

    /// <summary>
    /// The detectors a caller's integrator kept from a run that threw (the integrator itself, at its second segment) throw
    /// and change nothing (decision 95 of #668).
    /// </summary>
    [Theory]
    [MemberData(nameof(CapturingIntegrators))]
    public void ADetectorKeptPastARunThatThrew_Throws(string wrapper, string inner)
    {
        var (spacecraft, plan, earth) = TwoImpulses(-7963);
        var hooks = new CaptureHooks { Before = call => { if (call == 2) throw new ArithmeticException("integrator failure"); } };
        var propagator = new CentralBodyPropagator(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), spacecraft,
            NewCapturing(wrapper, inner, earth, hooks), TimeSpan.FromSeconds(10.0));

        Assert.Throws<ArithmeticException>(() => propagator.Propagate());
        Assert.Equal(2, hooks.Captured.Count);
        AssertDisarmed(hooks.Captured, spacecraft, plan, earth, $"thrown {wrapper} {inner}");
    }

    /// <summary>
    /// The detector of a <see cref="PlaneAlignmentManeuver"/> whose minimum epoch the run never reached, kept past it: an
    /// <see cref="IEventDetector.Evaluate"/> after the minimum epoch throws and leaves the node unlocked
    /// (<see cref="PlaneAlignmentManeuver.IsAscendingNode"/> null), where it locked it before decision 95 of #668.
    /// </summary>
    [Theory]
    [MemberData(nameof(CapturingIntegrators))]
    public void APlaneAlignmentDetectorKeptPastItsRun_LocksNoNode(string wrapper, string inner)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var (spacecraft, engine) = NewSpacecraft(-7964, MinimumEpochGateTests.Orbit(earth, System.Math.PI));
        var target = new KeplerianElements(MinimumEpochGateTests.A0, MinimumEpochGateTests.E0, 0.95, 0.3, 1.1, 0.0, earth, S, Frame.ICRF);
        var maneuver = new PlaneAlignmentManeuver(MinimumEpochGateTests.GateAtTt(5000.0), TimeSpan.Zero, target, engine);
        spacecraft.SetManeuvers(maneuver);
        var hooks = new CaptureHooks();
        var propagator = new CentralBodyPropagator(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), spacecraft,
            NewCapturing(wrapper, inner, earth, hooks), TimeSpan.FromSeconds(10.0));
        var solution = propagator.Propagate();

        Assert.Empty(solution.Maneuvers);
        Assert.Null(maneuver.IsAscendingNode);
        var detector = Assert.Single(hooks.Captured);
        var state = new StateVector(new Vector3(7e6, 0.0, 0.0), new Vector3(0.0, 7.5e3, 0.0), earth,
            MinimumEpochGateTests.GateAtTt(5100.0), Frame.ICRF);
        var before = Footprint(spacecraft, [maneuver]);

        var ex = Assert.Throws<InvalidOperationException>(() => detector.Evaluate(state));
        Assert.Contains(nameof(PlaneAlignmentManeuver), ex.Message);
        Assert.Throws<InvalidOperationException>(() => detector.HandleEvent(state, spacecraft));
        Assert.Null(maneuver.IsAscendingNode);
        Assert.Equal(before, Footprint(spacecraft, [maneuver]));
    }
}
