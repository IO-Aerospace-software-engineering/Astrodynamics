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
/// the solution is relative to the Sun, less the Earth's state of <c>spkezr</c> on the planetary ephemeris; there the
/// solution carries the noise of <c>ThirdBodyPerturbation</c> near the Earth (#576), whose step sequence differs per
/// platform: its figures are MEASURE lines with no assertion (restored by the PR of #576), and the velocity 1 µs before an
/// impulse is measured against the anchored reference, <see cref="ArcRun.AnchoredVelocityBefore"/>. The
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
/// <b>Limits.</b> Twice the worst over Linux x64, Windows and macOS arm64 (CI run 41651463194, 2026-11-03, the MEASURE
/// lines of the 93 cases of the grid), rounded up at the second significant digit; each summary states the three worsts
/// (L / W / M). Where twice a printed worst falls on a rounding boundary, the unprinted digits could put it one step
/// higher: the Linux worst is read at six digits (Linux x64, 2026-11-03, the same cases with only the format of the MEASURE
/// line changed, equal to CI Linux at the printed digits), and a worst of Windows or macOS arm64 takes the step above. The
/// grid takes about 30 s and runs in every CI job; <see cref="Spk_LayoutByDafus_AndTheFileOfSpkw09"/>, exact, is in the
/// Validation category (Linux). The velocity before an impulse in SSB mode against the anchored reference is measured by
/// CI run 41689602272, on the same three platforms. Each limit bounds the family of its worst, named in its summary; a
/// family is never bounded by the limit of a larger one (#515).
/// </para>
/// </remarks>
public class PropagatedArcsTests
{
    /// <summary>The managed reads within ±150 s of an impulse against the RK7(8) solution, at 1 and 10 s outputs, one or two
    /// impulses about the Earth: the far error of the Lagrange interpolation, no trace of the impulse. Worst 1.14e-8 /
    /// 1.14e-8 / 1.65e-8 m (a timed impulse 0.1 s before an output, 10 s; CI run 41651463194; twice 1.65e-8 falls on 3.3e-8, the
    /// step above for macOS arm64's unprinted digits); main 23.5 to 1 420 m. Since #569 the combined maneuver fires at its
    /// apogee instead of its minimum epoch: worst 1.21e-8 / 1.21e-8 / 1.65e-8 m (Combined at 1 s outputs on Linux and Windows,
    /// the timed impulse on macOS arm64; CI run 41852583362), the limit unchanged.</summary>
    internal const double NearRk = 3.4e-8;

    /// <summary>The same on the chain of four of <c>ScenarioTests</c> (Earth, Moon and Sun, 24.7 h). Worst 2.25e-8 / 2.25e-8
    /// / 2.32e-8 m (10 s; CI run 41651463194); main 763 m.</summary>
    internal const double NearRkChain = 4.7e-8;

    /// <summary>The same at 60 s outputs: the per-arc interpolation is one-sided at the ends of an arc. Worst 3.33e-4 m, the
    /// same on the three platforms (a timed impulse 1 s before an output; 2.97e-4 m on Apogee and Perigee; CI run 41651463194); main
    /// 2 120 m to 16.1 km
    /// (Apogee 2 250 m, Plane 8 670 m, Combined 11.3 and 16.1 km, the chain 2 120 m).</summary>
    internal const double NearRk60 = 6.7e-4;

    /// <summary>The same on the chain of four at 60 s outputs, whose arcs end 7 min apart. Worst 1.81e-2 m, the same on the
    /// three platforms (both paths; CI run 41651463194); main 2 120 m.</summary>
    internal const double NearRkChain60 = 3.7e-2;

    /// <summary>The managed reads near an impulse against the Velocity-Verlet solution at 1 s steps (the cubic Hermite of
    /// its steps), at 1, 10 and 60 s outputs: the event label of Velocity-Verlet, not carried to the tick grid, puts the
    /// states at the impulse up to 1e-7 s from their label (§9 item 4 of #554, its own issue), 7.5e-4 m at most.
    /// Worst 5.94e-4 m, the same on the three platforms (Perigee, 1, 10 and 60 s; CI run 41651463194); main 18.5 to 1 290 m.</summary>
    internal const double NearVv = 1.2e-3;

