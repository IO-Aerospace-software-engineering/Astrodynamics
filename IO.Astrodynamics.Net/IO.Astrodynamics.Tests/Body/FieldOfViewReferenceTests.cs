using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The kernels and the CSPICE windows of <c>tools/fov_reference</c> (choice 9 (a) of #515), loaded once for the class:
/// the reload of the solar system kernels by each test would take most of its time.
/// </summary>
public sealed class FieldOfViewReferenceKernels
{
    internal static readonly DirectoryInfo Path = new("Data/FieldOfViewReference");

    public FieldOfViewReferenceKernels()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        SpiceAPI.Instance.LoadKernels(Path);
        Root = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(Path.FullName, "fov_reference.json"))).RootElement;
    }

    internal JsonElement Root { get; }
}

/// <summary>
/// The field-of-view search (#515) against CSPICE, on the kernels of <c>tools/fov_reference</c> (choice 9 (a)): two
/// spacecraft that CSPICE reads, an inclined LEO whose attitude turns (a type 3 CK) and a polar LEO of constant attitude,
/// with the fields of the IK of each instrument; the Earth, the Moon and the Sun as points and as ellipsoids, DSS-13, a
/// spacecraft target, the Earth-Moon barycenter, the Lagrange point L1 and a star.
/// </summary>
/// <remarks>
/// <para>
/// Two references. The windows of <c>gftfov</c> (and <c>gfrfov</c> for the star) at a step of 1 s, converged to 1e-9 s
/// (<c>gfstol_c</c>), and the states of <c>fovtrg</c> and <c>fovray</c> on a grid, written by the harness of
/// <c>tools/fov_reference</c> (<c>fov_reference.json</c>), finer than what they measure: the managed search converges to
/// 1 µs. And the public path of the library, <c>gftfov</c> in the test process at its default tolerance (1e-6 s) for a
/// kernel-backed observer and target.
/// </para>
/// <para>
/// The managed search runs on the kernel-backed observers through <see cref="FieldOfViewSearch.Managed"/> (the public
/// method sends them to <c>gftfov</c>): it reads their states and their attitude from the kernels, so both searches see
/// the same trajectory and the same field, and the difference is that of the geometry, of the root finding and of the
/// rounding of the epochs (choice 10, below). The instruments are built from the definitions the harness wrote in the
/// JSON, the same as its IKs.
/// </para>
/// <para>
/// The rows of the uncategorized theories are the worst case of each limit, every field shape against both target
/// shapes, every aberration and every kind of target; the <c>Validation</c> theories hold the rest of the harness's grid.
/// </para>
/// </remarks>
public class FieldOfViewReferenceTests : IClassFixture<FieldOfViewReferenceKernels>
{
    // PROVISIONAL limits, twice the worst value over the rows on Linux x64 (2026-11-01), rounded up at the second digit,
    // until the three platforms of the CI have printed their MEASURE lines (#515, choice 9).
    // Geometric (NONE, and a star, read as a direction without light time): 4.77e-7 s against gftfov and gfrfov at
    // 1e-9 s, the managed search at 1 µs (its tolerance) and the 119 ns ulp of an ET of 8.3e8 s.
    // With the light time: 2.15e-6 s (-470101002, the Earth as an ellipsoid, LT). The trace of choice 10: the light-time
    // corrected epoch is a double ET in gftfov (et - lt) and in the library (its ticks converted for SPICE), each rounded
    // up to half its ulp, 6e-8 s, which moves the Earth by up to 1.8 mm from the barycenter; the two positions differ by
    // 0.25 to 1.5 mm at the edges of these rows, and the zero of the library's margin computed on gftfov's positions is
    // within 1.5e-7 s of gftfov's edge, its axes equal to SPICE's.
    // The public path, gftfov at 1e-6 s in the process: 9.54e-7 s against the reference
    private const double ReferenceLimit = 9.6e-7;
    private const double LightTimeReferenceLimit = 4.3e-6;
    private const double PublicPathLimit = 1.9e-6;

