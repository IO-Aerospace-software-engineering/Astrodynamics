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
/// propagation, rotated from its frame to ICRF by <c>pxform</c>, never the SPK the library wrote. In SSB mode the solution
/// is relative to the Sun: less the Earth's state of <c>spkezr</c> on the planetary ephemeris. The positions are measured
/// across the impulses too, the stored states being split into arcs at each impulse (#540).
/// The Velocity-Verlet path of <c>Scenario.SimulateAsync</c> keeps no solution: that of a twin propagated by
/// <c>Spacecraft.Propagate</c> with the scenario's bodies, whose stored states equal the scenario's to the bit. The
/// directions of the Sun and the Moon: <c>spkezr</c> from the Earth, geometric, as the attitudes read them
/// (<c>Aberration.None</c>; for the instrument pointing, whose light time is not applied to a celestial target, a pin of a
/// defect outside #537 with its own issue, #549, <see cref="PropagatedCase.LawAt"/>). The angular velocity of a law: the
/// rotation of its closed form over 0.1 s.
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
/// <b>Limits.</b> Twice the worst over Linux, Windows and macOS arm64 (CI run 41522747911, 2026-11-02), rounded up at
/// the second significant digit. The cases at the worst of each limit run in every CI job; the rest of the grid is in the
/// Validation category (Linux).
/// </para>
/// </remarks>
public class AttitudeRecordTests
{
    // Twice the worst over Linux, Windows and macOS arm64 (CI run 41522747911, 2026-11-02, MEASURE lines), rounded up at
    // the second significant digit: the 162 cases of the grid on Linux, the 13 of WorstCase_MatchesSpice on Windows and
    // macOS arm64, and the chains of NearEpochChain (managed - CK and its rate); every worst below is the same on the three,
    // L / W / M equal to the printed digit, and the case of each worst is in WorstCase_MatchesSpice. The SLERP residual at
    // 1 s outputs, in the Validation category, is twice the Linux worst; the occultation against its twin, measured 0 on
    // the three, keeps its one tick. Where twice the printed worst falls on a rounding boundary, the Linux worst is read at
    // six digits from the same 162 cases with only the format of the MEASURE line changed (Linux x64, 2026-11-02, every
    // row equal to CI Linux at the printed digits): FrameVelocity, FramePositionSunAfter2025, RateAgainstCkAfter2025

    /// <summary>The managed rotation against the CK (<c>pxform</c>), off the leads, before 2025-01-19: the encoded SCLK's
    /// spacing (2.4e-7 s) times the angular velocity. Worst 6.14e-10 rad (Prograde, VV, ICRF).</summary>
    internal const double ManagedAgainstCk = 1.3e-9;

    /// <summary>The same from 2025-01-19, where the spacing of the encoded SCLK doubles (4.8e-7 s). Worst 1.29e-9 rad
    /// (Prograde, RK7(8), ECLIPJ2000, 2026).</summary>
    internal const double ManagedAgainstCkAfter2025 = 2.6e-9;

    /// <summary>The managed angular velocity against <c>xf2rav(sxform(SC, J2000))</c> on the CK, off the leads, rad/s.
    /// Worst 9.86e-13 rad/s (InstrumentTriad, RK7(8), B1950).</summary>
    internal const double RateAgainstCk = 2.0e-12;

    /// <summary>The same from 2025-01-19, rad/s. Worst 1.45e-12 rad/s (Prograde, RK7(8), ECLIPJ2000, 2026; 1.447735e-12
    /// rad/s on Linux at six digits; Windows and macOS arm64 print 1.45e-12, which does not settle 2.9e-12 against
    /// 3.0e-12).</summary>
    internal const double RateAgainstCkAfter2025 = 2.9e-12;

    /// <summary>The CK at the entries of a continuous law (its samples) against the closed form of the law on the solution
    /// of the propagation, about the Earth. Worst 1.26e-15 rad (InstrumentTriad, VV, ECLIPJ2000).</summary>
    internal const double LawAtEntries = 2.6e-15;

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

    /// <summary>The managed positions against <c>spkezr</c> on the SPK, m: the agreement of the library's Lagrange
    /// interpolation with the SPK's, between the 10 s outputs, both of the same stored states (not an accuracy: the
    /// positions are compared with the solution in <see cref="PositionAgainstSolution"/>). Worst 7.37e-4 m (every frame
    /// alike).</summary>
    internal const double PositionAgainstSpk = 1.5e-3;

    /// <summary>The same in SSB mode, m: since 11.0.0 the SPK is of type 9 there too (#540; the native writer's type 8
    /// assumed equal steps, 2.26e-2 m before). PROVISIONAL: twice the Linux worst, 1.80e-3 m (Nadir, RK7(8), GALACTIC; Linux
    /// x64, 2026-11-03), to be measured on Linux, Windows and macOS arm64.</summary>
    internal const double PositionAgainstSpkSsb = 3.6e-3;

    /// <summary>The same in SSB mode for the cases with an impulse, m. PROVISIONAL: twice the Linux worst, 2.54e-3 m
    /// (ImpulseTriad, RK7(8), GALACTIC; Linux x64, 2026-11-03).</summary>
    internal const double PositionAgainstSpkSsbAcrossImpulses = 5.1e-3;

    /// <summary>The same for the cases with an impulse, whose stored states are split into arcs at the impulse (#540, choice 18
    /// of #554): near the ends of an arc both interpolations are one-sided, and the SPK's epochs, doubles of 1.2e-7 s
    /// resolution, weigh more there. These rows were excluded before. PROVISIONAL: twice the Linux worst, 1.64e-3 m
    /// (NadirImpulseTriad, RK7(8), ECLIPJ2000, 2026; Linux x64, 2026-11-03; 1.85e-3 m on the prototype of #554, with the
    /// attitude timeline), to be measured on Linux, Windows and macOS arm64.</summary>
    internal const double PositionAgainstSpkAcrossImpulses = 3.3e-3;