    /// <summary>Velocity-Verlet at the output step: the managed reads near an impulse over the reads far from it, both
    /// against its cubic Hermite, which the Lagrange of the outputs does not match (0.16 m at 10 s, 33 m at 60 s): no
    /// trace of the impulse, about the Earth and the Moon (in SSB mode a MEASURE line, #576). Worst 1.07 (the printed near
    /// over the printed far), the same on the three platforms (Apogee, 60 s; CI run 41651463194); main 1 200 (#540's case, 192 m
    /// against 0.16 m).</summary>
    internal const double NearOverFarVvd = 2.2;

    /// <summary>The managed reads near an impulse about the Moon, RK7(8). Worst 1.26e-9 / 1.26e-9 / 1.51e-9 m
    /// (CI run 41651463194).</summary>
    internal const double NearRkMoon = 3.1e-9;

    /// <summary>The same with Velocity-Verlet at 1 s. Worst 2.11e-5 m, the same on the three platforms (CI run 41651463194).</summary>
    internal const double NearVvMoon = 4.3e-5;

    /// <summary><c>spkezr</c> on the SPK the library writes against the managed reads, over every sample, about the Earth with
    /// one or two impulses: the resolution of the ET doubles of the nodes (1.2e-7 s) times the speed. Worst 1.16e-3 m, the
    /// same on the three platforms (a timed impulse 1 ms before an output, RK7(8), 60 s; CI run 41651463194).</summary>
    internal const double SpkAgainstManaged = 2.4e-3;

    /// <summary>The same on the chain of four. Worst 1.08e-3 m, the same on the three platforms (Velocity-Verlet at 60 s;
    /// CI run 41651463194; 1.85e-3 m in RK7(8) at 60 s before the review of PR 1 dropped the output within a tenth of the step of an
    /// impulse there).</summary>
    internal const double SpkAgainstManagedChain = 2.2e-3;

    /// <summary>The same in SSB mode (the SPK relative to the Sun), the library against CSPICE on the same stored states.
    /// Worst 1.71e-3 / 1.12e-3 / 1.12e-3 m (RK7(8) on Linux, Velocity-Verlet at 1 s on Windows and macOS arm64, whose RK7(8)
    /// step sequences differ; CI run 41651463194).</summary>
    internal const double SpkAgainstManagedSsb = 3.5e-3;

    /// <summary>The same about the Moon. Worst 7.97e-5 m, the same on the three platforms (CI run 41651463194).</summary>
    internal const double SpkAgainstManagedMoon = 1.6e-4;

    /// <summary>The managed velocity 1 µs before an impulse against the RK7(8) solution there (the state before it).
    /// Worst 1.25e-11 m/s, the same on the three platforms (the chain of four, 60 s; CI run 41651463194; twice the printed worst
    /// falls on 2.5e-11: 1.254476e-11 m/s on Linux at six digits); main 40 to 890 m/s.</summary>
    internal const double VelocityBeforeRk = 2.6e-11;

    /// <summary>The same against the solution of Velocity-Verlet, about the Earth. Worst 8.40e-7 m/s, the same on the three
    /// platforms (the chain of four, Velocity-Verlet at 10 s; CI run 41651463194).</summary>
    internal const double VelocityBefore = 1.7e-6;

    /// <summary>The managed velocity 1 µs before an impulse in SSB mode, RK7(8), against the anchored reference
    /// (<see cref="AnchoredVelocityBefore"/>: the state before the impulse moved back by 1 µs with the closed-form
    /// acceleration of the Sun and the Earth), not against the solution: in SSB mode <c>InterpolateAt</c> carries the noise
    /// of <c>ThirdBodyPerturbation</c> near the Earth (#576), 3.29e-8 / 3.83e-7 / 1.98e-6 m/s on Linux / Windows / macOS
    /// arm64 (CI run 41651463194), a figure kept in the MEASURE line with no assertion. Worst 4.90e-12 / 3.75e-12 / 4.17e-12 m/s
    /// on Linux / Windows / macOS arm64 (Apogee; CI run 41689602272; 4.897779e-12 at six digits on Linux). The
    /// reference's own floor is a few 1e-12 m/s, the size of this figure: 2.6e-12 to 9.1e-12 m/s over 26 step sequences on
    /// Linux x64.</summary>
    internal const double VelocityBeforeSsbRk = 9.8e-12;

