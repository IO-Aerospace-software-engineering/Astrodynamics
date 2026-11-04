// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
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
/// An impulse gated by its minimum epoch fires at the first crossing of its g-function in its direction at or after it,
/// searched from the integrated state there (#569, PR 2 of #554): every impulse maneuver, both integrators, RK7(8)
/// adaptive and fixed-step, covariance mode with and without the step control, <c>Spacecraft.Propagate</c> and
/// <c>Scenario.SimulateAsync</c>, ICRF and ECLIPJ2000, SSB mode, the Moon.
/// </summary>
/// <remarks>
/// <para>
/// <b>The case</b> (#569's): a 400 × 1 000 km orbit (a = 7 078 137 m, e = 0.042384034, i = 0.9 rad, Ω = 0.3 rad,
/// ω = 1.1 rad) from its apogee at S = 2024-03-01 12:00 TDB, about a point-mass Earth with the kernel GM; 1 000 kg dry and
/// 1 000 kg of fuel, Isp 300 s, 10 kg/s. A minimum epoch "m" is the TDB label whose TT is S + m.
/// </para>
/// <para>
/// <b>The reference.</b> The Keplerian closed form written in the test (<see cref="KeplerClosedForm"/>), from the initial
/// state, or from the state after the previous burn (the first state of the next arc of the stored states, the
/// integrator's): the first apsis, node or intersection at or after the minimum epoch, in TT seconds from S (TDB about the
/// Moon), and the delta-V of each maneuver by its own formula. The burn epochs are the ends of the arcs of the stored
/// states, the segment bases, never the thrust window. No value of the library before #569 is a reference: main is not in
/// the library, and a value recorded from it would be a snapshot.
/// </para>
/// <para>
/// <b>RK7(8)</b> fires within <see cref="Rk78Epoch"/> of the closed form, the 100 ns quantization of the epochs included,
/// with its delta-V within <see cref="Rk78DeltaV"/>, per family. <b>Velocity-Verlet</b> fires at the end of the step that
/// holds its own crossing, the root of the g-function on its cubic Hermite, which lags the closed form. Every row with a
/// solution is asserted on its own g-function: the first crossing in the maneuver's direction at or after the minimum
/// epoch (its cubic Hermite there, the state the gate evaluates, then its step ends) is at the step end of the burn. Where
/// the minimum epoch is at least 10 times that lag from the crossing, the row is also asserted on the closed form: the
/// burn less its own crossing in [0, h), the lag within <see cref="VelocityVerletLagLimit"/> of its family, and the burn
/// less the closed form in [−limit, h + limit) (after a Velocity-Verlet burn, the next crossing is that of
/// Velocity-Verlet's orbit, and can fall within the lag before a step end). Closer, the closed form does not decide the
/// side Velocity-Verlet's trajectory is on, and the row is asserted on its own g-function only. A row without a solution
/// (<c>Scenario</c>) is asserted on the closed form only, the burn less the closed form in [−limit, h + limit).
/// <b>SSB mode</b> is checked for the rule only, on the solution's own g-function: the closed form omits the Sun.
/// </para>
/// <para>
/// <b>Limits.</b> Twice the worst over Linux x64, Windows and macOS arm64 (CI run 41852583362, the MEASURE lines of this
/// class), rounded up at the second significant digit; where twice a printed worst falls on a rounding boundary and the
/// worst occurs on Windows or macOS arm64, the step above. The worsts are the same on the three platforms at the printed
/// digits unless stated. Each limit bounds its family (#515).
/// </para>
/// </remarks>
public class MinimumEpochGateTests
{
    /// <summary>
    /// RK7(8), the burn epoch less the closed form, s, per family: <c>apsis</c> (the apsides of the maneuvers, of the chains,
    /// of the user impulse in either direction; adaptive, fixed-step at 60 s, covariance mode with and without the step
    /// control, both paths, ICRF and ECLIPJ2000; F5, the two gated detectors, <c>EvaluateAvoidance</c>'s impulse), worst
    /// 9.86e-8 s (the second burn of F5, fixed-step at 60 s; 9.22e-8 s on the grid, an apogee raise 3 s before the
    /// perigee); <c>node</c>, worst 9.22e-8 s (the plane gated 10 ms before its node); <c>intersection</c> (the apsidal
    /// alignment), worst 1.42e-7 s; <c>time</c> (the time-like user impulse, whose root is a tick), 0; <c>moon</c> (the
    /// apsides about the Moon), worst 2.46e-9 s. Each includes the 100 ns quantization of the epochs.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, double> Rk78Epoch = new Dictionary<string, double>
    {
        ["apsis"] = 2.0e-7, ["node"] = 1.9e-7, ["intersection"] = 2.9e-7, ["time"] = 0.0, ["moon"] = 5.0e-9
    };

    /// <summary>
    /// RK7(8), the delta-V less the closed form at the closed-form epoch, m/s, per family: worst 6.96e-9 (apsis, the
    /// phasing 60 s before the perigee), 2.54e-11 (node), 7.06e-7 (intersection), 1.78e-15 (time), 3.62e-11 (moon).
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, double> Rk78DeltaV = new Dictionary<string, double>
    {
        ["apsis"] = 1.4e-8, ["node"] = 5.1e-11, ["intersection"] = 1.5e-6, ["time"] = 3.6e-15, ["moon"] = 7.3e-11
    };

    /// <summary>
    /// Velocity-Verlet, its own crossing (the root of the g-function on its cubic Hermite) less the closed form, s, per
    /// family and step: apsis 1.75e-3 / 1.76e-1 s at 1 / 10 s (the second burn of the chain with a hold of 4 000 s, on the
    /// orbit after Velocity-Verlet's first burn); node 2.19e-3 / 9.53e-2 s; intersection −1.44e-2 / −1.44 s (the g-function
    /// of the apsidal alignment swings fast there); time −2.27e-13 / −2.27e-13 s (a time-like g has no dynamics: its root is
    /// Velocity-Verlet's crossing, found to the resolution of t in double, an ulp at 1 100 s); moon 2.57e-4 / 2.55e-2 s. The
    /// reference root is bisected to the resolution of t in double, below every lag it bounds. Twice 1.75e-3 (apsis, VV1) and
    /// 2.55e-2 s (moon, VV10) fall on a rounding boundary, on the three platforms: the step above.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, double> VelocityVerletLagLimit = new Dictionary<string, double>
    {
        ["apsis|VV1"] = 3.6e-3, ["apsis|VV10"] = 3.6e-1, ["node|VV1"] = 4.4e-3, ["node|VV10"] = 2.0e-1, ["intersection|VV1"] = 2.9e-2,
        ["intersection|VV10"] = 2.9, ["time|VV1"] = 4.6e-13, ["time|VV10"] = 4.6e-13, ["moon|VV1"] = 5.2e-4, ["moon|VV10"] = 5.2e-2
    };

