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
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Body;

// Inside IO.Astrodynamics.Tests the namespaces Frame and Time shadow the library types
using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The states of a propagated spacecraft across its impulses, as the library reads them (the managed path) and as it
/// writes them (its SPK), against the propagation's own solution and CSPICE in the process on the SPK the library writes
/// (#540, §7.2 of the design of #554). Never a state the library wrote and read back, never a snapshot.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reference.</b> The solution of the propagation (<see cref="PropagationSolution.InterpolateAt"/>): in RK7(8), a
/// shortened step from the start of its accepted step, at the accuracy of the integrator, itself checked against CSPICE's
/// <c>prop2b</c> in the two-body cases (<see cref="Rk78Solution_MatchesProp2b"/>); in Velocity-Verlet, its cubic Hermite,
/// which does not resolve the Lagrange interpolation of the outputs (0.16 m at 10 s outputs, the integrator's own): the
/// Velocity-Verlet rows measure that no trace of the impulse is left, the error near a burn against the error far from
/// it. Rotated to ICRF by <c>pxform</c> (<see cref="SpiceAPI.TransformFrame(Time, Frame, Frame)"/>); in SSB mode, where
/// the solution is relative to the Sun, less the Earth's state of <c>spkezr</c> on the planetary ephemeris. The
/// Velocity-Verlet path of <c>Scenario.SimulateAsync</c> keeps no solution: that of a twin propagated by
/// <c>Spacecraft.Propagate</c>, whose stored states equal the scenario's to the bit.
/// </para>
/// <para>
/// <b>The readers.</b> The managed read, <c>GetEphemeris</c> relative to the central body in ICRF; the SPK, through a
/// kernel-backed spacecraft built on it (<c>spkezr</c>).
/// </para>
/// <para>
/// <b>The samples.</b> Near: every 0.25 s within ±150 s of each impulse, and at tc, tc ± 1 µs, tc ± 1 ms. Far: every 7 s
/// (37 s for the chain of four), farther than max(150 s, 10 output steps) from every impulse.
/// </para>
/// <para>
/// <b>Limits.</b> PROVISIONAL: twice the worst on Linux x64 (2026-11-03, the 73 cases of the grid, MEASURE lines),
/// rounded up at the second significant digit, to be replaced by twice the worst over Linux, Windows and macOS arm64 from
/// the CI's MEASURE lines. The grid takes about 30 s and runs in every CI job. Each limit bounds the family of its worst,
/// named in its summary; a family is never bounded by the limit of a larger one (#515).
/// </para>
/// </remarks>
public class PropagatedArcsTests
{
    /// <summary>The managed reads within ±150 s of an impulse against the RK7(8) solution, at 1 and 10 s outputs, one or two
    /// impulses about the Earth: the far error of the Lagrange interpolation, no trace of the impulse. PROVISIONAL, Linux
    /// worst 9.32e-9 m (Apogee, RK7(8), 10 s, FK4); main 23.5 to 1 420 m.</summary>
    internal const double NearRk = 1.9e-8;

    /// <summary>The same on the chain of four of <c>ScenarioTests</c> (Earth, Moon and Sun, 24.7 h). PROVISIONAL, Linux worst
    /// 2.25e-8 m (10 s, both paths); main 763 m.</summary>
    internal const double NearRkChain = 4.5e-8;

    /// <summary>The same at 60 s outputs: the per-arc interpolation is one-sided at the ends of an arc. PROVISIONAL, Linux
    /// worst 2.97e-4 m (Apogee and Perigee); main 2 250 m.</summary>
    internal const double NearRk60 = 6.0e-4;

    /// <summary>The same on the chain of four at 60 s outputs, whose arcs end 7 min apart. PROVISIONAL, Linux worst
    /// 1.81e-2 m (both paths); main 2 120 m.</summary>
    internal const double NearRkChain60 = 3.7e-2;

    /// <summary>The managed reads near an impulse against the Velocity-Verlet solution at 1 s steps (the cubic Hermite of
    /// its steps), at 1, 10 and 60 s outputs: the event label of Velocity-Verlet, not carried to the tick grid, puts the
    /// states at the impulse up to 1e-7 s from their label (§9 item 4 of #554, its own issue), 7.5e-4 m at most.
    /// PROVISIONAL, Linux worst 5.94e-4 m (Perigee, 1, 10 and 60 s); main 18.5 to 1 290 m.</summary>
    internal const double NearVv = 1.2e-3;

    /// <summary>Velocity-Verlet at the output step: the managed reads near an impulse over the reads far from it, both
    /// against its cubic Hermite, which the Lagrange of the outputs does not match (0.16 m at 10 s, 33 m at 60 s): no
    /// trace of the impulse. PROVISIONAL, Linux worst 1.07 (Apogee, 60 s); main 1 200 (#540's case, 192 m against
    /// 0.16 m).</summary>
    internal const double NearOverFarVvd = 2.2;

    /// <summary>The managed reads near an impulse in SSB mode, the stored states relative to the Sun, 1.5e11 m away, against
    /// the solution less the Earth of <c>spkezr</c>. PROVISIONAL, Linux worst 2.14e-4 m (RK7(8)); main 320 m.</summary>
    internal const double NearSsb = 4.3e-4;

    /// <summary>The managed reads near an impulse about the Moon, RK7(8). PROVISIONAL, Linux worst 1.26e-9 m.</summary>
    internal const double NearRkMoon = 2.6e-9;

