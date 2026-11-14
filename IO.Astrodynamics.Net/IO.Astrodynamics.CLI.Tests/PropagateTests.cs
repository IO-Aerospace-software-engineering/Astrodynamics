// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Globalization;
using System.Text;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Tests.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit.Abstractions;

namespace IO.Astrodynamics.CLI.Tests;

public class PropagateTests
{
    private readonly ITestOutputHelper _output;

    public PropagateTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task PropagateWithPerturbations()
    {
        var command = new PropagateCommand();
        StringBuilder sb = new StringBuilder();
        StringWriter sw = new StringWriter(sb);
        string res;

        using (await Configuration.LockAsync())
        {
            Console.SetOut(sw);

            await command.Propagate("Data", "MyBody",
                new Commands.Parameters.OrbitalParameters
                {
                    CenterOfMotionId = 399, OrbitalParametersEpoch = "0.0", Frame = "ICRF", OrbitalParametersValues = "6800000.0 0.0 0.0 0.0 8000.0 0.0",
                    FromStateVector = true
                }, new WindowParameters { Begin = "0.0", End = "3600.0" }, "PropagatorExport", true,
                true, 20, celestialBodies: [10, 301]);

            res = sb.ToString();
        }

        Assert.Contains("Propagation completed", res);
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
        var command = new PropagateCommand();
        StringBuilder sb = new StringBuilder();
        StringWriter sw = new StringWriter(sb);
        OrbitalParameters.StateVector start;
        double gm;

        using (await Configuration.LockAsync())
        {
            Console.SetOut(sw);

            // Act: propagate, then read the exported ephemeris of the spacecraft (-1000) at the start of the window
            await command.Propagate("Data", "P452",
                new Commands.Parameters.OrbitalParameters
                {
                    CenterOfMotionId = 399, OrbitalParametersEpoch = "0.0", Frame = "ICRF",
                    OrbitalParametersValues = c.CliValues, FromEquinoctial = true
                }, new WindowParameters { Begin = "0.0", End = "60.0" }, outputDirectory, false, false);
            var ephemeris = new FileInfo(Directory.GetFiles(outputDirectory, "P452.spk", SearchOption.AllDirectories)
                .Single());
            try
            {
                SpiceAPI.Instance.LoadKernels(ephemeris);
                start = SpiceAPI.Instance.ReadEphemeris(TimeSystem.Time.J2000TDB, 399, -1000, Frames.Frame.ICRF,
                    Aberration.None).ToStateVector();
                gm = new Body.CelestialBody(399).GM;
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(ephemeris);
            }
        }

        // Assert against the 60-digit state, with the GM of the kernels of these tests (gm_de431). The start goes
        // through AtEpoch (K): measured again on the Kepler solver of #453; twice the worst over Linux, Windows and
        // macOS arm64 (CI run 40413920226, 2026-10-26): 2.55e-16 (suite 156, macOS arm64) and 2.80e-16 (suite 155)
        Assert.Contains("Propagation completed", sb.ToString());
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
        var end = Time.J2000TDB.AddSeconds(600.0);
        var name = $"N{degree}{activation}";

        using (await Configuration.LockAsync())
        {
            var cli = await RunAndReadEnd(activation, values, frame, name, end, degree);
            var library = await LibraryRunEnd(activation, values, frame, degree, end);
            var degree10 = await LibraryRunEnd(activation, values, frame, 10, end);

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
        var end = Time.J2000TDB.AddSeconds(600.0);

        using (await Configuration.LockAsync())
        {
            var withoutOption = await Run("s", StateValues, "ICRF", "ND", "ND", end, null);
            var with10 = await Run("s", StateValues, "ICRF", "ND", "N10", end, 10);
            Assert.Equal(File.ReadAllBytes(withoutOption.FullName), File.ReadAllBytes(with10.FullName));

            var cli = ReadEnd(withoutOption, end);
            var library = await LibraryRunEnd("s", StateValues, "ICRF", 10, end);
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
    public async Task EarthGeopotentialDegreeAbove100_IsAnArgumentError()
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

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => Run("s", StateValues, "ICRF", output, output,
                Time.J2000TDB.AddSeconds(10.0), (ushort)(maximum + 1)));
            Assert.Contains(maximum.ToString(CultureInfo.InvariantCulture), exception.Message);
            Assert.False(Directory.Exists(output));

            exception = await Assert.ThrowsAsync<ArgumentException>(() => Run("s", "2000000.0 0.0 0.0 0.0 1600.0 0.0", "ICRF", output, output,
                Time.J2000TDB.AddSeconds(10.0), (ushort)(maximum + 1), 301));
            Assert.Contains(maximum.ToString(CultureInfo.InvariantCulture), exception.Message);
            Assert.False(Directory.Exists(output));

            var end = Time.J2000TDB.AddSeconds(10.0);
            var cli = await RunAndReadEnd("s", StateValues, "ICRF", "NM", end, (ushort)maximum);
            var library = await LibraryRunEnd("s", StateValues, "ICRF", (ushort)maximum, end);
            var error = (cli - library).Magnitude();
            _output.WriteLine($"MEASURE #681 EarthGeopotentialDegreeAbove100_IsAnArgumentError, n = {maximum}: CLI SPK to the library run {error:E2} m");
            Assert.True(error < SpkInterpolationThreshold, $"CLI SPK {error:E2} m from the library run of degree {maximum}");
        }
    }

