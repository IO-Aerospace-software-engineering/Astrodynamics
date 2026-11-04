// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.Tests.Body;
using IO.Astrodynamics.Tests.Propagators.Variational;
using IO.Astrodynamics.Tests.SSA;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Propagators;

// Inside IO.Astrodynamics.Tests the namespaces Frame and Time shadow the library types
using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The edges of the minimum-epoch gate (#569): a g-function exactly zero at the gate, inside a step, on a step end and at
/// the base (choice 6 of the design); a UTC gate whose TDB label compares before it (choice 1); a root just after the gate
/// with <see cref="EventAction.Continue"/>, through the propagator's calls and through <c>Spacecraft.Propagate</c>
/// (choice 3); two gated detectors through
/// <see cref="IIntegrator.IntegrateSegment"/>; the search-start rule of <see cref="PlaneAlignmentManeuver"/> (choice 7); the
/// public <see cref="ManeuverEventDetector.Evaluate"/> (choice 8); <c>EvaluateAvoidance</c> after an impulse whose gate
/// moved its burn; the two-<c>Propagate</c> construction of GMAT in SSB mode.
/// </summary>
/// <remarks>
/// References are written in the test: the zeros and roots of g-functions placed on a force-free straight line, and the
/// Keplerian closed form of <see cref="KeplerClosedForm"/>. Limits PROVISIONAL, twice the Linux worst (2026-11-04), until
/// the CI measures Linux, Windows and macOS arm64.
/// </remarks>
public class MinimumEpochGateEdgeTests
{
    /// <summary><c>EvaluateAvoidance</c>: the state of an option at its TCA against the closed form of the orbit after the
    /// impulse and the option's burn, m. Worst 8.72e-6 m (Linux x64). PROVISIONAL.</summary>
    internal const double AvoidanceAgainstClosedForm = 1.8e-5;

    /// <summary>SSB mode: the burn of one propagation gated at the minimum epoch against the burn of the propagation from
    /// the state at the minimum epoch, gated at its base (GMAT's two <c>Propagate</c> commands), s: the integration noise of
    /// RK7(8) at 1e-9 over two step sequences. Worst 4.79e-5 s (the apogee raise; 6.30e-6 s the perigee raise; Linux
    /// x64). PROVISIONAL.</summary>
    internal const double TwoPropagateSsb = 9.6e-5;

    private static readonly Time S = MinimumEpochGateTests.S;
    private readonly ITestOutputHelper _output;

    public MinimumEpochGateEdgeTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    /// <summary>A gated detector: +1 before its gate, as the guard of <see cref="ManeuverEventDetector"/>, g otherwise; the
    /// event continues the propagation.</summary>
    private sealed class GatedDetector : IGatedEventDetector
    {
        public Time GateEpoch { get; init; }
        public Func<StateVector, double> G { get; init; }
        public CrossingDirection Direction { get; init; }
        public double Evaluate(StateVector state) => state.Epoch < GateEpoch ? 1.0 : G(state);
        public EventResult HandleEvent(StateVector stateAtEvent, Spacecraft spacecraft) => new(stateAtEvent, null, EventAction.Continue, this);
        public bool IsActive => true;
        Time IGatedEventDetector.Gate => GateEpoch;
        double IGatedEventDetector.EvaluateOpen(StateVector state) => G(state);
    }

    private static Integrator NewIntegrator(string name) => name switch
    {
        "VV1" => new VVIntegrator(TimeSpan.FromSeconds(1.0)),
        "VV10" => new VVIntegrator(TimeSpan.FromSeconds(10.0)),
        "RKf10" => new RK78Integrator(10.0),
        "RK" => new RK78Integrator(1e-12, 1e-12),
        _ => throw new ArgumentException(name)
    };

    // A force-free straight line, x = t m: a g-function of the position or the epoch can be placed anywhere in t
    private static IntegrationResult IntegrateLine(Integrator integrator, IEventDetector detector, double duration = 400.0)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var start = new StateVector(new Vector3(0.0, 7e6, 0.0), new Vector3(1.0, 0.0, 0.0), earth, S, Frame.ICRF);
        integrator.Initialize(start);
        return integrator.IntegrateSegment(start.Position, start.Velocity, S, duration, [detector]);
    }

    /// <summary>
    /// g = ±(epoch − gate), exactly zero at the gate: a crossing at the gate when the sign that follows it is the target
    /// sign. RK7(8) fires at the gate; Velocity-Verlet at the first step end whose sign is known after it, the next one
    /// when the gate is a step end. Gates inside a step (100.5 s), on a step end of VV1, VV10 and fixed-step RK7(8) at 10 s
    /// (100 s), on a step end of VV1 inside a VV10 step (105 s), at the base (0 s); both directions and either.
    /// </summary>
    [Theory]
    [MemberData(nameof(ZeroCases))]
    public void ZeroAtTheGate_IsACrossingThereWhenTheSignAfterIsTheTarget(string integratorName, double gateSeconds, CrossingDirection direction, double sign)
    {
        var gate = S.AddSeconds(gateSeconds);
        var detector = new GatedDetector { GateEpoch = gate, Direction = direction, G = s => sign * (s.Epoch - gate).TotalSeconds };
        var result = IntegrateLine(NewIntegrator(integratorName), detector);

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
        double t = result.Event.Value.EventTime;
        double expected = integratorName switch
        {
            "VV1" => System.Math.Floor(gateSeconds) + 1.0,
            "VV10" => System.Math.Floor(gateSeconds / 10.0) * 10.0 + 10.0,
            _ => gateSeconds
        };
        _output.WriteLine($"MEASURE #569 gate zero {integratorName} gate {gateSeconds} s {direction} {sign:+0;-0}: event at {t:R} s, expected {expected:R} s");
        Assert.Equal(expected, t);
    }

    public static IEnumerable<object[]> ZeroCases()
    {
        foreach (var integrator in new[] { "VV1", "VV10", "RKf10", "RK" })
        foreach (var gate in new[] { 100.5, 100.0, 105.0, 0.0 })
        foreach (var direction in new[] { CrossingDirection.NegativeToPositive, CrossingDirection.PositiveToNegative, CrossingDirection.Any })
        foreach (var sign in new[] { 1.0, -1.0 })
            yield return [integrator, gate, direction, sign];
    }

    /// <summary>
    /// A UTC gate whose TDB label compares before it (1 UTC gate in 200 000; none in TT or TAI): the root of
    /// g = TT(epoch) − TT(gate), at the gate's instant, is labelled at the first TDB tick that does not compare before the
    /// gate in RK7(8), adaptive and fixed-step; Velocity-Verlet fires at the step end after it. Before the review of #569,
    /// fixed-step RK7(8) at 10 s labelled it one tick before the gate (choice 1).
    /// </summary>
    [Theory]
    [InlineData("RKf10")]
    [InlineData("RK")]
    [InlineData("VV1")]
    [InlineData("VV10")]
    public void UtcGate_WhoseTdbTickComparesBeforeIt_TheEventIsNotBeforeTheGate(string integratorName)
    {
        var gate = new Time(new DateTime(2024, 3, 1, 12, 0, 33, DateTimeKind.Utc).AddTicks(2826603), TimeFrame.UTCFrame);
        Assert.True(gate.ToTDB() < gate);
        var firstTickNotBefore = gate.ToTDB();
        while (firstTickNotBefore < gate) firstTickNotBefore += TimeSpan.FromTicks(1);

        double gateSeconds = PropagationTimeArgument.Tt.SecondsBetween(S, gate);
        var detector = new GatedDetector
        {
            GateEpoch = gate, Direction = CrossingDirection.NegativeToPositive,
            G = s => PropagationTimeArgument.Tt.SecondsBetween(S, s.Epoch) - gateSeconds
        };
        var integrator = NewIntegrator(integratorName);
        var result = IntegrateLine(integrator, detector);

        Assert.NotNull(result.Event);
        double t = result.Event.Value.EventTime;
        var epoch = integrator.TimeArgument.EpochAt(S, t);
        long ticks = epoch.ToTAI().DateTime.Ticks - gate.ToTAI().DateTime.Ticks;
        _output.WriteLine($"MEASURE #569 gate UTC {integratorName}: gate {gate.DateTime:O} UTC at {gateSeconds:R} TT s, event {epoch.DateTime:O} TDB at {t:R} s, {ticks} ticks (TAI) from the gate");
        Assert.False(epoch < gate);
        if (integratorName.StartsWith("RK"))
            Assert.Equal(firstTickNotBefore, epoch);
        else
            Assert.Equal(System.Math.Floor(gateSeconds / (integratorName == "VV1" ? 1.0 : 10.0) + 1.0) * (integratorName == "VV1" ? 1.0 : 10.0), t);
    }

    /// <summary>
    /// A root 1e-11 to 9e-11 s after its gate, with <see cref="EventAction.Continue"/>, through the calls the propagator
    /// makes (the next segment from the event, <c>SkipRootOfLastEvent</c>): one event, at or after the gate, in both
    /// directions, adaptive and fixed-step RK7(8) (choice 3: the whole-step bracket of the first prototype labelled 12 to
    /// 13.5 % of them one tick before the gate and looped without end).
    /// </summary>
    [Theory]
    [InlineData("RKf10")]
    [InlineData("RK")]
    public void RootJustAfterTheGate_WithContinue_OneEventAtOrAfterTheGate(string integratorName)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        int runs = 0, single = 0;
        foreach (var direction in new[] { CrossingDirection.NegativeToPositive, CrossingDirection.PositiveToNegative })
        for (int i = 1; i <= 60; i++)
        foreach (var offset in new[] { 1e-11, 4e-11, 9e-11 })
        {
            var gate = S.AddSeconds(System.Math.Round(100.5 + 0.0137 * i, 7));
            double root = (gate - S).TotalSeconds + offset;
            double sign = direction == CrossingDirection.NegativeToPositive ? 1.0 : -1.0;
            var detector = new GatedDetector { GateEpoch = gate, Direction = direction, G = s => sign * (s.Position.X - root) };
            var integrator = (RK78Integrator)NewIntegrator(integratorName);
            var events = new List<Time>();
            var position = new Vector3(0.0, 7e6, 0.0);
            var velocity = new Vector3(1.0, 0.0, 0.0);
            var baseEpoch = S;
            double remaining = 400.0;
            integrator.Initialize(new StateVector(position, velocity, earth, baseEpoch, Frame.ICRF));
            for (int segment = 0; segment < 8; segment++)
            {
                var result = integrator.IntegrateSegment(position, velocity, baseEpoch, remaining, [detector]);
                if (!result.Event.HasValue) break;
                var e = result.Event.Value;
                baseEpoch = baseEpoch.AddSeconds(e.EventTime);
                events.Add(baseEpoch);
                (position, velocity, remaining) = (e.EventPosition, e.EventVelocity, remaining - e.EventTime);
                integrator.Initialize(new StateVector(position, velocity, earth, baseEpoch, Frame.ICRF));
                integrator.SkipRootOfLastEvent();
            }

            runs++;
            Assert.Single(events);
            Assert.True(events[0] >= gate, $"event {(events[0] - gate).Ticks} ticks from the gate, root +{offset:E0} s");
            single++;
        }

        _output.WriteLine($"MEASURE #569 gate Continue {integratorName}: {single} of {runs} runs with one event at or after the gate");
    }

    /// <summary>
    /// The review's case through <c>Spacecraft.Propagate</c>: from 0.03 rad before the apogee (M0 = 3.1115924495453107), a
    /// <see cref="CombinedManeuver"/> whose precondition fails (|apogee · node| = 0.45), gated one tick after the label of the
    /// apogee root, completes with one <see cref="EventAction.Continue"/> at or after its gate, not executed; a
    /// <see cref="PerigeeHeightManeuver"/> gated there fires at or after it.
    /// </summary>
    [Fact]
    public void RootJustAfterTheGate_ThroughPropagate_ContinueOnce()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        const double m0 = 3.1115924495453107;
        (Spacecraft, ImpulseManeuver) Build(int id, Func<Engine, ImpulseManeuver> maneuver)
        {
            var spacecraft = new Spacecraft(id, $"GE{-id}", 1000.0, 3000.0, new Clock($"GE{-id}_CLK", 65536), MinimumEpochGateTests.Orbit(earth, m0));
            var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
            spacecraft.AddFuelTank(tank);
            var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
            spacecraft.AddEngine(engine);
            var m = maneuver(engine);
            spacecraft.SetStandbyManeuver(m);
            return (spacecraft, m);
        }

        PropagationSolution Propagate(Spacecraft spacecraft)
        {
            var task = Task.Run(() => spacecraft.Propagate(new Window(S, S.AddSeconds(200.0)), [earth], new RK78Integrator(1e-12, 1e-12), false, false,
                TimeSpan.FromSeconds(10.0)));
            Assert.True(task.Wait(TimeSpan.FromSeconds(60.0)), "the propagation did not end");
            return task.Result;
        }

        var (ungated, _) = Build(-7801, e => new PerigeeHeightManeuver(earth, S, TimeSpan.Zero, 6878137.0, e));
        var root = Propagate(ungated).Segments[1].BaseEpoch;
        var gate = root.Add(TimeSpan.FromTicks(1));

        var (perigee, _) = Build(-7802, e => new PerigeeHeightManeuver(earth, gate, TimeSpan.Zero, 6878137.0, e));
        var gated = Propagate(perigee);
        var (combined, maneuver) = Build(-7803, e => new CombinedManeuver(earth, gate, TimeSpan.Zero, 6878137.0, 0.45, e));
        var skipped = Propagate(combined);
        _output.WriteLine($"MEASURE #569 gate Continue Propagate: apogee root label {MinimumEpochGateTests.Tt(root):R} s, gated burn {(gated.Segments[1].BaseEpoch - gate).Ticks} ticks from the gate, " +
                          $"{skipped.Segments.Count - 1} Continue, {(skipped.Segments.Count > 1 ? (skipped.Segments[1].BaseEpoch - gate).Ticks : 0)} ticks from the gate");
        Assert.Equal(2, gated.Segments.Count);
        Assert.True(gated.Segments[1].BaseEpoch >= gate);
        Assert.Equal(2, skipped.Segments.Count);
        Assert.True(skipped.Segments[1].BaseEpoch >= gate);
        Assert.Null(maneuver.ThrustWindow);
    }

    /// <summary>
    /// Two gated detectors through the public <see cref="IIntegrator.IntegrateSegment"/>, whose gates open in the same step:
    /// a <see cref="PerigeeHeightManeuver"/> gated 2 s before the perigee (r·v &lt; 0, a false crossing for the guard) and an
    /// <see cref="ApogeeHeightManeuver"/> gated 3 s before it. Each is judged on its real g at its gate, in either order: the
    /// event is the perigee of the second.
    /// </summary>
    [Theory]
    [InlineData("VV1")]
    [InlineData("VV10")]
    [InlineData("RK")]
    public void TwoGatedDetectors_EachJudgedOnItsRealG(string integratorName)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var orbit = MinimumEpochGateTests.Orbit(earth, System.Math.PI);
        var sv = orbit.ToStateVector();
        var k = new KeplerClosedForm(earth.GM, sv.Position, sv.Velocity, 0.0);
        double perigee = k.T / 2.0;
        foreach (bool apogeeFirst in new[] { true, false })
        {
            var spacecraft = new Spacecraft(-7811, "GE7811", 1000.0, 3000.0, new Clock("GE7811_CLK", 65536), orbit);
            var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
            spacecraft.AddFuelTank(tank);
            var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
            spacecraft.AddEngine(engine);
            var apogee = new ManeuverEventDetector(new ApogeeHeightManeuver(earth, MinimumEpochGateTests.GateAtTt(perigee - 3.0), TimeSpan.Zero, 8378137.0, engine));
            var lowered = new ManeuverEventDetector(new PerigeeHeightManeuver(earth, MinimumEpochGateTests.GateAtTt(perigee - 2.0), TimeSpan.Zero, 6878137.0, engine));
            IEventDetector[] detectors = apogeeFirst ? [apogee, lowered] : [lowered, apogee];
            var integrator = NewIntegrator(integratorName);
            integrator.AddForce(new GravitationalAcceleration(earth));
            integrator.Initialize(sv);
            integrator.TimeArgument = PropagationTimeArgument.Tt;
            var result = integrator.IntegrateSegment(sv.Position, sv.Velocity, S, 4000.0, detectors);
            Assert.NotNull(result.Event);
            Assert.Same(apogee, detectors[result.Event.Value.DetectorIndex]);
            double error = result.Event.Value.EventTime - perigee;
            _output.WriteLine($"MEASURE #569 gate two detectors {integratorName} apogee first {apogeeFirst}: event - perigee {error:E2} s");
            if (integratorName == "RK") Assert.True(System.Math.Abs(error) <= MinimumEpochGateTests.Rk78Epoch["apsis"], $"{error:E3} s");
            else Assert.InRange(error, 0.0, integratorName == "VV1" ? 1.0 : 10.0);
        }
    }

    /// <summary>
    /// <see cref="PlaneAlignmentManeuver"/> at the start of a search, within 1e-6 |r| of a node: the node it moves toward,
    /// never one it has just passed (choice 7); outside the threshold, the node of the sign of g. Its g-function at a node
    /// handed to it keeps the node by dg/dt (<see cref="PlaneAlignmentManeuver.ComputeEventValue"/>).
    /// </summary>
    [Theory]
    [InlineData(-1.0, 1.0, true)]
    [InlineData(1.0, 1.0, false)]
    [InlineData(1.0, -1.0, false)]
    [InlineData(-1.0, -1.0, true)]
    [InlineData(-1.0e5, 1.0, true)]
    [InlineData(1.0e5, 1.0, false)]
    public void PlaneAlignment_SearchStart_TakesTheNodeItMovesToward(double height, double rate, bool ascending)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var target = new KeplerianElements(7000000.0, 0.0, 0.5, 0.0, 0.0, 0.0, earth, S, Frame.ICRF);
        var normal = target.SpecificAngularMomentum().Normalize();
        var spacecraft = new Spacecraft(-7821, "GE7821", 1000.0, 3000.0, new Clock("GE7821_CLK", 65536), target);
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        var inPlane = normal.Cross(Vector3.VectorZ).Normalize();
        var state = new StateVector(inPlane * 7000000.0 + normal * height, normal.Cross(inPlane) * 7500.0 + normal * (rate * 100.0), earth, S, Frame.ICRF);

        var atSearchStart = new PlaneAlignmentManeuver(S, TimeSpan.Zero, target, engine);
        double g = atSearchStart.ComputeEventValueAtSearchStart(state);
        var handed = new PlaneAlignmentManeuver(S, TimeSpan.Zero, target, engine);
        double gHanded = handed.ComputeEventValue(state);

        Assert.Equal(height, g, 1e-6);
        Assert.Equal(g, gHanded);
        Assert.Equal(ascending, atSearchStart.IsAscendingNode);
        Assert.Equal(ascending ? CrossingDirection.NegativeToPositive : CrossingDirection.PositiveToNegative, atSearchStart.EventCrossingDirection);
        // Within the threshold, the node of a state handed to the maneuver is the one of dg/dt
        bool byRate = System.Math.Abs(height) < 7.0 ? rate > 0.0 : ascending;
        Assert.Equal(byRate, handed.IsAscendingNode);
        // Once locked, the search start is the g-function itself
        Assert.Equal(atSearchStart.ComputeEventValue(state), atSearchStart.ComputeEventValueAtSearchStart(state));
    }

    /// <summary>
    /// The public <see cref="ManeuverEventDetector.Evaluate"/> keeps its +1 before the minimum epoch (choice 8), for a caller
    /// outside the library; the gate the integrators read is the minimum epoch, and the g-function there is the maneuver's.
    /// </summary>
    [Fact]
    public void ManeuverEventDetector_EvaluateKeepsItsGuard_TheGateIsTheMinimumEpoch()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var orbit = MinimumEpochGateTests.Orbit(earth, 1.0);
        var spacecraft = new Spacecraft(-7831, "GE7831", 1000.0, 3000.0, new Clock("GE7831_CLK", 65536), orbit);
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        var gate = S.AddSeconds(1000.0);
        var detector = new ManeuverEventDetector(new ApogeeHeightManeuver(earth, gate, TimeSpan.Zero, 8378137.0, engine));
        var state = orbit.ToStateVector();
        double rv = state.Position * state.Velocity;

        Assert.Equal(1.0, detector.Evaluate(state));
        var gated = (IGatedEventDetector)detector;
        Assert.Equal(gate, gated.Gate);
        Assert.Equal(rv, gated.EvaluateOpen(state));
        var atGate = new StateVector(state.Position, state.Velocity, earth, gate, Frame.ICRF);
        Assert.Equal(rv, detector.Evaluate(atGate));
    }

    /// <summary>
    /// <c>EvaluateAvoidance</c> after an impulse whose minimum epoch the fix moves (face A: a perigee raise gated where
    /// r·v &lt; 0, which fired at the gate before #569): its burn at the apogee, and each option, propagated from the state
    /// after its burn with the force model of the trajectory, against the closed form of the orbit after the impulse and
    /// the option's burn, at the option's TCA.
    /// </summary>
    [Fact]
    public void EvaluateAvoidance_AfterAMovedGate_TheOptionIsTheClosedFormOfItsOrbit()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var orbit = MinimumEpochGateTests.Orbit(earth, System.Math.PI);
        var sv = orbit.ToStateVector();
        var k = new KeplerClosedForm(earth.GM, sv.Position, sv.Velocity, 0.0);
        var protectedCraft = new Spacecraft(-7841, "GE7841", 1000.0, 3000.0, new Clock("GE7841_CLK", 65536), sv);
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        protectedCraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        protectedCraft.AddEngine(engine);
        var gate = MinimumEpochGateTests.GateAtTt(1000.0);
        var impulse = new PerigeeHeightManeuver(earth, gate, TimeSpan.Zero, 6878137.0, engine);
        protectedCraft.SetStandbyManeuver(impulse);
        var window = new Window(S, S.AddHours(3.0));
        var solution = protectedCraft.Propagate(window, [earth], new RK78Integrator(1e-12, 1e-12), false, false, TimeSpan.FromSeconds(60.0));

        // The closed form: the apogee at or after the gate, the perigee raise along the velocity there
        double tA = k.NextApsis(MinimumEpochGateTests.Tt(gate), true);
        var (rA, vA) = k.StateAt(tA);
        var afterImpulse = new KeplerClosedForm(k.Mu, rA, vA + vA.Normalize() * k.PerigeeChange(6878137.0), tA);
        double burnError = MinimumEpochGateTests.Tt(solution.Segments[1].BaseEpoch) - tA;
        Assert.True(System.Math.Abs(burnError) <= MinimumEpochGateTests.Rk78Epoch["apsis"], $"impulse {burnError:E3} s from the apogee");

        var tca = S.AddSeconds(8000.0);
        var secondary = ReferenceCase.NewSpacecraft(-7842, AvoidanceFallbackCase.SecondaryNear(protectedCraft.GetEphemeris(tca, earth, Frame.ICRF, Aberration.None).ToStateVector()));
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondary, new Window(S.AddSeconds(6500.0), S.AddSeconds(9500.0)));
        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter, new AvoidanceSearchOptions
        {
            LeadTimes = [TimeSpan.FromMinutes(20.0)], DeltaVMagnitudesMetersPerSecond = [0.1], MaxReturnedOptions = 6
        });

        Assert.NotEmpty(avoidance);
        double worst = 0.0;
        foreach (var option in avoidance)
        {
            Assert.True(option.BurnEpoch > solution.Segments[1].BaseEpoch, "the option burns after the impulse");
            double tb = MinimumEpochGateTests.Tt(option.BurnEpoch);
            var (rb, vb) = afterImpulse.StateAt(tb);
            var afterBurn = new KeplerClosedForm(k.Mu, rb, vb + option.DeltaVInertial, tb);
            var post = option.PostManeuverEncounter;
            var (expected, _) = afterBurn.StateAt(MinimumEpochGateTests.Tt(post.EncounterState.Epoch));
            worst = System.Math.Max(worst, (post.ProtectedState.Position - expected).Magnitude());
        }

        _output.WriteLine($"MEASURE #569 gate EvaluateAvoidance: impulse - apogee {burnError:E2} s, {avoidance.Count} options, worst option at its TCA - closed form {worst:E2} m");
        Assert.True(worst <= AvoidanceAgainstClosedForm, $"{worst:E3} m");
    }

    /// <summary>
    /// SSB mode, GMAT's two <c>Propagate</c> commands inside the library: a propagation gated at the minimum epoch, and a
    /// propagation to the minimum epoch followed by one from the state there, the gate at its base. The burns agree within
    /// an integration noise (the closed form omits the Sun: the rule only). Face A (a perigee raise gated where r·v &lt; 0)
    /// and face B (an apogee raise gated 3 s before the perigee).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SsbMode_TheBurnOfTwoPropagateCommands(bool perigeeRaise)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var sun = new CelestialBody(Stars.Sun, Frame.ICRF, S);
        var orbit = MinimumEpochGateTests.Orbit(earth, System.Math.PI).ToStateVector();
        var k = new KeplerClosedForm(earth.GM, orbit.Position, orbit.Velocity, 0.0);
        var fromSsb = earth.GetGeometricStateRelativeTo(S, Barycenters.SOLAR_SYSTEM_BARYCENTER);
        var start = new StateVector(fromSsb.Position + orbit.Position, fromSsb.Velocity + orbit.Velocity, Barycenters.SOLAR_SYSTEM_BARYCENTER, S, Frame.ICRF);
        var gate = MinimumEpochGateTests.GateAtTt(perigeeRaise ? 1000.0 : k.T / 2.0 - 3.0);
        var end = S.AddHours(4.0);
        const double tolerance = AttitudeRecordTests.SsbRk78Tolerance;

        Spacecraft Build(int id, StateVector initial, bool withManeuver)
        {
            var spacecraft = new Spacecraft(id, $"GE{-id}", 1000.0, 3000.0, new Clock($"GE{-id}_CLK", 65536), initial);
            var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
            spacecraft.AddFuelTank(tank);
            var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
            spacecraft.AddEngine(engine);
            if (withManeuver)
            {
                spacecraft.SetStandbyManeuver(perigeeRaise
                    ? new PerigeeHeightManeuver(earth, gate, TimeSpan.Zero, 6878137.0, engine)
                    : new ApogeeHeightManeuver(earth, gate, TimeSpan.Zero, 8378137.0, engine));
            }

            return spacecraft;
        }

        var one = Build(perigeeRaise ? -7851 : -7861, start, true).Propagate(new Window(S, end), [earth, sun], new RK78Integrator(tolerance, tolerance), false, false,
            TimeSpan.FromSeconds(10.0));
        var toGate = Build(perigeeRaise ? -7852 : -7862, start, false).Propagate(new Window(S, gate), [earth, sun], new RK78Integrator(tolerance, tolerance), false, false,
            TimeSpan.FromSeconds(10.0));
        var (position, velocity) = toGate.InterpolateAt(gate);
        var two = Build(perigeeRaise ? -7853 : -7863, new StateVector(position, velocity, toGate.Observer, gate, Frame.ICRF), true)
            .Propagate(new Window(gate, end), [earth, sun], new RK78Integrator(tolerance, tolerance), false, false, TimeSpan.FromSeconds(10.0));

        Assert.Equal(2, one.Segments.Count);
        Assert.Equal(2, two.Segments.Count);
        double difference = (one.Segments[1].BaseEpoch - two.Segments[1].BaseEpoch).TotalSeconds;
        double expected = perigeeRaise ? k.NextApsis(1000.0, true) : k.T / 2.0;
        _output.WriteLine($"MEASURE #569 gate SSB two Propagate {(perigeeRaise ? "perigee raise, gate S+1000" : "apogee raise, gate 3 s before the perigee")}: " +
                          $"one - two {difference:E2} s, one - Keplerian {MinimumEpochGateTests.Tt(one.Segments[1].BaseEpoch) - expected:E2} s");
        Assert.True(System.Math.Abs(difference) <= TwoPropagateSsb, $"{difference:E3} s");
    }
}
