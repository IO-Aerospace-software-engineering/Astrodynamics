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
/// A spacecraft propagated again fires its maneuver chain as a spacecraft never propagated (#573): the gate a propagation
/// raises on a chained maneuver, the end of the window of the maneuver before it, lasts for that propagation only, and a
/// minimum epoch given through <c>Spacecraft.SetStandbyManeuver(maneuver, minimumEpoch)</c> lasts for every propagation.
/// </summary>
/// <remarks>
/// <para>
/// <b>The case</b> (#573's, on #569's orbit, <see cref="MinimumEpochGateTests"/>): a 400 × 1 000 km orbit about a
/// point-mass Earth with the kernel GM; 1 000 kg dry and 1 000 kg of fuel, Isp 300 s, 10 kg/s; every maneuver built with
/// the minimum epoch S = 2024-03-01 12:00 TDB. The chains: <c>apo-apo</c>, an <see cref="ApogeeHeightManeuver"/> to
/// 8 378 137 m held 5 000 s, then one to 8 878 137 m (#573's); <c>att&lt;H&gt;</c>, an <see cref="ApogeeHeightManeuver"/>
/// to 8 378 137 m, a <see cref="NadirAttitude"/> held H s, then a <see cref="PerigeeHeightManeuver"/> to 6 878 137 m;
/// <c>lead&lt;H&gt;</c>, a leading <see cref="NadirAttitude"/> held H s, then an <see cref="ApogeeHeightManeuver"/> to
/// 8 378 137 m; <c>lead2</c>, a leading nadir and a prograde held 500 s each, then the same impulse (#539: the gates the
/// attitude timeline raises, #582 choice 4). Since #539 the nadir of <c>att&lt;H&gt;</c> activates at the end of the first
/// impulse's maneuver window (505.10 s after a burn at S + 500), so the gate of the second impulse is 505.10 + H.
/// Velocity-Verlet at 1 s, RK7(8) at 1e-12 / 1e-12. Epochs are printed in TT seconds from S.
/// </para>
/// <para>
/// <b>The rule of a rerun case.</b> A gate left from an earlier propagation moves a later one only when it is later than
/// the gate the later one sets itself. Every rerun case meant to catch #573 puts the maneuvers of the second run earlier
/// than those of the first (another state, an earlier window), and fails on the library before #573's fix (7f1cc0b8): its
/// second burn one orbit late. The rows of the timeline (#539) fail the same way with its raises left on the lasting
/// minimum epoch (the two-argument <c>SetStandbyManeuver</c> for an attitude's raise, <c>RaiseGivenMinimumEpoch</c> for
/// the TT end of an impulse's window), as a scratch copy of the library showed (PR of #539): <c>lead1000</c> run 2 at
/// S + 7 426.38 s instead of S + 1 500.00 s, <c>att1000</c> from another state at 10 347.95 s instead of 3 782.65 s,
/// <c>apo-apo</c> at 13 630.60 s instead of 7 065.30 s (RK7(8)). A case whose second run fires no earlier than the first, or whose stale gate falls before
/// the crossing it gates (<c>att600</c>), is a control of the trajectory, and its name says so: it passes before the fix,
/// except for the <c>MinimumEpoch</c> read after the run, which a separate test asserts.
/// </para>
/// <para>
/// <b>The reference.</b> A spacecraft built identically (same orbit, same chain built the same way) and never propagated,
/// on the same activation path: the defect lives in the state a spacecraft keeps between two propagations, so the
/// spacecraft that keeps nothing is the reference. Compared with <c>==</c>, bit for bit, never with a tolerance: the burn
/// epochs and delta-V, the state at the window end, the windows of <c>ExecutedManeuvers</c>, the <c>MinimumEpoch</c> of each
/// maneuver, the maneuvers of <c>ToOpm</c>, the summary, the attitude after the second burn (at the fresh run's second burn
/// + 1 s and + 300 s, and at the window end − 10 s; the samples after the first burn are a control) and, in covariance mode,
/// P at the window end.
/// </para>
/// <para>
/// <b>The reference checked.</b> Outside SSB mode (whose closed form would omit the Sun), the fresh run is checked against
/// the Keplerian closed form (<see cref="KeplerClosedForm"/>), in the seconds of the time argument of the propagation: TT
/// about the Earth, TDB about the Moon. Each chained impulse fires at the first apsis of its own at or after the end of the
/// previous maneuver's window, on the orbit after the burn before it; the first, at the first one at or after its minimum
/// epoch on the initial orbit (the window start, when the orbit starts at that apsis). RK7(8) within
/// <see cref="MinimumEpochGateTests.Rk78Epoch"/>["apsis"], which includes the 100 ns quantization of the epochs: about the
/// Moon the first burn, its epoch a whole tick, falls one tick (−1.000e-7 s) before the closed form, which the "moon" bound
/// of #577 (5.0e-9 s, worst 2.46e-9 s on its cases) does not cover. Velocity-Verlet within
/// [−limit, h + limit), limit = <see cref="MinimumEpochGateTests.VelocityVerletLagLimit"/>["apsis|VV1"] about the Earth,
/// ["moon|VV1"] about the Moon: #577's bounds, set on Linux x64, Windows and macOS arm64. And the <c>MinimumEpoch</c> of
/// each chained maneuver is the later of the one it was given and the end of the previous maneuver's window, on every
/// spacecraft, reused or fresh. No threshold is set here.
/// </para>
/// </remarks>
public class RepropagationTests
{
    private static readonly Time S = MinimumEpochGateTests.S;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(10.0);
    private static readonly TimeSpan VvStep = TimeSpan.FromSeconds(1.0);
    private const double MoonRadius = MinimumEpochGateTests.MoonRadius;
    private static int _nextId = -573000;
    private readonly ITestOutputHelper _output;

    public RepropagationTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static double Tt(Time t) => MinimumEpochGateTests.Tt(t);
    private static string F(double x) => x.ToString("R", Invariant);
    private static string Fs(IEnumerable<double> xs) => "[" + string.Join(", ", xs.Select(F)) + "]";
    private static Window Win(Time start) => new(start, start.AddSeconds(6.0 * 3600.0));
    private static Time Later(Time a, Time b) => b > a ? b : a;

    // ---------------------------------------------------------------------------------------------------------------------
    // The setup: a centre, a frame, a chain
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>The centre and the frame of a case: the Earth in ICRF, ECLIPJ2000 or SSB mode, or the Moon.</summary>
    private sealed class Env
    {
        public CelestialBody Center { get; private init; }
        public CelestialItem[] Bodies { get; private init; }
        public string FrameName { get; private init; }
        public bool Moon { get; private init; }
        public bool Ssb => FrameName == "SSB";

        /// <summary>The fresh run is checked against the closed form outside SSB mode, whose closed form omits the Sun.</summary>
        public bool ClosedForm => !Ssb;

        /// <summary>The seconds of the closed form from S, those of the time argument of the propagation: TT about the
        /// Earth, TDB about the Moon (#501).</summary>
        public double Seconds(Time t) => Moon ? PropagationTimeArgument.Tdb.SecondsBetween(S, t) : Tt(t);

