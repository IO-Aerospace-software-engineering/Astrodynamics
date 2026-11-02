using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The kernels a propagated spacecraft gives CSPICE (choice 9 (b) of #515): its clock, its frame, the frames and kernels of
/// its instruments, its SPK and its CK, and the SPKs of its spacecraft and site targets, written by the library before
/// any search (a search adds orientations to the spacecraft's frame, #523), then loaded and kept loaded.
/// </summary>
internal static class FieldOfViewKernels
{
    /// <summary>Writes and loads the kernels of <paramref name="spacecraft"/> and of its targets in <paramref name="directory"/>.</summary>
    internal static void WriteAndLoad(string directory, Spacecraft spacecraft, IEnumerable<Spacecraft> spacecraftTargets, IEnumerable<Site> siteTargets,
        Window siteWindow, Action<FileInfo> writeEphemeris = null, IEnumerable<StateOrientation> orientations = null)
    {
        // Relative paths, as the kernels of the other tests: the native writers take a bounded file name
        Directory.CreateDirectory(directory);
        FileInfo File(string name)
        {
            var file = new FileInfo(Path.Combine(directory, name));
            // Deleted by path: a FileInfo keeps the state it first read, and LoadKernels skips a kernel whose FileInfo read
            // it as missing
            System.IO.File.Delete(file.FullName);
            return new FileInfo(file.FullName);
        }

        string stem = spacecraft.Name.ToLowerInvariant();
        var clock = File(stem + ".tsc");
        spacecraft.Clock.WriteAsync(clock).GetAwaiter().GetResult();
        var frame = File(stem + ".tf");
        ((SpacecraftFrame)spacecraft.Frame).WriteAsync(frame).GetAwaiter().GetResult();
        var texts = new List<FileInfo> { clock, frame };
        foreach (var instrument in spacecraft.Instruments)
        {
            var instrumentFrame = File($"{stem}_{instrument.Name.ToLowerInvariant()}.tf");
            instrument.WriteFrameAsync(instrumentFrame).GetAwaiter().GetResult();
            var kernel = File($"{stem}_{instrument.Name.ToLowerInvariant()}.ti");
            instrument.WriteKernelAsync(kernel).GetAwaiter().GetResult();
            texts.Add(instrumentFrame);
            texts.Add(kernel);
        }

        foreach (var file in texts)
        {
            SpiceAPI.Instance.LoadKernels(file);
        }

        var spk = File(stem + ".bsp");
        (writeEphemeris ?? spacecraft.WriteEphemeris)(spk);
        SpiceAPI.Instance.LoadKernels(spk);
        var ck = File(stem + ".bc");
        if (orientations != null)
        {
            SpiceAPI.Instance.WriteOrientation(ck, spacecraft.NaifId, orientations.ToArray());
        }
        else
        {
            spacecraft.WriteOrientation(ck);
        }

        SpiceAPI.Instance.LoadKernels(ck);
        foreach (var target in spacecraftTargets)
        {
            var targetSpk = File(target.Name.ToLowerInvariant() + ".bsp");
            target.WriteEphemeris(targetSpk);
            SpiceAPI.Instance.LoadKernels(targetSpk);
        }

        foreach (var site in siteTargets)
        {
            var siteSpk = File(site.Name.ToLowerInvariant() + ".bsp");
            site.WriteEphemeris(siteWindow, siteSpk);
            SpiceAPI.Instance.LoadKernels(siteSpk);
        }
    }

    /// <summary>The largest difference between the edges of two lists of windows, in seconds; they must pair, both in TDB.</summary>
    internal static double WorstEdge(IReadOnlyList<Window> actual, IReadOnlyList<Window> expected)
    {
        Assert.Equal(expected.Count, actual.Count);
        double worst = 0.0;
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(TimeFrame.TDBFrame, actual[i].StartDate.Frame);
            Assert.Equal(TimeFrame.TDBFrame, actual[i].EndDate.Frame);
            worst = System.Math.Max(worst, System.Math.Abs((actual[i].StartDate - expected[i].StartDate).TotalSeconds));
            worst = System.Math.Max(worst, System.Math.Abs((actual[i].EndDate - expected[i].EndDate).TotalSeconds));
        }

        return worst;
    }

    internal static string Format(IEnumerable<Window> windows) =>
        string.Join(" ", windows.Select(x => $"[{x.StartDate.TimeSpanFromJ2000().TotalSeconds:F7}, {x.EndDate.TimeSpanFromJ2000().TotalSeconds:F7}]"));
}

/// <summary>
/// The trajectory of <c>InstrumentTests.FindWindowInFieldOfView</c> (a 6 800 km circular equatorial LEO from
/// 676555199.9846829 TDB, 6 448 s, Velocity-Verlet at 1 s, ICRF, constant attitude), propagated once for the class with
/// the 22 instruments of the design of #515 (§10), two crossed by the Lagrange point L1 and two by a star, and a spacecraft
/// target on a 7 000 km orbit, their kernels written by the library and loaded, and the same trajectories as kernel-backed
/// spacecraft read from those kernels. The star, at 1 pc without proper motion, has its SPK too (Star.WriteEphemeris), over
/// the light time before and after the window: gftfov reads it at its light-time corrected epoch, where it has not moved,
/// so that its windows with LT, CN and their "+S" and transmission forms are those of the direction the library reads,
/// with stelab's stellar aberration (choice 13).
/// </summary>
public sealed class PropagatedFieldOfViewCase
{
    internal const int ObserverId = -5151;
    internal const int TargetId = -5152;
    private const string KernelDirectory = "Data/User/FieldOfViewSearch515";

    public PropagatedFieldOfViewCase()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        Start = TimeSystem.Time.CreateUTC(676555130.80).ToTDB();
        End = Start.AddSeconds(6448.0);
        // The light time of a spacecraft target reads it before the search window: one second inside the propagation
        SearchWindow = new Window(Start.AddSeconds(1.0), End.AddSeconds(-1.0));
        Earth = new CelestialBody(399, Frames.Frame.ICRF, new TimeSystem.Time(2000, 1, 1, 12, 0, 0));
        Moon = new CelestialBody(301, Frames.Frame.ICRF, new TimeSystem.Time(2000, 1, 1, 12, 0, 0));
        Sun = new CelestialBody(10);
        Dss13 = new Site(13, "DSS-13", Earth);
        // A site given by its coordinates (the managed path; its SPK written by Site.WriteEphemeris for gftfov)
        CoordinateSite = new Site(151, "FOVSITE151", Earth, new Planetodetic(-116.8 * IO.Astrodynamics.Constants.Deg2Rad, 10.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0));
        Barycenter = new Barycenter(3);
        L1 = new LagrangePoint(LagrangePoints.L1);
        Star = new Star(515002, "FOVSTAR5152", 1.989e30, "G2V", 5.0, 1.0, new Equatorial(1.2, 0.3, Start), 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, Start);
        // A light time of 1.03e8 s
        Star.Propagate(new Window(Start.AddSeconds(-1.2e8), End.AddSeconds(1.2e8)), TimeSpan.FromSeconds(1.0e6));