    /// <summary>The managed angular velocity and the CK's against the rate of the law (a difference of its closed form over
    /// 0.1 s), rad/s: the model of the CK (choice 8), the mean of the SLERP rates of the intervals at the samples and linear
    /// between them, against the derivative. Worst 2.28e-5 rad/s (InstrumentTriad, RK7(8), B1950).</summary>
    internal const double RateAgainstLaw = 4.6e-5;

    /// <summary>The CK at the entries of a continuous law against its closed form in SSB mode: the solution is relative to
    /// the Sun, the reference state relative to the Earth is its difference with the Earth's of <c>spkezr</c>, two vectors
    /// of 1.5e11 m whose rounding (1e-5 m) is 2e-12 rad of the direction of a LEO; the library makes the same difference
    /// with its own roundings. Worst 3.51e-12 rad (Nadir, RK7(8), GALACTIC).</summary>
    internal const double LawAtEntriesSsb = 7.1e-12;

    /// <summary>The same for a discrete attitude in SSB mode. Worst 3.48e-12 rad (ImpulseTriad, VV, GALACTIC).</summary>
    internal const double DiscreteLawSsb = 7.0e-12;

    /// <summary>The managed positions between the outputs against the solution of an RK7(8) propagation rotated to ICRF by
    /// <c>pxform</c> (its dense output, the integrator's accuracy), m: the library's Lagrange interpolation of the stored
    /// states. Worst 1.28e-8 m (InstrumentPointing, RK7(8), GALACTIC). The dense output of Velocity-Verlet, a cubic
    /// Hermite, is 0.15 m off between the outputs and does not resolve it: the Velocity-Verlet cases are compared at the
    /// outputs only.</summary>
    internal const double PositionAgainstSolution = 2.6e-8;

    /// <summary>The same in SSB mode, m: the stored states relative to the Sun. Worst 7.07e-4 m (InstrumentTriad, RK7(8),
    /// GALACTIC).</summary>
    internal const double PositionAgainstSolutionSsb = 1.5e-3;

    /// <summary>The state of the spacecraft relative to the Earth in its own frame (<c>GetEphemeris</c>) against <c>spkezr</c>
    /// in the spacecraft frame on the written kernels, m and m/s: the floor of the CK times the distance, and the SPK's
    /// interpolation. Worst 3.93e-3 m and 1.00e-5 m/s (Prograde, RK7(8), B1950; 1.003777e-5 m/s on Linux at six digits,
    /// twice 2.0076e-5 m/s); 8.84e-3 m and 2.12e-5 m/s from 2025-01-19 (Prograde, RK7(8), ECLIPJ2000, 2026). In SSB mode,
    /// where the SPK is of type 9 since 11.0.0 (#540; 2.26e-2 m and 1.44e-3 m/s with the native writer's type 8), PROVISIONAL,
    /// twice the Linux worst (Linux x64, 2026-11-03): 2.34e-3 m (Nadir, RK7(8), GALACTIC) and 5.85e-6 m/s (InstrumentTriad,
    /// RK7(8), GALACTIC); with an impulse 2.54e-3 m and 1.68e-6 m/s (ImpulseTriad, RK7(8), GALACTIC and ICRF).</summary>
    internal const double FramePosition = 7.9e-3;

    internal const double FrameVelocity = 2.1e-5;
    internal const double FramePositionAfter2025 = 1.8e-2;
    internal const double FrameVelocityAfter2025 = 4.3e-5;
    internal const double FramePositionSsb = 4.7e-3;
    internal const double FrameVelocitySsb = 1.2e-5;
    internal const double FramePositionSsbAcrossImpulses = 5.1e-3;
    internal const double FrameVelocitySsbAcrossImpulses = 3.4e-6;

    /// <summary>The same relative to the Sun, 1.5e11 m away: the floor of the CK's rotation times the distance. Worst 88.2 m
    /// and 0.249 m/s (Prograde, RK7(8), B1950); 185 m and 0.527 m/s from 2025-01-19 (Prograde, RK7(8), ECLIPJ2000,
    /// 2026; 185.4724 m on Linux at six digits, twice 370.94 m).</summary>
    internal const double FramePositionSun = 1.8e2;

    internal const double FrameVelocitySun = 5.0e-1;
    internal const double FramePositionSunAfter2025 = 3.8e2;
    internal const double FrameVelocitySunAfter2025 = 1.1;

    /// <summary>The managed occultation from GALACTIC against its ICRF twin, s: measured 0 on the three platforms, below the
    /// resolution of a <see cref="Time"/>, one tick of 100 ns, which is the limit.</summary>
    internal const double OccultationAgainstTwin = 1.0e-7;

    /// <summary>The managed occultation from GALACTIC against <c>gfoclt</c> on the written SPK, s. Worst 4.0e-7 s.</summary>
    internal const double OccultationAgainstGfoclt = 8.0e-7;

    /// <summary>The SLERP residual against the law at 1 s outputs. Worst 5.73e-7 rad (InstrumentTriad; Nadir
    /// 3.96e-7 rad).</summary>
    internal const double SlerpResidualAtOneSecond = 1.2e-6;

    /// <summary>The same at 60 s outputs. Worst 2.06e-3 rad (InstrumentTriad; Nadir 1.41e-3 rad).</summary>
    internal const double SlerpResidualAtSixtySeconds = 4.2e-3;

    /// <summary>The tolerance of RK7(8) in SSB mode: about the Sun, 1e-12 does not finish in minutes (item 7 of §8 of the
    /// design, #544); 1e-9 takes seconds.</summary>
    internal const double SsbRk78Tolerance = 1e-9;

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

