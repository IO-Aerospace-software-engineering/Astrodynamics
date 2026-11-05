// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Events;
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

/// <summary>
/// The edges of the attitude timeline (#539, #547, #570; design of #554): the two clips of the impulse's attitude (choice
/// 6), <c>ManeuverEventDetector.HandleEvent</c> executing the impulse only (choice 10), no sentinel (choice 7), no
/// activation past the end (choice 8), the bookkeeping of the executed maneuvers (#570), an impulse whose attitude is
/// none, a gate the timeline puts at S where g is exactly 0 (R5 of the plan), and the CK the library writes on these
/// chains.
/// </summary>
/// <remarks>
/// <para>
/// The case and the references are those of <see cref="AttitudeTimelineTests"/>: the rules of <see cref="TimelineRules"/>
/// and the closed forms of the laws on the solution, the burns against <see cref="KeplerClosedForm"/>. The CK: written by
/// the library with its clock and frame kernels, read by <c>pxform</c> through a kernel-backed spacecraft, against the
/// managed attitude within <see cref="AttitudeRecordTests.ManagedAgainstCk"/> off the leads (the 1 µs before each discrete
/// change and the rounding of its encoded SCLK, [change − 1.5 µs, change + 0.3 µs]).
/// </para>
/// <para>
/// No limit of its own: the existing ones, and <see cref="TimelineRules.RuleBound"/> (PROVISIONAL).
/// </para>
/// </remarks>
public class AttitudeTimelineEdgeTests
{
    private static readonly Time S = MinimumEpochGateTests.S;
    private readonly ITestOutputHelper _output;

    public AttitudeTimelineEdgeTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    // Four digits: the frame of a spacecraft is its NAIF identifier times 1 000, which the CK writer needs in an int
    private static int _nextId = -9300;
    private static int NextId() => System.Threading.Interlocked.Decrement(ref _nextId);

    private AttitudeTimelineTests.Measures Verified(string key, AttitudeTimelineTests.TimelineRun run)
    {
        var m = AttitudeTimelineTests.Verify(run);
        _output.WriteLine($"MEASURE #539 timeline edge {key.Replace('|', ' ')}: windows - rules {m.Windows:E2} s, gates - rules {m.Gates:E2} s, " +
                          $"law at entries {m.AtEntries:E2} rad, between {m.Between:E2} rad, discrete {m.Discrete:E2} rad ({m.Checks} reads); " +
                          $"burns (s) [{string.Join(", ", run.Burns.Select(b => run.Seconds(b).ToString("F7")))}], - closed form [{string.Join(", ", m.BurnErrors.Select(x => x.ToString("E2")))}]; " +
                          $"activations [{string.Join(", ", m.Expected.Activations.Select(a => $"{a.Kind} {run.Seconds(a.Epoch):F7}"))}]");
        return m;
    }