    /// <summary>The same with Velocity-Verlet at 1 s. PROVISIONAL, Linux worst 2.11e-5 m.</summary>
    internal const double NearVvMoon = 4.3e-5;

    /// <summary><c>spkezr</c> on the SPK the library writes against the managed reads, over every sample, about the Earth with
    /// one or two impulses: the resolution of the ET doubles of the nodes (1.2e-7 s) times the speed. PROVISIONAL, Linux
    /// worst 8.67e-4 m (Perigee, RK7(8), 60 s).</summary>
    internal const double SpkAgainstManaged = 1.8e-3;

    /// <summary>The same on the chain of four. PROVISIONAL, Linux worst 1.85e-3 m (RK7(8), 60 s).</summary>
    internal const double SpkAgainstManagedChain = 3.7e-3;

    /// <summary>The same in SSB mode (the SPK relative to the Sun). PROVISIONAL, Linux worst 1.71e-3 m (RK7(8)).</summary>
    internal const double SpkAgainstManagedSsb = 3.5e-3;

    /// <summary>The same about the Moon. PROVISIONAL, Linux worst 7.99e-5 m.</summary>
    internal const double SpkAgainstManagedMoon = 1.6e-4;

    /// <summary>The managed velocity 1 µs before an impulse against the RK7(8) solution there (the state before it).
    /// PROVISIONAL, Linux worst 1.25e-11 m/s (the chain of four, 60 s); main 40 to 890 m/s.</summary>
    internal const double VelocityBeforeRk = 2.5e-11;

    /// <summary>The same against the solution of Velocity-Verlet, and in SSB mode. PROVISIONAL, Linux worst 8.40e-7 m/s (the
    /// chain of four, Velocity-Verlet at 10 s); 3.29e-8 m/s in SSB mode (RK7(8)).</summary>
    internal const double VelocityBefore = 1.7e-6;

    /// <summary>The velocity of <c>spkezr</c> on the SPK 1 µs before an impulse against the solution. PROVISIONAL, Linux worst
    /// 7.95e-7 m/s (Perigee, Velocity-Verlet at 60 s).</summary>
    internal const double VelocityBeforeSpk = 1.6e-6;

    /// <summary>The velocity of <c>spkezr</c> at an impulse against the state after it, the later segment. PROVISIONAL, Linux
    /// worst 2.92e-12 m/s (Apogee, Velocity-Verlet at 10 s, SSB mode).</summary>
    internal const double VelocityAtSpk = 5.9e-12;

    /// <summary>The velocity of the state after an impulse less that of the state before against the delta-V of the maneuver
    /// rotated to ICRF by <c>pxform</c>: the rounding of the sum and of the rotation. PROVISIONAL, Linux worst 2.38e-12 m/s
    /// (Apogee, Velocity-Verlet at 1 s, FK4).</summary>
    internal const double JumpAgainstDeltaV = 4.8e-12;

    /// <summary>The RK7(8) solution against <c>prop2b</c> in the two-body cases, m and m/s: 1.1e-6 m before the first
    /// impulse; after it, where the closed form restarts at the label of the impulse, 1.1e-3 m, 1.5e-7 s of the motion,
    /// the order of the resolution of the TT seconds between two labels (ET doubles, 1.2e-7 s at 7.6e8 s; not traced
    /// further). PROVISIONAL, Linux worst 1.14e-3 m and 1.33e-6 m/s (Plane).</summary>
    internal const double SolutionAgainstProp2b = 2.3e-3;

    internal const double SolutionVelocityAgainstProp2b = 2.7e-6;

    private readonly ITestOutputHelper _output;

    public PropagatedArcsTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    internal static readonly string[] OtherFrames = ["GCRF", "ECLIPJ2000", "B1950", "FK4", "ECLIPB1950", "GALACTIC"];

    internal static Frame FrameOf(string name) => name switch
    {
        "ICRF" => Frame.ICRF,
        "GCRF" => Frame.GCRF,
        _ => new Frame(name)
    };

    // Every case of the grid, in a fixed order: its index gives the NAIF identifier of its spacecraft. A key is
    // maneuver|integrator|output step|path|frame|variant; VV is Velocity-Verlet at 1 s, VVd at the output step (the
    // default integrator of Spacecraft.Propagate, #540's configuration)
    internal static readonly string[] AllCases = BuildCases();

    private static string[] BuildCases()
    {
        var cases = new List<string>();
        foreach (var maneuver in new[] { "Apogee", "Perigee", "Plane", "Combined" })
        foreach (var integrator in new[] { "VV", "VVd", "RK78" })
        foreach (var step in new[] { 1, 10, 60 })
        {
            if (integrator == "VVd" && step == 1) continue;
            cases.Add($"{maneuver}|{integrator}|{step}|Propagate|ICRF|Earth");
        }

        // An impulse within the first two outputs: a short arc, filled
        foreach (var integrator in new[] { "VV", "VVd", "RK78" })
            cases.Add($"CombinedLate|{integrator}|60|Propagate|ICRF|Earth");

        // The chain of four of ScenarioTests: plane and apsidal alignment, phasing, apogee height
        foreach (var integrator in new[] { "VVd", "RK78" })
        foreach (var step in new[] { 10, 60 })
        foreach (var path in new[] { "Propagate", "Scenario" })
            cases.Add($"Chain4|{integrator}|{step}|{path}|ICRF|Earth");

        foreach (var integrator in new[] { "VV", "VVd", "RK78" })
        {
            cases.Add($"Apogee|{integrator}|10|Scenario|ICRF|Earth");
            foreach (var frame in OtherFrames)
                cases.Add($"Apogee|{integrator}|10|Propagate|{frame}|Earth");
            cases.Add($"Apogee|{integrator}|10|Propagate|ICRF|SSB");
            cases.Add($"Apogee|{integrator}|10|Propagate|ICRF|Moon");
            cases.Add($"Apogee|{integrator}|10|Propagate|ECLIPJ2000|2026");
        }

        return cases.ToArray();
    }

