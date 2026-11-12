// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
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
/// The attitude timeline of a propagation (#539, #547, #570; design of #554, §4.1 and §4.2): each attitude law in force
/// from its activation to the next one, an attitude activated at the later of its minimum epoch and the end of the window
/// of the maneuver before it, the impulse's attitude over [tc − 0.666 dt, end of its maneuver window], the law in force
/// before an impulse resumed after it, no activation past the end, the holds and the windows in TT seconds about the
/// Earth. The cases of the design on the grid of both integrators, every activation path, the inertial frames, SSB mode
/// and the Moon.
/// </summary>
/// <remarks>
/// <para>
/// <b>The case</b> (#569's orbit, <see cref="MinimumEpochGateTests"/>): a 400 × 1 000 km orbit from its apogee at
/// S = 2024-03-01 12:00 TDB about a point-mass Earth with the kernel GM (its perigee at S + 2 963 s), 4 h, 10 s outputs;
/// 1 000 kg dry and 1 000 kg of fuel, Isp 300 s, 10 kg/s. The chains (letters of the design): A impulse → LVLH (0);
/// B nadir → impulse → LVLH (3 600 s); C nadir → impulse → LVLH (600 s) → prograde; E1 an impulse alone; E2 nadir →
/// impulse; F1 nadir (600 s) → prograde; F2 a prograde whose minimum epoch is S + 300; F3 nadir (3 600 s) → impulse;
/// F3′ nadir (2 000 s) → impulse; F4 impulse → LVLH (0) → perigee raise; F5 impulse → LVLH (4 000 s) → perigee raise;
/// F5′ the same with an apogee raise; G a nadir over a window of 1 000 s at 60 s outputs; I1 impulse → prograde whose
/// minimum epoch is S + 4 000; I2 impulse (hold 600 s) → LVLH; K impulse → LVLH (5 000 s) → prograde past the end of a
/// 2 h window; L nadir → impulse → perigee raise; M impulse (hold 0 or 600 s) → LVLH (0) → apogee raise to 9 378 137 m.
/// The impulse is an <see cref="ApogeeHeightManeuver"/> to 8 378 137 m at the perigee, the perigee raise a
/// <see cref="PerigeeHeightManeuver"/> to 6 978 137 m at the apogee. #547's eight rows (the activation after a TT hold on
/// an output label) are <c>AttitudeRecordTests.NearEpochChain</c>.
/// </para>
/// <para>
/// <b>The references.</b> The rules of the timeline written in the test (<see cref="TimelineRules"/>): from the epochs of
/// the burns (the bases of the segments of the solution), the burn durations by the rocket equation, the TT seconds by
/// <c>unitim_c</c>, the expected activations, windows and gates; compared with the library's on Linux within
/// <see cref="TimelineRules.RuleBound"/>. The attitude, every second, at each output and at −1 ms, −2.5 µs, +2.5 µs,
/// +1 ms and +0.5 s of each activation, against the closed form of the activation in force on the state of the solution
/// (its <c>InterpolateAt</c>, rotated to ICRF by <c>pxform</c>, relative to the centre): nadir (the front to −r),
/// prograde (the front to v), LVLH (the TRIAD of the down axis to −r and the front to v), the impulse's attitude (the front
/// along ΔV rotated to ICRF by <c>pxform</c>), the identity before any; within 1.5 µs of an activation the side is not
/// resolved and no comparison is made. At the entries of the record (its outputs) within
/// <see cref="AttitudeRecordTests.LawAtEntries"/>, between them within <see cref="AttitudeRecordTests.LawBetweenEntries"/>
/// (the residual of the SLERP of 10 s samples; 60 s outputs within <see cref="LawBetweenEntriesAt60"/>), discrete
/// within <see cref="AttitudeRecordTests.DiscreteLaw"/>. The burns, outside SSB mode, against the Keplerian closed form
/// (<see cref="KeplerClosedForm"/>): the first apsis of the maneuver at or after the gate of the rules, on the orbit before
/// the burn, within #577's bounds; the number of burns too: the first impulse the rules reach without a burn has its
/// closed-form apsis past the end of the window (less those bounds), every impulse of the SSB rows fires.
/// <c>ExecutedManeuvers</c> (#570) against the activations of the rules, the fuel against
/// the rocket equation. The Velocity-Verlet path of <c>Scenario.SimulateAsync</c> keeps no solution: its reference is the
/// solution of a twin propagated by <c>Spacecraft.Propagate</c>, whose stored states equal the scenario's to the bit.
/// </para>
/// <para>
/// <b>Limits.</b> The existing ones, set on Linux, Windows and macOS arm64, never relaxed. <see cref="LawBetweenEntriesAt60"/>
/// is twice the worst on Linux x64, the only platform of the grid, which is in the Validation category (CI run
/// 42021804663, equal to the session run); <see cref="TimelineRules.RuleBound"/>, from the three platforms. The rows of
/// <see cref="Worst"/> run in every CI job.
/// </para>
/// </remarks>
public class AttitudeTimelineTests
{
    private static readonly Time S = MinimumEpochGateTests.S;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private const double MoonRadius = MinimumEpochGateTests.MoonRadius;