    /// <summary>The same in SSB mode, Velocity-Verlet, against the anchored reference. Worst 1.02e-10 m/s, the same on the
    /// three platforms (Apogee, at the output step; 3.75e-12 m/s at 1 s; CI run 41689602272; 1.019446e-10 at six
    /// digits).</summary>
    internal const double VelocityBeforeSsbVv = 2.1e-10;

    /// <summary>The same about the Moon, RK7(8). Worst 1.25e-12 / 1.25e-12 / 5.82e-13 m/s (CI run 41651463194; twice the printed
    /// worst falls on 2.5e-12: 1.246674e-12 m/s on Linux at six digits, the step above for Windows' unprinted
    /// digits).</summary>
    internal const double VelocityBeforeMoonRk = 2.6e-12;

    /// <summary>The same about the Moon, Velocity-Verlet. Worst 9.71e-12 m/s, the same on the three platforms (at the output
    /// step; 2.27e-13 m/s at 1 s; CI run 41651463194).</summary>
    internal const double VelocityBeforeMoonVv = 2.0e-11;

    /// <summary>The velocity of <c>spkezr</c> on the SPK 1 µs before an impulse against the solution, about the Earth.
    /// Worst 8.54e-7 m/s, the same on the three platforms (Combined, Velocity-Verlet with 60 s steps; CI run 41852583362):
    /// since #569 the combined maneuver fires at its apogee instead of its minimum epoch (7.95e-7 m/s before, Perigee, CI run
    /// 41651463194).</summary>
    internal const double VelocityBeforeSpk = 1.8e-6;

    /// <summary>The same in SSB mode, against the anchored reference (<see cref="AnchoredVelocityBefore"/>): the resolution
    /// of the ET doubles of the read and of the node at tc (1.2e-7 s) times the acceleration, 8.67 m/s². Against the
    /// solution, kept in the MEASURE line with no assertion (#576), 5.99e-7 / 7.85e-7 / 1.58e-6 m/s on Linux / Windows /
    /// macOS arm64 (CI run 41651463194). Worst 6.32e-7 / 4.02e-7 / 4.02e-7 m/s on Linux / Windows / macOS
    /// arm64 (Apogee, RK7(8); 4.02e-7 m/s with Velocity-Verlet on the three; CI run 41689602272): one of the two values
    /// the ET doubles allow, 4.017e-7 and 6.319e-7 m/s, with the step sequence of each platform.</summary>
    internal const double VelocityBeforeSpkSsb = 1.3e-6;

    /// <summary>The same about the Moon. Worst 6.73e-8 m/s, the same on the three platforms (Apogee, RK7(8) and
    /// Velocity-Verlet; CI run 41651463194).</summary>
    internal const double VelocityBeforeSpkMoon = 1.4e-7;

    /// <summary>The velocity of <c>spkezr</c> at an impulse against the state after it, the later segment. Worst 2.92e-12 m/s,
    /// the same on the three platforms (Apogee, Velocity-Verlet at 10 s, SSB mode; CI run 41651463194).</summary>
    internal const double VelocityAtSpk = 5.9e-12;

    /// <summary>The velocity of the state after an impulse less that of the state before against the delta-V of the maneuver
    /// (the library's <c>ImpulseManeuver.DeltaV</c>: a consistency check of the arcs with the maneuver, not an independent
    /// reference) rotated to ICRF by <c>pxform</c>: the rounding of the sum and of the rotation. Worst 2.38e-12 m/s, the same
    /// on the three platforms (Apogee, Velocity-Verlet at 1 s, FK4; CI run 41651463194).</summary>
    internal const double JumpAgainstDeltaV = 4.8e-12;

    /// <summary>The RK7(8) solution against <c>prop2b</c> in the two-body cases, m and m/s: 1.1e-6 m before the first
    /// impulse; after it, where the closed form restarts at the label of the impulse, 1.1e-3 m, 1.5e-7 s of the motion,
    /// the order of the resolution of the TT seconds between two labels (ET doubles, 1.2e-7 s at 7.6e8 s; not traced
    /// further). Worst 1.14e-3 m and 1.33e-6 m/s, the same on the three platforms (Plane; CI run 41651463194).</summary>
    internal const double SolutionAgainstProp2b = 2.3e-3;

    internal const double SolutionVelocityAgainstProp2b = 2.7e-6;

    private readonly ITestOutputHelper _output;