    public static IEnumerable<object[]> Grid() => AllCases.Select(x => new object[] { x });

    [Theory]
    [MemberData(nameof(Grid))]
    public void Case_MatchesSolutionAndSpice(string key) => Check(key);

    private void Check(string key)
    {
        int index = Array.IndexOf(AllCases, key);
        Assert.True(index >= 0, key);
        using var run = ArcRun.Run(-(5400 + index), key);
        var m = Measure(run);
        _output.WriteLine($"MEASURE #540 arcs {key.Replace('|', ' ')}: near managed {m.NearManaged:E2} m SPK {m.NearSpk:E2} m, far managed {m.FarManaged:E2} m SPK {m.FarSpk:E2} m, " +
                          $"SPK - managed {m.SpkAgainstManaged:E2} m, v at tc - 1 us managed {m.VelocityBeforeManaged:E2} SPK {m.VelocityBeforeSpk:E2} m/s, " +
                          $"v at tc managed {m.VelocityAtManaged:E2} SPK {m.VelocityAtSpk:E2} m/s, jump - dV {m.JumpAgainstDeltaV:E2} m/s, boundary positions {m.BoundaryPositions:E2} m, " +
                          $"at the outputs {m.AtOutputs:E2} m, twin {m.TwinStates:E2}, {m.Impulses} impulses, arcs [{string.Join(",", run.Spacecraft.PropagatedArcs.Select(a => a.Length))}], " +
                          $"{m.NearReads} near and {m.FarReads} far reads, {m.FarDiffering} far reads differing from the former sort");

        // The arcs: one per interval between impulses, meeting at each impulse's epoch, the states before and after it there
        var arcs = run.Spacecraft.PropagatedArcs;
        Assert.Equal(run.ImpulseEpochs.Length + 1, arcs.Count);
        for (int k = 0; k < run.ImpulseEpochs.Length; k++)
        {
            Assert.Equal(run.ImpulseEpochs[k], arcs[k][^1].Epoch);
            Assert.Equal(run.ImpulseEpochs[k], arcs[k + 1][0].Epoch);
        }

        Assert.True(run.ImpulseEpochs.Length >= 1, "no impulse executed");
        Assert.True(m.FarReads > 0, "no far read");
        // Far from the impulses, the reads are those of the former sort of every stored state, to the bit
        Assert.Equal(0, m.FarDiffering);
        // At the states inside the arcs, the read is the stored state, the solution's rotated to ICRF by the rotation of the
        // same doubles; at an impulse, the state after it; on either side of it, the same position: measured 0 in every case
        Assert.Equal(0.0, m.AtOutputs);
        Assert.Equal(0.0, m.VelocityAtManaged);
        Assert.Equal(0.0, m.BoundaryPositions);
        // The Velocity-Verlet path of the scenario stores the states of its twin, to the bit
        Assert.Equal(0.0, m.TwinStates);

        bool chain = run.Maneuver == "Chain4";
        bool moon = run.ReadCenter.NaifId == 301;
        bool rk = run.Integrator == "RK78";
        bool sixty = run.Step.TotalSeconds >= 60.0;
        if (run.Ssb)
        {
            if (run.Integrator != "VVd") Assert.True(m.NearManaged <= NearSsb, $"near {m.NearManaged:E3} m");
        }
        else if (moon)
        {
            if (run.Integrator != "VVd") Assert.True(m.NearManaged <= (rk ? NearRkMoon : NearVvMoon), $"near {m.NearManaged:E3} m");
        }
        else if (rk)
        {
            double limit = chain ? sixty ? NearRkChain60 : NearRkChain : sixty ? NearRk60 : NearRk;
            Assert.True(m.NearManaged <= limit, $"near {m.NearManaged:E3} m");
        }
        else if (run.Integrator == "VV")
        {
            Assert.True(m.NearManaged <= NearVv, $"near {m.NearManaged:E3} m");
        }

        if (run.Integrator == "VVd")
        {
            Assert.True(m.NearManaged <= NearOverFarVvd * m.FarManaged, $"near {m.NearManaged:E3} m, far {m.FarManaged:E3} m");
        }

        double spkLimit = run.Ssb ? SpkAgainstManagedSsb : moon ? SpkAgainstManagedMoon : chain ? SpkAgainstManagedChain : SpkAgainstManaged;
        Assert.True(m.SpkAgainstManaged <= spkLimit, $"SPK - managed {m.SpkAgainstManaged:E3} m");
        Assert.True(m.VelocityBeforeManaged <= (rk && !run.Ssb ? VelocityBeforeRk : VelocityBefore), $"v before {m.VelocityBeforeManaged:E3} m/s");
        Assert.True(m.VelocityBeforeSpk <= VelocityBeforeSpk, $"SPK v before {m.VelocityBeforeSpk:E3} m/s");
        Assert.True(m.VelocityAtSpk <= VelocityAtSpk, $"SPK v at the impulse {m.VelocityAtSpk:E3} m/s");
        Assert.True(m.JumpAgainstDeltaV <= JumpAgainstDeltaV, $"jump - dV {m.JumpAgainstDeltaV:E3} m/s");
    }