    /// <summary>The SLERP residual between the samples of a law at 60 s outputs, rad, a property of the step (it grows as
    /// its square: 4.1e-5 rad at 10 s, 3.1e-7 rad at 1 s). Twice the Linux worst, 1.49e-3 rad (E2, nadir,
    /// RK7(8) and Velocity-Verlet alike; CI run 42021804663), rounded up at two digits: the grid runs on Linux only (Validation).</summary>
    internal const double LawBetweenEntriesAt60 = 3.0e-3;

    /// <summary>Within this of an activation of the rules, the side of the activation is not resolved: the bound of the rules
    /// and the 1 µs lead before a discrete change (<see cref="SpacecraftFrame.DiscreteChangeLead"/>).</summary>
    private const double Unresolved = 1.5e-6;

    private readonly ITestOutputHelper _output;

    public AttitudeTimelineTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The grid
    // ---------------------------------------------------------------------------------------------------------------------

    private static readonly string[] Letters = ["A", "B", "C", "E1", "E2", "F1", "F2", "F3", "F3p", "F4", "F5", "F5p", "G", "I1", "I2", "K", "L", "M0", "M600"];

    private static readonly string[] Frames = ["ICRF", "ECLIPJ2000", "B1950", "FK4", "ECLIPB1950", "GALACTIC"];

    private static readonly string[] WorstRows = ["B|RK|Propagate|ICRF|10", "C|VV1|Propagate|ICRF|10", "M0|RK|Propagate|ICRF|10", "F3|VV1|Propagate|ICRF|10"];

    private static string[] BuildGrid()
    {
        var rows = new List<string>();
        foreach (var letter in Letters)
        foreach (var integrator in new[] { "VV1", "RK" })
            rows.Add($"{letter}|{integrator}|Propagate|ICRF|{(letter == "G" ? 60 : 10)}");

        // Outputs at 1 and 60 s; the inertial frames; SSB mode; fixed-step RK7(8), whose gates sit in its first step
        foreach (var letter in new[] { "A", "B", "E2" })
        foreach (var integrator in new[] { "VV1", "RK" })
        {
            rows.Add($"{letter}|{integrator}|Propagate|ICRF|1");
            rows.Add($"{letter}|{integrator}|Propagate|ICRF|60");
            foreach (var frame in Frames.Skip(1)) rows.Add($"{letter}|{integrator}|Propagate|{frame}|10");
            rows.Add($"{letter}|{integrator}|Propagate|SSB|10");
        }

        foreach (var letter in new[] { "B", "E2", "F3p", "L", "M0" }) rows.Add($"{letter}|RKf60|Propagate|ICRF|10");

        // About the Moon (TDB seconds): A, B and C
        foreach (var letter in new[] { "A", "B", "C" })
        foreach (var integrator in new[] { "VV1", "RK" })
            rows.Add($"{letter}|{integrator}|Propagate|Moon|10");

        // The activation paths, with a leading hold and an attitude after an impulse (B, C)
        foreach (var letter in new[] { "B", "C" })
        {
            rows.Add($"{letter}|VV1|Default|ICRF|1");
            rows.Add($"{letter}|VV1|Async|ICRF|1");
            rows.Add($"{letter}|RK|PropagateCov|ICRF|10");
            rows.Add($"{letter}|RK|PropagateCovStep|ICRF|10");
            rows.Add($"{letter}|VV1|Scenario|ICRF|10");
            rows.Add($"{letter}|RK|Scenario|ICRF|10");
            rows.Add($"{letter}|VV1|ScenarioDefault|ICRF|1");
            rows.Add($"{letter}|RK|ScenarioCov|ICRF|10");
            rows.Add($"{letter}|RK|ScenarioCovStep|ICRF|10");
            rows.Add($"{letter}|VV1|Batch|ICRF|10");
            rows.Add($"{letter}|RK|Batch|ICRF|10");
            rows.Add($"{letter}|RK|BatchCov|ICRF|10");
            rows.Add($"{letter}|RK|BatchCovStep|ICRF|10");
            rows.Add($"{letter}|VV1|Builder|ICRF|10");
            rows.Add($"{letter}|RK|Builder|ICRF|10");
            rows.Add($"{letter}|VV1|Direct|ICRF|10");
            rows.Add($"{letter}|RK|Direct|ICRF|10");
        }

        // F3 and M through the covariance mode: P follows the burns that move
        rows.Add("F3|RK|PropagateCov|ICRF|10");
        rows.Add("M0|RK|BatchCov|ICRF|10");
        return rows.Distinct().ToArray();
    }

    private static readonly string[] AllRows = BuildGrid();

    public static IEnumerable<object[]> Grid() => AllRows.Where(x => !WorstRows.Contains(x)).Select(x => new object[] { x });

    public static IEnumerable<object[]> Worst() => WorstRows.Select(x => new object[] { x });

    [Theory]
    [MemberData(nameof(Worst))]
    public void WorstRow_FollowsTheRules(string key) => Check(key);