        var scenario = new Scenario("FieldOfView515", new IO.Astrodynamics.Mission.Mission("fov515"), new Window(Start, End));
        scenario.AddCelestialItem(Moon);
        scenario.AddCelestialItem(Earth);
        scenario.AddCelestialItem(Sun);
        var orbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), Earth, Start, Frames.Frame.ICRF);
        Propagated = new Spacecraft(ObserverId, "FOV5151", 1000.0, 3000.0, new Clock("FOV5151_CLK", 65536), orbit);
        AddInstruments(Propagated);
        scenario.AddSpacecraft(Propagated);
        var targetOrbit = new StateVector(new Vector3(7000000.0 * System.Math.Cos(-0.6), 7000000.0 * System.Math.Sin(-0.6), 0.0),
            new Vector3(7546.0 * System.Math.Sin(0.6), 7546.0 * System.Math.Cos(-0.6), 0.0), Earth, Start, Frames.Frame.ICRF);
        PropagatedTarget = new Spacecraft(TargetId, "FOV5152", 1000.0, 3000.0, new Clock("FOV5152_CLK", 65536), targetOrbit);
        scenario.AddSpacecraft(PropagatedTarget);
        scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(1.0)).GetAwaiter().GetResult();

        FieldOfViewKernels.WriteAndLoad(KernelDirectory, Propagated, [PropagatedTarget], [CoordinateSite], new Window(Start.AddSeconds(-600.0), End.AddSeconds(600.0)));
        var starSpk = new FileInfo(Path.Combine(KernelDirectory, "fovstar515.bsp"));
        File.Delete(starSpk.FullName);
        Star.WriteEphemeris(new FileInfo(starSpk.FullName));
        SpiceAPI.Instance.LoadKernels(new FileInfo(starSpk.FullName));
        Kernel = new Spacecraft(ObserverId, "FOV5151", Start, 1000.0, 3000.0, new Clock("FOV5151_CLK", 65536));
        AddInstruments(Kernel);
        KernelTarget = new Spacecraft(TargetId, "FOV5152", Start, 1000.0, 3000.0, new Clock("FOV5152_CLK", 65536));
    }

    internal TimeSystem.Time Start { get; }
    internal TimeSystem.Time End { get; }
    internal Window SearchWindow { get; }
    internal CelestialBody Earth { get; }
    internal CelestialBody Moon { get; }
    internal CelestialBody Sun { get; }
    internal Site Dss13 { get; }
    internal Site CoordinateSite { get; }
    internal Barycenter Barycenter { get; }
    internal LagrangePoint L1 { get; }
    internal Star Star { get; }
    internal Spacecraft Propagated { get; }
    internal Spacecraft PropagatedTarget { get; }
    internal Spacecraft Kernel { get; }
    internal Spacecraft KernelTarget { get; }

    private static Vector3 N(double x, double y, double z) => new Vector3(x, y, z).Normalize();

    /// <summary>
    /// The 22 instruments of the design of #515 (§10), and those of L1 and of the star, numbered n as the spacecraft
    /// identifier times 1000 minus n.
    /// </summary>
    internal static void AddInstruments(Spacecraft spacecraft)
    {
        var m3 = N(0.28117852, 0.87853543, 0.386153); // the apparent Moon at S + 3000 s (LT)
        var s3 = N(0.18922624, 0.90092672, 0.39054383); // the apparent Sun at S + 3000 s (LT)
        var sS = N(0.18971202, 0.90083906, 0.39051035);
        var sE = N(0.18851398, 0.90104487, 0.3906157);
        var e = sE - sS;
        e = (e - s3 * (e * s3)).Normalize(); // the drift of the Sun
        var sEdge = (s3 + e * 0.0048).Normalize(); // the Sun's limb reaches the field near S + 3000 s
        double ten = 10.0 * IO.Astrodynamics.Constants.Deg2Rad;
        var g = new Vector3(System.Math.Sin(ten), 0.0, System.Math.Cos(ten));
        var gRef = new Vector3(System.Math.Cos(ten), 0.0, -System.Math.Sin(ten));
        var y = new Vector3(0.0, IO.Astrodynamics.Constants.PI2, 0.0);
        var zero = Vector3.Zero;
        var mX = new Vector3(-1.0, 0.0, 0.0);
        var turned = new Vector3(0.0, System.Math.Cos(System.Math.PI / 6.0), System.Math.Sin(System.Math.PI / 6.0));
        int Id(int n) => spacecraft.NaifId * 1000 - n;
        // The Earth, the boresight along -X of ICRF, as InstrumentTests.FindWindowInFieldOfView
        spacecraft.AddCircularInstrument(Id(789), "CAMERA789", "fov515", 0.75, Vector3.VectorZ, Vector3.VectorY, y);
        spacecraft.AddEllipticalInstrument(Id(790), "ELL790", "fov515", 0.75, 0.3, Vector3.VectorZ, Vector3.VectorY, y);
        spacecraft.AddRectangularInstrument(Id(791), "REC791", "fov515", 0.75, 0.3, Vector3.VectorZ, Vector3.VectorY, y);
        spacecraft.AddEllipticalInstrument(Id(793), "ELL793", "fov515", 0.3, 0.75, Vector3.VectorZ, Vector3.VectorY, y);
        spacecraft.AddRectangularInstrument(Id(794), "REC794", "fov515", 0.3, 0.75, Vector3.VectorZ, Vector3.VectorY, y);
        spacecraft.AddCircularInstrument(Id(795), "CIR795", "fov515", 0.75, N(-System.Math.Cos(0.4), 0.0, System.Math.Sin(0.4)), Vector3.VectorZ, zero);
        spacecraft.AddRectangularInstrument(Id(796), "REC796", "fov515", 0.75, 0.3, mX, turned, zero);
        spacecraft.AddEllipticalInstrument(Id(797), "ELL797", "fov515", 0.75, 0.3, mX, new Vector3(0.5, 1.0, 0.2), zero);
        spacecraft.AddCircularInstrument(Id(798), "CIR798", "fov515", 0.05, mX, Vector3.VectorY, zero);
        spacecraft.AddEllipticalInstrument(Id(799), "ELL799", "fov515", 0.05, 0.02, mX, Vector3.VectorY, zero);
        spacecraft.AddRectangularInstrument(Id(800), "REC800", "fov515", 0.05, 0.02, mX, turned, zero);
        spacecraft.AddCircularInstrument(Id(801), "CIR801", "fov515", 0.18038, g, gRef, zero);
        spacecraft.AddRectangularInstrument(Id(802), "REC802", "fov515", 0.18038, 0.1, g, gRef, zero);
        // The Moon, the boresight on its apparent direction at S + 3000 s
        spacecraft.AddCircularInstrument(Id(810), "CIR810", "fov515", 0.008, m3, Vector3.VectorZ, zero);
        spacecraft.AddEllipticalInstrument(Id(811), "ELL811", "fov515", 0.012, 0.004, m3, Vector3.VectorZ, zero);
        spacecraft.AddRectangularInstrument(Id(812), "REC812", "fov515", 0.012, 0.004, m3, new Vector3(0.3, 0.0, 1.0), zero);
        spacecraft.AddCircularInstrument(Id(813), "CIR813", "fov515", 0.002, m3, Vector3.VectorZ, zero);
        spacecraft.AddCircularInstrument(Id(814), "CIR814", "fov515", 0.05, m3, Vector3.VectorZ, zero);
        // The Sun
        spacecraft.AddCircularInstrument(Id(820), "CIR820", "fov515", 0.0002, s3, Vector3.VectorZ, zero);
        spacecraft.AddCircularInstrument(Id(821), "CIR821", "fov515", 0.0002, sEdge, Vector3.VectorZ, zero);
        spacecraft.AddEllipticalInstrument(Id(822), "ELL822", "fov515", 0.0003, 0.0001, sEdge, e, zero);
        spacecraft.AddRectangularInstrument(Id(823), "REC823", "fov515", 0.0003, 0.0001, sEdge, e + Vector3.VectorZ * 0.5, zero);
        // L1, the boresight on its apparent direction at S + 3000 s (LT, spkpos): the parallax of the orbit moves it by up
        // to 8.4e-3 rad, in and out of the fields
        var l1 = N(0.19333405, 0.900347, 0.38986822);
        spacecraft.AddCircularInstrument(Id(824), "CIR824", "fov515", 0.004, l1, Vector3.VectorZ, zero);
        spacecraft.AddRectangularInstrument(Id(825), "REC825", "fov515", 0.004, 0.002, l1, Vector3.VectorZ, zero);
        // The star: the stellar aberration of the orbital velocity moves its apparent direction by about 2.5e-5 rad about
        // its mean (stelab, spiceypy, over the window); a circle 2.5e-5 rad from the mean "S" direction, and a rectangle
        // 2e-5 rad from the mean "XS" direction (stlabx), each crossed twice; the geometric direction stays outside both
        spacecraft.AddCircularInstrument(Id(830), "CIR830", "fov515", 2.5e-5, N(0.3462671531, 0.1070470016, 0.9320085827), Vector3.VectorZ, zero);
        spacecraft.AddRectangularInstrument(Id(831), "REC831", "fov515", 2.5e-5, 1.5e-5, N(0.3460816294, 0.107124567, 0.9320685774), Vector3.VectorZ, zero);
    }

    internal Instrument PropagatedInstrument(int number) => Propagated.Instruments.First(x => x.NaifId == ObserverId * 1000 - number);

    internal Instrument KernelInstrument(int number) => Kernel.Instruments.First(x => x.NaifId == ObserverId * 1000 - number);

    /// <summary>
    /// A target by its NAIF identifier in the case keys: 399, 301, 10, 399013, 399151, 3, 391, the star or -5152; the
    /// spacecraft propagated or kernel-backed.
    /// </summary>
    internal ILocalizable Target(string name, bool kernel) => name switch
    {
        "399" => Earth,
        "301" => Moon,
        "10" => Sun,
        "399013" => Dss13,
        "399151" => CoordinateSite,
        "3" => Barycenter,
        "391" => L1,
        "-5152" => kernel ? KernelTarget : PropagatedTarget,
        _ when name == Star.NaifId.ToString() => Star,
        _ => throw new ArgumentException(name)
    };

    internal static Frames.Frame TargetFrame(string name) => name switch
    {
        "399" => new Frames.Frame("ITRF93"),
        "301" => new Frames.Frame("MOON_ME"),
        "10" => new Frames.Frame("IAU_SUN"),
        _ => Frames.Frame.ICRF
    };

    /// <summary>gftfov in the test process at its default tolerance, 1e-6 s, on the kernels written by the library.</summary>
    internal Window[] Gftfov(int number, string target, ShapeType shape, Aberration aberration, TimeSpan step, Window? window = null)
    {
        var frame = shape == ShapeType.Point ? Frames.Frame.ICRF : TargetFrame(target);
        int targetId = Target(target, true).NaifId;
        return SpiceAPI.Instance.FindWindowsInFieldOfViewConstraint(window ?? SearchWindow, ObserverId, ObserverId * 1000 - number, targetId, frame, shape, aberration, step)
            .ToArray();
    }
}