    /// <summary>The RK7(8) solution against Kepler's closed form, in the two-body cases (the Earth as a point mass, the kernel
    /// GM): CSPICE's <c>prop2b</c> from the initial state over the TT seconds between the epochs, then from the closed-form
    /// state at each impulse plus the delta-V of its maneuver; at every sample of <see cref="Measure"/>. The reference of the
    /// grid resolves what it measures. PROVISIONAL, Linux worst <see cref="SolutionAgainstProp2b"/>.</summary>
    [Theory]
    [InlineData("Apogee")]
    [InlineData("Perigee")]
    [InlineData("Plane")]
    [InlineData("Combined")]
    public void Rk78Solution_MatchesProp2b(string maneuver)
    {
        string key = $"{maneuver}|RK78|10|Propagate|ICRF|Earth";
        using var run = ArcRun.Run(-(5400 + Array.IndexOf(AllCases, key)), key, writeKernel: false);
        double gm = run.ReadCenter.GM;
        var initial = run.Spacecraft.InitialOrbitalParameters.ToStateVector();
        // The closed-form state at the start and just after each impulse
        var bases = new List<StateVector> { initial };
        for (int k = 0; k < run.ImpulseEpochs.Length; k++)
        {
            var before = TwoBody(bases[^1], gm, PropagationTimeArgument.Tt.SecondsBetween(bases[^1].Epoch, run.ImpulseEpochs[k]));
            bases.Add(new StateVector(before.Position, before.Velocity + run.DeltaVInIcrf(k), before.Observer, run.ImpulseEpochs[k], Frame.ICRF));
        }

        double worst = 0.0, worstVelocity = 0.0, worstBeforeFirst = 0.0;
        int samples = 0;
        void Compare(Time t)
        {
            if (t < run.Window.StartDate || t > run.Window.EndDate) return;
            int k = run.ImpulseEpochs.Count(x => x <= t);
            var expected = TwoBody(bases[k], gm, PropagationTimeArgument.Tt.SecondsBetween(bases[k].Epoch, t));
            var (position, velocity) = run.StateAt(t);
            worst = System.Math.Max(worst, (position - expected.Position).Magnitude());
            if (k == 0) worstBeforeFirst = System.Math.Max(worstBeforeFirst, (position - expected.Position).Magnitude());
            worstVelocity = System.Math.Max(worstVelocity, (velocity - expected.Velocity).Magnitude());
            samples++;
        }

        foreach (var tc in run.ImpulseEpochs)
        {
            for (int i = -600; i <= 600; i++) Compare(tc.AddSeconds(i * 0.25));
            foreach (var d in new[] { -1e-6, 1e-6 }) Compare(tc.AddSeconds(d));
        }

        for (double s = 3.5; s < (run.Window.EndDate - run.Window.StartDate).TotalSeconds; s += 7.0) Compare(run.Window.StartDate.AddSeconds(s));
        _output.WriteLine($"MEASURE #540 RK7(8) solution - prop2b {maneuver}: {worst:E2} m, {worstVelocity:E2} m/s over {samples} samples ({worstBeforeFirst:E2} m before the first impulse), {run.ImpulseEpochs.Length} impulses");
        Assert.True(worst <= SolutionAgainstProp2b, $"solution - prop2b {worst:E3} m");
        Assert.True(worstVelocity <= SolutionVelocityAgainstProp2b, $"solution - prop2b {worstVelocity:E3} m/s");
    }

    // prop2b over the whole ticks of the elapsed time, then the sub-tick remainder along the velocity
    private static StateVector TwoBody(StateVector initial, double gm, double elapsed)
    {
        long ticks = (long)System.Math.Round(elapsed * TimeSpan.TicksPerSecond);
        double remainder = elapsed - ticks / (double)TimeSpan.TicksPerSecond;
        var state = SpiceAPI.Instance.Propagate2Bodies(initial, gm, TimeSpan.FromTicks(ticks));
        return new StateVector(state.Position + state.Velocity * remainder, state.Velocity, state.Observer, state.Epoch, state.Frame);
    }

