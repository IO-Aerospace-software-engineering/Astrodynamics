using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// Two LEO spacecraft about a point-mass Earth that cross at 60° with a 250 m miss at t0 + 1800 s, then at each node
/// crossing (the reproduction of #620 and #621), propagated over 3 h in RK7(8) at 60 s steps and written to two SPKs.
/// </summary>
public class CrossingEncounterCase : IDisposable
{
    internal const int Id1 = -620001;
    internal const int Id2 = -620002;

    public CrossingEncounterCase() : this(Id1, Id2, "CROSS", () => new RK78Integrator(absoluteTolerance: 1e-13,
        relativeTolerance: 1e-13, initialStepSize: 60.0, minStepSize: 1e-3, maxStepSize: 60.0))
    {
    }

    protected CrossingEncounterCase(int id1, int id2, string name, Func<RK78Integrator> integrator)
        : this(id1, id2, name, CrossingStates, integrator, integrator)
    {
    }

    protected CrossingEncounterCase(int id1, int id2, string name,
        Func<CelestialBody, TimeSystem.Time, (StateVector, StateVector)> states, Func<RK78Integrator> integrator1,
        Func<RK78Integrator> integrator2)
    {
        ProtectedId = id1;
        SecondaryId = id2;
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));
        T0 = new TimeSystem.Time(2026, 1, 20, 6, 0, 0, frame: TimeFrame.UTCFrame).ToTDB();
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, T0);
        var (s1, s2) = states(earth, T0);

        Sat1 = new Spacecraft(id1, name + "1", 500.0, 5000.0, new Clock(name + "1_CLK", 256), s1);
        Sat2 = new Spacecraft(id2, name + "2", 100.0, 1000.0, new Clock(name + "2_CLK", 256), s2);
        Window = new Window(T0, T0.AddSeconds(10800.0));
        Solution1 = Sat1.Propagate(Window, new CelestialItem[] { earth }, integrator1(), false, false, TimeSpan.FromSeconds(60.0));
        Solution2 = Sat2.Propagate(Window, new CelestialItem[] { earth }, integrator2(), false, false, TimeSpan.FromSeconds(60.0));

        _directory = Directory.CreateTempSubdirectory("io-crossing-");
        Spk1 = new FileInfo(Path.Combine(_directory.FullName, name.ToLowerInvariant() + "1.bsp"));
        Spk2 = new FileInfo(Path.Combine(_directory.FullName, name.ToLowerInvariant() + "2.bsp"));
        Sat1.WriteEphemeris(Spk1);
        Sat2.WriteEphemeris(Spk2);
        SpiceAPI.Instance.LoadKernels(Spk1);
        SpiceAPI.Instance.LoadKernels(Spk2);
    }

    // The encounter designed at t0 + 1800 s: equal circular orbits of 500 km inclined at 51.6° and 111.6°, the second
    // 150 m higher and 200 m off along the normal of the relative velocity
    private static (StateVector, StateVector) CrossingStates(CelestialBody earth, TimeSystem.Time t0)
    {
        var encounter = t0.AddSeconds(1800.0);
        double r = 6878137.0, v = System.Math.Sqrt(earth.GM / r);
        double i1 = 51.6 * IO.Astrodynamics.Constants.Deg2Rad, i2 = 111.6 * IO.Astrodynamics.Constants.Deg2Rad;
        var v1 = new Vector3(0, v * System.Math.Cos(i1), v * System.Math.Sin(i1));
        var v2 = new Vector3(0, v * System.Math.Cos(i2), v * System.Math.Sin(i2));
        var dv = v2 - v1;
        var u = new Vector3(0, -dv.Z, dv.Y).Normalize();
        var p1 = new Vector3(r, 0, 0);
        var p2 = p1 + new Vector3(150.0, 0, 0) + u * 200.0;
        return (new StateVector(p1, v1, earth, encounter, Frames.Frame.ICRF).AtEpoch(t0).ToStateVector(),
            new StateVector(p2, v2, earth, encounter, Frames.Frame.ICRF).AtEpoch(t0).ToStateVector());
    }

    /// <summary>
    /// A slow pass at t0: a circular orbit of 500 km in the equator, and a second one 50 m behind it along the track,
    /// drifting across the plane at 100 m times the mean motion: the distance oscillates between about 50 m and 112 m over
    /// each orbit, at relative speeds below 0.12 m/s (the third reviewer's probe in round 3 of the lot-6 review).
    /// </summary>
    protected static Func<CelestialBody, TimeSystem.Time, (StateVector, StateVector)> SlowPassStates(double crossTrackMeters) =>
        (earth, t0) =>
        {
            double r = 6878137.0, v = System.Math.Sqrt(earth.GM / r), n = v / r, angle = 50.0 / r;
            return (new StateVector(new Vector3(r, 0, 0), new Vector3(0, v, 0), earth, t0, Frames.Frame.ICRF),
                new StateVector(new Vector3(r * System.Math.Cos(angle), r * System.Math.Sin(angle), 0),
                    new Vector3(-v * System.Math.Sin(angle), v * System.Math.Cos(angle), n * crossTrackMeters), earth, t0,
                    Frames.Frame.ICRF));
        };

    private readonly DirectoryInfo _directory;

    public int ProtectedId { get; }
    public int SecondaryId { get; }
    public TimeSystem.Time T0 { get; }
    public Window Window { get; }
    public Spacecraft Sat1 { get; }
    public Spacecraft Sat2 { get; }
    public PropagationSolution Solution1 { get; }
    public PropagationSolution Solution2 { get; }
    public FileInfo Spk1 { get; }
    public FileInfo Spk2 { get; }

    public Window Seconds(double start, double end) => new(T0.AddSeconds(start), T0.AddSeconds(end));

    public void Dispose()
    {
        SpiceAPI.Instance.UnloadKernels(Spk1);
        SpiceAPI.Instance.UnloadKernels(Spk2);
        _directory.Delete(true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// The same crossing propagated with the defaults of <c>new RK78Integrator()</c> (47 steps, median 234 s): the minima of
/// the cubic Hermite of its steps are 12 to 212 µs before those of its states (#363), the band of S1 of the review of
/// lot 6 of #457.
/// </summary>
public sealed class DefaultStepCrossingEncounterCase : CrossingEncounterCase
{
    public DefaultStepCrossingEncounterCase() : base(-620011, -620012, "CROSSD", () => new RK78Integrator())
    {
    }
}

/// <summary>
/// The slow pass at 60 s steps: minima of about 50 m every orbit, maxima of about 112 m between them.
/// </summary>
public sealed class SlowPassEncounterCase : CrossingEncounterCase
{
    public SlowPassEncounterCase() : base(-620021, -620022, "SLOW", SlowPassStates(100.0), Steps60, Steps60)
    {
    }

    internal static RK78Integrator Steps60() => new(absoluteTolerance: 1e-13, relativeTolerance: 1e-13, initialStepSize: 60.0,
        minStepSize: 1e-3, maxStepSize: 60.0);
}

/// <summary>
/// The slow pass at the defaults of <c>new RK78Integrator()</c> (47 steps, median 235 s): the cubic Hermite of the steps
/// has minima near the maxima of the distance (round 3 of the lot-6 review), which the refinement on the range rate cannot
/// bracket.
/// </summary>
public sealed class DefaultStepSlowPassEncounterCase : CrossingEncounterCase
{
    public DefaultStepSlowPassEncounterCase() : base(-620031, -620032, "SLOWD", SlowPassStates(100.0),
        () => new RK78Integrator(), () => new RK78Integrator())
    {
    }
}

/// <summary>
/// The slow pass with a drift of 1 m across the plane, the first spacecraft at the default steps and the second at 30 s
/// steps: the distance stays within 50 m and 50.01 m, and the Hermite of the default steps, whose error no longer cancels
/// between the two, seeds minima three candidates or more from those of the states (round 4 of the lot-6 review).
/// </summary>
public sealed class MixedStepSlowPassEncounterCase : CrossingEncounterCase
{
    public MixedStepSlowPassEncounterCase() : base(-620041, -620042, "SLOWX", SlowPassStates(1.0), () => new RK78Integrator(),
        () => new RK78Integrator(absoluteTolerance: 1e-13, relativeTolerance: 1e-13, initialStepSize: 30.0, minStepSize: 1e-3,
            maxStepSize: 30.0))
    {
    }
}

/// <summary>
/// A pass with two minima of the distance per orbit and a maximum between them: a circular orbit of 500 km in the equator,
/// and the second spacecraft on the Clohessy-Wiltshire ellipse 1000 m above it, <c>y0</c> metres ahead along the track (the
/// fourth reviewer's probe in round 4 of the lot-6 review). With <c>y0</c> = 1490 m, minima of about 509.7 m at t0 + 1317.43
/// and 1526.28 s and of about 514.3 m at 6971.33 and 7224.79 s; with 1500 m, minima of 499.85 m at t0 + 1457.38 s and of
/// about 504.6 m at 7029.75 and 7173.55 s. At the default steps, the Hermite of the steps seeds the minimum at
/// t0 + 1526.28 s 1.6 s past it, where the range rate of the states is already positive, as are the candidates on either side.
/// </summary>
public abstract class CwPassEncounterCase : CrossingEncounterCase
{
    protected CwPassEncounterCase(int id1, int id2, string name, double y0, bool defaultSteps)
        : base(id1, id2, name, CwStates(y0), Steps(defaultSteps), Steps(defaultSteps))
    {
    }

    protected CwPassEncounterCase(int id1, int id2, string name, double y0, Func<RK78Integrator> integrator1,
        Func<RK78Integrator> integrator2)
        : base(id1, id2, name, CwStates(y0), integrator1, integrator2)
    {
    }

    private static Func<RK78Integrator> Steps(bool defaultSteps) =>
        defaultSteps ? () => new RK78Integrator() : SlowPassEncounterCase.Steps60;

    /// <summary>
    /// The step of CSPICE's <c>gfdist_c</c> for the reference minima: 1 s, the maxima lying less than 60 s from a minimum.
    /// </summary>
    public virtual double GfStepSeconds => 1.0;

    private static Func<CelestialBody, TimeSystem.Time, (StateVector, StateVector)> CwStates(double y0) =>
        (earth, t0) =>
        {
            const double b = 1000.0;
            double r = 6878137.0, v = System.Math.Sqrt(earth.GM / r), n = v / r;
            return (new StateVector(new Vector3(r, 0, 0), new Vector3(0, v, 0), earth, t0, Frames.Frame.ICRF),
                new StateVector(new Vector3(r + b, y0, 0), new Vector3(-n * y0, v - b * n, 0), earth, t0, Frames.Frame.ICRF));
        };
}

/// <summary>The pass with two minima per orbit, <c>y0</c> = 1490 m, at the defaults of <c>new RK78Integrator()</c>.</summary>
public sealed class CwPassDefaultStepCase : CwPassEncounterCase
{
    public CwPassDefaultStepCase() : base(-620051, -620052, "CWD", 1490.0, true)
    {
    }
}

/// <summary>The pass with two minima per orbit, <c>y0</c> = 1490 m, at 60 s steps.</summary>
public sealed class CwPass60StepCase : CwPassEncounterCase
{
    public CwPass60StepCase() : base(-620061, -620062, "CW60", 1490.0, false)
    {
    }
}

/// <summary>The pass with two minima per orbit, <c>y0</c> = 1500 m, at the defaults of <c>new RK78Integrator()</c>.</summary>
public sealed class CwPass1500DefaultStepCase : CwPassEncounterCase
{
    public CwPass1500DefaultStepCase() : base(-620071, -620072, "CWE", 1500.0, true)
    {
    }
}

/// <summary>The pass with two minima per orbit, <c>y0</c> = 1500 m, at 60 s steps.</summary>
public sealed class CwPass1500Step60Case : CwPassEncounterCase
{
    public CwPass1500Step60Case() : base(-620081, -620082, "CWE60", 1500.0, false)
    {
    }
}

/// <summary>
/// The pass with two minima per orbit, <c>y0</c> = 1502.358 m (the fifth reviewer's probe in round 5 of the lot-6 review): a
/// shallow minimum at t0 + 7065.55 s, 2.9e-7 m deep, next to a fold of the range rate, positive only over
/// [7065.56, 7067.17] s, then minima at t0 + 1438.02 and 7156.45 s. At 60 s steps the Hermite seeds the shallow minimum
/// 4.85 s before it with the range rate still negative, and the reads of the search from the seed, at distances doubling, stepped over
/// the 1.6 s where it is positive (at 4.096 and 8.192 s from the seed). CSPICE's step is 0.2 s, below that interval.
/// </summary>
public abstract class CwPassFoldEncounterCase : CwPassEncounterCase
{
    protected CwPassFoldEncounterCase(int id1, int id2, string name, bool defaultSteps)
        : base(id1, id2, name, 1502.358, defaultSteps)
    {
    }

    protected CwPassFoldEncounterCase(int id1, int id2, string name, Func<RK78Integrator> integrator1,
        Func<RK78Integrator> integrator2)
        : base(id1, id2, name, 1502.358, integrator1, integrator2)
    {
    }

    public override double GfStepSeconds => 0.2;
}

/// <summary>The pass with the fold of the range rate at the defaults of <c>new RK78Integrator()</c>.</summary>
public sealed class CwPassFoldDefaultStepCase : CwPassFoldEncounterCase
{
    public CwPassFoldDefaultStepCase() : base(-620091, -620092, "CWF", true)
    {
    }
}

/// <summary>The pass with the fold of the range rate at 60 s steps.</summary>
public sealed class CwPassFoldStep60Case : CwPassFoldEncounterCase
{
    public CwPassFoldStep60Case() : base(-620101, -620102, "CWF60", false)
    {
    }
}

/// <summary>
/// The pass with two minima per orbit, <c>y0</c> = 1500 m, the first spacecraft at the defaults of
/// <c>new RK78Integrator()</c> and the second at 60 s steps (#628): the Hermite of the mixed steps places no minimum near
/// the minimum at t0 + 7029.70 s, where the range rate closes at one candidate and recedes at the next.
/// </summary>
public sealed class CwPassMixedStep1500Case : CwPassEncounterCase
{
    public CwPassMixedStep1500Case() : base(-620111, -620112, "CWM", 1500.0, () => new RK78Integrator(),
        SlowPassEncounterCase.Steps60)
    {
    }
}

/// <summary>The pass with two minima per orbit, <c>y0</c> = 1490 m, at the default steps and 60 s steps (#628).</summary>
public sealed class CwPassMixedStep1490Case : CwPassEncounterCase
{
    public CwPassMixedStep1490Case() : base(-620121, -620122, "CWM90", 1490.0, () => new RK78Integrator(),
        SlowPassEncounterCase.Steps60)
    {
    }
}

/// <summary>
/// The pass with two minima per orbit, <c>y0</c> = 1495 m, the first spacecraft at 60 s steps and the second at the
/// defaults (#628): no seed after t0 + 1165.32 s, the range rate closing at t0 + 1440 s and receding at 1500 s about the
/// minimum at t0 + 1497.86 s.
/// </summary>
public sealed class CwPassMixedStep1495Case : CwPassEncounterCase
{
    public CwPassMixedStep1495Case() : base(-620131, -620132, "CWN", 1495.0, SlowPassEncounterCase.Steps60,
        () => new RK78Integrator())
    {
    }
}

/// <summary>
/// The pass with two minima per orbit, <c>y0</c> = 1490 m, at fixed 600 s steps (#628): the step [1200, 1800] s holds the
/// minima at t0 + 1317.45 and 1526.28 s and a maximum between them, its Hermite one minimum only, at 1502.10 s.
/// </summary>
public sealed class CwPassStep600Case : CwPassEncounterCase
{
    public CwPassStep600Case() : base(-620141, -620142, "CW600", 1490.0, () => new RK78Integrator(600.0),
        () => new RK78Integrator(600.0))
    {
    }
}

/// <summary>The pass with two minima per orbit, <c>y0</c> = 1500 m, at fixed 600 s steps (#628).</summary>
public sealed class CwPass1500Step600Case : CwPassEncounterCase
{
    public CwPass1500Step600Case() : base(-620151, -620152, "CWE600", 1500.0, () => new RK78Integrator(600.0),
        () => new RK78Integrator(600.0))
    {
    }
}

/// <summary>
/// The pass with the fold of the range rate, <c>y0</c> = 1502.358 m, the first spacecraft at the defaults of
/// <c>new RK78Integrator()</c> and the second at 60 s steps (round 1 of the review of #628): the range rate is positive
/// over [7065.14, 7067.49] s only, and no minimum of the Hermite lies before it, so that the reads 60 s apart step over it;
/// reads 1 s apart do not.
/// </summary>
public sealed class CwPassFoldMixedStepCase : CwPassFoldEncounterCase
{
    public CwPassFoldMixedStepCase() : base(-620161, -620162, "CWFM", () => new RK78Integrator(), SlowPassEncounterCase.Steps60)
    {
    }
}

/// <summary>
/// The conjunction analysis of kernel-backed spacecraft (#620), the window ends of the dense search (#621), and the reads
/// outside the coverage of the kernels (#454) or of the propagated states (#527). Expected values from CSPICE on the SPKs
/// (<c>spkezr_c</c>, <c>gfdist_c</c>) and from the case's geometry.
/// </summary>
public class ConjunctionKernelAndWindowTests : IClassFixture<CrossingEncounterCase>,
    IClassFixture<DefaultStepCrossingEncounterCase>, IClassFixture<SlowPassEncounterCase>,
    IClassFixture<DefaultStepSlowPassEncounterCase>, IClassFixture<MixedStepSlowPassEncounterCase>,
    IClassFixture<CwPassDefaultStepCase>, IClassFixture<CwPass60StepCase>, IClassFixture<CwPass1500DefaultStepCase>,
    IClassFixture<CwPass1500Step60Case>, IClassFixture<CwPassFoldDefaultStepCase>, IClassFixture<CwPassFoldStep60Case>,
    IClassFixture<CwPassMixedStep1500Case>, IClassFixture<CwPassMixedStep1490Case>, IClassFixture<CwPassMixedStep1495Case>,
    IClassFixture<CwPassStep600Case>, IClassFixture<CwPass1500Step600Case>, IClassFixture<CwPassFoldMixedStepCase>
{
    private static readonly ConjunctionAnalysisOptions Options = new() { CovariancePropagation = null };

    // The reads of the range rate over the window of the dense search (#628), opt-in (decision 54 of #457)
    private static readonly ConjunctionAnalysisOptions ReadOverWindowOptions = Options with { ReadRangeRateOverWindow = true };

    private readonly CrossingEncounterCase _case;
    private readonly DefaultStepCrossingEncounterCase _defaultStepCase;
    private readonly SlowPassEncounterCase _slowCase;
    private readonly DefaultStepSlowPassEncounterCase _defaultStepSlowCase;
    private readonly MixedStepSlowPassEncounterCase _mixedStepSlowCase;
    private readonly CwPassDefaultStepCase _cwDefaultCase;
    private readonly CwPass60StepCase _cw60Case;
    private readonly CwPass1500DefaultStepCase _cw1500DefaultCase;
    private readonly CwPass1500Step60Case _cw1500Step60Case;
    private readonly CwPassFoldDefaultStepCase _cwFoldDefaultCase;
    private readonly CwPassFoldStep60Case _cwFoldStep60Case;
    private readonly CwPassMixedStep1500Case _cwMixed1500Case;
    private readonly CwPassMixedStep1490Case _cwMixed1490Case;
    private readonly CwPassMixedStep1495Case _cwMixed1495Case;
    private readonly CwPassStep600Case _cw600Case;
    private readonly CwPass1500Step600Case _cw1500Step600Case;
    private readonly CwPassFoldMixedStepCase _cwFoldMixedCase;
    private readonly ITestOutputHelper _output;

    public ConjunctionKernelAndWindowTests(CrossingEncounterCase crossingCase,
        DefaultStepCrossingEncounterCase defaultStepCase, SlowPassEncounterCase slowCase,
        DefaultStepSlowPassEncounterCase defaultStepSlowCase, MixedStepSlowPassEncounterCase mixedStepSlowCase,
        CwPassDefaultStepCase cwDefaultCase, CwPass60StepCase cw60Case, CwPass1500DefaultStepCase cw1500DefaultCase,
        CwPass1500Step60Case cw1500Step60Case, CwPassFoldDefaultStepCase cwFoldDefaultCase,
        CwPassFoldStep60Case cwFoldStep60Case, CwPassMixedStep1500Case cwMixed1500Case,
        CwPassMixedStep1490Case cwMixed1490Case, CwPassMixedStep1495Case cwMixed1495Case, CwPassStep600Case cw600Case,
        CwPass1500Step600Case cw1500Step600Case, CwPassFoldMixedStepCase cwFoldMixedCase, ITestOutputHelper output)
    {
        _cwFoldMixedCase = cwFoldMixedCase;
        _cwMixed1500Case = cwMixed1500Case;
        _cwMixed1490Case = cwMixed1490Case;
        _cwMixed1495Case = cwMixed1495Case;
        _cw600Case = cw600Case;
        _cw1500Step600Case = cw1500Step600Case;
        _cwFoldDefaultCase = cwFoldDefaultCase;
        _cwFoldStep60Case = cwFoldStep60Case;
        _cwDefaultCase = cwDefaultCase;
        _cw60Case = cw60Case;
        _cw1500DefaultCase = cw1500DefaultCase;
        _cw1500Step60Case = cw1500Step60Case;
        _case = crossingCase;
        _defaultStepCase = defaultStepCase;
        _slowCase = slowCase;
        _defaultStepSlowCase = defaultStepSlowCase;
        _mixedStepSlowCase = mixedStepSlowCase;
        _output = output;
    }

    // ---- #620: a kernel-backed protected spacecraft gets the RTN of its orbit about the Earth ----

    [Fact]
    public void KernelBacked_DefaultCentreIsTheEarth_RtnIsCspiceAboutTheEarth()
    {
        var k1 = new Spacecraft(CrossingEncounterCase.Id1, "K1", _case.T0.AddSeconds(10.0));
        var k2 = new Spacecraft(CrossingEncounterCase.Id2, "K2", _case.T0.AddSeconds(10.0));
        Assert.Equal(PlanetsAndMoons.EARTH.NaifId, k1.InitialOrbitalParameters.Observer.NaifId);

        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(k1), k2, _case.Seconds(1200.0, 2400.0), Options);

        var (position, velocity) = CspiceRtn(encounter.EncounterState.Epoch, PlanetsAndMoons.EARTH.NaifId);
        AssertRtn(encounter, position, velocity, "Earth");

        // The design of the case, about the Earth at the encounter: (150, -100 √3, -100) m, |Δv| along T and N. The
        // relative velocity, 7.6 km/s along (0, -1/2, √3/2) in RTN, moves the RTN position by 0.76 mm over a tick. At
        // the tick nearer the zero (#624) the deltas are -1.282e-8, -8.817e-5 and 1.527e-4 m, the same on the three
        // platforms: the bound is twice the worst over Linux x64, Windows x64 and macOS arm64 (CI of #629 at 8c864b23,
        // 2026-11-10), 3.1e-4 m. Before #624, one tick early, 1.003e-8, 3.656e-4 and -6.333e-4 m, then checked to two
        // decimals against -173.205 m, a rounding boundary that the tick before the zero happened to meet
        var rtn = encounter.EncounterState.RelativeState.RelativePositionRtn;
        var design = new Vector3(150.0, -100.0 * System.Math.Sqrt(3.0), -100.0);
        _output.WriteLine($"MEASURE #620 RTN - design: {rtn.X - design.X:E3} {rtn.Y - design.Y:E3} {rtn.Z - design.Z:E3} m");
        Assert.Equal(design.X, rtn.X, 3.1e-4);
        Assert.Equal(design.Y, rtn.Y, 3.1e-4);
        Assert.Equal(design.Z, rtn.Z, 3.1e-4);
    }

    [Fact]
    public void KernelBacked_BarycentreGiven_RtnIsCspiceAboutTheBarycentre()
    {
        var ssb = new Barycenter(0);
        var k1 = new Spacecraft(CrossingEncounterCase.Id1, "K1", _case.T0.AddSeconds(10.0), centerOfMotion: ssb);
        var k2 = new Spacecraft(CrossingEncounterCase.Id2, "K2", _case.T0.AddSeconds(10.0), centerOfMotion: ssb);
        Assert.Equal(0, k1.InitialOrbitalParameters.Observer.NaifId);

        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(k1), k2, _case.Seconds(1200.0, 2400.0), Options);

        var (position, velocity) = CspiceRtn(encounter.EncounterState.Epoch, 0);
        AssertRtn(encounter, position, velocity, "SSB");
    }

    // The relative state of K2 from K1 in the RTN of K1 about the observer, from CSPICE's spkezr_c: R = r/|r|,
    // N = (r × v)/|r × v|, T = N × R; the velocity a pure rotation (CCSDS 508.0 annex E)
    private static (Vector3 Position, Vector3 Velocity) CspiceRtn(TimeSystem.Time epoch, int observer)
    {
        var s1 = CspiceState(epoch, CrossingEncounterCase.Id1, observer);
        var s2 = CspiceState(epoch, CrossingEncounterCase.Id2, observer);
        var dr = s2.Position - s1.Position;
        var dv = s2.Velocity - s1.Velocity;
        var rHat = s1.Position.Normalize();
        var nHat = s1.Position.Cross(s1.Velocity).Normalize();
        var tHat = nHat.Cross(rHat);
        return (new Vector3(dr * rHat, dr * tHat, dr * nHat), new Vector3(dv * rHat, dv * tHat, dv * nHat));
    }

    private void AssertRtn(EncounterCase encounter, Vector3 position, Vector3 velocity, string observer)
    {
        var relative = encounter.EncounterState.RelativeState;
        double positionError = (relative.RelativePositionRtn - position).Magnitude();
        double velocityError = (relative.RelativeVelocityRtn - velocity).Magnitude();
        _output.WriteLine($"RTN about the {observer}: position {positionError:E3} m, velocity {velocityError:E3} m/s");

        // Twice the worst over both observers and over Linux x64, Windows x64 and macOS arm64 (CI of #622 at 7f5e6dfb,
        // 2026-11-10), 2.842e-14 m and 9.095e-13 m/s, the same on the three: IO reads the same spkezr_c states (before
        // #620, about the Earth: 316.6 m and 7557.5 m/s off, the RTN of the orbit about the SSB)
        Assert.True(positionError <= 5.7e-14, $"position {positionError:E3} m");
        Assert.True(velocityError <= 1.9e-12, $"velocity {velocityError:E3} m/s");
    }

    // ---- #621: the window ends are no close approaches of the dense search ----

    [Fact]
    public void AnalyzeAll_Dense_ListsTheLocalMinimaOfCspice()
    {
        var all = ConjunctionAssessment.AnalyzeAll(new ProtectedSpacecraftProfile(_case.Sat1), _case.Solution1, _case.Sat2,
            _case.Solution2, _case.Window, Options);
        var locmin = CspiceLocalMinima(_case.Window);

        // 4 minima; before 11.0.0 the window start, 6277 km away and receding, was listed first
        Assert.Equal(4, locmin.Length);
        Assert.Equal(locmin.Length, all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            AssertIsMinimum(all[i], locmin[i]);
        }
    }

    [Fact]
    public void Analyze_Dense_EndCloserThanTheInteriorMinimum_ReturnsTheInteriorMinimum()
    {
        // Starts 0.1 s after the 250 m minimum: the start, about 800 m away and receding, is closer than the interior
        // minimum of 939 m at t0 + 4638.5 s, which CSPICE's ABSMIN shows
        var window = _case.Seconds(1800.1, 4700.0);
        var absmin = SpiceAPI.Instance.FindWindowsOnDistanceConstraint(window, CrossingEncounterCase.Id1, CrossingEncounterCase.Id2,
            RelationnalOperator.AbsoluteMin, 0.0, Aberration.None, TimeSpan.FromSeconds(60.0)).Single();
        Assert.Equal(0.0, (absmin.StartDate - window.StartDate).TotalSeconds, 3);

        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(_case.Sat1), _case.Solution1, _case.Sat2,
            _case.Solution2, window, Options);

        AssertIsMinimum(encounter, CspiceLocalMinima(window).Single());
    }

    [Fact]
    public void Dense_MonotonicWindow_AnalyzeAllIsEmpty_AnalyzeKeepsTheEnd()
    {
        // A monotonic approach: no local minimum (CSPICE), so AnalyzeAll lists none; Analyze keeps the end where the
        // distance grows into the window, as before (what it returns there is #621's own change)
        var window = _case.Seconds(1200.0, 1500.0);
        Assert.Empty(CspiceLocalMinima(window));
        var profile = new ProtectedSpacecraftProfile(_case.Sat1);

        Assert.Empty(ConjunctionAssessment.AnalyzeAll(profile, _case.Solution1, _case.Sat2, _case.Solution2, window, Options));

        var encounter = ConjunctionAssessment.Analyze(profile, _case.Solution1, _case.Sat2, _case.Solution2, window, Options);
        Assert.Equal(window.EndDate, encounter.EncounterState.Epoch);

        // The sampled path keeps its midpoint fallback
        var sampled = ConjunctionAssessment.AnalyzeAll(profile, _case.Sat2, window, Options);
        Assert.Equal(window.StartDate.AddSeconds(150.0), Assert.Single(sampled).EncounterState.Epoch);
    }

    // The local minima of the distance on the two SPKs: those of CSPICE's gfdist_c (LOCMIN, step 60 s, its convergence
    // tolerance of 1e-6 s), each refined to the zero of the range rate of CSPICE's states by secants (1 ms base) to an
    // epoch on the grid of ticks (Time.AddSeconds truncates to the tick), then below the tick (CspiceZeroBelowTheTick): the
    // zero is Epoch + Offset, Offset in s, and Distance the distance there; the secants read the SPKs within their
    // coverage only. The passes with two minima per orbit take the step of their case (1 s, 0.2 s with the fold): their
    // maxima lie less than 60 s from a minimum, which a step of 60 s steps over
    private (TimeSystem.Time Epoch, double Offset, double Distance)[] CspiceLocalMinima(Window window,
        CrossingEncounterCase crossing = null)
    {
        crossing ??= _case;
        var step = TimeSpan.FromSeconds(crossing is CwPassEncounterCase cw ? cw.GfStepSeconds : 60.0);
        var first = crossing.Window.StartDate;
        var last = crossing.Window.EndDate.AddSeconds(-1.0e-3);
        return SpiceAPI.Instance.FindWindowsOnDistanceConstraint(window, crossing.ProtectedId, crossing.SecondaryId,
                RelationnalOperator.LocalMin, 0.0, Aberration.None, step)
            .Select(w =>
            {
                var epoch = w.StartDate;
                for (int iteration = 0; iteration < 4; iteration++)
                {
                    double f0 = CspiceRangeRate(epoch, crossing);
                    double f1 = CspiceRangeRate(epoch.AddSeconds(1.0e-3), crossing);
                    if (f1 == f0) break;
                    epoch = epoch.AddSeconds(-f0 * 1.0e-3 / (f1 - f0));
                    epoch = epoch < first ? first : epoch > last ? last : epoch;
                }

                var (offset, distance) = CspiceZeroBelowTheTick(epoch, crossing);
                return (epoch, offset, distance);
            })
            .ToArray();
    }

    // CSPICE's minimum within the TCA bound of a listed epoch near a window end, searched over [epoch − w, epoch + w],
    // w = max(2 × bound, 1 s), within the propagation; none when CSPICE has no minimum there
    private (TimeSystem.Time Epoch, double Offset, double Distance)? CspiceMinimumNear(TimeSystem.Time epoch, Window window, double tcaBound,
        CrossingEncounterCase crossing)
    {
        double w = System.Math.Max(2.0 * tcaBound, 1.0);
        var start = epoch.AddSeconds(-w);
        var end = epoch.AddSeconds(w);
        start = start < crossing.Window.StartDate ? crossing.Window.StartDate : start;
        end = end > crossing.Window.EndDate ? crossing.Window.EndDate : end;
        var near = CspiceLocalMinima(new Window(start, end), crossing)
            .Where(m => System.Math.Abs((m.Epoch - epoch).TotalSeconds) <= tcaBound)
            .ToArray();
        return near.Length == 0 ? null : near.MinBy(m => System.Math.Abs((m.Epoch - epoch).TotalSeconds));
    }

    private static (Vector3 Position, Vector3 Velocity) CspiceRelative(TimeSystem.Time epoch, CrossingEncounterCase crossing = null)
    {
        var s1 = CspiceState(epoch, crossing?.ProtectedId ?? CrossingEncounterCase.Id1);
        var s2 = CspiceState(epoch, crossing?.SecondaryId ?? CrossingEncounterCase.Id2);
        return (s2.Position - s1.Position, s2.Velocity - s1.Velocity);
    }

    private static double CspiceRangeRate(TimeSystem.Time epoch, CrossingEncounterCase crossing = null)
    {
        var (position, velocity) = CspiceRelative(epoch, crossing);
        return position * velocity;
    }

    private static StateVector CspiceState(TimeSystem.Time epoch, int id, int observer = 399)
    {
        return SpiceAPI.Instance.ReadEphemeris(epoch, observer, id, Frames.Frame.ICRF, Aberration.None).ToStateVector();
    }

    private void AssertIsMinimum(EncounterCase encounter, (TimeSystem.Time Epoch, double Offset, double Distance) reference,
        double missBound = 2.3e-8)
    {
        double tcaError = (encounter.EncounterState.Epoch - reference.Epoch).TotalSeconds - reference.Offset;
        double missError = encounter.EncounterState.MissDistanceMeters - reference.Distance;
        _output.WriteLine($"TCA t0 + {(encounter.EncounterState.Epoch - _case.T0).TotalSeconds:F7} s: {tcaError:E3} s " +
                          $"(CSPICE's zero {reference.Offset:E3} s from its tick), miss {missError:E3} m");
        _worstTcaError = System.Math.Max(_worstTcaError, System.Math.Abs(tcaError));
        _worstMissError = System.Math.Max(_worstMissError, System.Math.Abs(missError));

        // The TCA, against CSPICE's zero below the tick (reference.Offset from its epoch on the grid of ticks): twice
        // the worst over Linux x64, Windows x64 and macOS arm64 (CI of #629 at 8c864b23, 2026-11-10; the same on the
        // three), 8.949e-8 s, rounded up to two digits. At the tick nearer the zero (#624), 6.430e-8 s at 60 s steps
        // (#621, S1 and F1) and 8.949e-8 s at the default steps (S1), where the states and their SPK place the zero
        // further apart; before #624, against the same reference, 1.525e-7 s and 1.493e-7 s. Before, CSPICE's minimum
        // was taken on the grid of ticks (Time.AddSeconds truncates to the tick), up to 0.96 tick from its zero: 1e-7 s
        // and 2e-7 s measured, bounds 2e-7 s and 4e-7 s. The miss: twice the worst over Linux x64, Windows x64 and
        // macOS arm64 (CI of #622 at 7f5e6dfb, 2026-11-10; the same on the three), 1.129e-8 m, the propagation against
        // its SPK (degree-7 interpolation of the 60 s outputs); 1.115e-8 m against CSPICE's distance at its zero below
        // the tick, before #624 (Linux x64) and after it (the same on the three, CI of #629 at 8c864b23, 2026-11-10).
        // Measured again in round 3 of the lot-6 review over every window here, those of 1 and 3 µs and of 1 to 3 ticks
        // about the minima included: 1.129e-8 m, the TCA t0 + 4638.5363740 s of a window starting two ticks before the
        // minimum; against the second reviewer's spiceypy distance at the minimum, over its 341 windows, 1.143e-8 m (its
        // 1.16e-8 m printed the library's miss to 1e-9 m): 2.3e-8 m is twice the worst, rounded up to two digits
        Assert.True(System.Math.Abs(tcaError) <= 1.8e-7, $"TCA {tcaError:E3} s");
        Assert.True(System.Math.Abs(missError) <= missBound, $"miss {missError:E3} m");
    }

    // ---- S1 of the review of lot 6 of #457: a window end within the Hermite error of a minimum ----

    // Offsets of the window ends from the minima: 10 µs to 1 ms, inside and past the band of the default steps (the
    // minima of the Hermite 12, 62, 139 and 212 µs before those of the states; below 1 µs at 60 s steps)
    private static readonly double[] EndOffsets = { 1.0e-6, 3.0e-6, 1.0e-5, 3.0e-5, 1.0e-4, 3.0e-4, 1.0e-3 };

    private double _worstTcaError;
    private double _worstMissError;

    private CrossingEncounterCase Crossing(string steps) => steps == "60 s" ? _case : _defaultStepCase;

    // The bound of the miss distance against the SPK: #621's at 60 s steps (1.129e-8 m measured again here, 1.115e-8 m
    // against CSPICE's distance at its zero below the tick); at the default steps, twice the worst over Linux x64,
    // Windows x64 and macOS arm64 (CI of #622 at 7f5e6dfb, 2026-11-10; the same on the three), 2.740e-7 m, on the whole
    // window as on every window of S1: the states of RK7(8) at 1e-9 inside a step of 234 s against the degree-7
    // interpolation of their 60 s outputs, whatever the window; 2.744e-7 m at the tick nearer the zero (#624, Linux
    // x64, 2026-11-10), the same bound (the TCA bound, 1.8e-7 s, is that of AssertIsMinimum, 8.949e-8 s measured)
    private double MissBound(CrossingEncounterCase crossing) => crossing == _case ? 2.3e-8 : DefaultStepMissBound;

    private const double DefaultStepMissBound = 5.5e-7;

    [Fact]
    public void AnalyzeAll_Dense_DefaultSteps_ListsTheLocalMinimaOfCspice()
    {
        // The whole window, no end near a minimum: the miss of the default steps against their SPK
        var crossing = _defaultStepCase;
        AssertWindowMatchesCspice(crossing, crossing.Window, expectedCount: 4);
        _output.WriteLine($"MEASURE S1 whole window default: TCA {_worstTcaError:E3} s, miss {_worstMissError:E3} m");
    }

    [Theory]
    [InlineData("60 s", 0)]
    [InlineData("60 s", 1)]
    [InlineData("60 s", 2)]
    [InlineData("60 s", 3)]
    [InlineData("default", 0)]
    [InlineData("default", 1)]
    [InlineData("default", 2)]
    [InlineData("default", 3)]
    public void Dense_WindowEndingJustBeforeAMinimum_DoesNotListIt(string steps, int minimum)
    {
        // Before, a window ending inside the band listed the minimum of the Hermite, unrefined: a point still closing
        var crossing = Crossing(steps);
        var minima = CspiceLocalMinima(crossing.Window, crossing);
        Assert.Equal(4, minima.Length);
        var tca = minima[minimum].Epoch;
        foreach (double offset in EndOffsets)
        {
            // No minimum inside: AnalyzeAll lists none, Analyze returns the end, where the distance grows into the window
            AssertWindowMatchesCspice(crossing, new Window(tca.AddSeconds(-600.0), tca.AddSeconds(-offset)), expectedCount: 0);

            // The previous minimum inside: AnalyzeAll lists it alone, Analyze returns it
            if (minimum > 0)
            {
                var previous = minima[minimum - 1].Epoch;
                AssertWindowMatchesCspice(crossing, new Window(previous.AddSeconds(-600.0), tca.AddSeconds(-offset)),
                    expectedCount: 1);
            }

            // Past the minimum, the window holds it
            AssertWindowMatchesCspice(crossing, new Window(tca.AddSeconds(-600.0), tca.AddSeconds(offset)), expectedCount: 1);
        }

        _output.WriteLine($"MEASURE S1 window end {steps} minimum {minimum}: TCA {_worstTcaError:E3} s, miss {_worstMissError:E3} m");
    }

    [Theory]
    [InlineData("60 s", 0)]
    [InlineData("60 s", 1)]
    [InlineData("60 s", 2)]
    [InlineData("60 s", 3)]
    [InlineData("default", 0)]
    [InlineData("default", 1)]
    [InlineData("default", 2)]
    [InlineData("default", 3)]
    public void Dense_WindowStartingJustBeforeAMinimum_ListsIt(string steps, int minimum)
    {
        // Before, a window starting inside the band listed nothing: the minimum of the Hermite lay before the start
        var crossing = Crossing(steps);
        var minima = CspiceLocalMinima(crossing.Window, crossing);
        Assert.Equal(4, minima.Length);
        var tca = minima[minimum].Epoch;
        foreach (double offset in EndOffsets)
        {
            AssertWindowMatchesCspice(crossing, new Window(tca.AddSeconds(-offset), tca.AddSeconds(400.0)), expectedCount: 1);

            // Starting past the minimum: none inside but the next one, which Analyze returns, never the start, closer
            var end = minimum < minima.Length - 1 ? minima[minimum + 1].Epoch.AddSeconds(400.0) : tca.AddSeconds(400.0);
            AssertWindowMatchesCspice(crossing, new Window(tca.AddSeconds(offset), end),
                expectedCount: minimum < minima.Length - 1 ? 1 : 0);
        }

        _output.WriteLine($"MEASURE S1 window start {steps} minimum {minimum}: TCA {_worstTcaError:E3} s, miss {_worstMissError:E3} m");
    }

    // ---- F1 of the round-2 review of lot 6 of #457: a window flat within the tolerance of the seeds ----

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Dense_WindowOfAFewTicksAboutAMinimum_ListsTheMinimumOfCspice(int minimum)
    {
        // Windows of 1 to 3 ticks (100 ns) on each side of a minimum, at 60 s steps: the distances of the candidates all
        // lie within the flatness tolerance of the seeds, so before, no seed survived and AnalyzeAll listed nothing
        // although the range rate goes from negative to positive inside. With them, windows ending or starting 1 to 3
        // ticks past the minimum, 400 s on the other side (the windows of the second reviewer's worst miss, a window
        // starting on the tick before the zero). The states and their SPK place the zero of the range rate apart by up to
        // the TCA accuracy of #621 (the band of the round-2 review: 1.3e-7 s on the grid of ticks; the tick nearer the zero
        // within 6.4e-8 s of CSPICE's zero below the tick, #624), so an end one tick from the minimum may hold it
        // for CSPICE and not for the states, or the reverse: there AnalyzeAll lists at most one minimum, strictly inside.
        // Both ends two ticks or more from it, CSPICE's range rate goes from negative to positive and its gfdist_c LOCMIN
        // lists the minimum: AnalyzeAll lists it, within #621's bounds, and Analyze returns it
        var crossing = _case;
        var tca = CspiceLocalMinima(crossing.Window, crossing)[minimum].Epoch;
        var profile = new ProtectedSpacecraftProfile(crossing.Sat1);
        const long farTicks = 400 * TimeSpan.TicksPerSecond;
        for (long ticks = 1; ticks <= 3; ticks++)
        {
            foreach (var (startTicks, endTicks) in new[] { (-ticks, ticks), (-ticks, farTicks), (-farTicks, ticks) })
            {
                var window = new Window(tca.AddTicks(startTicks), tca.AddTicks(endTicks));
                var locmin = CspiceLocalMinima(window, crossing);
                double startRate = CspiceRangeRate(window.StartDate, crossing);
                double endRate = CspiceRangeRate(window.EndDate, crossing);
                var all = ConjunctionAssessment.AnalyzeAll(profile, crossing.Solution1, crossing.Sat2, crossing.Solution2,
                    window, Options);
                var encounter = ConjunctionAssessment.Analyze(profile, crossing.Solution1, crossing.Sat2,
                    crossing.Solution2, window, Options);
                double analyzeSeconds = (encounter.EncounterState.Epoch - crossing.T0).TotalSeconds;
                _output.WriteLine($"[{startTicks}, {endTicks}] ticks: CSPICE range rate {startRate:E3} {endRate:E3} m²/s, " +
                                  $"LOCMIN {locmin.Length}, AnalyzeAll {all.Count}, Analyze t0 + {analyzeSeconds:F7} s");

                Assert.True(all.Count <= 1);
                Assert.All(all, e => Assert.True(e.EncounterState.Epoch > window.StartDate &&
                                                 e.EncounterState.Epoch < window.EndDate));
                Assert.True(encounter.EncounterState.Epoch >= window.StartDate &&
                            encounter.EncounterState.Epoch <= window.EndDate);
                if (ticks == 1)
                {
                    continue;
                }

                Assert.True(startRate < 0.0 && endRate > 0.0);
                AssertIsMinimum(Assert.Single(all), Assert.Single(locmin));
                AssertIsMinimum(encounter, locmin[0]);
            }
        }

        _output.WriteLine($"MEASURE F1 minimum {minimum}: TCA {_worstTcaError:E3} s, miss {_worstMissError:E3} m");
    }

    // AnalyzeAll lists the local minima of CSPICE's gfdist_c in the window, each within the bounds of #621, and Analyze
    // returns the closest of them; without one, the window end where the distance grows into the window by CSPICE's range
    // rate (the closer one when both do)
    private void AssertWindowMatchesCspice(CrossingEncounterCase crossing, Window window, int expectedCount)
    {
        var profile = new ProtectedSpacecraftProfile(crossing.Sat1);
        var locmin = CspiceLocalMinima(window, crossing);
        Assert.Equal(expectedCount, locmin.Length);

        var all = ConjunctionAssessment.AnalyzeAll(profile, crossing.Solution1, crossing.Sat2, crossing.Solution2, window, Options);
        Assert.Equal(locmin.Length, all.Count);
        double missBound = MissBound(crossing);
        for (int i = 0; i < all.Count; i++)
        {
            AssertIsMinimum(all[i], locmin[i], missBound);
        }

        var encounter = ConjunctionAssessment.Analyze(profile, crossing.Solution1, crossing.Sat2, crossing.Solution2, window, Options);
        if (locmin.Length > 0)
        {
            AssertIsMinimum(encounter, locmin.MinBy(m => m.Distance), missBound);
            return;
        }

        var ends = new[] { window.StartDate, window.EndDate }
            .Where(epoch => epoch == window.StartDate
                ? CspiceRangeRate(epoch, crossing) >= 0.0
                : CspiceRangeRate(epoch, crossing) <= 0.0)
            .ToArray();
        Assert.NotEmpty(ends);
        Assert.Equal(ends.MinBy(epoch => CspiceRelative(epoch, crossing).Position.Magnitude()), encounter.EncounterState.Epoch);
    }

    // ---- Round 4 of the lot-6 review: a maximum of the distance is no close approach ----

    // The bounds of the TCA and of the miss distance of the slow passes against CSPICE on their SPKs, twice the worst
    // over every minimum of each propagation, over Linux x64, Windows x64 and macOS arm64 (CI of #622 at 7f5e6dfb,
    // 2026-11-10) and the reviewers' probes (Linux x64), rounded up to two digits. Measured by
    // Dense_EveryMinimumOfThePass_IsListedWithinTheBounds (1222 to 1527 windows, those that end or start at the zero
    // included, a listed minimum within the TCA bound of an end measured against CSPICE's minimum just outside the
    // window, against CSPICE's zero below the tick) and by the reviewers' probes on the same propagations (their 634 to
    // 1571 windows, gfdist_c refined by bisection of spkezr_c's range rate to 1e-10 s), the larger of the two. This
    // test gives the same figures on the three platforms but CWE (TCA 3.068e-6 s on Linux x64, 3.205e-6 s on Windows
    // x64, 3.282e-6 s on macOS arm64; miss 9.269e-8, 9.212e-8 and 9.206e-8 m) and CWF (miss 9.685e-7 m on Linux x64 and
    // macOS arm64, 9.684e-7 m on Windows x64); the worst over the three:
    //                                         this test                 the probe                 bound
    //   SLOW (60 s steps)                     1.272e-7 s  3.136e-10 m   1.480e-7 s  2.294e-10 m   3.0e-7 s  6.3e-10 m
    //   SLOWD (default steps)                 2.907e-5 s  1.147e-6 m    2.903e-5 s  1.147e-6 m    5.9e-5 s  2.3e-6 m
    //   SLOWX (default and 30 s steps, 1 m)   5.047e-2 s  2.074e-7 m    5.041e-2 s  2.072e-7 m    1.1e-1 s  4.2e-7 m
    //   CWD (two minima per orbit, default)   6.498e-7 s  4.732e-8 m    7.190e-7 s  4.807e-8 m    1.5e-6 s  9.7e-8 m
    //   CW60 (60 s steps)                     7.096e-7 s  4.702e-8 m    7.110e-7 s  4.753e-8 m    1.5e-6 s  9.6e-8 m
    //   CWE (y0 = 1500 m, default)            3.282e-6 s  9.269e-8 m    3.025e-6 s  9.163e-8 m    6.6e-6 s  1.9e-7 m
    //   CWE60 (y0 = 1500 m, 60 s steps)       1.233e-6 s  4.893e-8 m    1.270e-6 s  4.905e-8 m    2.6e-6 s  9.9e-8 m
    //   CWF (the fold, default)               2.094e-3 s  9.685e-7 m    2.083e-3 s  9.684e-7 m    4.2e-3 s  2.0e-6 m
    //   CWF60 (the fold, 60 s steps)          3.338e-5 s  3.864e-8 m    3.232e-5 s  3.848e-8 m    6.7e-5 s  7.8e-8 m
    // At the tick nearer the zero (#624), this test over Linux x64, Windows x64 and macOS arm64 (CI of #629 at
    // 8c864b23, 2026-11-10; 1187 to 1524 windows: the windows about the listed minima now coincide more often with
    // those about CSPICE's), the same on the three but CWE (TCA 3.068e-6 s on Linux x64, 3.205e-6 s on Windows x64,
    // 3.282e-6 s on macOS arm64; miss 9.269e-8, 9.212e-8 and 9.206e-8 m, as on lot 6), and the probes, run again on
    // #624 by its verifier (Linux x64, 634 to 911 windows, gfdist_c refined by bisection of spkezr_c's range rate to
    // 1e-10 s), give, the worst over the three:
    //                                         this test                 the probe                 bound
    //   SLOW (60 s steps)                     1.272e-7 s  3.136e-10 m   1.480e-7 s  2.294e-10 m   3.0e-7 s  6.3e-10 m
    //   SLOWD (default steps)                 2.907e-5 s  1.147e-6 m    2.903e-5 s  1.147e-6 m    5.9e-5 s  2.3e-6 m
    //   SLOWX (default and 30 s steps, 1 m)   5.048e-2 s  2.076e-7 m    5.041e-2 s  2.072e-7 m    1.1e-1 s  4.2e-7 m
    //   CWD (two minima per orbit, default)   6.498e-7 s  4.732e-8 m    6.190e-7 s  4.779e-8 m    1.3e-6 s  9.7e-8 m
    //   CW60 (60 s steps)                     7.096e-7 s  4.702e-8 m    6.110e-7 s  4.753e-8 m    1.5e-6 s  9.6e-8 m
    //   CWE (y0 = 1500 m, default)            3.282e-6 s  9.269e-8 m    3.025e-6 s  9.163e-8 m    6.6e-6 s  1.9e-7 m
    //   CWE60 (y0 = 1500 m, 60 s steps)       1.233e-6 s  4.893e-8 m    1.170e-6 s  4.892e-8 m    2.5e-6 s  9.9e-8 m
    //   CWF (the fold, default)               2.094e-3 s  9.677e-7 m    2.082e-3 s  9.676e-7 m    4.2e-3 s  2.0e-6 m
    //   CWF60 (the fold, 60 s steps)          3.328e-5 s  3.864e-8 m    3.222e-5 s  3.848e-8 m    6.7e-5 s  7.8e-8 m
    // Every bound is twice the worst of the two, rounded up to two digits (CWE's TCA bound from 3.282e-6 s on macOS
    // arm64, as on lot 6: this test gives CWE the same figures on each platform before and after #624). CWD's and
    // CWE60's move from 1.5e-6 s and 2.6e-6 s to 1.3e-6 s and 2.5e-6 s: the verifier's probe gives 6.190e-7 s and
    // 1.170e-6 s on lot 6 before #624 (a9f2a371) as on #624, and the earlier probes, 7.190e-7 s and 1.270e-6 s, lay
    // exactly one tick above it, as a reference on the grid of ticks would. The miss bounds are unchanged (the figures
    // within 1 % of the earlier ones).
    // The TCA error is the difference of the range rate of the states and of their SPK (the degree-7 interpolation of
    // their 60 s outputs) over its slope, 2.6e-4 m/s per second at a drift of 100 m and 2.6e-6 at 1 m; at the fold, 2.9e-7 m
    // deep, the states and their SPK of the default steps place the zero 2.1 ms apart (the library within 7.4e-6 s of the
    // zero of the states, from a scan of their range rate). A CSPICE minimum or a listed one within the TCA bound of a
    // window end may be on either side of it for the states and their SPK.
    // The passes of #628, whose minima no seed reached before (mixed steps, fixed 600 s steps), measured by this test on
    // Linux x64 (2026-11-10) and by the probe of #628 (843 to 1084 windows per propagation, gfdist_c refined by bisection
    // of spkezr_c's range rate to 1e-10 s); the CI of Linux x64, Windows x64 and macOS arm64 (run 43000949510, 2026-11-11,
    // by default and with the reads over the window) gives this test's figures on the three platforms, but CW600's miss
    // on Windows x64, 1.278e-5 m:
    //                                         this test                 the probe                 bound
    //   CWM (y0 = 1500 m, default and 60 s)   3.543e-5 s  8.430e-7 m    3.546e-5 s  8.433e-7 m    7.1e-5 s  1.7e-6 m
    //   CWM90 (y0 = 1490 m, default and 60 s) 6.057e-6 s  6.567e-7 m    6.066e-6 s  6.564e-7 m    1.3e-5 s  1.4e-6 m
    //   CWN (y0 = 1495 m, 60 s and default)   6.519e-5 s  1.960e-6 m    6.507e-5 s  1.960e-6 m    1.4e-4 s  4.0e-6 m
    //   CW600 (y0 = 1490 m, fixed 600 s)      3.107e-5 s  1.277e-5 m    3.105e-5 s  1.277e-5 m    6.3e-5 s  2.6e-5 m
    //   CWE600 (y0 = 1500 m, fixed 600 s)     1.016e-4 s  2.090e-5 m    1.016e-4 s  2.090e-5 m    2.1e-4 s  4.2e-5 m
    //   CWFM (the fold, default and 60 s, read every 1 s, round 1 of the review of #628)
    //                                         3.223e-3 s  2.279e-6 m    3.219e-3 s  2.280e-6 m    6.5e-3 s  4.6e-6 m
    // twice the worst of the two, rounded up to two digits: the states of mixed or 600 s steps against the degree-7
    // interpolation of their 60 s outputs in the SPK
    private static (double Tca, double Miss) SlowPassBounds(CrossingEncounterCase crossing) => crossing switch
    {
        SlowPassEncounterCase => (3.0e-7, 6.3e-10),
        DefaultStepSlowPassEncounterCase => (5.9e-5, 2.3e-6),
        MixedStepSlowPassEncounterCase => (1.1e-1, 4.2e-7),
        CwPassDefaultStepCase => (1.3e-6, 9.7e-8),
        CwPass60StepCase => (1.5e-6, 9.6e-8),
        CwPass1500DefaultStepCase => (6.6e-6, 1.9e-7),
        CwPass1500Step60Case => (2.5e-6, 9.9e-8),
        CwPassFoldDefaultStepCase => (4.2e-3, 2.0e-6),
        CwPassFoldStep60Case => (6.7e-5, 7.8e-8),
        CwPassMixedStep1500Case => (7.1e-5, 1.7e-6),
        CwPassMixedStep1490Case => (1.3e-5, 1.4e-6),
        CwPassMixedStep1495Case => (1.4e-4, 4.0e-6),
        CwPassStep600Case => (6.3e-5, 2.6e-5),
        CwPass1500Step600Case => (2.1e-4, 4.2e-5),
        CwPassFoldMixedStepCase => (6.5e-3, 4.6e-6),
        _ => throw new ArgumentOutOfRangeException(nameof(crossing))
    };

    private CrossingEncounterCase Pass(string fixture) => fixture switch
    {
        "SLOW" => _slowCase,
        "SLOWD" => _defaultStepSlowCase,
        "SLOWX" => _mixedStepSlowCase,
        "CWD" => _cwDefaultCase,
        "CW60" => _cw60Case,
        "CWE" => _cw1500DefaultCase,
        "CWE60" => _cw1500Step60Case,
        "CWF" => _cwFoldDefaultCase,
        "CWM" => _cwMixed1500Case,
        "CWM90" => _cwMixed1490Case,
        "CWN" => _cwMixed1495Case,
        "CW600" => _cw600Case,
        "CWE600" => _cw1500Step600Case,
        "CWFM" => _cwFoldMixedCase,
        _ => _cwFoldStep60Case
    };

    private CrossingEncounterCase SlowPass(string steps) => steps == "60 s" ? _slowCase : _defaultStepSlowCase;

    [Theory]
    [InlineData("60 s", 3000.0, 5500.0, 0)]
    [InlineData("60 s", 4000.0, 4500.0, 0)]
    [InlineData("60 s", 0.0, 10800.0, 3)]
    [InlineData("60 s", 2800.0, 5700.0, 2)]
    [InlineData("default", 3000.0, 5500.0, 0)]
    [InlineData("default", 4000.0, 4500.0, 0)]
    [InlineData("default", 0.0, 10800.0, 3)]
    [InlineData("default", 2800.0, 5700.0, 2)]
    public void Dense_SlowPass_ListsTheLocalMinimaOfCspice_NoMaximum(string steps, double start, double end, int count)
    {
        // Before, at the default steps, the Hermite of the steps had minima near the maxima of the distance (about 112 m,
        // t0 + 1398.7, 4246.7, 7096.0 and 9945.6 s), which the refinement could not bracket on the range rate of the
        // states and kept unrefined: AnalyzeAll listed them, a range rate of +0.0012 m/s at t0 + 4246.66 s, and the dense
        // Analyze returned that maximum on [3000, 5500] s instead of the closer end (53.06 m against 111.79 m). The count is
        // that of the minima away from the window ends: on [0, 10800] s a fourth one lies at the start, its range rate
        // -2e-13 m²/s there, which CSPICE lists at 6e-8 s and the states one tick after the start
        var crossing = SlowPass(steps);
        AssertSlowPassMatchesCspice(crossing, crossing.Seconds(start, end), count);
    }

    [Theory]
    [InlineData(-400.0, 1400.0)]
    [InlineData(-120.0, 1400.0)]
    [InlineData(-232.0, 1268.0)]
    public void Dense_MinimumPastTheWideningsOfItsSeeds_IsListed(double startOffset, double endOffset)
    {
        // The windows about the third minimum of the mixed-step slow pass (t0 + 5761.03 s) where the seeds of the polynomial search lie
        // three candidates or more from the zero of the range rate of the states: the minimum is no longer kept unrefined
        // at a seed, it is reached by following the range rate over the candidates (before, AnalyzeAll listed the seeds,
        // range rates of up to 1.2e-5 m/s; dropping them without following the range rate listed nothing)
        var crossing = _mixedStepSlowCase;
        var minimum = CspiceLocalMinima(crossing.Seconds(4000.0, 7000.0), crossing).Single().Epoch;
        var window = new Window(minimum.AddSeconds(startOffset), minimum.AddSeconds(endOffset));
        AssertSlowPassMatchesCspice(crossing, window, 1);
    }

    [Theory]
    [InlineData("default", 0.0, 10800.0, 4)]
    [InlineData("default", 1000.0, 2000.0, 2)]
    [InlineData("default", 1200.0, 1800.0, 2)]
    [InlineData("default", 1300.0, 1600.0, 2)]
    [InlineData("default", 1400.0, 1700.0, 1)]
    [InlineData("default", 1450.0, 1600.0, 1)]
    [InlineData("60 s", 0.0, 10800.0, 4)]
    [InlineData("60 s", 1000.0, 2000.0, 2)]
    [InlineData("60 s", 1200.0, 1800.0, 2)]
    [InlineData("60 s", 1300.0, 1600.0, 2)]
    [InlineData("60 s", 1400.0, 1700.0, 1)]
    [InlineData("60 s", 1450.0, 1600.0, 1)]
    public void Dense_TwoMinimaPerOrbit_ListsTheLocalMinimaOfCspice(string steps, double start, double end, int count)
    {
        // At the default steps the Hermite seeds the minimum at t0 + 1526.28 s 1.6 s past it, where the range rate of the
        // states is already positive (+0.086 m²/s), as at the candidates on either side, the maximum between the two minima
        // lying before the one on the left: the zero lies between that candidate and the seed. Before round 5 of the lot-6
        // review, the refinement followed the candidates to the left, reached the pair that holds the minimum at
        // t0 + 1317.43 s and dropped the seed, so that AnalyzeAll listed the minimum at 1317.43 s alone and Analyze returned
        // it (509.744 m against 509.695 m); before round 4 it kept the seed unrefined, 1.6 s off, and on [1400, 1700] s it
        // listed nothing, Analyze returning the start. It is now searched from the seed on its own range rate
        CrossingEncounterCase crossing = steps == "60 s" ? _cw60Case : _cwDefaultCase;
        AssertSlowPassMatchesCspice(crossing, crossing.Seconds(start, end), count);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("60 s")]
    public void Dense_TwoMinimaPerOrbit_MinimumSeededPastItsZero_IsListed(string steps)
    {
        // y0 = 1500 m: the window ends 60 s after the minimum at t0 + 7029.75 s and starts 1400 s before it, where the
        // fourth reviewer found it dropped in round 4 of the lot-6 review, as in 134 other windows
        CrossingEncounterCase crossing = steps == "60 s" ? _cw1500Step60Case : _cw1500DefaultCase;
        var minimum = CspiceLocalMinima(crossing.Seconds(6500.0, 7100.0), crossing).Single().Epoch;
        AssertSlowPassMatchesCspice(crossing, new Window(minimum.AddSeconds(-1400.0), minimum.AddSeconds(60.0)), 1);
    }

    [Theory]
    [InlineData("default", 0.0, 10800.0, 3)]
    [InlineData("default", 7000.0, 7100.0, 1)]
    [InlineData("default", 7060.0, 7067.0, 1)]
    [InlineData("default", 4171.0, 7171.0, 2)]
    [InlineData("default", 5141.0, 8141.0, 2)]
    [InlineData("default", 6499.0, 7199.0, 2)]
    [InlineData("default", 6887.0, 7187.0, 2)]
    [InlineData("default", 6984.0, 9984.0, 2)]
    [InlineData("60 s", 0.0, 10800.0, 3)]
    [InlineData("60 s", 7000.0, 7100.0, 1)]
    [InlineData("60 s", 7060.0, 7067.0, 1)]
    [InlineData("60 s", 4171.0, 7171.0, 2)]
    [InlineData("60 s", 5141.0, 8141.0, 2)]
    [InlineData("60 s", 6499.0, 7199.0, 2)]
    [InlineData("60 s", 6887.0, 7187.0, 2)]
    [InlineData("60 s", 6984.0, 9984.0, 2)]
    public void Dense_ShallowMinimumNextToAFold_IsListed(string steps, double start, double end, int count)
    {
        // The shallow minimum at t0 + 7065.55 s, its range rate positive over 1.6 s only: the Hermite seeds it 4.85 s
        // before at 60 s steps, closing, and the reads of the search from the seed, at distances doubling, read the range rate negative
        // 4.096 and 8.192 s after the seed, on either side of the 1.6 s. In round 5 of the lot-6 review it was lost on these
        // windows (204 to 229 windows of the fifth reviewer per propagation), except on the short [7060, 7067] s; before,
        // the seed was listed unrefined, 4.85 s off at 60 s steps. The reads are now 1 s apart at most
        CrossingEncounterCase crossing = steps == "60 s" ? _cwFoldStep60Case : _cwFoldDefaultCase;
        AssertSlowPassMatchesCspice(crossing, crossing.Seconds(start, end), count);
    }

    [Theory]
    [InlineData("default", -800.0, 10.0, 1)]
    [InlineData("default", -400.0, 30.0, 1)]
    [InlineData("default", -30.0, 60.0, 1)]
    [InlineData("default", -10.0, 200.0, 2)]
    [InlineData("60 s", -800.0, 10.0, 1)]
    [InlineData("60 s", -400.0, 30.0, 1)]
    [InlineData("60 s", -30.0, 60.0, 1)]
    [InlineData("60 s", -10.0, 200.0, 2)]
    public void Dense_ShallowMinimumNextToAFold_WindowsAboutIt_IsListed(string steps, double startOffset, double endOffset,
        int count)
    {
        // Windows of the fifth reviewer about the shallow minimum (CSPICE's on the SPKs of each propagation), the next
        // minimum 91 s after it
        CrossingEncounterCase crossing = steps == "60 s" ? _cwFoldStep60Case : _cwFoldDefaultCase;
        var minimum = CspiceLocalMinima(crossing.Seconds(7000.0, 7100.0), crossing).Single().Epoch;
        AssertSlowPassMatchesCspice(crossing, new Window(minimum.AddSeconds(startOffset), minimum.AddSeconds(endOffset)), count);
    }

    [Theory]
    [InlineData("CWM", 6969.70, 7089.70, 1)]
    [InlineData("CWM", 0.0, 10800.0, 3)]
    [InlineData("CWM90", 6970.31, 7171.31, 1)]
    [InlineData("CWM90", 0.0, 10800.0, 4)]
    [InlineData("CWN", 97.86, 1507.86, 2)]
    [InlineData("CWN", 0.0, 10800.0, 4)]
    [InlineData("CW600", 0.0, 10800.0, 4)]
    [InlineData("CW600", 1200.0, 1800.0, 2)]
    [InlineData("CWE600", 0.0, 10800.0, 3)]
    [InlineData("CWE600", 4268.0, 7268.0, 2)]
    public void Dense_MinimumWithoutASeedNearIt_IsListed(string fixture, double start, double end, int count)
    {
        // #628, with ReadRangeRateOverWindow (opt-in, decision 54 of #457; without it, Dense_ByDefault_* below): the
        // windows the review of lot 6 traced, where the Hermite of mixed or long steps places no minimum near a
        // minimum of the states (form A: CWM's 504.6428 m at t0 + 7029.70 s, the range rate -3.18, -0.22, +0.062 and
        // -0.044 m²/s at the candidates of the window, the only seed its end; CWM90's minimum 1 s after the start; CWN's at
        // t0 + 1497.86 s, closing at t0 + 1440 s and receding at 1500 s), or one 600 s step holds two minima (form B:
        // CW600's at t0 + 1317.45 and 1526.28 s, a maximum between them, the Hermite one minimum at 1502.10 s; CWE600's at
        // t0 + 7029.94 s). Before, AnalyzeAll did not list them on the traced windows and on the whole windows of CW600
        // and CWE600, and Analyze returned a window end (504.6578 m on CWM's window) or another minimum (CWN, CW600); the
        // windows of 3000 s every 97 s of the sweeps lost them in 163 to 164 windows at 600 s steps. The whole windows of
        // CWM, CWM90 and CWN are regression rows: main b237eec2 already lists every minimum there, the seeds of the whole
        // window reaching them (round 1 of the review of #628)
        AssertSlowPassMatchesCspice(Pass(fixture), Pass(fixture).Seconds(start, end), count, options: ReadOverWindowOptions);
    }

    [Theory]
    [InlineData("CWM", 6969.70, 7089.70)]
    [InlineData("CWM", 0.0, 10800.0)]
    [InlineData("CWM90", 6970.31, 7171.31)]
    [InlineData("CWM90", 0.0, 10800.0)]
    [InlineData("CWN", 97.86, 1507.86)]
    [InlineData("CWN", 0.0, 10800.0)]
    [InlineData("CW600", 0.0, 10800.0)]
    [InlineData("CW600", 1200.0, 1800.0)]
    [InlineData("CWE600", 0.0, 10800.0)]
    [InlineData("CWE600", 4268.0, 7268.0)]
    [InlineData("CWFM", 7000.0, 7100.0)]
    [InlineData("CWFM", 0.0, 10800.0)]
    public void Dense_ByDefault_MinimumWithoutASeedNearIt_ListsOnlyMinima(string fixture, double start, double end)
    {
        // The windows of #628 with the default options (decision 54 of #457: the reads of the range rate over the window
        // opt-in): a minimum that no minimum of the Hermite seeds may be lost (on CWM's window, AnalyzeAll lists nothing and
        // Analyze returns the window end, 504.658 m, for the minimum of 504.643 m; on CW600's [1200, 1800] s, AnalyzeAll
        // lists only the minimum at t0 + 1526.28 s, 509.692 m, losing the farther one at 1317.45 s, 509.741 m, and Analyze
        // returns 1526.28 s either way), but every TCA listed is a minimum of CSPICE within the bounds, inside the window, in
        // order, none twice, and Analyze returns the closest one listed, or without one the end where the distance grows
        // into the window
        Assert.False(new ConjunctionAnalysisOptions().ReadRangeRateOverWindow);
        AssertSlowPassListsOnlyMinima(Pass(fixture), Pass(fixture).Seconds(start, end), Options, log: true);
    }

    [Theory]
    [InlineData("CW600", 1200.0, 1800.0)]
    [InlineData("CW600", 0.0, 10800.0)]
    [InlineData("CWM", 6969.70, 7089.70)]
    [InlineData("CWM", 0.0, 10800.0)]
    public void Dense_ByDefault_TheRangeRateIsNotReadOverTheWindow(string fixture, double start, double end)
    {
        // Decision 54 of #457, through the public Analyze and AnalyzeAll: by default the dense search reads the range rate
        // the seeds need only, so MaximumEventSearchStep changes neither its reads nor its results, and at a spacing of 1 s
        // it reads fewer states than the reads over the window alone need (one per second of the window); with
        // ReadRangeRateOverWindow it reads at least those (the review of decision 54: a search that always read over the
        // window passed every other test)
        var crossing = Pass(fixture);
        var window = crossing.Seconds(start, end);
        var profile = new ProtectedSpacecraftProfile(crossing.Sat1);
        double length = (window.EndDate - window.StartDate).TotalSeconds;

        (long Reads, string Result) All(ConjunctionAnalysisOptions options)
        {
            ConjunctionAssessment.DenseRangeRateReads = 0;
            var all = ConjunctionAssessment.AnalyzeAll(profile, crossing.Solution1, crossing.Sat2, crossing.Solution2, window,
                options);
            return (ConjunctionAssessment.DenseRangeRateReads, string.Join("; ",
                all.Select(e => $"{e.EncounterState.Epoch.TimeSpanFromJ2000().Ticks} {e.EncounterState.MissDistanceMeters:R}")));
        }

        (long Reads, string Result) One(ConjunctionAnalysisOptions options)
        {
            ConjunctionAssessment.DenseRangeRateReads = 0;
            var encounter = ConjunctionAssessment.Analyze(profile, crossing.Solution1, crossing.Sat2, crossing.Solution2,
                window, options);
            return (ConjunctionAssessment.DenseRangeRateReads,
                $"{encounter.EncounterState.Epoch.TimeSpanFromJ2000().Ticks} {encounter.EncounterState.MissDistanceMeters:R}");
        }

        var fine = Options with { MaximumEventSearchStep = TimeSpan.FromSeconds(1.0) };
        foreach (var search in new Func<ConjunctionAnalysisOptions, (long Reads, string Result)>[] { All, One })
        {
            var byDefault = search(Options);
            var byDefaultFine = search(fine);
            var withTheReads = search(fine with { ReadRangeRateOverWindow = true });
            _output.WriteLine($"reads by default {byDefault.Reads} (at 1 s {byDefaultFine.Reads}), with the option at 1 s " +
                              $"{withTheReads.Reads}, window {length:F2} s");
            Assert.Equal(byDefault, byDefaultFine);
            Assert.True(byDefaultFine.Reads < length, $"{byDefaultFine.Reads} reads by default at 1 s");
            Assert.True(withTheReads.Reads >= length, $"{withTheReads.Reads} reads with the option at 1 s");
        }
    }

    [Theory]
    [InlineData(7000.0, 7100.0, 1)]
    [InlineData(6900.0, 7300.0, 2)]
    [InlineData(0.0, 10800.0, 3)]
    public void Dense_TheReadsOfTheRangeRate_AreMaximumEventSearchStepApart(double start, double end, int count)
    {
        // The fold of CWFM, positive over [7065.14, 7067.49] s with no minimum of the Hermite before it: at a
        // MaximumEventSearchStep of 1 s the reads over the window land in it, and AnalyzeAll lists every minimum of CSPICE
        // (the round-1 review of #628: the option reaches the dense search; the 60 s reads step over this fold, the
        // documented limit, which this test does not assert)
        var options = ReadOverWindowOptions with { MaximumEventSearchStep = TimeSpan.FromSeconds(1.0) };
        AssertSlowPassMatchesCspice(_cwFoldMixedCase, _cwFoldMixedCase.Seconds(start, end), count, options: options);
    }

    [Theory]
    [InlineData("SLOW")]
    [InlineData("SLOWD")]
    [InlineData("SLOWX")]
    [InlineData("CWD")]
    [InlineData("CW60")]
    [InlineData("CWE")]
    [InlineData("CWE60")]
    [InlineData("CWF")]
    [InlineData("CWF60")]
    [InlineData("SLOW", true)]
    [InlineData("SLOWD", true)]
    [InlineData("SLOWX", true)]
    [InlineData("CWD", true)]
    [InlineData("CW60", true)]
    [InlineData("CWE", true)]
    [InlineData("CWE60", true)]
    [InlineData("CWF", true)]
    [InlineData("CWF60", true)]
    [InlineData("CWM", true)]
    [InlineData("CWM90", true)]
    [InlineData("CWN", true)]
    [InlineData("CW600", true)]
    [InlineData("CWE600", true)]
    public void Dense_EveryMinimumOfThePass_IsListedWithinTheBounds(string fixture, bool readRangeRateOverWindow = false)
    {
        // The windows of the reviewers' sweeps over the whole propagation: the start and the end of each window at 1 to
        // 1400 s on either side of each local minimum of CSPICE on the whole window (within the propagation), windows of
        // 300, 700, 1500 and 3000 s every 97 s, and the windows of the fifth reviewer (round 5 of the lot-6 review): about
        // each minimum of CSPICE, 0.1 ms to 5 s on both sides and 0.1 ms to 600 s on either side, and the windows that end
        // or start at the zero, at -10 to 10 ticks and at 1 µs to 10 ms on either side of each minimum of CSPICE and of each
        // minimum the dense AnalyzeAll lists on the whole window, from 300 s and from 20 ms on the other side. Every minimum
        // of the propagation is measured, from many windows, against CSPICE's zero below the tick: the bounds of
        // SlowPassBounds are twice the worst of this test
        // The passes of #628 (CWM to CWE600) only with ReadRangeRateOverWindow (decision 54 of #457): by default a minimum
        // that no minimum of the Hermite seeds may be lost there (Dense_ByDefault_EveryListedMinimumOfThePass_IsAMinimum)
        var crossing = Pass(fixture);
        var options = readRangeRateOverWindow ? ReadOverWindowOptions : Options;
        var (minima, windows) = SweepWindows(crossing, options);

        _worstSlowTcaError = 0.0;
        _worstSlowMissError = 0.0;
        foreach (var (start, end) in windows)
        {
            AssertSlowPassMatchesCspice(crossing, crossing.Seconds(start, end), null, log: false, options: options);
        }

        _output.WriteLine($"MEASURE {fixture}{(readRangeRateOverWindow ? " (reads over the window)" : "")} every minimum " +
                          $"({minima.Length} on the whole window, {windows.Count} windows): " +
                          $"TCA {_worstSlowTcaError:E3} s, miss {_worstSlowMissError:E3} m");
    }

    [Theory]
    [InlineData("CWM")]
    [InlineData("CWM90")]
    [InlineData("CWN")]
    [InlineData("CW600")]
    [InlineData("CWE600")]
    [InlineData("CWFM")]
    public void Dense_ByDefault_EveryListedMinimumOfThePass_IsAMinimumWithinTheBounds(string fixture)
    {
        // The windows of the sweep above, on the passes of #628, with the default options (decision 54 of #457): a minimum
        // that no minimum of the Hermite seeds may be missing, but every TCA listed is a minimum of CSPICE within the bounds
        // of the pass, inside the window, in order, none twice, and Analyze returns the closest one listed, or without one
        // the end where the distance grows into the window (the contract that holds by default)
        var crossing = Pass(fixture);
        var (minima, windows) = SweepWindows(crossing, Options);
        _worstSlowTcaError = 0.0;
        _worstSlowMissError = 0.0;
        foreach (var (start, end) in windows)
        {
            AssertSlowPassListsOnlyMinima(crossing, crossing.Seconds(start, end), Options, log: false);
        }

        _output.WriteLine($"MEASURE {fixture} by default, every listed minimum ({minima.Length} on the whole window, " +
                          $"{windows.Count} windows): TCA {_worstSlowTcaError:E3} s, miss {_worstSlowMissError:E3} m");
    }

    // The windows of the sweeps: the start and the end of each window at 1 to 1400 s on either side of each local minimum of
    // CSPICE on the whole window (within the propagation), windows of 300, 700, 1500 and 3000 s every 97 s, and the windows
    // of the fifth reviewer (round 5 of the lot-6 review), about each minimum of CSPICE and of each minimum AnalyzeAll lists
    // on the whole window with the given options
    private (double[] Minima, System.Collections.Generic.SortedSet<(double Start, double End)> Windows) SweepWindows(
        CrossingEncounterCase crossing, ConjunctionAnalysisOptions options)
    {
        double duration = (crossing.Window.EndDate - crossing.Window.StartDate).TotalSeconds;
        var minima = CspiceLocalMinima(crossing.Window, crossing)
            .Select(m => (m.Epoch - crossing.T0).TotalSeconds)
            .ToArray();
        var listed = ConjunctionAssessment.AnalyzeAll(new ProtectedSpacecraftProfile(crossing.Sat1), crossing.Solution1,
                crossing.Sat2, crossing.Solution2, crossing.Window, options)
            .Select(e => (e.EncounterState.Epoch - crossing.T0).TotalSeconds)
            .ToArray();
        double[] offsets = { 1.0, 10.0, 30.0, 60.0, 120.0, 200.0, 400.0, 800.0, 1400.0 };
        var windows = new System.Collections.Generic.SortedSet<(double Start, double End)>();
        foreach (double minimum in minima)
        {
            foreach (double before in offsets)
            {
                foreach (double after in offsets)
                {
                    double start = System.Math.Round(System.Math.Max(0.0, minimum - before), 7);
                    double end = System.Math.Round(System.Math.Min(duration, minimum + after), 7);
                    if (end > start)
                    {
                        windows.Add((start, end));
                    }
                }
            }
        }

        foreach (double length in new[] { 300.0, 700.0, 1500.0, 3000.0 })
        {
            for (double start = 0.0; start + length <= duration; start += 97.0)
            {
                windows.Add((start, start + length));
            }
        }

        foreach (double minimum in minima)
        {
            foreach (double half in new[] { 1.0e-4, 1.0e-3, 1.0e-2, 0.1, 1.0, 5.0 })
            {
                AddWindow(minimum - half, minimum + half);
            }

            double[] sides = { 1.0e-4, 1.0e-2, 1.0, 60.0, 600.0 };
            foreach (double before in sides)
            {
                foreach (double after in sides)
                {
                    AddWindow(minimum - before, minimum + after);
                }
            }
        }

        var splits = Enumerable.Range(-10, 21).Select(k => k * 1.0e-7)
            .Concat(new[] { 1.0e-6, 1.0e-5, 1.0e-4, 1.0e-3, 1.0e-2 }.SelectMany(d => new[] { -d, d }))
            .ToArray();
        foreach (double centre in minima.Concat(listed))
        {
            foreach (double split in splits)
            {
                double at = centre + split;
                foreach (double side in new[] { 300.0, 0.02 })
                {
                    AddWindow(at - side, at);
                    AddWindow(at, at + side);
                }
            }
        }

        void AddWindow(double start, double end)
        {
            double s0 = System.Math.Round(System.Math.Max(0.0, start), 7);
            double e0 = System.Math.Round(System.Math.Min(duration, end), 7);
            if (e0 > s0)
            {
                windows.Add((s0, e0));
            }
        }

        return (minima, windows);
    }

    // The contract of the dense search by default (decision 54 of #457): AnalyzeAll lists only local minima of CSPICE's
    // gfdist_c, each within the bounds of the slow passes (or, within the TCA bound of a window end, one CSPICE places just
    // outside the window, or none, the states and their SPK placing the zero on either side of that end), strictly inside
    // the window, in order, none twice; Analyze returns the closest one listed, or without one the window end where the
    // distance grows into the window (AssertTheEndWhereTheDistanceGrows)
    private void AssertSlowPassListsOnlyMinima(CrossingEncounterCase crossing, Window window,
        ConjunctionAnalysisOptions options, bool log)
    {
        var profile = new ProtectedSpacecraftProfile(crossing.Sat1);
        var (tcaBound, missBound) = SlowPassBounds(crossing);
        var locmin = CspiceLocalMinima(window, crossing);
        var all = ConjunctionAssessment.AnalyzeAll(profile, crossing.Solution1, crossing.Sat2, crossing.Solution2, window, options);
        var encounter = ConjunctionAssessment.Analyze(profile, crossing.Solution1, crossing.Sat2, crossing.Solution2, window, options);
        string listed = $"[{(window.StartDate - crossing.T0).TotalSeconds:F7}, {(window.EndDate - crossing.T0).TotalSeconds:F7}] s: " +
                        $"LOCMIN [{string.Join("; ", locmin.Select(m => $"{(m.Epoch - crossing.T0).TotalSeconds:F7} s {m.Distance:R} m"))}], " +
                        $"AnalyzeAll [{string.Join("; ", all.Select(e => $"{(e.EncounterState.Epoch - crossing.T0).TotalSeconds:F7} s {e.EncounterState.MissDistanceMeters:R} m"))}], " +
                        $"Analyze {(encounter.EncounterState.Epoch - crossing.T0).TotalSeconds:F7} s {encounter.EncounterState.MissDistanceMeters:R} m";
        if (log)
        {
            _output.WriteLine(listed);
        }

        try
        {
            bool NearAnEnd(TimeSystem.Time epoch) => System.Math.Abs((epoch - window.StartDate).TotalSeconds) <= tcaBound ||
                                                     System.Math.Abs((window.EndDate - epoch).TotalSeconds) <= tcaBound;

            var epochs = all.Select(e => e.EncounterState.Epoch).ToArray();
            Assert.All(epochs, epoch => Assert.True(epoch > window.StartDate && epoch < window.EndDate));
            Assert.True(epochs.Zip(epochs.Skip(1), (a, b) => a < b).All(ordered => ordered), "ordered, none twice");
            var matched = new System.Collections.Generic.List<int>();
            foreach (var e in all)
            {
                int index = locmin.Select((m, i) => (m, i))
                    .Where(x => System.Math.Abs((e.EncounterState.Epoch - x.m.Epoch).TotalSeconds) <= tcaBound)
                    .Select(x => x.i).DefaultIfEmpty(-1).First();
                if (index >= 0)
                {
                    AssertSlowPassMinimum(e, locmin[index], tcaBound, missBound, crossing, log);
                    matched.Add(index);
                    continue;
                }

                Assert.True(NearAnEnd(e.EncounterState.Epoch), "a listed TCA that is no minimum of CSPICE");
                if (CspiceMinimumNear(e.EncounterState.Epoch, window, tcaBound, crossing) is { } outside)
                {
                    AssertSlowPassMinimum(e, outside, tcaBound, missBound, crossing, log);
                }
            }

            Assert.Equal(matched.Count, matched.Distinct().Count());
            if (all.Count > 0)
            {
                Assert.Equal(all.MinBy(e => e.EncounterState.MissDistanceMeters).EncounterState.Epoch,
                    encounter.EncounterState.Epoch);
            }
            else if (locmin.Length > 0 && locmin.All(m => NearAnEnd(m.Epoch)))
            {
                // Only minima within the band of an end, none listed: an end, as AssertSlowPassLists
                Assert.True(encounter.EncounterState.Epoch == window.StartDate ||
                            encounter.EncounterState.Epoch == window.EndDate);
            }
            else
            {
                // None listed, a minimum inside lost by default or none: the end where the distance grows into the window
                AssertTheEndWhereTheDistanceGrows(crossing, window, encounter, tcaBound, missBound);
            }
        }
        catch (Xunit.Sdk.XunitException e) when (!log)
        {
            throw new Xunit.Sdk.XunitException($"{listed}{Environment.NewLine}{e.Message}");
        }
    }

    // AnalyzeAll lists the local minima of CSPICE's gfdist_c in the window, each within the bounds of the slow passes, and
    // nothing else; a CSPICE minimum within the TCA bound of a window end may be listed or not, and a listed one there may
    // have no CSPICE minimum, the states and their SPK placing the zero of the range rate on either side of that end.
    // Analyze returns the closest listed minimum, or without one the window end where the distance grows into the window by
    // CSPICE's range rate (the closer one when both do)
    private void AssertSlowPassMatchesCspice(CrossingEncounterCase crossing, Window window, int? expectedCount,
        bool log = true, ConjunctionAnalysisOptions options = null)
    {
        options ??= Options;
        var profile = new ProtectedSpacecraftProfile(crossing.Sat1);
        var (tcaBound, missBound) = SlowPassBounds(crossing);
        var locmin = CspiceLocalMinima(window, crossing);

        var all = ConjunctionAssessment.AnalyzeAll(profile, crossing.Solution1, crossing.Sat2, crossing.Solution2, window, options);
        var encounter = ConjunctionAssessment.Analyze(profile, crossing.Solution1, crossing.Sat2, crossing.Solution2, window, options);
        string listed = $"[{(window.StartDate - crossing.T0).TotalSeconds:F7}, {(window.EndDate - crossing.T0).TotalSeconds:F7}] s: " +
                          $"LOCMIN [{string.Join("; ", locmin.Select(m => $"{(m.Epoch - crossing.T0).TotalSeconds:F7} s {m.Distance:R} m"))}], " +
                          $"AnalyzeAll [{string.Join("; ", all.Select(e => $"{(e.EncounterState.Epoch - crossing.T0).TotalSeconds:F7} s {e.EncounterState.MissDistanceMeters:R} m"))}], " +
                          $"Analyze {(encounter.EncounterState.Epoch - crossing.T0).TotalSeconds:F7} s";
        if (log)
        {
            _output.WriteLine(listed);
        }

        try
        {
            AssertSlowPassLists(crossing, window, expectedCount, locmin, all, encounter, tcaBound, missBound, log);
        }
        catch (Xunit.Sdk.XunitException e) when (!log)
        {
            throw new Xunit.Sdk.XunitException($"{listed}{Environment.NewLine}{e.Message}");
        }
    }

    private void AssertSlowPassLists(CrossingEncounterCase crossing, Window window, int? expectedCount,
        (TimeSystem.Time Epoch, double Offset, double Distance)[] locmin, System.Collections.Generic.IReadOnlyList<EncounterCase> all,
        EncounterCase encounter, double tcaBound, double missBound, bool log)
    {
        bool NearAnEnd(TimeSystem.Time epoch) => System.Math.Abs((epoch - window.StartDate).TotalSeconds) <= tcaBound ||
                                                 System.Math.Abs((window.EndDate - epoch).TotalSeconds) <= tcaBound;

        if (expectedCount.HasValue)
        {
            Assert.Equal(expectedCount.Value, locmin.Count(m => !NearAnEnd(m.Epoch)));
        }

        Assert.All(all, e => Assert.True(e.EncounterState.Epoch > window.StartDate && e.EncounterState.Epoch < window.EndDate));
        var matched = all.Select(e => locmin.Select((m, i) => (m, i))
                .Where(x => System.Math.Abs((e.EncounterState.Epoch - x.m.Epoch).TotalSeconds) <= tcaBound)
                .Select(x => x.i).DefaultIfEmpty(-1).First())
            .ToArray();
        Assert.Equal(matched.Where(i => i >= 0).Count(), matched.Where(i => i >= 0).Distinct().Count());
        Assert.All(locmin.Where((m, i) => !matched.Contains(i)), m => Assert.True(NearAnEnd(m.Epoch)));
        var outsideReferences = new System.Collections.Generic.List<(TimeSystem.Time Epoch, double Offset, double Distance)>();
        for (int i = 0; i < all.Count; i++)
        {
            if (matched[i] < 0)
            {
                Assert.True(NearAnEnd(all[i].EncounterState.Epoch));
                // A listed minimum within the TCA bound of an end whose CSPICE minimum lies just outside the window (a window
                // starting or ending at the zero, the states placing it inside): measured against that minimum, from
                // CSPICE's minima of the window widened by the bound (the round-6 verifier's windows)
                var outside = CspiceMinimumNear(all[i].EncounterState.Epoch, window, tcaBound, crossing);
                if (outside.HasValue)
                {
                    AssertSlowPassMinimum(all[i], outside.Value, tcaBound, missBound, crossing, log);
                    outsideReferences.Add(outside.Value);
                }

                continue;
            }

            AssertSlowPassMinimum(all[i], locmin[matched[i]], tcaBound, missBound, crossing, log);
        }

        if (all.Count > 0)
        {
            // Analyze returns the closest listed minimum, one within the TCA bound of an end included, measured against
            // its CSPICE minimum just outside the window (#628: a window ending at a minimum, the states placing it one
            // tick inside, with another minimum inside that no seed reached before)
            var references = matched.Where(i => i >= 0).Select(i => locmin[i]).Concat(outsideReferences).ToArray();
            if (references.Length > 0)
            {
                AssertSlowPassMinimum(encounter, references.MinBy(m => m.Distance), tcaBound, missBound, crossing, log);
            }
            else
            {
                Assert.Contains(encounter.EncounterState.Epoch, all.Select(e => e.EncounterState.Epoch));
            }

            return;
        }

        if (locmin.Length > 0)
        {
            // Only minima within the band of an end, none listed: an end
            Assert.True(encounter.EncounterState.Epoch == window.StartDate || encounter.EncounterState.Epoch == window.EndDate);
            return;
        }

        AssertTheEndWhereTheDistanceGrows(crossing, window, encounter, tcaBound, missBound);
    }

    // Analyze without a listed minimum: the window end where the distance grows into the window by CSPICE's range rate, the
    // closer one when both do (either one when their distances lie within the miss bound of each other), or an end within
    // the TCA bound of a zero of CSPICE's range rate
    private static void AssertTheEndWhereTheDistanceGrows(CrossingEncounterCase crossing, Window window,
        EncounterCase encounter, double tcaBound, double missBound)
    {
        // A zero of CSPICE's range rate, from negative to positive, within the TCA bound of a window end (inside the window
        // or outside it, too near an end for gfdist_c): the states may place it anywhere within that bound, so that the
        // whole window recedes (the start the closest point) or closes (the end) on the states. A window narrower than the
        // bound about a minimum, or starting or ending at one, as in the fifth reviewer's windows (round 5 of the lot-6 review)
        bool ZeroNear(TimeSystem.Time end)
        {
            var before = end.AddSeconds(-tcaBound);
            var after = end.AddSeconds(tcaBound);
            before = before < crossing.Window.StartDate ? crossing.Window.StartDate : before;
            after = after > crossing.Window.EndDate ? crossing.Window.EndDate : after;
            return CspiceRangeRate(before, crossing) < 0.0 && CspiceRangeRate(after, crossing) >= 0.0;
        }

        if ((encounter.EncounterState.Epoch == window.StartDate && ZeroNear(window.StartDate)) ||
            (encounter.EncounterState.Epoch == window.EndDate && ZeroNear(window.EndDate)))
        {
            return;
        }

        var ends = new[] { window.StartDate, window.EndDate }
            .Where(epoch => epoch == window.StartDate
                ? CspiceRangeRate(epoch, crossing) >= 0.0
                : CspiceRangeRate(epoch, crossing) <= 0.0)
            .ToArray();
        Assert.NotEmpty(ends);

        // Both ends nearer to each other than the miss bound of the states against their SPK: either one
        var distances = ends.Select(epoch => CspiceRelative(epoch, crossing).Position.Magnitude()).ToArray();
        if (distances.Length == 2 && System.Math.Abs(distances[0] - distances[1]) <= 2.0 * missBound)
        {
            Assert.Contains(encounter.EncounterState.Epoch, ends);
            return;
        }

        Assert.Equal(ends.MinBy(epoch => CspiceRelative(epoch, crossing).Position.Magnitude()), encounter.EncounterState.Epoch);
    }

    private double _worstSlowTcaError;
    private double _worstSlowMissError;

    private void AssertSlowPassMinimum(EncounterCase encounter,
        (TimeSystem.Time Epoch, double Offset, double Distance) reference, double tcaBound, double missBound,
        CrossingEncounterCase crossing, bool log)
    {
        double tcaError = (encounter.EncounterState.Epoch - reference.Epoch).TotalSeconds - reference.Offset;
        double missError = encounter.EncounterState.MissDistanceMeters - reference.Distance;
        _worstSlowTcaError = System.Math.Max(_worstSlowTcaError, System.Math.Abs(tcaError));
        _worstSlowMissError = System.Math.Max(_worstSlowMissError, System.Math.Abs(missError));
        if (log)
        {
            _output.WriteLine($"MEASURE slow pass TCA t0 + {(encounter.EncounterState.Epoch - crossing.T0).TotalSeconds:F7} s: " +
                              $"{tcaError:E3} s, miss {missError:E3} m");
        }

        Assert.True(System.Math.Abs(tcaError) <= tcaBound,
            $"TCA t0 + {(encounter.EncounterState.Epoch - crossing.T0).TotalSeconds:F7} s: {tcaError:E3} s");
        Assert.True(System.Math.Abs(missError) <= missBound,
            $"miss at t0 + {(encounter.EncounterState.Epoch - crossing.T0).TotalSeconds:F7} s: {missError:E3} m");
    }

    // The zero of CSPICE's range rate below the tick, from a reference minimum (an epoch, on the grid of ticks): the secant of
    // the range rate g at the epoch and 1 ms after it (before it at the end of the SPKs), in double precision, gives the
    // offset of the zero from the epoch, δ = -g/g', and the distance there, sqrt(d² - g²/g'), d² being quadratic over δ
    private static (double Offset, double Distance) CspiceZeroBelowTheTick(TimeSystem.Time epoch, CrossingEncounterCase crossing)
    {
        double h = epoch.AddSeconds(1.0e-3) > crossing.Window.EndDate ? -1.0e-3 : 1.0e-3;
        var (position, velocity) = CspiceRelative(epoch, crossing);
        double g = position * velocity;
        double slope = (CspiceRangeRate(epoch.AddSeconds(h), crossing) - g) / h;
        double squared = position.MagnitudeSquared();
        if (slope <= 0.0)
        {
            return (0.0, System.Math.Sqrt(squared));
        }

        return (-g / slope, System.Math.Sqrt(squared - g * g / slope));
    }

    // ---- #527: the ILocalizable overloads do not extrapolate the propagated states ----

    [Fact]
    public void Localizable_WindowPastThePropagatedStates_Throws()
    {
        var profile = new ProtectedSpacecraftProfile(_case.Sat1);
        var beyond = _case.Seconds(10200.0, 11400.0);
        var exception = Assert.Throws<ArgumentException>(() => ConjunctionAssessment.Analyze(profile, _case.Sat2, beyond, Options));
        Assert.Equal("screeningWindow", exception.ParamName);
        Assert.Throws<ArgumentException>(() => ConjunctionAssessment.AnalyzeAll(profile, _case.Sat2, beyond, Options));
        Assert.Throws<ArgumentException>(() => ConjunctionAssessment.Screen(profile, new ILocalizable[] { _case.Sat2 }, beyond));

        // The secondary alone leaves its states: a protected spacecraft without stored states, read in two-body motion
        var twoBody = new Spacecraft(-620003, "TWOBODY", 100.0, 1000.0, new Clock("TWOBODY_CLK", 256),
            _case.Sat1.InitialOrbitalParameters);
        Assert.Throws<ArgumentException>(() =>
            ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(twoBody), _case.Sat2, beyond, Options));

        // Inside the states, the ILocalizable path finds the encounter
        var inside = ConjunctionAssessment.Analyze(profile, _case.Sat2, _case.Seconds(1200.0, 2400.0), Options);
        Assert.Equal(250.0, inside.EncounterState.MissDistanceMeters, 3);
    }

    // ---- #454: reads outside the coverage of the kernels throw ----

    [Fact]
    public void Reads_OutsideTheKernels_Throw()
    {
        var past = _case.T0.AddSeconds(14400.0);

        // spkezr_c past the SPK: a NaN state from the native layer, SPICE's message in the exception
        var single = Assert.Throws<InvalidOperationException>(() =>
            SpiceAPI.Instance.ReadEphemeris(past, PlanetsAndMoons.EARTH.NaifId, CrossingEncounterCase.Id1, Frames.Frame.ICRF, Aberration.None));
        Assert.Contains("Insufficient ephemeris data", single.Message);
        Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.ReadEphemeris(new Window(_case.T0.AddSeconds(10000.0), past),
            PlanetsAndMoons.EARTH.NaifId, CrossingEncounterCase.Id1, Frames.Frame.ICRF, Aberration.None, TimeSpan.FromSeconds(600.0)).ToArray());
        Assert.Throws<InvalidOperationException>(() => new Spacecraft(CrossingEncounterCase.Id1, "K1", past));

        // The analysis of kernel-backed spacecraft past their SPKs: before, a TCA with a miss of 0 m
        var k1 = new Spacecraft(CrossingEncounterCase.Id1, "K1", _case.T0.AddSeconds(10.0));
        var k2 = new Spacecraft(CrossingEncounterCase.Id2, "K2", _case.T0.AddSeconds(10.0));
        Assert.Throws<InvalidOperationException>(() => ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(k1), k2,
            _case.Seconds(12600.0, 18000.0), Options));

        // A frame past its PCK (earth_latest_high_prec.bpc ends in 2026): before, the zero quaternion
        var frame = Assert.Throws<InvalidOperationException>(() =>
            SpiceAPI.Instance.TransformFrame(new TimeSystem.Time(2040, 1, 1), new Frames.Frame("ITRF93"), Frames.Frame.ICRF));
        Assert.Contains("ITRF93", frame.Message);

        // A propagation reads the orientation of its central body only where a force needs it: a point mass about the
        // Earth runs past the PCK, a geopotential there throws
        var epoch2040 = new TimeSystem.Time(2040, 1, 1, 0, 0, 0, frame: TimeFrame.TDBFrame);
        var window2040 = new Window(epoch2040, epoch2040.AddSeconds(600.0));
        StateVector Leo(CelestialBody body) => new(new Vector3(6878137.0, 0.0, 0.0), new Vector3(0.0, 7612.6, 0.0), body, epoch2040,
            Frames.Frame.ICRF);
        var pointMass = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, epoch2040);
        var free = new Spacecraft(-620004, "PCK2040", 100.0, 1000.0, new Clock("PCK2040_CLK", 256), Leo(pointMass));
        free.Propagate(window2040, new CelestialItem[] { pointMass }, new RK78Integrator(), false, false, TimeSpan.FromSeconds(60.0));
        var withField = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, epoch2040,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
        var bound = new Spacecraft(-620005, "PCK2040F", 100.0, 1000.0, new Clock("PCK2040F_CLK", 256), Leo(withField));
        Assert.Throws<InvalidOperationException>(() => bound.Propagate(window2040, new CelestialItem[] { withField }, new RK78Integrator(),
            false, false, TimeSpan.FromSeconds(60.0)));

        // de440s does not chain Mars to its barycenter: the body is built without initial orbital parameters (an
        // undefined state before), and a read of its position throws
        var mars = new CelestialBody(PlanetsAndMoons.MARS, Frames.Frame.ICRF, _case.T0);
        Assert.Null(mars.InitialOrbitalParameters);
        Assert.Throws<InvalidOperationException>(() => mars.GetEphemeris(_case.T0, new Barycenter(0), Frames.Frame.ICRF, Aberration.None));

        // Inside the coverage nothing moves: a unit quaternion
        var inside = SpiceAPI.Instance.TransformFrame(_case.T0, new Frames.Frame("ITRF93"), Frames.Frame.ICRF);
        Assert.Equal(1.0, inside.Rotation.Magnitude(), 12);
    }
}