/// <summary>
/// The field-of-view search of a propagated spacecraft (#515) against CSPICE's <c>gftfov</c> run in the test process on the
/// kernels the library writes from the same propagation (choice 9 (b)): the public managed path of the propagated
/// spacecraft, the internal managed search of the same trajectory as a kernel-backed spacecraft, and the public SPICE path
/// of that one; every field of the design's grid as a point and as an ellipsoid, the Earth, the Moon, the Sun, DSS-13, a
/// site given by its coordinates, a spacecraft target, the Earth-Moon barycenter, L1 and a star; the grazing and slow edges, the events shorter than the step,
/// the edges of the search window, the time frame of the windows, the argument errors of both paths, the regressions of
/// #518 to #521, and concurrent searches.
/// </summary>
/// <remarks>
/// <c>gftfov</c> converges to 1e-6 s, as the managed search: its bisection is in the limits, as <c>GfocltThreshold</c>
/// in the occultation tests (#495). The managed search is at a step of 60 s; <c>gftfov</c> at 60 s, or 1 s for the fields
/// whose events are shorter than 60 s (801 and 802), as it requires.
/// </remarks>
public class FieldOfViewSearchTests : IClassFixture<PropagatedFieldOfViewCase>
{
    // PROVISIONAL limits, twice the worst value over the rows on Linux x64 (2026-11-01), rounded up at the second digit,
    // until the three platforms of the CI have printed their MEASURE lines (#515, choice 9 (b)): gftfov at 1e-6 s in the
    // process against the managed search at 1 µs, off the grazing and slow edges: 1.3e-6 s on the 221 rows, propagated,
    // kernel-backed and between the two public paths alike (789 ELLIPSOID XCN+S); the times are ticks, 100 ns
    private const double PropagatedLimit = 2.6e-6;
    private const double KernelLimit = 2.6e-6;
    private const double PublicPathsLimit = 2.6e-6;

    // PROVISIONAL, a kernel-backed observer with a propagated spacecraft target: 5e-7 s on its 9 rows (Linux x64, 2026-11-01)
    private const double KernelObserverLimit = 1.0e-6;

    // The grazing fields 801 and 802 and the slow edges of 811 POINT LT+S (choice 10): the slope of their margin is
    // 1.2e-6 rad/s (801), 3.1e-5 rad/s (802's gaps) and 3.7e-6 rad/s (811), where the 0.5 to 4 mm by which the apparent
    // Earth of the library and that of gftfov round their light-time corrected epochs to a double ET (half its 119 ns ulp,
    // the Earth's barycentric motion) move the edge by 1e-6 to 1e-3 s; traced in the log of #515, the library's geometry on
    // gftfov's positions within 2.5e-6 s of gftfov's edges, its axes equal to SPICE's. Each row has its own limits, twice
    // its measured edge, propagated and kernel-backed (PROVISIONAL, Linux x64, 2026-11-01)

    private static readonly TimeSpan Step = TimeSpan.FromSeconds(60.0);
    private static readonly TimeSpan ShortStep = TimeSpan.FromSeconds(1.0);

    private readonly PropagatedFieldOfViewCase _case;
    private readonly ITestOutputHelper _output;

    // The kernels are loaded once, by the fixture: the reload of the solar system kernels by each row would take most of its time
    public FieldOfViewSearchTests(PropagatedFieldOfViewCase fieldOfViewCase, ITestOutputHelper output)
    {
        _case = fieldOfViewCase;
        _output = output;
    }

    private static readonly int[] EarthFields = [789, 790, 791, 793, 794, 795, 796, 797, 798, 799, 800];
    private static readonly int[] MoonFields = [810, 811, 812, 813, 814];
    private static readonly int[] SunFields = [820, 821, 822, 823];
    private static readonly string[] Three = ["NONE", "LT", "LT+S"];

