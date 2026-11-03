using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Cosmographia;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Body;

// Inside IO.Astrodynamics.Tests the namespaces Frame and Time shadow the library types
using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The edges of the stored states across an impulse (#540, §7.2 of the design of #554): an impulse at the end of the
/// propagation, the covariance mode with an execution error, a filled arc, a Velocity-Verlet output at the impulse,
/// <c>AddPropagatedStates</c>, the Cosmographia export, a conjunction with a maneuvering <see cref="ILocalizable"/>, and
/// the managed occultation and field-of-view searches near a burn against <c>gfoclt</c> and <c>gftfov</c>; the managed
/// writer against CSPICE's <c>spkw09</c> and the layout of its file by <c>dafus</c>.
/// </summary>
/// <remarks>
/// Limits: twice the worst over Linux x64, Windows and macOS arm64 (CI run 41651463194, 2026-11-03, MEASURE lines), rounded
/// up at the second significant digit; each summary states the worsts (L / W / M, or the same on the three platforms).
/// Every test of the class runs in every CI job; the comparisons with CSPICE's own DAF routines (<c>dafus</c>) run on Linux
/// only.
/// </remarks>
public class PropagatedArcsEdgeTests
{
    /// <summary>The covariance of the state before an impulse against <see cref="PropagationSolution.GetCovariance"/> 1 µs
    /// before it, relative to the largest element: the propagation of P over that microsecond. Worst 1.51e-9, the same on
    /// the three platforms (Apogee, 10 s; CI run 41651463194).</summary>
    internal const double CovarianceBefore = 3.1e-9;

    /// <summary>The covariance after an impulse less the covariance before it, velocity block, against the covariance of the
    /// execution error written in the test from the four terms of Gates on the velocity jump of the stored states, relative
    /// to the largest element of the latter: the rounding of the jump, a difference of two velocities of 7.8 km/s, in the
    /// σ_s² v vᵀ and σ_p² v vᵀ terms. Worst 6.13e-15, the same on the three platforms (Apogee from ECLIPJ2000, 2.31e-14 against
    /// 3.77 m²/s²; CI run 41651463194).</summary>
    internal const double ExecutionErrorJump = 1.3e-14;

    /// <summary>The covariance of a stored state after an impulse, or of a filled one, against the solution's rotated to ICRF
    /// by <c>pxform</c> in the test, relative to the largest element, from ECLIPJ2000: the rounding of the two rotations.
    /// Worst 4.28e-16, the same on the three platforms (Apogee, 10 s; CI run 41651463194).</summary>
    internal const double RotatedCovariance = 8.6e-16;

    /// <summary>The managed occultation and field-of-view edges near a burn against <c>gfoclt</c> and <c>gftfov</c> on the
    /// kernels the library writes, s. Worst 4.0e-7 s, four ticks of 100 ns, the same on the three platforms (the field of view
    /// and the occultation in RK7(8); CI run 41651463194).</summary>
    internal const double EdgesAgainstSpice = 8.0e-7;

    /// <summary>The TCA of a conjunction read through the stored states against the same analysis on the RK7(8) solutions:
    /// measured 0 on the three platforms (CI run 41651463194), below the resolution of a <see cref="Time"/>, one tick of 100 ns, which is
    /// the limit.</summary>
    internal const double ConjunctionTca = 1.0e-7;

    /// <summary>The miss distance of the same, m. Worst 6.18e-10 / 6.18e-10 / 3.31e-10 m (CI run 41651463194).</summary>
    internal const double ConjunctionMiss = 1.3e-9;

    /// <summary>The read at a Velocity-Verlet output within 2 µs of an impulse, which is not stored, against that output,
    /// m: the Lagrange of the later arc from the state after the impulse. Worst 7.22e-7 m, the same on the three platforms (the
    /// chain of four, 60 s; CI run 41651463194).</summary>
    internal const double OutputAtTheImpulse = 1.5e-6;

    /// <summary>The SPK at the end of a propagation that ends at, or just after, an impulse, against the solution there, m and
    /// m/s, and at the start and the end of a window with an impulse within 2 µs of either. Worst 9.31e-10 m, the same on the
    /// three platforms, and 1.29e-12 / 1.29e-12 / 9.09e-13 m/s (the impulse at the end, RK7(8), 10 s; CI run 41651463194).</summary>
    internal const double SpkAtTheEnd = 1.9e-9;

    internal const double SpkVelocityAtTheEnd = 2.6e-12;

    /// <summary>The SPK of the Cosmographia export at tc - 1 µs, tc and tc + 1 µs against the solution, m and m/s. Worst
    /// 5.88e-4 m and 6.32e-7 m/s, the same on the three platforms (CI run 41651463194).</summary>
    internal const double CosmographiaPosition = 1.2e-3;

    internal const double CosmographiaVelocity = 1.3e-6;

    private readonly ITestOutputHelper _output;

    public PropagatedArcsEdgeTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static readonly Time Start = new(2024, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);

    private static StateVector Leo(CelestialItem earth) =>
        new(new Vector3(-7378137.0, 0.0, 0.0), new Vector3(0.0, -5509.9, -4623.4), earth, Start, Frame.ICRF);

    private static (Spacecraft, ApogeeHeightManeuver) Apogee(int naifId, StateVector initial, CelestialBody earth, ManeuverExecutionError error = null)
    {
        var spacecraft = new Spacecraft(naifId, $"PE{-naifId}", 1000.0, 3000.0, new Clock($"PE{-naifId}_CLK", 65536), initial);
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        var apogee = new ApogeeHeightManeuver(earth, Start, TimeSpan.Zero, 8378137.0, engine) { ExecutionError = error };
        spacecraft.SetStandbyManeuver(apogee);
        return (spacecraft, apogee);
    }

    private static FileInfo NewSpk(string name)
    {
        Directory.CreateDirectory("Data/User/PropagatedArcs540");
        return new FileInfo(Path.Combine("Data/User/PropagatedArcs540", name + ".bsp"));
    }

    /// <summary>
    /// An impulse at the end of the propagation (review probe E3): Velocity-Verlet locates the perigee crossing at the end of
    /// its last step, the impulse executes (§9 item 7 of #554, its own issue) and the solution ends with the state before it,
    /// one segment. An impulse that no segment carries opens no arc, its state before it closes the last one: the reads and
    /// the SPK end as the solution does, with the state before the impulse, at the end of the propagation, past the last
    /// output when the output step does not divide the window (on the prototype, a one-state arc: the read at the end gave
    /// the state after, 8 063 m/s against 7 829 m/s, and WriteEphemeris threw). RK7(8) over 2 970 s locates the impulse
    /// 6.8 s before the end: a short arc, filled.
    /// </summary>
    [Theory]
    [InlineData("VVd", 10.0, 2964.0, -5501)]
    [InlineData("VVd", 4.0, 2964.0, -5502)]
    [InlineData("RK78", 10.0, 2970.0, -5503)]
    public void ImpulseAtTheEnd_NoArcOfZeroLength(string integrator, double step, double length, int naifId)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, Start);
        var (sc, apogee) = Apogee(naifId, Leo(earth), earth);
        var window = new Window(Start, Start.AddSeconds(length));
        var outputStep = TimeSpan.FromSeconds(step);
        var solution = sc.Propagate(window, [earth], integrator == "VVd" ? new VVIntegrator(outputStep) : new RK78Integrator(1e-12, 1e-12), false, false, outputStep);
        var arcs = sc.PropagatedArcs;
        var spk = NewSpk(sc.Name.ToLowerInvariant());
        sc.WriteEphemeris(spk);
        SpiceAPI.Instance.LoadKernels(spk);
        try
        {
            var kernel = new Spacecraft(sc.NaifId, sc.Name, Start, 1000.0, 3000.0, new Clock(sc.Name + "K_CLK", 65536));
            var end = window.EndDate;
            var (position, velocity) = solution.InterpolateAt(end);
            var managed = sc.GetEphemeris(end, earth, Frame.ICRF, Aberration.None).ToStateVector();
            var read = kernel.GetGeometricStateRelativeTo(end, earth);
            double managedVelocity = (managed.Velocity - velocity).Magnitude(), spkVelocity = (read.Velocity - velocity).Magnitude();
            double managedPosition = (managed.Position - position).Magnitude(), spkPosition = (read.Position - position).Magnitude();
            _output.WriteLine($"MEASURE #540 impulse at the end {integrator} {step} s over {length} s: {solution.Segments.Count} segments, executed {apogee.ThrustWindow.HasValue} " +
                              $"(dV {apogee.DeltaV.Magnitude():F1} m/s), arcs [{string.Join(",", arcs.Select(a => a.Length))}], at the end managed {managedPosition:E2} m {managedVelocity:E2} m/s, " +
                              $"SPK {spkPosition:E2} m {spkVelocity:E2} m/s, |v| solution {velocity.Magnitude():F2} m/s");
            Assert.True(apogee.ThrustWindow.HasValue);
            if (solution.Segments.Count == 1)
            {
                // The impulse at the end: one arc, ending with the state before the impulse
                Assert.Single(arcs);
                Assert.True(arcs[0].Length >= 2);
                Assert.Equal(0.0, managedVelocity);
                Assert.Equal(0.0, managedPosition);
            }
            else
            {
                // Just before the end: the later arc, filled
                Assert.Equal(2, arcs.Count);
                Assert.Equal(PropagatedEphemeris.FilledArcStates, arcs[1].Length);
                // The last state of the filled arc is the last output, the solution at the end
                Assert.Equal(0.0, managedVelocity);
                Assert.Equal(0.0, managedPosition);
            }

            Assert.True(spkVelocity <= SpkVelocityAtTheEnd, $"SPK at the end {spkVelocity:E3} m/s");
            Assert.True(spkPosition <= SpkAtTheEnd, $"SPK at the end {spkPosition:E3} m");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spk);
            spk.Delete();
        }
    }

    /// <summary>
    /// An impulse within 2 µs of the start or of the end of the window, located by RK7(8) (a timed impulse of 20 m/s, review
    /// of PR 1 of #554, ARC-2): the solution then has a second segment shorter than 2 µs. The first and the last output are
    /// kept in their own arc with the impulse's state, unfilled: the managed reads and the SPK cover the window, the solution
    /// there to the bit managed and within the resolution of the SPK (before, the arc was dropped with its output: the read
    /// at the boundary off by the whole ΔV, <c>spkezr</c> SPICE(SPKINSUFFDATA)). One tick (1e-7 s) before the end too.
    /// </summary>
    [Theory]
    [InlineData(1.0e-6, -5591)]
    [InlineData(7200.0 - 1.0e-6, -5592)]
    [InlineData(7200.0 - 1.0e-7, -5593)]
    public void ImpulseWithin2MicrosecondsOfTheStartOrTheEnd_TheWindowStaysCovered(double target, int naifId)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, Start);
        var spacecraft = new Spacecraft(naifId, $"PE{-naifId}", 1000.0, 3000.0, new Clock($"PE{-naifId}_CLK", 65536), Leo(earth));
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        spacecraft.SetStandbyManeuver(new TimedImpulse(earth, Start, engine, Start.AddSeconds(target), 20.0));
        var window = new Window(Start, Start.AddHours(2.0));
        var solution = spacecraft.Propagate(window, [earth], new RK78Integrator(1e-12, 1e-12), false, false, TimeSpan.FromSeconds(10.0));
        Assert.Equal(2, solution.Segments.Count);
        var tc = solution.Segments[1].BaseEpoch;
        var arcs = spacecraft.PropagatedArcs;
        Assert.Equal(2, arcs.Count);
        // The arc of the boundary: the output there and the impulse's state, unfilled
        var boundary = target < 1.0 ? arcs[0] : arcs[1];
        Assert.Equal(2, boundary.Length);
        Assert.Equal(target < 1.0 ? window.StartDate : tc, boundary[0].Epoch);
        Assert.Equal(target < 1.0 ? tc : window.EndDate, boundary[1].Epoch);
        Assert.Equal(window, spacecraft.InterpolatedStatesWindow);

        var spk = NewSpk(spacecraft.Name.ToLowerInvariant());
        spacecraft.WriteEphemeris(spk);
        SpiceAPI.Instance.LoadKernels(spk);
        try
        {
            double managed = 0.0, managedVelocity = 0.0, spkPosition = 0.0, spkVelocity = 0.0;
            foreach (var t in new[] { window.StartDate, window.EndDate })
            {
                var (position, velocity) = solution.InterpolateAt(t);
                var read = spacecraft.GetEphemeris(t, earth, Frame.ICRF, Aberration.None).ToStateVector();
                managed = System.Math.Max(managed, (read.Position - position).Magnitude());
                managedVelocity = System.Math.Max(managedVelocity, (read.Velocity - velocity).Magnitude());
                // The windowed read, which throws on a SPICE error (the single-epoch read does not, spk-O1 of the review)
                var fromSpk = SpiceAPI.Instance.ReadEphemeris(new Window(t, t), 399, spacecraft.NaifId, Frame.ICRF, Aberration.None, TimeSpan.FromSeconds(1.0)).First().ToStateVector();
                spkPosition = System.Math.Max(spkPosition, (fromSpk.Position - position).Magnitude());
                spkVelocity = System.Math.Max(spkVelocity, (fromSpk.Velocity - velocity).Magnitude());
            }

            _output.WriteLine($"MEASURE #540 impulse at S + {(tc - Start).TotalSeconds:R} s (target {target:R}): arcs [{string.Join(",", arcs.Select(a => a.Length))}], " +
                              $"at the start and the end managed {managed:E2} m {managedVelocity:E2} m/s, SPK {spkPosition:E2} m {spkVelocity:E2} m/s");
            Assert.Equal(0.0, managed);
            Assert.Equal(0.0, managedVelocity);
            Assert.True(spkPosition <= SpkAtTheEnd, $"SPK {spkPosition:E3} m");
            Assert.True(spkVelocity <= SpkVelocityAtTheEnd, $"SPK {spkVelocity:E3} m/s");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spk);
            spk.Delete();
        }
    }

    /// <summary>
    /// Two impulses at one event (main's case M, review of #554, which PR 1 still produces: the timeline that fixes it is PR
    /// 2): an apogee-raising impulse, an LVLH attitude held 0 s, a second apogee-raising impulse that RK7(8) fires at the same
    /// event. The arc between them, of zero length, is not stored: the earlier arc ends with the state before both, the later
    /// one starts with the state after both, at one epoch, the jump the sum of the two delta-V; the managed reads within
    /// ±150 s against the solution, at tc the state after both; <c>spkezr</c> on the SPK, two segments, against them.
    /// </summary>
    [Fact]
    public void TwoImpulsesAtOneEvent_OneBoundary()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, Start);
        var spacecraft = new Spacecraft(-5594, "PE5594", 1000.0, 3000.0, new Clock("PE5594_CLK", 65536), Leo(earth));
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        var first = new ApogeeHeightManeuver(earth, Start, TimeSpan.Zero, 8378137.0, engine);
        var lvlh = TriadAttitude.CreateLVLH(earth, Start, TimeSpan.Zero, engine);
        var second = new ApogeeHeightManeuver(earth, Start, TimeSpan.Zero, 9378137.0, engine);
        first.SetNextManeuver(lvlh);
        lvlh.SetNextManeuver(second);
        spacecraft.SetStandbyManeuver(first);
        var solution = spacecraft.Propagate(new Window(Start, Start.AddHours(2.0)), [earth], new RK78Integrator(1e-12, 1e-12), false, false, TimeSpan.FromSeconds(10.0));
        var events = solution.Segments.Skip(1).Select(x => x.BaseEpoch).ToArray();
        Assert.Equal(2, events.Length);
        Assert.Equal(events[0], events[1]);
        Assert.True(first.ThrustWindow.HasValue && second.ThrustWindow.HasValue);
        var tc = events[0];
        var arcs = spacecraft.PropagatedArcs;
        Assert.Equal(2, arcs.Count);
        Assert.Equal(tc, arcs[0][^1].Epoch);
        Assert.Equal(tc, arcs[1][0].Epoch);
        double boundary = (arcs[1][0].Position - arcs[0][^1].Position).Magnitude();
        double jump = (arcs[1][0].Velocity - arcs[0][^1].Velocity - first.DeltaV - second.DeltaV).Magnitude();

        var spk = NewSpk("pe5594");
        spacecraft.WriteEphemeris(spk);
        SpiceAPI.Instance.LoadKernels(spk);
        try
        {
            var segments = OperatingSystem.IsLinux() ? CspiceDirect.ReadSegments(spk.FullName).Count : 2;
            var kernel = new Spacecraft(-5594, "PE5594", Start, 1000.0, 3000.0, new Clock("PE5594K_CLK", 65536));
            double near = 0.0, spkAgainstManaged = 0.0;
            for (int i = -600; i <= 600; i++)
            {
                var t = tc.AddSeconds(i * 0.25);
                var (position, _) = solution.InterpolateAt(t);
                var managed = spacecraft.GetEphemeris(t, earth, Frame.ICRF, Aberration.None).ToStateVector();
                near = System.Math.Max(near, (managed.Position - position).Magnitude());
                spkAgainstManaged = System.Math.Max(spkAgainstManaged, (kernel.GetGeometricStateRelativeTo(t, earth).Position - managed.Position).Magnitude());
            }

            var (_, vAt) = solution.InterpolateAt(tc);
            double atTc = (spacecraft.GetEphemeris(tc, earth, Frame.ICRF, Aberration.None).ToStateVector().Velocity - vAt).Magnitude();
            _output.WriteLine($"MEASURE #540 two impulses at one event S + {(tc - Start).TotalSeconds:R} s: arcs [{string.Join(",", arcs.Select(a => a.Length))}], {segments} segments, " +
                              $"boundary positions {boundary:E2} m, jump - dV1 - dV2 {jump:E2} m/s, near managed {near:E2} m, SPK - managed {spkAgainstManaged:E2} m, v at tc {atTc:E2} m/s");
            Assert.Equal(2, segments);
            Assert.Equal(0.0, boundary);
            Assert.True(jump <= PropagatedArcsTests.JumpAgainstDeltaV, $"jump {jump:E3} m/s");
            Assert.Equal(0.0, atTc);
            Assert.True(near <= PropagatedArcsTests.NearRk, $"near {near:E3} m");
            Assert.True(spkAgainstManaged <= PropagatedArcsTests.SpkAgainstManaged, $"SPK - managed {spkAgainstManaged:E3} m");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spk);
            spk.Delete();
        }
    }

    /// <summary>
    /// The covariance mode with an impulse and its execution error (Gates' model): every stored state carries its covariance
    /// (<c>Spacecraft.Propagate</c>'s contract), the states at the impulse included: before it, the covariance at the event;
    /// after it, <see cref="PropagationSolution.GetCovariance"/> at the impulse (the later segment); the states of a filled
    /// arc, that of their epoch; all rotated to ICRF, where the spacecraft stores them, by R6 = diag(R, R), R the matrix of
    /// <c>pxform</c>, when the propagation starts from ECLIPJ2000. The jump of the velocity block at the impulse against the
    /// covariance of the execution error, written here from the four terms of Gates (JPL TR 32-504, 1963, section III) on the
    /// velocity jump of the stored states: σ_s² v vᵀ + σ_r² u uᵀ + σ_p² (V² I − v vᵀ) + σ_a² (I − u uᵀ). Through
    /// <c>Spacecraft.Propagate</c> and <c>Scenario.SimulateAsync</c>. On the prototype the two states at the impulse carried
    /// none (review probe E2).
    /// </summary>
    [Theory]
    [InlineData("Apogee", 10.0, "ICRF", "Propagate", -5511)]
    [InlineData("CombinedLate", 60.0, "ICRF", "Propagate", -5512)]
    [InlineData("Apogee", 10.0, "ECLIPJ2000", "Propagate", -5513)]
    [InlineData("Apogee", 10.0, "ICRF", "Scenario", -5514)]
    public void Covariance_EveryStoredStateCarriesIt(string maneuver, double step, string frameName, string path, int naifId)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, Start);
        var frame = PropagatedArcsTests.FrameOf(frameName);
        var p0 = new Matrix(6, 6);
        for (int i = 0; i < 6; i++) p0.Set(i, i, i < 3 ? 1.0e4 : 1.0e-2);
        var leo = Leo(earth);
        var inFrame = frame == Frame.ICRF ? leo : leo.ToFrame(frame).ToStateVector();
        var initial = new StateVector(inFrame.Position, inFrame.Velocity, earth, Start, frame, p0);
        var error = new ManeuverExecutionError { ResolutionSigma = 0.05, ShutoffSigma = 0.01, AutopilotSigma = 0.02, PointingSigma = 0.005 };
        var spacecraft = new Spacecraft(naifId, $"PE{-naifId}", 1000.0, 3000.0, new Clock($"PE{-naifId}_CLK", 65536), initial);
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        ImpulseManeuver impulse = maneuver == "Apogee"
            ? new ApogeeHeightManeuver(earth, Start, TimeSpan.Zero, 8378137.0, engine) { ExecutionError = error }
            : new CombinedManeuver(earth, Start.AddSeconds(120.0), TimeSpan.Zero, 7078137.0, 45.0 * IO.Astrodynamics.Constants.Deg2Rad, engine) { ExecutionError = error };
        spacecraft.SetStandbyManeuver(impulse);
        var window = new Window(Start, Start.AddHours(2.0));
        var options = new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance };
        PropagationSolution solution;
        if (path == "Scenario")
        {
            var scenario = new Scenario($"SPE{-naifId}", new IO.Astrodynamics.Mission.Mission($"mPE{-naifId}"), window);
            scenario.AddCelestialItem(earth);
            scenario.AddSpacecraft(spacecraft);
            scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(step), () => new RK78Integrator(1e-12, 1e-12), options).GetAwaiter().GetResult();
            solution = spacecraft.LastPropagation;
        }
        else
        {
            solution = spacecraft.Propagate(window, [earth], new RK78Integrator(1e-12, 1e-12), false, false, TimeSpan.FromSeconds(step), options);
        }

        var stored = spacecraft.PropagatedStates;
        var arcs = spacecraft.PropagatedArcs;
        var tc = solution.Segments[1].BaseEpoch;
        var before = arcs[0][^1];
        var after = arcs[1][0];

        // R, the matrix of pxform(frame, J2000), by the images of the axes; the covariance of the solution in ICRF, R6 P R6ᵀ
        var q = SpiceAPI.Instance.TransformFrame(Start, frame, Frame.ICRF).Rotation;
        Vector3[] columns = [Vector3.VectorX.Rotate(q), Vector3.VectorY.Rotate(q), Vector3.VectorZ.Rotate(q)];
        double R(int i, int j) => i switch { 0 => columns[j].X, 1 => columns[j].Y, _ => columns[j].Z };
        Matrix InIcrf(Matrix p)
        {
            if (frame == Frame.ICRF) return p;
            var rotated = new Matrix(6, 6);
            for (int i = 0; i < 6; i++)
            for (int j = 0; j < 6; j++)
            {
                double value = 0.0;
                for (int k = 0; k < 6; k++)
                for (int l = 0; l < 6; l++)
                {
                    double rik = i / 3 == k / 3 ? R(i % 3, k % 3) : 0.0;
                    double rjl = j / 3 == l / 3 ? R(j % 3, l % 3) : 0.0;
                    value += rik * p.Get(k, l) * rjl;
                }

                rotated.Set(i, j, value);
            }

            return rotated;
        }

        static double Relative(Matrix a, Matrix b)
        {
            double worst = 0.0, scale = 0.0;
            for (int i = 0; i < 6; i++)
            for (int j = 0; j < 6; j++)
            {
                worst = System.Math.Max(worst, System.Math.Abs(a.Get(i, j) - b.Get(i, j)));
                scale = System.Math.Max(scale, System.Math.Abs(b.Get(i, j)));
            }

            return worst / scale;
        }

        double beforeDelta = Relative(before.Covariance!.Value, InIcrf(solution.GetCovariance(tc.AddSeconds(-1e-6))));
        double afterDelta = Relative(after.Covariance!.Value, InIcrf(solution.GetCovariance(tc)));
        // The covariance of the execution error, the four terms of Gates on the velocity jump v of the stored states (the
        // model is invariant under a rotation, so it is written in ICRF)
        var v = after.Velocity - before.Velocity;
        double speed = v.Magnitude();
        var u = v / speed;
        double sr2 = error.ResolutionSigma * error.ResolutionSigma, ss2 = error.ShutoffSigma * error.ShutoffSigma;
        double sa2 = error.AutopilotSigma * error.AutopilotSigma, sp2 = error.PointingSigma * error.PointingSigma;
        double[] vc = [v.X, v.Y, v.Z], uc = [u.X, u.Y, u.Z];
        var le = new double[9];
        for (int i = 0; i < 3; i++)
        for (int j = 0; j < 3; j++)
        {
            double identity = i == j ? 1.0 : 0.0;
            le[3 * i + j] = ss2 * vc[i] * vc[j] + sr2 * uc[i] * uc[j] + sp2 * (speed * speed * identity - vc[i] * vc[j]) + sa2 * (identity - uc[i] * uc[j]);
        }

        double jump = 0.0, leScale = le.Select(System.Math.Abs).Max();
        for (int i = 0; i < 3; i++)
        for (int j = 0; j < 3; j++)
        {
            double delta = after.Covariance.Value.Get(i + 3, j + 3) - before.Covariance.Value.Get(i + 3, j + 3);
            jump = System.Math.Max(jump, System.Math.Abs(delta - le[3 * i + j]));
        }

        // The filled states: the covariance of their epoch
        double filled = 0.0;
        int filledStates = 0;
        foreach (var arc in arcs.Where(a => a.Length == PropagatedEphemeris.FilledArcStates))
        {
            for (int i = 1; i < arc.Length - 1; i++)
            {
                filled = System.Math.Max(filled, Relative(arc[i].Covariance!.Value, InIcrf(solution.GetCovariance(arc[i].Epoch))));
                filledStates++;
            }
        }

        _output.WriteLine($"MEASURE #540 covariance {maneuver} {step} s {frameName} {path}: {stored.Count(x => x.Covariance == null)} of {stored.Length} stored states without P, " +
                          $"before - GetCovariance(tc - 1 us) {beforeDelta:E2}, after - GetCovariance(tc) {afterDelta:E2} (relative), " +
                          $"velocity jump - L_e of Gates {jump:E2} m2/s2 (L_e {leScale:E2}), {filledStates} filled states - GetCovariance {filled:E2}");
        Assert.All(stored, x => Assert.NotNull(x.Covariance));
        Assert.True(beforeDelta <= CovarianceBefore, $"before {beforeDelta:E3}");
        Assert.True(jump <= ExecutionErrorJump * leScale, $"jump - L_e {jump:E3}");
        if (frame == Frame.ICRF)
        {
            // Without a rotation, the covariance of the solution itself, to the bit
            Assert.Equal(0.0, afterDelta);
            Assert.Equal(0.0, filled);
        }
        else
        {
            Assert.True(afterDelta <= RotatedCovariance, $"after {afterDelta:E3}");
            Assert.True(filled <= RotatedCovariance, $"filled {filled:E3}");
        }

        Assert.Equal(maneuver == "CombinedLate" ? PropagatedEphemeris.FilledArcStates - 2 : 0, filledStates);
    }

    /// <summary>
    /// A short arc, an impulse within the first two outputs (a combined maneuver from S + 120 s, 60 s outputs): the first arc
    /// takes 16 states evenly spaced, its ends the state at the start and the state before the impulse, its others the
    /// solution's (<see cref="PropagationSolution.GetStateVector"/>), to the bit; the outputs within it are not stored.
    /// </summary>
    [Theory]
    [InlineData("RK78", -5521)]
    [InlineData("VVd", -5522)]
    public void ShortArc_IsFilledFromTheSolution(string integrator, int naifId)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, Start);
        var spacecraft = new Spacecraft(naifId, $"PE{-naifId}", 1000.0, 3000.0, new Clock($"PE{-naifId}_CLK", 65536), Leo(earth));
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        spacecraft.SetStandbyManeuver(new CombinedManeuver(earth, Start.AddSeconds(120.0), TimeSpan.Zero, 7078137.0, 45.0 * IO.Astrodynamics.Constants.Deg2Rad, engine));
        var step = TimeSpan.FromSeconds(60.0);
        var solution = spacecraft.Propagate(new Window(Start, Start.AddHours(2.0)), [earth], integrator == "RK78" ? new RK78Integrator(1e-12, 1e-12) : new VVIntegrator(step),
            false, false, step);
        var arc = spacecraft.PropagatedArcs[0];
        var tc = solution.Segments[1].BaseEpoch;
        Assert.Equal(PropagatedEphemeris.FilledArcStates, arc.Length);
        Assert.Equal(Start, arc[0].Epoch);
        Assert.Equal(tc, arc[^1].Epoch);
        double span = (tc - Start).TotalSeconds;
        for (int i = 1; i < arc.Length - 1; i++)
        {
            Assert.Equal(Start.AddSeconds(span * i / (PropagatedEphemeris.FilledArcStates - 1)), arc[i].Epoch);
            var expected = solution.GetStateVector(arc[i].Epoch);
            Assert.Equal(expected.Position, arc[i].Position);
            Assert.Equal(expected.Velocity, arc[i].Velocity);
        }

        _output.WriteLine($"MEASURE #540 filled arc {integrator}: tc = S + {span:F7} s, arcs [{string.Join(",", spacecraft.PropagatedArcs.Select(a => a.Length))}]");
    }

    /// <summary>
    /// Velocity-Verlet puts an output at the impulse or one tick after it (1.19e-7 s in the ET double, events at the end of
    /// a step on the grid): the output is not stored, the states of the impulse are; the read at that output is the state
    /// after the impulse, against the output state.
    /// </summary>
    [Theory]
    [InlineData("Apogee|VVd|10|Propagate|ICRF|Earth", 1, 0)]
    [InlineData("Chain4|VVd|10|Propagate|ICRF|Earth", 1, 2)]
    [InlineData("Chain4|VVd|60|Propagate|ICRF|Earth", 1, 3)]
    public void VelocityVerletOutputAtTheImpulse_IsNotStored(string key, int expectedAtTc, int expectedTickAfter)
    {
        using var run = PropagatedArcsTests.ArcRun.Run(-(5530 + Array.IndexOf(PropagatedArcsTests.AllCases, key)), key, writeKernel: false);
        var stored = run.Spacecraft.PropagatedStates;
        int atImpulses = 0;
        double worst = 0.0;
        foreach (var output in run.Solution.StateVectors)
        {
            var tc = run.ImpulseEpochs.FirstOrDefault(x => System.Math.Abs((output.Epoch - x).TotalSeconds) <= PropagatedEphemeris.SameEpochTolerance);
            if (tc == default) continue;
            atImpulses++;
            Assert.DoesNotContain(stored, x => x.Epoch == output.Epoch && x.Epoch != tc);
            Assert.True(output.Epoch >= tc, $"an output {(output.Epoch - tc).TotalSeconds:E3} s before the impulse");
            var read = run.Spacecraft.GetEphemeris(output.Epoch, run.ReadCenter, Frame.ICRF, Aberration.None).ToStateVector();
            worst = System.Math.Max(worst, (read.Position - output.Position).Magnitude());
        }

        int atTc = run.Solution.StateVectors.Count(o => run.ImpulseEpochs.Contains(o.Epoch));
        int tickAfter = run.Solution.StateVectors.Count(o => run.ImpulseEpochs.Any(tc => o.Epoch == tc.AddSeconds(1e-7)));
        _output.WriteLine($"MEASURE #540 VV outputs at the impulse {key.Replace('|', ' ')}: {atImpulses} of {run.ImpulseEpochs.Length} impulses ({atTc} at tc, {tickAfter} one tick after), read - output {worst:E2} m");
        Assert.True(atImpulses > 0);
        Assert.Equal(expectedAtTc, atTc);
        Assert.Equal(expectedTickAfter, tickAfter);
        // Both states of each impulse are stored at its epoch
        foreach (var tc in run.ImpulseEpochs) Assert.Equal(2, stored.Count(x => x.Epoch == tc));
        Assert.True(worst <= OutputAtTheImpulse, $"read - output {worst:E3} m");
    }

    /// <summary>
    /// <c>AddPropagatedStates</c>: without a propagation the states make one arc, read as before 11.0.0 (the Lagrange
    /// interpolation of the 8 nearest of all, to the bit) and written as one segment; after a propagation each state goes to
    /// the arc of its epoch, the later one at an impulse; a propagation replaces the states added before it (they were merged
    /// before 11.0.0).
    /// </summary>
    [Fact]
    public void AddPropagatedStates_ToTheArcOfTheirEpoch_AndReplacedByAPropagation()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, Start);
        var (source, _) = Apogee(-5541, Leo(earth), earth);
        var window = new Window(Start, Start.AddHours(2.0));
        var step = TimeSpan.FromSeconds(10.0);
        var sourceSolution = source.Propagate(window, [earth], new RK78Integrator(1e-12, 1e-12), false, false, step);
        var outputs = sourceSolution.StateVectors.Take(200).ToArray();

        // Without a propagation: one arc, the former read
        var added = new Spacecraft(-5542, "PE5542", 1000.0, 3000.0, new Clock("PE5542_CLK", 65536), Leo(earth));
        Assert.Throws<ArgumentNullException>(() => added.AddPropagatedStates(null));
        added.AddPropagatedStates(outputs.Reverse().ToArray());
        Assert.Single(added.PropagatedArcs);
        Assert.Equal(outputs.Select(x => x.Epoch), added.PropagatedStates.Select(x => x.Epoch));
        for (double s = 1.3; s < 1990.0; s += 37.0)
        {
            var t = Start.AddSeconds(s);
            var former = IO.Astrodynamics.Math.Lagrange.Interpolate(outputs, t);
            var read = added.GetGeometricStateRelativeTo(t, earth);
            Assert.Equal(former.Position, read.Position);
            Assert.Equal(former.Velocity, read.Velocity);
        }

        // A state at an epoch already stored replaces it
        var replacement = new StateVector(outputs[5].Position + new Vector3(1.0, 0.0, 0.0), outputs[5].Velocity, earth, outputs[5].Epoch, Frame.ICRF);
        added.AddPropagatedStates(replacement);
        Assert.Equal(outputs.Length, added.PropagatedStates.Length);
        Assert.Same(replacement, added.PropagatedStates[5]);
        var spk = NewSpk("pe5542");
        added.WriteEphemeris(spk);
        var segments = CspiceDirectOrNull(spk.FullName);
        if (segments != null) Assert.Single(segments);
        spk.Delete();

        // After a propagation: the arc of each epoch
        var (propagated, _) = Apogee(-5543, Leo(earth), earth);
        propagated.AddPropagatedStates(new StateVector(Leo(earth).Position, Leo(earth).Velocity, earth, Start.AddSeconds(3.3), Frame.ICRF));
        var solution = propagated.Propagate(window, [earth], new RK78Integrator(1e-12, 1e-12), false, false, step);
        // The propagation replaced the state added before it
        Assert.DoesNotContain(propagated.PropagatedStates, x => x.Epoch == Start.AddSeconds(3.3));
        var tc = solution.Segments[1].BaseEpoch;
        var lengths = propagated.PropagatedArcs.Select(a => a.Length).ToArray();
        var early = solution.GetStateVector(tc.AddSeconds(-3.0));
        var late = solution.GetStateVector(tc.AddSeconds(3.0));
        var atImpulse = solution.GetStateVector(tc);
        propagated.AddPropagatedStates(early, late, atImpulse);
        var arcs = propagated.PropagatedArcs;
        Assert.Equal(lengths[0] + 1, arcs[0].Length);
        Assert.Equal(lengths[1] + 1, arcs[1].Length);
        Assert.Contains(arcs[0], x => x.Epoch == early.Epoch);
        Assert.Contains(arcs[1], x => x.Epoch == late.Epoch);
        // At the impulse, the state after it is replaced
        Assert.Same(atImpulse, arcs[1][0]);
        // Before every arc and after every arc: the first and the last
        var first = new StateVector(Leo(earth).Position, Leo(earth).Velocity, earth, Start.AddSeconds(-5.0), Frame.ICRF);
        var last = solution.GetStateVector(window.EndDate);
        last = new StateVector(last.Position, last.Velocity, earth, window.EndDate.AddSeconds(5.0), Frame.ICRF);
        propagated.AddPropagatedStates(first, last);
        Assert.Same(first, propagated.PropagatedArcs[0][0]);
        Assert.Same(last, propagated.PropagatedArcs[^1][^1]);
        Assert.Equal(new Window(first.Epoch, last.Epoch), propagated.InterpolatedStatesWindow);
    }

    // The segments of an SPK by dafus where the native library is known to export CSPICE (Linux), null elsewhere
    private static List<CspiceDirect.Segment> CspiceDirectOrNull(string path) => OperatingSystem.IsLinux() ? CspiceDirect.ReadSegments(path) : null;

    /// <summary>
    /// The Cosmographia export of a scenario whose spacecraft maneuvers: its SPK, read by <c>spkezr</c> through a
    /// kernel-backed spacecraft, gives the state before the impulse 1 µs before it and the state after at it, against the
    /// RK7(8) solution.
    /// </summary>
    [Fact]
    public async Task CosmographiaExport_SpkAcrossTheImpulse()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, Start);
        var (sc, _) = Apogee(-5551, Leo(earth), earth);
        var scenario = new Scenario("PA540Cosmo", new IO.Astrodynamics.Mission.Mission("PA540CosmoMission"), new Window(Start, Start.AddHours(2.0)));
        scenario.AddCelestialItem(earth);
        scenario.AddSpacecraft(sc);
        await scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(10.0), () => new RK78Integrator(1e-12, 1e-12));
        var solution = sc.LastPropagation;
        var tc = solution.Segments[1].BaseEpoch;
        var directory = new DirectoryInfo("CosmographiaExport540");
        await new CosmographiaExporter().ExportAsync(scenario, directory);
        var spk = directory.GetFiles($"{sc.Name}.spk", SearchOption.AllDirectories).Single();
        SpiceAPI.Instance.LoadKernels(spk);
        try
        {
            var kernel = new Spacecraft(sc.NaifId, sc.Name, Start, 1000.0, 3000.0, new Clock(sc.Name + "K_CLK", 65536));
            double worstPosition = 0.0, worstVelocity = 0.0;
            foreach (var t in new[] { tc.AddSeconds(-1e-6), tc, tc.AddSeconds(1e-6) })
            {
                var (position, velocity) = solution.InterpolateAt(t);
                var read = kernel.GetGeometricStateRelativeTo(t, earth);
                worstPosition = System.Math.Max(worstPosition, (read.Position - position).Magnitude());
                worstVelocity = System.Math.Max(worstVelocity, (read.Velocity - velocity).Magnitude());
            }

            _output.WriteLine($"MEASURE #540 Cosmographia SPK at tc - 1 us, tc, tc + 1 us: {worstPosition:E2} m, {worstVelocity:E2} m/s");
            Assert.True(worstPosition <= CosmographiaPosition, $"{worstPosition:E3} m");
            Assert.True(worstVelocity <= CosmographiaVelocity, $"{worstVelocity:E3} m/s");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spk);
        }
    }

    /// <summary>
    /// A conjunction whose secondary maneuvers 60 s before the TCA, read as an <see cref="ILocalizable"/> (its stored states)
    /// against the same analysis on the RK7(8) solutions of both participants (<c>InterpolateAt</c>): the TCA and the miss
    /// distance. The protected spacecraft is built to pass 200 m radially from the secondary's position 60 s after its burn,
    /// on a plane 20° away, by <c>prop2b</c> back to the start.
    /// </summary>
    [Fact]
    public void Conjunction_WithAManeuveringSecondary_ReadThroughItsStoredStates()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, Start);
        var window = new Window(Start, Start.AddHours(2.0));
        var step = TimeSpan.FromSeconds(10.0);
        var (secondary, _) = Apogee(-5561, Leo(earth), earth);
        var secondarySolution = secondary.Propagate(window, [earth], new RK78Integrator(1e-12, 1e-12), false, false, step);
        var tc = secondarySolution.Segments[1].BaseEpoch;
        var tca = tc.AddSeconds(60.0);
        var (r, v) = secondarySolution.InterpolateAt(tca);
        var axis = r.Normalize();
        double angle = 20.0 * IO.Astrodynamics.Constants.Deg2Rad;
        var turned = v * System.Math.Cos(angle) + axis.Cross(v) * System.Math.Sin(angle) + axis * (axis * v) * (1.0 - System.Math.Cos(angle));
        var atTca = new StateVector(r + axis * 200.0, turned, earth, tca, Frame.ICRF);
        double back = -PropagationTimeArgument.Tt.SecondsBetween(Start, tca);
        long ticks = (long)System.Math.Round(back * TimeSpan.TicksPerSecond);
        var initial = SpiceAPI.Instance.Propagate2Bodies(atTca, earth.GM, TimeSpan.FromTicks(ticks));
        initial = new StateVector(initial.Position, initial.Velocity, earth, Start, Frame.ICRF);
        var protectedCraft = new Spacecraft(-5562, "PE5562", 1000.0, 3000.0, new Clock("PE5562_CLK", 65536), initial);
        var protectedSolution = protectedCraft.Propagate(window, [earth], new RK78Integrator(1e-12, 1e-12), false, false, step);

        var search = new Window(tc.AddSeconds(-100.0), tc.AddSeconds(300.0));
        var profile = new ProtectedSpacecraftProfile(protectedCraft);
        var throughStates = ConjunctionAssessment.Analyze(profile, secondary, search);
        var throughSolutions = ConjunctionAssessment.Analyze(profile, protectedSolution, secondary, secondarySolution, search);
        double tcaDelta = System.Math.Abs((throughStates.EncounterState.Epoch - throughSolutions.EncounterState.Epoch).TotalSeconds);
        double missDelta = System.Math.Abs(throughStates.EncounterState.MissDistanceMeters - throughSolutions.EncounterState.MissDistanceMeters);
        _output.WriteLine($"MEASURE #540 conjunction with a maneuvering ILocalizable: TCA S + {(throughSolutions.EncounterState.Epoch - Start).TotalSeconds:F6} s (designed {(tca - Start).TotalSeconds:F6} s), " +
                          $"miss {throughSolutions.EncounterState.MissDistanceMeters:F4} m; stored states - solutions: TCA {tcaDelta:E2} s, miss {missDelta:E2} m");
        Assert.True(System.Math.Abs((throughSolutions.EncounterState.Epoch - tca).TotalSeconds) < 1.0);
        Assert.True(tcaDelta <= ConjunctionTca, $"TCA {tcaDelta:E3} s");
        Assert.True(missDelta <= ConjunctionMiss, $"miss {missDelta:E3} m");
    }

    /// <summary>
    /// The managed occultation of the Sun by the Earth seen from a spacecraft whose impulse falls at the edge of the shadow
    /// (#540's orbit turned in its plane), RK7(8) and Velocity-Verlet, against <c>gfoclt</c> on the SPK the library writes
    /// (a kernel-backed spacecraft): every edge within ±150 s of the impulse.
    /// </summary>
    [Theory]
    [InlineData("Apogee|RK78|10|Propagate|ICRF|Eclipse", -5571)]
    [InlineData("Apogee|VVd|10|Propagate|ICRF|Eclipse", -5572)]
    public void ManagedOccultation_NearABurn_IsThatOfGfoclt(string key, int naifId)
    {
        using var run = PropagatedArcsTests.ArcRun.Run(naifId, key);
        var tc = run.ImpulseEpochs[0];
        var sun = new CelestialBody(Stars.Sun, Frame.ICRF, run.Window.StartDate);
        var search = new Window(tc.AddSeconds(-150.0), tc.AddSeconds(150.0));
        Window[] Search(Spacecraft observer, OccultationType type) => sun.FindWindowsOnOccultationConstraint(search, observer, ShapeType.Ellipsoid, run.ReadCenter,
            ShapeType.Ellipsoid, type, Aberration.None, TimeSpan.FromSeconds(1.0)).ToArray();
        double worst = 0.0;
        var edges = new List<double>();
        foreach (var type in new[] { OccultationType.Any, OccultationType.Full })
        {
            var managed = Search(run.Spacecraft, type);
            var spice = Search(run.Kernel, type);
            Assert.Equal(spice.Length, managed.Length);
            for (int i = 0; i < managed.Length; i++)
            {
                foreach (var (a, b) in new[] { (managed[i].StartDate, spice[i].StartDate), (managed[i].EndDate, spice[i].EndDate) })
                {
                    if (a <= search.StartDate || a >= search.EndDate) continue;
                    edges.Add((a - tc).TotalSeconds);
                    worst = System.Math.Max(worst, System.Math.Abs((a - b).TotalSeconds));
                }
            }
        }

        _output.WriteLine($"MEASURE #540 occultation near a burn {key.Replace('|', ' ')}: edges at tc + [{string.Join(", ", edges.Select(x => x.ToString("F3")))}] s, managed - gfoclt {worst:E2} s");
        Assert.NotEmpty(edges);
        Assert.True(worst <= EdgesAgainstSpice, $"managed - gfoclt {worst:E3} s");
    }

    /// <summary>
    /// The managed field-of-view search near a burn against <c>gftfov</c> on the kernels the library writes: the centre of the
    /// Earth crosses the circular field of view of two instruments 30 s before and 30 s after the impulse (their boresights
    /// along the direction of the Earth there, from a first propagation: the identity of ICRF before the impulse, the
    /// impulse's attitude after it), RK7(8), the orbit of <c>AttitudeRecordTests</c>.
    /// </summary>
    [Fact]
    public void ManagedFieldOfView_NearABurn_IsThatOfGftfov()
    {
        ApogeeHeightManeuver Chain(Spacecraft sc, Engine engine, CelestialBody earth, Time start)
        {
            var apogee = new ApogeeHeightManeuver(earth, start.AddSeconds(100.0), TimeSpan.Zero, 7200000.0, engine);
            sc.SetStandbyManeuver(apogee);
            return apogee;
        }

        // A first propagation: the epoch of the impulse, the attitude after it and the directions of the Earth
        Vector3 before = default, after = default;
        Time tc = default;
        using (var probe = AttitudeRecordTests.PropagatedCase.Run(-5581, "Custom", "RK78", Frame.ICRF, "Propagate", 2024,
                   new AttitudeRecordTests.PropagatedCase.Hooks { Configure = (sc, engine, earth, _, start) => Chain(sc, engine, earth, start), WriteKernels = false }))
        {
            tc = probe.Solution.Segments[1].BaseEpoch;
            before = probe.StateAt(tc.AddSeconds(-30.0)).Position.Inverse().Normalize();
            var q = probe.Spacecraft.Frame.GetStateOrientationToICRF(tc.AddSeconds(30.0)).Rotation;
            after = probe.StateAt(tc.AddSeconds(30.0)).Position.Inverse().Normalize().Rotate(q.Conjugate());
        }

        static Vector3 Perpendicular(Vector3 b) => (System.Math.Abs(b.X) < 0.9 ? Vector3.VectorX : Vector3.VectorY).Cross(b).Normalize();
        using var run = AttitudeRecordTests.PropagatedCase.Run(-5582, "Custom", "RK78", Frame.ICRF, "Propagate", 2024, new AttitudeRecordTests.PropagatedCase.Hooks
        {
            Configure = (sc, engine, earth, _, start) =>
            {
                sc.AddCircularInstrument(-5582901, "BEFORE", "fov540", 0.02, before, Perpendicular(before), Vector3.Zero);
                sc.AddCircularInstrument(-5582902, "AFTER", "fov540", 0.02, after, Perpendicular(after), Vector3.Zero);
                Chain(sc, engine, earth, start);
            }
        });
        Assert.Equal(tc, run.Solution.Segments[1].BaseEpoch);
        var search = new Window(tc.AddSeconds(-150.0), tc.AddSeconds(150.0));
        var step = TimeSpan.FromSeconds(5.0);
        double worst = 0.0;
        var edges = new List<double>();
        foreach (var instrument in run.Spacecraft.Instruments.Where(x => x.Name is "BEFORE" or "AFTER"))
        {
            var managed = instrument.FindWindowsInFieldOfViewConstraint(search, run.Spacecraft, run.Earth, run.Earth.Frame, ShapeType.Point, Aberration.None, step).ToArray();
            var spice = SpiceAPI.Instance.FindWindowsInFieldOfViewConstraint(search, run.Spacecraft.NaifId, instrument.NaifId, run.Earth.NaifId, run.Earth.Frame, ShapeType.Point,
                Aberration.None, step).ToArray();
            Assert.Single(managed);
            Assert.Equal(spice.Length, managed.Length);
            foreach (var (a, b) in new[] { (managed[0].StartDate, spice[0].StartDate), (managed[0].EndDate, spice[0].EndDate) })
            {
                edges.Add((a - tc).TotalSeconds);
                worst = System.Math.Max(worst, System.Math.Abs((a - b).TotalSeconds));
            }
        }

        _output.WriteLine($"MEASURE #540 field of view near a burn: edges at tc + [{string.Join(", ", edges.Select(x => x.ToString("F3")))}] s, managed - gftfov {worst:E2} s");
        Assert.Equal(4, edges.Count);
        Assert.True(worst <= EdgesAgainstSpice, $"managed - gftfov {worst:E3} s");
    }
}
