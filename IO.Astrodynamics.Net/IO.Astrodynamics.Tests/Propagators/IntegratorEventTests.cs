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
using IO.Astrodynamics.Tests.Body;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Propagators;

// Inside IO.Astrodynamics.Tests the namespaces Frame and Time shadow the library types
using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;
using OrbitalParams = IO.Astrodynamics.OrbitalParameters.OrbitalParameters;

/// <summary>
/// When the events of a segment fire (11.0.0): a g-function exactly zero at a step end is a crossing (#580), a gated
/// detector exactly zero at the base with its gate before the base fires there (#580, GMAT's convention), several detectors
/// report the earliest crossing (#578), a plane alignment starting just past a node takes the node ahead (#581), a
/// Velocity-Verlet event carries the state of its label (#572), and an impulse at the end of the propagation leaves the
/// solution with the state after it (#575).
/// </summary>
/// <remarks>
/// The references are written here: the roots of time-like g-functions, the Keplerian closed form of the orbit
/// (<see cref="KeplerClosedForm"/>; the 400 × 1 000 km orbit of #569 from its apogee, a point-mass Earth with the kernel
/// GM), and the same propagation without the event. RK7(8) limits are those of the families of
/// <see cref="MinimumEpochGateTests"/>.
/// </remarks>
public partial class IntegratorEventTests
{
    private static readonly Time S = MinimumEpochGateTests.S;
    private readonly ITestOutputHelper _output;

    public IntegratorEventTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    /// <summary>A detector without a gate: g = f(state), continuing the propagation.</summary>
    private sealed class Detector : IEventDetector
    {
        public Func<StateVector, double> G { get; init; }
        public CrossingDirection Direction { get; init; }
        public double Evaluate(StateVector state) => G(state);
        public EventResult HandleEvent(StateVector stateAtEvent, Spacecraft spacecraft) => new(stateAtEvent, null, EventAction.Continue, this);
        public bool IsActive => true;
    }

    private static Integrator NewIntegrator(string name) => name switch
    {
        "VV1" => new VVIntegrator(TimeSpan.FromSeconds(1.0)),
        "VV10" => new VVIntegrator(TimeSpan.FromSeconds(10.0)),
        "RKf10" => new RK78Integrator(10.0),
        "RKf60" => new RK78Integrator(60.0),
        "RK" => new RK78Integrator(1e-12, 1e-12),
        _ => throw new ArgumentException(name)
    };

    private static double Step(string name) => name switch
    {
        "VV1" => 1.0,
        "VV10" => 10.0,
        "RKf10" => 10.0,
        "RKf60" => 60.0,
        _ => 0.0
    };

    private static bool IsVelocityVerlet(string name) => name.StartsWith("VV");