    /// <summary>The design's grid (§10 of #515), the grazing and slow cases apart: "&lt;n&gt; &lt;target&gt; &lt;POINT|ELLIPSOID&gt; &lt;abcorr&gt;".</summary>
    private static IEnumerable<string> Grid()
    {
        foreach (var (fields, target) in new[] { (EarthFields, "399"), (MoonFields, "301"), (SunFields, "10") })
        {
            foreach (int n in fields)
            {
                foreach (var shape in new[] { "POINT", "ELLIPSOID" })
                {
                    foreach (var aberration in Three)
                    {
                        if (!(n == 811 && shape == "POINT" && aberration == "LT+S"))
                        {
                            yield return $"{n} {target} {shape} {aberration}";
                        }
                    }
                }
            }
        }

        foreach (var (n, target) in new[] { (789, "399"), (821, "10") })
        {
            foreach (var shape in new[] { "POINT", "ELLIPSOID" })
            {
                foreach (var aberration in new[] { "CN", "CN+S", "XLT", "XLT+S", "XCN", "XCN+S" })
                {
                    yield return $"{n} {target} {shape} {aberration}";
                }
            }
        }

        foreach (int n in new[] { 789, 790, 791, 795, 796, 797 })
        {
            foreach (var target in new[] { "399013", "399151", "-5152" })
            {
                foreach (var aberration in Three)
                {
                    yield return $"{n} {target} POINT {aberration}";
                }
            }
        }

        // The Earth-Moon barycenter, in the fields it crosses
        foreach (int n in new[] { 789, 795, 796 })
        {
            foreach (var aberration in Three)
            {
                yield return $"{n} 3 POINT {aberration}";
            }
        }

        foreach (int n in new[] { 824, 825 })
        {
            foreach (var aberration in Three)
            {
                yield return $"{n} 391 POINT {aberration}";
            }
        }

        // The star: "+S" moves it across the fields, the geometric direction stays outside (gftfov on its SPK, choice 13)
        foreach (var aberration in new[] { "NONE", "LT", "LT+S", "CN+S", "XLT+S" })
        {
            yield return $"830 {StarKey} POINT {aberration}";
        }

        foreach (var aberration in new[] { "NONE", "LT+S", "XLT+S", "XCN+S" })
        {
            yield return $"831 {StarKey} POINT {aberration}";
        }
    }

    // The NAIF identifier of the fixture's star
    private const string StarKey = "1000515002";

    // The uncategorized rows: every field shape against both target shapes, each kind of target, each aberration, and the
    // worst row of each limit (Linux, 2026-11-01)
    private static readonly string[] WorstCases =
    [
        "789 399 POINT LT", "789 399 ELLIPSOID LT", "790 399 ELLIPSOID LT", "791 399 POINT LT+S", "791 399 ELLIPSOID NONE",
        "796 399 ELLIPSOID LT", "797 399 POINT LT", "797 399 ELLIPSOID LT+S", "798 399 ELLIPSOID LT", "800 399 POINT LT",
        "811 301 ELLIPSOID LT+S", "812 301 POINT LT", "813 301 ELLIPSOID LT", "814 301 POINT NONE", "821 10 ELLIPSOID LT+S",
        "822 10 POINT LT", "823 10 ELLIPSOID LT", "789 399 ELLIPSOID CN", "789 399 POINT CN+S", "821 10 POINT XLT",
        "789 399 ELLIPSOID XLT+S", "821 10 ELLIPSOID XCN", "789 399 POINT XCN+S", "789 399 ELLIPSOID XCN+S", "813 301 POINT LT+S",
        "797 399 ELLIPSOID LT", "789 399013 POINT LT", "797 399151 POINT LT+S",
        "796 -5152 POINT LT", "789 3 POINT LT", "824 391 POINT LT+S", "825 391 POINT LT", "830 " + StarKey + " POINT LT+S",
        "831 " + StarKey + " POINT XLT+S", "830 " + StarKey + " POINT NONE"
    ];

    public static IEnumerable<object[]> WorstRows() => WorstCases.Select(x => new object[] { x });

    public static IEnumerable<object[]> OtherRows() => Grid().Except(WorstCases).Select(x => new object[] { x }).ToArray();

    private static (int number, string target, ShapeType shape, Aberration aberration) Parse(string key)
    {
        var parts = key.Split(' ');
        return (int.Parse(parts[0]), parts[1], parts[2] == "POINT" ? ShapeType.Point : ShapeType.Ellipsoid, FieldOfViewReferenceTests.ParseAberration(parts[3]));
    }

    private (double managed, double kernel, double paths, int count) Compare(string key, TimeSpan spiceStep)
    {
        var (number, target, shape, aberration) = Parse(key);
        var frame = PropagatedFieldOfViewCase.TargetFrame(target);
        var expected = _case.Gftfov(number, target, shape, aberration, spiceStep);

        // The public path of the propagated spacecraft: the managed search
        var instrument = _case.PropagatedInstrument(number);
        var managed = instrument.FindWindowsInFieldOfViewConstraint(_case.SearchWindow, _case.Propagated, _case.Target(target, false), frame, shape, aberration, Step).ToArray();

        // The same trajectory read from its kernels: the managed search, and the public path (SPICE for a kernel target)
        var kernelInstrument = _case.KernelInstrument(number);
        var kernelTarget = _case.Target(target, true);
        var kernel = FieldOfViewSearch.Managed(kernelInstrument, _case.SearchWindow, kernelTarget, shape, frame, aberration, Step);
        var kernelPublic = kernelInstrument.FindWindowsInFieldOfViewConstraint(_case.SearchWindow, _case.Kernel, kernelTarget, frame, shape, aberration, spiceStep).ToArray();

        double worstManaged = FieldOfViewKernels.WorstEdge(managed, expected);
        double worstKernel = FieldOfViewKernels.WorstEdge(kernel, expected);
        double worstPaths = FieldOfViewKernels.WorstEdge(managed, kernelPublic);
        _output.WriteLine($"MEASURE #515 propagated {key}, {expected.Length} windows: |managed propagated - gftfov (1e-6 s, step {spiceStep.TotalSeconds} s)| {worstManaged:E2} s, " +
                          $"|managed kernel-backed - gftfov| {worstKernel:E2} s, |public managed (propagated) - public SPICE path (kernel-backed)| {worstPaths:E2} s");
        _output.WriteLine($"  gftfov {FieldOfViewKernels.Format(expected)}; managed {FieldOfViewKernels.Format(managed)}");
        return (worstManaged, worstKernel, worstPaths, expected.Length);
    }

    /// <summary>The comparison of a row, its edges within the limits; returns the number of windows of gftfov.</summary>
    private int AssertWithin(string key, double propagatedLimit, double kernelLimit, double pathsLimit, TimeSpan spiceStep)
    {
        var (managed, kernel, paths, count) = Compare(key, spiceStep);
        Assert.True(managed < propagatedLimit, $"propagated {managed:E2} s");
        Assert.True(kernel < kernelLimit, $"kernel-backed {kernel:E2} s");
        Assert.True(paths < pathsLimit, $"public paths {paths:E2} s");
        return count;
    }