    [Theory]
    [Trait("Category", "Validation")]
    [MemberData(nameof(Grid))]
    public void Row_FollowsTheRules(string key) => Check(key);

    private void Check(string key)
    {
        int index = Array.IndexOf(AllRows, key);
        Assert.True(index >= 0, key);
        var run = TimelineRun.Run(-(5390000 + index), key);
        var m = Verify(run);
        _output.WriteLine($"MEASURE #539 timeline {key.Replace('|', ' ')}: windows - rules {m.Windows:E2} s, gates - rules {m.Gates:E2} s, " +
                          $"law at entries {m.AtEntries:E2} rad, between {m.Between:E2} rad, discrete {m.Discrete:E2} rad ({m.Checks} reads); " +
                          $"burns (TT s) [{string.Join(", ", run.Burns.Select(b => run.Seconds(b).ToString("F7", Invariant)))}], - closed form [{string.Join(", ", m.BurnErrors.Select(x => x.ToString("E2", Invariant)))}]; " +
                          $"activations [{string.Join(", ", m.Expected.Activations.Select(a => $"{a.Kind} {run.Seconds(a.Epoch).ToString("F7", Invariant)}"))}]; " +
                          $"executed [{string.Join(",", run.Spacecraft.ExecutedManeuvers.Select(x => Array.IndexOf(run.Chain, x)))}], fuel - rocket equation {m.Fuel:E2} kg" +
                          (m.UnfiredApsis is { } unfired ? $"; unfired {Array.IndexOf(run.Chain, m.Expected.Unfired)}, its closed-form apsis - end {unfired.ToString("F3", Invariant)} s" : ""));
        AssertMeasures(run, m);
    }