    internal static readonly Time S = new(2024, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
    internal const double Apogee0 = 7378137.0;
    internal const double Perigee0 = 6778137.0;
    internal const double A0 = (Apogee0 + Perigee0) / 2.0;
    internal const double E0 = (Apogee0 - Perigee0) / (Apogee0 + Perigee0);
    internal const double MoonRadius = 1737400.0;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private readonly ITestOutputHelper _output;

    public MinimumEpochGateTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    /// <summary>The TDB label whose TT is S + <paramref name="tt"/>, to the tick.</summary>
    internal static Time GateAtTt(double tt) => PropagationTimeArgument.Tt.EpochAt(S, tt);

    /// <summary>The TT seconds from S.</summary>
    internal static double Tt(Time t) => PropagationTimeArgument.Tt.SecondsBetween(S, t);

    // ---------------------------------------------------------------------------------------------------------------------
    // The grid: a key is group/name|integrator|path|frame
    // ---------------------------------------------------------------------------------------------------------------------

    internal static readonly string[] AllCases = BuildCases();

    private static string[] BuildCases()
    {
        var keys = new List<string>();
        string[] integrators = ["VV1", "VV10", "RK", "RKf60"];
        void Add(string name, IEnumerable<string> integs, string path = "Propagate", string frame = "ICRF")
        {
            foreach (var i in integs) keys.Add($"{name}|{i}|{path}|{frame}");
        }

        // Face A: a positive-to-negative maneuver gated where r·v < 0
        foreach (var m in new[] { "m1000", "m2000", "m2900" }) Add("perigee/" + m, integrators);
        foreach (var m in new[] { "m1000", "m2000", "m2900" }) Add("combined/" + m, integrators);

        // Controls: the decision of the guard holds (r·v > 0 at the gate, a true crossing in the gated step; a gate far
        // from a negative-to-positive crossing)
        foreach (var m in new[] { "m3500", "apo-0.5", "apo-3", "apo-30", "apo-120" }) Add("perigee/" + m, integrators);
        Add("combined/apo-3", integrators);
        Add("apogee/m1000", integrators);

        // Face B: a negative-to-positive maneuver gated just before its crossing
        foreach (var x in new[] { "0.5", "3", "8", "30", "60" }) Add("apogee/peri-" + x, integrators);
        foreach (var x in new[] { "0.5", "3", "30", "60" }) Add("phasing/peri-" + x, integrators);
        foreach (var x in new[] { "0.5", "3", "30" }) Add("apsidal/int-" + x, integrators);

        // Face B': the plane alignment gated before a node, and near it (choice 7)
        foreach (var x in new[] { "-60", "-30", "-3", "-0.5" }) Add("plane/node" + x, integrators);
        foreach (var x in new[] { "-0.01", "-0.001", "+0", "+0.001", "+0.01", "+0.03" }) Add("plane/node" + x, ["VV1", "VV10", "RK"]);

        // Chains: the gate at the end of the previous window
        foreach (var h in new[] { "0", "600", "4000" }) Add("chain-apo-peri/hold" + h, integrators);
        foreach (var x in new[] { "3", "30", "300" }) Add("chain-peri-apo/x" + x, integrators);

        // Edges
        foreach (var e in new[] { "before-base", "at-base", "at-perigee", "perigee-1us", "perigee+1us", "perigee+1ms" })
            Add("edge-apogee/" + e, integrators);
        foreach (var e in new[] { "step-start", "step-start+1tick", "prev-step-end-1tick", "step-end" }) Add("edge-step/" + e, integrators);

        // g exactly zero at a gate equal to the base: the Keplerian apogee (r·v = 0.0), and one tick after it
        Add("zero/at-base", integrators);
        Add("zero/base+1tick", integrators);

        // User impulses: a time-like g and an apsis in either direction
        foreach (var c in new[] { "timed/root-before-gate", "timed/root-after-gate", "timed/root-in-gated-step", "any/m1000", "any/peri-3", "any/m3500" })
            Add("custom/" + c, integrators);

        // Paths and frames
        string[] paths = ["perigee/m1000", "apogee/peri-3", "apogee/peri-30", "chain-apo-peri/hold4000"];
        foreach (var p in paths) Add(p, ["VV1", "VV10", "RK"], "Scenario");
        foreach (var p in paths.Concat(["plane/node-3", "plane/node-30"])) Add(p, ["VV1", "VV10", "RK"], frame: "ECLIPJ2000");
        foreach (var p in paths.Concat(["combined/m1000", "plane/node-3", "apsidal/int-3", "phasing/peri-3"]))
        {
            Add(p, ["RK", "RKf60"], "Cov");
            Add(p, ["RK"], "CovStep");
        }

        // The other activation paths of the covariance propagation, on face A and face B: the builder, the Scenario
        // overload, the batch task
        foreach (var p in new[] { "perigee/m1000", "apogee/peri-3" })
        {
            foreach (var path in new[] { "CovBuilder", "CovScenario", "CovTask" }) Add(p, ["RK", "RKf60"], path);
            foreach (var path in new[] { "CovBuilderStep", "CovScenarioStep", "CovTaskStep" }) Add(p, ["RK"], path);
        }

        foreach (var p in paths) Add(p, ["VV1", "VV10", "RK"], frame: "SSB");

        // The Moon (TDB time argument)
        foreach (var m in new[] { "moon-perigee/m1000", "moon-perigee/apo-3", "moon-apogee/m1000", "moon-apogee/peri-3", "moon-apogee/peri-30" })
            Add(m, ["VV1", "VV10", "RK"]);
        return keys.ToArray();
    }

    public static IEnumerable<object[]> Grid() => AllCases.Select(x => new object[] { x });

    [Theory]
    [MemberData(nameof(Grid))]
    public void Burn_IsTheFirstCrossingAtOrAfterTheMinimumEpoch(string key)
    {
        int index = Array.IndexOf(AllCases, key);
        var run = GateRun.Run(-(7000 + index), key);
        var expected = run.Case.Expected(run);
        var parts = new List<string>();
        Assert.Equal(expected.Count, run.Burns.Count);

        for (int k = 0; k < expected.Count; k++)
        {
            var (tc, dv, family, period) = expected[k];
            var burn = run.Burns[k];
            double error = run.Seconds(burn.Epoch) - tc;
            double dvError = burn.DeltaV - dv;
            parts.Add($"burn {k} - closed form {error:E2} s, dV - closed form {dvError:E2} m/s ({family})");

            if (run.Ssb)
            {
                // SSB mode: the rule only, on the solution's own g-function (the closed form omits the Sun)
                if (run.Solution != null) parts.Add(AssertRuleOnOwnG(run, k));
                continue;
            }

            if (run.IsVelocityVerlet)
            {
                // Velocity-Verlet fires at the first step end at or after its own crossing, which lags the closed form; a
                // gate closer to the closed-form crossing than 10 times that lag is decided on its own g-function only
                double h = run.Step;
                double lagLimit = VelocityVerletLagLimit[$"{family}|{run.Integrator}"];
                string ownG = run.Solution != null ? AssertRuleOnOwnG(run, k) : "no solution (Scenario)";
                var (lag, crossing) = run.Solution != null ? VelocityVerletLag(run, k, tc, period) : (double.NaN, tc);
                double separation = System.Math.Abs(run.Seconds(run.Gate(k)) - crossing);
                bool withinLag = run.Solution != null && (double.IsNaN(lag) || separation < 10.0 * System.Math.Abs(lag));
                parts.Add($"VV own g: {ownG}, VV lag {lag:E2} s{(withinLag ? ", gate within 10 lags: its own g only" : "")}");
                if (!withinLag)
                {
                    if (!double.IsNaN(lag))
                    {
                        Assert.True(System.Math.Abs(lag) <= lagLimit, $"VV lag {lag:E3} s");
                        Assert.Equal(tc, crossing);
                        Assert.InRange(error - lag, 0.0, h);
                        Assert.True(error - lag < h, $"burn {k}: {error - lag:R} s after its own crossing, a step of {h} s");
                    }

                    Assert.InRange(error, -lagLimit, h + lagLimit);
                }
            }
            else
            {
                Assert.True(System.Math.Abs(error) <= Rk78Epoch[family], $"burn {k}: {error:E3} s from the closed form");
                Assert.True(System.Math.Abs(dvError) <= Rk78DeltaV[family], $"burn {k}: dV {dvError:E3} m/s");
            }
        }

        _output.WriteLine($"MEASURE #569 gate {key.Replace('|', ' ')}: {run.Burns.Count} burns; {string.Join("; ", parts)}");
        run.Case.Check?.Invoke(run);
    }

    /// <summary>
    /// PR 3's case F5 (#554): an attitude between two impulses moves the minimum epoch of the second by its hold. The second
    /// burn is the same across the holds, byte for byte, and at the apogee of the orbit after the first.
    /// </summary>
    [Theory]
    [InlineData("VV1")]
    [InlineData("VV10")]
    [InlineData("RK")]
    [InlineData("RKf60")]
    public void AttitudeBetweenTwoImpulses_TheSecondBurnIsTheSameForEveryHold(string integrator)
    {
        // A hold of 0 in RK7(8) is #554's case M (the attitude drops the gate of the impulse before it, §10 item 3 of the
        // design of #569, PR 3's): the gate of the second impulse is the first burn itself
        var holds = integrator.StartsWith("VV") ? new[] { "0", "60", "65.1", "66", "600" } : new[] { "60", "65.1", "66", "600" };
        var runs = holds.Select((h, i) =>
            GateRun.Run(-(7900 + 10 * Array.IndexOf(new[] { "VV1", "VV10", "RK", "RKf60" }, integrator) + i), $"f5/hold{h}|{integrator}|Propagate|ICRF")).ToArray();
        var first = runs[0];
        var expected = first.Case.Expected(first);
        double error0 = first.Seconds(first.Burns[0].Epoch) - expected[0].Tc;
        double dvError0 = first.Burns[0].DeltaV - expected[0].DeltaV;
        double error = first.Seconds(first.Burns[1].Epoch) - expected[1].Tc;
        double dvError = first.Burns[1].DeltaV - expected[1].DeltaV;
        foreach (var run in runs)
        {
            Assert.Equal(2, run.Burns.Count);
            Assert.Equal(first.Burns[1].Epoch, run.Burns[1].Epoch);
            Assert.Equal(first.Burns[1].DeltaV, run.Burns[1].DeltaV);
            Assert.Equal(first.Spacecraft.PropagatedStates[^1].Position, run.Spacecraft.PropagatedStates[^1].Position);
        }

        _output.WriteLine($"MEASURE #569 gate F5 {integrator}: first burn - closed form {error0:E2} s, dV - closed form {dvError0:E2} m/s; second burn - closed form {error:E2} s, dV {first.Burns[1].DeltaV:F4} m/s (closed form {expected[1].DeltaV:F4}), dV - closed form {dvError:E2} m/s; the same for the holds {string.Join(", ", holds)} s");
        if (first.IsVelocityVerlet)
        {
            // The first burn on the initial orbit, within Velocity-Verlet's lag of its step end; the second at the step end
            // after the apogee of the orbit after the first
            double lagLimit = VelocityVerletLagLimit[$"apsis|{integrator}"];
            Assert.InRange(error0, -lagLimit, first.Step + lagLimit);
            Assert.InRange(error, 0.0, first.Step);
        }
        else
        {
            Assert.True(System.Math.Abs(error0) <= Rk78Epoch["apsis"], $"first burn {error0:E3} s");
            Assert.True(System.Math.Abs(dvError0) <= Rk78DeltaV["apsis"], $"first burn dV {dvError0:E3} m/s");
            Assert.True(System.Math.Abs(error) <= Rk78Epoch["apsis"], $"{error:E3} s");
            Assert.True(System.Math.Abs(dvError) <= Rk78DeltaV["apsis"], $"dV {dvError:E3} m/s");
        }
    }

    // The burn k is the first crossing in the maneuver's direction at or after its minimum epoch on the solution's own
    // g-function: in Velocity-Verlet, the g at the gate on its cubic Hermite, then the step ends of the segment that ends
    // at the burn; in RK7(8) (SSB mode), the g at the gate and every 10 s after it, the crossing in the interval of the
    // burn. A zero at the gate is a crossing there when the sign that follows it is the target sign
    private static string AssertRuleOnOwnG(GateRun run, int k)
    {
        var maneuver = run.Executed[k];
        var segment = run.Solution.Segments[run.SegmentOfBurn(k)];
        var gate = run.Gate(k);
        var direction = maneuver.EventCrossingDirection;
        double G(Time epoch, Vector3 position, Vector3 velocity) => maneuver.ComputeEventValue(run.Local(epoch, position, velocity));
        double GAt(Time epoch)
        {
            var (p, v) = run.Solution.InterpolateAt(epoch);
            return G(epoch, p, v);
        }

        bool Crossing(double before, double after) => before * after < 0.0 && direction switch
        {
            CrossingDirection.NegativeToPositive => after > 0.0,
            CrossingDirection.PositiveToNegative => after < 0.0,
            _ => true
        };

        bool TargetSign(double value) => direction switch
        {
            CrossingDirection.NegativeToPositive => value > 0.0,
            CrossingDirection.PositiveToNegative => value < 0.0,
            _ => value != 0.0
        };

        var start = gate > segment.BaseEpoch ? gate : segment.BaseEpoch;
        double gGate = GAt(start);
        var burn = run.Burns[k].Epoch;
        if (run.IsVelocityVerlet)
        {
            double tGate = run.Solution.TimeArgument.SecondsBetween(segment.BaseEpoch, start);
            double previous = gGate;
            bool zero = gGate == 0.0 && !(gate < segment.BaseEpoch);
            foreach (var step in segment.Steps)
            {
                double end = step.CumulativeTime + step.StepSize;
                if (end <= tGate) continue;
                var epoch = run.Solution.TimeArgument.EpochAt(segment.BaseEpoch, end);
                double g = G(epoch, step.EndPosition, step.EndVelocity);
                if (Crossing(previous, g) || (zero && TargetSign(g)))
                {
                    Assert.Equal(epoch, burn);
                    return $"g at the gate {gGate:E3}, first crossing at the step end {end:F3}";
                }

                if (g != 0.0) zero = false;
                previous = g;
            }

            Assert.Fail($"no crossing on the Velocity-Verlet steps after the gate, burn at {run.Seconds(burn):R}");
        }

        // RK7(8): sampled every 10 s from the gate; the first sampled crossing brackets the burn
        double tPrevious = 0.0, gPrevious = gGate;
        for (double dt = 10.0; ; dt += 10.0)
        {
            var epoch = start.AddSeconds(dt);
            double g = GAt(epoch < burn ? epoch : burn.AddSeconds(-1e-3));
            if (Crossing(gPrevious, g) || epoch >= burn)
            {
                Assert.True(epoch >= burn, $"a crossing at or after the gate before the burn, between {tPrevious} and {dt} s after it");
                break;
            }

            (tPrevious, gPrevious) = (dt, g);
        }

        double before = GAt(burn.AddSeconds(-1e-3));
        double after = GAt(burn.AddSeconds(1e-3));
        Assert.True(TargetSign(-before) || direction == CrossingDirection.Any, $"g 1 ms before the burn {before:E3}");
        Assert.True(TargetSign(after) || direction == CrossingDirection.Any, $"g 1 ms after the burn {after:E3}");
        Assert.True(gGate == 0.0 || !TargetSign(gGate) || direction == CrossingDirection.Any || burn > gate.AddSeconds(run.Case.MinimumCycle),
            $"g at the gate {gGate:E3}");
        return $"g at the gate {gGate:E3}, 1 ms before the burn {before:E3}, after {after:E3}";
    }

    // Velocity-Verlet's own crossing less the closed form: the root of the g-function of burn k, in its direction, on the
    // cubic Hermite of the steps of the segment that ends at the burn, nearest the closed-form crossing tc or the same
    // crossing one period before or after it, and that closed-form crossing; NaN when none is within a step of them
    private static (double Lag, double ClosedForm) VelocityVerletLag(GateRun run, int k, double tc, double period)
    {
        var maneuver = run.Executed[k];
        var direction = maneuver.EventCrossingDirection;
        var segment = run.Solution.Segments[run.SegmentOfBurn(k)];
        double origin = run.Seconds(segment.BaseEpoch);
        // The crossing near tc first; one period before or after it only when Velocity-Verlet has none near tc
        double best = double.NaN, closedForm = tc;
        bool nearTc = false;
        foreach (var step in segment.Steps)
        {
            double t0 = step.CumulativeTime, t1 = t0 + step.StepSize;
            double G(double t)
            {
                var (p, v) = PropagationSegment.HermiteInterpolate(step, t);
                return maneuver.ComputeEventValue(run.Local(run.Solution.TimeArgument.EpochAt(segment.BaseEpoch, t), p, v));
            }

            foreach (double center in double.IsNaN(period) ? [tc] : new[] { tc - period, tc, tc + period })
            {
                double c = center - origin;
                if (t1 < c - step.StepSize - 1.0 || t0 > c + step.StepSize + 1.0) continue;
                double lo = t0, hi = t1, gLo = G(lo), gHi = G(hi);
                bool crossing = gLo * gHi < 0.0 && direction switch
                {
                    CrossingDirection.NegativeToPositive => gHi > 0.0,
                    CrossingDirection.PositiveToNegative => gHi < 0.0,
                    _ => true
                };
                if (!crossing) continue;
                // To the resolution of t in double, so that the reference resolves better than the lags it bounds
                for (int i = 0; i < 200; i++)
                {
                    double mid = 0.5 * (lo + hi);
                    if (mid <= lo || mid >= hi) break;
                    double gMid = G(mid);
                    if (gLo * gMid <= 0.0) hi = mid;
                    else (lo, gLo) = (mid, gMid);
                }

                double lag = 0.5 * (lo + hi) - c;
                bool atTc = center == tc;
                if (nearTc && !atTc) continue;
                if (double.IsNaN(best) || (atTc && !nearTc) || System.Math.Abs(lag) < System.Math.Abs(best)) (best, closedForm) = (lag, center);
                nearTc |= atTc;
            }
        }

        return (best, closedForm);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // A run: the case, its propagation, its burns
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>One expected burn: its epoch in seconds of the reference, its delta-V (NaN: not compared), its family.</summary>
    internal readonly record struct ExpectedBurn(double Tc, double DeltaV, string Family, double Period = double.NaN);

    internal sealed class GateCase
    {
        public Func<CelestialBody, OrbitalParams> Orbit { get; init; }
        public Func<Engine, CelestialBody, string, ImpulseManeuver[]> Chain { get; init; }
        public Func<GateRun, List<ExpectedBurn>> Expected { get; init; }
        public Action<GateRun> Check { get; init; }
        public double Hours { get; init; } = 4.0;
        public bool Moon { get; init; }

        /// <summary>The time after the gate beyond which a burn may follow a gate whose g already has the target sign: one
        /// crossing in the other direction first.</summary>
        public double MinimumCycle { get; init; } = 1000.0;

        public Attitude Attitude { get; init; }
    }

    internal sealed record Burn(Time Epoch, double DeltaV);

    internal sealed class GateRun
    {
        public string Key { get; private init; }
        public GateCase Case { get; private init; }
        public string Integrator { get; private init; }
        public string Path { get; private init; }
        public string FrameName { get; private init; }
        public bool Ssb => FrameName == "SSB";
        public bool IsVelocityVerlet => Integrator.StartsWith("VV");
        public double Step => Integrator == "VV1" ? 1.0 : Integrator == "VV10" ? 10.0 : double.NaN;
        public CelestialBody Center { get; private init; }
        public Spacecraft Spacecraft { get; private init; }
        public PropagationSolution Solution { get; private init; }
        public ImpulseManeuver[] Chain { get; private init; }
        public ImpulseManeuver[] Executed { get; private init; }
        public List<Burn> Burns { get; private init; }
        public OrbitalParams InitialIcrf { get; private init; }

        /// <summary>The seconds of the reference from S: TT about the Earth, TDB about the Moon.</summary>
        public double Seconds(Time t) => Case.Moon ? (t.ToTDB() - S).TotalSeconds : Tt(t);

        /// <summary>The minimum epoch of the k-th executed impulse, as the library set it.</summary>
        public Time Gate(int k) => Executed[k].MinimumEpoch;

        /// <summary>The index of the segment that ends at burn k.</summary>
        public int SegmentOfBurn(int k)
        {
            var burn = Burns[k].Epoch;
            for (int i = 0; i < Solution.Segments.Count - 1; i++)
                if (Solution.Segments[i + 1].BaseEpoch == burn) return i;
            throw new InvalidOperationException("no segment ends at the burn");
        }

        /// <summary>A state of the propagation relative to the center of the maneuvers, in the frame of the propagation.</summary>
        public StateVector Local(Time epoch, Vector3 position, Vector3 velocity)
        {
            // The solution is relative to its central body (the Sun in SSB mode), in the frame of the initial state
            return new StateVector(position, velocity, Solution.Observer, epoch, Solution.Frame).RelativeTo(Center, Aberration.None).ToStateVector();
        }

        /// <summary>The closed form of the initial orbit, in ICRF, times from S.</summary>
        public KeplerClosedForm Initial()
        {
            var sv = InitialIcrf.ToStateVector();
            return new KeplerClosedForm(Center.GM, sv.Position, sv.Velocity, 0.0);
        }

        /// <summary>The closed form of the orbit after burn k, from the state after it the spacecraft stores, in ICRF.</summary>
        public KeplerClosedForm After(int k)
        {
            var after = Spacecraft.PropagatedArcs[k + 1][0];
            var local = after.RelativeTo(Center, Aberration.None).ToStateVector();
            if (local.Frame != Frame.ICRF) local = local.ToFrame(Frame.ICRF).ToStateVector();
            return new KeplerClosedForm(Center.GM, local.Position, local.Velocity, Seconds(after.Epoch));
        }

        public static GateRun Run(int naifId, string key)
        {
            var parts = key.Split('|');
            string integrator = parts[1], path = parts[2], frameName = parts[3];
            var @case = Cases(parts[0]);
            var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
            var center = @case.Moon ? new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, S) : earth;
            var orbitIcrf = @case.Orbit(center);
            OrbitalParams orbit = orbitIcrf;
            CelestialItem[] bodies = [center];
            if (frameName == "ECLIPJ2000")
            {
                orbit = orbitIcrf.ToStateVector().ToFrame(Frame.ECLIPTIC_J2000).ToStateVector();
            }
            else if (frameName == "SSB")
            {
                var sv = orbitIcrf.ToStateVector();
                var fromSsb = center.GetGeometricStateRelativeTo(S, Barycenters.SOLAR_SYSTEM_BARYCENTER);
                orbit = new StateVector(fromSsb.Position + sv.Position, fromSsb.Velocity + sv.Velocity, Barycenters.SOLAR_SYSTEM_BARYCENTER, S, Frame.ICRF);
                bodies = [center, new CelestialBody(Stars.Sun, Frame.ICRF, S)];
            }

            if (path.StartsWith("Cov"))
            {
                var sv = orbit.ToStateVector();
                var p0 = new Matrix(6, 6);
                for (int i = 0; i < 6; i++) p0.Set(i, i, i < 3 ? 100.0 : 1.0e-4);
                orbit = new StateVector(sv.Position, sv.Velocity, sv.Observer, sv.Epoch, sv.Frame, p0);
            }

            var spacecraft = new Spacecraft(naifId, $"GT{-naifId}", 1000.0, 3000.0, new Clock($"GT{-naifId}_CLK", 65536), orbit);
            var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
            spacecraft.AddFuelTank(tank);
            var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
            spacecraft.AddEngine(engine);
            var chain = @case.Chain(engine, center, integrator);
            for (int i = 0; i + 1 < chain.Length; i++)
                if (chain[i].NextManeuver == null) chain[i].SetNextManeuver(chain[i + 1]);
            spacecraft.SetStandbyManeuver(chain[0]);

            var window = new Window(S, S.AddSeconds(@case.Hours * 3600.0));
            var output = TimeSpan.FromSeconds(10.0);
            Integrator NewIntegrator() => integrator switch
            {
                "VV1" => new VVIntegrator(TimeSpan.FromSeconds(1.0)),
                "VV10" => new VVIntegrator(TimeSpan.FromSeconds(10.0)),
                "RKf60" => new RK78Integrator(60.0),
                _ => frameName == "SSB" ? new RK78Integrator(AttitudeRecordTests.SsbRk78Tolerance, AttitudeRecordTests.SsbRk78Tolerance) : new RK78Integrator(1e-12, 1e-12)
            };

            PropagationSolution solution;
            var covariance = path.StartsWith("Cov")
                ? new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance, IncludeInStepControl = path.EndsWith("Step") }
                : null;
            if (path is "Scenario" or "CovScenario" or "CovScenarioStep")
            {
                var scenario = new Scenario($"SGT{-naifId}", new IO.Astrodynamics.Mission.Mission($"mGT{-naifId}"), window);
                foreach (var body in bodies) scenario.AddCelestialItem(body);
                scenario.AddSpacecraft(spacecraft);
                var simulation = covariance == null
                    ? scenario.SimulateAsync(false, false, output, NewIntegrator)
                    : scenario.SimulateAsync(false, false, output, NewIntegrator, covariance);
                simulation.GetAwaiter().GetResult();
                solution = spacecraft.LastPropagation;
            }
            else if (path.StartsWith("CovBuilder"))
            {
                using var propagator = new CentralBodyPropagatorBuilder(window, spacecraft, NewIntegrator(), output)
                    .WithPerturbingBodies(bodies).WithCovariancePropagation(covariance).Build();
                solution = propagator.Propagate();
            }
            else if (path.StartsWith("CovTask"))
            {
                var task = new PropagationTask(spacecraft, window, bodies, false, false, output, NewIntegrator) { CovariancePropagation = covariance };
                var result = BatchPropagator.Propagate([task]).Results[0];
                Assert.Null(result.Error);
                solution = result.Solution;
            }
            else if (covariance != null)
            {
                solution = spacecraft.Propagate(window, bodies, NewIntegrator(), false, false, output, covariance);
            }
            else
            {
                solution = spacecraft.Propagate(window, bodies, NewIntegrator(), false, false, output);
            }

            var executed = chain.Where(x => x.ThrustWindow.HasValue).ToArray();
            var arcs = spacecraft.PropagatedArcs;
            var burns = new List<Burn>();
            // The burn epochs: the segment bases when the solution is at hand (an impulse at the base leaves no arc before
            // it), the arc boundaries of the stored states otherwise
            for (int k = 0; k < executed.Length; k++)
                burns.Add(new Burn(solution != null ? solution.Segments[k + 1].BaseEpoch : arcs[k][^1].Epoch, executed[k].DeltaV.Magnitude()));
            if (solution != null) Assert.Equal(executed.Length + 1, solution.Segments.Count);

            return new GateRun
            {
                Key = key, Case = @case, Integrator = integrator, Path = path, FrameName = frameName, Center = center, Spacecraft = spacecraft,
                Solution = solution, Chain = chain, Executed = executed, Burns = burns, InitialIcrf = orbitIcrf
            };
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The cases
    // ---------------------------------------------------------------------------------------------------------------------

    internal static KeplerianElements Orbit(CelestialItem center, double m0, double inclination = 0.9, double raan = 0.3, double argp = 1.1) =>
        new(A0, E0, inclination, raan, argp, m0, center, S, Frame.ICRF);

    private static double Value(string text) => double.Parse(text, Invariant);

    private static List<ExpectedBurn> One(double tc, double dv, double period, string family = "apsis") => [new ExpectedBurn(tc, dv, family, period)];

    // The period of #569's orbit, from its closed form
    private static double Period(CelestialItem center, OrbitalParams orbit)
    {
        var sv = orbit.ToStateVector();
        return new KeplerClosedForm(center.GM, sv.Position, sv.Velocity, 0.0).T;
    }

    internal static GateCase Cases(string name)
    {
        var group = name[..name.IndexOf('/')];
        var item = name[(name.IndexOf('/') + 1)..];
        var earthTemplate = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
        double T = Period(earthTemplate, Orbit(earthTemplate, System.Math.PI));
        double T2 = T / 2.0;

        // A minimum epoch in TT seconds from S: m1000, apo-x (T − x), peri-x (T/2 − x)
        double GateSeconds(string spec) => spec.StartsWith("apo-") ? T - Value(spec[4..]) : spec.StartsWith("peri-") ? T2 - Value(spec[5..]) : Value(spec[1..]);

        switch (group)
        {
            case "perigee":
            {
                double m = GateSeconds(item);
                return new GateCase
                {
                    Orbit = c => Orbit(c, System.Math.PI),
                    Chain = (e, c, _) => [new PerigeeHeightManeuver(c, GateAtTt(m), TimeSpan.Zero, 6878137.0, e)],
                    Expected = r =>
                    {
                        var k = r.Initial();
                        return One(k.NextApsis(Tt(GateAtTt(m)), true), k.PerigeeChange(6878137.0), k.T);
                    }
                };
            }
            case "combined":
            {
                // ω = π: the apogee at the ascending node, i 0.5 → 0.45 rad
                double m = GateSeconds(item);
                return new GateCase
                {
                    Orbit = c => Orbit(c, System.Math.PI, 0.5, 0.3, System.Math.PI),
                    Chain = (e, c, _) => [new CombinedManeuver(c, GateAtTt(m), TimeSpan.Zero, 6878137.0, 0.45, e)],
                    Expected = r =>
                    {
                        var k = r.Initial();
                        double va = k.SpeedAt(k.ApogeeRadius);
                        double vf = System.Math.Sqrt(k.Mu * (2.0 / k.ApogeeRadius - 2.0 / (k.ApogeeRadius + 6878137.0)));
                        double dv = System.Math.Sqrt(va * va + vf * vf - 2.0 * va * vf * System.Math.Cos(0.05));
                        return One(k.NextApsis(Tt(GateAtTt(m)), true), dv, k.T);
                    }
                };
            }
            case "apogee":
            {
                double m = GateSeconds(item);
                return new GateCase
                {
                    Orbit = c => Orbit(c, System.Math.PI),
                    Chain = (e, c, _) => [new ApogeeHeightManeuver(c, GateAtTt(m), TimeSpan.Zero, 8378137.0, e)],
                    Expected = r =>
                    {
                        var k = r.Initial();
                        return One(k.NextApsis(Tt(GateAtTt(m)), false), k.ApogeeChange(8378137.0), k.T);
                    }
                };
            }
            case "phasing":
            {
                double m = GateSeconds(item);
                return new GateCase
                {
                    Orbit = c => Orbit(c, System.Math.PI),
                    Chain = (e, c, _) => [new PhasingManeuver(GateAtTt(m), TimeSpan.Zero, PhasingTarget(c), 1, e)],
                    Hours = 3.0,
                    Expected = r =>
                    {
                        var k = r.Initial();
                        double t = k.NextApsis(Tt(GateAtTt(m)), false);
                        return One(t, PhasingDeltaV(k, PhasingTarget(r.Center), t), k.T);
                    }
                };
            }
            case "apsidal":
            {
                return new GateCase
                {
                    Orbit = c => Orbit(c, System.Math.PI),
                    Chain = (e, c, _) =>
                    {
                        var k = ClosedFormOf(c, Orbit(c, System.Math.PI));
                        var (first, second, _) = k.IntersectionsWith(ClosedFormOf(c, ApsidalTarget(c)));
                        double tFirst = System.Math.Min(k.TimeOfTrue(first, 0.0), k.TimeOfTrue(second, 0.0));
                        return [new ApsidalAlignmentManeuver(GateAtTt(tFirst - Value(item[4..])), TimeSpan.Zero, ApsidalTarget(c), e)];
                    },
                    Expected = r =>
                    {
                        var k = r.Initial();
                        var target = ClosedFormOf(r.Center, ApsidalTarget(r.Center));
                        var (first, second, dw) = k.IntersectionsWith(target);
                        double gate = Tt(r.Chain[0].MinimumEpoch);
                        double t1 = k.TimeOfTrue(first, gate), t2 = k.TimeOfTrue(second, gate);
                        double nu = t1 <= t2 ? first : second;
                        var (_, v1) = k.StateAtTrue(nu);
                        var (_, v2) = target.StateAtTrue(nu - dw);
                        return One(System.Math.Min(t1, t2), (v2 - v1).Magnitude(), k.T, "intersection");
                    }
                };
            }
            case "plane":
            {
                // ω = π, M0 = π: the ascending node at S, the descending node at the perigee T/2; the target i + 0.05 rad
                double m = T2 + Value(item[4..]);
                return new GateCase
                {
                    Orbit = c => Orbit(c, System.Math.PI, 0.5, 0.3, System.Math.PI),
                    Chain = (e, c, _) => [new PlaneAlignmentManeuver(GateAtTt(m), TimeSpan.Zero, PlaneTarget(c, e), e)],
                    Expected = r =>
                    {
                        var k = r.Initial();
                        var ht = PlaneNormal(r.Center);
                        var d = k.Hh.Cross(ht).Normalize();
                        double gate = Tt(r.Chain[0].MinimumEpoch);
                        double nu = k.TrueAnomalyOf(d);
                        double t = System.Math.Min(k.TimeOfTrue(nu, gate), k.TimeOfTrue(nu + System.Math.PI, gate));
                        var (rr, v) = k.StateAt(t);
                        double theta = System.Math.Acos(System.Math.Clamp(k.Hh * ht, -1.0, 1.0));
                        var horizontal = v - rr.Normalize() * (v * rr.Normalize());
                        return One(t, 2.0 * horizontal.Magnitude() * System.Math.Sin(theta / 2.0), k.T, "node");
                    },
                    Check = r =>
                    {
                        if (!r.IsVelocityVerlet || r.Ssb) return;
                        // Velocity-Verlet: the node chosen by the search-start rule on its own state at the gate
                        var gate = r.Chain[0].MinimumEpoch;
                        var (p, v) = r.Solution.InterpolateAt(gate);
                        var local = r.Local(gate, p, v);
                        var targetH = ((PlaneAlignmentManeuver)r.Chain[0]).TargetOrbit.SpecificAngularMomentum().Normalize();
                        double g = local.Position * targetH, dgdt = local.Velocity * targetH;
                        bool ascending = System.Math.Abs(g) < 1e-6 * local.Position.Magnitude() ? g * dgdt > 0.0 ? g < 0.0 : dgdt > 0.0 : !(g > 0.0);
                        Assert.Equal(ascending, ((PlaneAlignmentManeuver)r.Chain[0]).IsAscendingNode);
                    }
                };
            }
            case "chain-apo-peri":
            {
                double hold = Value(item[4..]);
                return new GateCase
                {
                    Orbit = c => Orbit(c, System.Math.PI),
                    Hours = 6.0,
                    Chain = (e, c, _) => [new ApogeeHeightManeuver(c, S, TimeSpan.FromSeconds(hold), 8378137.0, e), new PerigeeHeightManeuver(c, S, TimeSpan.Zero, 6878137.0, e)],
                    Expected = r =>
                    {
                        var k = r.Initial();
                        var list = One(k.NextApsis(0.0, false), k.ApogeeChange(8378137.0), k.T);
                        var k2 = r.After(0);
                        list.Add(new ExpectedBurn(k2.NextApsis(Tt(r.Chain[1].MinimumEpoch), true), k2.PerigeeChange(6878137.0), "apsis", k2.T));
                        return list;
                    }
                };
            }
            case "chain-peri-apo":
            {
                // The perigee raised at the apogee; the hold puts the gate of the apogee raise x + 1.24 s before the next
                // perigee (the gate is the thrust start, 0.666 dt before the burn, plus the hold)
                double x = Value(item[1..]);
                return new GateCase
                {
                    Orbit = c => Orbit(c, System.Math.PI),
                    Hours = 6.0,
                    Chain = (e, c, _) =>
                    {
                        double a2 = (Apogee0 + 6878137.0) / 2.0;
                        double t2 = 2.0 * System.Math.PI * System.Math.Sqrt(a2 * a2 * a2 / c.GM);
                        return [new PerigeeHeightManeuver(c, GateAtTt(3500.0), TimeSpan.FromSeconds(t2 / 2.0 - x), 6878137.0, e), new ApogeeHeightManeuver(c, S, TimeSpan.Zero, 8378137.0, e)];
                    },
                    Expected = r =>
                    {
                        var k = r.Initial();
                        var list = One(k.NextApsis(Tt(GateAtTt(3500.0)), true), k.PerigeeChange(6878137.0), k.T);
                        var k2 = r.After(0);
                        list.Add(new ExpectedBurn(k2.NextApsis(Tt(r.Chain[1].MinimumEpoch), false), k2.ApogeeChange(8378137.0), "apsis", k2.T));
                        return list;
                    }
                };
            }
            case "f5":
            {
                double hold = Value(item[4..]);
                return new GateCase
                {
                    Orbit = c => Orbit(c, System.Math.PI),
                    Chain = (e, c, _) =>
                    {
                        var first = new PerigeeHeightManeuver(c, GateAtTt(3500.0), TimeSpan.Zero, 6878137.0, e);
                        var attitude = new NadirAttitude(c, S, TimeSpan.FromSeconds(hold), e);
                        var second = new PerigeeHeightManeuver(c, S, TimeSpan.Zero, 6978137.0, e);
                        first.SetNextManeuver(attitude);
                        attitude.SetNextManeuver(second);
                        return [first, second];
                    },
                    Expected = r =>
                    {
                        var k = r.Initial();
                        var list = One(k.NextApsis(Tt(GateAtTt(3500.0)), true), k.PerigeeChange(6878137.0), k.T);
                        var k2 = r.After(0);
                        list.Add(new ExpectedBurn(k2.NextApsis(Tt(r.Chain[1].MinimumEpoch), true), k2.PerigeeChange(6978137.0), "apsis", k2.T));
                        return list;
                    }
                };
            }
            case "edge-apogee":
            {
                double m = item switch
                {
                    "before-base" => -100.0, "at-base" => 0.0, "at-perigee" => T2, "perigee-1us" => T2 - 1e-6, "perigee+1us" => T2 + 1e-6, "perigee+1ms" => T2 + 1e-3,
                    _ => throw new ArgumentException(item)
                };
                return new GateCase
                {
                    Orbit = c => Orbit(c, System.Math.PI),
                    Chain = (e, c, _) => [new ApogeeHeightManeuver(c, GateAtTt(m), TimeSpan.Zero, 8378137.0, e)],
                    Expected = r =>
                    {
                        var k = r.Initial();
                        return One(k.NextApsis(Tt(GateAtTt(m)), false), k.ApogeeChange(8378137.0), k.T);
                    }
                };
            }
            case "edge-step":
            {
                return new GateCase
                {
                    Orbit = c => Orbit(c, System.Math.PI),
                    Chain = (e, c, integrator) => [new ApogeeHeightManeuver(c, StepEdgeGate(c, integrator, item), TimeSpan.Zero, 8378137.0, e)],
                    Expected = r =>
                    {
                        var k = r.Initial();
                        return One(k.NextApsis(Tt(r.Chain[0].MinimumEpoch), false), k.ApogeeChange(8378137.0), k.T);
                    }
                };
            }
            case "zero":
            {
                // The apogee given by Keplerian elements (M0 = π): r·v = 0.0 at the base
                return new GateCase
                {
                    Orbit = c => Orbit(c, System.Math.PI),
                    Chain = (e, c, _) => [new PerigeeHeightManeuver(c, item == "at-base" ? S : S.Add(TimeSpan.FromTicks(1)), TimeSpan.Zero, 6878137.0, e)],
                    Expected = r =>
                    {
                        var k = r.Initial();
                        double tc = item == "at-base" ? 0.0 : k.NextApsis(Tt(r.Chain[0].MinimumEpoch), true);
                        return One(tc, k.PerigeeChange(6878137.0), k.T);
                    },
                    Check = r =>
                    {
                        var sv = r.InitialIcrf.ToStateVector();
                        Assert.Equal(0.0, sv.Position * sv.Velocity);
                    }
                };
            }
            case "custom":
            {
                if (item.StartsWith("timed/"))
                {
                    var (root, gate) = item switch
                    {
                        "timed/root-before-gate" => (900.0, 1000.0), "timed/root-after-gate" => (1100.5, 1000.0), "timed/root-in-gated-step" => (1000.3, 1000.0),
                        _ => throw new ArgumentException(item)
                    };
                    return new GateCase
                    {
                        Orbit = c => Orbit(c, System.Math.PI),
                        Chain = (e, c, _) => [new TimedImpulse(c, GateAtTt(gate), e, GateAtTt(root), 10.0)],
                        Expected = r => root < gate ? [] : One(Tt(GateAtTt(root)), 10.0, double.NaN, "time")
                    };
                }

                double m = item switch { "any/m1000" => 1000.0, "any/peri-3" => T2 - 3.0, "any/m3500" => 3500.0, _ => throw new ArgumentException(item) };
                return new GateCase
                {
                    Orbit = c => Orbit(c, System.Math.PI),
                    Chain = (e, c, _) => [new AnyApsisImpulse(c, GateAtTt(m), e)],
                    Expected = r =>
                    {
                        var k = r.Initial();
                        double gate = Tt(GateAtTt(m));
                        return One(System.Math.Min(k.NextApsis(gate, true), k.NextApsis(gate, false)), 10.0, k.T / 2.0);
                    }
                };
            }
            case "moon-perigee":
            case "moon-apogee":
            {
                bool perigee = group == "moon-perigee";
                return new GateCase
                {
                    Moon = true,
                    Hours = 5.0,
                    Orbit = c => MoonOrbit(c),
                    Chain = (e, c, _) =>
                    {
                        var k = ClosedFormOf(c, MoonOrbit(c));
                        double m = item.StartsWith("apo-") ? k.T - Value(item[4..]) : item.StartsWith("peri-") ? k.T / 2.0 - Value(item[5..]) : Value(item[1..]);
                        return perigee
                            ? [new PerigeeHeightManeuver(c, S.AddSeconds(m), TimeSpan.Zero, MoonRadius + 150000.0, e)]
                            : [new ApogeeHeightManeuver(c, S.AddSeconds(m), TimeSpan.Zero, MoonRadius + 500000.0, e)];
                    },
                    Expected = r =>
                    {
                        var k = r.Initial();
                        double gate = r.Seconds(r.Chain[0].MinimumEpoch);
                        return perigee
                            ? One(k.NextApsis(gate, true), k.PerigeeChange(MoonRadius + 150000.0), k.T, "moon")
                            : One(k.NextApsis(gate, false), k.ApogeeChange(MoonRadius + 500000.0), k.T, "moon");
                    }
                };
            }
            default:
                throw new ArgumentException(name);
        }
    }

    // A gate on the steps of the propagation without maneuver, the same as the propagation's up to the burn: at the start
    // of the step that holds the perigee, one tick after it, one tick before it, or at its end
    private static Time StepEdgeGate(CelestialBody center, string integrator, string edge)
    {
        var orbit = Orbit(center, System.Math.PI);
        var spacecraft = new Spacecraft(-7999, "GTSTEPS", 1000.0, 3000.0, new Clock("GTSTEPS_CLK", 65536), orbit);
        Integrator propagator = integrator switch
        {
            "VV1" => new VVIntegrator(TimeSpan.FromSeconds(1.0)),
            "VV10" => new VVIntegrator(TimeSpan.FromSeconds(10.0)),
            "RKf60" => new RK78Integrator(60.0),
            _ => new RK78Integrator(1e-12, 1e-12)
        };
        var solution = spacecraft.Propagate(new Window(S, S.AddSeconds(4.0 * 3600.0)), [center], propagator, false, false, TimeSpan.FromSeconds(10.0));
        double perigee = ClosedFormOf(center, orbit).T / 2.0;
        var step = solution.Segments[0].Steps.First(x => x.CumulativeTime + x.StepSize >= perigee);
        var start = PropagationTimeArgument.Tt.EpochAt(S, step.CumulativeTime);
        return edge switch
        {
            "step-start" => start,
            "step-start+1tick" => start.Add(TimeSpan.FromTicks(1)),
            "prev-step-end-1tick" => start.Add(TimeSpan.FromTicks(-1)),
            "step-end" => PropagationTimeArgument.Tt.EpochAt(S, step.CumulativeTime + step.StepSize),
            _ => throw new ArgumentException(edge)
        };
    }

    private static KeplerClosedForm ClosedFormOf(CelestialItem center, OrbitalParams orbit)
    {
        var sv = orbit.ToStateVector();
        return new KeplerClosedForm(center.GM, sv.Position, sv.Velocity, 0.0);
    }

    // 100 × 300 km lunar orbit from its apolune
    private static KeplerianElements MoonOrbit(CelestialItem moon)
    {
        double ra = MoonRadius + 300000.0, rp = MoonRadius + 100000.0;
        return new KeplerianElements((ra + rp) / 2.0, (ra - rp) / (ra + rp), 0.9, 0.3, 1.1, System.Math.PI, moon, S, Frame.ICRF);
    }

    private static KeplerianElements PhasingTarget(CelestialItem center) => new(A0, E0, 0.9, 0.3, 1.1, System.Math.PI + 0.2, center, S, Frame.ICRF);

    private static KeplerianElements ApsidalTarget(CelestialItem center) => new(7178137.0, 0.06, 0.9, 0.3, 1.1 + 0.6, 0.0, center, S, Frame.ICRF);

    private static KeplerianElements PlaneTargetIcrf(CelestialItem center) => new(A0, E0, 0.55, 0.3, System.Math.PI, System.Math.PI, center, S, Frame.ICRF);

    // The target in the frame of the spacecraft, which the maneuver requires
    private static OrbitalParams PlaneTarget(CelestialItem center, Engine engine)
    {
        var target = PlaneTargetIcrf(center);
        var frame = engine.FuelTank.Spacecraft.InitialOrbitalParameters.Frame;
        return frame == Frame.ICRF ? target : target.ToStateVector().ToFrame(frame).ToStateVector();
    }

    private static Vector3 PlaneNormal(CelestialItem center)
    {
        var sv = PlaneTargetIcrf(center).ToStateVector();
        return sv.Position.Cross(sv.Velocity).Normalize();
    }

    // The delta-V of PhasingManeuver's formula (one revolution) on the closed-form states at the perigee
    private static double PhasingDeltaV(KeplerClosedForm k, KeplerianElements target, double t)
    {
        var tsv = target.ToStateVector();
        var tk = new KeplerClosedForm(k.Mu, tsv.Position, tsv.Velocity, 0.0);
        var (r, _) = k.StateAt(t);
        var (rt, _) = tk.StateAt(t);
        double dnu = KeplerClosedForm.Normalized(System.Math.Atan2(rt.Cross(r) * -1.0 * k.Hh, rt * r));
        double e = k.E;
        double ea = 2.0 * System.Math.Atan(System.Math.Sqrt((1.0 - e) / (1.0 + e)) * System.Math.Tan(dnu / 2.0));
        double tt = k.T / (2.0 * System.Math.PI) * (ea - e * System.Math.Sin(ea));
        double a2 = System.Math.Pow(System.Math.Sqrt(k.Mu) * (k.T - tt) / (2.0 * System.Math.PI), 2.0 / 3.0);
        double rp = k.PerigeeRadius;
        double ra = 2.0 * a2 - rp;
        double h2 = System.Math.Sqrt(2.0 * k.Mu) * System.Math.Sqrt(ra * rp / (ra + rp));
        double ht = System.Math.Sqrt(k.Mu * tk.P);
        return System.Math.Abs(h2 / rp - ht / rp);
    }
}

/// <summary>A user impulse at an apsis in either direction: g = r · v, <see cref="CrossingDirection.Any"/>, 10 m/s along the
/// velocity.</summary>
internal sealed class AnyApsisImpulse : ImpulseManeuver
{
    internal AnyApsisImpulse(CelestialItem center, Time minimumEpoch, Engine engine) : base(center, minimumEpoch, TimeSpan.Zero, engine)
    {
    }

    public override double ComputeEventValue(StateVector localState) => localState.Position * localState.Velocity;

    public override CrossingDirection EventCrossingDirection => CrossingDirection.Any;

    protected override Vector3 Execute(StateVector vector) => vector.Velocity.Normalize() * 10.0;
}