        // Added after the review of the implementation (appended, so that the cases above keep their identifiers): the other
        // laws through Scenario.SimulateAsync, the discrete and instrument laws in SSB mode, and RK7(8) in SSB mode
        foreach (var law in Laws.Where(x => x != "Nadir" && x != "ImpulseTriad"))
        foreach (var integrator in new[] { "VV", "RK78" })
        foreach (var frame in new[] { "ICRF", "ECLIPJ2000", "GALACTIC" })
            cases.Add($"{law}|{integrator}|{frame}|Scenario|2024");

        foreach (var law in new[] { "ImpulseTriad", "InstrumentTriad" })
        foreach (var frame in new[] { "ICRF", "ECLIPJ2000", "GALACTIC" })
            cases.Add($"{law}|VV|{frame}|SSB|2024");

        foreach (var law in new[] { "Nadir", "ImpulseTriad", "InstrumentTriad" })
        foreach (var frame in new[] { "ICRF", "GALACTIC" })
            cases.Add($"{law}|RK78|{frame}|SSB|2024");
        return cases.ToArray();
    }

    // The cases at the worst of each limit (Linux x64, 2026-11-02, after the review of the implementation), which run in
    // every CI job: on Windows and macOS arm64 their worsts are Linux's (CI run 41522747911, 2026-11-02)
    private static readonly string[] WorstCases =
    [
        "Prograde|RK78|B1950|Propagate|2024", // managed - CK, instruments, the states in the spacecraft frame
        "InstrumentTriad|RK78|B1950|Propagate|2024", // rate against the CK and the law, law between entries
        "InstrumentPointing|RK78|B1950|Propagate|2024", // positions against the SPK
        "InstrumentPointing|RK78|GALACTIC|Propagate|2024", // positions against the solution
        "InstrumentTriad|VV|ECLIPJ2000|Propagate|2024", // law at entries
        "ImpulseTriad|RK78|ICRF|Propagate|2024", // discrete law
        "Nadir|VV|GALACTIC|Scenario|2024", // the twin of the scenario's Velocity-Verlet path
        "Nadir|RK78|GALACTIC|SSB|2024", // law at entries in SSB mode
        "ImpulseTriad|VV|GALACTIC|SSB|2024", // discrete law in SSB mode
        "InstrumentTriad|VV|ECLIPJ2000|SSB|2024", // positions against the SPK and in the spacecraft frame in SSB mode
        "InstrumentTriad|RK78|GALACTIC|SSB|2024", // positions against the solution in SSB mode
        "InstrumentTriad|RK78|ICRF|SSB|2024", // velocities in the spacecraft frame in SSB mode
        "Prograde|RK78|ECLIPJ2000|Propagate|2026" // managed - CK, rate, instruments and the spacecraft frame from 2025-01-19
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
        _output.WriteLine($"MEASURE #537 attitude {label}: managed - CK {m.ManagedAgainstCk:E2} rad (lead {m.ManagedAgainstCkLead:E2}), rate managed - CK {m.RateAgainstCk:E2} rad/s, " +
                          $"rate - law managed {m.RateAgainstLaw:E2} CK {m.RateCkAgainstLaw:E2} rad/s, discrete rate {m.DiscreteRate:E2} rad/s, " +
                          $"law at entries {m.LawAtEntries:E2}, between {m.LawBetweenEntries:E2}, discrete {m.DiscreteLaw:E2} rad, instruments {m.Instruments:E2} rad, " +
                          $"positions - SPK {m.PositionsAgainstSpk:E2} m, - solution {m.PositionsAgainstSolution:E2} m, at the outputs {m.StatesAtOutputs:E2} m {m.VelocitiesAtOutputs:E2} m/s, " +
                          $"in the spacecraft frame - spkezr from the Earth {m.FramePositions[0]:E2} m {m.FrameVelocities[0]:E2} m/s (rotation term {m.FrameRotationTerm[0]:E2} m/s), " +
                          $"from the Sun {m.FramePositions[1]:E2} m {m.FrameVelocities[1]:E2} m/s (rotation term {m.FrameRotationTerm[1]:E2} m/s), " +
                          $"twin {m.TwinStates:E2} m, {m.Reads} reads, {m.Entries} entries");

        bool after2025 = year >= 2026;
        bool ssb = path == "SSB";
        Assert.True(m.ManagedAgainstCk <= (after2025 ? ManagedAgainstCkAfter2025 : ManagedAgainstCk), $"managed - CK {m.ManagedAgainstCk:E3} rad");
        Assert.True(m.RateAgainstCk <= (after2025 ? RateAgainstCkAfter2025 : RateAgainstCk), $"rate managed - CK {m.RateAgainstCk:E3} rad/s");
        Assert.True(m.RateAgainstLaw <= RateAgainstLaw, $"rate managed - law {m.RateAgainstLaw:E3} rad/s");
        Assert.True(m.RateCkAgainstLaw <= RateAgainstLaw, $"rate CK - law {m.RateCkAgainstLaw:E3} rad/s");
        Assert.Equal(0.0, m.DiscreteRate);
        Assert.True(m.LawAtEntries <= (ssb ? LawAtEntriesSsb : LawAtEntries), $"law at the entries {m.LawAtEntries:E3} rad");
        Assert.True(m.LawBetweenEntries <= LawBetweenEntries, $"law between the entries {m.LawBetweenEntries:E3} rad");
        Assert.True(m.DiscreteLaw <= (ssb ? DiscreteLawSsb : DiscreteLaw), $"discrete law {m.DiscreteLaw:E3} rad");
        Assert.True(m.Instruments <= (after2025 ? ManagedAgainstCkAfter2025 : InstrumentAgainstFrameKernel), $"instruments {m.Instruments:E3} rad");
        bool impulse = run.Law.Contains("Impulse");
        Assert.True(m.PositionsAgainstSpk <= (ssb ? impulse ? PositionAgainstSpkSsbAcrossImpulses : PositionAgainstSpkSsb : impulse ? PositionAgainstSpkAcrossImpulses : PositionAgainstSpk),
            $"positions - SPK {m.PositionsAgainstSpk:E3} m");
        Assert.True(m.PositionsAgainstSolution <= (ssb ? PositionAgainstSolutionSsb : PositionAgainstSolution), $"positions - solution {m.PositionsAgainstSolution:E3} m");
        // At the outputs the stored states are the solution's rotated by pxform(F, J2000), the rotation of the same doubles
        // as the reference's: measured 0 in every case
        Assert.Equal(0.0, m.StatesAtOutputs);
        Assert.Equal(0.0, m.VelocitiesAtOutputs);
        Assert.True(m.FramePositions[0] <= (ssb ? impulse ? FramePositionSsbAcrossImpulses : FramePositionSsb : after2025 ? FramePositionAfter2025 : FramePosition), $"positions from the Earth in the spacecraft frame {m.FramePositions[0]:E3} m");
        Assert.True(m.FrameVelocities[0] <= (ssb ? impulse ? FrameVelocitySsbAcrossImpulses : FrameVelocitySsb : after2025 ? FrameVelocityAfter2025 : FrameVelocity), $"velocities from the Earth in the spacecraft frame {m.FrameVelocities[0]:E3} m/s");
        Assert.True(m.FramePositions[1] <= (after2025 ? FramePositionSunAfter2025 : FramePositionSun), $"positions from the Sun in the spacecraft frame {m.FramePositions[1]:E3} m");
        Assert.True(m.FrameVelocities[1] <= (after2025 ? FrameVelocitySunAfter2025 : FrameVelocitySun), $"velocities from the Sun in the spacecraft frame {m.FrameVelocities[1]:E3} m/s");
        // The Velocity-Verlet path of the scenario keeps no solution: its reference is the solution of a twin propagated by
        // Spacecraft.Propagate with the scenario's bodies, whose stored states are those of the scenario to the bit
        Assert.Equal(0.0, m.TwinStates);
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
        public double RateAgainstLaw { get; set; }
        public double RateCkAgainstLaw { get; set; }
        public double DiscreteRate { get; set; }
        public double LawAtEntries { get; set; }
        public double LawBetweenEntries { get; set; }
        public double DiscreteLaw { get; set; }
        public double Instruments { get; set; }
        public double PositionsAgainstSpk { get; set; }
        public double PositionsAgainstSolution { get; set; }
        public double StatesAtOutputs { get; set; }
        public double VelocitiesAtOutputs { get; set; }
        // From the Earth [0] and from the Sun [1]
        public double[] FramePositions { get; } = new double[2];
        public double[] FrameVelocities { get; } = new double[2];
        public double[] FrameRotationTerm { get; } = new double[2];
        public double TwinStates { get; set; }
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
        var outputs = new HashSet<Time>(sc.PropagatedStates.Select(x => x.Epoch));
        var changes = run.DiscreteChanges();
        var epochs = run.ReadEpochs(record);
        var m = new Measures { Entries = record.Length, Reads = epochs.Count, TwinStates = run.TwinStates };
        var instrumentFrames = sc.Instruments.Select(x => new Frame(sc.Name.ToUpper() + "_" + x.Name.ToUpper())).ToArray();
        // The positions do not depend on the attitude; across an impulse too, the stored states being split into arcs at
        // each impulse (#540)
        bool positions = run.Law != "Custom";

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
            // The agreement of the managed rate with the CK's (xf2rav of sxform on the CK the library wrote with that rate)
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
                var (position, velocity) = run.StateAt(t);
                // The SPK against the managed reads: the agreement of the two interpolations of the stored states
                m.PositionsAgainstSpk = System.Math.Max(m.PositionsAgainstSpk, (managedState.Position - spk.Position).Magnitude());
                // The managed reads against the solution of the propagation rotated to ICRF by pxform (#535), between the outputs
                // where its dense output resolves them (RK7(8))
                if (run.Integrator == "RK78")
                {
                    m.PositionsAgainstSolution = System.Math.Max(m.PositionsAgainstSolution, (managedState.Position - position).Magnitude());
                }

                if (outputs.Contains(t))
                {
                    m.StatesAtOutputs = System.Math.Max(m.StatesAtOutputs, (managedState.Position - position).Magnitude());
                    m.VelocitiesAtOutputs = System.Math.Max(m.VelocitiesAtOutputs, (managedState.Velocity - velocity).Magnitude());
                }

                // The states in the spacecraft frame, from the Earth and from the Sun, against spkezr in the spacecraft frame on
                // the written kernels: the velocity takes the rotation of the frame, v' = R v - (R w) x r'. From the Sun, 1.5e11 m
                // away, the floor of the CK's rotation (managed - CK) gives tens of metres: its own limits
                for (int b = 0; b < 2; b++)
                {
                    var body = b == 0 ? run.Earth : run.Sun;
                    var inFrame = sc.GetEphemeris(t, body, sc.Frame, Aberration.None).ToStateVector();
                    var spice = SpiceAPI.Instance.ReadEphemeris(t, body.NaifId, sc.NaifId, run.Kernel.Frame, Aberration.None).ToStateVector();
                    var inIcrf = sc.GetEphemeris(t, body, Frame.ICRF, Aberration.None).ToStateVector();
                    double positionDelta = (inFrame.Position - spice.Position).Magnitude();
                    double velocityDelta = (inFrame.Velocity - spice.Velocity).Magnitude();
                    double term = (inFrame.Velocity - inIcrf.Velocity.Rotate(managed.Rotation.Conjugate())).Magnitude();
                    m.FramePositions[b] = System.Math.Max(m.FramePositions[b], positionDelta);
                    m.FrameVelocities[b] = System.Math.Max(m.FrameVelocities[b], velocityDelta);
                    m.FrameRotationTerm[b] = System.Math.Max(m.FrameRotationTerm[b], term);
                }
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
                // A discrete attitude does not turn: the managed rate and the CK's are zero
                m.DiscreteRate = System.Math.Max(m.DiscreteRate, System.Math.Max(managed.AngularVelocity.Magnitude(), ck.AngularVelocity.Magnitude()));
                continue;
            }

            if (entries.Contains(t))
            {
                m.LawAtEntries = System.Math.Max(m.LawAtEntries, fromLaw);
            }
            else
            {
                m.LawBetweenEntries = System.Math.Max(m.LawBetweenEntries, fromLaw);
            }

            // The rate of the law (a difference of its closed form over 0.1 s), in the convention of the frames, against the
            // managed rate and the CK's: the model of the CK (choice 8) against the derivative
            var lawRate = run.LawRateAt(t);
            m.RateAgainstLaw = System.Math.Max(m.RateAgainstLaw, (managed.AngularVelocity - lawRate).Magnitude());
            m.RateCkAgainstLaw = System.Math.Max(m.RateCkAgainstLaw, (ck.AngularVelocity - lawRate).Magnitude());
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
    [InlineData("ECLIPJ2000", -8403)]
    public void Reads_BeforeDuringAfter_LeaveTheRecordAndTheCk(string frame, int naifId) => Reads(frame, naifId);

    [Theory]
    [Trait("Category", "Validation")]
    [InlineData("ICRF", -8401)]
    public void Reads_BeforeDuringAfter_LeaveTheRecordAndTheCk_Icrf(string frame, int naifId) => Reads(frame, naifId);

    private void Reads(string frame, int naifId)
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

    // Twice the worst over the partial step before the impulse over Linux, Windows and macOS arm64 (CI run 41522747911,
    // 2026-11-02): 1.23e-4 / 1.23e-4 / 1.23e-4 rad, the managed path and the CK alike, the residual of the SLERP between
    // the last output sample (60 s step) and the law's sample 1 µs before the change; held, it was 4.70e-2 rad (the
    // review's probe on the prototype; 7.11e-2 rad on the base)
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
    public void NearEpochChain_WritesItsCk(double hold, int naifId) => NearEpochChain(hold, naifId);

    [Theory]
    [Trait("Category", "Validation")]
    [InlineData(300.0, -8422)]
    [InlineData(600.0, -8423)]
    public void NearEpochChain_OtherHolds_WriteTheirCk(double hold, int naifId) => NearEpochChain(hold, naifId);

    private void NearEpochChain(double hold, int naifId)
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

    // Twice the worst over the frames and the attitudes over Linux, Windows and macOS arm64 (CI run 41522747911, 2026-11-02):
    // 6.01e-16 / 6.01e-16 / 6.01e-16 rad, InstrumentPointing from B1950, which runs in every job (the other frames, in the
    // Validation category, on Linux: at most the same 6.01e-16 rad)
    internal const double TryExecuteLaw = 1.3e-15;

    /// <summary>
    /// <c>Attitude.TryExecute</c> (public) on a state in each inertial frame: the rotation from the spacecraft frame to ICRF,
    /// labelled ICRF, that of the closed form of the law on the same orbit in ICRF (#529, #535; choice 6).
    /// </summary>
    [Fact]
    public void AttitudeTryExecute_FromB1950_IsTheRotationToIcrf() => TryExecute("B1950");

    [Theory]
    [Trait("Category", "Validation")]
    [MemberData(nameof(FrameNames))]
    public void AttitudeTryExecute_FromEachFrame_IsTheRotationToIcrf(string frame) => TryExecute(frame);

    private void TryExecute(string frame)
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

    // B1950 holds the worst (Linux x64, 2026-11-02) and runs in every job
    public static IEnumerable<object[]> FrameNames() => Frames.Where(x => x != "B1950").Select(x => new object[] { x });

    // Twice the worst over Linux, Windows and macOS arm64 (CI run 41522747911, 2026-11-02): 3.73e-9 / 3.73e-9 / 3.73e-9 m,
    // 3.28e-12 / 3.28e-12 / 3.31e-12 m/s; the SPK's reproduction of its records, not an accuracy of the conversion
    internal const double TlePosition = 7.5e-9;
    internal const double TleVelocity = 6.7e-12;

    /// <summary>
    /// <c>TLEPropagator</c> (public): its SPK is written in J2000 (on the base, SPICE(INVALIDREFFRAME), TEME), and holds the
    /// states <c>Spacecraft.AddPropagatedStates</c> stored, the SGP4 states converted from TEME to ICRF by the library,
    /// read by <c>spkezr</c> at its records. The expected states take the same conversion: a check of the frame and of the
    /// content of the SPK, not of the conversion, which the TLE tests check. Its CK is left to its own issue, #546 (item
    /// 10 of §8 of the design: its identity attitudes go to a dictionary nothing reads).
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

            _output.WriteLine($"MEASURE #535 TLEPropagator SPK - its stored records (SGP4 converted to ICRF by the library): {position:E2} m, {velocity:E2} m/s");
            Assert.True(position <= TlePosition, $"{position:E3} m");
            Assert.True(velocity <= TleVelocity, $"{velocity:E3} m/s");
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(spk);
        }
    }

    // Twice the worst over Linux, Windows and macOS arm64 (CI run 41522747911, 2026-11-02): the covariance relative to its
    // largest element, 6.12e-16 / 6.12e-16 / 6.12e-16; the states in m and m/s, 2.44e-9 / 2.44e-9 / 2.44e-9 m (the two-body
    // branch; 0 for the propagated states and the added states)
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

    /// <summary>
    /// The managed occultation of the Sun by the Earth, both ellipsoids, seen from a spacecraft propagated from GALACTIC, over
    /// [60, 3 540] s: the windows of its ICRF twin, and those of CSPICE's <c>gfoclt</c> on the SPK the library wrote (a
    /// kernel-backed spacecraft). On the base the search read the states of GALACTIC as ICRF, and its window ended at
    /// 922.37 s instead of 2 071.45 s (§5 of the design, #535).
    /// </summary>
    [Fact]
    public void ManagedOccultation_FromGalactic_IsThatOfItsIcrfTwinAndOfGfoclt()
    {
        using var galactic = PropagatedCase.Run(-8481, "None", "VV", FrameOf("GALACTIC"), "Propagate", 2024);
        using var icrf = PropagatedCase.Run(-8482, "None", "VV", Frame.ICRF, "Propagate", 2024, new PropagatedCase.Hooks { WriteKernels = false });
        var window = new Window(galactic.Start.AddSeconds(60.0), galactic.Start.AddSeconds(3540.0));
        Window[] Search(Spacecraft observer, CelestialBody sun, CelestialBody earth) => sun.FindWindowsOnOccultationConstraint(window, observer, ShapeType.Ellipsoid, earth,
            ShapeType.Ellipsoid, OccultationType.Any, Aberration.None, TimeSpan.FromSeconds(60.0)).ToArray();
        var managed = Search(galactic.Spacecraft, galactic.Sun, galactic.Earth);
        var twin = Search(icrf.Spacecraft, icrf.Sun, icrf.Earth);
        // Both the observer and the target in the kernels, the observer a spacecraft: gfoclt
        var spice = Search(galactic.Kernel, galactic.Sun, galactic.Earth);
        Assert.Single(managed);
        Assert.Equal(managed.Length, twin.Length);
        Assert.Equal(managed.Length, spice.Length);
        double Delta(Window[] a, Window[] b) => a.Zip(b, (x, y) => System.Math.Max(System.Math.Abs((x.StartDate - y.StartDate).TotalSeconds), System.Math.Abs((x.EndDate - y.EndDate).TotalSeconds))).Max();
        double toTwin = Delta(managed, twin), toSpice = Delta(managed, spice);
        _output.WriteLine($"MEASURE #535 occultation from GALACTIC: [{(managed[0].StartDate - galactic.Start).TotalSeconds:F6}, {(managed[0].EndDate - galactic.Start).TotalSeconds:F6}] s, " +
                          $"ICRF twin {toTwin:E2} s, gfoclt {toSpice:E2} s");
        Assert.True(toTwin <= OccultationAgainstTwin, $"twin {toTwin:E3} s");
        Assert.True(toSpice <= OccultationAgainstGfoclt, $"gfoclt {toSpice:E3} s");
    }

    /// <summary>
    /// The residual of the SLERP between the samples of a continuous law, a property of the output step (choice 1), at 1 s
    /// and 60 s outputs (10 s is the grid's): the managed attitude at the midpoints and quarter points of the samples
    /// against the closed form of the law on the solution, RK7(8), the grid's orbit from ICRF.
    /// </summary>
    [Theory]
    [InlineData("Nadir", 60.0, -8491)]
    [InlineData("InstrumentTriad", 60.0, -8492)]
    public void SlerpResidual_AtSixtySecondOutputs(string law, double step, int naifId) => SlerpResidual(law, step, naifId);

    [Theory]
    [Trait("Category", "Validation")]
    [InlineData("Nadir", 1.0, -8493)]
    [InlineData("InstrumentTriad", 1.0, -8494)]
    public void SlerpResidual_AtOneSecondOutputs(string law, double step, int naifId) => SlerpResidual(law, step, naifId);

    private void SlerpResidual(string law, double step, int naifId)
    {
        using var run = PropagatedCase.Run(naifId, law, "RK78", Frame.ICRF, "Propagate", 2024, new PropagatedCase.Hooks { Step = TimeSpan.FromSeconds(step) });
        var record = run.Spacecraft.Frame.GetStateOrientationsToICRF().ToArray();
        double worst = 0.0;
        // Every interval at 60 s, one in ten at 1 s
        int stride = step < 10.0 ? 10 : 1;
        for (int i = 0; i + 1 < record.Length; i += stride)
        {
            foreach (var fraction in new[] { 0.25, 0.5, 0.75 })
            {
                var t = record[i].Epoch.AddSeconds((record[i + 1].Epoch - record[i].Epoch).TotalSeconds * fraction);
                var (closedForm, continuous) = run.LawAt(t);
                Assert.True(continuous);
                worst = System.Math.Max(worst, RotationAngle(closedForm, v => v.Rotate(run.Spacecraft.Frame.GetStateOrientationToICRF(t).Rotation)));
            }
        }

        double limit = step < 10.0 ? SlerpResidualAtOneSecond : SlerpResidualAtSixtySeconds;
        _output.WriteLine($"MEASURE #529 SLERP residual {law} at {step} s outputs: {worst:E2} rad");
        Assert.True(worst <= limit, $"{worst:E3} rad");
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

    /// <summary>The inverse of a rotation, a map of vectors, applied to <paramref name="v"/>: Lᵀ v, by the images of the
    /// axes.</summary>
    internal static Vector3 Transpose(Func<Vector3, Vector3> rotation, Vector3 v) =>
        new(rotation(Vector3.VectorX) * v, rotation(Vector3.VectorY) * v, rotation(Vector3.VectorZ) * v);

    /// <summary>The rotation vector (angle times axis) of a rotation, a map of vectors: from the antisymmetric part and the
    /// trace of its matrix.</summary>
    internal static Vector3 RotationVector(Func<Vector3, Vector3> rotation)
    {
        Vector3 ex = rotation(Vector3.VectorX), ey = rotation(Vector3.VectorY), ez = rotation(Vector3.VectorZ);
        // The columns are the images of the axes: m[i, j] = e_j[i]
        var s = new Vector3(ey.Z - ez.Y, ez.X - ex.Z, ex.Y - ey.X) * 0.5;
        double sin = s.Magnitude();
        if (sin == 0.0)
        {
            return Vector3.Zero;
        }

        double cos = (ex.X + ey.Y + ez.Z - 1.0) / 2.0;
        return s * (System.Math.Atan2(sin, cos) / sin);
    }

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
    /// ("Defining a TK Frame Using Euler Angles", https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/req/frames.html: "M =
    /// [angle_1]axis_1 [angle_2]axis_2 [angle_3]axis_3 where the symbol [ A ]i stands for a rotation by the angle A about the
    /// i'th axis", the axes as subscripts; its matrices are the frame rotations, the transposes of the vector rotations),
    /// the instrument frame to the spacecraft frame.</summary>
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
        public string Integrator { get; private init; }
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

        /// <summary>The worst difference between the states the scenario's Velocity-Verlet path stored and those of its twin,
        /// whose solution is the reference (0 on the other paths), m and m/s.</summary>
        public double TwinStates { get; private init; }
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
            ImpulseManeuver impulse = null;

            // The spacecraft of the case, its instruments and its attitude chain
            Spacecraft Build(int id, string name)
            {
                var spacecraft = new Spacecraft(id, name, 1000.0, 3000.0, new Clock(name + "_CLK", 65536), initial);
                var tank = new FuelTank("ft", "model", "sn", 1000.0, 1000.0);
                spacecraft.AddFuelTank(tank);
                var engine = new Engine("eng", "model", "sn", 300.0, 10.0, tank);
                spacecraft.AddEngine(engine);
                for (int i = 0; i < InstrumentOrientations.Length; i++)
                {
                    spacecraft.AddCircularInstrument(id * 1000 - 1 - i, $"I{i + 1}", "m", 0.5, Vector3.VectorZ, Vector3.VectorY, InstrumentOrientations[i]);
                }

                ImpulseManeuver Apogee()
                {
                    var apogee = new ApogeeHeightManeuver(earth, start.AddSeconds(100.0), TimeSpan.Zero, 7200000.0, engine);
                    impulse ??= apogee;
                    return apogee;
                }

                TriadAttitude TriadAfter() => new(start.AddSeconds(100.0), TimeSpan.Zero, Spacecraft.Front, earth, Spacecraft.Up, sun, engine);
                switch (law)
                {
                    case "None":
                        break;
                    case "Nadir":
                        spacecraft.SetStandbyManeuver(new NadirAttitude(earth, Min, TimeSpan.Zero, engine));
                        break;
                    case "Prograde":
                        spacecraft.SetStandbyManeuver(new ProgradeAttitude(earth, Min, TimeSpan.Zero, engine));
                        break;
                    case "InstrumentTriad":
                        // The boresight of the instrument at (0.3, 0.5, 0.2) to the Earth, its reference vector toward the Sun
                        spacecraft.SetStandbyManeuver(new TriadAttitude(Min, TimeSpan.Zero, spacecraft.Instruments.First(), earth, sun, engine));
                        break;
                    case "InstrumentPointing":
                        spacecraft.SetStandbyManeuver(new InstrumentPointingToAttitude(Min, TimeSpan.Zero, spacecraft.Instruments.First(), moon, engine));
                        break;
                    case "ImpulseAlone":
                        spacecraft.SetStandbyManeuver(Apogee());
                        break;
                    case "ImpulseTriad":
                    {
                        var apogee = Apogee();
                        apogee.SetNextManeuver(TriadAfter());
                        spacecraft.SetStandbyManeuver(apogee);
                        break;
                    }
                    case "NadirImpulseTriad":
                    {
                        var nadir = new NadirAttitude(earth, Min, TimeSpan.Zero, engine);
                        var apogee = Apogee();
                        nadir.SetNextManeuver(apogee);
                        apogee.SetNextManeuver(TriadAfter());
                        spacecraft.SetStandbyManeuver(nadir);
                        break;
                    }
                    case "Custom":
                        hooks.Configure(spacecraft, engine, earth, sun, start);
                        break;
                    default:
                        throw new ArgumentException(law);
                }

                return spacecraft;
            }

            string name = $"AR{-naifId}";
            var sc = Build(naifId, name);
            // RK7(8) in SSB mode: about the Sun, a tolerance of 1e-12 does not finish in minutes (item 7 of §8
            // of the design, #544)
            double tolerance = ssb ? SsbRk78Tolerance : 1e-12;

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
            double twinStates = 0.0;
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
                    // The solution the RK7(8) propagation keeps
                    solution = sc.LastPropagation;
                }
                else
                {
                    scenario.SimulateAsync(false, false, step).GetAwaiter().GetResult();
                    // The Velocity-Verlet path keeps no solution: that of a twin propagated by Spacecraft.Propagate with the
                    // scenario's bodies, in its order, whose stored states are compared with the scenario's
                    var twin = Build(naifId - 100000, name + "T");
                    solution = twin.Propagate(window, scenario.CelestialBodies, false, false, step);
                    var a = sc.PropagatedStates;
                    var b = twin.PropagatedStates;
                    twinStates = a.Length == b.Length ? a.Zip(b, (x, y) => x.Epoch == y.Epoch ? (x.Position - y.Position).Magnitude() + (x.Velocity - y.Velocity).Magnitude() : double.PositiveInfinity).Max() : double.PositiveInfinity;
                }
            }
            else if (integrator == "RK78")
            {
                solution = sc.Propagate(window, bodies, new RK78Integrator(tolerance, tolerance), false, false, step);
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
                Law = law, Integrator = integrator, Spacecraft = sc, Solution = solution, InitialFrame = frame, Ssb = ssb, Start = start, Earth = earth, Sun = sun,
                Moon = moon, Impulse = impulse, OutputStep = step, TwinStates = twinStates
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
        /// propagation rotated from its frame by <c>pxform</c>; in SSB mode, where the solution is relative to the Sun, less
        /// the state of the Earth relative to the Sun of <c>spkezr</c> on the planetary ephemeris (not the SPK the library
        /// wrote).</summary>
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

        // The geometric position of a body relative to the Earth in ICRF (spkezr)
        private Vector3 FromEarth(CelestialBody body, Time t) => SpiceAPI.Instance.ReadEphemeris(t, 399, body.NaifId, Frame.ICRF, Aberration.None).ToStateVector().Position;

        private Time? _change;

        /// <summary>The epoch of the impulse, the first discrete change of the record after the start.</summary>
        internal Time Change => _change ??= DiscreteChanges().First(x => x > Start);

        /// <summary>The closed form of the attitude at <paramref name="t"/> (a map of vectors from the spacecraft frame to
        /// ICRF) and whether it is a continuous law; null where the attitude has no closed form here: after the TRIAD that
        /// follows a nadir attitude, whose continuous attitude resumes at the next output (the attitude timeline, item 2 of
        /// §8 of the design, its own issue, #539).</summary>
        /// <remarks>Two expectations pin the behaviour of the library where it departs from its documentation, each a
        /// defect outside #537 with its own issue, listed in the description of #537: the instrument pointing at the
        /// Moon takes its geometric direction, although the maneuver asks for <c>Aberration.LT</c> (its light time is not
        /// applied to a celestial target, 4.4e-5 rad from this LEO, #549); the TRIAD after an impulse is held inertially from
        /// the change (item 2 of §8 of the design, #539), not recomputed.</remarks>
        internal (Func<Vector3, Vector3> Law, bool Continuous) LawAt(Time t)
        {
            switch (Law)
            {
                case "Custom":
                    return (null, false);
                case "None":
                    return (v => v, false);
                case "Nadir":
                case "Prograde":
                case "InstrumentTriad":
                case "InstrumentPointing":
                    return (ContinuousLaw(t), true);
                case "ImpulseAlone":
                    return t < Change ? (v => v, false) : (MinimalRotation(Spacecraft.Front, ImpulseInIcrf()), false);
                case "ImpulseTriad":
                    // Pin: the TRIAD held from the change (item 2 of §8 of the design, #539)
                    return t < Change ? (v => v, false) : (TriadAtChange(), false);
                case "NadirImpulseTriad":
                    return t < Change ? (ContinuousLaw(t), true) : (null, false);
                default:
                    throw new ArgumentException(Law);
            }
        }

        // The closed form of the continuous law of the case at t, null for a case without one
        private Func<Vector3, Vector3> ContinuousLaw(Time t)
        {
            switch (Law)
            {
                case "Nadir":
                case "NadirImpulseTriad":
                    return MinimalRotation(Spacecraft.Front, StateAt(t).Position.Inverse());
                case "Prograde":
                    return MinimalRotation(Spacecraft.Front, StateAt(t).Velocity);
                case "InstrumentTriad":
                {
                    var r = StateAt(t).Position;
                    var (b1, b2) = InstrumentAxesInSpacecraft();
                    return Triad(b1, b2, r.Inverse(), FromEarth(Sun, t) - r);
                }
                case "InstrumentPointing":
                {
                    // Pin: the geometric direction of the Moon, the light time not applied (#549, above)
                    var (b1, _) = InstrumentAxesInSpacecraft();
                    return MinimalRotation(b1, FromEarth(Moon, t) - StateAt(t).Position);
                }
                default:
                    return null;
            }
        }

        /// <summary>The span of the difference of <see cref="LawRateAt"/>, s.</summary>
        internal const double LawRateSpan = 0.1;

        /// <summary>The angular velocity of the continuous law at <paramref name="t"/> in the convention of the frames (ICRF
        /// relative to the spacecraft, in the spacecraft frame, -Lᵀω): the rotation of its closed form from t - 0.05 s to
        /// t + 0.05 s over that time, the span cut at the window and before the change of a nadir followed by an impulse
        /// (one-sided there: its error is the angular acceleration times half the cut, below 1e-7 rad/s).</summary>
        internal Vector3 LawRateAt(Time t)
        {
            double before = System.Math.Min(LawRateSpan / 2.0, (t - Start).TotalSeconds);
            double after = System.Math.Min(LawRateSpan / 2.0, (Start.AddSeconds(Length) - t).TotalSeconds);
            if (Law == "NadirImpulseTriad")
            {
                after = System.Math.Min(after, (Change - t).TotalSeconds - 1e-6);
            }

            var t0 = t.AddSeconds(-before);
            var t1 = t.AddSeconds(after);
            var l0 = ContinuousLaw(t0);
            var l1 = ContinuousLaw(t1);
            var l = ContinuousLaw(t);
            // L1 L0ᵀ, the rotation over the span, in ICRF
            var w = RotationVector(v => l1(Transpose(l0, v))) / (t1 - t0).TotalSeconds;
            return Transpose(l, w).Inverse();
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
