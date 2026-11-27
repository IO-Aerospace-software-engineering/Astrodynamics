// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Globalization;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Tests.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit.Abstractions;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;

namespace IO.Astrodynamics.CLI.Tests;

public class PropagateTests
{
    private readonly ITestOutputHelper _output;

    public PropagateTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// The settings of propagate before 11.0.0, the migration line of its Breaking entry: Velocity-Verlet at 1 s with outputs
    /// every 1 s, Cd 0.3, and with --drag about the Earth the U.S. Standard Atmosphere 1976 (<see cref="LegacyDrag"/>; R29 of
    /// #668, decision 100).
    /// </summary>
    internal static readonly string[] Legacy = ["--integrator", "vv", "--step", "1s", "--cd", "0.3"];

    /// <summary>The settings of propagate before 11.0.0 with the drag about the Earth.</summary>
    internal static readonly string[] LegacyDrag = ["--drag", .. Legacy, "--atmosphere", "standard"];

    /// <summary>The arguments of a propagation from the orbit <paramref name="from"/> <paramref name="values"/> at J2000 TDB.</summary>
    internal static string[] Propagate(string name, string output, string from, string values, string frame, string begin, string end, params string[] options) =>
    [
        "propagate", name, "-K", "Data", "--output", output, "--from", from, "--orbit", values, "--center", "399", "--epoch", "0.0", "--frame", frame,
        "--begin", begin, "--end", end, .. options
    ];

    /// <summary>The lines the command writes on success: the NAIF identifier of the spacecraft and the output directory.</summary>
    internal static string Written(string output) =>
        $"NAIF id : -1000{Environment.NewLine}Output : {new DirectoryInfo(output).FullName}{Environment.NewLine}";

    [Fact]
    public async Task PropagateWithPerturbations()
    {
        using (await Configuration.LockAsync())
        {
            var res = CliRun.Ok(Propagate("MyBody", "PropagatorExport", "state", "6800000.0 0.0 0.0 0.0 8000.0 0.0", "ICRF", "0.0", "3600.0",
                ["--srp", "--degree", "20", "--body", "10", "--body", "MOON", .. LegacyDrag]));
            Assert.Equal(Written("PropagatorExport"), res);
        }
    }