    // An epoch of the grids within this much of an edge of the reference is not compared: the edges are known to the
    // limits above, the grid is every 30 s, or every 1 s for the narrow fields
    private const double GridEdgeMargin = 1.0e-3;

    // The rows of the uncategorized theory: the worst row of each limit and of each kind of target, every field shape
    // with both target shapes, every aberration (Linux, 2026-11-01)
    private static readonly string[] WorstCases =
    [
        "-470101002 EARTH ITRF93 LT", "-470101010 EARTH ITRF93 LT+S", "-470101001 EARTH ITRF93 CN", "-470101004 EARTH ITRF93 LT",
        "-470101005 EARTH ITRF93 XLT+S", "-470101011 -470102 POINT CN", "-470101005 EARTH POINT LT+S", "-470101005 EARTH ITRF93 CN+S",
        "-470101002 3 POINT LT", "-470101001 399013 POINT CN+S", "-470101010 3 POINT LT+S", "-470101002 3 POINT LT+S",
        "-470101001 EARTH POINT NONE", "-470101001 EARTH ITRF93 NONE", "-470101002 EARTH POINT NONE", "-470101002 EARTH ITRF93 NONE",
        "-470101004 EARTH POINT LT", "-470101005 EARTH ITRF93 NONE", "-470101007 EARTH POINT LT", "-470101007 EARTH ITRF93 LT+S",
        "-470101008 EARTH ITRF93 LT", "-470101009 EARTH POINT LT", "-470101011 EARTH ITRF93 LT", "-470101001 EARTH IAU_EARTH LT",
        "-470101001 EARTH POINT XLT", "-470101001 EARTH ITRF93 XCN", "-470101001 EARTH POINT XCN+S", "-470101021 MOON MOON_ME LT+S",
        "-470101022 MOON POINT LT", "-470101023 MOON MOON_ME NONE", "-470101024 MOON MOON_ME LT", "-470101025 MOON POINT LT+S",
        "-470101031 SUN IAU_SUN LT+S", "-470101033 SUN POINT LT", "-470101034 SUN IAU_SUN NONE", "-470101035 391 POINT LT",
        "-470103001 EARTH ITRF93 LT", "-470103002 EARTH POINT LT+S", "-470103003 EARTH POINT LT", "-470101011 399013 POINT LT+S"
    ];

    private readonly ITestOutputHelper _output;
    private readonly JsonElement _root;

    public FieldOfViewReferenceTests(FieldOfViewReferenceKernels kernels, ITestOutputHelper output)
    {
        _output = output;
        _root = kernels.Root;
    }