    // The epochs of the record strictly increase, and the activations of the rules never decrease
    private static void AssertMonotone(AttitudeTimelineTests.TimelineRun run, AttitudeTimelineTests.Measures m)
    {
        var record = run.Spacecraft.Frame.GetStateOrientationsToICRF().ToArray();
        Assert.True(record.Zip(record.Skip(1), (x, y) => y.Epoch > x.Epoch).All(x => x), "the record is not monotone");
        var activations = m.Expected.Activations;
        Assert.True(activations.Zip(activations.Skip(1), (x, y) => y.Epoch >= x.Epoch).All(x => x), "the activations decrease");
        Assert.True(record[^1].Epoch <= run.Window.EndDate, "the record goes past the window");
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The two clips of choice 6
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The impulse's attitude clipped to S: the apogee raise at the perigee 2 s after S, whose thrust window opens 0.666 dt
    /// (10.2 s) before, before S; with and without a leading nadir held 0 s, which the impulse's attitude replaces at S
    /// (the same epoch, later in the chain). The windows of the impulse stay [tc − 0.666 dt, …]: only its attitude is
    /// clipped. The CK is written and is the managed attitude.
    /// </summary>
    [Theory]
    [InlineData("ClipS|RK|Propagate|ICRF|10")]
    [InlineData("ClipS|VV1|Propagate|ICRF|10")]
    [InlineData("ClipSLaw|RK|Propagate|ICRF|10")]
    [InlineData("ClipSLaw|VV1|Propagate|ICRF|10")]
    public void ImpulseAttitude_ClippedToTheStart(string key)
    {
        var run = AttitudeTimelineTests.TimelineRun.Run(NextId(), key);
        var m = Verified(key, run);
        var impulse = run.Chain.OfType<ImpulseManeuver>().Single();
        Assert.True(impulse.ThrustWindow!.Value.StartDate < S, "the thrust window opens after S");
        var discrete = m.Expected.Activations.Single(a => a.Kind == "discrete");
        Assert.Equal(S, discrete.Epoch);
        AssertMonotone(run, m);
        AttitudeTimelineTests.AssertMeasures(run, m);
        AssertCk(run, m);
    }

    /// <summary>
    /// The impulse's attitude clipped to the previous activation (#577 choice 12): a perigee raise at the apogee after
    /// S + 3 500 held until 5 s before the next perigee, then an apogee raise there, whose thrust window opens 10.7 s before
    /// its burn, before the end of the first one's window; with and without a leading nadir, whose resumption at that end
    /// the impulse's attitude replaces (a law of zero length, the same epoch, later in the chain).
    /// </summary>
    [Theory]
    [InlineData("ClipLast|RK|Propagate|ICRF|10")]
    [InlineData("ClipLast|VV1|Propagate|ICRF|10")]
    [InlineData("ClipLastLaw|RK|Propagate|ICRF|10")]
    [InlineData("ClipLastLaw|VV1|Propagate|ICRF|10")]
    public void ImpulseAttitude_ClippedToThePreviousActivation(string key)
    {
        var run = AttitudeTimelineTests.TimelineRun.Run(NextId(), key);
        var m = Verified(key, run);
        var impulses = run.Chain.OfType<ImpulseManeuver>().ToArray();
        Assert.Equal(2, run.Burns.Count);
        Assert.True(impulses[1].ThrustWindow!.Value.StartDate < impulses[0].ManeuverWindow!.Value.EndDate, "no clip: the second thrust window opens after the first window");
        var second = m.Expected.Activations.Last(a => a.Kind == "discrete");
        Assert.True(TimelineRules.SecondsBetween(impulses[0].ManeuverWindow.Value.EndDate, second.Epoch, true) <= TimelineRules.RuleBound);
        if (key.StartsWith("ClipLastLaw"))
        {
            // The nadir resumed at the end of the first window, replaced by the second impulse's attitude at the same epoch
            Assert.Contains(m.Expected.Activations, a => a.Kind == "resumed" && a.Epoch == second.Epoch);
        }

        AssertMonotone(run, m);
        AttitudeTimelineTests.AssertMeasures(run, m);
        AssertCk(run, m);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Choice 10: HandleEvent executes the impulse only
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The public <c>ManeuverEventDetector.HandleEvent</c> on an impulse → attitude → impulse chain: it executes the impulse
    /// (its ΔV added to the state, its windows set, the fuel burned, the attitude the front along ΔV) and nothing else: the
    /// attitude chained after it is neither executed nor recorded, and no next detector is returned (#539, choice 10).
    /// </summary>
    [Fact]
    public void HandleEvent_ExecutesTheImpulseOnly()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var (spacecraft, chain, _, _) = AttitudeTimelineTests.Build(NextId(), "M0", MinimumEpochGateTests.Orbit(earth, 0.0), earth);
        var impulse = (ImpulseManeuver)chain[0];
        var detector = new ManeuverEventDetector(impulse);
        var state = spacecraft.InitialOrbitalParameters.ToStateVector();
        var velocity = state.Velocity;
        int records = spacecraft.Frame.GetStateOrientationsToICRF().Count();

        var result = detector.HandleEvent(state, spacecraft);

        Assert.Equal(EventAction.StopAndRestart, result.Action);
        Assert.Null(result.NextDetector);
        Assert.Equal(velocity + impulse.DeltaV, result.ModifiedState.Velocity);
        Assert.True(impulse.DeltaV.Magnitude() > 100.0);
        Assert.NotNull(result.Orientation);
        Assert.True(AttitudeRecordTests.RotationAngle(AttitudeRecordTests.MinimalRotation(Spacecraft.Front, impulse.DeltaV), v => v.Rotate(result.Orientation.Rotation)) < 1e-15);
        Assert.True(impulse.ThrustWindow.HasValue && impulse.ManeuverWindow.HasValue);
        Assert.True(impulse.FuelBurned > 0.0);
        Assert.Null(chain[1].ManeuverWindow);
        Assert.Null(chain[1].ThrustWindow);
        Assert.Null(chain[2].ManeuverWindow);
        Assert.Equal(new[] { impulse }, spacecraft.ExecutedManeuvers.ToArray());
        Assert.Same(chain[1], spacecraft.StandbyManeuver);
        Assert.Equal(records, spacecraft.Frame.GetStateOrientationsToICRF().Count());
    }

    /// <summary>When the preconditions of the impulse fail, <c>HandleEvent</c> continues with the same detector and changes
    /// nothing, as before #539.</summary>
    [Fact]
    public void HandleEvent_PreconditionsFail_Continues()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        var spacecraft = new Spacecraft(NextId(), "TLEDGEPRE", 1000.0, 3000.0, new Clock("TLEDGEPRE_CLK", 65536), MinimumEpochGateTests.Orbit(earth, 0.0));
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        var impulse = new NeverReadyImpulse(earth, S, engine);
        impulse.SetNextManeuver(new NadirAttitude(earth, S, TimeSpan.Zero, engine));
        spacecraft.SetStandbyManeuver(impulse);
        var detector = new ManeuverEventDetector(impulse);
        var state = spacecraft.InitialOrbitalParameters.ToStateVector();

        var result = detector.HandleEvent(state, spacecraft);

        Assert.Equal(EventAction.Continue, result.Action);
        Assert.Same(detector, result.NextDetector);
        Assert.Same(state, result.ModifiedState);
        Assert.Null(result.Orientation);
        Assert.Null(impulse.ThrustWindow);
        Assert.Empty(spacecraft.ExecutedManeuvers);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Choice 7: no sentinel; choice 8: no activation past the end
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// D: a leading nadir that is exactly the identity at S (−r̂ along the front, +Y) is recorded like any rotation (no
    /// sentinel, choice 7): the record holds it at S and the nadir after; the CK the library writes is read by
    /// <c>pxform</c> from S on (before #539: not recorded, the first entry at S + 60 s, <c>pxform</c> failed before it).
    /// </summary>
    [Theory]
    [InlineData("D|VV1|Propagate|ICRF|60")]
    [InlineData("D|RK|Propagate|ICRF|60")]
    public void LeadingAttitude_TheIdentityAtTheStart_IsRecorded(string key)
    {
        var run = AttitudeTimelineTests.TimelineRun.Run(NextId(), key);
        var m = Verified(key, run);
        var first = run.Spacecraft.Frame.GetStateOrientationsToICRF().First();
        Assert.Equal(S, first.Epoch);
        Assert.True(AttitudeRecordTests.RotationAngle(v => v, v => v.Rotate(first.Rotation)) < 1e-15);
        AttitudeTimelineTests.AssertMeasures(run, m);
        var reads = AssertCk(run, m, Enumerable.Range(0, 60).Select(i => S.AddSeconds(i)).ToArray());
        Assert.Equal(60, reads);
    }

    /// <summary>
    /// K (choice 8): impulse → LVLH (5 000 s) → prograde whose activation falls after the end of a 2 h window. The prograde
    /// is not executed (null windows, not in <c>ExecutedManeuvers</c>, still the standby maneuver), the record ends at the
    /// end of the window, and the LVLH law is in force to it. Also a leading hold past the end (the impulse never fires),
    /// a leading minimum epoch past the end (nothing is executed), and a resumed law past the end (not activated).
    /// </summary>
    [Theory]
    [InlineData("K|RK|Propagate|ICRF|10")]
    [InlineData("K|VV1|Propagate|ICRF|10")]
    [InlineData("LeadPast|RK|Propagate|ICRF|10")]
    [InlineData("LeadPast|VV1|Propagate|ICRF|10")]
    [InlineData("MinPast|RK|Propagate|ICRF|10")]
    [InlineData("MinPast|VV1|Propagate|ICRF|10")]
    [InlineData("Npast|RK|Propagate|ICRF|10")]
    [InlineData("Npast|VV1|Propagate|ICRF|10")]
    public void ActivationPastTheEnd_IsNotExecuted(string key)
    {
        var run = AttitudeTimelineTests.TimelineRun.Run(NextId(), key);
        var m = Verified(key, run);
        AttitudeTimelineTests.AssertMeasures(run, m);
        AssertMonotone(run, m);
        var notExecuted = run.Chain.Where(x => !m.Expected.Executed.Contains(x)).ToArray();
        switch (run.Letter)
        {
            case "K":
                Assert.Equal(new[] { run.Chain[2] }, notExecuted);
                Assert.Same(run.Chain[2], run.Spacecraft.StandbyManeuver);
                break;
            case "LeadPast":
                Assert.Empty(run.Burns);
                Assert.Equal(new[] { run.Chain[1] }, notExecuted);
                Assert.True(run.Chain[1].MinimumEpoch > run.Window.EndDate);
                break;
            case "MinPast":
                Assert.Empty(run.Burns);
                Assert.Empty(run.Spacecraft.ExecutedManeuvers);
                Assert.Single(run.Spacecraft.Frame.GetStateOrientationsToICRF().Where(x => x.Epoch < run.Window.EndDate));
                break;
            case "Npast":
                Assert.DoesNotContain(m.Expected.Activations, a => a.Kind == "resumed");
                break;
        }

        Assert.Equal(run.Window.EndDate, run.Spacecraft.Frame.GetStateOrientationsToICRF().Last().Epoch);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // #570: the executed maneuvers
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// #570: <c>ExecutedManeuvers</c> holds every maneuver the chain executed, leading, chained and trailing attitudes and
    /// impulses, once each, in the order of the chain (before #539 a leading attitude was missing, and the first impulse
    /// after it, B, C, E2, F3, L: indices {0, 2}); <c>ToOpm</c> writes the impulses, their ignition the opening of their
    /// thrust window and their mass the fuel of the rocket equation; the summary of a scenario merges their windows and sums
    /// their fuel. Against the chain and the rules, never a recorded value. Both integrators, <c>Spacecraft.Propagate</c> and
    /// <c>Scenario.SimulateAsync</c>.
    /// </summary>
    [Theory]
    [InlineData("E2|RK|Propagate|ICRF|10")]
    [InlineData("E2|VV1|Scenario|ICRF|10")]
    [InlineData("B|RK|Scenario|ICRF|10")]
    [InlineData("B|VV1|Propagate|ICRF|10")]
    [InlineData("C|RK|Propagate|ICRF|10")]
    [InlineData("NPI|RK|Propagate|ICRF|10")]
    [InlineData("NPI|VV1|Scenario|ICRF|10")]
    [InlineData("L|RK|Scenario|ICRF|10")]
    [InlineData("L|VV1|Propagate|ICRF|10")]
    public void ExecutedManeuvers_HoldTheWholeChain(string key)
    {
        var run = AttitudeTimelineTests.TimelineRun.Run(NextId(), key);
        var m = Verified(key, run);
        AttitudeTimelineTests.AssertMeasures(run, m);
        var executed = run.Spacecraft.ExecutedManeuvers.ToArray();
        Assert.Equal(run.Chain.Where(x => m.Expected.Executed.Contains(x)).ToArray(), executed);
        Assert.Equal(executed.Length, executed.Distinct().Count());
        Assert.Contains(run.Chain[0], executed);

        // ToOpm: the impulses
        var opm = run.Spacecraft.ToOpm(includeKeplerianElements: false, includeManeuvers: true).Data.Maneuvers.ToArray();
        var impulses = executed.OfType<ImpulseManeuver>().ToArray();
        Assert.Equal(impulses.Length, opm.Length);
        double mass = 2000.0;
        for (int k = 0; k < impulses.Length; k++)
        {
            var (thrust, _) = m.Expected.Windows[impulses[k]];
            double fuel = TimelineRules.FuelBurned(mass, impulses[k].DeltaV.Magnitude(), 300.0);
            mass -= fuel;
            Assert.True(System.Math.Abs(fuel + opm[k].ManDeltaMass) <= 1e-9, $"impulse {k}: delta mass {opm[k].ManDeltaMass} against {fuel}");
            if (TimelineRules.Checked)
            {
                Assert.True(System.Math.Abs((thrust.StartDate.ToTDB().DateTime - opm[k].ManEpochIgnition).TotalSeconds) <= TimelineRules.RuleBound, $"impulse {k}: ignition");
            }
        }

        // The summary: the merged windows and the fuel of the rules
        var summary = run.Spacecraft.GetSummary();
        var windows = m.Expected.Executed.Select(x => m.Expected.Windows[x].Maneuver).ToArray();
        var start = windows.Min(x => x.StartDate);
        var end = windows.Max(x => x.EndDate);
        _output.WriteLine($"MEASURE #570 {key.Replace('|', ' ')}: executed [{string.Join(",", executed.Select(x => Array.IndexOf(run.Chain, x)))}], " +
                          $"summary window - rules [{(summary.ManeuverWindow!.Value.StartDate - start).TotalSeconds:E2}, {(summary.ManeuverWindow.Value.EndDate - end).TotalSeconds:E2}] s, " +
                          $"fuel - rocket equation {summary.FuelConsumption - m.Expected.Fuel:E2} kg");
        Assert.True(System.Math.Abs(summary.FuelConsumption - m.Expected.Fuel) <= 1e-9);
        if (TimelineRules.Checked)
        {
            Assert.True(System.Math.Abs((summary.ManeuverWindow.Value.StartDate - start).TotalSeconds) <= TimelineRules.RuleBound);
            Assert.True(System.Math.Abs((summary.ManeuverWindow.Value.EndDate - end).TotalSeconds) <= TimelineRules.RuleBound);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // An impulse without an attitude; a gate at S where g is zero
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A user impulse whose execution returns no attitude, then an LVLH: the timeline records no attitude for it, the
    /// identity holds to the end of its maneuver window, where the LVLH activates.
    /// </summary>
    [Theory]
    [InlineData("RK")]
    [InlineData("VV1")]
    public void ImpulseWithoutAttitude_TheEarlierAttitudeHolds(string integrator)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        int id = NextId();
        var spacecraft = new Spacecraft(id, $"TL{-id}", 1000.0, 3000.0, new Clock($"TL{-id}_CLK", 65536), MinimumEpochGateTests.Orbit(earth, System.Math.PI));
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        var impulse = new NoAttitudeImpulse(earth, engine);
        var lvlh = TriadAttitude.CreateLVLH(earth, S, TimeSpan.Zero, engine);
        impulse.SetNextManeuver(lvlh);
        spacecraft.SetStandbyManeuver(impulse);
        Integrator propagator = integrator == "RK" ? new RK78Integrator(1e-12, 1e-12) : new VVIntegrator(TimeSpan.FromSeconds(1.0));
        var solution = spacecraft.Propagate(new Window(S, S.AddHours(2.0)), [earth], propagator, false, false, TimeSpan.FromSeconds(10.0));
        var tc = solution.Segments[1].BaseEpoch;
        double dt = TimelineRules.BurnDuration(2000.0, impulse.DeltaV.Magnitude(), 300.0, 10.0);
        var end = TimelineRules.ManeuverEnd(tc, dt, 0.0, true);
        double rule = System.Math.Abs((lvlh.ManeuverWindow!.Value.StartDate - end).TotalSeconds);
        double identity = 0.0;
        for (var t = S; t < end.AddSeconds(-1e-3); t = t.AddSeconds(1.0))
            identity = System.Math.Max(identity, AttitudeRecordTests.RotationAngle(v => v, v => v.Rotate(spacecraft.Frame.GetStateOrientationToICRF(t).Rotation)));
        var after = spacecraft.Frame.GetStateOrientationToICRF(end.AddSeconds(1e-3)).Rotation;
        _output.WriteLine($"MEASURE #539 impulse without an attitude {integrator}: LVLH start - rule {rule:E2} s, identity before it {identity:E2} rad, " +
                          $"at its start + 1 ms {AttitudeRecordTests.RotationAngle(v => v, v => v.Rotate(after)):E2} rad from the identity");
        Assert.Equal(0.0, identity);
        Assert.True(AttitudeRecordTests.RotationAngle(v => v, v => v.Rotate(after)) > 0.1);
        if (TimelineRules.Checked) Assert.True(rule <= TimelineRules.RuleBound);
    }

    /// <summary>
    /// R5: a leading nadir held 0 s puts the gate of the impulse after it at S (choice 4), where the orbit given by Keplerian
    /// elements at its apogee has g = r·v exactly 0; by #577's rule of a zero at a gate equal to the base (choice 6) the
    /// perigee raise fires there in RK7(8), at the end of the first step in Velocity-Verlet. Without the leading attitude the
    /// impulse has no gate (its minimum epoch is before the base) and the crossing at S is not taken: it fires one orbit
    /// later (#580, out of scope). An intended move of the trajectory, stated in the notes.
    /// </summary>
    [Theory]
    [InlineData("RK")]
    [InlineData("VV1")]
    public void LeadingHoldZero_GateAtTheStartWhereGIsZero_FiresThere(string integrator)
    {
        var withLead = AttitudeTimelineTests.TimelineRun.Run(NextId(), $"R5|{integrator}|Propagate|ICRF|10");
        var alone = AttitudeTimelineTests.TimelineRun.Run(NextId(), $"R5alone|{integrator}|Propagate|ICRF|10");
        var sv = withLead.InitialIcrf.ToStateVector();
        Assert.Equal(0.0, sv.Position * sv.Velocity);
        double burn = withLead.Seconds(withLead.Burns[0]);
        double later = alone.Seconds(alone.Burns[0]);
        var orbit = new KeplerClosedForm(withLead.Center.GM, sv.Position, sv.Velocity, 0.0);
        Assert.Equal(S, withLead.Chain[1].MinimumEpoch);
        _output.WriteLine($"MEASURE #539 R5 {integrator}: the burn after a leading nadir held 0 s at S + {burn:R} s, without it at S + {later:R} s (period {orbit.T:F3} s)");
        if (integrator == "RK")
        {
            Assert.Equal(S, withLead.Burns[0]);
        }
        else
        {
            Assert.InRange(burn, 0.0, 1.0 + 1e-6);
        }

        Assert.True(later > orbit.T / 2.0, "without the leading attitude the crossing at S is taken (#580 fixed?)");
        var m = AttitudeTimelineTests.Verify(withLead);
        if (TimelineRules.Checked) Assert.True(m.Windows <= TimelineRules.RuleBound);
        Assert.True(m.Discrete <= AttitudeRecordTests.DiscreteLaw);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The CK
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>Writes the clock, frame and CK kernels of the run, loads them, reads the attitude by <c>pxform</c> through a
    /// kernel-backed spacecraft every second and about each activation, off the leads, against the managed attitude.</summary>
    /// <returns>The number of reads.</returns>
    private int AssertCk(AttitudeTimelineTests.TimelineRun run, AttitudeTimelineTests.Measures m, Time[] epochs = null)
    {
        var sc = run.Spacecraft;
        string directory = $"Data/User/AttitudeTimeline539/{sc.Name}";
        Directory.CreateDirectory(directory);
        string stem = sc.Name.ToLowerInvariant();
        // Deleted before the FileInfo is made: FileInfo.Exists, read by LoadKernels, is cached at its first read
        FileInfo NewFile(string name)
        {
            var path = Path.Combine(directory, name);
            File.Delete(path);
            return new FileInfo(path);
        }

        var clock = NewFile(stem + ".tsc");
        var frame = NewFile(stem + ".tf");
        var ck = NewFile(stem + ".bc");
        sc.Clock.WriteAsync(clock).GetAwaiter().GetResult();
        ((SpacecraftFrame)sc.Frame).WriteAsync(frame).GetAwaiter().GetResult();
        SpiceAPI.Instance.LoadKernels(clock);
        SpiceAPI.Instance.LoadKernels(frame);
        try
        {
            sc.WriteOrientation(ck);
            SpiceAPI.Instance.LoadKernels(ck);
            try
            {
                var kernel = new Spacecraft(sc.NaifId, sc.Name, S, 1000.0, 3000.0, new Clock(sc.Name + "_CLK", 65536));
                // The changes after the start, each with its lead; an activation at the start has none
                var changes = m.Expected.Activations.Select(a => a.Epoch).Where(x => x > S).ToArray();
                var reads = epochs ?? Enumerable.Range(0, (int)(run.Window.EndDate - S).TotalSeconds).Select(i => S.AddSeconds(i))
                    .Concat(changes.SelectMany(c => new[] { c.AddSeconds(-1e-3), c.AddSeconds(2.5e-6), c.AddSeconds(1e-3) }))
                    .Where(t => t >= S && t <= run.Window.EndDate).ToArray();
                double worst = 0.0;
                int count = 0;
                foreach (var t in reads)
                {
                    if (changes.Any(c => (t - c).TotalSeconds >= -1.5e-6 && (t - c).TotalSeconds <= 0.3e-6 + TimelineRules.RuleBound)) continue;
                    var managed = sc.Frame.GetStateOrientationToICRF(t).Rotation;
                    var read = kernel.Frame.GetStateOrientationToICRF(t).Rotation;
                    worst = System.Math.Max(worst, AttitudeRecordTests.RotationAngle(managed, read));
                    count++;
                }

                _output.WriteLine($"MEASURE #539 timeline edge {run.Key.Replace('|', ' ')} CK: managed - pxform {worst:E2} rad over {count} reads");
                Assert.True(worst <= AttitudeRecordTests.ManagedAgainstCk, $"managed - CK {worst:E3} rad");
                return count;
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(ck);
            }
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(frame);
            SpiceAPI.Instance.UnloadKernels(clock);
        }
    }
}

/// <summary>An impulse whose preconditions never hold.</summary>
internal sealed class NeverReadyImpulse : ImpulseManeuver
{
    internal NeverReadyImpulse(CelestialItem center, Time minimumEpoch, Engine engine) : base(center, minimumEpoch, TimeSpan.Zero, engine)
    {
    }

    public override double ComputeEventValue(StateVector localState) => localState.Position * localState.Velocity;

    public override CrossingDirection EventCrossingDirection => CrossingDirection.NegativeToPositive;

    public override bool CheckPreconditions(StateVector state) => false;

    protected override Vector3 Execute(StateVector vector) => vector.Velocity.Normalize() * 10.0;
}

/// <summary>A user impulse at the perigee, 100 m/s along the velocity, whose execution returns no attitude.</summary>
internal sealed class NoAttitudeImpulse : ImpulseManeuver
{
    internal NoAttitudeImpulse(CelestialItem center, Engine engine) : base(center, new Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, engine)
    {
    }

    public override double ComputeEventValue(StateVector localState) => localState.Position * localState.Velocity;

    public override CrossingDirection EventCrossingDirection => CrossingDirection.NegativeToPositive;

    protected override Vector3 Execute(StateVector vector) => vector.Velocity.Normalize() * 100.0;

    public override (StateVector sv, StateOrientation so) TryExecute(StateVector stateVector)
    {
        var (sv, _) = base.TryExecute(stateVector);
        return (sv, null);
    }
}
