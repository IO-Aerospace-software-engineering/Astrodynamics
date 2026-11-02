using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
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
/// The attitude and the states of a propagated spacecraft as the library reads them (the managed path) and as it writes them
/// (its CK and SPK), against CSPICE in the process on the kernels the library writes and against the closed forms of the
/// attitude laws (#523, #524, #529, #534, #535; choice 14 of the design of #537). Never a quaternion the library wrote and
/// read back, never a snapshot.
/// </summary>
/// <remarks>
/// <para>
/// <b>The readers.</b> The CK and the SPK are read by CSPICE through a kernel-backed spacecraft built on the written kernels:
/// its frame is <c>sxform(SC, J2000)</c> (<see cref="SpiceAPI.TransformFrame(Time, Frame, Frame)"/>: the rotation of
/// <c>pxform</c> and the angular velocity of <c>xf2rav</c>, ICRF relative to the spacecraft in its axes), its positions
/// <c>spkezr</c>. The instruments are read by <c>pxform(instrument, J2000)</c> on the frame kernels the library writes.
/// </para>
/// <para>
/// <b>The closed forms</b>, written in the test: nadir, the minimal rotation (Rodrigues) taking the front +Y to -r;
/// prograde, to v; TRIAD, the rotation taking the triad of the body vectors to that of the directions; the instrument
/// pointing, the minimal rotation taking the boresight in the spacecraft frame (<c>pxform(instrument, SC)</c>) to the Moon;
/// the impulse, the front along the delta-V rotated to ICRF by <c>pxform</c>. The state they take: the solution of the
/// propagation, rotated from its frame to ICRF by <c>pxform</c> (the SPK's Lagrange interpolation across an impulse is
/// off by up to 15 m over the steps around it, item 3 of §8 of the design, out of scope), or the SPK's (<c>spkezr</c>)
/// where the propagation keeps no solution (the Velocity-Verlet path of <c>Scenario.SimulateAsync</c>, whose events are
/// on its output grid) and in SSB mode. The directions of the Sun and the Moon: <c>spkezr</c> from the Earth, geometric,
/// as the attitudes read them (<c>Aberration.None</c>; for the instrument pointing, whose light time is not applied
/// to a celestial target, a defect outside #537 with its own issue).
/// </para>
/// <para>
/// <b>The case</b> of the prototype probe: an inclined LEO (a = 6 778 137 m, e = 0.001, i = 51.6°) from 2024-03-01 12:00
/// TDB, one hour, 10 s outputs, the Earth, the Moon and the Sun; Velocity-Verlet at the output step or RK7(8) at 1e-12;
/// the six inertial initial frames F; the paths of <c>Spacecraft.Propagate</c> and <c>Scenario.SimulateAsync</c>; SSB mode;
/// an epoch after 2025-01-19, where the spacing of the encoded SCLK doubles. Each spacecraft carries three instruments,
/// their Euler angles (0.3, 0.5, 0.2), (0, π/2, 0) and (-0.7, 1.1, 2.3).
/// </para>
/// <para>
/// <b>The leads.</b> The CK repeats the earlier attitude 1 µs before each discrete change and ramps over that microsecond
/// (choice 2): the comparisons of the managed path with the CK leave out [change - 1.5 µs, change + 0.3 µs], the lead
/// and the rounding of the encoded SCLK of its records.
/// </para>
/// <para>
/// <b>Limits.</b> Twice the worst over the grid, PROVISIONAL: measured on Linux x64 only (2026-11-02), to be set from
/// the MEASURE lines of the Windows and macOS arm64 CI logs. The cases at the worst of each limit run in every CI job;
/// the rest of the grid is in the Validation category (Linux).
/// </para>
/// </remarks>
public class AttitudeRecordTests
{
    // PROVISIONAL limits: twice the worst over the grid of 114 cases on Linux x64 (2026-11-02, MEASURE lines), to be set
    // from the Windows and macOS arm64 CI logs; the case of each worst is in WorstCase_MatchesSpice

    /// <summary>The managed rotation against the CK (<c>pxform</c>), off the leads, before 2025-01-19: the encoded SCLK's
    /// spacing (2.4e-7 s) times the angular velocity. Worst 6.14e-10 rad (Prograde, VV, ICRF).</summary>
    internal const double ManagedAgainstCk = 1.3e-9;

    /// <summary>The same from 2025-01-19, where the spacing of the encoded SCLK doubles (4.8e-7 s). Worst 1.29e-9 rad
    /// (Prograde, RK7(8), ECLIPJ2000, 2026).</summary>
    internal const double ManagedAgainstCkAfter2025 = 2.6e-9;

    /// <summary>The managed angular velocity against <c>xf2rav(sxform(SC, J2000))</c> on the CK, off the leads, rad/s.
    /// Worst 9.86e-13 rad/s (InstrumentTriad, RK7(8), B1950).</summary>
    internal const double RateAgainstCk = 2.0e-12;

    /// <summary>The same from 2025-01-19, rad/s. Worst 1.45e-12 rad/s (Prograde, RK7(8), ECLIPJ2000, 2026).</summary>
    internal const double RateAgainstCkAfter2025 = 2.9e-12;

    /// <summary>The CK at the entries of a continuous law (its samples) against the closed form of the law. Worst 1.64e-11
    /// rad (Nadir, SSB mode, ICRF, the law on spkezr's state; 1e-15 on the solution's).</summary>
    internal const double LawAtEntries = 3.3e-11;

    /// <summary>The CK between the samples of a continuous law against its closed form: the residual of the SLERP between
    /// samples 10 s apart, a property of the step (choice 1). Worst 5.73e-5 rad (InstrumentTriad, RK7(8), B1950).</summary>
    internal const double LawBetweenEntries = 1.2e-4;

    /// <summary>The CK of a discrete attitude (the identity, an impulse's, a TRIAD held from its change) against its closed
    /// form, off the leads. Worst 4.83e-16 rad (ImpulseTriad, RK7(8), ICRF, Scenario).</summary>
    internal const double DiscreteLaw = 9.7e-16;

    /// <summary>The boresight and the reference vector of an instrument in ICRF against <c>pxform(instrument, J2000)</c>,
    /// before 2025-01-19 (the floor of the CK). Worst 6.13e-10 rad (Prograde, VV, ICRF); 1.29e-9 rad from 2025-01-19,
    /// <see cref="ManagedAgainstCkAfter2025"/>.</summary>
    internal const double InstrumentAgainstFrameKernel = 1.3e-9;

    /// <summary>The managed positions against <c>spkezr</c> on the SPK, m: the library's Lagrange interpolation against the
    /// SPK's, between the 10 s outputs. Worst 7.37e-4 m (Nadir, RK7(8), ICRF, and every frame alike).</summary>
    internal const double PositionAgainstSpk = 1.5e-3;

    /// <summary>The same in SSB mode, m. Worst 2.26e-2 m (Nadir, VV, ECLIPJ2000).</summary>
    internal const double PositionAgainstSpkSsb = 4.6e-2;

    private const double Length = 3600.0;
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(10.0);
    private static readonly Time Min = new(DateTime.MinValue, TimeFrame.TDBFrame);

    private static readonly Vector3[] InstrumentOrientations = [new(0.3, 0.5, 0.2), new(0.0, System.Math.PI / 2.0, 0.0), new(-0.7, 1.1, 2.3)];

    internal static readonly string[] Frames = ["ICRF", "ECLIPJ2000", "B1950", "FK4", "ECLIPB1950", "GALACTIC"];

    private static readonly string[] Laws = ["None", "Nadir", "Prograde", "ImpulseAlone", "ImpulseTriad", "NadirImpulseTriad", "InstrumentTriad", "InstrumentPointing"];

    private readonly ITestOutputHelper _output;