    internal static void AssertMeasures(TimelineRun run, Measures m)
    {
        if (TimelineRules.Checked)
        {
            Assert.True(m.Windows <= TimelineRules.RuleBound, $"windows - rules {m.Windows:E3} s");
            Assert.True(m.Gates <= TimelineRules.RuleBound, $"gates - rules {m.Gates:E3} s");
        }

        Assert.True(m.Checks > 0);
        Assert.True(m.AtEntries <= AttitudeRecordTests.LawAtEntries, $"law at the entries {m.AtEntries:E3} rad");
        Assert.True(m.Between <= (run.OutputStep > 10.0 ? LawBetweenEntriesAt60 : AttitudeRecordTests.LawBetweenEntries), $"law between the entries {m.Between:E3} rad");
        Assert.True(m.Discrete <= AttitudeRecordTests.DiscreteLaw, $"discrete {m.Discrete:E3} rad");
        Assert.Equal(m.Expected.Executed, run.Spacecraft.ExecutedManeuvers.ToArray());
        foreach (var maneuver in run.Chain.Where(x => !m.Expected.Executed.Contains(x)))
        {
            Assert.Null(maneuver.ManeuverWindow);
        }

        Assert.True(m.Fuel <= 1e-9, $"fuel - rocket equation {m.Fuel:E3} kg");
        double lag = MinimumEpochGateTests.VelocityVerletLagLimit[run.Moon ? "moon|VV1" : "apsis|VV1"];
        double rk = MinimumEpochGateTests.Rk78Epoch[run.Moon ? "moon" : "apsis"] + (run.Moon ? 1.0e-7 : 0.0);
        for (int k = 0; k < m.BurnErrors.Count; k++)
        {
            double error = m.BurnErrors[k];
            if (run.IsVelocityVerlet)
            {
                Assert.InRange(error, -lag, 1.0 + lag);
            }
            else
            {
                Assert.True(System.Math.Abs(error) <= rk, $"burn {k}: {error:E3} s");
            }
        }

        // An impulse without a burn: its closed-form apsis past the end, but for the bound of a burn that may fall after it
        if (m.UnfiredApsis is { } unfired)
        {
            Assert.True(unfired > -(run.IsVelocityVerlet ? 1.0 + lag : rk),
                $"impulse {Array.IndexOf(run.Chain, m.Expected.Unfired)} not fired, its closed-form apsis {-unfired:F3} s before the end");
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The run
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>A case of the timeline propagated on one row of the grid.</summary>
    internal sealed class TimelineRun
    {
        public string Key { get; private init; }
        public string Letter { get; private init; }
        public string Integrator { get; private init; }
        public string Path { get; private init; }
        public string FrameName { get; private init; }
        public bool Ssb => FrameName == "SSB";
        public bool Moon => FrameName == "Moon";
        public bool IsVelocityVerlet => Integrator.StartsWith("VV");
        public double OutputStep { get; private init; }
        public Frame Frame { get; private init; }
        public CelestialBody Center { get; private init; }
        public Spacecraft Spacecraft { get; private init; }
        public Window Window { get; private init; }

        /// <summary>The solution the references read: the propagation's, or its twin's on the scenario's Velocity-Verlet
        /// path.</summary>
        public PropagationSolution Solution { get; private init; }

        public Maneuver.Maneuver[] Chain { get; private init; }
        public Time[] Given { get; private init; }
        public Dictionary<Maneuver.Maneuver, string> Kinds { get; private init; }
        public List<Time> Burns { get; private init; }
        public OrbitalParams InitialIcrf { get; private init; }

        /// <summary>Whether the seconds of the rules are TT seconds: about the Earth, outside SSB mode.</summary>
        public bool Tt => !Ssb && !Moon;

        /// <summary>The seconds from S of the time argument: TT about the Earth, TDB about the Moon and in SSB mode.</summary>
        public double Seconds(Time t) => Tt ? MinimumEpochGateTests.Tt(t) : (t.ToTDB() - S).TotalSeconds;

        public static TimelineRun Run(int naifId, string key, Action<Spacecraft, Maneuver.Maneuver[]> before = null)
        {
            var parts = key.Split('|');
            string letter = parts[0], integrator = parts[1], path = parts[2], frameName = parts[3];
            double output = double.Parse(parts[4], Invariant);
            bool moon = frameName == "Moon", ssb = frameName == "SSB";
            var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
            var center = moon ? new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, S) : earth;
            OrbitalParams orbitIcrf = moon ? MoonOrbit(center) : OrbitOf(letter, center);
            OrbitalParams orbit = orbitIcrf;
            CelestialItem[] bodies = [center];
            var frame = Frame.ICRF;
            if (ssb)
            {
                var sv = orbitIcrf.ToStateVector();
                var fromSsb = center.GetGeometricStateRelativeTo(S, Barycenters.SOLAR_SYSTEM_BARYCENTER);
                orbit = new StateVector(fromSsb.Position + sv.Position, fromSsb.Velocity + sv.Velocity, Barycenters.SOLAR_SYSTEM_BARYCENTER, S, Frame.ICRF);
                bodies = [center, new CelestialBody(Stars.Sun, Frame.ICRF, S)];
            }
            else if (!moon && frameName != "ICRF")
            {
                frame = AttitudeRecordTests.FrameOf(frameName);
                orbit = orbitIcrf.ToStateVector().ToFrame(frame).ToStateVector();
            }

            bool covariance = path.Contains("Cov");
            if (covariance)
            {
                var sv = orbit.ToStateVector();
                var p0 = new Matrix(6, 6);
                for (int i = 0; i < 6; i++) p0.Set(i, i, i < 3 ? 100.0 : 1.0e-4);
                orbit = new StateVector(sv.Position, sv.Velocity, sv.Observer, sv.Epoch, sv.Frame, p0);
            }

            var (spacecraft, chain, kinds, hours) = Build(naifId, letter, orbit, center);
            before?.Invoke(spacecraft, chain);
            var given = chain.Select(x => x.MinimumEpoch).ToArray();
            var window = new Window(S, S.AddSeconds(hours * 3600.0));
            var step = TimeSpan.FromSeconds(output);
            Integrator NewIntegrator() => integrator switch
            {
                "VV1" => new VVIntegrator(TimeSpan.FromSeconds(1.0)),
                "RKf60" => new RK78Integrator(60.0),
                _ => ssb ? new RK78Integrator(AttitudeRecordTests.SsbRk78Tolerance, AttitudeRecordTests.SsbRk78Tolerance) : new RK78Integrator(1e-12, 1e-12)
            };
            var options = covariance ? new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance, IncludeInStepControl = path.EndsWith("Step") } : null;

            PropagationSolution solution;
            switch (path)
            {
                case "Propagate":
                    solution = spacecraft.Propagate(window, bodies, NewIntegrator(), false, false, step);
                    break;
                case "Default":
                    Assert.Equal("VV1", integrator);
                    solution = spacecraft.Propagate(window, bodies, false, false, step);
                    break;
                case "Async":
                    Assert.Equal("VV1", integrator);
                    solution = spacecraft.PropagateAsync(window, bodies, false, false, step).GetAwaiter().GetResult();
                    break;
                case "PropagateCov":
                case "PropagateCovStep":
                    solution = spacecraft.Propagate(window, bodies, NewIntegrator(), false, false, step, options);
                    break;
                case "Scenario":
                case "ScenarioDefault":
                case "ScenarioCov":
                case "ScenarioCovStep":
                {
                    var scenario = new Scenario($"STL{-naifId}", new IO.Astrodynamics.Mission.Mission($"mTL{-naifId}"), window);
                    foreach (var body in bodies) scenario.AddCelestialItem(body);
                    scenario.AddSpacecraft(spacecraft);
                    var simulation = path switch
                    {
                        "ScenarioDefault" => scenario.SimulateAsync(false, false, step),
                        "Scenario" => scenario.SimulateAsync(false, false, step, NewIntegrator),
                        _ => scenario.SimulateAsync(false, false, step, NewIntegrator, options)
                    };
                    simulation.GetAwaiter().GetResult();
                    solution = spacecraft.LastPropagation;
                    break;
                }
                case "Batch":
                case "BatchCov":
                case "BatchCovStep":
                {
                    var task = new PropagationTask(spacecraft, window, bodies, false, false, step, NewIntegrator) { CovariancePropagation = options };
                    var result = BatchPropagator.Propagate([task]).Results[0];
                    Assert.Null(result.Error);
                    solution = result.Solution;
                    break;
                }
                case "Builder":
                {
                    // A first run only: the builder never resets the spacecraft (#583)
                    using var propagator = new CentralBodyPropagatorBuilder(window, spacecraft, NewIntegrator(), step).WithPerturbingBodies(bodies).Build();
                    solution = propagator.Propagate();
                    break;
                }
                case "Direct":
                {
                    using var propagator = new CentralBodyPropagator(window, spacecraft, NewIntegrator(), bodies, false, false, step);
                    solution = propagator.Propagate();
                    break;
                }
                default:
                    throw new ArgumentException(path);
            }

            if (solution == null)
            {
                // The scenario's Velocity-Verlet path keeps no solution: a twin through Spacecraft.Propagate, whose stored
                // states are the scenario's to the bit
                var (twin, _, _, _) = Build(naifId - 500000, letter, orbit, center);
                solution = twin.Propagate(window, bodies, NewIntegrator(), false, false, step);
                Assert.Equal(twin.PropagatedStates.Select(x => (x.Epoch, x.Position, x.Velocity)), spacecraft.PropagatedStates.Select(x => (x.Epoch, x.Position, x.Velocity)));
            }

            var burns = solution.Segments.Skip(1).Select(x => x.BaseEpoch).ToList();
            return new TimelineRun
            {
                Key = key, Letter = letter, Integrator = integrator, Path = path, FrameName = frameName, OutputStep = output, Frame = frame,
                Center = center, Spacecraft = spacecraft, Window = window, Solution = solution, Chain = chain, Given = given, Kinds = kinds,
                Burns = burns, InitialIcrf = orbitIcrf
            };
        }

        /// <summary>The position and the velocity relative to the centre in ICRF at <paramref name="t"/>: the solution, rotated
        /// from its frame by <c>pxform</c>, relative to the centre.</summary>
        public (Vector3 Position, Vector3 Velocity) StateAt(Time t)
        {
            var (position, velocity) = Solution.InterpolateAt(t);
            var state = new StateVector(position, velocity, Solution.Observer, t, Solution.Frame);
            if (Solution.Frame != Frame.ICRF)
            {
                var q = SpiceAPI.Instance.TransformFrame(t, Solution.Frame, Frame.ICRF).Rotation;
                state = new StateVector(position.Rotate(q), velocity.Rotate(q), Solution.Observer, t, Frame.ICRF);
            }

            var local = state.RelativeTo(Center, Aberration.None).ToStateVector();
            return (local.Position, local.Velocity);
        }

        /// <summary>The delta-V of an impulse in ICRF, rotated from the frame of the propagation by <c>pxform</c>.</summary>
        public Vector3 DeltaVInIcrf(ImpulseManeuver impulse, Time tc) =>
            Frame == Frame.ICRF ? impulse.DeltaV : impulse.DeltaV.Rotate(SpiceAPI.Instance.TransformFrame(tc, Frame, Frame.ICRF).Rotation);
    }