        public static Env Of(string setup = "Earth|ICRF")
        {
            var parts = setup.Split('|');
            bool moon = parts[0] == "Moon";
            var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, S);
            var center = moon ? new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, S) : earth;
            CelestialItem[] bodies = parts[1] == "SSB" ? [center, new CelestialBody(Stars.Sun, Frame.ICRF, S)] : [center];
            return new Env { Center = center, Bodies = bodies, FrameName = parts[1], Moon = moon };
        }

        /// <summary>The orbit of the case at <paramref name="epoch"/>, mean anomaly <paramref name="m"/> there, in the frame
        /// of the case, with P0 = diag(100 m², 1e-4 m²/s²) when <paramref name="withP0"/>.</summary>
        public OrbitalParams Orbit(double m, Time epoch, bool withP0 = false)
        {
            var (a, e) = Shape;
            OrbitalParams orbit = new KeplerianElements(a, e, 0.9, 0.3, 1.1, m, Center, epoch, Frame.ICRF);
            if (FrameName == "ECLIPJ2000")
            {
                orbit = orbit.ToStateVector().ToFrame(Frame.ECLIPTIC_J2000).ToStateVector();
            }
            else if (Ssb)
            {
                var sv = orbit.ToStateVector();
                var fromSsb = Center.GetGeometricStateRelativeTo(epoch, Barycenters.SOLAR_SYSTEM_BARYCENTER);
                orbit = new StateVector(fromSsb.Position + sv.Position, fromSsb.Velocity + sv.Velocity, Barycenters.SOLAR_SYSTEM_BARYCENTER, epoch,
                    Frame.ICRF);
            }

            if (!withP0) return orbit;
            var state = orbit.ToStateVector();
            var p0 = new Matrix(6, 6);
            for (int i = 0; i < 6; i++) p0.Set(i, i, i < 3 ? 100.0 : 1.0e-4);
            return new StateVector(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame, p0);
        }

        /// <summary>The semi-major axis and the eccentricity: #569's 400 × 1 000 km orbit, or 100 × 300 km about the Moon.</summary>
        private (double A, double E) Shape
        {
            get
            {
                if (!Moon) return (MinimumEpochGateTests.A0, MinimumEpochGateTests.E0);
                double ra = MoonRadius + 300000.0, rp = MoonRadius + 100000.0;
                return ((ra + rp) / 2.0, (ra - rp) / (ra + rp));
            }
        }

        public OrbitalParams Apogee(bool withP0 = false) => Orbit(System.Math.PI, S, withP0);
        public OrbitalParams PerigeeAt(Time epoch, bool withP0 = false) => Orbit(0.0, epoch, withP0);

        /// <summary>The orbit of the case whose perigee is <paramref name="lead"/> seconds after <paramref name="epoch"/>:
        /// a window that starts there does not start on the apsis, where the side of the crossing is a rounding.</summary>
        public OrbitalParams PerigeeAfter(Time epoch, double lead)
        {
            double a = Shape.A, n = System.Math.Sqrt(Center.GM / (a * a * a));
            return Orbit(2.0 * System.Math.PI - n * lead, epoch);
        }

        public Integrator NewIntegrator(string name) => name switch
        {
            "VV1" => new VVIntegrator(VvStep),
            "RK" => Ssb ? new RK78Integrator(AttitudeRecordTests.SsbRk78Tolerance, AttitudeRecordTests.SsbRk78Tolerance) : new RK78Integrator(1e-12, 1e-12),
            _ => throw new ArgumentException(name)
        };
    }

    /// <summary>A spacecraft, its chain, and the minimum epoch each maneuver of the chain was given.</summary>
    private sealed class Sc
    {
        public Env Env { get; init; }
        public Spacecraft Spacecraft { get; init; }
        public Maneuver.Maneuver[] Chain { get; init; }
        public Time[] Given { get; init; }
    }

    private static Sc Build(Env env, OrbitalParams orbit, string chain, Time? userGate = null)
    {
        int id = System.Threading.Interlocked.Decrement(ref _nextId);
        var spacecraft = new Spacecraft(id, $"RP{-id}", 1000.0, 3000.0, new Clock($"RP{-id}_CLK", 65536), orbit, cosparId: "2024-001A");
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        var c = env.Center;
        Maneuver.Maneuver[] maneuvers;
        if (chain == "apo-apo")
        {
            // About the Moon (orbit 100 × 300 km, period 7 654 s), a hold of 7 000 s puts the second burn of a run from
            // the perigee at S + 500 at the perigee 8 252 s later, before the gate of a run from the apogee at S (10 827 s)
            maneuvers = env.Moon
                ? [new ApogeeHeightManeuver(c, S, TimeSpan.FromSeconds(7000.0), MoonRadius + 500000.0, engine), new ApogeeHeightManeuver(c, S, TimeSpan.Zero, MoonRadius + 700000.0, engine)]
                : [new ApogeeHeightManeuver(c, S, TimeSpan.FromSeconds(5000.0), 8378137.0, engine), new ApogeeHeightManeuver(c, S, TimeSpan.Zero, 8878137.0, engine)];
        }
        else if (chain.StartsWith("lead2"))
        {
            // Two leading attitudes, a nadir and a prograde held 500 s each, then an ApogeeHeightManeuver: the raise of the
            // gate of an attitude by the attitude before it, and of the impulse by the last (#539, #582 choice 4)
            maneuvers =
            [
                new NadirAttitude(c, S, TimeSpan.FromSeconds(500.0), engine), new ProgradeAttitude(c, S, TimeSpan.FromSeconds(500.0), engine),
                new ApogeeHeightManeuver(c, S, TimeSpan.Zero, 8378137.0, engine)
            ];
        }
        else if (chain.StartsWith("lead"))
        {
            // A leading NadirAttitude held H s, then an ApogeeHeightManeuver: the hold gates the impulse (#539)
            double hold = double.Parse(chain[4..], Invariant);
            maneuvers = [new NadirAttitude(c, S, TimeSpan.FromSeconds(hold), engine), new ApogeeHeightManeuver(c, S, TimeSpan.Zero, 8378137.0, engine)];
        }
        else
        {
            double hold = double.Parse(chain[3..], Invariant);
            maneuvers =
            [
                new ApogeeHeightManeuver(c, S, TimeSpan.Zero, 8378137.0, engine), new NadirAttitude(c, S, TimeSpan.FromSeconds(hold), engine),
                new PerigeeHeightManeuver(c, S, TimeSpan.Zero, 6878137.0, engine)
            ];
        }

        for (int i = 0; i + 1 < maneuvers.Length; i++) maneuvers[i].SetNextManeuver(maneuvers[i + 1]);
        spacecraft.SetStandbyManeuver(maneuvers[0], userGate);
        // Read before any propagation: the minimum epoch each maneuver was given
        return new Sc { Env = env, Spacecraft = spacecraft, Chain = maneuvers, Given = maneuvers.Select(m => m.MinimumEpoch).ToArray() };
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // A run: the activation paths and what is compared
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>What a propagation leaves, as a user reads it.</summary>
    private sealed class Run
    {
        public Window Window { get; init; }
        public Time[] Burns { get; init; }
        public Vector3[] DeltaV { get; init; }
        public Vector3 R { get; init; }
        public Vector3 V { get; init; }
        public Time[] Gates { get; init; }
        public (int Index, Window? Maneuver, Window? Thrust)[] Executed { get; init; }
        public (DateTime Ignition, double Duration, double DeltaMass, double Dv1, double Dv2, double Dv3)[] Opm { get; init; }
        public (Window? Window, double Fuel, int Count) Summary { get; init; }
        public Matrix? P { get; init; }
    }

    private static Run Propagate(Sc sc, Window window, string integrator, string path, CovariancePropagationOptions covariance = null)
    {
        var env = sc.Env;
        var spacecraft = sc.Spacecraft;
        PropagationSolution solution;
        switch (path)
        {
            case "Propagate":
                Assert.Null(covariance);
                solution = spacecraft.Propagate(window, env.Bodies, env.NewIntegrator(integrator), false, false, Output);
                break;
            case "PropagateOptions":
                solution = spacecraft.Propagate(window, env.Bodies, env.NewIntegrator(integrator), false, false, Output, covariance);
                break;
            case "Default":
                // The default Velocity-Verlet integrator, at the step of the output
                Assert.Equal("VV1", integrator);
                solution = spacecraft.Propagate(window, env.Bodies, false, false, VvStep);
                break;
            case "Async":
                Assert.Equal("VV1", integrator);
                solution = spacecraft.PropagateAsync(window, env.Bodies, false, false, VvStep).GetAwaiter().GetResult();
                break;
            case "Batch":
            {
                var task = new PropagationTask(spacecraft, window, env.Bodies, false, false, Output, () => env.NewIntegrator(integrator))
                    { CovariancePropagation = covariance };
                var result = BatchPropagator.Propagate([task]).Results[0];
                Assert.Null(result.Error);
                solution = result.Solution;
                break;
            }
            default:
                throw new ArgumentException(path);
        }

        return Capture(sc, solution, window, spacecraft.GetSummary(), covariance?.Mode == CovariancePropagationMode.Covariance);
    }

    private static Scenario NewScenario(Sc sc, Window window)
    {
        int id = sc.Spacecraft.NaifId;
        var scenario = new Scenario($"RPS{-id}", new IO.Astrodynamics.Mission.Mission($"RPM{-id}"), window);
        foreach (var body in sc.Env.Bodies) scenario.AddCelestialItem(body);
        scenario.AddSpacecraft(sc.Spacecraft);
        return scenario;
    }

    /// <summary>A simulation of the scenario by one of the three overloads of <c>Scenario.SimulateAsync</c>.</summary>
    private static Run Simulate(Scenario scenario, Sc sc, Window window, string integrator, string overload)
    {
        var env = sc.Env;
        CovariancePropagationOptions covariance = overload.StartsWith("Covariance")
            ? new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance, IncludeInStepControl = overload == "CovarianceStep" }
            : null;
        var summary = overload switch
        {
            "Default" => scenario.SimulateAsync(false, false, VvStep),
            "Factory" => scenario.SimulateAsync(false, false, Output, () => env.NewIntegrator(integrator)),
            _ => scenario.SimulateAsync(false, false, Output, () => env.NewIntegrator(integrator), covariance)
        };
        if (overload == "Default") Assert.Equal("VV1", integrator);
        var s = summary.GetAwaiter().GetResult();
        // Velocity-Verlet through a Scenario keeps no solution: the burns are then read from the arcs of the stored states
        return Capture(sc, sc.Spacecraft.LastPropagation, window, s.SpacecraftSummaries.Single(), covariance != null);
    }

    private static Run Capture(Sc sc, PropagationSolution solution, Window window, SpacecraftSummary summary, bool covariance)
    {
        var spacecraft = sc.Spacecraft;
        var arcs = spacecraft.PropagatedArcs;
        Time[] burns;
        Vector3 r, v;
        if (solution != null)
        {
            burns = solution.Segments.Skip(1).Select(s => s.BaseEpoch).ToArray();
            (r, v) = solution.InterpolateAt(window.EndDate);
        }
        else
        {
            burns = arcs.Take(arcs.Count - 1).Select(a => a[^1].Epoch).ToArray();
            r = arcs[^1][^1].Position;
            v = arcs[^1][^1].Velocity;
        }

        var opm = spacecraft.ToOpm(includeKeplerianElements: false, includeManeuvers: true);
        return new Run
        {
            Window = window,
            Burns = burns,
            DeltaV = sc.Chain.OfType<ImpulseManeuver>().Where(m => m.ThrustWindow.HasValue).Select(m => m.DeltaV).ToArray(),
            R = r,
            V = v,
            Gates = sc.Chain.Select(m => m.MinimumEpoch).ToArray(),
            Executed = spacecraft.ExecutedManeuvers.Select(m => (Array.IndexOf(sc.Chain, m), m.ManeuverWindow, m.ThrustWindow)).ToArray(),
            Opm = (opm.Data.Maneuvers ?? []).Select(m => (m.ManEpochIgnition, m.ManDuration, m.ManDeltaMass, m.ManDv1, m.ManDv2, m.ManDv3)).ToArray(),
            Summary = (summary.ManeuverWindow, summary.FuelConsumption, summary.Maneuvers.Count()),
            P = covariance ? solution!.GetCovariance(window.EndDate) : null
        };
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The comparisons
    // ---------------------------------------------------------------------------------------------------------------------

    private static Quaternion[] Attitudes(Sc sc, IEnumerable<Time> epochs) =>
        epochs.Select(t => sc.Spacecraft.Frame.ToFrame(Frame.ICRF, t).Rotation).ToArray();

    // The rotation angle between two attitudes, 2 acos |q1·q2|; its floor is the rounding of acos (4e-8 rad)
    private static double Angle(Quaternion a, Quaternion b) =>
        2.0 * System.Math.Acos(System.Math.Min(1.0, System.Math.Abs(a.W * b.W + a.VectorPart.X * b.VectorPart.X + a.VectorPart.Y * b.VectorPart.Y + a.VectorPart.Z * b.VectorPart.Z)));

    /// <summary>
    /// Asserts that <paramref name="actual"/> is bit for bit <paramref name="expected"/>, after a MEASURE line of the deltas.
    /// With the two spacecraft, also their attitudes: after the second burn of <paramref name="expected"/> (+ 1 s, + 300 s, and
    /// the window end − 10 s), the check, and after its first burn (+ 1, 300 and 590 s), the control. Without
    /// <paramref name="compareGates"/>, the <c>MinimumEpoch</c> of each maneuver is printed but not compared: the trajectory
    /// alone.
    /// </summary>
    private void AssertSame(string tag, Run actual, Run expected, Sc actualSc = null, Sc expectedSc = null, bool compareGates = true)
    {
        string burnDelta = actual.Burns.Length == expected.Burns.Length
            ? Fs(actual.Burns.Zip(expected.Burns, (a, b) => Tt(a) - Tt(b)))
            : $"count {actual.Burns.Length} against {expected.Burns.Length}";
        double dr = (actual.R - expected.R).Magnitude(), dv = (actual.V - expected.V).Magnitude();
        double dp = 0.0;
        if (expected.P.HasValue)
        {
            Assert.True(actual.P.HasValue);
            for (int i = 0; i < 6; i++)
            for (int j = 0; j < 6; j++)
                dp = System.Math.Max(dp, System.Math.Abs(actual.P.Value.Get(i, j) - expected.P.Value.Get(i, j)));
        }

        bool executed = actual.Executed.SequenceEqual(expected.Executed), opm = actual.Opm.SequenceEqual(expected.Opm), summary = actual.Summary == expected.Summary;
        string attitudes = "";
        Quaternion[] qa = [], qe = [], ca = [], ce = [];
        if (actualSc != null)
        {
            var b = expected.Burns;
            var check = new List<Time>();
            if (b.Length > 1) check.AddRange([b[1].AddSeconds(1.0), b[1].AddSeconds(300.0)]);
            check.Add(expected.Window.EndDate.AddSeconds(-10.0));
            var control = b.Length > 0 ? new[] { b[0].AddSeconds(1.0), b[0].AddSeconds(300.0), b[0].AddSeconds(590.0) } : [];
            qa = Attitudes(actualSc, check);
            qe = Attitudes(expectedSc, check);
            ca = Attitudes(actualSc, control);
            ce = Attitudes(expectedSc, control);
            attitudes = $"; attitude after the second burn: max angle {qa.Zip(qe, Angle).Max():E3} rad, equal {qa.SequenceEqual(qe)} (TT {Fs(check.Select(Tt))}), " +
                        $"after the first (control): equal {ca.SequenceEqual(ce)}";
        }

        _output.WriteLine($"MEASURE #573 {tag}: burns (TT s from S) {Fs(actual.Burns.Select(Tt))} against {Fs(expected.Burns.Select(Tt))}, Δ {burnDelta}; " +
                          $"|Δr| {F(dr)} m, |Δv| {F(dv)} m/s at the window end; MinimumEpoch {Fs(actual.Gates.Select(Tt))} against {Fs(expected.Gates.Select(Tt))}; " +
                          $"ExecutedManeuvers equal {executed}, ToOpm equal {opm}, summary equal {summary} (fuel {F(actual.Summary.Fuel)} against {F(expected.Summary.Fuel)} kg)" +
                          (expected.P.HasValue ? $"; max|ΔP| {dp:E3}" : "") + attitudes);

        Assert.Equal(expected.Burns, actual.Burns);
        Assert.Equal(expected.DeltaV, actual.DeltaV);
        Assert.Equal(expected.R, actual.R);
        Assert.Equal(expected.V, actual.V);
        if (compareGates) Assert.Equal(expected.Gates, actual.Gates);
        Assert.Equal(expected.Executed, actual.Executed);
        Assert.Equal(expected.Opm, actual.Opm);
        Assert.Equal(expected.Summary, actual.Summary);
        Assert.Equal(qe, qa);
        Assert.Equal(ce, ca);
        Assert.Equal(0.0, dp);
    }

    /// <summary>
    /// The <c>MinimumEpoch</c> of each maneuver: the one it was given for the head of the chain, the later of the one it was
    /// given and the end of the previous maneuver's window for a chained one (the given one when that maneuver did not run).
    /// </summary>
    private void AssertGatesFollowTheWindows(string tag, Sc sc)
    {
        var expected = sc.Chain.Select((m, i) => i == 0 || !sc.Chain[i - 1].ManeuverWindow.HasValue
            ? sc.Given[i]
            : Later(sc.Given[i], sc.Chain[i - 1].ManeuverWindow.Value.EndDate)).ToArray();
        _output.WriteLine($"MEASURE #573 {tag} MinimumEpoch: {Fs(sc.Chain.Select(m => Tt(m.MinimumEpoch)))}, the given ones and the windows before them " +
                          $"{Fs(expected.Select(Tt))}");
        Assert.Equal(expected, sc.Chain.Select(m => m.MinimumEpoch).ToArray());
    }

    /// <summary>
    /// The fresh run on the Keplerian closed form: each impulse at the first apsis of its own at or after its gate, the
    /// later of its minimum epoch and the window start for the first (the window start when the orbit starts at that
    /// apsis, <paramref name="startsAtTheApsis"/>), the end of the previous maneuver's window for a chained one, on the
    /// orbit before it. RK7(8) within #577's apsis bound, Velocity-Verlet within [−limit, h + limit) of its centre.
    /// </summary>
    private void AssertOnTheClosedForm(string tag, Sc fresh, Run f, string integrator, bool startsAtTheApsis)
    {
        var env = fresh.Env;
        if (!env.ClosedForm) return;
        var impulses = fresh.Chain.OfType<ImpulseManeuver>().ToArray();
        Assert.Equal(impulses.Length, f.Burns.Length);
        var parts = new List<string>();
        for (int k = 0; k < impulses.Length; k++)
        {
            var m = impulses[k];
            int index = Array.IndexOf(fresh.Chain, m);
            bool apogee = m is PerigeeHeightManeuver;
            Assert.True(apogee || m is ApogeeHeightManeuver);
            double expected;
            if (k == 0 && index > 0)
            {
                // After leading attitudes, the end of the window of the last one (#539)
                var gate = fresh.Chain[index - 1].ManeuverWindow!.Value.EndDate;
                expected = ClosedForm(env, fresh.Spacecraft.InitialOrbitalParameters.ToStateVector(f.Window.StartDate)).NextApsis(env.Seconds(gate), apogee);
            }
            else if (k == 0)
            {
                var gate = Later(fresh.Given[index], f.Window.StartDate);
                expected = startsAtTheApsis && gate == f.Window.StartDate
                    ? env.Seconds(f.Window.StartDate)
                    : ClosedForm(env, fresh.Spacecraft.InitialOrbitalParameters.ToStateVector(f.Window.StartDate)).NextApsis(env.Seconds(gate), apogee);
            }
            else
            {
                var gate = fresh.Chain[index - 1].ManeuverWindow!.Value.EndDate;
                expected = ClosedForm(env, fresh.Spacecraft.PropagatedArcs[k][0]).NextApsis(env.Seconds(gate), apogee);
            }

            double error = env.Seconds(f.Burns[k]) - expected;
            parts.Add($"burn {k} − closed form {error:E3} s");
            if (integrator == "RK")
            {
                Assert.True(System.Math.Abs(error) <= MinimumEpochGateTests.Rk78Epoch["apsis"], $"{tag} burn {k}: {error:E3} s from the closed form");
            }
            else
            {
                double limit = MinimumEpochGateTests.VelocityVerletLagLimit[env.Moon ? "moon|VV1" : "apsis|VV1"], h = VvStep.TotalSeconds;
                Assert.InRange(error, -limit, h + limit);
                Assert.True(error < h + limit, $"{tag} burn {k}: {error:R} s from the closed form");
            }
        }

        _output.WriteLine($"MEASURE #573 {tag} fresh on the closed form ({integrator}): {string.Join("; ", parts)}");
    }

    // The closed form of a state relative to the centre, in ICRF, times in seconds from S (TT about the Earth, TDB about the Moon)
    private static KeplerClosedForm ClosedForm(Env env, StateVector state)
    {
        var local = state.RelativeTo(env.Center, Aberration.None).ToStateVector();
        if (local.Frame != Frame.ICRF) local = local.ToFrame(Frame.ICRF).ToStateVector();
        return new KeplerClosedForm(env.Center.GM, local.Position, local.Velocity, env.Seconds(local.Epoch));
    }

    /// <summary>A fresh spacecraft built as <paramref name="reused"/> was, from <paramref name="orbit"/>, run once.</summary>
    private (Sc Sc, Run Run) Fresh(string tag, Sc reused, OrbitalParams orbit, string chain, Window window, string integrator, string path,
        bool startsAtTheApsis, CovariancePropagationOptions covariance = null, Time? userGate = null)
    {
        var fresh = Build(reused.Env, orbit, chain, userGate);
        var f = Propagate(fresh, window, integrator, path, covariance);
        AssertOnTheClosedForm(tag, fresh, f, integrator, startsAtTheApsis);
        AssertGatesFollowTheWindows(tag + " fresh", fresh);
        return (fresh, f);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Run 2 from another state: run 1 from the apogee at S, run 2 from the perigee at S + 500
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// #573's case: after a run from the apogee at S, a run from the perigee at S + 500 fires as a fresh spacecraft, on every
    /// activation path of <c>Spacecraft.Propagate</c> and <c>BatchPropagator</c>, both integrators.
    /// </summary>
    [Theory]
    [InlineData("apo-apo", "RK", "Propagate")]
    [InlineData("apo-apo", "VV1", "Propagate")]
    [InlineData("apo-apo", "RK", "PropagateOptions")]
    [InlineData("apo-apo", "VV1", "Default")]
    [InlineData("apo-apo", "VV1", "Async")]
    [InlineData("apo-apo", "RK", "Batch")]
    [InlineData("apo-apo", "VV1", "Batch")]
    [InlineData("att1000", "RK", "Propagate")]
    [InlineData("att1000", "VV1", "Propagate")]
    [InlineData("att1000", "VV1", "Default")]
    [InlineData("att1000", "VV1", "Async")]
    [InlineData("att1000", "RK", "Batch")]
    [InlineData("att1000", "VV1", "Batch")]
    public void AnotherState_FiresAsAFreshSpacecraft(string chain, string integrator, string path)
    {
        // Arrange
        string tag = $"another state {chain} {integrator} {path}";
        var env = Env.Of();
        var s2 = S.AddSeconds(500.0);
        var reused = Build(env, env.Apogee(), chain);
        var r1 = Propagate(reused, Win(S), integrator, path);
        reused.Spacecraft.SetInitialOrbitalParameters(env.PerigeeAt(s2));
        // Between two runs, MinimumEpoch shows the gates of the last run, with its windows: SetInitialOrbitalParameters
        // clears nothing
        Assert.Equal(r1.Gates, reused.Chain.Select(m => m.MinimumEpoch).ToArray());

        // Act
        var r2 = Propagate(reused, Win(s2), integrator, path);

        // Assert
        var (fresh, f) = Fresh(tag, reused, env.PerigeeAt(s2), chain, Win(s2), integrator, path, true);
        AssertSame(tag, r2, f, reused, fresh);
        AssertGatesFollowTheWindows(tag + " reused", reused);
    }

    /// <summary>
    /// A control of the trajectory: <c>att600</c>, run 2 from another state as above. The gate run 1 leaves on the
    /// <c>PerigeeHeightManeuver</c> (3 568 s since #539, the nadir from the end of the first impulse's window; 3 563 s before)
    /// falls before run 2's apogee (3 783 s), so the burns, the state, the windows,
    /// <c>ToOpm</c>, the summary and the attitudes of run 2 were a fresh spacecraft's before #573's fix already. Its
    /// <c>MinimumEpoch</c> was not: <see cref="AnotherState_AStaleGateBeforeTheBurn_MinimumEpochIsAFreshSpacecrafts"/>.
    /// </summary>
    [Theory]
    [InlineData("RK")]
    [InlineData("VV1")]
    public void AnotherState_AStaleGateBeforeTheBurn_Control_TrajectoryIsAFreshSpacecrafts(string integrator)
    {
        var (reused, r2, fresh, f, tag) = AnotherStateAtt600(integrator, "(control of the trajectory)");
        AssertSame(tag, r2, f, reused, fresh, compareGates: false);
    }

    /// <summary>
    /// The same <c>att600</c> run 2: the trajectory does not move, but the <c>MinimumEpoch</c> a user reads after run 2 is
    /// the gate of run 2, a fresh spacecraft's, never the later gate run 1 left (2 968 and 3 568 s with the raise left on
    /// the lasting minimum epoch, against 505 and 1 105 s; 3 563 and 1 100 s before #539).
    /// </summary>
    [Theory]
    [InlineData("RK")]
    [InlineData("VV1")]
    public void AnotherState_AStaleGateBeforeTheBurn_MinimumEpochIsAFreshSpacecrafts(string integrator)
    {
        var (reused, r2, _, f, tag) = AnotherStateAtt600(integrator, "(MinimumEpoch)");
        _output.WriteLine($"MEASURE #573 {tag}: MinimumEpoch after run 2 {Fs(r2.Gates.Select(Tt))} against {Fs(f.Gates.Select(Tt))}");
        Assert.Equal(f.Gates, r2.Gates);
        AssertGatesFollowTheWindows(tag + " reused", reused);
    }

    private (Sc Reused, Run R2, Sc Fresh, Run F, string Tag) AnotherStateAtt600(string integrator, string label)
    {
        string tag = $"another state att600 {integrator} Propagate {label}";
        var env = Env.Of();
        var s2 = S.AddSeconds(500.0);
        var reused = Build(env, env.Apogee(), "att600");
        Propagate(reused, Win(S), integrator, "Propagate");
        reused.Spacecraft.SetInitialOrbitalParameters(env.PerigeeAt(s2));
        var r2 = Propagate(reused, Win(s2), integrator, "Propagate");
        var (fresh, f) = Fresh(tag, reused, env.PerigeeAt(s2), "att600", Win(s2), integrator, "Propagate", true);
        return (reused, r2, fresh, f, tag);
    }

    /// <summary>
    /// SSB mode, ECLIPJ2000 and the Moon (a TDB time argument): the fix holds no frame or time computation, so the second
    /// run is a fresh spacecraft's there too. About the Moon, a 100 × 300 km orbit and holds of 7 000 s. Run 2 starts at
    /// S + 500 with the perigee 10 s later: a start on the perigee itself takes it in ICRF, but the next one, a period later,
    /// once the state is rotated to ECLIPJ2000 or taken about the Moon (r·v just past its zero), the fresh spacecraft as
    /// the reused one; that start is not what this test measures.
    /// </summary>
    [Theory]
    [InlineData("Earth|ECLIPJ2000", "RK")]
    [InlineData("Earth|ECLIPJ2000", "VV1")]
    [InlineData("Earth|SSB", "RK")]
    [InlineData("Earth|SSB", "VV1")]
    [InlineData("Moon|ICRF", "RK")]
    [InlineData("Moon|ICRF", "VV1")]
    public void AnotherState_FramesAndCentres_FiresAsAFreshSpacecraft(string setup, string integrator)
    {
        // Arrange
        string tag = $"another state {setup.Replace('|', ' ')} apo-apo {integrator}";
        var env = Env.Of(setup);
        var s2 = S.AddSeconds(500.0);
        var reused = Build(env, env.Apogee(), "apo-apo");
        Propagate(reused, Win(S), integrator, "Propagate");
        reused.Spacecraft.SetInitialOrbitalParameters(env.PerigeeAfter(s2, 10.0));

        // Act
        var r2 = Propagate(reused, Win(s2), integrator, "Propagate");

        // Assert
        var (fresh, f) = Fresh(tag, reused, env.PerigeeAfter(s2, 10.0), "apo-apo", Win(s2), integrator, "Propagate", false);
        Assert.Equal(2, f.Burns.Length);
        AssertSame(tag, r2, f, reused, fresh);
        AssertGatesFollowTheWindows(tag + " reused", reused);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The gates the attitude timeline raises (#539, #582 choice 4): run 2 earlier than run 1, a crossing between the two
    // hold ends
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A leading attitude held 1 000 s gates the impulse after it (#539), for that propagation only (#582 choice 4): run 1
    /// from the perigee at S + 3 000 (the gate at S + 4 000), run 2 from S with the perigee 1 500 s later (the gate at
    /// S + 1 000), a crossing between the two hold ends. Run 2 fires at the perigee at S + 1 500 as a fresh spacecraft; with
    /// the raise left on the lasting minimum epoch (the two-argument <c>SetStandbyManeuver</c>), it fired one orbit late,
    /// from the stale gate (<c>wpr3/impl.md</c>). Every activation path of <c>Spacecraft.Propagate</c> and
    /// <c>BatchPropagator</c>, both integrators.
    /// </summary>
    [Theory]
    [InlineData("lead1000", "RK", "Propagate")]
    [InlineData("lead1000", "VV1", "Propagate")]
    [InlineData("lead1000", "RK", "PropagateOptions")]
    [InlineData("lead1000", "VV1", "Default")]
    [InlineData("lead1000", "VV1", "Async")]
    [InlineData("lead1000", "RK", "Batch")]
    [InlineData("lead2", "RK", "Propagate")]
    [InlineData("lead2", "VV1", "Propagate")]
    public void LeadingHold_RunTwoEarlier_FiresAsAFreshSpacecraft(string chain, string integrator, string path)
    {
        // Arrange
        string tag = $"leading hold, run 2 earlier {chain} {integrator} {path}";
        var env = Env.Of();
        var s1 = S.AddSeconds(3000.0);
        var reused = Build(env, env.PerigeeAt(s1), chain);
        Propagate(reused, Win(s1), integrator, path);
        reused.Spacecraft.SetInitialOrbitalParameters(env.PerigeeAfter(S, 1500.0));

        // Act
        var r2 = Propagate(reused, Win(S), integrator, path);

        // Assert
        var (fresh, f) = Fresh(tag, reused, env.PerigeeAfter(S, 1500.0), chain, Win(S), integrator, path, false);
        AssertLeadingAttitudesOnTheRules(tag, fresh, Win(S));
        AssertSame(tag, r2, f, reused, fresh);
        AssertGatesFollowTheWindows(tag + " reused", reused);
    }

    /// <summary>
    /// The same in SSB mode, ECLIPJ2000 and about the Moon (TDB seconds there), RK7(8) and Velocity-Verlet: the gate of the
    /// leading hold is that of this propagation in every frame and time argument.
    /// </summary>
    [Theory]
    [InlineData("Earth|ECLIPJ2000", "RK")]
    [InlineData("Earth|ECLIPJ2000", "VV1")]
    [InlineData("Earth|SSB", "RK")]
    [InlineData("Earth|SSB", "VV1")]
    [InlineData("Moon|ICRF", "RK")]
    [InlineData("Moon|ICRF", "VV1")]
    public void LeadingHold_FramesAndCentres_RunTwoEarlier_FiresAsAFreshSpacecraft(string setup, string integrator)
    {
        string tag = $"leading hold, run 2 earlier {setup.Replace('|', ' ')} lead1000 {integrator}";
        var env = Env.Of(setup);
        var s1 = S.AddSeconds(3000.0);
        var reused = Build(env, env.PerigeeAfter(s1, 10.0), "lead1000");
        Propagate(reused, Win(s1), integrator, "Propagate");
        reused.Spacecraft.SetInitialOrbitalParameters(env.PerigeeAfter(S, 1500.0));
        var r2 = Propagate(reused, Win(S), integrator, "Propagate");
        var (fresh, f) = Fresh(tag, reused, env.PerigeeAfter(S, 1500.0), "lead1000", Win(S), integrator, "Propagate", false);
        AssertLeadingAttitudesOnTheRules(tag, fresh, Win(S));
        AssertSame(tag, r2, f, reused, fresh);
        AssertGatesFollowTheWindows(tag + " reused", reused);
    }

    /// <summary>
    /// A control: the leading hold through <c>Scenario.SimulateAsync</c>, its three overloads, the covariance one with and
    /// without <c>IncludeInStepControl</c>. The window of a scenario is fixed, so the leading nadir activates at S in every
    /// simulation and raises the same gate (S + 1 000): runs 1 and 2 from an orbit whose perigee is at S + 3 000, run 3 from
    /// one whose perigee is at S + 1 500, each a fresh scenario's, summary included.
    /// </summary>
    [Theory]
    [InlineData("VV1", "Default")]
    [InlineData("RK", "Factory")]
    [InlineData("RK", "Covariance")]
    [InlineData("RK", "CovarianceStep")]
    public void LeadingHold_Scenario_SimulatedAgain_Control_FiresAsAFreshScenario(string integrator, string overload)
    {
        string tag = $"leading hold, scenario lead1000 {integrator} {overload}";
        var env = Env.Of();
        bool p0 = overload.StartsWith("Covariance");
        var window = Win(S);
        var reused = Build(env, Perigee(env, S, 3000.0, p0), "lead1000");
        var scenario = NewScenario(reused, window);
        var r1 = Simulate(scenario, reused, window, integrator, overload);
        var r2 = Simulate(scenario, reused, window, integrator, overload);
        reused.Spacecraft.SetInitialOrbitalParameters(Perigee(env, S, 1500.0, p0));
        var r3 = Simulate(scenario, reused, window, integrator, overload);
        AssertSame(tag + " run 2 (identical) against run 1", r2, r1);
        var fresh = Build(env, Perigee(env, S, 1500.0, p0), "lead1000");
        var f = Simulate(NewScenario(fresh, window), fresh, window, integrator, overload);
        AssertOnTheClosedForm(tag, fresh, f, integrator, false);
        AssertLeadingAttitudesOnTheRules(tag, fresh, window);
        AssertSame(tag + " run 3 against a fresh scenario", r3, f, reused, fresh);
        AssertGatesFollowTheWindows(tag + " reused", reused);
    }

    // The orbit whose perigee is lead seconds after epoch, with P0 when asked
    private static OrbitalParams Perigee(Env env, Time epoch, double lead, bool withP0)
    {
        var orbit = env.PerigeeAfter(epoch, lead).ToStateVector();
        if (!withP0) return orbit;
        var p0 = new Matrix(6, 6);
        for (int i = 0; i < 6; i++) p0.Set(i, i, i < 3 ? 100.0 : 1.0e-4);
        return new StateVector(orbit.Position, orbit.Velocity, orbit.Observer, orbit.Epoch, orbit.Frame, p0);
    }

    /// <summary>
    /// A control: run 2 from S + 3 500, later than run 1 (S + 3 000), from the same orbit: its gate (S + 4 500) is later than
    /// the stale one (S + 4 000), so run 2 was a fresh spacecraft's with the raise left on the lasting minimum epoch too.
    /// </summary>
    [Theory]
    [InlineData("RK")]
    [InlineData("VV1")]
    public void LeadingHold_RunTwoLater_Control_FiresAsAFreshSpacecraft(string integrator)
    {
        string tag = $"leading hold, run 2 later (control) lead1000 {integrator}";
        var env = Env.Of();
        var s1 = S.AddSeconds(3000.0);
        var reused = Build(env, env.PerigeeAt(s1), "lead1000");
        Propagate(reused, Win(s1), integrator, "Propagate");
        var r2 = Propagate(reused, Win(S.AddSeconds(3500.0)), integrator, "Propagate");
        var (fresh, f) = Fresh(tag, reused, env.PerigeeAt(s1), "lead1000", Win(S.AddSeconds(3500.0)), integrator, "Propagate", false);
        AssertLeadingAttitudesOnTheRules(tag, fresh, Win(S.AddSeconds(3500.0)));
        AssertSame(tag, r2, f, reused, fresh);
    }

    /// <summary>
    /// The leading attitudes of a fresh run on the rules of the timeline (<see cref="TimelineRules"/>): each activates at
    /// the later of its minimum epoch and the end of the previous one's hold, from the window start, its hold counted in TT
    /// seconds about the Earth, TDB about the Moon and in SSB mode; compared on Linux, within
    /// <see cref="TimelineRules.RuleBound"/>.
    /// </summary>
    private void AssertLeadingAttitudesOnTheRules(string tag, Sc fresh, Window window)
    {
        bool tt = !fresh.Env.Moon && !fresh.Env.Ssb;
        var previousEnd = window.StartDate;
        double worst = 0.0;
        for (int i = 0; i < fresh.Chain.Length && fresh.Chain[i] is Attitude attitude; i++)
        {
            var start = TimelineRules.Activation(fresh.Given[i], previousEnd);
            var end = TimelineRules.After(start, attitude.ManeuverHoldDuration.TotalSeconds, tt);
            Assert.True(attitude.ManeuverWindow.HasValue, $"{tag}: attitude {i} not executed");
            worst = System.Math.Max(worst, System.Math.Abs((attitude.ManeuverWindow.Value.StartDate - start).TotalSeconds));
            worst = System.Math.Max(worst, System.Math.Abs((attitude.ManeuverWindow.Value.EndDate - end).TotalSeconds));
            Assert.Contains(attitude, fresh.Spacecraft.ExecutedManeuvers);
            previousEnd = end;
        }

        _output.WriteLine($"MEASURE #539 {tag} leading attitudes - rules {worst:E2} s");
        if (TimelineRules.Checked)
        {
            Assert.True(worst <= TimelineRules.RuleBound, $"{tag}: leading attitudes - rules {worst:E3} s");
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Run 2 over another window from the same state, and the identical run
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The same state (the apogee at S), run 1 over [S + 3 000, + 6 h] (first burn at 8 890 s), run 2 over [S, + 6 h]
    /// (2 963 s): run 2 fires as a fresh spacecraft over [S, + 6 h].
    /// </summary>
    [Theory]
    [InlineData("apo-apo", "RK")]
    [InlineData("apo-apo", "VV1")]
    [InlineData("att1000", "RK")]
    [InlineData("att1000", "VV1")]
    public void EarlierWindow_FiresAsAFreshSpacecraft(string chain, string integrator)
    {
        // Arrange
        string tag = $"earlier window {chain} {integrator}";
        var env = Env.Of();
        var reused = Build(env, env.Apogee(), chain);
        Propagate(reused, Win(S.AddSeconds(3000.0)), integrator, "Propagate");

        // Act
        var r2 = Propagate(reused, Win(S), integrator, "Propagate");

        // Assert
        var (fresh, f) = Fresh(tag, reused, env.Apogee(), chain, Win(S), integrator, "Propagate", false);
        AssertSame(tag, r2, f, reused, fresh);
        AssertGatesFollowTheWindows(tag + " reused", reused);
    }

    /// <summary>
    /// A control: the same state, run 1 over [S, + 6 h], run 2 over [S + 500, + 6 h]. Run 2's first burn is run 1's, so is
    /// its gate: bit for bit a fresh spacecraft's before #573's fix already.
    /// </summary>
    [Theory]
    [InlineData("apo-apo", "RK")]
    [InlineData("apo-apo", "VV1")]
    [InlineData("att1000", "RK")]
    public void LaterWindow_Control_FiresAsAFreshSpacecraft(string chain, string integrator)
    {
        string tag = $"later window (control) {chain} {integrator}";
        var env = Env.Of();
        var reused = Build(env, env.Apogee(), chain);
        Propagate(reused, Win(S), integrator, "Propagate");
        var r2 = Propagate(reused, Win(S.AddSeconds(500.0)), integrator, "Propagate");
        var (fresh, f) = Fresh(tag, reused, env.Apogee(), chain, Win(S.AddSeconds(500.0)), integrator, "Propagate", false);
        AssertSame(tag, r2, f, reused, fresh);
    }

    /// <summary>A control: an identical second run (same state, same window) is bit for bit the first and a fresh spacecraft's.</summary>
    [Theory]
    [InlineData("apo-apo", "RK")]
    [InlineData("apo-apo", "VV1")]
    [InlineData("att1000", "RK")]
    [InlineData("att1000", "VV1")]
    public void IdenticalRun_Control_IsBitForBitTheFirst(string chain, string integrator)
    {
        string tag = $"identical (control) {chain} {integrator}";
        var env = Env.Of();
        var reused = Build(env, env.Apogee(), chain);
        var r1 = Propagate(reused, Win(S), integrator, "Propagate");
        var r2 = Propagate(reused, Win(S), integrator, "Propagate");
        AssertSame(tag + " run 2 against run 1", r2, r1);
        var (fresh, f) = Fresh(tag, reused, env.Apogee(), chain, Win(S), integrator, "Propagate", false);
        AssertSame(tag + " run 1 against fresh", r1, f);
        AssertSame(tag + " run 2 against fresh", r2, f, reused, fresh);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Scenario.SimulateAsync, its three overloads, twice and three times
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A scenario simulated twice is bit for bit, and a third time after <c>SetInitialOrbitalParameters</c> (the perigee at
    /// S: the window of a scenario is fixed) fires as a fresh scenario, summary included: the three overloads of
    /// <c>Scenario.SimulateAsync</c>, the covariance one with and without <c>IncludeInStepControl</c>.
    /// </summary>
    [Theory]
    [InlineData("apo-apo", "VV1", "Default")]
    [InlineData("apo-apo", "VV1", "Factory")]
    [InlineData("apo-apo", "RK", "Factory")]
    [InlineData("apo-apo", "RK", "Covariance")]
    [InlineData("apo-apo", "RK", "CovarianceStep")]
    [InlineData("att1000", "VV1", "Default")]
    [InlineData("att1000", "RK", "Factory")]
    public void Scenario_SimulatedAgain_FiresAsAFreshScenario(string chain, string integrator, string overload)
    {
        // Arrange
        string tag = $"scenario {chain} {integrator} {overload}";
        var env = Env.Of();
        bool p0 = overload.StartsWith("Covariance");
        var window = Win(S);
        var reused = Build(env, env.Apogee(p0), chain);
        var scenario = NewScenario(reused, window);
        var r1 = Simulate(scenario, reused, window, integrator, overload);

        // Act
        var r2 = Simulate(scenario, reused, window, integrator, overload);
        reused.Spacecraft.SetInitialOrbitalParameters(env.PerigeeAt(S, p0));
        var r3 = Simulate(scenario, reused, window, integrator, overload);

        // Assert
        AssertSame(tag + " run 2 (identical) against run 1", r2, r1);
        var fresh = Build(env, env.PerigeeAt(S, p0), chain);
        var f = Simulate(NewScenario(fresh, window), fresh, window, integrator, overload);
        AssertOnTheClosedForm(tag, fresh, f, integrator, true);
        AssertGatesFollowTheWindows(tag + " fresh", fresh);
        AssertSame(tag + " run 3 against a fresh scenario", r3, f, reused, fresh);
        AssertGatesFollowTheWindows(tag + " reused", reused);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Covariance mode
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Covariance mode, P0 = diag(100 m², 1e-4 m²/s²), through <c>Spacecraft.Propagate</c> and
    /// <c>PropagationTask.CovariancePropagation</c>, with and without <c>IncludeInStepControl</c>: run 2 from another state
    /// is a fresh spacecraft's, burns, state and P.
    /// </summary>
    [Theory]
    [InlineData("apo-apo", "PropagateOptions", false)]
    [InlineData("apo-apo", "PropagateOptions", true)]
    [InlineData("apo-apo", "Batch", false)]
    [InlineData("apo-apo", "Batch", true)]
    [InlineData("att1000", "PropagateOptions", false)]
    public void Covariance_AnotherState_FiresAsAFreshSpacecraft(string chain, string path, bool includeInStepControl)
    {
        // Arrange
        string tag = $"covariance {chain} {path}{(includeInStepControl ? " IncludeInStepControl" : "")}";
        var env = Env.Of();
        var s2 = S.AddSeconds(500.0);
        var options = new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance, IncludeInStepControl = includeInStepControl };
        var reused = Build(env, env.Apogee(true), chain);
        Propagate(reused, Win(S), "RK", path, options);
        reused.Spacecraft.SetInitialOrbitalParameters(env.PerigeeAt(s2, true));

        // Act
        var r2 = Propagate(reused, Win(s2), "RK", path, options);

        // Assert
        var (fresh, f) = Fresh(tag, reused, env.PerigeeAt(s2, true), chain, Win(s2), "RK", path, true, options);
        AssertSame(tag, r2, f, reused, fresh);
        AssertGatesFollowTheWindows(tag + " reused", reused);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // A minimum epoch given by the user
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>SetStandbyManeuver(m1, S + 4 000)</c> before run 1: both runs honour it, each as a fresh spacecraft given the same
    /// call, and <c>MinimumEpoch</c>, read before run 1, after it, after <c>SetInitialOrbitalParameters</c> and after run 2, is
    /// a fresh spacecraft's at the same step.
    /// </summary>
    [Theory]
    [InlineData("RK")]
    [InlineData("VV1")]
    public void UserGate_IsKeptByEveryPropagation(string integrator)
    {
        // Arrange
        string tag = $"user gate {integrator}";
        var env = Env.Of();
        var userGate = S.AddSeconds(4000.0);
        var s2 = S.AddSeconds(500.0);
        var reused = Build(env, env.Apogee(), "apo-apo", userGate);
        var fresh1 = Build(env, env.Apogee(), "apo-apo", userGate);
        var before = reused.Chain.Select(m => m.MinimumEpoch).ToArray();
        Assert.Equal(fresh1.Chain.Select(m => m.MinimumEpoch), before);
        Assert.Equal(userGate, before[0]);

        // Act
        var r1 = Propagate(reused, Win(S), integrator, "Propagate");
        reused.Spacecraft.SetInitialOrbitalParameters(env.PerigeeAt(s2));
        var between = reused.Chain.Select(m => m.MinimumEpoch).ToArray();
        var r2 = Propagate(reused, Win(s2), integrator, "Propagate");

        // Assert
        var f1 = Propagate(fresh1, Win(S), integrator, "Propagate");
        AssertOnTheClosedForm(tag + " run 1", fresh1, f1, integrator, false);
        AssertSame(tag + " run 1", r1, f1);
        Assert.Equal(r1.Gates, between);
        var (fresh2, f2) = Fresh(tag + " run 2", reused, env.PerigeeAt(s2), "apo-apo", Win(s2), integrator, "Propagate", true, userGate: userGate);
        _output.WriteLine($"MEASURE #573 {tag} MinimumEpoch as a user reads it: before run 1 {Fs(before.Select(Tt))}, after run 1 {Fs(r1.Gates.Select(Tt))}, " +
                          $"after SetInitialOrbitalParameters {Fs(between.Select(Tt))}, after run 2 {Fs(r2.Gates.Select(Tt))} (fresh {Fs(f2.Gates.Select(Tt))}); " +
                          $"first burn − user gate: run 1 {F(Tt(r1.Burns[0]) - Tt(userGate))} s, run 2 {F(Tt(r2.Burns[0]) - Tt(userGate))} s");
        Assert.Equal(userGate, r1.Gates[0]);
        Assert.Equal(userGate, r2.Gates[0]);
        Assert.True(r1.Burns[0] >= userGate && r2.Burns[0] >= userGate);
        AssertSame(tag + " run 2", r2, f2, reused, fresh2);
        AssertGatesFollowTheWindows(tag + " reused", reused);
    }

    /// <summary>
    /// A second call between two runs: an earlier minimum epoch (S + 3 000) changes nothing, a later one (S + 6 000) is the
    /// new minimum epoch of the maneuver, kept by run 2, which fires as a fresh spacecraft given S + 6 000.
    /// </summary>
    [Theory]
    [InlineData("RK")]
    [InlineData("VV1")]
    public void UserGate_ASecondCallBetweenRuns_RaisesIt(string integrator)
    {
        // Arrange
        string tag = $"user gate, second call {integrator}";
        var env = Env.Of();
        var s2 = S.AddSeconds(500.0);
        var reused = Build(env, env.Apogee(), "apo-apo", S.AddSeconds(4000.0));
        Propagate(reused, Win(S), integrator, "Propagate");
        reused.Spacecraft.SetInitialOrbitalParameters(env.PerigeeAt(s2));

        // Act
        reused.Spacecraft.SetStandbyManeuver(reused.Chain[0], S.AddSeconds(3000.0));
        var afterEarlier = reused.Chain[0].MinimumEpoch;
        reused.Spacecraft.SetStandbyManeuver(reused.Chain[0], S.AddSeconds(6000.0));
        var afterLater = reused.Chain[0].MinimumEpoch;
        reused.Given[0] = afterLater;
        var r2 = Propagate(reused, Win(s2), integrator, "Propagate");

        // Assert
        Assert.Equal(S.AddSeconds(4000.0), afterEarlier);
        Assert.Equal(S.AddSeconds(6000.0), afterLater);
        var (fresh, f) = Fresh(tag, reused, env.PerigeeAt(s2), "apo-apo", Win(s2), integrator, "Propagate", true, userGate: S.AddSeconds(6000.0));
        Assert.Equal(S.AddSeconds(6000.0), r2.Gates[0]);
        AssertSame(tag, r2, f, reused, fresh);
        AssertGatesFollowTheWindows(tag + " reused", reused);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // A rejected second propagation
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A second propagation rejected by the propagator's constructor (the covariance asked for without P0, process noise
    /// without the covariance, initial orbital parameters in a non-inertial frame), before the reset: the gates, the
    /// windows, <c>ExecutedManeuvers</c> and the solution of run 1 are left as they were. A valid run after it then fires
    /// as a fresh spacecraft.
    /// </summary>
    [Theory]
    [InlineData("CovarianceWithoutP0")]
    [InlineData("ProcessNoiseWithoutCovariance")]
    [InlineData("NonInertialFrame")]
    public void ARejectedSecondPropagation_LeavesTheFirstAsItWas(string rejection)
    {
        // Arrange
        string tag = $"rejected ({rejection})";
        var env = Env.Of();
        var s2 = S.AddSeconds(500.0);
        var reused = Build(env, env.Apogee(), "apo-apo");
        var r1 = Propagate(reused, Win(S), "RK", "Propagate");
        var solution = reused.Spacecraft.LastPropagation;
        var states = reused.Spacecraft.PropagatedStates.Select(s => (s.Epoch, s.Position, s.Velocity)).ToArray();
        var windows = reused.Chain.Select(m => (m.ManeuverWindow, m.ThrustWindow)).ToArray();

        // Act
        Exception error = rejection switch
        {
            "CovarianceWithoutP0" => Assert.Throws<InvalidOperationException>(() => reused.Spacecraft.Propagate(Win(S), env.Bodies, env.NewIntegrator("RK"), false,
                false, Output, new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance })),
            "ProcessNoiseWithoutCovariance" => Assert.Throws<ArgumentException>(() => reused.Spacecraft.Propagate(Win(S), env.Bodies, env.NewIntegrator("RK"), false,
                false, Output, new CovariancePropagationOptions { ProcessNoise = new RtnProcessNoise(1e-12, 1e-12, 1e-12) })),
            _ => RejectNonInertial(reused, env, s2)
        };

        // Assert
        _output.WriteLine($"MEASURE #573 {tag}: {error.GetType().Name}: {error.Message}");
        Assert.Same(solution, reused.Spacecraft.LastPropagation);
        Assert.Equal(states, reused.Spacecraft.PropagatedStates.Select(s => (s.Epoch, s.Position, s.Velocity)).ToArray());
        Assert.Equal(windows, reused.Chain.Select(m => (m.ManeuverWindow, m.ThrustWindow)).ToArray());
        var after = Capture(reused, solution, Win(S), reused.Spacecraft.GetSummary(), false);
        AssertSame(tag + " run 1 after the rejection against run 1", after, r1);

        // A valid run 2 from another state after the rejection
        reused.Spacecraft.SetInitialOrbitalParameters(env.PerigeeAt(s2));
        var r2 = Propagate(reused, Win(s2), "RK", "Propagate");
        var (fresh, f) = Fresh(tag, reused, env.PerigeeAt(s2), "apo-apo", Win(s2), "RK", "Propagate", true);
        AssertSame(tag + " run 2 after the rejection", r2, f, reused, fresh);
    }

    private static Exception RejectNonInertial(Sc sc, Env env, Time epoch)
    {
        var sv = env.PerigeeAt(epoch).ToStateVector();
        sc.Spacecraft.SetInitialOrbitalParameters(new StateVector(sv.Position, sv.Velocity, env.Center, epoch, new Frame("ITRF93")));
        return Assert.Throws<ArgumentException>(() => sc.Spacecraft.Propagate(Win(epoch), env.Bodies, env.NewIntegrator("RK"), false, false, Output));
    }
}