    [Theory]
    [MemberData(nameof(WorstRows))]
    public void Propagated_MatchesGftfov(string key)
    {
        AssertWithin(key, PropagatedLimit, KernelLimit, PublicPathsLimit, Step);
    }

    [Theory]
    [Trait("Category", "Validation")]
    [MemberData(nameof(OtherRows))]
    public void Propagated_MatchesGftfov_Grid(string key)
    {
        AssertWithin(key, PropagatedLimit, KernelLimit, PublicPathsLimit, Step);
    }

    /// <summary>
    /// The grazing fields (801 grazes the Earth's limb, 802's side grazes it) against gftfov at a step of 1 s, which their
    /// events of 10.8 s and gaps of 24.8 s and 3.2 s require, and the slow edges of 811 POINT LT+S (a gap that exists by
    /// 2 % of the field): their own limits (choice 10), twice the measured edge of each row, propagated and kernel-backed
    /// (PROVISIONAL, Linux x64, 2026-11-01), those at the level of the other rows under their limits; their window counts
    /// exact. Measured, propagated / kernel-backed: 801 ELLIPSOID LT 2.17e-4 / 2.52e-4 s; 802 ELLIPSOID NONE 3e-7 /
    /// 4e-7 s, LT 2.2e-6 / 3.0e-6 s, LT+S 4.16e-5 / 1.75e-4 s; 811 POINT LT+S 1.6e-6 / 4.4e-6 s; the other rows have no
    /// window. The two public paths differ as the propagated spacecraft does from gftfov, which the SPICE path is.
    /// </summary>
    [Theory]
    [InlineData("801 399 POINT NONE", 0, PropagatedLimit, KernelLimit)]
    [InlineData("801 399 ELLIPSOID NONE", 0, PropagatedLimit, KernelLimit)]
    [InlineData("801 399 ELLIPSOID LT", 1, 4.4e-4, 5.1e-4)]
    [InlineData("801 399 ELLIPSOID LT+S", 0, PropagatedLimit, KernelLimit)]
    [InlineData("802 399 ELLIPSOID NONE", 2, PropagatedLimit, KernelLimit)]
    [InlineData("802 399 ELLIPSOID LT", 1, 4.4e-6, 6.0e-6)]
    [InlineData("802 399 ELLIPSOID LT+S", 2, 8.4e-5, 3.5e-4)]
    [InlineData("802 399 POINT LT", 0, PropagatedLimit, KernelLimit)]
    public void Grazing_MatchesGftfov(string key, int count, double propagatedLimit, double kernelLimit)
    {
        Assert.Equal(count, AssertWithin(key, propagatedLimit, kernelLimit, propagatedLimit, ShortStep));
    }

    [Fact]
    public void SlowEdges_MatchGftfov()
    {
        Assert.Equal(2, AssertWithin("811 301 POINT LT+S", 3.2e-6, 8.8e-6, 3.2e-6, Step));
    }

    /// <summary>
    /// Events shorter than the step (choice 12), on purpose: at 60 s the managed search finds 801's window of 10.8 s and
    /// 802's gap of 24.8 s, which gftfov finds at 1 s only; the two public paths differ with this step, as documented. The
    /// limits are those of the rows of <see cref="Grazing_MatchesGftfov"/>.
    /// </summary>
    [Fact]
    public void ShortEvents_FoundByTheManagedSearch_MissedByGftfovAtTheSameStep()
    {
        var earth = _case.Earth;
        var itrf = PropagatedFieldOfViewCase.TargetFrame("399");
        var grazing = _case.PropagatedInstrument(801).FindWindowsInFieldOfViewConstraint(_case.SearchWindow, _case.Propagated, earth, itrf, ShapeType.Ellipsoid, Aberration.LT, Step)
            .ToArray();
        Assert.Empty(_case.Gftfov(801, "399", ShapeType.Ellipsoid, Aberration.LT, Step));
        var fine = _case.Gftfov(801, "399", ShapeType.Ellipsoid, Aberration.LT, ShortStep);
        Assert.Single(fine);
        Assert.InRange((fine[0].EndDate - fine[0].StartDate).TotalSeconds, 10.0, 11.0);
        Assert.True(FieldOfViewKernels.WorstEdge(grazing, fine) < 4.4e-4);

        var gap = _case.PropagatedInstrument(802).FindWindowsInFieldOfViewConstraint(_case.SearchWindow, _case.Propagated, earth, itrf, ShapeType.Ellipsoid, Aberration.None, Step)
            .ToArray();
        Assert.Single(_case.Gftfov(802, "399", ShapeType.Ellipsoid, Aberration.None, Step));
        var fineGap = _case.Gftfov(802, "399", ShapeType.Ellipsoid, Aberration.None, ShortStep);
        Assert.Equal(2, fineGap.Length);
        Assert.InRange((fineGap[1].StartDate - fineGap[0].EndDate).TotalSeconds, 24.0, 26.0);
        Assert.True(FieldOfViewKernels.WorstEdge(gap, fineGap) < PropagatedLimit);
    }

    /// <summary>
    /// A kernel-backed observer with a propagated spacecraft target: the public method takes the managed path (the target
    /// is not in the kernels), against gftfov on the target's SPK.
    /// </summary>
    [Theory]
    [InlineData(789)]
    [InlineData(796)]
    [InlineData(797)]
    public void KernelObserver_PropagatedTarget_MatchesGftfov(int number)
    {
        foreach (var aberration in new[] { Aberration.None, Aberration.LT, Aberration.LTS })
        {
            var actual = _case.KernelInstrument(number).FindWindowsInFieldOfViewConstraint(_case.SearchWindow, _case.Kernel, _case.PropagatedTarget, Frames.Frame.ICRF,
                ShapeType.Point, aberration, Step).ToArray();
            var expected = _case.Gftfov(number, "-5152", ShapeType.Point, aberration, Step);
            double worst = FieldOfViewKernels.WorstEdge(actual, expected);
            _output.WriteLine($"MEASURE #515 kernel observer, propagated target {number} {aberration}, {expected.Length} windows: |managed - gftfov (1e-6 s)| {worst:E2} s");
            Assert.NotEmpty(expected);
            Assert.True(worst < KernelObserverLimit, $"{worst:E2} s");
        }
    }

    /// <summary>
    /// The edges of the search window: a field in view at the start starts there (the managed path looked one step before
    /// it, #461 for this path, and its bisection returned the earlier end of a 1 s bracket, #521), and one in view at the
    /// end ends there; a search window inside a window is returned whole, on both paths.
    /// </summary>
    [Fact]
    public void WindowEdges_StartAndEndAtTheSearchWindow()
    {
        var instrument = _case.PropagatedInstrument(789);
        var earth = _case.Earth;
        var itrf = PropagatedFieldOfViewCase.TargetFrame("399");
        var point = instrument.FindWindowsInFieldOfViewConstraint(_case.SearchWindow, _case.Propagated, earth, itrf, ShapeType.Point, Aberration.LT, Step).ToArray();
        Assert.Equal(_case.SearchWindow.StartDate, point[0].StartDate);
        var ellipsoid = instrument.FindWindowsInFieldOfViewConstraint(_case.SearchWindow, _case.Propagated, earth, itrf, ShapeType.Ellipsoid, Aberration.LT, Step).ToArray();
        Assert.Equal(2, ellipsoid.Length);
        Assert.Equal(_case.SearchWindow.StartDate, ellipsoid[0].StartDate);
        Assert.Equal(_case.SearchWindow.EndDate, ellipsoid[1].EndDate);

        var inside = new Window(_case.Start.AddSeconds(100.0), _case.Start.AddSeconds(200.0));
        var managed = instrument.FindWindowsInFieldOfViewConstraint(inside, _case.Propagated, earth, itrf, ShapeType.Ellipsoid, Aberration.LT, Step).ToArray();
        var spice = _case.KernelInstrument(789).FindWindowsInFieldOfViewConstraint(inside, _case.Kernel, earth, itrf, ShapeType.Ellipsoid, Aberration.LT, Step).ToArray();
        Assert.Single(managed);
        Assert.Equal(inside.StartDate, managed[0].StartDate);
        Assert.Equal(inside.EndDate, managed[0].EndDate);
        Assert.True(FieldOfViewKernels.WorstEdge(managed, spice) < PublicPathsLimit);
    }