    /// <summary>The orbit of a case about the Earth: #569's from its apogee at S; for the edges, from 2 s before its perigee
    /// (the clip to S), or a circular one whose −r̂ at S is the front of the
    /// spacecraft (+Y), the nadir attitude there the identity (D). R5 starts at the apogee given by Keplerian elements, where
    /// r·v is 0.0.</summary>
    internal static OrbitalParams OrbitOf(string letter, CelestialItem center)
    {
        double n = System.Math.Sqrt(center.GM / (MinimumEpochGateTests.A0 * MinimumEpochGateTests.A0 * MinimumEpochGateTests.A0));
        return letter switch
        {
            "ClipS" or "ClipSLaw" => MinimumEpochGateTests.Orbit(center, 2.0 * System.Math.PI - 2.0 * n),
            "D" => new StateVector(new Vector3(0.0, -7378137.0, 0.0), new Vector3(System.Math.Sqrt(center.GM / 7378137.0), 0.0, 0.0), center, S, Frame.ICRF),
            _ => MinimumEpochGateTests.Orbit(center, System.Math.PI)
        };
    }

    // 100 × 300 km lunar orbit from its apolune
    private static KeplerianElements MoonOrbit(CelestialItem moon)
    {
        double ra = MoonRadius + 300000.0, rp = MoonRadius + 100000.0;
        return new KeplerianElements((ra + rp) / 2.0, (ra - rp) / (ra + rp), 0.9, 0.3, 1.1, System.Math.PI, moon, S, Frame.ICRF);
    }

    private static readonly Time Min = new(DateTime.MinValue, TimeFrame.TDBFrame);