    // A force-free straight line, x = t m, in the TDB time argument: a g-function of the epoch can be placed anywhere in t
    private static (Integrator Integrator, StateVector Start) Line(string integratorName)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var start = new StateVector(new Vector3(0.0, 7e6, 0.0), new Vector3(1.0, 0.0, 0.0), earth, S, Frame.ICRF);
        var integrator = NewIntegrator(integratorName);
        integrator.Initialize(start);
        return (integrator, start);
    }

    // ------------------------------------------------------------------------------------------------------------------
    // #580: a g-function exactly zero at a step end
    // ------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// g = ±(epoch − root) through the public <see cref="IIntegrator.IntegrateSegment"/>, a detector without a gate: a root
    /// on a step end of VV1, VV10 or fixed-step RK7(8) at 10 s is a crossing there (#580: before, the product of a zero with
    /// the previous g was no crossing, and the zero, the next previous g, hid every later one: no event in 400 s); a root
    /// inside a step fires at the step end after it (Velocity-Verlet) or at the root (RK7(8)). The restart from the event,
    /// with the same detector, finds no second crossing: a zero at the base of a detector without a gate is not a crossing
    /// (Hipparchus's convention).
    /// </summary>
    [Theory]
    [MemberData(nameof(StepEndZeroCases))]
    public void ZeroAtAStepEnd_IsACrossing(string integratorName, double root, CrossingDirection direction, double sign)
    {
        var (integrator, start) = Line(integratorName);
        var rootEpoch = S.AddSeconds(root);
        var detector = new Detector { Direction = direction, G = s => sign * (s.Epoch - rootEpoch).TotalSeconds };
        var result = integrator.IntegrateSegment(start.Position, start.Velocity, S, 400.0, [detector]);

        bool fires = direction switch
        {
            CrossingDirection.NegativeToPositive => sign > 0.0,
            CrossingDirection.PositiveToNegative => sign < 0.0,
            _ => true
        };
        if (!fires)
        {
            Assert.Null(result.Event);
            return;
        }

        Assert.NotNull(result.Event);
        double h = Step(integratorName);
        double expected = IsVelocityVerlet(integratorName) ? System.Math.Ceiling(root / h) * h : root;
        double t = result.Event.Value.EventTime;
        _output.WriteLine($"MEASURE #580 step-end zero {integratorName} root {root} s {direction} {sign:+0;-0}: event at {t:R} s, expected {expected:R} s");
        Assert.Equal(expected, t);

        // The caller restarts from the event with the same detector: no second crossing of the same root
        var evt = result.Event.Value;
        integrator.Initialize(new StateVector(evt.EventPosition, evt.EventVelocity, start.Observer, S.AddSeconds(t), Frame.ICRF));
        var again = integrator.IntegrateSegment(evt.EventPosition, evt.EventVelocity, S.AddSeconds(t), 50.0, [detector]);
        Assert.Null(again.Event);
    }

    public static IEnumerable<object[]> StepEndZeroCases()
    {
        foreach (var (integrator, roots) in new[] { ("VV1", new[] { 105.0, 100.5 }), ("VV10", new[] { 100.0, 100.5 }), ("RKf10", new[] { 100.0, 100.5 }), ("RK", new[] { 100.0 }) })
        foreach (var root in roots)
        foreach (var direction in new[] { CrossingDirection.NegativeToPositive, CrossingDirection.PositiveToNegative, CrossingDirection.Any })
        foreach (var sign in new[] { 1.0, -1.0 })
            yield return [integrator, root, direction, sign];
    }

    /// <summary>A user impulse whose g-function is ±(epoch − root), 10 m/s along the velocity.</summary>
    private sealed class TimeLikeImpulse : ImpulseManeuver
    {
        private readonly Time _root;
        private readonly double _sign;

        internal TimeLikeImpulse(CelestialItem center, Time minimumEpoch, Engine engine, Time root, double sign) : base(center, minimumEpoch, TimeSpan.Zero, engine)
        {
            _root = root;
            _sign = sign;
        }

        public override double ComputeEventValue(StateVector localState) => _sign * (localState.Epoch - _root).TotalSeconds;

        public override CrossingDirection EventCrossingDirection => _sign > 0.0 ? CrossingDirection.NegativeToPositive : CrossingDirection.PositiveToNegative;

        protected override Vector3 Execute(StateVector vector) => vector.Velocity.Normalize() * 10.0;
    }

    private static (Spacecraft Spacecraft, Engine Engine) NewSpacecraft(int naifId, OrbitalParams orbit)
    {
        var spacecraft = new Spacecraft(naifId, $"IE{-naifId}", 1000.0, 3000.0, new Clock($"IE{-naifId}_CLK", 65536), orbit);
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        return (spacecraft, engine);
    }

    /// <summary>
    /// A time-like user <see cref="ImpulseManeuver"/> through <c>Spacecraft.Propagate</c>, whose root is TT 1 100 s from the
    /// start, a step end of VV1, VV10 and fixed-step RK7(8) at 10 s, in both directions, its minimum epoch at the start: it
    /// fires at its root, the base of the second segment exactly (#580: it never fired in the 3 000 s window). Adaptive
    /// RK7(8) fires there too.
    /// </summary>
    [Theory]
    [InlineData("VV1", 1.0)]
    [InlineData("VV1", -1.0)]
    [InlineData("VV10", 1.0)]
    [InlineData("VV10", -1.0)]
    [InlineData("RKf10", 1.0)]
    [InlineData("RKf10", -1.0)]
    [InlineData("RK", 1.0)]
    [InlineData("RK", -1.0)]
    public void TimeLikeImpulse_OnAStepEnd_FiresAtItsRoot(string integratorName, double sign)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var (spacecraft, engine) = NewSpacecraft(sign > 0.0 ? -7901 : -7902, MinimumEpochGateTests.Orbit(earth, System.Math.PI));
        var root = MinimumEpochGateTests.GateAtTt(1100.0);
        var impulse = new TimeLikeImpulse(earth, S, engine, root, sign);
        spacecraft.SetStandbyManeuver(impulse);
        var solution = spacecraft.Propagate(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), [earth], NewIntegrator(integratorName), false, false, TimeSpan.FromSeconds(10.0));

        _output.WriteLine($"MEASURE #580 time-like impulse {integratorName} {sign:+0;-0}: {solution.Segments.Count} segments" +
                          (solution.Segments.Count > 1 ? $", burn at TT {MinimumEpochGateTests.Tt(solution.Segments[1].BaseEpoch):R} s" : ""));
        Assert.Equal(2, solution.Segments.Count);
        Assert.Equal(root, solution.Segments[1].BaseEpoch);
        Assert.Equal(10.0, impulse.DeltaV.Magnitude(), 12);
        Assert.Contains(impulse, spacecraft.ExecutedManeuvers);
    }

    /// <summary>
    /// The apogee given by Keplerian elements (M0 = π, r·v = 0.0 exactly at the start) and a <see cref="PerigeeHeightManeuver"/>
    /// whose minimum epoch is before the start: the zero at the base is a crossing there, since r·v is negative after it
    /// (GMAT's convention, #577's choice 6, decided by S. Guillet, 2026-11-17 for #580), the burn RK7(8) fires at the start
    /// and Velocity-Verlet at its first step end (the tick of its label, #572), as with the minimum epoch at the start (the row zero/at-base of
    /// <see cref="MinimumEpochGateTests"/>). Before, the burn came one orbit later (S + 5 926.379 s in RK7(8)).
    /// </summary>
    [Theory]
    [InlineData("VV1", 100.0)]
    [InlineData("VV10", 100.0)]
    [InlineData("RK", 100.0)]
    [InlineData("RKf60", 100.0)]
    [InlineData("RK", 1.0e-7)]
    [InlineData("RK", 0.0)]
    public void ApsisExactlyAtTheBase_GatedBeforeIt_FiresAtTheBase(string integratorName, double gateBefore)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var orbit = MinimumEpochGateTests.Orbit(earth, System.Math.PI);
        var sv = orbit.ToStateVector();
        Assert.Equal(0.0, sv.Position * sv.Velocity);
        var (spacecraft, engine) = NewSpacecraft(-7903, orbit);
        var gate = S.Add(TimeSpan.FromSeconds(-gateBefore));
        var maneuver = new PerigeeHeightManeuver(earth, gate, TimeSpan.Zero, 6878137.0, engine);
        spacecraft.SetStandbyManeuver(maneuver);
        var solution = spacecraft.Propagate(new Window(S, MinimumEpochGateTests.GateAtTt(3000.0)), [earth], NewIntegrator(integratorName), false, false, TimeSpan.FromSeconds(10.0));

        Assert.True(solution.Segments.Count >= 2, $"{solution.Segments.Count} segments: no burn in the window");
        double burn = MinimumEpochGateTests.Tt(solution.Segments[1].BaseEpoch);
        // Velocity-Verlet fires at the end of its first step, carried onto the tick of its label (#572)
        double expected = IsVelocityVerlet(integratorName) ? MinimumEpochGateTests.Tt(MinimumEpochGateTests.GateAtTt(Step(integratorName))) : 0.0;
        var k = new KeplerClosedForm(earth.GM, sv.Position, sv.Velocity, 0.0);
        double deltaV = maneuver.DeltaV.Magnitude() - k.PerigeeChange(6878137.0);
        _output.WriteLine($"MEASURE #580 apsis at the base, gate {gateBefore} s before, {integratorName}: burn at TT {burn:R} s (expected {expected} s), ΔV less the closed form {deltaV:E3} m/s");
        Assert.Equal(expected, burn);
        if (!IsVelocityVerlet(integratorName))
        {
            Assert.True(System.Math.Abs(deltaV) <= MinimumEpochGateTests.Rk78DeltaV["apsis"], $"{deltaV:E3} m/s");
        }
    }
}
