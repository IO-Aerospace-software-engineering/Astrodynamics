using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Occultation;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Occultation;

/// <summary>
/// The managed occultation search (#277, decision 18 (b) of #457) against CSPICE's <c>gfoclt</c>, on trajectories
/// that CSPICE reads: the two-body SPKs of <c>tools/occultation_reference</c> (eclipse 001, the start of a GEO eclipse
/// season, grazing LEO eclipses), the reconstructed LRO trajectory (the Moon occulting the Sun), the solar eclipse of
/// 2001-12-14 seen from the Earth's center and that of 2005-10-03 seen from DSS-13 and from DSS-65 (annular); and
/// eclipse 001 of a propagated spacecraft against the decided golden, GMAT R2026a's EclipseLocator.
/// </summary>
/// <remarks>
/// <para>
/// Two references. The windows of <c>gfoclt</c> converged to 1e-9 s (<c>gfstol_c</c>), written by the harness of
/// <c>tools/occultation_reference</c> (<c>occultation_reference.json</c>), finer than what they measure: the managed
/// search converged to 100 ns, the resolution of a <see cref="TimeSystem.Time"/>. And <c>gfoclt</c> run in the test
/// process on the same kernels through <see cref="SpiceAPI"/>, at its default tolerance of 1e-6 s, against the managed
/// search at its own default, 1e-6 s: whether the two paths of the library agree.
/// </para>
/// <para>
/// The managed search runs on the kernel-backed observers through <see cref="OccultationSearch.Managed"/> (the public
/// methods send them to <c>gfoclt</c>): it reads their states from the kernels, so both searches see the same
/// trajectory, and the difference is that of the geometry and of the root finding. The thresholds are provisional,
/// twice the Linux value rounded up at the second significant digit, until the three platforms of the CI have printed
/// their MEASURE lines.
/// </para>
/// </remarks>
public class OccultationSearchValidationTests
{
    private static readonly DirectoryInfo ReferencePath = new("Data/OccultationReference");
    private static readonly FileInfo LroSpkPath = new("Data/LRO/lro-reconstructed-2023349_2024075_v01_little_endian.bsp");

    // Provisional thresholds, twice the worst value over the scenarios on Linux x64 (2026-10-27), rounded up at the second
    // digit. Geometric: 1.49e-7 s against the reference at 1e-9 s, the managed search at 100 ns (the tick, and the 119 ns
    // ulp of an ET of 8e8 s); 7e-7 s against gfoclt at 1e-6 s, the managed search at 1e-6 s. With the light time, both
    // gfoclt and the managed search read the Earth at its corrected epoch, an ET in double whose rounding (up to 60 ns,
    // half the ulp) moves the Earth by up to 1.8 mm; seen from 7000 km, the contacts of eclipse 001 move by about 1 µs:
    // 1.31e-6 s against the reference, 1.5e-6 s against gfoclt, the noise of the reference itself
    private const double ReferenceThreshold = 3.0e-7;
    private const double GfocltThreshold = 1.4e-6;
    private const double LightTimeReferenceThreshold = 2.7e-6;
    private const double LightTimeGfocltThreshold = 3.0e-6;

    private static readonly TimeSpan FineTolerance = TimeSpan.FromTicks(1);

    private static readonly Lazy<Dictionary<string, double[][]>> Reference = new(() =>
        JsonSerializer.Deserialize<Dictionary<string, double[][]>>(File.ReadAllText(Path.Combine(ReferencePath.FullName, "occultation_reference.json"))));

    private readonly ITestOutputHelper _output;