    public PropagatedArcsTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    /// <summary>
    /// #540's LEO 90 s before its apogee, by CSPICE's <c>prop2b</c> back from the apogee, labelled at
    /// <paramref name="start"/>: a combined maneuver open from the start fires at the apogee, S + 90 s, within the first two
    /// outputs at 60 s, the short arc of the <c>CombinedLate</c> cases (choice 13 of the design of #569; before #569 they
    /// were gated at S + 120 s, where r·v &lt; 0, and fired there on the false crossing of the guard).
    /// </summary>
    internal static StateVector LeoBeforeApogee(CelestialItem earth, Time start)
    {
        var apogee = new StateVector(new Vector3(-7378137.0, 0.0, 0.0), new Vector3(0.0, -5509.9, -4623.4), earth, start, Frame.ICRF);
        var before = SpiceAPI.Instance.Propagate2Bodies(apogee, earth.GM, TimeSpan.FromSeconds(-90.0));
        return new StateVector(before.Position, before.Velocity, earth, start, Frame.ICRF);
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

        // An impulse within the first two outputs: a short arc, filled (an ungated combined maneuver from 90 s before its
        // apogee, choice 13 of the design of #569)
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

        // An output near the impulse (review of PR 1 of #554, ARC-1): a timed impulse of 20 m/s 2.2 µs, 3 µs, 1 ms, 0.1 s
        // and 1 s before or after the output at S + 1 980 s, RK7(8), 10 and 60 s outputs; closer than a tenth of the step the
        // output is dropped
        foreach (var step in new[] { 10, 60 })
        foreach (var offset in new[] { "-0.0000022", "+0.0000022", "-0.000003", "+0.000003", "-0.001", "+0.001", "-0.1", "+0.1", "-1", "+1" })
            cases.Add($"Timed{offset}|RK78|{step}|Propagate|ICRF|Earth");

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
                          $"{m.NearReads} near and {m.FarReads} far reads, {m.FarDiffering} far reads differing from the former sort" +
                          (run.Ssb ? $", v at tc - 1 us against the anchored reference managed {m.VelocityBeforeManagedAnchored:E2} SPK {m.VelocityBeforeSpkAnchored:E2} m/s" : ""));

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
        // No stored output closer to an impulse than a tenth of the output step (review of PR 1 of #554, ARC-1); the output
        // near a timed impulse is stored beyond it
        var stored = run.Spacecraft.PropagatedStates;
        foreach (var tc in run.ImpulseEpochs)
        {
            Assert.DoesNotContain(stored, x => x.Epoch != tc && System.Math.Abs((x.Epoch - tc).TotalSeconds) < PropagatedEphemeris.DropFraction * run.Step.TotalSeconds
                                               && run.Solution.StateVectors.Any(o => o.Epoch == x.Epoch));
        }

        if (run.Maneuver.StartsWith("Timed"))
        {
            var output = run.Window.StartDate.AddSeconds(1980.0);
            double distance = System.Math.Abs((output - run.ImpulseEpochs[0]).TotalSeconds);
            // At a tenth of the step (1 s at 10 s outputs) the root of RK7(8) falls on either side of the threshold
            if (System.Math.Abs(distance - PropagatedEphemeris.DropFraction * run.Step.TotalSeconds) > 1e-6)
                Assert.Equal(distance >= PropagatedEphemeris.DropFraction * run.Step.TotalSeconds, stored.Any(x => x.Epoch == output));
        }