    /// <summary>
    /// The SPK of the CLI read at the window end against the library run of the same degree, the same computation on the
    /// same platform: what remains is the reading of the type-9 SPK, one unit in the last place of a 6.8e6 m position
    /// (9.3e-10 m). Measured on Linux (#681): 0 to 1.04e-9 m over the rows of these tests (the -q row the worst).
    /// Provisional: twice the Linux worst, to be set from the three-platform CI logs.
    /// </summary>
    private const double SpkInterpolationThreshold = 2.1e-9;

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

    private static async Task<FileInfo> Run(string activation, string values, string frame, string bodyName, string output, Time end,
        ushort? degree, int center = 399)
    {
        var command = new PropagateCommand();
        var sb = new StringBuilder();
        Console.SetOut(new StringWriter(sb));
        if (Directory.Exists(output))
        {
            Directory.Delete(output, true);
        }

        var parameters = new Commands.Parameters.OrbitalParameters
        {
            CenterOfMotionId = center, OrbitalParametersEpoch = "0.0", Frame = frame, OrbitalParametersValues = values,
            FromStateVector = activation == "s", FromKeplerian = activation == "k", FromEquinoctial = activation == "q"
        };
        var window = new WindowParameters { Begin = "0.0", End = end.TimeSpanFromJ2000().TotalSeconds.ToString("R", CultureInfo.InvariantCulture) };
        if (degree.HasValue)
        {
            await command.Propagate("Data", bodyName, parameters, window, output, false, false, degree.Value);
        }
        else
        {
            await command.Propagate("Data", bodyName, parameters, window, output, false, false);
        }

        Assert.Contains("Propagation completed", sb.ToString());
        return new FileInfo(Directory.GetFiles(output, $"{bodyName}.spk", SearchOption.AllDirectories).Single());
    }

    private static async Task<Vector3> RunAndReadEnd(string activation, string values, string frame, string name, Time end, ushort degree)
    {
        return ReadEnd(await Run(activation, values, frame, name, name, end, degree), end);
    }

    /// <summary>The position of the spacecraft (-1000) relative to the Earth in ICRF at the window end, read from its SPK.</summary>
    private static Vector3 ReadEnd(FileInfo ephemeris, Time end)
    {
        try
        {
            SpiceAPI.Instance.LoadKernels(ephemeris);
            return SpiceAPI.Instance.ReadEphemeris(end, 399, -1000, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position;
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(ephemeris);
        }
    }

    /// <summary>
    /// The reference: the scenario the command builds, with the library's own <see cref="GeopotentialModelParameters"/>
    /// of <paramref name="degree"/> on a fresh stream of the embedded model (a parameter set reads its stream once).
    /// </summary>
    private static async Task<Vector3> LibraryRunEnd(string activation, string values, string frame, ushort degree, Time end)
    {
        var stream = typeof(PropagateCommand).Assembly.GetManifestResourceStream(EmbeddedModel)!;
        var earth = new CelestialBody(399, new GeopotentialModelParameters(stream, degree), new EarthStandardAtmosphere());
        var v = values.Split(' ').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        var inputFrame = new Frames.Frame(frame == "ICRF" ? "J2000" : frame);
        var start = Time.J2000TDB;
        IO.Astrodynamics.OrbitalParameters.OrbitalParameters initial = activation switch
        {
            "s" => new IO.Astrodynamics.OrbitalParameters.StateVector(new Vector3(v[0], v[1], v[2]), new Vector3(v[3], v[4], v[5]), earth, start,
                inputFrame),
            "k" => new IO.Astrodynamics.OrbitalParameters.KeplerianElements(v[0], v[1], v[2], v[3], v[4], v[5], earth, start, inputFrame),
            _ => new IO.Astrodynamics.OrbitalParameters.EquinoctialElements(v[0], v[1], v[2], v[3], v[4], v[5], earth, start, inputFrame)
        };
        var spacecraft = new Spacecraft(-1000, "Reference", 100.0, 100.0, new Clock("clockReference", 65536), initial, 1.0, 0.3);
        var scenario = new Mission.Scenario("ScenarioReference", new Mission.Mission("MissionReference"), new Window(start, end));
        scenario.AddSpacecraft(spacecraft);
        scenario.AddCelestialItem(earth);
        await scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(1.0));
        return spacecraft.GetEphemeris(end, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position;
    }
}