    /// <summary>A search window tagged UTC: TDB windows on both paths, equal to those of the TDB search window.</summary>
    [Fact]
    public void UtcSearchWindow_ReturnsTdbWindowsOnBothPaths()
    {
        var utc = new Window(_case.SearchWindow.StartDate.ToUTC(), _case.SearchWindow.EndDate.ToUTC());
        var earth = _case.Earth;
        var itrf = PropagatedFieldOfViewCase.TargetFrame("399");
        var managedUtc = _case.PropagatedInstrument(790).FindWindowsInFieldOfViewConstraint(utc, _case.Propagated, earth, itrf, ShapeType.Ellipsoid, Aberration.LT, Step)
            .ToArray();
        var managedTdb = _case.PropagatedInstrument(790).FindWindowsInFieldOfViewConstraint(_case.SearchWindow, _case.Propagated, earth, itrf, ShapeType.Ellipsoid,
            Aberration.LT, Step).ToArray();
        var spiceUtc = _case.KernelInstrument(790).FindWindowsInFieldOfViewConstraint(utc, _case.Kernel, earth, itrf, ShapeType.Ellipsoid, Aberration.LT, Step).ToArray();
        Assert.Equal(0.0, FieldOfViewKernels.WorstEdge(managedUtc, managedTdb));
        Assert.True(FieldOfViewKernels.WorstEdge(managedUtc, spiceUtc) < PublicPathsLimit);
    }

    /// <summary>
    /// #518: the ellipse's half-angles were swapped against the IK (FOV_REF_ANGLE applied across the reference plane), up
    /// to 400 s at every edge. 790 and 793 swap REF and CROSS: each equals gftfov, and they differ by hundreds of seconds.
    /// </summary>
    [Fact]
    public void Ellipse_RefAngleInTheReferencePlane_518()
    {
        var earth = _case.Earth;
        var e790 = _case.PropagatedInstrument(790).FindWindowsInFieldOfViewConstraint(_case.SearchWindow, _case.Propagated, earth, Frames.Frame.ICRF, ShapeType.Point,
            Aberration.LT, Step).ToArray();
        var e793 = _case.PropagatedInstrument(793).FindWindowsInFieldOfViewConstraint(_case.SearchWindow, _case.Propagated, earth, Frames.Frame.ICRF, ShapeType.Point,
            Aberration.LT, Step).ToArray();
        Assert.True(FieldOfViewKernels.WorstEdge(e790, _case.Gftfov(790, "399", ShapeType.Point, Aberration.LT, Step)) < PropagatedLimit);
        Assert.True(FieldOfViewKernels.WorstEdge(e793, _case.Gftfov(793, "399", ShapeType.Point, Aberration.LT, Step)) < PropagatedLimit);
        Assert.Equal(e790.Length, e793.Length);
        Assert.True(System.Math.Abs((e790[0].EndDate - e793[0].EndDate).TotalSeconds) > 300.0);
    }

    /// <summary>
    /// #519: the rectangle was tested on the ICRF components of the direction, and found no window where gftfov finds two;
    /// the rectangles 791 (along the reference vector) and 796 (turned 30° about the boresight) now equal gftfov.
    /// </summary>
    [Fact]
    public void Rectangle_OnTheInstrumentAxes_519()
    {
        foreach (int n in new[] { 791, 796 })
        {
            var actual = _case.PropagatedInstrument(n).FindWindowsInFieldOfViewConstraint(_case.SearchWindow, _case.Propagated, _case.Earth, Frames.Frame.ICRF,
                ShapeType.Point, Aberration.LT, Step).ToArray();
            var expected = _case.Gftfov(n, "399", ShapeType.Point, Aberration.LT, Step);
            Assert.Equal(2, expected.Length);
            Assert.True(FieldOfViewKernels.WorstEdge(actual, expected) < PropagatedLimit);
        }
    }

    /// <summary>
    /// #520: the reference vector was not made perpendicular to the boresight (193 s on the Earth and 198 s on the spacecraft
    /// target with the half-angles of #518 corrected, 756 s with them); the ellipse 797, whose reference vector is 63° from
    /// its boresight, now equals gftfov on the Earth and on the spacecraft target.
    /// </summary>
    [Fact]
    public void ReferenceVector_MadePerpendicular_520()
    {
        foreach (var target in new[] { "399", "-5152" })
        {
            var actual = _case.PropagatedInstrument(797).FindWindowsInFieldOfViewConstraint(_case.SearchWindow, _case.Propagated, _case.Target(target, false),
                Frames.Frame.ICRF, ShapeType.Point, Aberration.LT, Step).ToArray();
            var expected = _case.Gftfov(797, target, ShapeType.Point, Aberration.LT, Step);
            Assert.NotEmpty(expected);
            Assert.True(FieldOfViewKernels.WorstEdge(actual, expected) < PropagatedLimit);
        }
    }

    /// <summary>
    /// #521: the edges were bisected to 1 s, each up to 1 s before gftfov's, and a window open at the start was looked
    /// for one step before it. The edges of the circle now equal gftfov's within the limit, the first start included.
    /// </summary>
    [Fact]
    public void Edges_ToTheMicrosecond_521()
    {
        var actual = _case.PropagatedInstrument(789).FindWindowsInFieldOfViewConstraint(_case.SearchWindow, _case.Propagated, _case.Earth, Frames.Frame.ICRF,
            ShapeType.Point, Aberration.LT, TimeSpan.FromSeconds(360.0)).ToArray();
        var expected = _case.Gftfov(789, "399", ShapeType.Point, Aberration.LT, TimeSpan.FromSeconds(360.0));
        Assert.Equal(2, expected.Length);
        Assert.Equal(expected[0].StartDate, actual[0].StartDate);
        Assert.True(FieldOfViewKernels.WorstEdge(actual, expected) < PropagatedLimit);
    }

    /// <summary>An instrument of a user's subclass, which the library cannot read a cross angle from.</summary>
    private sealed class UserInstrument : Instrument
    {
        public UserInstrument(Spacecraft spacecraft, InstrumentShape shape) : base(spacecraft, spacecraft.NaifId * 1000 - 999, "USER999", "fov515", 0.3, shape,
            Vector3.VectorZ, Vector3.VectorY, Vector3.Zero)
        {
        }

        public override Task WriteKernelAsync(FileInfo outputFile) => Task.CompletedTask;
    }