        bool chain = run.Maneuver == "Chain4";
        bool moon = run.ReadCenter.NaifId == 301;
        bool rk = run.Integrator == "RK78";
        bool sixty = run.Step.TotalSeconds >= 60.0;
        if (run.Ssb)
        {
            // In SSB mode the solution's dense output carries the noise of ThirdBodyPerturbation near the Earth (#576), and the
            // RK7(8) step sequence, so the noise, differs per platform: the near and far figures against the solution (2.1e-4
            // to 3.4e-4 m near, CI run 41651463194) are MEASURE lines with no assertion, restored by the PR of #576. What is
            // asserted compares the library with itself or with CSPICE on the same stored states, and the velocity before the
            // impulse against the anchored reference
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

        if (run.Integrator == "VVd" && !run.Ssb)
        {
            Assert.True(m.NearManaged <= NearOverFarVvd * m.FarManaged, $"near {m.NearManaged:E3} m, far {m.FarManaged:E3} m");
        }

        double spkLimit = run.Ssb ? SpkAgainstManagedSsb : moon ? SpkAgainstManagedMoon : chain ? SpkAgainstManagedChain : SpkAgainstManaged;
        Assert.True(m.SpkAgainstManaged <= spkLimit, $"SPK - managed {m.SpkAgainstManaged:E3} m");
        if (run.Ssb)
        {
            // Against the anchored reference; the figures against the solution stay in the MEASURE line (#576)
            Assert.True(m.VelocityBeforeManagedAnchored <= (rk ? VelocityBeforeSsbRk : VelocityBeforeSsbVv), $"v before - anchored {m.VelocityBeforeManagedAnchored:E3} m/s");
            Assert.True(m.VelocityBeforeSpkAnchored <= VelocityBeforeSpkSsb, $"SPK v before - anchored {m.VelocityBeforeSpkAnchored:E3} m/s");
        }
        else
        {
            double velocityBefore = moon ? rk ? VelocityBeforeMoonRk : VelocityBeforeMoonVv : rk ? VelocityBeforeRk : VelocityBefore;
            Assert.True(m.VelocityBeforeManaged <= velocityBefore, $"v before {m.VelocityBeforeManaged:E3} m/s");
            Assert.True(m.VelocityBeforeSpk <= (moon ? VelocityBeforeSpkMoon : VelocityBeforeSpk), $"SPK v before {m.VelocityBeforeSpk:E3} m/s");
        }

        Assert.True(m.VelocityAtSpk <= VelocityAtSpk, $"SPK v at the impulse {m.VelocityAtSpk:E3} m/s");
        Assert.True(m.JumpAgainstDeltaV <= JumpAgainstDeltaV, $"jump - dV {m.JumpAgainstDeltaV:E3} m/s");
    }

    /// <summary>The RK7(8) solution against Kepler's closed form, in the two-body cases (the Earth as a point mass, the kernel
    /// GM): CSPICE's <c>prop2b</c> from the initial state over the TT seconds between the epochs, then from the closed-form
    /// state at each impulse plus the delta-V of its maneuver; at every sample of <see cref="Measure"/>. The reference of the
    /// grid resolves what it measures. Limits <see cref="SolutionAgainstProp2b"/> and
    /// <see cref="SolutionVelocityAgainstProp2b"/>.</summary>
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
        public double VelocityBeforeManagedAnchored { get; set; }
        public double VelocityBeforeSpkAnchored { get; set; }
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
            if (run.Ssb)
            {
                var anchored = run.AnchoredVelocityBefore(Array.IndexOf(run.ImpulseEpochs, tc), before);
                m.VelocityBeforeManagedAnchored = System.Math.Max(m.VelocityBeforeManagedAnchored,
                    (sc.GetEphemeris(before, center, Frame.ICRF, Aberration.None).ToStateVector().Velocity - anchored).Magnitude());
                m.VelocityBeforeSpkAnchored = System.Math.Max(m.VelocityBeforeSpkAnchored, (run.Kernel.GetGeometricStateRelativeTo(before, center).Velocity - anchored).Magnitude());
            }

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
                    orbit = maneuver == "CombinedLate"
                        ? LeoBeforeApogee(center, start)
                        : new StateVector(new Vector3(-7378137.0, 0.0, 0.0), new Vector3(0.0, -5509.9, -4623.4), center, start, Frame.ICRF);
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
                    return [new CombinedManeuver(center, start, TimeSpan.Zero, 7078137.0, 45.0 * IO.Astrodynamics.Constants.Deg2Rad, engine)];
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
                case not null when maneuver.StartsWith("Timed"):
                    return [new TimedImpulse(center, start, engine, start.AddSeconds(1980.0 + double.Parse(maneuver[5..], System.Globalization.CultureInfo.InvariantCulture)), 20.0)];
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