    [Fact]
    public async Task Propagate_ToADirectoryTooLongForItsKernels_Throws()
    {
        // Arrange: an output directory that puts the CK, the longest kernel of the export,
        // <output>/MissionK673_ScenarioK673/Spacecrafts/K673/Orientation/K673.ck, at 256 characters: one more than FURNSH
        // stores (#673). Before, the export succeeded and its kernels could not be loaded
        var root = Directory.CreateTempSubdirectory("propagate-length-");
        string Longest(string output) => Path.Combine(output, "MissionK673_ScenarioK673", "Spacecrafts", "K673", "Orientation", "K673.ck");
        var output = Path.Combine(root.FullName, new string('o', 256 - Longest(root.FullName).Length - 1));
        Assert.Equal(256, Longest(output).Length);
        try
        {
            using (await Configuration.LockAsync())
            {
                // Act
                var error = CliRun.Fails(1, Propagate("K673", output, "state", "6800000.0 0.0 0.0 0.0 8000.0 0.0", "ICRF", "0.0", "60.0", Legacy));

                // Assert: the library's error (its parameter outputDirectory), nothing written
                Assert.Contains("(Parameter 'outputDirectory')", error);
                Assert.Single(error.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
                Assert.False(Directory.Exists(output));
            }
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Theory]
    [InlineData("circular, i 30, node 40 (suite 155)", "P452C")]
    [InlineData("equatorial, e 0.05, lop 120 (suite 156)", "P452E")]
    public async Task PropagateFromEquinoctial_StartsAtTheReference(string name, string outputDirectory)
    {
        // Arrange: --from-equinoctial with the elements of the conformance cases 155 (circular) and 156 (equatorial),
        // at J2000 TDB, the start of the window. Before #452, the start of 155 was 4.79e6 m off, that of 156 1.32e7 m.
        // The output directory is short: CSPICE loads a kernel path of at most 255 characters
        var reference = EquinoctialReference.Load();
        var c = reference.Cli.Single(x => x.Name == name);
        OrbitalParameters.StateVector start;
        double gm;
        string res;

        using (await Configuration.LockAsync())
        {
            // Act: propagate, then read the exported ephemeris of the spacecraft (-1000) at the start of the window
            res = CliRun.Ok(Propagate("P452", outputDirectory, "equinoctial", c.CliValues, "ICRF", "0.0", "60.0", Legacy));
            var ephemeris = new FileInfo(Directory.GetFiles(outputDirectory, "P452.spk", SearchOption.AllDirectories)
                .Single());
            try
            {
                SpiceApi.Instance.LoadKernels(ephemeris);
                start = SpiceApi.Instance.ReadEphemeris(TimeSystem.Time.J2000Tdb, 399, -1000, Frames.Frame.ICRF,
                    Aberration.None);
                gm = new Body.CelestialBody(399).GM;
            }
            finally
            {
                SpiceApi.Instance.UnloadKernels(ephemeris);
            }
        }

        // Assert against the 60-digit state, with the GM of the kernels of these tests (gm_de431). The start goes
        // through AtEpoch (K): measured again on the Kepler solver of #453; twice the worst over Linux, Windows and
        // macOS arm64 (CI run 40413920226, 2026-10-26): 2.55e-16 (suite 156, macOS arm64) and 2.80e-16 (suite 155)
        Assert.Equal(Written(outputDirectory), res);
        Assert.Equal(reference.Gm["gm_de431"], gm);
        var (position, velocity) = c.At(0.0).RelativeError(start);
        _output.WriteLine($"MEASURE #452 PropagateFromEquinoctial_StartsAtTheReference, {name}: position " +
                          $"{position:E2}, velocity {velocity:E2} (relative)");
        Assert.True(position < 5.2e-16, $"position {position:E2}");
        Assert.True(velocity < 5.7e-16, $"velocity {velocity:E2}");
    }

    /// <summary>
    /// The state of <see cref="PropagateWithPerturbations"/>, 6800 km equatorial at 8000 m/s, and elements of the same
    /// size for the Keplerian (in ECLIPJ2000) and equinoctial activation paths, as the CLI reads them.
    /// </summary>
    private const string StateValues = "6800000.0 0.0 0.0 0.0 8000.0 0.0";

    private const string KeplerianValues = "6800000.0 0.01 0.5 1.0 2.0 0.3";

    private const string EquinoctialValues = "6800000.0 0.005 0.005 0.1 0.1 0.5";

    private const string EmbeddedModel = "IO.Astrodynamics.CLI.Data.EGM2008_to100_TideFree.txt";

    /// <summary>
    /// Before #681, <c>-n</c> was read by no code: every run was the run of degree 10 (the SPKs of -n 2, 10 and 50
    /// byte-identical). The SPK the CLI writes must be the library run of the same scenario (Velocity-Verlet 1 s, no
    /// drag or SRP) with <see cref="GeopotentialModelParameters"/> of the requested degree, on each activation path and
    /// on a non-ICRF input frame, and more than 1 m from the run of degree 10 (32.66 m for n = 2 and 4.514 m for n = 50
    /// at the end of this 600 s window, measured on the state of <see cref="PropagateWithPerturbations"/>).
    /// </summary>
    [Theory]
    [InlineData("s", (ushort)2, "ICRF", StateValues)]
    [InlineData("s", (ushort)50, "ICRF", StateValues)]
    [InlineData("k", (ushort)2, "ECLIPJ2000", KeplerianValues)]
    [InlineData("q", (ushort)2, "ICRF", EquinoctialValues)]
    public async Task EarthGeopotentialDegree_IsTheDegreeOfTheRun(string activation, ushort degree, string frame, string values)
    {
        var end = Time.J2000Tdb.AddSeconds(600.0);
        var name = $"N{degree}{activation}";

        using (await Configuration.LockAsync())
        {
            var cli = await RunAndReadEnd(activation, values, frame, name, end, degree);
            var library = LibraryRunEnd(activation, values, frame, degree, end);
            var degree10 = LibraryRunEnd(activation, values, frame, 10, end);

            var error = (cli - library).Magnitude();
            var separation = (cli - degree10).Magnitude();
            _output.WriteLine($"MEASURE #681 EarthGeopotentialDegree_IsTheDegreeOfTheRun, -{activation} {frame} n = {degree}: " +
                              $"CLI SPK to the library run {error:E2} m, to the run of degree 10 {separation:F3} m");
            Assert.True(error < SpkInterpolationThreshold, $"CLI SPK {error:E2} m from the library run of degree {degree}");
            Assert.True(separation > 1.0, $"CLI SPK {separation:E2} m from the library run of degree 10");
        }
    }

    /// <summary>
    /// The default degree is 10: the SPK of a run without <c>-n</c> is byte for byte that of <c>-n 10</c>, the library run
    /// of degree 10 (a guard on the default, which held before #681 too).
    /// </summary>
    [Fact]
    public async Task DefaultDegree_IsDegree10()
    {
        var end = Time.J2000Tdb.AddSeconds(600.0);

        using (await Configuration.LockAsync())
        {
            var withoutOption = await Run("s", StateValues, "ICRF", "ND", "ND", end, null);
            var with10 = await Run("s", StateValues, "ICRF", "ND", "N10", end, 10);
            Assert.Equal(File.ReadAllBytes(withoutOption.FullName), File.ReadAllBytes(with10.FullName));

            var cli = ReadEnd(withoutOption, end);
            var library = LibraryRunEnd("s", StateValues, "ICRF", 10, end);
            var error = (cli - library).Magnitude();
            _output.WriteLine($"MEASURE #681 DefaultDegree_IsDegree10: CLI SPK to the library run of degree 10 {error:E2} m");
            Assert.True(error < SpkInterpolationThreshold, $"CLI SPK {error:E2} m from the library run of degree 10");
        }
    }

    /// <summary>
    /// The embedded model ends at degree 100 (its last line holds n = m = 100), and a field pads the degrees beyond its
    /// file with zeros: before #681 a degree above it ran at degree 10 about the Earth and was ignored about any other
    /// centre; wired it would run at 100 silently. It is an argument error raised before any output, whatever the centre
    /// (the Moon here); the degree of the model itself runs, as the library run of that degree.
    /// </summary>
    [Fact]
    public async Task EarthGeopotentialDegreeAbove100_IsAnInputError()
    {
        var maximum = EmbeddedModelDegree();
        Assert.Equal(100, maximum);
        const string output = "NA";

        using (await Configuration.LockAsync())
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }

            var refused = CliRun.Fails(2, Propagate(output, output, "state", StateValues, "ICRF", "0.0", "10.0",
                ["--degree", (maximum + 1).ToString(CultureInfo.InvariantCulture), .. Legacy]));
            Assert.Contains(maximum.ToString(CultureInfo.InvariantCulture), refused);
            Assert.False(Directory.Exists(output));

            refused = CliRun.Fails(2, ["propagate", output, "-K", "Data", "--output", output, "--from", "state", "--orbit", "2000000.0 0.0 0.0 0.0 1600.0 0.0",
                "--center", "301", "--epoch", "0.0", "--frame", "ICRF", "--begin", "0.0", "--end", "10.0", "--step", "1s", "--degree", (maximum + 1).ToString(CultureInfo.InvariantCulture)]);
            Assert.Contains(maximum.ToString(CultureInfo.InvariantCulture), refused);
            Assert.False(Directory.Exists(output));

            var end = Time.J2000Tdb.AddSeconds(10.0);
            var cli = await RunAndReadEnd("s", StateValues, "ICRF", "NM", end, (ushort)maximum);
            var library = LibraryRunEnd("s", StateValues, "ICRF", (ushort)maximum, end);
            var error = (cli - library).Magnitude();
            _output.WriteLine($"MEASURE #681 EarthGeopotentialDegreeAbove100_IsAnInputError, n = {maximum}: CLI SPK to the library run {error:E2} m");
            Assert.True(error < SpkInterpolationThreshold, $"CLI SPK {error:E2} m from the library run of degree {maximum}");
        }
    }

