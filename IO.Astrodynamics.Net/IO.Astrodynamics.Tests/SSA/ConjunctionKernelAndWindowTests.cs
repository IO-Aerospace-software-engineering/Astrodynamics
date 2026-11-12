using System;
using System.Collections.Concurrent;
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
        TestKernels.EnsureSolarSystem();
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

    /// <summary>
    /// The local minima of CSPICE on a window of this fixture, by window (<c>CspiceLocalMinima</c> of
    /// <see cref="ConjunctionDensePassTestBase"/>): computed once per window, the same bits as each call computed them, the
    /// SPKs of the fixture staying loaded until it is disposed and its ids appearing nowhere else in the tests.
    /// </summary>
    internal readonly ConcurrentDictionary<Window, (TimeSystem.Time Epoch, double Offset, double Distance)[]> LocalMinima = new();

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
/// <remarks>
/// The slow passes and the passes with two minima per orbit of round 4 of the lot-6 review to #628 are in the five classes
/// of <c>ConjunctionDensePassTests.cs</c>, which own their fixtures (<see cref="ConjunctionDensePassTestBase"/>: xUnit runs
/// the tests of a class one after the other, and this class held them all, 571 s on Linux x64 in run 43256478324).
/// </remarks>
public class ConjunctionKernelAndWindowTests : ConjunctionDensePassTestBase, IClassFixture<CrossingEncounterCase>,
    IClassFixture<DefaultStepCrossingEncounterCase>
{
    private readonly CrossingEncounterCase _case;
    private readonly DefaultStepCrossingEncounterCase _defaultStepCase;

    public ConjunctionKernelAndWindowTests(CrossingEncounterCase crossingCase,
        DefaultStepCrossingEncounterCase defaultStepCase, ITestOutputHelper output)
        : base(output, ("CROSS", crossingCase), ("CROSSD", defaultStepCase))
    {
        _case = crossingCase;
        _defaultStepCase = defaultStepCase;
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

    // The local minima of CSPICE on a window of the crossing at 60 s steps (CspiceLocalMinima of the base)
    private (TimeSystem.Time Epoch, double Offset, double Distance)[] CspiceLocalMinima(Window window) =>
        CspiceLocalMinima(window, _case);

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
        // was taken on the grid of ticks (Time.AddSeconds then truncated to the tick), up to 0.96 tick from its zero: 1e-7 s
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