    /// <summary>The spacecraft of a case and its chain, head first; the kind of each attitude law.</summary>
    internal static (Spacecraft, Maneuver.Maneuver[], Dictionary<Maneuver.Maneuver, string>, double Hours) Build(int naifId, string letter, OrbitalParams orbit,
        CelestialBody center)
    {
        var spacecraft = new Spacecraft(naifId, $"TL{-naifId}", 1000.0, 3000.0, new Clock($"TL{-naifId}_CLK", 65536), orbit, cosparId: "2024-001A");
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        bool moon = center.NaifId == PlanetsAndMoons.MOON.NaifId;
        var kinds = new Dictionary<Maneuver.Maneuver, string>();

        Maneuver.Maneuver Nadir(double hold, double? minimum = null) => Kind(new NadirAttitude(center, minimum.HasValue ? S.AddSeconds(minimum.Value) : Min, TimeSpan.FromSeconds(hold), engine), "nadir");
        Maneuver.Maneuver Prograde(double hold, double? minimum = null) => Kind(new ProgradeAttitude(center, minimum.HasValue ? S.AddSeconds(minimum.Value) : Min, TimeSpan.FromSeconds(hold), engine), "prograde");
        Maneuver.Maneuver Lvlh(double hold) => Kind(TriadAttitude.CreateLVLH(center, Min, TimeSpan.FromSeconds(hold), engine), "lvlh");
        Maneuver.Maneuver Apo(double hold = 0.0, double target = 8378137.0) =>
            new ApogeeHeightManeuver(center, Min, TimeSpan.FromSeconds(hold), moon ? MoonRadius + (target == 8378137.0 ? 500000.0 : 700000.0) : target, engine);
        Maneuver.Maneuver Peri() => new PerigeeHeightManeuver(center, Min, TimeSpan.Zero, 6978137.0, engine);

        Maneuver.Maneuver Kind(Maneuver.Maneuver m, string kind)
        {
            kinds[m] = kind;
            return m;
        }

        double hours = 4.0;
        // The clip to the previous activation (#577 choice 12, its chain-peri-apo): the perigee raised at the apogee after
        // S + 3 500, its hold ending 5 s before the next perigee, where the apogee raise fires: its thrust window opens
        // 0.666 dt (10.7 s) before, before the end of the window of the first
        double a2 = (MinimumEpochGateTests.Apogee0 + 6878137.0) / 2.0;
        double halfT2 = System.Math.PI * System.Math.Sqrt(a2 * a2 * a2 / center.GM);
        Maneuver.Maneuver PeriThenHold() => new PerigeeHeightManeuver(center, MinimumEpochGateTests.GateAtTt(3500.0), TimeSpan.FromSeconds(halfT2 - 5.0), 6878137.0, engine);
        Maneuver.Maneuver[] chain = letter switch
        {
            "ClipS" => [Apo()],
            "ClipSLaw" => [Nadir(0.0), Apo()],
            "ClipLast" => [PeriThenHold(), Apo()],
            "ClipLastLaw" => [Nadir(0.0), PeriThenHold(), Apo()],
            "R5" => [Nadir(0.0), Peri()],
            "R5alone" => [Peri()],
            "D" => [Nadir(0.0)],
            "NPI" => [Nadir(0.0), Prograde(600.0), Apo()],
            "Npast" => [Nadir(0.0), Apo(6000.0)],
            "LeadPast" => [Nadir(5.0 * 3600.0), Apo()],
            "MinPast" => [Prograde(0.0, 5.0 * 3600.0), Apo()],
            "A" => [Apo(), Lvlh(0.0)],
            "B" => [Nadir(0.0), Apo(), Lvlh(3600.0)],
            "C" => [Nadir(0.0), Apo(), Lvlh(600.0), Prograde(0.0)],
            "E1" => [Apo()],
            "E2" => [Nadir(0.0), Apo()],
            "F1" => [Nadir(600.0), Prograde(0.0)],
            "F2" => [Prograde(0.0, 300.0)],
            "F3" => [Nadir(3600.0), Apo()],
            "F3p" => [Nadir(2000.0), Apo()],
            "F4" => [Apo(), Lvlh(0.0), Peri()],
            "F5" => [Apo(), Lvlh(4000.0), Peri()],
            "F5p" => [Apo(), Lvlh(4000.0), Apo(0.0, 9378137.0)],
            "G" => [Nadir(0.0)],
            "I1" => [Apo(), Prograde(0.0, 4000.0)],
            "I2" => [Apo(600.0), Lvlh(0.0)],
            "K" => [Apo(), Lvlh(5000.0), Prograde(0.0)],
            "L" => [Nadir(0.0), Apo(), Peri()],
            "M0" => [Apo(), Lvlh(0.0), Apo(0.0, 9378137.0)],
            "M600" => [Apo(600.0), Lvlh(0.0), Apo(0.0, 9378137.0)],
            _ => throw new ArgumentException(letter)
        };
        if (letter == "G") hours = 1000.0 / 3600.0;
        if (letter is "K" or "Npast") hours = 2.0;
        // D within a sixth of its orbit: the nadir stays within 1 rad of the identity, where the closed form is conditioned
        if (letter == "D") hours = 0.25;
        if (letter.StartsWith("ClipLast")) hours = 6.0;
        for (int i = 0; i + 1 < chain.Length; i++) chain[i].SetNextManeuver(chain[i + 1]);
        spacecraft.SetStandbyManeuver(chain[0]);
        return (spacecraft, chain, kinds, hours);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The rules and the comparisons
    // ---------------------------------------------------------------------------------------------------------------------

    internal sealed record Activation(Time Epoch, string Kind, Maneuver.Maneuver Maneuver, Attitude Law, Vector3 DeltaV);

    internal sealed class ExpectedTimeline
    {
        public List<Activation> Activations { get; } = new();
        public Dictionary<Maneuver.Maneuver, (Window Thrust, Window Maneuver)> Windows { get; } = new();
        public Dictionary<Maneuver.Maneuver, Time> Gates { get; } = new();
        public Maneuver.Maneuver[] Executed { get; set; }
        public double Fuel { get; set; }

        /// <summary>The first impulse of the chain the rules reached without a burn of the run left for it, null if none;
        /// the closed form puts its apsis past the end of the window, or the library missed it.</summary>
        public ImpulseManeuver Unfired { get; set; }
    }

    /// <summary>The timeline of the rules (<see cref="TimelineRules"/>) from the burns of the run.</summary>
    internal static ExpectedTimeline Rules(TimelineRun run)
    {
        var expected = new ExpectedTimeline();
        var executed = new List<Maneuver.Maneuver>();
        var previousEnd = run.Window.StartDate;
        var end = run.Window.EndDate.AddSeconds(1e-7);
        double mass = 2000.0, fuel = 0.0;
        Attitude current = null;
        int k = 0;
        for (int i = 0; i < run.Chain.Length; i++)
        {
            var maneuver = run.Chain[i];
            var gate = i == 0 ? run.Given[i] : TimelineRules.Activation(run.Given[i], previousEnd);
            expected.Gates[maneuver] = gate;
            if (maneuver is Attitude attitude)
            {
                var start = TimelineRules.Activation(run.Given[i], previousEnd);
                if (start > end) break;
                var stop = TimelineRules.After(start, attitude.ManeuverHoldDuration.TotalSeconds, run.Tt);
                expected.Activations.Add(new Activation(start, "law", attitude, attitude, Vector3.Zero));
                expected.Windows[attitude] = (new Window(start, start), new Window(start, stop));
                executed.Add(attitude);
                current = attitude;
                previousEnd = stop;
                continue;
            }

            var impulse = (ImpulseManeuver)maneuver;
            if (k >= run.Burns.Count)
            {
                expected.Unfired = impulse;
                break;
            }

            var tc = run.Burns[k++];
            double dv = impulse.DeltaV.Magnitude();
            double dt = TimelineRules.BurnDuration(mass, dv, 300.0, 10.0);
            double burned = TimelineRules.FuelBurned(mass, dv, 300.0);
            mass -= burned;
            fuel += burned;
            var thrustStart = TimelineRules.ThrustStart(tc, dt, run.Tt);
            var thrustEnd = TimelineRules.After(thrustStart, dt, run.Tt);
            var maneuverEnd = TimelineRules.ManeuverEnd(tc, dt, impulse.ManeuverHoldDuration.TotalSeconds, run.Tt);
            expected.Windows[impulse] = (new Window(thrustStart, thrustEnd), new Window(thrustStart, maneuverEnd));
            var attitudeStart = TimelineRules.Activation(thrustStart, run.Window.StartDate);
            if (expected.Activations.Count > 0) attitudeStart = TimelineRules.Activation(attitudeStart, expected.Activations[^1].Epoch);
            expected.Activations.Add(new Activation(attitudeStart, "discrete", impulse, null, run.DeltaVInIcrf(impulse, tc)));
            if (current != null && maneuverEnd <= end)
            {
                expected.Activations.Add(new Activation(maneuverEnd, "resumed", current, current, Vector3.Zero));
            }

            executed.Add(impulse);
            previousEnd = maneuverEnd;
        }

        expected.Executed = executed.ToArray();
        expected.Fuel = fuel;
        return expected;
    }

    internal sealed class Measures
    {
        public ExpectedTimeline Expected { get; init; }
        public double Windows { get; set; }
        public double Gates { get; set; }
        public double AtEntries { get; set; }
        public double Between { get; set; }
        public double Discrete { get; set; }
        public int Checks { get; set; }
        public double Fuel { get; set; }
        public List<double> BurnErrors { get; } = new();

        /// <summary>The closed-form apsis of <see cref="ExpectedTimeline.Unfired"/> after its gate, less the end of the
        /// window, s (time argument); null if every impulse the rules reached fired, or in SSB mode.</summary>
        public double? UnfiredApsis { get; set; }
    }

    /// <summary>The closed form of the attitude in force at <paramref name="t"/> (spacecraft frame to ICRF) and whether it is a
    /// law; null within <see cref="Unresolved"/> of an activation.</summary>
    internal static (Func<Vector3, Vector3> Map, bool Law)? ExpectedAt(TimelineRun run, ExpectedTimeline expected, Time t)
    {
        if (expected.Activations.Any(a => System.Math.Abs((t - a.Epoch).TotalSeconds) <= Unresolved)) return null;
        var active = expected.Activations.LastOrDefault(a => a.Epoch < t);
        if (active == null) return (v => v, false);
        if (active.Law == null) return (AttitudeRecordTests.MinimalRotation(Spacecraft.Front, active.DeltaV), false);
        var (r, v) = run.StateAt(t);
        return run.Kinds[active.Law] switch
        {
            "nadir" => (AttitudeRecordTests.MinimalRotation(Spacecraft.Front, r.Inverse()), true),
            "prograde" => (AttitudeRecordTests.MinimalRotation(Spacecraft.Front, v), true),
            "lvlh" => (AttitudeRecordTests.Triad(Spacecraft.Down, Spacecraft.Front, r.Inverse(), v), true),
            var kind => throw new ArgumentException(kind)
        };
    }

    internal static Measures Verify(TimelineRun run)
    {
        var expected = Rules(run);
        var m = new Measures { Expected = expected, Fuel = System.Math.Abs(1000.0 - run.Spacecraft.FuelTanks.Single().Quantity - expected.Fuel) };

        // The windows and the gates against the rules
        foreach (var (maneuver, (thrust, window)) in expected.Windows)
        {
            Assert.True(maneuver.ThrustWindow.HasValue && maneuver.ManeuverWindow.HasValue, $"{Array.IndexOf(run.Chain, maneuver)} not executed");
            foreach (var (a, b) in new[]
                     {
                         (maneuver.ThrustWindow.Value.StartDate, thrust.StartDate), (maneuver.ThrustWindow.Value.EndDate, thrust.EndDate),
                         (maneuver.ManeuverWindow.Value.StartDate, window.StartDate), (maneuver.ManeuverWindow.Value.EndDate, window.EndDate)
                     })
                m.Windows = System.Math.Max(m.Windows, System.Math.Abs((a - b).TotalSeconds));
        }

        foreach (var (maneuver, gate) in expected.Gates)
            m.Gates = System.Math.Max(m.Gates, System.Math.Abs((maneuver.MinimumEpoch - gate).TotalSeconds));

        // The burns against the closed form: the first apsis of the maneuver at or after its gate, on the orbit before it;
        // for the first impulse without a burn, that apsis past the end of the window (the library would otherwise have
        // missed it). In SSB mode (no closed form) every impulse of these chains fires
        if (!run.Ssb)
        {
            var impulses = expected.Executed.OfType<ImpulseManeuver>().ToArray();
            double ApsisAfterGate(int k, ImpulseManeuver impulse)
            {
                KeplerClosedForm orbit;
                if (k == 0)
                {
                    var sv = run.InitialIcrf.ToStateVector();
                    orbit = new KeplerClosedForm(run.Center.GM, sv.Position, sv.Velocity, 0.0);
                }
                else
                {
                    var after = run.Spacecraft.PropagatedArcs[k][0].RelativeTo(run.Center, Aberration.None).ToStateVector();
                    if (after.Frame != Frame.ICRF) after = after.ToFrame(Frame.ICRF).ToStateVector();
                    orbit = new KeplerClosedForm(run.Center.GM, after.Position, after.Velocity, run.Seconds(after.Epoch));
                }

                var gate = TimelineRules.Activation(expected.Gates[impulse], run.Window.StartDate);
                return orbit.NextApsis(run.Seconds(gate), impulse is PerigeeHeightManeuver);
            }

            for (int k = 0; k < impulses.Length; k++)
            {
                m.BurnErrors.Add(run.Seconds(run.Burns[k]) - ApsisAfterGate(k, impulses[k]));
            }

            if (expected.Unfired != null)
            {
                m.UnfiredApsis = ApsisAfterGate(impulses.Length, expected.Unfired) - run.Seconds(run.Window.EndDate);
            }
        }
        else
        {
            Assert.Null(expected.Unfired);
        }

        // The attitude: every second, at each entry, and about each activation
        var record = run.Spacecraft.Frame.GetStateOrientationsToICRF().ToArray();
        var entries = new HashSet<Time>(record.Select(x => x.Epoch));
        var epochs = new SortedSet<Time>();
        double length = (run.Window.EndDate - run.Window.StartDate).TotalSeconds;
        for (double s = 0.0; s <= length; s += 1.0) epochs.Add(run.Window.StartDate.AddSeconds(s));
        foreach (var entry in record) epochs.Add(entry.Epoch);
        foreach (var activation in expected.Activations)
        foreach (var offset in new[] { -1e-3, -2.5e-6, 2.5e-6, 1e-3, 0.5 })
            epochs.Add(activation.Epoch.AddSeconds(offset));

        foreach (var t in epochs.Where(x => x >= run.Window.StartDate && x <= run.Window.EndDate))
        {
            var law = ExpectedAt(run, expected, t);
            if (law == null) continue;
            var managed = run.Spacecraft.Frame.GetStateOrientationToICRF(t).Rotation;
            double angle = AttitudeRecordTests.RotationAngle(law.Value.Map, v => v.Rotate(managed));
            m.Checks++;
            if (!law.Value.Law) m.Discrete = System.Math.Max(m.Discrete, angle);
            else if (entries.Contains(t)) m.AtEntries = System.Math.Max(m.AtEntries, angle);
            else m.Between = System.Math.Max(m.Between, angle);
        }

        return m;
    }
}