    public OccultationSearchValidationTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        SpiceAPI.Instance.LoadKernels(ReferencePath);
        SpiceAPI.Instance.LoadKernels(LroSpkPath);
    }

    private static readonly Frames.Frame IauEarth = new("IAU_EARTH");

    private static Window Eclipse001Window => new(new TimeSystem.Time("2026-03-20T00:00:00.0000000 TDB"), new TimeSystem.Time("2026-03-20T12:00:00.0000000 TDB"));

    private static Window GeoSeasonWindow => new(new TimeSystem.Time("2026-02-20T00:00:00.0000000 TDB"), new TimeSystem.Time("2026-03-08T00:00:00.0000000 TDB"));

    private static Window LeoGrazingWindow => new(new TimeSystem.Time("2026-06-10T00:00:00.0000000 TDB"), new TimeSystem.Time("2026-06-11T00:00:00.0000000 TDB"));

    private static Window LroWindow => new(new TimeSystem.Time(2023, 12, 15, 0, 5, 0, frame: TimeFrame.UTCFrame), new TimeSystem.Time(2023, 12, 15, 12, 0, 0, frame: TimeFrame.UTCFrame));

    private static Window Eclipse2005Window => new(new TimeSystem.Time("2005-10-03 00:00:00"), new TimeSystem.Time("2005-10-04 00:00:00"));

    private static Window Eclipse2001Window => new(TimeSystem.Time.CreateTDB(61473664.183390938), TimeSystem.Time.CreateTDB(61646464.183445148));

    /// <summary>The largest difference between the edges of two lists of windows, in seconds; they must pair.</summary>
    private static double WorstEdge(IReadOnlyList<Window> actual, IReadOnlyList<Window> expected)
    {
        Assert.Equal(expected.Count, actual.Count);
        double worst = 0.0;
        for (int i = 0; i < expected.Count; i++)
        {
            worst = System.Math.Max(worst, System.Math.Abs((actual[i].StartDate - expected[i].StartDate).TotalSeconds));
            worst = System.Math.Max(worst, System.Math.Abs((actual[i].EndDate - expected[i].EndDate).TotalSeconds));
        }

        return worst;
    }

    /// <summary>The largest difference between the edges of windows and those of the reference, in seconds.</summary>
    private static double WorstEdge(IReadOnlyList<Window> actual, double[][] reference)
    {
        Assert.Equal(reference.Length, actual.Count);
        double worst = 0.0;
        for (int i = 0; i < reference.Length; i++)
        {
            worst = System.Math.Max(worst, System.Math.Abs(actual[i].StartDate.ToTDB().TimeSpanFromJ2000().TotalSeconds - reference[i][0]));
            worst = System.Math.Max(worst, System.Math.Abs(actual[i].EndDate.ToTDB().TimeSpanFromJ2000().TotalSeconds - reference[i][1]));
        }

        return worst;
    }

    private static string CspiceName(OccultationType type) => type.ToString().ToUpperInvariant();

    private static string CspiceName(Aberration aberration) => aberration switch
    {
        Aberration.None => "NONE",
        Aberration.LT or Aberration.LTS => "LT",
        Aberration.CN or Aberration.CNS => "CN",
        Aberration.XLT or Aberration.XLTS => "XLT",
        _ => "XCN"
    };

    /// <summary>
    /// The managed search on a scenario against both references: <c>gfoclt</c> at 1e-9 s (the harness) with the managed
    /// search at 100 ns, and <c>gfoclt</c> in the process at 1e-6 s with the managed search at its default.
    /// </summary>
    private void Compare(string scenario, string referenceFrame, Window window, ILocalizable observer, CelestialBody target, CelestialBody front, OccultationType type,
        Aberration aberration, TimeSpan step, TimeSpan gfocltStep, Frames.Frame frontFrame, int count, bool publicPath = false)
    {
        var reference = Reference.Value[$"{scenario} {CspiceName(type)} {CspiceName(aberration)} {referenceFrame}"];
        Assert.Equal(count, reference.Length);

        IReadOnlyList<Window> Search(TimeSpan tolerance)
        {
            var options = new OccultationSearchOptions { FrontFrame = frontFrame, ConvergenceTolerance = tolerance };
            if (!publicPath)
            {
                return OccultationSearch.Managed(window, observer, target, ShapeType.Ellipsoid, front, ShapeType.Ellipsoid, type, aberration, step, options);
            }

            return (observer is Site site
                ? site.FindWindowsOnOccultationConstraint(window, target, ShapeType.Ellipsoid, front, ShapeType.Ellipsoid, type, aberration, step, options)
                : target.FindWindowsOnOccultationConstraint(window, observer, ShapeType.Ellipsoid, front, ShapeType.Ellipsoid, type, aberration, step, options)).ToArray();
        }

        double worstReference = WorstEdge(Search(FineTolerance), reference);
        var gfoclt = SpiceAPI.Instance.FindWindowsOnOccultationConstraint(window, observer.NaifId, target.NaifId, ShapeType.Ellipsoid, front.NaifId, ShapeType.Ellipsoid,
            type, aberration, gfocltStep, frontFrame, null).ToArray();
        double worstGfoclt = WorstEdge(Search(OccultationSearchOptions.DefaultConvergenceTolerance), gfoclt);
        _output.WriteLine($"MEASURE #277 {scenario}, {type}, {aberration}, {referenceFrame}, step {step.TotalSeconds} s, {count} windows: " +
                          $"worst |managed (100 ns) - gfoclt (1e-9 s)| {worstReference:E2} s, |managed (1 us) - gfoclt (1 us)| {worstGfoclt:E2} s");
        bool lightTime = aberration != Aberration.None;
        Assert.True(worstReference < (lightTime ? LightTimeReferenceThreshold : ReferenceThreshold), $"{worstReference:E2} s");
        Assert.True(worstGfoclt < (lightTime ? LightTimeGfocltThreshold : GfocltThreshold), $"{worstGfoclt:E2} s");
    }

    private void CompareEarthSun(string scenario, int naifId, Window window, OccultationType type, Aberration aberration, TimeSpan step, TimeSpan gfocltStep,
        string frontFrame, int count)
    {
        var spacecraft = new Spacecraft(naifId, scenario, epoch: window.StartDate);
        Compare(scenario, frontFrame, window, spacecraft, new CelestialBody(Stars.Sun), new CelestialBody(PlanetsAndMoons.EARTH), type, aberration, step, gfocltStep,
            new Frames.Frame(frontFrame), count);
    }

    [Theory]
    [InlineData(OccultationType.Any, 8)]
    [InlineData(OccultationType.Full, 8)]
    [InlineData(OccultationType.Partial, 15)]
    [InlineData(OccultationType.Annular, 0)]
    public void Eclipse001_MatchesCspice(OccultationType type, int count)
    {
        CompareEarthSun("eclipse_001", -470001, Eclipse001Window, type, Aberration.None, TimeSpan.FromSeconds(60.0), TimeSpan.FromSeconds(1.0), "IAU_EARTH", count);
    }

    [Theory]
    [InlineData(Aberration.LT)]
    [InlineData(Aberration.CN)]
    [InlineData(Aberration.XLT)]
    [InlineData(Aberration.XCN)]
    [InlineData(Aberration.LTS)]
    public void Eclipse001_LightTime_MatchesCspice(Aberration aberration)
    {
        // LT+S is LT: gfoclt ignores the stellar aberration, and so does the managed search
        CompareEarthSun("eclipse_001", -470001, Eclipse001Window, OccultationType.Any, aberration, TimeSpan.FromSeconds(60.0), TimeSpan.FromSeconds(1.0), "IAU_EARTH", 8);
    }

    [Theory]
    [InlineData(OccultationType.Any)]
    [InlineData(OccultationType.Full)]
    public void Eclipse001_Itrf93_MatchesCspice(OccultationType type)
    {
        CompareEarthSun("eclipse_001", -470001, Eclipse001Window, type, Aberration.None, TimeSpan.FromSeconds(60.0), TimeSpan.FromSeconds(1.0), "ITRF93", 8);
    }

    [Theory]
    [InlineData(OccultationType.Any, 10)]
    [InlineData(OccultationType.Full, 9)]
    [InlineData(OccultationType.Partial, 19)]
    public void GeoEclipseSeason_StepLongerThanTheEclipses_MatchesCspice(OccultationType type, int count)
    {
        // A one-hour step, longer than the first eclipses of the season (845 s, penumbral only, the Sun grazing the
        // limb): the extremes between the samples find them
        CompareEarthSun("geo_season", -470002, GeoSeasonWindow, type, Aberration.None, TimeSpan.FromHours(1.0), TimeSpan.FromSeconds(60.0), "IAU_EARTH", count);
    }

    [Theory]
    [InlineData(OccultationType.Any, 10)]
    [InlineData(OccultationType.Full, 2)]
    [InlineData(OccultationType.Partial, 12)]
    public void LeoGrazingEclipses_StepLongerThanTheEclipses_MatchesCspice(OccultationType type, int count)
    {
        // A 1200 s step, a quarter of the period: the grazing eclipses last 390 s down to 67 s
        CompareEarthSun("leo_grazing", -470003, LeoGrazingWindow, type, Aberration.None, TimeSpan.FromSeconds(1200.0), TimeSpan.FromSeconds(1.0), "IAU_EARTH", count);
    }

    [Theory]
    [InlineData(OccultationType.Any, 6)]
    [InlineData(OccultationType.Full, 6)]
    [InlineData(OccultationType.Partial, 12)]
    public void LunarOccultationOfTheSunSeenFromLro_MatchesCspice(OccultationType type, int count)
    {
        // The Moon hides the Sun from LRO once per orbit (about 2 h); the Moon of pck00011 is a sphere
        var lro = new Spacecraft(-85, "LRO", epoch: LroWindow.StartDate);
        Compare("lro", "IAU_MOON", LroWindow, lro, new CelestialBody(Stars.Sun), new CelestialBody(PlanetsAndMoons.MOON), type, Aberration.None,
            TimeSpan.FromSeconds(600.0), TimeSpan.FromSeconds(1.0), null, count);
    }

    [Theory]
    [InlineData(OccultationType.Any, 1)]
    [InlineData(OccultationType.Annular, 0)]
    [InlineData(OccultationType.Partial, 1)]
    [InlineData(OccultationType.Full, 0)]
    public void SolarEclipseSeenFromTheEarthCenter_MatchesCspice(OccultationType type, int count)
    {
        // The annular eclipse of 2001-12-14, partial from the Earth's center; the public method of a body searches in
        // managed code, the observer being no spacecraft
        Compare("earth_center_2001", "IAU_MOON", Eclipse2001Window, TestHelpers.EarthAtJ2000, TestHelpers.Sun, TestHelpers.MoonAt20011214, type, Aberration.None,
            TimeSpan.FromHours(1.0), TimeSpan.FromSeconds(60.0), null, count, true);
    }

    [Theory]
    [InlineData(OccultationType.Any, 1)]
    [InlineData(OccultationType.Annular, 1)]
    [InlineData(OccultationType.Partial, 2)]
    [InlineData(OccultationType.Full, 0)]
    public void AnnularSolarEclipseSeenFromDss65_StepLongerThanTheAnnularPhase_MatchesCspice(OccultationType type, int count)
    {
        // 2005-10-03 from Madrid, on the central line: the Moon's disc within the Sun's for 243 s, shorter than the 360 s
        // step; a site of the kernels searches in managed code, the target not being a spacecraft
        Compare("dss65_2005", "IAU_MOON", Eclipse2005Window, new Site(65, "DSS-65", TestHelpers.EarthAtJ2000), TestHelpers.Sun, TestHelpers.MoonAtJ2000, type,
            Aberration.None, TimeSpan.FromSeconds(360.0), TimeSpan.FromSeconds(10.0), null, count, true);
    }

    [Theory]
    [InlineData(OccultationType.Any, Aberration.None)]
    [InlineData(OccultationType.Partial, Aberration.None)]
    [InlineData(OccultationType.Any, Aberration.LT)]
    public void SolarEclipseSeenFromDss13_MatchesCspice(OccultationType type, Aberration aberration)
    {
        Compare("dss13_2005", "IAU_MOON", Eclipse2005Window, new Site(13, "DSS-13", TestHelpers.EarthAtJ2000), TestHelpers.Sun, TestHelpers.MoonAtJ2000, type,
            aberration, TimeSpan.FromSeconds(360.0), TimeSpan.FromSeconds(10.0), null, 1, true);
    }

    // The decided golden of eclipse 001: GMAT R2026a's EclipseLocator, run "icrf" of the study of conformance-tests (two-body,
    // GMAT's Earth GM 398600.4415 km^3/s^2, pck00011 ellipsoid in IAU_EARTH, geometric), epochs printed to the millisecond in
    // UTC, durations to 1e-7 s
    private static readonly TimeSystem.Time GmatPenumbraEntry = new("2026-03-20T00:18:37.494Z");
    private static readonly TimeSystem.Time GmatUmbraEntry = new("2026-03-20T00:18:46.864Z");
    private static readonly TimeSystem.Time GmatUmbraExit = new("2026-03-20T00:53:10.862Z");
    private static readonly TimeSystem.Time GmatPenumbraExit = new("2026-03-20T00:53:20.204Z");
    private const double GmatPenumbraDuration = 18.7118606991;
    private const double GmatUmbraDuration = 2063.9981917;

    private static Spacecraft PropagatedEclipse001(int naifId, TimeSpan outputStep)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Eclipse001Window.StartDate);
        var orbit = new KeplerianElements(7000000.0, 0.001, 28.5 * IO.Astrodynamics.Constants.Deg2Rad, 45.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, earth,
            Eclipse001Window.StartDate, Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(naifId, "Eclipse001", 100.0, 1000.0, new Clock("Eclipse001Clk", 65536), orbit);
        spacecraft.Propagate(Eclipse001Window, [earth], new RK78Integrator(1e-12, 1e-12, 60.0), false, false, outputStep);
        return spacecraft;
    }

    [Fact]
    public void Eclipse001_PropagatedSpacecraft_MatchesGfocltAndGmat()
    {
        // The public path of a propagated spacecraft: two-body with the kernel GM, searched in IAU_EARTH as decided.
        // gfoclt's trajectory and GMAT's take GMAT's GM, 1.5e-8 of it larger: the eclipses drift by 0.75e-8 of the time
        // from the epoch of the elements, 3.2e-4 s after 12 h; GMAT's epochs are rounded to the millisecond
        var spacecraft = PropagatedEclipse001(-470101, TimeSpan.FromSeconds(10.0));
        var kernelSpacecraft = new Spacecraft(-470001, "eclipse_001", epoch: Eclipse001Window.StartDate);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var sun = new CelestialBody(Stars.Sun);
        var options = new OccultationSearchOptions { FrontFrame = IauEarth };
        var started = DateTime.UtcNow;
        var any = sun.FindWindowsOnOccultationConstraint(Eclipse001Window, spacecraft, ShapeType.Ellipsoid, earth, ShapeType.Ellipsoid, OccultationType.Any,
            Aberration.None, TimeSpan.FromSeconds(60.0), options).ToArray();
        var elapsed = DateTime.UtcNow - started;
        var full = sun.FindWindowsOnOccultationConstraint(Eclipse001Window, spacecraft, ShapeType.Ellipsoid, earth, ShapeType.Ellipsoid, OccultationType.Full,
            Aberration.None, TimeSpan.FromSeconds(60.0), options).ToArray();
        var gfocltAny = SpiceAPI.Instance.FindWindowsOnOccultationConstraint(Eclipse001Window, kernelSpacecraft.NaifId, sun.NaifId, ShapeType.Ellipsoid, earth.NaifId,
            ShapeType.Ellipsoid, OccultationType.Any, Aberration.None, TimeSpan.FromSeconds(1.0), IauEarth, null).ToArray();
        var gfocltFull = SpiceAPI.Instance.FindWindowsOnOccultationConstraint(Eclipse001Window, kernelSpacecraft.NaifId, sun.NaifId, ShapeType.Ellipsoid, earth.NaifId,
            ShapeType.Ellipsoid, OccultationType.Full, Aberration.None, TimeSpan.FromSeconds(1.0), IauEarth, null).ToArray();

        double worstAny = WorstEdge(any, gfocltAny);
        double worstFull = WorstEdge(full, gfocltFull);
        double firstEclipse = new[]
        {
            (any[0].StartDate - gfocltAny[0].StartDate).TotalSeconds, (any[0].EndDate - gfocltAny[0].EndDate).TotalSeconds,
            (full[0].StartDate - gfocltFull[0].StartDate).TotalSeconds, (full[0].EndDate - gfocltFull[0].EndDate).TotalSeconds
        }.Max(System.Math.Abs);
        _output.WriteLine($"MEASURE #277 eclipse_001 propagated (kernel GM, 10 s output) - gfoclt (GMAT GM): first eclipse {firstEclipse:E2} s, " +
                          $"worst over 12 h, Any {worstAny:E2} s, Full {worstFull:E2} s; search of Any over 12 h at 60 s: {elapsed.TotalMilliseconds:F0} ms");

        double[] gmat =
        [
            (any[0].StartDate - GmatPenumbraEntry).TotalSeconds, (full[0].StartDate - GmatUmbraEntry).TotalSeconds,
            (full[0].EndDate - GmatUmbraExit).TotalSeconds, (any[0].EndDate - GmatPenumbraExit).TotalSeconds,
            any[0].Length.TotalSeconds - full[0].Length.TotalSeconds - GmatPenumbraDuration, full[0].Length.TotalSeconds - GmatUmbraDuration
        ];
        _output.WriteLine("MEASURE #277 eclipse_001 propagated - GMAT (penumbra entry, umbra entry, umbra exit, penumbra exit, penumbra duration, " +
                          $"umbra duration): {string.Join(", ", gmat.Select(x => x.ToString("E2")))} s");
        Assert.True(firstEclipse < 5.2e-5, $"{firstEclipse:E2} s");
        Assert.True(worstAny < 6.4e-4 && worstFull < 6.4e-4, $"{worstAny:E2} s, {worstFull:E2} s");
        Assert.All(gmat, x => Assert.True(System.Math.Abs(x) < 9.7e-4, $"{x:E2} s"));
    }

    [Fact]
    public void ConcurrentSearches_GiveTheResultsOfSequentialOnes()
    {
        // Searches on separate propagations, and on kernel trajectories, in parallel: the managed search keeps no state,
        // so each gives, to the tick, what it gives alone
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var sun = new CelestialBody(Stars.Sun);
        var spacecraft = Enumerable.Range(0, 4).Select(i => (ILocalizable)PropagatedEclipse001(-470110 - i, TimeSpan.FromSeconds(10.0)))
            .Concat([new Spacecraft(-470001, "eclipse_001", epoch: Eclipse001Window.StartDate), new Spacecraft(-470003, "leo_grazing", epoch: LeoGrazingWindow.StartDate)])
            .ToArray();
        var windows = spacecraft.Select(s => s.NaifId == -470003 ? LeoGrazingWindow : Eclipse001Window).ToArray();
        var options = new OccultationSearchOptions { FrontFrame = IauEarth };

        IReadOnlyList<Window> Search(int i) => OccultationSearch.Managed(windows[i], spacecraft[i], sun, ShapeType.Ellipsoid, earth, ShapeType.Ellipsoid,
            OccultationType.Partial, Aberration.LT, TimeSpan.FromSeconds(300.0), options);

        var sequential = Enumerable.Range(0, spacecraft.Length).Select(Search).ToArray();
        var concurrent = new IReadOnlyList<Window>[spacecraft.Length * 2];
        Parallel.For(0, concurrent.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i => concurrent[i] = Search(i % spacecraft.Length));

        for (int i = 0; i < concurrent.Length; i++)
        {
            Assert.Equal(sequential[i % spacecraft.Length], concurrent[i]);
        }

        Assert.All(sequential, s => Assert.NotEmpty(s));
    }
}