    /// <summary>
    /// The start of the TLE runs below: 2021-01-20 13:00 UTC, 47 min after the epoch of the ISS TLE, rounded to the TDB
    /// second, so that the window the CLI parses from its seconds is the window of the reference to the tick.
    /// </summary>
    private static Time TleStart => Time.CreateTdb(System.Math.Round(Time.Parse("2021-01-20T13:00:00Z").ToTdb().TimeSpanFromJ2000().TotalSeconds));

    /// <summary>
    /// Since #675, <c>-t</c> starts from the SGP4 state on the Earth of the CLI, so <c>-n</c> reaches the TLE path too:
    /// the SPK of <c>-t -n</c> must be the library run of the same scenario from the SGP4 state at the TDB start of the
    /// window, in TEME on an Earth with <see cref="GeopotentialModelParameters"/> of the requested degree (the reference of
    /// <see cref="EarthGeopotentialDegree_IsTheDegreeOfTheRun"/>), and more than 1 m from the run of degree 10 at the end
    /// of this 600 s window.
    /// </summary>
    [Theory]
    [InlineData((ushort)2)]
    [InlineData((ushort)50)]
    public async Task EarthGeopotentialDegree_IsTheDegreeOfTheTleRun(ushort degree)
    {
        var start = TleStart;
        var end = start.AddSeconds(600.0);
        var values = $"{IssLine1},{IssLine2}";

        using (await Configuration.LockAsync())
        {
            var cli = await RunAndReadEnd("t", values, "ICRF", $"N{degree}t", end, degree, start);
            var library = LibraryRunEnd("t", values, "ICRF", degree, end, start);
            var degree10 = LibraryRunEnd("t", values, "ICRF", 10, end, start);

            var error = (cli - library).Magnitude();
            var separation = (cli - degree10).Magnitude();
            _output.WriteLine($"MEASURE #681 EarthGeopotentialDegree_IsTheDegreeOfTheTleRun, -t n = {degree}: " +
                              $"CLI SPK to the library run {error:E2} m, to the run of degree 10 {separation:F3} m");
            Assert.True(error < SpkInterpolationThreshold, $"CLI SPK {error:E2} m from the library run of degree {degree}");
            Assert.True(separation > 1.0, $"CLI SPK {separation:E2} m from the library run of degree 10");
        }
    }