    /// <summary>
    /// The SPK the library writes, against CSPICE: its layout read by <c>dafus</c> (one type-9 segment per arc, in time order,
    /// the body, the centre and J2000, the first and last epochs of the arc, its states, epochs, directory, degree
    /// min(7, N - 1) and N by <c>dafgda</c>), and the file CSPICE's own <c>spkopn</c>/<c>spkw09</c>/<c>spkcls</c> write from the
    /// same segments, byte for byte. In the Validation category: the native library exports CSPICE on Linux.
    /// </summary>
    [Theory]
    [Trait("Category", "Validation")]
    [InlineData("Apogee|RK78|10|Propagate|ICRF|Earth")]
    [InlineData("Combined|VVd|10|Propagate|ICRF|Earth")]
    [InlineData("CombinedLate|RK78|60|Propagate|ICRF|Earth")]
    [InlineData("Chain4|RK78|10|Propagate|ICRF|Earth")]
    [InlineData("Apogee|VV|10|Propagate|ICRF|SSB")]
    [InlineData("Apogee|RK78|10|Propagate|ICRF|Moon")]
    public void Spk_LayoutByDafus_AndTheFileOfSpkw09(string key)
    {
        using var run = ArcRun.Run(-(5400 + Array.IndexOf(AllCases, key)), key);
        var arcs = run.Spacecraft.PropagatedArcs;
        var segments = CspiceDirect.ReadSegments(run.SpkFile.FullName);
        Assert.Equal(arcs.Count, segments.Count);
        var reference = new List<(int Center, int Degree, double[] Epochs, double[] States, string Name)>();
        for (int k = 0; k < arcs.Count; k++)
        {
            var arc = arcs[k];
            int n = arc.Length;
            var epochs = arc.Select(x => x.Epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds).ToArray();
            var states = arc.SelectMany(x => new[] { x.Position.X * 1e-3, x.Position.Y * 1e-3, x.Position.Z * 1e-3, x.Velocity.X * 1e-3, x.Velocity.Y * 1e-3, x.Velocity.Z * 1e-3 })
                .ToArray();
            int degree = System.Math.Min(7, n - 1);
            var segment = segments[k];
            Assert.Equal(run.Spacecraft.NaifId, segment.Body);
            Assert.Equal(arc[0].Observer.NaifId, segment.Center);
            Assert.Equal(1, segment.Frame);
            Assert.Equal(9, segment.Type);
            Assert.Equal(epochs[0], segment.Start);
            Assert.Equal(epochs[^1], segment.End);
            // The data of a type-9 segment: the states, the epochs, every hundredth epoch, the degree and N
            Assert.Equal(7 * n + (n - 1) / 100 + 2, segment.Data.Length);
            Assert.Equal(states, segment.Data.Take(6 * n));
            Assert.Equal(epochs, segment.Data.Skip(6 * n).Take(n));
            Assert.Equal(degree, segment.Data[^2]);
            Assert.Equal(n, segment.Data[^1]);
            if (k > 0) Assert.Equal(segments[k - 1].End, segment.Start);
            reference.Add((arc[0].Observer.NaifId, degree, epochs, states, $"Seg{k + 1}"));
        }

        var spkw09 = new FileInfo(Path.ChangeExtension(run.SpkFile.FullName, ".spkw09.bsp"));
        if (spkw09.Exists) spkw09.Delete();
        CspiceDirect.WriteType9(spkw09.FullName, run.Spacecraft.NaifId, reference);
        var written = File.ReadAllBytes(run.SpkFile.FullName);
        var expected = File.ReadAllBytes(spkw09.FullName);
        spkw09.Delete();
        var differing = Enumerable.Range(0, System.Math.Min(written.Length, expected.Length)).Where(i => written[i] != expected[i]).ToArray();
        _output.WriteLine($"MEASURE #540 writer against spkw09 {key.Replace('|', ' ')}: {segments.Count} segments, {written.Length} and {expected.Length} bytes, " +
                          $"{differing.Length} bytes differ{(differing.Length > 0 ? $" (first at {differing[0]})" : "")}");
        Assert.Equal(expected.Length, written.Length);
        Assert.Empty(differing);
    }

    /// <summary>What a case measures.</summary>
    internal sealed record Measures
    {
        public double NearManaged { get; set; }
        public double NearSpk { get; set; }
        public double FarManaged { get; set; }
        public double FarSpk { get; set; }
        public double SpkAgainstManaged { get; set; }
        public double VelocityBeforeManaged { get; set; }
        public double VelocityBeforeSpk { get; set; }
        public double VelocityAtManaged { get; set; }
        public double VelocityAtSpk { get; set; }
        public double JumpAgainstDeltaV { get; set; }
        public double BoundaryPositions { get; set; }
        public double AtOutputs { get; set; }
        public double TwinStates { get; set; }
        public int Impulses { get; set; }
        public int NearReads { get; set; }
        public int FarReads { get; set; }
        public int FarDiffering { get; set; }
    }