    /// <summary>The argument errors of choices 7 and 14, the same exception on both paths for the same call.</summary>
    [Fact]
    public void Errors_SameExceptionOnBothPaths()
    {
        var earth = _case.Earth;
        var window = _case.SearchWindow;
        var star = new Star(515001, "FOVSTAR515", 1.989e30, "G2V", 5.0, 1.0e-6, new Equatorial(0.3, 1.2, _case.Start), 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, _case.Start);

        // A propagated spacecraft (the managed path) and the same trajectory read from its kernels (the SPICE path with a
        // kernel target): a step not positive, the instrument's spacecraft as the target, an ellipsoid without radii
        foreach (var (spacecraft, other) in new[] { (_case.Propagated, _case.PropagatedTarget), (_case.Kernel, _case.KernelTarget) })
        {
            var instrument = spacecraft.Instruments.First();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                instrument.FindWindowsInFieldOfViewConstraint(window, spacecraft, earth, Frames.Frame.ICRF, ShapeType.Point, Aberration.LT, TimeSpan.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                instrument.FindWindowsInFieldOfViewConstraint(window, spacecraft, earth, Frames.Frame.ICRF, ShapeType.Point, Aberration.LT, TimeSpan.FromSeconds(-60.0)));
            Assert.Throws<ArgumentException>(() =>
                instrument.FindWindowsInFieldOfViewConstraint(window, spacecraft, spacecraft, Frames.Frame.ICRF, ShapeType.Point, Aberration.LT, Step));
            // A search window that ends before it starts, which the constructor from a length allows
            Assert.Throws<ArgumentException>(() => instrument.FindWindowsInFieldOfViewConstraint(new Window(window.EndDate, TimeSpan.FromSeconds(-60.0)), spacecraft, earth,
                Frames.Frame.ICRF, ShapeType.Point, Aberration.LT, Step));
            foreach (ILocalizable target in new ILocalizable[] { _case.Dss13, _case.CoordinateSite, other, new Barycenter(3), new LagrangePoint(LagrangePoints.L1), star })
            {
                Assert.Throws<ArgumentException>(() =>
                    instrument.FindWindowsInFieldOfViewConstraint(window, spacecraft, target, Frames.Frame.ICRF, ShapeType.Ellipsoid, Aberration.LT, Step));
            }

            Assert.Throws<ArgumentNullException>(() =>
                instrument.FindWindowsInFieldOfViewConstraint(window, null, earth, Frames.Frame.ICRF, ShapeType.Point, Aberration.LT, Step));
            Assert.Throws<ArgumentNullException>(() =>
                instrument.FindWindowsInFieldOfViewConstraint(window, spacecraft, null, Frames.Frame.ICRF, ShapeType.Point, Aberration.LT, Step));
            Assert.Throws<ArgumentNullException>(() =>
                instrument.FindWindowsInFieldOfViewConstraint(window, spacecraft, earth, null, ShapeType.Point, Aberration.LT, Step));
        }

        // A field CSPICE rejects, on an unpropagated spacecraft (the managed path) and on a kernel-backed one (the SPICE
        // path; the checks come first, so the instruments need no kernel)
        var unpropagated = new Spacecraft(-5153, "FOV5153", 1000.0, 3000.0, new Clock("FOV5153_CLK", 65536),
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, _case.Start, Frames.Frame.ICRF));
        var kernel = new Spacecraft(PropagatedFieldOfViewCase.ObserverId, "FOV5151", _case.Start, 1000.0, 3000.0, new Clock("FOV5151_CLK", 65536));
        foreach (var spacecraft in new[] { unpropagated, kernel })
        {
            int id = spacecraft.NaifId * 1000;
            spacecraft.AddCircularInstrument(id - 901, "ZERO", "fov515", 0.3, Vector3.Zero, Vector3.VectorY, Vector3.Zero);
            spacecraft.AddCircularInstrument(id - 902, "PARALLEL", "fov515", 0.3, Vector3.VectorZ, Vector3.VectorZ * 2.0, Vector3.Zero);
            spacecraft.AddEllipticalInstrument(id - 903, "NOCROSSE", "fov515", 0.3, 0.0, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);
            spacecraft.AddRectangularInstrument(id - 904, "NOCROSSR", "fov515", 0.3, 0.0, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);
            spacecraft.AddCircularInstrument(id - 905, "WIDEC", "fov515", IO.Astrodynamics.Constants.PI2 - 1e-7, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);
            spacecraft.AddRectangularInstrument(id - 906, "WIDER", "fov515", IO.Astrodynamics.Constants.PI2 - 1e-7, 0.1, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);
            spacecraft.AddRectangularInstrument(id - 907, "HALFPI", "fov515", 0.1, IO.Astrodynamics.Constants.PI2, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);
            spacecraft.AddEllipticalInstrument(id - 908, "WIDEE", "fov515", 0.1, IO.Astrodynamics.Constants.PI2 - 1e-7, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);
            foreach (var instrument in spacecraft.Instruments.Where(x => -x.NaifId % 1000 > 900))
            {
                Assert.Throws<InvalidOperationException>(() =>
                    instrument.FindWindowsInFieldOfViewConstraint(window, spacecraft, earth, Frames.Frame.ICRF, ShapeType.Point, Aberration.LT, Step));
                Assert.Throws<InvalidOperationException>(() => instrument.IsInFOV(window.StartDate, earth, Aberration.LT));
            }

            // An instrument of a user's subclass with a shape other than a circle: its cross angle is unknown
            var user = new UserInstrument(spacecraft, InstrumentShape.Elliptical);
            Assert.Throws<InvalidOperationException>(() =>
                user.FindWindowsInFieldOfViewConstraint(window, spacecraft, earth, Frames.Frame.ICRF, ShapeType.Point, Aberration.LT, Step));
            Assert.Throws<InvalidOperationException>(() => user.IsInFOV(window.StartDate, earth, Aberration.LT));
            // and a circle of a user's subclass is searched, its cross angle its field of view
            Assert.NotNull(new UserInstrument(spacecraft, InstrumentShape.Circular).FindWindowsInFieldOfViewConstraint(new Window(window.StartDate, window.StartDate.AddSeconds(60.0)),
                spacecraft, earth, Frames.Frame.ICRF, ShapeType.Point, Aberration.None, Step));
        }

        // The coverage of the propagated states (choice 14): the observer's, and a spacecraft target's for a kernel-backed observer
        var before = new Window(_case.Start.AddSeconds(-10.0), _case.End);
        var after = new Window(_case.Start.AddSeconds(1.0), _case.End.AddSeconds(10.0));
        Assert.Throws<ArgumentException>(() => _case.PropagatedInstrument(789).FindWindowsInFieldOfViewConstraint(before, _case.Propagated, earth, Frames.Frame.ICRF,
            ShapeType.Point, Aberration.LT, Step));
        Assert.Throws<ArgumentException>(() => _case.PropagatedInstrument(789).FindWindowsInFieldOfViewConstraint(after, _case.Propagated, earth, Frames.Frame.ICRF,
            ShapeType.Point, Aberration.LT, Step));
        Assert.Throws<ArgumentException>(() => _case.KernelInstrument(789).FindWindowsInFieldOfViewConstraint(after, _case.Kernel, _case.PropagatedTarget,
            Frames.Frame.ICRF, ShapeType.Point, Aberration.LT, Step));
        Assert.Throws<ArgumentException>(() => _case.KernelInstrument(789).FindWindowsInFieldOfViewConstraint(before, _case.Kernel, _case.PropagatedTarget,
            Frames.Frame.ICRF, ShapeType.Point, Aberration.LT, Step));