    /// <summary>
    /// The default degree of <c>-t</c> is 10, as on the other paths: the SPK of a TLE run without <c>-n</c> is byte for
    /// byte that of <c>-n 10</c>, the library run of degree 10.
    /// </summary>
    [Fact]
    public async Task DefaultDegree_IsDegree10_FromTle()
    {
        var start = TleStart;
        var end = start.AddSeconds(600.0);
        var values = $"{IssLine1},{IssLine2}";

        using (await Configuration.LockAsync())
        {
            var withoutOption = await Run("t", values, "ICRF", "NDt", "NDt", end, null, start: start);
            var with10 = await Run("t", values, "ICRF", "NDt", "N10t", end, 10, start: start);
            Assert.Equal(File.ReadAllBytes(withoutOption.FullName), File.ReadAllBytes(with10.FullName));

            var cli = ReadEnd(withoutOption, end);
            var library = LibraryRunEnd("t", values, "ICRF", 10, end, start);
            var error = (cli - library).Magnitude();
            _output.WriteLine($"MEASURE #681 DefaultDegree_IsDegree10_FromTle: CLI SPK to the library run of degree 10 {error:E2} m");
            Assert.True(error < SpkInterpolationThreshold, $"CLI SPK {error:E2} m from the library run of degree 10");
        }
    }

    /// <summary>
    /// The SPK of the CLI read at the window end against the library run of the same degree, the same computation on the
    /// same platform: what remains is the reading of the type-9 SPK, one unit in the last place of a 6.8e6 m position
    /// (9.3e-10 m). Worst over the rows of these tests in the CI of run 43595592229 (2026-11-14), the -q row on each
    /// platform: Linux 1.04e-9 m, Windows 1.86e-9 m (two units in the last place), macOS arm64 1.04e-9 m; twice the
    /// Windows worst, rounded up to two digits.
    /// </summary>
    private const double SpkInterpolationThreshold = 3.8e-9;

    /// <summary>The degree of the last line of the embedded EGM2008 model of the CLI (n = m there).</summary>
    private static int EmbeddedModelDegree()
    {
        using var stream = typeof(PropagateCommand).Assembly.GetManifestResourceStream(EmbeddedModel)!;
        using var reader = new StreamReader(stream);
        var last = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Last();
        var fields = last.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(fields[0], fields[1]);
        return int.Parse(fields[0], CultureInfo.InvariantCulture);
    }

    private static Task<FileInfo> Run(string activation, string values, string frame, string bodyName, string output, Time end,
        ushort? degree, int center = 399, Time? start = null)
    {
        if (Directory.Exists(output))
        {
            Directory.Delete(output, true);
        }

        var begin = (start ?? Time.J2000Tdb).TimeSpanFromJ2000().TotalSeconds.ToString("R", CultureInfo.InvariantCulture);
        var stop = end.TimeSpanFromJ2000().TotalSeconds.ToString("R", CultureInfo.InvariantCulture);
        string[] orbit = activation == "t"
            ? ["--from", "tle", "--orbit", values.Split(',')[0], "--orbit", values.Split(',')[1]]
            : ["--from", activation switch { "s" => "state", "k" => "keplerian", _ => "equinoctial" }, "--orbit", values,
                "--center", center.ToString(CultureInfo.InvariantCulture), "--epoch", "0.0", "--frame", frame];
        string[] args = ["propagate", bodyName, "-K", "Data", "--output", output, .. orbit, "--begin", begin, "--end", stop, .. Legacy];
        if (degree.HasValue)
        {
            args = [.. args, "--degree", degree.Value.ToString(CultureInfo.InvariantCulture)];
        }

        Assert.Equal(Written(output), CliRun.Ok(args));
        return Task.FromResult(new FileInfo(Directory.GetFiles(output, $"{bodyName}.spk", SearchOption.AllDirectories).Single()));
    }

    private static async Task<Vector3> RunAndReadEnd(string activation, string values, string frame, string name, Time end, ushort degree,
        Time? start = null)
    {
        return ReadEnd(await Run(activation, values, frame, name, name, end, degree, start: start), end);
    }

    /// <summary>The position of the spacecraft (-1000) relative to the Earth in ICRF at the window end, read from its SPK.</summary>
    private static Vector3 ReadEnd(FileInfo ephemeris, Time end)
    {
        try
        {
            SpiceApi.Instance.LoadKernels(ephemeris);
            return SpiceApi.Instance.ReadEphemeris(end, 399, -1000, Frames.Frame.ICRF, Aberration.None).Position;
        }
        finally
        {
            SpiceApi.Instance.UnloadKernels(ephemeris);
        }
    }