    internal static Measures Measure(ArcRun run)
    {
        var m = new Measures { Impulses = run.ImpulseEpochs.Length, TwinStates = run.TwinStates };
        var sc = run.Spacecraft;
        var center = run.ReadCenter;
        var end = run.Window.EndDate;
        // The former read: the Lagrange interpolation of the 8 nearest of every stored state, sorted by distance
        var all = sc.PropagatedStates;
        var stored = (CelestialItem)all[0].Observer;

        void Read(Time t, bool near)
        {
            if (t < run.Window.StartDate || t > end) return;
            var (position, _) = run.StateAt(t);
            var managed = sc.GetEphemeris(t, center, Frame.ICRF, Aberration.None).ToStateVector();
            var spk = run.Kernel.GetGeometricStateRelativeTo(t, center);
            double dm = (managed.Position - position).Magnitude();
            double ds = (spk.Position - position).Magnitude();
            m.SpkAgainstManaged = System.Math.Max(m.SpkAgainstManaged, (spk.Position - managed.Position).Magnitude());
            if (near)
            {
                m.NearManaged = System.Math.Max(m.NearManaged, dm);
                m.NearSpk = System.Math.Max(m.NearSpk, ds);
                m.NearReads++;
            }
            else
            {
                m.FarManaged = System.Math.Max(m.FarManaged, dm);
                m.FarSpk = System.Math.Max(m.FarSpk, ds);
                m.FarReads++;
            }
        }

        foreach (var tc in run.ImpulseEpochs)
        {
            for (int i = -600; i <= 600; i++) Read(tc.AddSeconds(i * 0.25), true);
            foreach (var d in new[] { -1e-3, -1e-6, 1e-6, 1e-3 }) Read(tc.AddSeconds(d), true);

            // The velocity 1 µs before the impulse, from the earlier arc, and at it, from the later one (the state after)
            var before = tc.AddSeconds(-1e-6);
            var (_, vBefore) = run.StateAt(before);
            m.VelocityBeforeManaged = System.Math.Max(m.VelocityBeforeManaged, (sc.GetEphemeris(before, center, Frame.ICRF, Aberration.None).ToStateVector().Velocity - vBefore).Magnitude());
            m.VelocityBeforeSpk = System.Math.Max(m.VelocityBeforeSpk, (run.Kernel.GetGeometricStateRelativeTo(before, center).Velocity - vBefore).Magnitude());
            var (_, vAt) = run.StateAt(tc);
            m.VelocityAtManaged = System.Math.Max(m.VelocityAtManaged, (sc.GetEphemeris(tc, center, Frame.ICRF, Aberration.None).ToStateVector().Velocity - vAt).Magnitude());
            m.VelocityAtSpk = System.Math.Max(m.VelocityAtSpk, (run.Kernel.GetGeometricStateRelativeTo(tc, center).Velocity - vAt).Magnitude());
        }

        double nearSpan = System.Math.Max(150.0, 10.0 * run.Step.TotalSeconds);
        double gridStep = run.Maneuver == "Chain4" ? 37.0 : 7.0;
        double length = (end - run.Window.StartDate).TotalSeconds;
        for (double s = gridStep / 2.0; s < length; s += gridStep)
        {
            var t = run.Window.StartDate.AddSeconds(s);
            if (run.ImpulseEpochs.All(tc => System.Math.Abs((t - tc).TotalSeconds) > nearSpan))
            {
                Read(t, false);
                var former = IO.Astrodynamics.Math.Lagrange.Interpolate(all, t);
                var read = sc.GetGeometricStateRelativeTo(t, stored);
                if (former.Position != read.Position || former.Velocity != read.Velocity) m.FarDiffering++;
            }
        }

        // The states on either side of each impulse: the same position, the velocity jump the delta-V of the maneuver
        var arcs = sc.PropagatedArcs;
        for (int k = 0; k < run.ImpulseEpochs.Length; k++)
        {
            var before = arcs[k][^1];
            var after = arcs[k + 1][0];
            m.BoundaryPositions = System.Math.Max(m.BoundaryPositions, (after.Position - before.Position).Magnitude());
            m.JumpAgainstDeltaV = System.Math.Max(m.JumpAgainstDeltaV, (after.Velocity - before.Velocity - run.DeltaVInIcrf(k)).Magnitude());
        }

        // At the stored states inside the arcs, the managed read is the stored state: the solution there
        foreach (var arc in arcs)
        {
            for (int i = 1; i < arc.Length - 1; i++)
            {
                var (position, _) = run.StateAt(arc[i].Epoch);
                m.AtOutputs = System.Math.Max(m.AtOutputs, (sc.GetEphemeris(arc[i].Epoch, center, Frame.ICRF, Aberration.None).ToStateVector().Position - position).Magnitude());
            }
        }

        return m;
    }

    /// <summary>A propagated spacecraft of the grid, its reference solution, its SPK written by the library and the
    /// kernel-backed spacecraft that reads it.</summary>
    internal sealed class ArcRun : IDisposable
    {
        public string Maneuver { get; private init; }
        public string Integrator { get; private init; }
        public Spacecraft Spacecraft { get; private init; }
        public Spacecraft Kernel { get; private set; }
        public PropagationSolution Solution { get; private init; }
        public Frame InitialFrame { get; private init; }
        public bool Ssb { get; private init; }
        public CelestialBody ReadCenter { get; private init; }
        public Window Window { get; private init; }
        public TimeSpan Step { get; private init; }
        public Time[] ImpulseEpochs { get; private init; }
        public ImpulseManeuver[] Executed { get; private init; }
        public double TwinStates { get; private init; }
        public FileInfo SpkFile { get; private set; }

        private static readonly Time Min = new(DateTime.MinValue, TimeFrame.TDBFrame);