    public AttitudeRecordTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    internal static Frame FrameOf(string name) => name == "ICRF" ? Frame.ICRF : new Frame(name);

    // Every case of the grid, in a fixed order: its index gives the NAIF identifier of its spacecraft
    private static readonly string[] AllCases = BuildCases();

    private static string[] BuildCases()
    {
        var cases = new List<string>();
        foreach (var law in Laws)
        foreach (var integrator in new[] { "VV", "RK78" })
        foreach (var frame in Frames)
            cases.Add($"{law}|{integrator}|{frame}|Propagate|2024");

        foreach (var law in new[] { "Nadir", "ImpulseTriad" })
        foreach (var integrator in new[] { "VV", "RK78" })
        foreach (var frame in new[] { "ICRF", "ECLIPJ2000", "GALACTIC" })
            cases.Add($"{law}|{integrator}|{frame}|Scenario|2024");

        foreach (var frame in new[] { "ICRF", "ECLIPJ2000", "GALACTIC" })
            cases.Add($"Nadir|VV|{frame}|SSB|2024");

        // An epoch after 2025-01-19 03:13 TDB, where the spacing of the encoded SCLK doubles
        foreach (var (law, integrator) in new[] { ("Nadir", "VV"), ("Prograde", "RK78"), ("NadirImpulseTriad", "RK78") })
            cases.Add($"{law}|{integrator}|ECLIPJ2000|Propagate|2026");
        return cases.ToArray();
    }

    // The cases at the worst of each limit (Linux x64, 2026-11-02), which run in every CI job
    private static readonly string[] WorstCases =
    [
        "Prograde|VV|ICRF|Propagate|2024", // managed - CK, instruments
        "InstrumentTriad|RK78|B1950|Propagate|2024", // rate, law between entries
        "Nadir|RK78|ICRF|Propagate|2024", // positions
        "Nadir|VV|ICRF|SSB|2024", // law at entries
        "Nadir|VV|ECLIPJ2000|SSB|2024", // positions in SSB mode
        "ImpulseTriad|RK78|ICRF|Scenario|2024", // discrete law
        "Prograde|RK78|ECLIPJ2000|Propagate|2026" // managed - CK, rate and instruments from 2025-01-19
    ];

    public static IEnumerable<object[]> Grid() => AllCases.Where(x => !WorstCases.Contains(x)).Select(x => new object[] { x });

    public static IEnumerable<object[]> Worst() => WorstCases.Select(x => new object[] { x });

    [Theory]
    [MemberData(nameof(Worst))]
    public void WorstCase_MatchesSpice(string key) => Check(key);

    [Theory]
    [Trait("Category", "Validation")]
    [MemberData(nameof(Grid))]
    public void Case_MatchesSpice(string key) => Check(key);

    private void Check(string key)
    {
        var parts = key.Split('|');
        string law = parts[0], integrator = parts[1], frame = parts[2], path = parts[3];
        int year = int.Parse(parts[4]);
        int index = Array.IndexOf(AllCases, key);
        Assert.True(index >= 0, key);
        using var run = PropagatedCase.Run(-(8100 + index), law, integrator, FrameOf(frame), path, year);
        var m = Measure(run);
        string label = $"{law} {integrator} {frame} {path} {year}";
        _output.WriteLine($"MEASURE #537 attitude {label}: managed - CK {m.ManagedAgainstCk:E2} rad (lead {m.ManagedAgainstCkLead:E2}), rate {m.RateAgainstCk:E2} rad/s, " +
                          $"law at entries {m.LawAtEntries:E2}, between {m.LawBetweenEntries:E2}, discrete {m.DiscreteLaw:E2} rad, instruments {m.Instruments:E2} rad, " +
                          $"positions {m.Positions:E2} m, {m.Reads} reads, {m.Entries} entries");

        Assert.True(m.ManagedAgainstCk <= (year >= 2026 ? ManagedAgainstCkAfter2025 : ManagedAgainstCk), $"managed - CK {m.ManagedAgainstCk:E3} rad");
        Assert.True(m.RateAgainstCk <= (year >= 2026 ? RateAgainstCkAfter2025 : RateAgainstCk), $"rate {m.RateAgainstCk:E3} rad/s");
        Assert.True(m.LawAtEntries <= LawAtEntries, $"law at the entries {m.LawAtEntries:E3} rad");
        Assert.True(m.LawBetweenEntries <= LawBetweenEntries, $"law between the entries {m.LawBetweenEntries:E3} rad");
        Assert.True(m.DiscreteLaw <= DiscreteLaw, $"discrete law {m.DiscreteLaw:E3} rad");
        Assert.True(m.Instruments <= (year >= 2026 ? ManagedAgainstCkAfter2025 : InstrumentAgainstFrameKernel), $"instruments {m.Instruments:E3} rad");
        Assert.True(m.Positions <= (path == "SSB" ? PositionAgainstSpkSsb : PositionAgainstSpk), $"positions {m.Positions:E3} m");
        Assert.True(m.RecordUnchanged, "a read changed the attitude record (#523)");
        // The law was compared where it applies: every case but the impulse alone samples or holds one
        Assert.True(m.LawChecks > 0);
    }

    /// <summary>What a case measures.</summary>
    internal sealed record Measures
    {
        public double ManagedAgainstCk { get; set; }
        public double ManagedAgainstCkLead { get; set; }
        public double RateAgainstCk { get; set; }
        public double LawAtEntries { get; set; }
        public double LawBetweenEntries { get; set; }
        public double DiscreteLaw { get; set; }
        public double Instruments { get; set; }
        public double Positions { get; set; }
        public bool RecordUnchanged { get; set; }
        public int Reads { get; set; }
        public int Entries { get; set; }
        public int LawChecks { get; set; }
    }

    internal static Measures Measure(PropagatedCase run)
    {
        var sc = run.Spacecraft;
        var record = sc.Frame.GetStateOrientationsToICRF().ToArray();
        var entries = new HashSet<Time>(record.Select(x => x.Epoch));
        var changes = run.DiscreteChanges();
        var epochs = run.ReadEpochs(record);
        var m = new Measures { Entries = record.Length, Reads = epochs.Count };
        var instrumentFrames = sc.Instruments.Select(x => new Frame(sc.Name.ToUpper() + "_" + x.Name.ToUpper())).ToArray();
        // The positions do not depend on the attitude: off the impulses, whose output steps the library's Lagrange interpolation
        // and the SPK's both cross (item 3 of §8 of the design, out of scope)
        bool positions = !run.Law.Contains("Impulse") && run.Law != "Custom";

        foreach (var t in epochs)
        {
            bool lead = changes.Any(c => (t - c).TotalSeconds >= -1.5e-6 && (t - c).TotalSeconds <= 0.3e-6);
            var managed = sc.Frame.GetStateOrientationToICRF(t);
            var ck = run.Kernel.Frame.GetStateOrientationToICRF(t);
            double angle = RotationAngle(managed.Rotation, ck.Rotation);
            if (lead)
            {
                m.ManagedAgainstCkLead = System.Math.Max(m.ManagedAgainstCkLead, angle);
                continue;
            }

            m.ManagedAgainstCk = System.Math.Max(m.ManagedAgainstCk, angle);
            m.RateAgainstCk = System.Math.Max(m.RateAgainstCk, (managed.AngularVelocity - ck.AngularVelocity).Magnitude());

            for (int i = 0; i < instrumentFrames.Length; i++)
            {
                var instrument = sc.Instruments.ElementAt(i);
                var q = SpiceAPI.Instance.TransformFrame(t, instrumentFrames[i], Frame.ICRF).Rotation;
                m.Instruments = System.Math.Max(m.Instruments, Angle(instrument.GetBoresightInICRFFrame(t), instrument.Boresight.Rotate(q)));
                m.Instruments = System.Math.Max(m.Instruments, Angle(instrument.GetRefVectorInICRFFrame(t), instrument.RefVector.Rotate(q)));
            }

            if (positions)
            {
                var managedState = sc.GetGeometricStateRelativeTo(t, run.Earth);
                var spk = run.Kernel.GetGeometricStateRelativeTo(t, run.Earth);
                m.Positions = System.Math.Max(m.Positions, (managedState.Position - spk.Position).Magnitude());
            }

            var (law, continuous) = run.LawAt(t);
            if (law == null)
            {
                continue;
            }

            m.LawChecks++;
            double fromLaw = RotationAngle(law, v => v.Rotate(ck.Rotation));
            if (!continuous)
            {
                m.DiscreteLaw = System.Math.Max(m.DiscreteLaw, fromLaw);
            }
            else if (entries.Contains(t))
            {
                m.LawAtEntries = System.Math.Max(m.LawAtEntries, fromLaw);
            }
            else
            {
                m.LawBetweenEntries = System.Math.Max(m.LawBetweenEntries, fromLaw);
            }
        }

        m.RecordUnchanged = record.SequenceEqual(sc.Frame.GetStateOrientationsToICRF());
        return m;
    }