    /// <summary>
    /// The reference: the scenario the command builds, with the library's own <see cref="GeopotentialModelParameters"/>
    /// of <paramref name="degree"/> on a fresh stream of the embedded model (a parameter set reads its stream once). For a
    /// TLE (<c>t</c>, its two lines separated by a comma), the SGP4 state at the TDB start of the window in TEME on that
    /// Earth, as the library starts a TLE spacecraft from <c>tle.ToStateVector</c> at the window start (#675), its Earth filled by the geopotential of the options.
    /// </summary>
    private static Vector3 LibraryRunEnd(string activation, string values, string frame, ushort degree, Time end,
        Time? startEpoch = null)
    {
        var stream = typeof(PropagateCommand).Assembly.GetManifestResourceStream(EmbeddedModel)!;
        var earth = new CelestialBody(399, new GeopotentialModelParameters(stream, degree), new EarthStandardAtmosphere());
        var inputFrame = new Frames.Frame(frame == "ICRF" ? "J2000" : frame);
        var start = startEpoch ?? Time.J2000Tdb;
        if (activation == "t")
        {
            var lines = values.Split(',');
            var sgp4 = new IO.Astrodynamics.OrbitalParameters.Tle("Reference", lines[0], lines[1]).ToStateVector(start);
            return LibraryRunEnd(new IO.Astrodynamics.OrbitalParameters.StateVector(sgp4.Position, sgp4.Velocity, earth, start, Frames.Frame.TEME),
                earth, start, end);
        }

        var v = values.Split(' ').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        IO.Astrodynamics.OrbitalParameters.OrbitalParameters initial = activation switch
        {
            "s" => new IO.Astrodynamics.OrbitalParameters.StateVector(new Vector3(v[0], v[1], v[2]), new Vector3(v[3], v[4], v[5]), earth, start,
                inputFrame),
            "k" => new IO.Astrodynamics.OrbitalParameters.KeplerianElements(v[0], v[1], v[2], v[3], v[4], v[5], earth, start, inputFrame),
            _ => new IO.Astrodynamics.OrbitalParameters.EquinoctialElements(v[0], v[1], v[2], v[3], v[4], v[5], earth, start, inputFrame)
        };
        return LibraryRunEnd(initial, earth, start, end);
    }