        internal static ArcRun Run(int naifId, string key, bool writeKernel = true)
        {
            var parts = key.Split('|');
            string maneuver = parts[0], integrator = parts[1], path = parts[3], variant = parts[5];
            var step = TimeSpan.FromSeconds(int.Parse(parts[2]));
            var frame = FrameOf(parts[4]);
            bool chain4 = maneuver == "Chain4";
            bool ssb = variant == "SSB";
            bool moonCentred = variant == "Moon";

            Time start;
            Window window;
            CelestialBody center;
            CelestialItem[] bodies;
            StateVector orbit;
            if (chain4)
            {
                // The chain of ScenarioTests.PropagateSpacecraft, its orbit, its bodies and its window
                var initialEpoch = Time.CreateUTC(667915269.18539762).ToTDB();
                start = Time.CreateUTC(668085555.829810).ToTDB();
                window = new Window(start, Time.CreateUTC(668174400.000000).ToTDB());
                center = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, initialEpoch);
                bodies = [new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, initialEpoch), center, new CelestialBody(Stars.Sun, Frame.ICRF, initialEpoch)];
                orbit = new StateVector(new Vector3(5056554.1874925727, 4395595.4942363985, 0.0), new Vector3(-3708.6305608890916, 4266.2914313011433, 6736.8538488755494),
                    center, initialEpoch, Frame.ICRF);
            }
            else
            {
                start = new Time(variant == "2026" ? 2026 : 2024, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
                window = new Window(start, start.AddHours(2.0));
                if (moonCentred)
                {
                    // A lunar orbit from its apolune, 100 to 150 km high: the apolune-raising impulse at perilune, S + 3 540 s
                    center = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, start);
                    orbit = new KeplerianElements(1862400.0, 0.0134, 1.0, 0.3, 0.7, System.Math.PI, center, start, Frame.ICRF).ToStateVector();
                    bodies = [center];
                }
                else
                {
                    // #540's LEO from apogee, 400 x 1 000 km: the apogee-raising impulse at perigee, about S + 2 964 s
                    center = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, start);
                    orbit = new StateVector(new Vector3(-7378137.0, 0.0, 0.0), new Vector3(0.0, -5509.9, -4623.4), center, start, Frame.ICRF);
                    if (variant == "Eclipse")
                    {
                        // The same orbit turned by 91.7° in its plane: its perigee, and so the impulse, at the edge of the Earth's
                        // shadow (a cylinder of the equatorial radius, the Sun of spkpos at S + 2 963 s)
                        orbit = TurnedInItsPlane(orbit, 91.7 * IO.Astrodynamics.Constants.Deg2Rad);
                    }
                    bodies = ssb ? [center, new CelestialBody(Stars.Sun, Frame.ICRF, start)] : [center];
                    if (ssb)
                    {
                        var earthFromSsb = center.GetGeometricStateRelativeTo(start, Barycenters.SOLAR_SYSTEM_BARYCENTER);
                        orbit = new StateVector(earthFromSsb.Position + orbit.Position, earthFromSsb.Velocity + orbit.Velocity, Barycenters.SOLAR_SYSTEM_BARYCENTER,
                            start, Frame.ICRF);
                    }
                }
            }

            var initial = frame == Frame.ICRF ? orbit : orbit.ToFrame(frame).ToStateVector();

            (Spacecraft, ImpulseManeuver[]) Build(int id, string name)
            {
                Spacecraft spacecraft;
                Engine engine;
                if (chain4)
                {
                    spacecraft = new Spacecraft(id, name, 1000.0, 10000.0, new Clock(name + "_CLK", 65536), initial);
                    var tank = new FuelTank("ft1", "model1", "sn1", 9000.0, 9000.0);
                    engine = new Engine("engine1", "model1", "sn1", 450.0, 50.0, tank);
                    spacecraft.AddFuelTank(tank);
                    spacecraft.AddEngine(engine);
                    spacecraft.AddPayload(new Payload("payload1", 50.0, "pay01"));
                }
                else
                {
                    spacecraft = new Spacecraft(id, name, 1000.0, 3000.0, new Clock(name + "_CLK", 65536), initial);
                    var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
                    spacecraft.AddFuelTank(tank);
                    engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
                    spacecraft.AddEngine(engine);
                }

                var chain = Chain(maneuver, engine, center, start);
                for (int i = 0; i + 1 < chain.Length; i++) chain[i].SetNextManeuver(chain[i + 1]);
                spacecraft.SetStandbyManeuver(chain[0]);
                return (spacecraft, chain);
            }

            Integrator NewIntegrator() => integrator switch
            {
                "VV" => new VVIntegrator(TimeSpan.FromSeconds(1.0)),
                "VVd" => new VVIntegrator(step),
                _ => ssb ? new RK78Integrator(AttitudeRecordTests.SsbRk78Tolerance, AttitudeRecordTests.SsbRk78Tolerance) : new RK78Integrator(1e-12, 1e-12)
            };

            string runName = $"PA{-naifId}";
            var (sc, chainOfRun) = Build(naifId, runName);
            PropagationSolution solution;
            ImpulseManeuver[] reference = chainOfRun;
            double twinStates = 0.0;
            if (path == "Scenario")
            {
                var scenario = new Scenario("S" + runName, new IO.Astrodynamics.Mission.Mission("m" + runName), window);
                foreach (var body in bodies) scenario.AddCelestialItem(body);
                scenario.AddSpacecraft(sc);
                scenario.SimulateAsync(false, false, step, NewIntegrator).GetAwaiter().GetResult();
                solution = sc.LastPropagation;
                if (solution == null)
                {
                    // The Velocity-Verlet path keeps no solution: that of a twin propagated by Spacecraft.Propagate with the
                    // scenario's bodies, in its order, whose stored states are compared with the scenario's
                    var (twin, twinChain) = Build(naifId - 100000, runName + "T");
                    solution = twin.Propagate(window, scenario.CelestialBodies, NewIntegrator(), false, false, step);
                    reference = twinChain;
                    var a = sc.PropagatedStates;
                    var b = twin.PropagatedStates;
                    twinStates = a.Length == b.Length
                        ? a.Zip(b, (x, y) => x.Epoch == y.Epoch ? (x.Position - y.Position).Magnitude() + (x.Velocity - y.Velocity).Magnitude() : double.PositiveInfinity).Max()
                        : double.PositiveInfinity;
                }
            }
            else
            {
                solution = sc.Propagate(window, bodies, NewIntegrator(), false, false, step);
            }

            var run = new ArcRun
            {
                Maneuver = maneuver, Integrator = integrator, Spacecraft = sc, Solution = solution, InitialFrame = frame, Ssb = ssb,
                ReadCenter = center, Window = window, Step = step, TwinStates = twinStates,
                ImpulseEpochs = solution.Segments.Skip(1).Select(x => x.BaseEpoch).ToArray(),
                Executed = reference.Where(x => x.ThrustWindow.HasValue).ToArray()
            };
            if (writeKernel)
            {
                run.WriteAndLoadKernel();
            }

            return run;
        }

        // A state turned about the normal of its orbit (Rodrigues)
        private static StateVector TurnedInItsPlane(StateVector state, double angle)
        {
            var n = state.Position.Cross(state.Velocity).Normalize();
            Vector3 Turn(Vector3 x) => x * System.Math.Cos(angle) + n.Cross(x) * System.Math.Sin(angle) + n * (n * x) * (1.0 - System.Math.Cos(angle));
            return new StateVector(Turn(state.Position), Turn(state.Velocity), state.Observer, state.Epoch, state.Frame);
        }

        // The impulses of a case, in chain order
        private static ImpulseManeuver[] Chain(string maneuver, Engine engine, CelestialBody center, Time start)
        {
            switch (maneuver)
            {
                case "Apogee":
                    return [new ApogeeHeightManeuver(center, start, TimeSpan.Zero, center.NaifId == 301 ? 2200000.0 : 8378137.0, engine)];
                case "Perigee":
                    return [new ApogeeHeightManeuver(center, start, TimeSpan.Zero, 8378137.0, engine), new PerigeeHeightManeuver(center, start, TimeSpan.Zero, 7078137.0, engine)];
                case "Plane":
                    return
                    [
                        new PlaneAlignmentManeuver(start, TimeSpan.Zero,
                            new KeplerianElements(7078137.0, 0.0565, 42.0 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, center, start,
                                Frame.ICRF), engine)
                    ];
                case "Combined":
                    return [new CombinedManeuver(center, start.AddSeconds(60.0), TimeSpan.Zero, 7078137.0, 45.0 * IO.Astrodynamics.Constants.Deg2Rad, engine)];
                case "CombinedLate":
                    return [new CombinedManeuver(center, start.AddSeconds(120.0), TimeSpan.Zero, 7078137.0, 45.0 * IO.Astrodynamics.Constants.Deg2Rad, engine)];
                case "Chain4":
                {
                    var target = new StateVector(new Vector3(4390853.7278876612, 5110607.0005866792, 917659.86391987884),
                        new Vector3(-4979.4693432656513, 3033.2639866911495, 6933.1803797017265), center, engine.FuelTank.Spacecraft.InitialOrbitalParameters.Epoch, Frame.ICRF);
                    return
                    [
                        new PlaneAlignmentManeuver(Min, TimeSpan.Zero, target, engine), new ApsidalAlignmentManeuver(Min, TimeSpan.Zero, target, engine),
                        new PhasingManeuver(Min, TimeSpan.Zero, target, 1, engine), new ApogeeHeightManeuver(center, Min, TimeSpan.Zero, 15866666.666666666, engine)
                    ];
                }
                default:
                    throw new ArgumentException(maneuver);
            }
        }

        /// <summary>The SPK, written by the library, then the kernel-backed spacecraft that reads it.</summary>
        private void WriteAndLoadKernel()
        {
            string directory = $"Data/User/PropagatedArcs540";
            Directory.CreateDirectory(directory);
            SpkFile = new FileInfo(Path.Combine(directory, Spacecraft.Name.ToLowerInvariant() + ".bsp"));
            Spacecraft.WriteEphemeris(SpkFile);
            SpiceAPI.Instance.LoadKernels(SpkFile);
            Kernel = new Spacecraft(Spacecraft.NaifId, Spacecraft.Name, Window.StartDate, 1000.0, 3000.0, new Clock(Spacecraft.Name + "K_CLK", 65536));
        }

        /// <summary>The position and the velocity of the spacecraft relative to <see cref="ReadCenter"/> in ICRF: the solution
        /// rotated from its frame by <c>pxform</c>; in SSB mode, where it is relative to the Sun, less the state of the Earth
        /// relative to the Sun of <c>spkezr</c> on the planetary ephemeris.</summary>
        internal (Vector3 Position, Vector3 Velocity) StateAt(Time t)
        {
            var (position, velocity) = Solution.InterpolateAt(t);
            if (InitialFrame != Frame.ICRF)
            {
                var q = SpiceAPI.Instance.TransformFrame(t, InitialFrame, Frame.ICRF).Rotation;
                position = position.Rotate(q);
                velocity = velocity.Rotate(q);
            }

            if (Ssb)
            {
                var earth = SpiceAPI.Instance.ReadEphemeris(t, 10, 399, Frame.ICRF, Aberration.None).ToStateVector();
                position -= earth.Position;
                velocity -= earth.Velocity;
            }

            return (position, velocity);
        }

        /// <summary>The delta-V of the k-th impulse, in the frame of the propagation, rotated to ICRF by <c>pxform</c>.</summary>
        internal Vector3 DeltaVInIcrf(int k)
        {
            var dv = Executed[k].DeltaV;
            return InitialFrame == Frame.ICRF ? dv : dv.Rotate(SpiceAPI.Instance.TransformFrame(ImpulseEpochs[k], InitialFrame, Frame.ICRF).Rotation);
        }

        public void Dispose()
        {
            if (SpkFile != null)
            {
                SpiceAPI.Instance.UnloadKernels(SpkFile);
                SpkFile.Delete();
            }
        }
    }
}