    #region The additions of the review and the reads of #523

    /// <summary>
    /// Reads before the propagation (an instrument's field of view before the start, an orientation 100 s in), during it (on
    /// another thread) and after it (every output and 3.3 s after it, past the end, a managed field-of-view search) leave the
    /// attitude record and the CK of a twin without reads (#523): on the base, the CK could not be written (ICRF,
    /// SPICE(TIMESOUTOFORDER)) or aborted the process (ECLIPJ2000).
    /// </summary>
    [Theory]
    [InlineData("ICRF", -8401)]
    [InlineData("ECLIPJ2000", -8403)]
    public void Reads_BeforeDuringAfter_LeaveTheRecordAndTheCk(string frame, int naifId)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var sun = new CelestialBody(Stars.Sun);
        int during = 0;
        var hooks = new PropagatedCase.Hooks
        {
            Before = sc =>
            {
                var start = sc.InitialOrbitalParameters.Epoch;
                sc.Instruments.First().IsInFOV(start.AddSeconds(-10.0), earth, Aberration.None);
                sc.GetOrientation(Frame.ICRF, start.AddSeconds(100.0));
                sc.Frame.GetStateOrientationToICRF(start.AddSeconds(-10.0));
            },
            During = (sc, token) =>
            {
                var start = sc.InitialOrbitalParameters.Epoch;
                while (!token.IsCancellationRequested)
                {
                    sc.Frame.GetStateOrientationToICRF(start.AddSeconds(during % 3700 + 0.37));
                    during++;
                }
            },
            After = sc =>
            {
                var start = sc.InitialOrbitalParameters.Epoch;
                var instrument = sc.Instruments.First();
                instrument.IsInFOV(start.AddSeconds(-10.0), earth, Aberration.None);
                for (int k = 0; k <= 360; k++)
                {
                    instrument.IsInFOV(start.AddSeconds(k * 10.0), earth, Aberration.None);
                    instrument.IsInFOV(start.AddSeconds(k * 10.0 + 3.3), earth, Aberration.None);
                }

                instrument.GetBoresightInICRFFrame(start.AddSeconds(Length + 100.0));
                instrument.FindWindowsInFieldOfViewConstraint(new Window(start.AddSeconds(10.0), start.AddSeconds(Length - 10.0)), sc, sun, Frame.ICRF,
                    ShapeType.Point, Aberration.None, TimeSpan.FromSeconds(60.0)).ToArray();
            }
        };
        using var read = PropagatedCase.Run(naifId, "Nadir", "VV", FrameOf(frame), "Propagate", 2024, hooks);
        using var twin = PropagatedCase.Run(naifId - 1, "Nadir", "VV", FrameOf(frame), "Propagate", 2024);
        _output.WriteLine($"{frame}: {during} reads during the propagation");

        var a = read.Spacecraft.Frame.GetStateOrientationsToICRF().ToArray();
        var b = twin.Spacecraft.Frame.GetStateOrientationsToICRF().ToArray();
        Assert.Equal(b.Length, a.Length);
        for (int i = 0; i < a.Length; i++)
        {
            Assert.Equal(b[i].Epoch, a[i].Epoch);
            Assert.Equal(b[i].Rotation, a[i].Rotation);
            Assert.Equal(b[i].AngularVelocity, a[i].AngularVelocity);
        }

        // The two CKs, read by pxform every 0.1 s: the same attitude, to the bit
        double worst = 0.0;
        for (int k = 0; k <= 36000; k++)
        {
            var t = read.Start.AddSeconds(k * 0.1);
            worst = System.Math.Max(worst, RotationAngle(read.Kernel.Frame.GetStateOrientationToICRF(t).Rotation, twin.Kernel.Frame.GetStateOrientationToICRF(t).Rotation));
        }