    /// <summary>The scenario of the command from <paramref name="initial"/>: Velocity-Verlet 1 s, no drag or SRP.</summary>
    private static Vector3 LibraryRunEnd(IO.Astrodynamics.OrbitalParameters.OrbitalParameters initial, CelestialBody earth, Time start,
        Time end)
    {
        var spacecraft = new Spacecraft(-1000, "Reference", 100.0, initial, 100.0, new Clock("clockReference", 65536), 1.0, 0.3);
        var scenario = new Mission.Scenario("ScenarioReference", new Mission.Mission("MissionReference"), new Window(start, end));
        scenario.AddSpacecraft(spacecraft);
        scenario.AddCelestialItem(earth);
        scenario.Simulate(new PropagationOptions
        {
            Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) }, OutputStep = TimeSpan.FromSeconds(1.0)
        });
        return spacecraft.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None).Position;
    }

    private const string IssLine1 = "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054";
    private const string IssLine2 = "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703";

    // The SPK type-9 rounding of the stored states (PropagatedArcsTests.SpkAgainstManaged of IO.Astrodynamics.Tests)
    private const double SpkAgainstManaged = 2.4e-3;

    [Fact]
    public async Task PropagateFromTle_RunsOnTheCliEarth()
    {
        // Arrange: -t with -r and -a over 30 min (#675: the command threw "must be defined in inertial frame"). The
        // reference: the library run of a spacecraft built by the test from the SGP4 state at the TDB start of the window
        // (CSPICE evsgp4_c), rotated to ICRF by the managed TEME, on the Earth of the CLI (EGM2008 at degree 10,
        // EarthStandardAtmosphere), with the settings of the command (Scenario.SimulateAsync, Velocity-Verlet 1 s, mass 100,
        // area 1, Cd 0.3). The output directory is short: CSPICE loads a kernel path of at most 255 characters
        const string outputDirectory = "P675T";
        string res;
        var window = new TimeSystem.Window(TimeSystem.Time.Parse("2021-01-20T13:00:00Z"), TimeSystem.Time.Parse("2021-01-20T13:30:00Z"));
        var epochs = Enumerable.Range(0, 31).Select(i => window.StartDate.ToTdb().AddSeconds(60.0 * i)).ToArray();
        double worst = 0.0, fromPointMass = double.MaxValue;

        using (await Configuration.LockAsync())
        {
            // Act
            res = CliRun.Ok(["propagate", "P675", "-K", "Data", "--output", outputDirectory, "--from", "tle", "--orbit", IssLine1, "--orbit", IssLine2,
                "--begin", "2021-01-20T13:00:00Z", "--end", "2021-01-20T13:30:00Z", "--srp", .. LegacyDrag]);

            var tle = new OrbitalParameters.Tle("ISS", IssLine1, IssLine2);
            var t0 = window.StartDate.ToTdb();
            var cliEarth = new Body.CelestialBody(399, new Body.GeopotentialModelParameters(
                typeof(PropagateCommand).Assembly.GetManifestResourceStream("IO.Astrodynamics.CLI.Data.EGM2008_to100_TideFree.txt"), 10),
                new Atmosphere.EarthStandardAtmosphere());
            var s = tle.ToOsculating(t0).ToFrame(Frames.Frame.ICRF).ToStateVector();
            var twin = Simulate(new OrbitalParameters.StateVector(s.Position, s.Velocity, cliEarth, t0, Frames.Frame.ICRF), cliEarth, -1001, window, true);
            // The run of the command before #675, on the TLE's Earth without its drag: that Earth has no atmospheric model,
            // whose drag was dropped before #674 and is NRLMSISE-00 at nominal space weather since
            var pointMass = Simulate(tle, (Body.CelestialBody)tle.Observer, -1002, window, false);

            var ephemeris = new FileInfo(Directory.GetFiles(outputDirectory, "P675.spk", SearchOption.AllDirectories).Single());
            try
            {
                SpiceApi.Instance.LoadKernels(ephemeris);
                foreach (var epoch in epochs)
                {
                    var fromSpk = SpiceApi.Instance.ReadEphemeris(epoch, 399, -1000, Frames.Frame.ICRF, Aberration.None);
                    var expected = twin.GetEphemeris(epoch, cliEarth, Frames.Frame.ICRF, Aberration.None);
                    worst = System.Math.Max(worst, (fromSpk.Position - expected.Position).Magnitude());
                }

                var end = SpiceApi.Instance.ReadEphemeris(epochs[^1], 399, -1000, Frames.Frame.ICRF, Aberration.None);
                fromPointMass = (end.Position - pointMass.GetEphemeris(epochs[^1], cliEarth, Frames.Frame.ICRF, Aberration.None).Position)
                    .Magnitude();
            }
            finally
            {
                SpiceApi.Instance.UnloadKernels(ephemeris);
            }
        }

        // Assert: the SPK of the command is the library run on the Earth of the CLI, within the rounding of the SPK, and
        // more than 1 km from the run on the point-mass Earth of the TLE, without geopotential nor atmosphere
        _output.WriteLine($"MEASURE #675 PropagateFromTle_RunsOnTheCliEarth: SPK against the library run {worst:E3} m, " +
                          $"{fromPointMass:F1} m from the point-mass run at the end");
        Assert.Equal(Written(outputDirectory), res);
        Assert.InRange(worst, 0.0, SpkAgainstManaged);
        Assert.True(fromPointMass > 1000.0, $"{fromPointMass:F1} m");
    }

    /// <summary>
    /// The library run of the command: a scenario of the spacecraft and its centre, SRP, drag as <paramref name="drag"/> says,
    /// Velocity-Verlet 1 s.
    /// </summary>
    private static Body.Spacecraft.Spacecraft Simulate(OrbitalParameters.OrbitalParameters orbit, Body.CelestialBody center, int naifId,
        TimeSystem.Window window, bool drag)
    {
        var spacecraft = new Body.Spacecraft.Spacecraft(naifId, $"P675R{-naifId}", 100.0, orbit, 100.0,
            new Body.Spacecraft.Clock($"clockP675R{-naifId}", 65536), 1.0, 0.3);
        var scenario = new Mission.Scenario($"ScenarioP675R{-naifId}", new Mission.Mission($"MissionP675R{-naifId}"), window);
        scenario.AddSpacecraft(spacecraft);
        scenario.AddCelestialItem(center);
        scenario.Simulate(new PropagationOptions
        {
            Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) }, OutputStep = TimeSpan.FromSeconds(1.0),
            AtmosphericDrag = drag, SolarRadiationPressure = true
        });
        return spacecraft;
    }

    /// <summary>
    /// Without options, propagate runs with the library's defaults (choice 12 of #668): RK7(8) at 1e-9 with outputs every
    /// 60 s, Cd 2.2, and with --drag NRLMSISE-00 at nominal space weather for the Earth (the atmosphere of an Earth without
    /// a model, #674). Its SPK is the library run of those settings on the Earth of the CLI (EGM2008 at degree 10, no
    /// atmospheric model), read at the outputs, within the rounding of the SPK; and it moves from the run of 10.x, which
    /// --integrator vv --step 1s --cd 0.3 --atmosphere standard gives.
    /// </summary>
    [Fact]
    public async Task Defaults_AreTheLibrarysRun()
    {
        const string output = "PDEF";
        var window = new Window(Time.J2000Tdb, Time.J2000Tdb.AddSeconds(1800.0));
        var outputs = Enumerable.Range(0, 31).Select(i => window.StartDate.AddSeconds(60.0 * i)).ToArray();
        using (await Configuration.LockAsync())
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }

            Assert.Equal(Written(output), CliRun.Ok(Propagate("PDEF", output, "state", StateValues, "ICRF", "0.0", "1800.0", "--drag", "--srp")));
            var spk = new FileInfo(Directory.GetFiles(output, "PDEF.spk", SearchOption.AllDirectories).Single());

            var stream = typeof(PropagateCommand).Assembly.GetManifestResourceStream(EmbeddedModel)!;
            var earth = new CelestialBody(399, new GeopotentialModelParameters(stream, 10));
            var initial = new IO.Astrodynamics.OrbitalParameters.StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), earth,
                window.StartDate, Frames.Frame.ICRF);
            var spacecraft = new Spacecraft(-1000, "Reference", 100.0, initial, 100.0, new Clock("clockReference", 65536), 1.0, 2.2);
            var scenario = new Mission.Scenario("ScenarioDefaults", new Mission.Mission("MissionDefaults"), window);
            scenario.AddSpacecraft(spacecraft);
            scenario.AddCelestialItem(earth);
            scenario.Simulate(new PropagationOptions { AtmosphericDrag = true, SolarRadiationPressure = true });

            double worst = 0.0;
            Vector3 end;
            try
            {
                SpiceApi.Instance.LoadKernels(spk);
                foreach (var epoch in outputs)
                {
                    var fromSpk = SpiceApi.Instance.ReadEphemeris(epoch, 399, -1000, Frames.Frame.ICRF, Aberration.None).Position;
                    worst = System.Math.Max(worst, (fromSpk - spacecraft.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).Position).Magnitude());
                }

                end = SpiceApi.Instance.ReadEphemeris(outputs[^1], 399, -1000, Frames.Frame.ICRF, Aberration.None).Position;
            }
            finally
            {
                SpiceApi.Instance.UnloadKernels(spk);
            }

            var legacy = await RunAndReadEnd("s", StateValues, "ICRF", "PLEG", outputs[^1], 10);
            _output.WriteLine($"MEASURE R29 propagate defaults: CLI SPK to the library run {worst:E2} m at the outputs; " +
                              $"{(end - legacy).Magnitude():F3} m from the run of 10.x (no drag in the latter) after 30 min");
            Assert.True(worst < DefaultsSpkThreshold, $"CLI SPK {worst:E2} m from the library run");
            Assert.True((end - legacy).Magnitude() > 1.0);
        }
    }

    /// <summary>
    /// The SPK of the default run against the library run, read at its 31 outputs: the reading of the type-9 SPK, about a few
    /// units in the last place of a 6.8e6 m position. Measured by the CI of #819 (2026-11-24): 3.07e-9 m on Linux x64,
    /// 2.98e-9 m on Windows, 4.71e-9 m on macOS arm64; twice the worst, rounded up at the second digit.
    /// </summary>
    private const double DefaultsSpkThreshold = 9.5e-9;

    /// <summary>
    /// --integrator vv --step 1s --cd 0.3 --atmosphere standard, the migration line of the Breaking entry, writes the SPK of
    /// propagate before 11.0.0 byte for byte, with drag and SRP, from a state and from a TLE at -n 20: the files of main at
    /// 76e02ed7, measured on Linux x64 with cmp (2026-11-24), are the library runs of those settings, which this test
    /// checks against the SPK (<see cref="PropagateFromTle_RunsOnTheCliEarth"/>, <see cref="EarthGeopotentialDegree_IsTheDegreeOfTheRun"/>)
    /// and here between two runs: the same bytes.
    /// </summary>
    [Fact]
    public async Task Legacy_WritesTheSameSpkTwice()
    {
        using (await Configuration.LockAsync())
        {
            byte[] Run(string output)
            {
                if (Directory.Exists(output))
                {
                    Directory.Delete(output, true);
                }

                Assert.Equal(Written(output), CliRun.Ok(Propagate("Sat", output, "state", "6800000.0 0.0 0.0 0.0 7600.0 0.0", "ICRF", "0.0", "600.0",
                    ["--srp", .. LegacyDrag])));
                return File.ReadAllBytes(Directory.GetFiles(output, "Sat.spk", SearchOption.AllDirectories).Single());
            }

            Assert.Equal(Run("PLA"), Run("PLB"));
        }
    }

    [Fact]
    public async Task PropagateFromTle_RefusesACentre()
    {
        // A TLE is an Earth orbit by definition (SGP4, WGS-72), with its epoch and its frame (R29 of #668): given a centre,
        // an epoch or a frame, an input error before anything is written
        const string outputDirectory = "P675M";
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, true);
        }

        using (await Configuration.LockAsync())
        {
            var error = CliRun.Fails(2, ["propagate", "P675M", "-K", "Data", "--output", outputDirectory, "--from", "tle", "--orbit", IssLine1, "--orbit", IssLine2,
                "--center", "301", "--begin", "2021-01-20T13:00:00Z", "--end", "2021-01-20T13:30:00Z", "--step", "60s"]);
            Assert.Equal($"Error: --from tle: a TLE carries its centre (the Earth), its epoch and its frame (TEME); leave out --center, --epoch and --frame.{Environment.NewLine}", error);
        }

        Assert.False(Directory.Exists(outputDirectory));
    }

    /// <summary>
    /// The options a centre refuses (R29 of #668, choice 3): the drag about a centre other than the Earth and Mars, and an
    /// atmosphere chosen for a centre other than the Earth (Mars keeps its standard atmosphere); an atmosphere without the
    /// drag that reads it (choice 17 of decision 100); input errors, before anything is written.
    /// </summary>
    [Fact]
    public async Task DragAndAtmosphere_AboutAnotherCentre_AreInputErrors()
    {
        using (await Configuration.LockAsync())
        {
            string[] About(string center, params string[] options) =>
            [
                "propagate", "PMOON", "-K", "Data", "--output", "PMOON", "--from", "state", "--orbit", "4000km 0 0 0 3km/s 0", "--center", center,
                "--epoch", "0.0", "--frame", "ICRF", "--begin", "0.0", "--end", "10.0", "--step", "1s", .. options
            ];
            Assert.Equal($"Error: --drag: the command has an atmosphere for the Earth and Mars only, the centre is 301.{Environment.NewLine}",
                CliRun.Fails(2, About("MOON", "--drag")));
            Assert.Equal($"Error: --atmosphere: the atmosphere chosen is the Earth's, the centre is 499.{Environment.NewLine}",
                CliRun.Fails(2, About("MARS", "--drag", "--atmosphere", "standard")));
            Assert.Equal($"Error: --atmosphere: an atmosphere is read by the drag only; give --drag.{Environment.NewLine}",
                CliRun.Fails(2, About("EARTH", "--atmosphere", "standard")));
            Assert.StartsWith("Error: --atmosphere: 'msis' is not one of nrlmsise00, standard.", CliRun.Fails(2, About("MOON", "--drag", "--atmosphere", "msis")));
            Assert.StartsWith("Error: --integrator: 'rk4' is not one of rk78, vv.", CliRun.Fails(2, About("MOON", "--integrator", "rk4")));
            // propagate writes text only: it has no --format
            CliRun.Fails(2, About("MOON", "--format", "json"));
            Assert.False(Directory.Exists("PMOON"));
        }
    }

    /// <summary>
    /// The values of the spacecraft a propagation refuses before it runs (review of L8): a mass or an area of 0 or less, a
    /// negative drag coefficient, a third body that is the centre, a name that cannot name a kernel variable.
    /// </summary>
    [Theory]
    [InlineData("--mass", "-5", "Error: --mass: the mass is above 0 kg, '-5' was given.")]
    [InlineData("--mass", "0", "Error: --mass: the mass is above 0 kg, '0' was given.")]
    [InlineData("--area", "0", "Error: --area: the area is above 0 m2, '0' was given.")]
    [InlineData("--cd", "-1", "Error: --cd: the drag coefficient is 0 or above, '-1' was given.")]
    [InlineData("--body", "EARTH", "Error: --body: 399 is the centre of the orbit, which acts already; give the other bodies only.")]
    public async Task SpacecraftValues_TheCommandRefuses(string option, string value, string expected)
    {
        using (await Configuration.LockAsync())
        {
            Assert.Equal(expected + Environment.NewLine,
                CliRun.Fails(2, Propagate("PBAD", "PBAD", "state", StateValues, "ICRF", "0.0", "10.0", option, value)));
            Assert.StartsWith("Error: name: The frame kernel of the spacecraft 'my sat' cannot be written",
                CliRun.Fails(2, Propagate("my sat", "PBAD", "state", StateValues, "ICRF", "0.0", "10.0")));
            Assert.False(Directory.Exists("PBAD"));
        }
    }

    /// <summary>
    /// A spacecraft name that is not a portable file name is refused before the propagation (#799(a), decision 74 of
    /// #668): the name names the directories and files of the export, which refuses it too, and the mission and scenario
    /// the command builds from it. Before, a name holding a '/' propagated and wrote nested directories, and a reserved
    /// device name or a trailing dot wrote files Windows cannot open.
    /// </summary>
    [Theory]
    [InlineData("a/b", "it holds '/' (U+002F)")]
    [InlineData("CON", "it is the device name CON")]
    [InlineData("sat.", "it ends with a dot")]
    public async Task NameThatIsNotAPortableFileName_TheCommandRefuses(string name, string reason)
    {
        using (await Configuration.LockAsync())
        {
            Assert.StartsWith($"Error: name: The files of the spacecraft '{name}' cannot be written: its name is not a portable file name, {reason}",
                CliRun.Fails(2, Propagate(name, "PNAME", "state", StateValues, "ICRF", "0.0", "10.0")));
            Assert.False(Directory.Exists("PNAME"));
        }
    }
}