        // An observer inside the target's ellipsoid, on the managed path only (zzocced's rule)
        var inside = new Spacecraft(-5155, "FOV5155", 1000.0, 3000.0, new Clock("FOV5155_CLK", 65536),
            new StateVector(new Vector3(1000000.0, 0.0, 0.0), new Vector3(0.0, 100.0, 0.0), earth, _case.Start, Frames.Frame.ICRF));
        inside.AddCircularInstrument(-5155001, "INSIDE", "fov515", 0.3, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);
        inside.AddRectangularInstrument(-5155002, "INSIDER", "fov515", 0.3, 0.2, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);
        foreach (var instrument in inside.Instruments)
        {
            Assert.Throws<InvalidOperationException>(() => instrument.FindWindowsInFieldOfViewConstraint(new Window(_case.Start, _case.Start.AddSeconds(60.0)), inside, earth,
                PropagatedFieldOfViewCase.TargetFrame("399"), ShapeType.Ellipsoid, Aberration.None, Step));
        }
    }

    /// <summary>
    /// Twelve searches in parallel, each on a fresh spacecraft (six propagated, three of them with a nadir attitude, and six
    /// kernel-backed; circles, ellipses and rectangles), equal the same searches run one after the other on other fresh
    /// spacecraft, to the tick; the CK of a propagated one is still written after its search (the search reads nothing
    /// before the search window, #523).
    /// </summary>
    [Fact]
    public async Task ConcurrentSearches_EqualSequentialSearches()
    {
        var window = new Window(_case.Start.AddSeconds(1.0), _case.Start.AddSeconds(2000.0));
        var propagation = new Window(_case.Start, _case.Start.AddSeconds(2001.0));
        var bodies = new CelestialItem[] { _case.Earth, _case.Moon, _case.Sun };
        var earth = _case.Earth;
        var itrf = PropagatedFieldOfViewCase.TargetFrame("399");

        (Spacecraft spacecraft, Func<IReadOnlyList<Window>> search) Fresh(int i, int generation)
        {
            int number = new[] { 789, 790, 791 }[i % 3];
            var shape = i % 2 == 0 ? ShapeType.Ellipsoid : ShapeType.Point;
            if (i >= 6)
            {
                var kernel = new Spacecraft(PropagatedFieldOfViewCase.ObserverId, "FOV5151", _case.Start, 1000.0, 3000.0, new Clock("FOV5151_CLK", 65536));
                PropagatedFieldOfViewCase.AddInstruments(kernel);
                var instrument = kernel.Instruments.First(x => x.NaifId == PropagatedFieldOfViewCase.ObserverId * 1000 - number);
                return (kernel, () => FieldOfViewSearch.Managed(instrument, window, earth, shape, itrf, Aberration.LT, Step));
            }

            // Identifiers no other test class loads kernels for (FieldOfViewDynamicsTests: -5171 to -5177)
            int id = -5180 - 10 * generation - i;
            var orbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, _case.Start, Frames.Frame.ICRF);
            var spacecraft = new Spacecraft(id, $"FOV{-id}", 1000.0, 3000.0, new Clock($"FOV{-id}_CLK", 65536), orbit);
            PropagatedFieldOfViewCase.AddInstruments(spacecraft);
            ILocalizable target = earth;
            var targetShape = shape;
            if (i >= 3)
            {
                // Nadir: the Earth stays in place in the frame, the Sun turns about the orbit's normal
                var tank = new FuelTank("ft515", "model", "sn515", 1000.0, 1000.0);
                spacecraft.AddFuelTank(tank);
                var engine = new Engine("eng515", "model", "sn515", 300.0, 10.0, tank);
                spacecraft.AddEngine(engine);
                spacecraft.SetStandbyManeuver(new NadirAttitude(earth, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, engine));
                target = _case.Sun;
                targetShape = ShapeType.Point;
                number = 795;
            }

            spacecraft.Propagate(propagation, bodies, false, false, TimeSpan.FromSeconds(1.0));
            var inst = spacecraft.Instruments.First(x => x.NaifId == id * 1000 - number);
            return (spacecraft, () => inst.FindWindowsInFieldOfViewConstraint(window, spacecraft, target, itrf, targetShape, Aberration.LTS, Step).ToArray());
        }

        var sequential = Enumerable.Range(0, 12).Select(i => Fresh(i, 0).search()).ToArray();
        var fresh = Enumerable.Range(0, 12).Select(i => Fresh(i, 1)).ToArray();
        var concurrent = new IReadOnlyList<Window>[12];
        Parallel.For(0, 12, new ParallelOptions { MaxDegreeOfParallelism = 12 }, i => concurrent[i] = fresh[i].search());
        for (int i = 0; i < 12; i++)
        {
            Assert.Equal(sequential[i].Count, concurrent[i].Count);
            for (int k = 0; k < sequential[i].Count; k++)
            {
                Assert.Equal(sequential[i][k].StartDate.TimeSpanFromJ2000().Ticks, concurrent[i][k].StartDate.TimeSpanFromJ2000().Ticks);
                Assert.Equal(sequential[i][k].EndDate.TimeSpanFromJ2000().Ticks, concurrent[i][k].EndDate.TimeSpanFromJ2000().Ticks);
            }
        }

        Assert.True(sequential.Sum(x => x.Count) > 6);
        // The CK is written in the ticks of the spacecraft's clock, whose kernel is loaded first
        var clock = new FileInfo("Data/User/FieldOfViewSearch515/concurrent.tsc");
        File.Delete(clock.FullName);
        await fresh[0].spacecraft.Clock.WriteAsync(new FileInfo(clock.FullName));
        SpiceAPI.Instance.LoadKernels(new FileInfo(clock.FullName));
        var ck = new FileInfo("Data/User/FieldOfViewSearch515/concurrent.bc");
        File.Delete(ck.FullName);
        fresh[0].spacecraft.WriteOrientation(new FileInfo(ck.FullName));
        Assert.True(new FileInfo(ck.FullName).Exists);
    }

    /// <summary>
    /// The internal checks the public paths cannot reach: a convergence tolerance not positive, an aberration out of the
    /// enumeration, and the stellar aberration of a position along the observer's velocity (no rotation, as stelab).
    /// </summary>
    [Fact]
    public void InternalChecks()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IO.Astrodynamics.Occultation.OccultationFinder.CheckStep(Step, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IO.Astrodynamics.Occultation.ApparentPositionReader(_case.Propagated, (Aberration)99, true, true));
        // Along an axis, so that the position and the velocity are parallel to the last bit: u × v / c is exactly zero
        var position = new Vector3(1.0e11, 0.0, 0.0);
        Assert.Equal(position, IO.Astrodynamics.Occultation.ApparentPositionReader.StellarAberration(position, new Vector3(-3.0e4, 0.0, 0.0)));
        Assert.Empty(IO.Astrodynamics.Occultation.OccultationFinder.PositiveWindows([0.0], [-1.0], _ => -1.0, 1e-6));
    }
}