        _output.WriteLine($"MEASURE #523 reads {frame}: CK with reads - CK of the twin {worst:E2} rad");
        Assert.Equal(0.0, worst);
    }

    // PROVISIONAL: twice the worst over the partial step before the impulse (Linux x64, 2026-11-02: 1.23e-4 rad, the managed
    // path and the CK alike), the residual of the SLERP between the last output sample (60 s step) and the law's sample 1 µs
    // before the change; held, it was 4.70e-2 rad (the review's probe on the prototype; 7.11e-2 rad on the base)
    internal const double PartialStepLaw = 2.5e-4;

    /// <summary>
    /// The partial step before a discrete change off the output grid (RK7(8), 60 s outputs, nadir then an impulse at about
    /// S + 2 739.5 s): the attitude follows the nadir law up to the change, the law's sample 1 µs before it (choice 1), in
    /// the managed path and in the CK, no longer the last output sample held.
    /// </summary>
    [Fact]
    public void PartialStepBeforeAnRk78Event_FollowsTheLaw()
    {
        using var run = PropagatedCase.Run(-8411, "NadirImpulseTriad", "RK78", Frame.ICRF, "Propagate", 2024, new PropagatedCase.Hooks { Step = TimeSpan.FromSeconds(60.0) });
        var change = run.Change;
        var record = run.Spacecraft.Frame.GetStateOrientationsToICRF().ToArray();
        // The entry before the lead: the last output sample of the law before the change
        var lead = record.Single(x => x.Epoch == change.AddSeconds(-1e-6));
        var lastSample = record.Last(x => x.Epoch < lead.Epoch);
        double span = (change - lastSample.Epoch).TotalSeconds;
        Assert.InRange(span, 1.0, 59.0);

        double managed = 0.0, ck = 0.0;
        var epochs = Enumerable.Range(0, (int)(span / 0.25)).Select(k => lastSample.Epoch.AddSeconds(k * 0.25)).Append(change.AddSeconds(-1e-3)).Append(change.AddSeconds(-2e-6));
        foreach (var t in epochs.Where(x => x < change.AddSeconds(-1.5e-6)))
        {
            var (law, continuous) = run.LawAt(t);
            Assert.True(continuous);
            managed = System.Math.Max(managed, RotationAngle(law, v => v.Rotate(run.Spacecraft.Frame.GetStateOrientationToICRF(t).Rotation)));
            ck = System.Math.Max(ck, RotationAngle(law, v => v.Rotate(run.Kernel.Frame.GetStateOrientationToICRF(t).Rotation)));
        }

        double atLead = RotationAngle(run.LawAt(lead.Epoch).Law, v => v.Rotate(lead.Rotation));
        _output.WriteLine($"MEASURE #529 partial step before the impulse at S + {(change - run.Start).TotalSeconds:F6} s ({span:F3} s): managed - nadir {managed:E2} rad, CK - nadir {ck:E2} rad, the lead sample - nadir {atLead:E2} rad");
        Assert.True(managed <= PartialStepLaw, $"managed {managed:E3} rad");
        Assert.True(ck <= PartialStepLaw, $"CK {ck:E3} rad");
        Assert.True(atLead <= LawAtEntries, $"lead {atLead:E3} rad");
        var m = Measure(run);
        Assert.True(m.ManagedAgainstCk <= ManagedAgainstCk, $"managed - CK {m.ManagedAgainstCk:E3} rad");
        Assert.True(m.RateAgainstCk <= RateAgainstCk, $"rate {m.RateAgainstCk:E3} rad/s");
    }

    /// <summary>
    /// A chain whose held attitude ends within 2 µs of an output sample: nadir, then an impulse, a TRIAD held 290, 300 or
    /// 600 s, then a prograde attitude, Velocity-Verlet at 10 s. The activation at tc + hold, counted in TDB seconds, falls
    /// 1.2e-7 to 2.4e-7 s from an output sample of the TT grid (#501): the record keeps the activation at its epoch and
    /// drops the sample (choice 3), so the CK is written (base: SPICE(TIMESOUTOFORDER); prototype: its spacing check).
    /// </summary>
    [Theory]
    [InlineData(290.0, -8421)]
    [InlineData(300.0, -8422)]
    [InlineData(600.0, -8423)]
    public void NearEpochChain_WritesItsCk(double hold, int naifId)
    {
        var hooks = new PropagatedCase.Hooks
        {
            Configure = (sc, engine, earth, sun, start) =>
            {
                var nadir = new NadirAttitude(earth, Min, TimeSpan.Zero, engine);
                var impulse = new ApogeeHeightManeuver(earth, start.AddSeconds(100.0), TimeSpan.Zero, 7200000.0, engine);
                var triad = new TriadAttitude(start.AddSeconds(100.0), TimeSpan.FromSeconds(hold), Spacecraft.Front, earth, Spacecraft.Up, sun, engine);
                var prograde = new ProgradeAttitude(earth, start.AddSeconds(100.0), TimeSpan.Zero, engine);
                nadir.SetNextManeuver(impulse);
                impulse.SetNextManeuver(triad);
                triad.SetNextManeuver(prograde);
                sc.SetStandbyManeuver(nadir);
            }
        };
        // The impulse of the chain needs the orbit below circular: the grid's impulse orbit
        using var run = PropagatedCase.Run(naifId, "Custom", "VV", Frame.ICRF, "Propagate", 2024, hooks);
        var record = run.Spacecraft.Frame.GetStateOrientationsToICRF().ToArray();
        var gaps = record.Zip(record.Skip(1), (x, y) => (y.Epoch - x.Epoch).TotalSeconds).ToArray();
        double smallest = gaps.Min();
        var activation = run.Change.AddSeconds(hold);
        double nearest = record.Min(x => System.Math.Abs((x.Epoch - activation).TotalSeconds));
        // The output sample the activation replaced: an output epoch (a stored state) within 2 µs of it, not in the record
        var output = run.Spacecraft.PropagatedStates.Select(x => x.Epoch).OrderBy(x => System.Math.Abs((x - activation).TotalSeconds)).First();
        double outputGap = System.Math.Abs((output - activation).TotalSeconds);
        var m = Measure(run);
        _output.WriteLine($"MEASURE #534 chain held {hold} s: smallest gap {smallest:E3} s, the entry nearest tc + hold {nearest:E3} s from it, the output sample {outputGap:E3} s, managed - CK {m.ManagedAgainstCk:E2} rad, rate {m.RateAgainstCk:E2} rad/s");
        // The lead (1 µs) and the entries at least 2 µs apart
        Assert.True(gaps.All(x => x >= SpacecraftFrame.SameEpochTolerance || System.Math.Abs(x - SpacecraftFrame.DiscreteChangeLead) < 1e-9), $"smallest gap {smallest:E3} s");
        Assert.True(nearest < 1e-6, $"the activation is {nearest:E3} s from the nearest entry");
        Assert.InRange(outputGap, 1e-8, SpacecraftFrame.SameEpochTolerance);
        Assert.DoesNotContain(record, x => x.Epoch == output);
        Assert.True(m.ManagedAgainstCk <= ManagedAgainstCk, $"managed - CK {m.ManagedAgainstCk:E3} rad");
        Assert.True(m.RateAgainstCk <= RateAgainstCk, $"rate {m.RateAgainstCk:E3} rad/s");
        Assert.True(m.RecordUnchanged);
    }

    // PROVISIONAL: twice the worst over the frames and the attitudes (Linux x64, 2026-11-02: 6.01e-16 rad, InstrumentPointing
    // from B1950)
    internal const double TryExecuteLaw = 1.3e-15;

    /// <summary>
    /// <c>Attitude.TryExecute</c> (public) on a state in each inertial frame: the rotation from the spacecraft frame to ICRF,
    /// labelled ICRF, that of the closed form of the law on the same orbit in ICRF (#529, #535; choice 6).
    /// </summary>
    [Theory]
    [MemberData(nameof(FrameNames))]
    public void AttitudeTryExecute_FromEachFrame_IsTheRotationToIcrf(string frame)
    {
        var start = new Time(2024, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, start);
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, start);
        var sun = new CelestialBody(Stars.Sun, Frame.ICRF, start);
        var leo = new KeplerianElements(6778137.0, 0.001, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 0.4, 1.1, 0.3, earth, start, Frame.ICRF).ToStateVector();
        var f = FrameOf(frame);
        var inF = f == Frame.ICRF ? leo : leo.ToFrame(f).ToStateVector();
        int id = -(8431 + Array.IndexOf(Frames, frame));
        var sc = new Spacecraft(id, $"TX{-id}", 1000.0, 3000.0, new Clock($"TX{-id}_CLK", 65536), inF);
        var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
        sc.AddFuelTank(tank);
        var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
        sc.AddEngine(engine);
        sc.AddCircularInstrument(id * 1000 - 1, "I1", "m", 0.5, Vector3.VectorZ, Vector3.VectorY, InstrumentOrientations[0]);
        var instrument = sc.Instruments.First();
        Vector3 FromEarth(CelestialItem body) => SpiceAPI.Instance.ReadEphemeris(start, 399, body.NaifId, Frame.ICRF, Aberration.None).ToStateVector().Position;
        var boresight = EulerToSpacecraft(instrument.Orientation, instrument.Boresight);
        var reference = EulerToSpacecraft(instrument.Orientation, instrument.RefVector);
        var cases = new (string, Attitude, Func<Vector3, Vector3>)[]
        {
            ("Nadir", new NadirAttitude(earth, Min, TimeSpan.Zero, engine), MinimalRotation(Spacecraft.Front, leo.Position.Inverse())),
            ("Prograde", new ProgradeAttitude(earth, Min, TimeSpan.Zero, engine), MinimalRotation(Spacecraft.Front, leo.Velocity)),
            ("Triad", new TriadAttitude(Min, TimeSpan.Zero, Spacecraft.Front, earth, Spacecraft.Up, sun, engine),
                Triad(Spacecraft.Front, Spacecraft.Up, leo.Position.Inverse(), FromEarth(sun) - leo.Position)),
            ("InstrumentTriad", new TriadAttitude(Min, TimeSpan.Zero, instrument, earth, sun, engine),
                Triad(boresight, reference, leo.Position.Inverse(), FromEarth(sun) - leo.Position)),
            ("InstrumentPointing", new InstrumentPointingToAttitude(Min, TimeSpan.Zero, instrument, moon, engine), MinimalRotation(boresight, FromEarth(moon) - leo.Position))
        };
        foreach (var (name, attitude, law) in cases)
        {
            var (_, orientation) = attitude.TryExecute(inF);
            Assert.Equal(Frame.ICRF, orientation.ReferenceFrame);
            Assert.Equal(start, orientation.Epoch);
            double angle = RotationAngle(law, v => v.Rotate(orientation.Rotation));
            _output.WriteLine($"MEASURE #535 TryExecute {name} from {frame}: {angle:E2} rad");
            Assert.True(angle <= TryExecuteLaw, $"{name}: {angle:E3} rad");
        }
    }

    public static IEnumerable<object[]> FrameNames() => Frames.Select(x => new object[] { x });

    // PROVISIONAL: twice the worst (Linux x64, 2026-11-02: 3.73e-9 m, 3.28e-12 m/s), m and m/s
    internal const double TlePosition = 7.5e-9;
    internal const double TleVelocity = 6.6e-12;

    /// <summary>
    /// <c>TLEPropagator</c> (public): its SPK is written in J2000 (on the base, SPICE(INVALIDREFFRAME), TEME), and holds the
    /// SGP4 states in ICRF, read by <c>spkezr</c> at its records. Its CK is left to its own issue (item 10 of §8 of the
    /// design: its identity attitudes go to a dictionary nothing reads).
    /// </summary>
    [Fact]
    public void TlePropagator_WritesItsSpkInJ2000()
    {
        var tle = new TLE("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var start = new Time(2021, 1, 20, 12, 0, 0, frame: TimeFrame.TDBFrame);
        var sc = new Spacecraft(-8441, "TLE8441", 1000.0, 3000.0, new Clock("TLE8441_CLK", 65536), tle);
        using (var propagator = new TLEPropagator(new Window(start, start.AddSeconds(600.0)), sc, TimeSpan.FromSeconds(10.0)))
        {
            propagator.Propagate();
        }

        string directory = "Data/User/AttitudeRecord537/TLE8441";
        Directory.CreateDirectory(directory);
        var spk = new FileInfo(Path.Combine(directory, "tle8441.bsp"));
        File.Delete(spk.FullName);
        spk = new FileInfo(spk.FullName);
        sc.WriteEphemeris(spk);
        SpiceAPI.Instance.LoadKernels(spk);
        try
        {
            double position = 0.0, velocity = 0.0;
            for (int k = 0; k <= 60; k++)
            {
                var t = start.AddSeconds(k * 10.0);
                var expected = tle.ToStateVector(t).ToFrame(Frame.ICRF).ToStateVector();
                var read = SpiceAPI.Instance.ReadEphemeris(t, 399, sc.NaifId, Frame.ICRF, Aberration.None).ToStateVector();
                position = System.Math.Max(position, (read.Position - expected.Position).Magnitude());
                velocity = System.Math.Max(velocity, (read.Velocity - expected.Velocity).Magnitude());
            }

            _output.WriteLine($"MEASURE #535 TLEPropagator SPK - SGP4 in ICRF at its records: {position:E2} m, {velocity:E2} m/s");
            Assert.True(position <= TlePosition, $"{position:E3} m");
            Assert.True(velocity <= TleVelocity, $"{velocity:E3} m/s");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spk);
        }
    }

    // PROVISIONAL: twice the worst (Linux x64, 2026-11-02): the covariance relative to its largest element (6.12e-16), the
    // states in m and m/s (0 for the propagated states, 2.44e-9 m for the two-body branch)
    internal const double StoredCovariance = 1.3e-15;
    internal const double StoredState = 4.9e-9;

    /// <summary>
    /// A spacecraft propagated from ECLIPJ2000 with a covariance (RK7(8), covariance propagation): the states it stores are
    /// those of the solution rotated to ICRF by <c>pxform(ECLIPJ2000, J2000)</c>, their covariance R P Rᵀ (#535).
    /// </summary>
    [Fact]
    public void StoredStates_FromAnotherFrame_AreInIcrfWithTheirCovariance()
    {
        var start = new Time(2024, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, start);
        var f = Frame.ECLIPTIC_J2000;
        var leo = new KeplerianElements(6778137.0, 0.001, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 0.4, 1.1, 0.3, earth, start, Frame.ICRF).ToStateVector();
        var inF = leo.ToFrame(f).ToStateVector();
        var p0 = new double[36];
        double[] sigma = [100.0, 200.0, 300.0, 0.1, 0.2, 0.3];
        for (int i = 0; i < 6; i++) p0[i * 7] = sigma[i] * sigma[i];
        var sc = new Spacecraft(-8451, "COV8451", 1000.0, 3000.0, new Clock("COV8451_CLK", 65536),
            new StateVector(inF.Position, inF.Velocity, earth, start, f, new Matrix(6, 6, p0)));
        var solution = sc.Propagate(new Window(start, start.AddSeconds(600.0)), new CelestialItem[] { earth }, new RK78Integrator(1e-12, 1e-12), false, false,
            TimeSpan.FromSeconds(60.0), new IO.Astrodynamics.Propagator.Variational.CovariancePropagationOptions { Mode = IO.Astrodynamics.Propagator.Variational.CovariancePropagationMode.Covariance });
        var q = SpiceAPI.Instance.TransformFrame(start, f, Frame.ICRF).Rotation;
        // R, the matrix of pxform(ECLIPJ2000, J2000), by the images of the axes
        Vector3[] columns = [Vector3.VectorX.Rotate(q), Vector3.VectorY.Rotate(q), Vector3.VectorZ.Rotate(q)];
        double R(int i, int j) => i switch { 0 => columns[j].X, 1 => columns[j].Y, _ => columns[j].Z };
        double covariance = 0.0, state = 0.0;
        var stored = sc.PropagatedStates;
        Assert.Equal(11, stored.Length);
        foreach (var sv in stored)
        {
            Assert.Equal(Frame.ICRF, sv.Frame);
            var (position, velocity) = solution.InterpolateAt(sv.Epoch);
            state = System.Math.Max(state, (sv.Position - position.Rotate(q)).Magnitude());
            state = System.Math.Max(state, (sv.Velocity - velocity.Rotate(q)).Magnitude());
            var pf = solution.GetCovariance(sv.Epoch);
            Assert.True(sv.Covariance.HasValue);
            double largest = 0.0, delta = 0.0;
            for (int i = 0; i < 6; i++)
            for (int j = 0; j < 6; j++)
            {
                // (R6 P R6ᵀ)ij, R6 = diag(R, R)
                double expected = 0.0;
                for (int k = 0; k < 6; k++)
                for (int l = 0; l < 6; l++)
                {
                    double rik = i / 3 == k / 3 ? R(i % 3, k % 3) : 0.0;
                    double rjl = j / 3 == l / 3 ? R(j % 3, l % 3) : 0.0;
                    expected += rik * pf.Get(k, l) * rjl;
                }

                largest = System.Math.Max(largest, System.Math.Abs(expected));
                delta = System.Math.Max(delta, System.Math.Abs(sv.Covariance.Value.Get(i, j) - expected));
            }

            covariance = System.Math.Max(covariance, delta / largest);
        }

        _output.WriteLine($"MEASURE #535 stored states from ECLIPJ2000: state - solution in ICRF {state:E2}, covariance - R P Rᵀ {covariance:E2} (relative)");
        Assert.True(state <= StoredState, $"{state:E3}");
        Assert.True(covariance <= StoredCovariance, $"{covariance:E3}");
    }

    /// <summary>
    /// A spacecraft from ECLIPJ2000 not propagated (the two-body branch), and states added in ECLIPJ2000
    /// (<c>Spacecraft.AddPropagatedStates</c>): the spacecraft reads them in ICRF, rotated by <c>pxform(ECLIPJ2000, J2000)</c>
    /// (#535).
    /// </summary>
    [Fact]
    public void StatesFromAnotherFrame_NotPropagatedOrAdded_AreReadInIcrf()
    {
        var start = new Time(2024, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, start);
        var f = Frame.ECLIPTIC_J2000;
        var leo = new KeplerianElements(6778137.0, 0.001, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 0.4, 1.1, 0.3, earth, start, Frame.ICRF).ToStateVector();
        var inF = leo.ToFrame(f).ToStateVector();
        var q = SpiceAPI.Instance.TransformFrame(start, f, Frame.ICRF).Rotation;

        var twoBody = new Spacecraft(-8461, "TB8461", 1000.0, 3000.0, new Clock("TB8461_CLK", 65536), inF);
        var read = twoBody.GetGeometricStateRelativeTo(start, earth);
        Assert.Equal(Frame.ICRF, read.Frame);
        double twoBodyDelta = (read.Position - inF.Position.Rotate(q)).Magnitude();

        var added = new Spacecraft(-8462, "AD8462", 1000.0, 3000.0, new Clock("AD8462_CLK", 65536), inF);
        var later = new StateVector(inF.Position * 1.001, inF.Velocity, earth, start.AddSeconds(10.0), f);
        added.AddPropagatedStates(inF, later);
        double addedDelta = System.Math.Max((added.GetGeometricStateRelativeTo(start, earth).Position - inF.Position.Rotate(q)).Magnitude(),
            (added.GetGeometricStateRelativeTo(later.Epoch, earth).Position - later.Position.Rotate(q)).Magnitude());
        Assert.All(added.PropagatedStates, x => Assert.Equal(Frame.ICRF, x.Frame));
        _output.WriteLine($"MEASURE #535 from ECLIPJ2000: two-body branch {twoBodyDelta:E2} m, added states {addedDelta:E2} m");
        Assert.True(twoBodyDelta <= StoredState, $"{twoBodyDelta:E3} m");
        Assert.True(addedDelta <= StoredState, $"{addedDelta:E3} m");
    }

    /// <summary>The CK of a kernel-backed spacecraft cannot be written: its attitude is that of its kernels.</summary>
    [Fact]
    public void WriteOrientation_KernelBacked_Throws()
    {
        using var run = PropagatedCase.Run(-8471, "Nadir", "VV", Frame.ICRF, "Propagate", 2024);
        var file = new FileInfo("Data/User/AttitudeRecord537/kernelbacked.bc");
        Assert.Throws<InvalidOperationException>(() => run.Kernel.WriteOrientation(file));
    }

    #endregion

    #region Closed forms

    /// <summary>The angle between two vectors, by atan2: accurate at any angle.</summary>
    internal static double Angle(in Vector3 a, in Vector3 b) => System.Math.Atan2(a.Cross(b).Magnitude(), a * b);

    /// <summary>The angle of the rotation from <paramref name="a"/> to <paramref name="b"/>, two rotations as maps of vectors:
    /// E = B Aᵀ, its angle from its antisymmetric part and its trace.</summary>
    internal static double RotationAngle(Func<Vector3, Vector3> a, Func<Vector3, Vector3> b)
    {
        Vector3[] ea = [a(Vector3.VectorX), a(Vector3.VectorY), a(Vector3.VectorZ)];
        Vector3[] eb = [b(Vector3.VectorX), b(Vector3.VectorY), b(Vector3.VectorZ)];
        // E = sum_k eb_k ea_kᵀ
        var e = new double[3, 3];
        for (int k = 0; k < 3; k++)
        {
            double[] bk = [eb[k].X, eb[k].Y, eb[k].Z];
            double[] ak = [ea[k].X, ea[k].Y, ea[k].Z];
            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                e[i, j] += bk[i] * ak[j];
        }

        double s = System.Math.Sqrt(System.Math.Pow(e[2, 1] - e[1, 2], 2) + System.Math.Pow(e[0, 2] - e[2, 0], 2) + System.Math.Pow(e[1, 0] - e[0, 1], 2)) / 2.0;
        double c = (e[0, 0] + e[1, 1] + e[2, 2] - 1.0) / 2.0;
        return System.Math.Atan2(s, c);
    }

    internal static double RotationAngle(Quaternion a, Quaternion b) => RotationAngle(v => v.Rotate(a), v => v.Rotate(b));

    /// <summary>The minimal rotation taking the direction of <paramref name="from"/> to that of <paramref name="to"/>
    /// (Rodrigues, about their cross product).</summary>
    internal static Func<Vector3, Vector3> MinimalRotation(Vector3 from, Vector3 to)
    {
        var a = from.Normalize();
        var b = to.Normalize();
        var axis = a.Cross(b);
        double sin = axis.Magnitude();
        double cos = a * b;
        if (sin == 0.0)
        {
            return v => v;
        }

        var k = axis / sin;
        return v => v * cos + k.Cross(v) * sin + k * ((k * v) * (1.0 - cos));
    }

    /// <summary>The TRIAD rotation: the body triad of (<paramref name="b1"/>, <paramref name="b2"/>) to the reference triad of
    /// (<paramref name="r1"/>, <paramref name="r2"/>): b1 to r1, the plane (b1, b2) to the plane (r1, r2).</summary>
    internal static Func<Vector3, Vector3> Triad(Vector3 b1, Vector3 b2, Vector3 r1, Vector3 r2)
    {
        (Vector3, Vector3, Vector3) Basis(Vector3 u, Vector3 v)
        {
            var t1 = u.Normalize();
            var t2 = u.Cross(v).Normalize();
            return (t1, t2, t1.Cross(t2));
        }

        var (s1, s2, s3) = Basis(b1, b2);
        var (d1, d2, d3) = Basis(r1, r2);
        return v => d1 * (s1 * v) + d2 * (s2 * v) + d3 * (s3 * v);
    }

    /// <summary>Rx(-x) Ry(-y) Rz(-z) acting on a vector: the frame rotations [x]1 [y]2 [z]3 of Frames Required Reading
    /// ("Defining a TK Frame Using Euler Angles", M = [angle_1]axis_1 [angle_2]axis_2 [angle_3]axis_3, [A]i the frame
    /// rotation by A about the i-th axis, the transpose of the vector rotation), the instrument frame to the spacecraft
    /// frame.</summary>
    internal static Vector3 EulerToSpacecraft(Vector3 orientation, Vector3 v)
    {
        // The vector rotation by angle a about an axis, written per axis
        static Vector3 Rx(double a, Vector3 u) => new(u.X, u.Y * System.Math.Cos(a) - u.Z * System.Math.Sin(a), u.Y * System.Math.Sin(a) + u.Z * System.Math.Cos(a));
        static Vector3 Ry(double a, Vector3 u) => new(u.X * System.Math.Cos(a) + u.Z * System.Math.Sin(a), u.Y, -u.X * System.Math.Sin(a) + u.Z * System.Math.Cos(a));
        static Vector3 Rz(double a, Vector3 u) => new(u.X * System.Math.Cos(a) - u.Y * System.Math.Sin(a), u.X * System.Math.Sin(a) + u.Y * System.Math.Cos(a), u.Z);
        return Rx(-orientation.X, Ry(-orientation.Y, Rz(-orientation.Z, v)));
    }

    #endregion

    /// <summary>A spacecraft of the grid, propagated, its kernels written by the library and loaded, and the kernel-backed
    /// spacecraft that reads them.</summary>
    internal sealed class PropagatedCase : IDisposable
    {
        public string Law { get; private init; }
        public Spacecraft Spacecraft { get; private init; }
        public Spacecraft Kernel { get; private set; }
        public PropagationSolution Solution { get; private init; }
        public Frame InitialFrame { get; private init; }
        public bool Ssb { get; private init; }
        public Time Start { get; private init; }
        public CelestialBody Earth { get; private init; }
        public CelestialBody Sun { get; private init; }
        public CelestialBody Moon { get; private init; }
        public ImpulseManeuver Impulse { get; private init; }
        public TimeSpan OutputStep { get; private init; }
        private readonly List<FileInfo> _kernels = new();

        /// <summary>Hooks of a case outside the grid: an attitude chain of its own (law "Custom"), reads before the
        /// propagation, during it (on another thread) and after it, before the kernels are written.</summary>
        internal sealed record Hooks
        {
            public Action<Spacecraft, Engine, CelestialBody, CelestialBody, Time> Configure { get; init; }
            public Action<Spacecraft> Before { get; init; }
            public Action<Spacecraft, CancellationToken> During { get; init; }
            public Action<Spacecraft> After { get; init; }
            public TimeSpan? Step { get; init; }
            public bool WriteKernels { get; init; } = true;
        }

        internal static PropagatedCase Run(int naifId, string law, string integrator, Frame frame, string path, int year, Hooks hooks = null)
        {
            hooks ??= new Hooks();
            var step = hooks.Step ?? Step;
            var start = new Time(year, 3, 1, 12, 0, 0, frame: TimeFrame.TDBFrame);
            var window = new Window(start, start.AddSeconds(Length));
            var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, start);
            var moon = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, start);
            var sun = new CelestialBody(Stars.Sun, Frame.ICRF, start);
            var bodies = new CelestialItem[] { earth, moon, sun };
            bool ssb = path == "SSB";

            // An inclined LEO; just below circular for the impulses, so that the apogee-raising impulse fires at perigee inside
            // the window, at about S + 2 740 s (off the output grid in RK7(8))
            var leo = new KeplerianElements(6778137.0, 0.001, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 0.4, 1.1, 0.3, earth, start, Frame.ICRF).ToStateVector();
            var orbit = law.Contains("Impulse") || law == "Custom" ? new StateVector(leo.Position, leo.Velocity * 0.993, earth, start, Frame.ICRF) : leo;
            if (ssb)
            {
                var earthFromSsb = earth.GetGeometricStateRelativeTo(start, Barycenters.SOLAR_SYSTEM_BARYCENTER);
                orbit = new StateVector(earthFromSsb.Position + orbit.Position, earthFromSsb.Velocity + orbit.Velocity, Barycenters.SOLAR_SYSTEM_BARYCENTER, start,
                    Frame.ICRF);
            }

            var initial = frame == Frame.ICRF ? orbit : orbit.ToFrame(frame).ToStateVector();
            string name = $"AR{-naifId}";
            var sc = new Spacecraft(naifId, name, 1000.0, 3000.0, new Clock(name + "_CLK", 65536), initial);
            var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
            sc.AddFuelTank(tank);
            var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
            sc.AddEngine(engine);
            for (int i = 0; i < InstrumentOrientations.Length; i++)
            {
                sc.AddCircularInstrument(naifId * 1000 - 1 - i, $"I{i + 1}", "m", 0.5, Vector3.VectorZ, Vector3.VectorY, InstrumentOrientations[i]);
            }

            ImpulseManeuver impulse = null;
            ImpulseManeuver Apogee() => impulse = new ApogeeHeightManeuver(earth, start.AddSeconds(100.0), TimeSpan.Zero, 7200000.0, engine);
            TriadAttitude TriadAfter() => new(start.AddSeconds(100.0), TimeSpan.Zero, Spacecraft.Front, earth, Spacecraft.Up, sun, engine);
            switch (law)
            {
                case "None":
                    break;
                case "Nadir":
                    sc.SetStandbyManeuver(new NadirAttitude(earth, Min, TimeSpan.Zero, engine));
                    break;
                case "Prograde":
                    sc.SetStandbyManeuver(new ProgradeAttitude(earth, Min, TimeSpan.Zero, engine));
                    break;
                case "InstrumentTriad":
                    // The boresight of the instrument at (0.3, 0.5, 0.2) to the Earth, its reference vector toward the Sun
                    sc.SetStandbyManeuver(new TriadAttitude(Min, TimeSpan.Zero, sc.Instruments.First(), earth, sun, engine));
                    break;
                case "InstrumentPointing":
                    sc.SetStandbyManeuver(new InstrumentPointingToAttitude(Min, TimeSpan.Zero, sc.Instruments.First(), moon, engine));
                    break;
                case "ImpulseAlone":
                    sc.SetStandbyManeuver(Apogee());
                    break;
                case "ImpulseTriad":
                {
                    var apogee = Apogee();
                    apogee.SetNextManeuver(TriadAfter());
                    sc.SetStandbyManeuver(apogee);
                    break;
                }
                case "NadirImpulseTriad":
                {
                    var nadir = new NadirAttitude(earth, Min, TimeSpan.Zero, engine);
                    var apogee = Apogee();
                    nadir.SetNextManeuver(apogee);
                    apogee.SetNextManeuver(TriadAfter());
                    sc.SetStandbyManeuver(nadir);
                    break;
                }
                case "Custom":
                    hooks.Configure(sc, engine, earth, sun, start);
                    break;
                default:
                    throw new ArgumentException(law);
            }

            hooks.Before?.Invoke(sc);
            using var cancel = new CancellationTokenSource();
            // The reads during the propagation start before it
            using var started = new ManualResetEventSlim(hooks.During == null);
            var during = hooks.During == null ? Task.CompletedTask : Task.Run(() =>
            {
                started.Set();
                hooks.During(sc, cancel.Token);
            });
            started.Wait();
            PropagationSolution solution = null;
            if (path == "Scenario")
            {
                var scenario = new Scenario("S" + name, new IO.Astrodynamics.Mission.Mission("m" + name), window);
                scenario.AddCelestialItem(moon);
                scenario.AddCelestialItem(earth);
                scenario.AddCelestialItem(sun);
                scenario.AddSpacecraft(sc);
                if (integrator == "RK78")
                {
                    scenario.SimulateAsync(false, false, step, () => new RK78Integrator(1e-12, 1e-12)).GetAwaiter().GetResult();
                    // The solution the RK7(8) propagation keeps; the Velocity-Verlet path keeps none
                    solution = sc.LastPropagation;
                }
                else
                {
                    scenario.SimulateAsync(false, false, step).GetAwaiter().GetResult();
                }
            }
            else if (integrator == "RK78")
            {
                solution = sc.Propagate(window, bodies, new RK78Integrator(1e-12, 1e-12), false, false, step);
            }
            else
            {
                solution = sc.Propagate(window, bodies, false, false, step);
            }

            cancel.Cancel();
            during.GetAwaiter().GetResult();
            hooks.After?.Invoke(sc);

            var run = new PropagatedCase
            {
                Law = law, Spacecraft = sc, Solution = ssb ? null : solution, InitialFrame = frame, Ssb = ssb, Start = start, Earth = earth, Sun = sun,
                Moon = moon, Impulse = impulse, OutputStep = step
            };
            if (hooks.WriteKernels)
            {
                run.WriteAndLoadKernels();
            }

            return run;
        }

        private FileInfo NewFile(string directory, string name)
        {
            var path = Path.Combine(directory, name);
            File.Delete(path);
            var file = new FileInfo(path);
            _kernels.Add(file);
            return file;
        }

        /// <summary>The clock, the frames, the instrument kernels, the SPK and the CK, written by the library, then the
        /// kernel-backed spacecraft that reads them.</summary>
        internal void WriteAndLoadKernels()
        {
            var sc = Spacecraft;
            // Relative paths, as the kernels of the other tests: the native writers take a bounded file name
            string directory = $"Data/User/AttitudeRecord537/{sc.Name}";
            Directory.CreateDirectory(directory);
            string stem = sc.Name.ToLowerInvariant();
            var clock = NewFile(directory, stem + ".tsc");
            sc.Clock.WriteAsync(clock).GetAwaiter().GetResult();
            var frame = NewFile(directory, stem + ".tf");
            ((SpacecraftFrame)sc.Frame).WriteAsync(frame).GetAwaiter().GetResult();
            SpiceAPI.Instance.LoadKernels(clock);
            SpiceAPI.Instance.LoadKernels(frame);
            foreach (var instrument in sc.Instruments)
            {
                var instrumentFrame = NewFile(directory, $"{stem}_{instrument.Name.ToLowerInvariant()}.tf");
                instrument.WriteFrameAsync(instrumentFrame).GetAwaiter().GetResult();
                var kernel = NewFile(directory, $"{stem}_{instrument.Name.ToLowerInvariant()}.ti");
                instrument.WriteKernelAsync(kernel).GetAwaiter().GetResult();
                SpiceAPI.Instance.LoadKernels(instrumentFrame);
                SpiceAPI.Instance.LoadKernels(kernel);
            }

            var spk = NewFile(directory, stem + ".bsp");
            sc.WriteEphemeris(spk);
            SpiceAPI.Instance.LoadKernels(spk);
            var ck = NewFile(directory, stem + ".bc");
            sc.WriteOrientation(ck);
            SpiceAPI.Instance.LoadKernels(ck);
            Kernel = new Spacecraft(sc.NaifId, sc.Name, Start, 1000.0, 3000.0, new Clock(sc.Name + "_CLK", 65536));
        }

        /// <summary>The epochs of the discrete changes: the second record of each pair of CK records 1 µs apart.</summary>
        internal Time[] DiscreteChanges()
        {
            var records = ((SpacecraftFrame)Spacecraft.Frame).CkRecords();
            var changes = new List<Time>();
            for (int i = 1; i < records.Length; i++)
            {
                if ((records[i].Epoch - records[i - 1].Epoch).TotalSeconds <= 1.5e-6)
                {
                    changes.Add(records[i].Epoch);
                }
            }

            return changes.ToArray();
        }

        /// <summary>Every 2.5 s (the outputs and between them), and about each entry of the record: -1 ms, -2 µs, -0.5 µs,
        /// +0.5 µs, +1 ms, and the entry itself.</summary>
        internal List<Time> ReadEpochs(StateOrientation[] record)
        {
            var end = Start.AddSeconds(Length);
            var epochs = new SortedSet<Time>();
            for (double s = 0.0; s <= Length; s += 2.5)
            {
                epochs.Add(Start.AddSeconds(s));
            }

            foreach (var entry in record)
            {
                foreach (var offset in new[] { -1e-3, -2e-6, -0.5e-6, 0.0, 0.5e-6, 1e-3 })
                {
                    var t = entry.Epoch.AddSeconds(offset);
                    if (t >= Start && t <= end)
                    {
                        epochs.Add(t);
                    }
                }
            }

            return epochs.ToList();
        }

        /// <summary>The position and the velocity of the spacecraft relative to the Earth in ICRF: the solution of the
        /// propagation rotated from its frame by <c>pxform</c>, or <c>spkezr</c> without a solution.</summary>
        internal (Vector3 Position, Vector3 Velocity) StateAt(Time t)
        {
            if (Solution == null)
            {
                var spk = Kernel.GetGeometricStateRelativeTo(t, Earth);
                return (spk.Position, spk.Velocity);
            }

            var (position, velocity) = Solution.InterpolateAt(t);
            if (InitialFrame == Frame.ICRF)
            {
                return (position, velocity);
            }

            var q = SpiceAPI.Instance.TransformFrame(t, InitialFrame, Frame.ICRF).Rotation;
            return (position.Rotate(q), velocity.Rotate(q));
        }

        // The geometric position of a body relative to the Earth in ICRF (spkezr)
        private Vector3 FromEarth(CelestialBody body, Time t) => SpiceAPI.Instance.ReadEphemeris(t, 399, body.NaifId, Frame.ICRF, Aberration.None).ToStateVector().Position;

        private Time? _change;

        /// <summary>The epoch of the impulse, the first discrete change of the record after the start.</summary>
        internal Time Change => _change ??= DiscreteChanges().First(x => x > Start);

        /// <summary>The closed form of the attitude at <paramref name="t"/> (a map of vectors from the spacecraft frame to
        /// ICRF) and whether it is a continuous law; null where the attitude has no closed form here: after the TRIAD that
        /// follows a nadir attitude, whose continuous attitude resumes at the next output (the attitude timeline, item 2 of
        /// §8 of the design, its own issue).</summary>
        internal (Func<Vector3, Vector3> Law, bool Continuous) LawAt(Time t)
        {
            switch (Law)
            {
                case "Custom":
                    return (null, false);
                case "None":
                    return (v => v, false);
                case "Nadir":
                    return (MinimalRotation(Spacecraft.Front, StateAt(t).Position.Inverse()), true);
                case "Prograde":
                    return (MinimalRotation(Spacecraft.Front, StateAt(t).Velocity), true);
                case "InstrumentTriad":
                {
                    var r = StateAt(t).Position;
                    var (b1, b2) = InstrumentAxesInSpacecraft();
                    return (Triad(b1, b2, r.Inverse(), FromEarth(Sun, t) - r), true);
                }
                case "InstrumentPointing":
                {
                    var (b1, _) = InstrumentAxesInSpacecraft();
                    return (MinimalRotation(b1, FromEarth(Moon, t) - StateAt(t).Position), true);
                }
                case "ImpulseAlone":
                    return t < Change ? (v => v, false) : (MinimalRotation(Spacecraft.Front, ImpulseInIcrf()), false);
                case "ImpulseTriad":
                    return t < Change ? (v => v, false) : (TriadAtChange(), false);
                case "NadirImpulseTriad":
                    return t < Change ? (MinimalRotation(Spacecraft.Front, StateAt(t).Position.Inverse()), true) : (null, false);
                default:
                    throw new ArgumentException(Law);
            }
        }

        private Func<Vector3, Vector3> _triadAtChange;

        // The TRIAD of the change, on the state at its epoch: the front to the Earth, the up axis toward the Sun
        private Func<Vector3, Vector3> TriadAtChange()
        {
            if (_triadAtChange == null)
            {
                var r = StateAt(Change).Position;
                _triadAtChange = Triad(Spacecraft.Front, Spacecraft.Up, r.Inverse(), FromEarth(Sun, Change) - r);
            }

            return _triadAtChange;
        }

        // The delta-V of the impulse, in the frame of the propagation, rotated to ICRF by pxform
        private Vector3 ImpulseInIcrf() =>
            InitialFrame == Frame.ICRF ? Impulse.DeltaV : Impulse.DeltaV.Rotate(SpiceAPI.Instance.TransformFrame(Change, InitialFrame, Frame.ICRF).Rotation);

        private (Vector3, Vector3)? _axes;

        // The boresight and the reference vector of the first instrument in the spacecraft frame: pxform(instrument, SC) on
        // the frame kernels the library writes, applied to the vectors of its kernel
        private (Vector3, Vector3) InstrumentAxesInSpacecraft()
        {
            if (_axes == null)
            {
                var instrument = Spacecraft.Instruments.First();
                var q = SpiceAPI.Instance.TransformFrame(Start, new Frame(Spacecraft.Name.ToUpper() + "_" + instrument.Name.ToUpper()), Spacecraft.Frame).Rotation;
                _axes = (instrument.Boresight.Rotate(q), instrument.RefVector.Rotate(q));
            }

            return _axes.Value;
        }

        public void Dispose()
        {
            foreach (var kernel in _kernels)
            {
                SpiceAPI.Instance.UnloadKernels(kernel);
            }
        }
    }
}