        /// <summary>
        /// The velocity relative to the Earth in ICRF at <paramref name="t"/>, just before the k-th impulse, in SSB mode, by the
        /// anchored reference (decision of S. Guillet, 2026-11-03, for #568): the state before the impulse
        /// (<c>PropagatedArcs[k][^1]</c>, the integrator's state at the event, tied to the solution by the velocity at tc,
        /// 0, and the jump against the delta-V) moved by dt = SecondsBetween(base, t) - SecondsBetween(base, tc) with the
        /// closed-form acceleration of the model, the Sun and the Earth as point masses with the kernel GMs,
        /// a = -GM_S r/|r|³ - GM_E (ρ/|ρ|³ + d/|d|³), ρ = r - d, d the Earth from the Sun at tc by <c>spkezr</c>; then less the
        /// Earth from the Sun by <c>spkezr</c> at <paramref name="t"/>, as <see cref="StateAt"/>. Its floor is a few 1e-12 m/s,
        /// the size of what it measures: dt, the difference of two TotalSeconds near 2 963 s whose ulp is 4.55e-13 s, misses
        /// -1 µs by up to one ulp, 3.9e-12 m/s at 8.67 m/s² (1.0e-12 or 2.9e-12 m/s by the tick counts of the epochs);
        /// before.Velocity + a dt rounds at the ulp of the heliocentric velocity, 3.64e-12 m/s (1.8e-12 m/s per component); the
        /// state before the impulse is the integrator's at the event, one ulp of time (4.5e-13 s, 3.9e-12 m/s) from its label
        /// tc, an offset the managed read of the same state shares. The acceleration's own error, the rounding of ρ (3e-5 m,
        /// 8e-11 m/s²) and the jerk over 1 µs, is below 1e-13 m/s. Managed against it: 2.6e-12 to 9.1e-12 m/s over 26 RK7(8)
        /// step sequences on Linux x64 (2026-11-03). The solution's own <c>InterpolateAt</c> carries there the noise of
        /// <c>ThirdBodyPerturbation</c> near the Earth (#576), up to 6.5e-6 m/s over those step sequences.
        /// </summary>
        internal Vector3 AnchoredVelocityBefore(int k, Time t)
        {
            var before = Spacecraft.PropagatedArcs[k][^1];
            var tc = ImpulseEpochs[k];
            Assert.Equal(tc, before.Epoch);
            Assert.Equal(Stars.Sun.NaifId, before.Observer.NaifId);
            // The result is in the frame of the stored states, compared with managed reads in ICRF: every SSB row is in ICRF
            Assert.Equal(Frame.ICRF, before.Frame);
            var baseEpoch = Solution.Segments[k].BaseEpoch;
            double dt = Solution.TimeArgument.SecondsBetween(baseEpoch, t) - Solution.TimeArgument.SecondsBetween(baseEpoch, tc);
            double gmSun = new CelestialBody(Stars.Sun, Frame.ICRF, tc).GM;
            double gmEarth = ReadCenter.GM;
            var d = SpiceAPI.Instance.ReadEphemeris(tc, Stars.Sun.NaifId, ReadCenter.NaifId, before.Frame, Aberration.None).ToStateVector().Position;
            var r = before.Position;
            var rho = r - d;
            double rm = r.Magnitude(), pm = rho.Magnitude(), dm = d.Magnitude();
            var a = r * (-gmSun / (rm * rm * rm)) - (rho * (1.0 / (pm * pm * pm)) + d * (1.0 / (dm * dm * dm))) * gmEarth;
            var velocity = before.Velocity + a * dt;
            var earth = SpiceAPI.Instance.ReadEphemeris(t, Stars.Sun.NaifId, ReadCenter.NaifId, before.Frame, Aberration.None).ToStateVector();
            return velocity - earth.Velocity;
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

/// <summary>An impulse at a set epoch: the event t - target, from positive to negative, the delta-V along the velocity.</summary>
internal sealed class TimedImpulse : ImpulseManeuver
{
    private readonly Time _target;
    private readonly double _deltaV;

    internal TimedImpulse(CelestialItem center, Time minimumEpoch, Engine engine, Time target, double deltaV) : base(center, minimumEpoch, TimeSpan.Zero, engine)
    {
        _target = target;
        _deltaV = deltaV;
    }

    public override double ComputeEventValue(StateVector localState) => (_target - localState.Epoch).TotalSeconds;

    public override IO.Astrodynamics.Propagator.Events.CrossingDirection EventCrossingDirection => IO.Astrodynamics.Propagator.Events.CrossingDirection.PositiveToNegative;

    protected override Vector3 Execute(StateVector vector) => vector.Velocity.Normalize() * _deltaV;
}