    private static JsonElement ReadRoot()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(FieldOfViewReferenceKernels.Path.FullName, "fov_reference.json")));
        return document.RootElement.Clone();
    }

    private TimeSystem.Time Epoch => TimeSystem.Time.CreateTDB(_root.GetProperty("epoch").GetDouble());

    private Window SearchWindow
    {
        get
        {
            var window = _root.GetProperty("window");
            return new Window(TimeSystem.Time.CreateTDB(window[0].GetDouble()), TimeSystem.Time.CreateTDB(window[1].GetDouble()));
        }
    }

    public static IEnumerable<object[]> WorstGftfovCases() => WorstCases.Select(x => new object[] { x });

    public static IEnumerable<object[]> OtherGftfovCases() =>
        ReadRoot().GetProperty("gftfov").EnumerateObject().Select(x => x.Name).Except(WorstCases).Select(x => new object[] { x }).ToArray();

    public static IEnumerable<object[]> FovtrgCases() =>
        ReadRoot().GetProperty("fovtrg").EnumerateObject().Select(x => new object[] { x.Name }).ToArray();

    /// <summary>A kernel-backed spacecraft of the harness, with its instruments as the JSON defines them.</summary>
    private Spacecraft Observer(int naifId)
    {
        var definition = _root.GetProperty("spacecraft").EnumerateArray().First(x => x.GetProperty("id").GetInt32() == naifId);
        string name = definition.GetProperty("name").GetString();
        var spacecraft = new Spacecraft(naifId, name, Epoch, 1000.0, 3000.0, new Clock(name + "_CLK", 65536));
        foreach (var instrument in _root.GetProperty("instruments").EnumerateArray().Where(x => x.GetProperty("spacecraft").GetInt32() == naifId))
        {
            int id = instrument.GetProperty("id").GetInt32();
            string instrumentName = instrument.GetProperty("name").GetString();
            double refAngle = instrument.GetProperty("refAngle").GetDouble();
            double crossAngle = instrument.GetProperty("crossAngle").GetDouble();
            var boresight = Vector(instrument.GetProperty("boresight"));
            var refVector = Vector(instrument.GetProperty("refVector"));
            var orientation = Vector(instrument.GetProperty("orientation"));
            switch (instrument.GetProperty("shape").GetString())
            {
                case "CIRCLE":
                    spacecraft.AddCircularInstrument(id, instrumentName, "fov_reference", refAngle, boresight, refVector, orientation);
                    break;
                case "ELLIPSE":
                    spacecraft.AddEllipticalInstrument(id, instrumentName, "fov_reference", refAngle, crossAngle, boresight, refVector, orientation);
                    break;
                default:
                    spacecraft.AddRectangularInstrument(id, instrumentName, "fov_reference", refAngle, crossAngle, boresight, refVector, orientation);
                    break;
            }
        }

        return spacecraft;
    }

    private static Vector3 Vector(JsonElement element)
    {
        return new Vector3(element[0].GetDouble(), element[1].GetDouble(), element[2].GetDouble());
    }

    private Instrument InstrumentOf(int instrumentId)
    {
        return Observer(instrumentId / 1000).Instruments.First(x => x.NaifId == instrumentId);
    }

    /// <summary>The star of the harness: a direction of J2000, at a distance where the parallax of the observer is 5e-12 rad.</summary>
    private Star FovStar
    {
        get
        {
            var star = _root.GetProperty("star");
            var epoch = Epoch;
            return new Star(470001, "FOVSTAR1", 1.989e30, "G2V", 5.0, 1.0e-6,
                new Equatorial(star.GetProperty("declination").GetDouble(), star.GetProperty("rightAscension").GetDouble(), epoch), 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
                epoch);
        }
    }

    private ILocalizable Target(string name)
    {
        return name switch
        {
            "EARTH" => new CelestialBody(PlanetsAndMoons.EARTH),
            "MOON" => new CelestialBody(PlanetsAndMoons.MOON),
            "SUN" => new CelestialBody(Stars.Sun),
            "399013" => new Site(13, "DSS-13", new CelestialBody(PlanetsAndMoons.EARTH)),
            "-470102" => new Spacecraft(-470102, "FOVTARGET", Epoch, 1000.0, 3000.0, new Clock("FOVTARGET_CLK", 65536)),
            "3" => new Barycenter(3),
            "391" => new LagrangePoint(LagrangePoints.L1),
            _ => throw new ArgumentException(name)
        };
    }

    internal static Aberration ParseAberration(string abcorr)
    {
        return abcorr switch
        {
            "NONE" => Aberration.None,
            "LT" => Aberration.LT,
            "LT+S" => Aberration.LTS,
            "CN" => Aberration.CN,
            "CN+S" => Aberration.CNS,
            "XLT" => Aberration.XLT,
            "XLT+S" => Aberration.XLTS,
            "XCN" => Aberration.XCN,
            "XCN+S" => Aberration.XCNS,
            _ => throw new ArgumentException(abcorr)
        };
    }

    /// <summary>The correction of a ray (gfrfov, fovray) that the library applies to a star read as a direction (choice 13).</summary>
    private static string RayCorrection(Aberration aberration)
    {
        return aberration switch
        {
            Aberration.LTS or Aberration.CNS => "S",
            Aberration.XLTS or Aberration.XCNS => "XS",
            _ => "NONE"
        };
    }

    /// <summary>The managed search's step: 60 s about the Earth, 2 s for the narrow fields of the Moon, the Sun and the star.</summary>
    private static TimeSpan ManagedStep(Instrument instrument)
    {
        return TimeSpan.FromSeconds(IsNarrow(instrument) ? 2.0 : 60.0);
    }

    /// <summary>gftfov's step, shorter than every window and gap, as gftfov requires: 1 s for the short events.</summary>
    private static TimeSpan SpiceStep(Instrument instrument)
    {
        return TimeSpan.FromSeconds(IsNarrow(instrument) || instrument.Name == "ELLIPSE4" ? 1.0 : 60.0);
    }

    private static bool IsNarrow(Instrument instrument)
    {
        return instrument.Spacecraft.NaifId == -470101 && -instrument.NaifId % 1000 > 20;
    }

    private static double[][] Windows(JsonElement element)
    {
        return element.EnumerateArray().Select(x => new[] { x[0].GetDouble(), x[1].GetDouble() }).ToArray();
    }

    /// <summary>The largest difference between the edges of windows and those of the reference, in seconds; they must pair, in TDB.</summary>
    internal static double WorstEdge(IReadOnlyList<Window> actual, double[][] reference)
    {
        Assert.Equal(reference.Length, actual.Count);
        double worst = 0.0;
        for (int i = 0; i < reference.Length; i++)
        {
            Assert.Equal(TimeFrame.TDBFrame, actual[i].StartDate.Frame);
            Assert.Equal(TimeFrame.TDBFrame, actual[i].EndDate.Frame);
            worst = System.Math.Max(worst, System.Math.Abs(actual[i].StartDate.TimeSpanFromJ2000().TotalSeconds - reference[i][0]));
            worst = System.Math.Max(worst, System.Math.Abs(actual[i].EndDate.TimeSpanFromJ2000().TotalSeconds - reference[i][1]));
        }

        return worst;
    }

    /// <summary>The worst rows: the managed search against the reference, and the public path (gftfov in the process) against it.</summary>
    [Theory]
    [MemberData(nameof(WorstGftfovCases))]
    public void ManagedSearch_MatchesGftfov(string key)
    {
        CompareWithGftfov(key);
    }

    /// <summary>The rest of the harness's grid.</summary>
    [Theory]
    [Trait("Category", "Validation")]
    [MemberData(nameof(OtherGftfovCases))]
    public void ManagedSearch_MatchesGftfov_Grid(string key)
    {
        CompareWithGftfov(key);
    }

    private void CompareWithGftfov(string key)
    {
        var parts = key.Split(' ');
        var instrument = InstrumentOf(int.Parse(parts[0]));
        var target = Target(parts[1]);
        var shape = parts[2] == "POINT" ? ShapeType.Point : ShapeType.Ellipsoid;
        var frame = shape == ShapeType.Point ? Frames.Frame.ICRF : new Frames.Frame(parts[2]);
        var aberration = ParseAberration(parts[3]);
        var reference = Windows(_root.GetProperty("gftfov").GetProperty(key));

        var managed = FieldOfViewSearch.Managed(instrument, SearchWindow, target, shape, frame, aberration, ManagedStep(instrument));
        double worstManaged = WorstEdge(managed, reference);

        var spiceStep = SpiceStep(instrument);
        var publicPath = instrument.FindWindowsInFieldOfViewConstraint(SearchWindow, instrument.Spacecraft, target, frame, shape, aberration, spiceStep).ToArray();
        double worstPublic = WorstEdge(publicPath, reference);
        _output.WriteLine($"MEASURE #515 reference {key}, {reference.Length} windows: |managed (step {ManagedStep(instrument).TotalSeconds} s) - gftfov (1e-9 s)| " +
                          $"{worstManaged:E2} s, |public path (step {spiceStep.TotalSeconds} s) - gftfov (1e-9 s)| {worstPublic:E2} s");
        Assert.True(worstManaged < (aberration == Aberration.None ? ReferenceLimit : LightTimeReferenceLimit), $"{worstManaged:E2} s");
        Assert.True(worstPublic < PublicPathLimit, $"{worstPublic:E2} s");
    }

    /// <summary>
    /// The star, a direction (choice 13): every correction of the library against gfrfov with NONE, S or XS, on the
    /// managed path, which the public method takes for a star; an ellipsoid star throws on that path.
    /// </summary>
    [Theory]
    [InlineData(-470101041)]
    [InlineData(-470101042)]
    public void Star_MatchesGfrfov(int instrumentId)
    {
        var instrument = InstrumentOf(instrumentId);
        var star = FovStar;
        foreach (var aberration in Enum.GetValues<Aberration>())
        {
            var reference = Windows(_root.GetProperty("gfrfov").GetProperty($"{instrumentId} FOVSTAR1 {RayCorrection(aberration)}"));
            var managed = instrument.FindWindowsInFieldOfViewConstraint(SearchWindow, instrument.Spacecraft, star, Frames.Frame.ICRF, ShapeType.Point, aberration,
                ManagedStep(instrument)).ToArray();
            double worst = WorstEdge(managed, reference);
            _output.WriteLine($"MEASURE #515 reference {instrumentId} FOVSTAR1 {aberration}, {reference.Length} windows: |managed - gfrfov {RayCorrection(aberration)} (1e-9 s)| {worst:E2} s");
            Assert.True(worst < ReferenceLimit, $"{worst:E2} s");
        }

        Assert.Throws<ArgumentException>(() => instrument.FindWindowsInFieldOfViewConstraint(SearchWindow, instrument.Spacecraft, star, Frames.Frame.ICRF,
            ShapeType.Ellipsoid, Aberration.None, TimeSpan.FromSeconds(2.0)));
    }

    /// <summary>
    /// <see cref="Instrument.IsInFOV"/> against fovtrg POINT on the harness's grid, at every epoch more than 1 ms from an
    /// edge of gftfov's windows when the reference has them.
    /// </summary>
    [Theory]
    [Trait("Category", "Validation")]
    [MemberData(nameof(FovtrgCases))]
    public void IsInFOV_MatchesFovtrg(string key)
    {
        CompareWithFovtrg(key);
    }

    /// <summary>The same, uncategorized, on a circle, an ellipse, a rectangle (turned), a narrow field of the Sun and the polar orbit.</summary>
    [Theory]
    [InlineData("-470101001 EARTH LT+S")]
    [InlineData("-470101010 EARTH LT")]
    [InlineData("-470101007 EARTH NONE")]
    [InlineData("-470101001 399013 LT")]
    [InlineData("-470101011 -470102 LT")]
    [InlineData("-470101033 SUN LT+S")]
    [InlineData("-470103003 EARTH LT")]
    public void IsInFOV_MatchesFovtrg_Worst(string key)
    {
        CompareWithFovtrg(key);
    }

    private void CompareWithFovtrg(string key)
    {
        var parts = key.Split(' ');
        var instrument = InstrumentOf(int.Parse(parts[0]));
        var target = Target(parts[1]);
        var aberration = ParseAberration(parts[2]);
        var grid = _root.GetProperty("fovtrg").GetProperty(key);
        var edges = _root.GetProperty("gftfov").TryGetProperty($"{parts[0]} {parts[1]} POINT {parts[2]}", out var windows)
            ? Windows(windows).SelectMany(x => x).ToArray()
            : Array.Empty<double>();
        int compared = CompareGrid(grid, edges, epoch => instrument.IsInFOV(epoch, target, aberration));
        _output.WriteLine($"MEASURE #515 fovtrg {key}: {compared} epochs equal");
        Assert.True(compared >= grid.GetProperty("inView").GetString()!.Length - 4);
    }

    /// <summary><see cref="Instrument.IsInFOV"/> of the star against fovray, for every correction of the library.</summary>
    [Theory]
    [InlineData(-470101041)]
    [InlineData(-470101042)]
    public void IsInFOV_Star_MatchesFovray(int instrumentId)
    {
        var instrument = InstrumentOf(instrumentId);
        var star = FovStar;
        foreach (var aberration in Enum.GetValues<Aberration>())
        {
            string correction = RayCorrection(aberration);
            var grid = _root.GetProperty("fovray").GetProperty($"{instrumentId} FOVSTAR1 {correction}");
            var edges = Windows(_root.GetProperty("gfrfov").GetProperty($"{instrumentId} FOVSTAR1 {correction}")).SelectMany(x => x).ToArray();
            int compared = CompareGrid(grid, edges, epoch => instrument.IsInFOV(epoch, star, aberration));
            Assert.True(compared > 190);
        }
    }

    private static int CompareGrid(JsonElement grid, double[] edges, Func<TimeSystem.Time, bool> inView)
    {
        double first = grid.GetProperty("first").GetDouble();
        double step = grid.GetProperty("step").GetDouble();
        string states = grid.GetProperty("inView").GetString()!;
        int compared = 0;
        for (int i = 0; i < states.Length; i++)
        {
            double et = first + i * step;
            if (edges.Any(x => System.Math.Abs(x - et) < GridEdgeMargin))
            {
                continue;
            }

            Assert.True(states[i] == '1' == inView(TimeSystem.Time.CreateTDB(et)), $"epoch {et}: fovtrg {states[i]}");
            compared++;
        }

        return compared;
    }

    /// <summary>
    /// A search window tagged UTC: both paths return TDB windows, the same as with the TDB window (the managed path returned
    /// the frame of the search window before #515).
    /// </summary>
    [Fact]
    public void UtcSearchWindow_ReturnsTdbWindows()
    {
        var instrument = InstrumentOf(-470101001);
        var utcWindow = new Window(SearchWindow.StartDate.ToUTC(), SearchWindow.EndDate.ToUTC());
        Assert.Equal(TimeFrame.UTCFrame, utcWindow.StartDate.Frame);
        foreach (var key in new[] { "-470101001 EARTH ITRF93 LT", "-470101001 399013 POINT LT" })
        {
            var parts = key.Split(' ');
            var target = Target(parts[1]);
            var shape = parts[2] == "POINT" ? ShapeType.Point : ShapeType.Ellipsoid;
            var frame = shape == ShapeType.Point ? Frames.Frame.ICRF : new Frames.Frame(parts[2]);
            var reference = Windows(_root.GetProperty("gftfov").GetProperty(key));
            var managed = FieldOfViewSearch.Managed(instrument, utcWindow, target, shape, frame, Aberration.LT, TimeSpan.FromSeconds(60.0));
            var publicPath = instrument.FindWindowsInFieldOfViewConstraint(utcWindow, instrument.Spacecraft, target, frame, shape, Aberration.LT, TimeSpan.FromSeconds(60.0))
                .ToArray();
            Assert.True(WorstEdge(managed, reference) < LightTimeReferenceLimit);
            Assert.True(WorstEdge(publicPath, reference) < PublicPathLimit);
        }
    }

    /// <summary>
    /// The SPICE path of a kernel-backed spacecraft, a search window outside the coverage of its kernels: no window and no
    /// exception, the error of gftfov dropped by the native proxy (#454, the C++ layer frozen; §8 item 5 of #515). The
    /// managed path has no coverage check for a kernel-backed spacecraft (choice 14 is for propagated states): the native
    /// reading of its states outside the kernels is a defect of its own, outside #515.
    /// </summary>
    [Fact]
    public void SpicePath_OutsideKernelCoverage_ReturnsNoWindow()
    {
        var instrument = InstrumentOf(-470101001);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var outside = new Window(SearchWindow.EndDate.AddSeconds(3600.0), SearchWindow.EndDate.AddSeconds(7200.0));
        Assert.Empty(instrument.FindWindowsInFieldOfViewConstraint(outside, instrument.Spacecraft, earth, Frames.Frame.ICRF, ShapeType.Point, Aberration.LT,
            TimeSpan.FromSeconds(60.0)));
    }
}
