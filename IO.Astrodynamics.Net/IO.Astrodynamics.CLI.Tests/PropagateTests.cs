// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Tests.OrbitalParameters;
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
        var command = new PropagateCommand();
        var sb = new StringBuilder();
        var sw = new StringWriter(sb);
        var window = new TimeSystem.Window(new TimeSystem.Time("2021-01-20T13:00:00Z"), new TimeSystem.Time("2021-01-20T13:30:00Z"));
        var epochs = Enumerable.Range(0, 31).Select(i => window.StartDate.ToTDB().AddSeconds(60.0 * i)).ToArray();
        double worst = 0.0, fromPointMass = double.MaxValue;

        using (await Configuration.LockAsync())
        {
            Console.SetOut(sw);

            // Act
            await command.Propagate("Data", "P675", new Commands.Parameters.OrbitalParameters
            {
                CenterOfMotionId = 399, OrbitalParametersEpoch = "2021-01-20T13:00:00Z", Frame = "ICRF",
                OrbitalParametersValues = $"{IssLine1},{IssLine2}", FromTLE = true
            }, new WindowParameters { Begin = "2021-01-20T13:00:00Z", End = "2021-01-20T13:30:00Z" }, outputDirectory, true, true);

            var tle = new OrbitalParameters.TLE.TLE("ISS", IssLine1, IssLine2);
            var t0 = window.StartDate.ToTDB();
            var cliEarth = new Body.CelestialBody(399, new Body.GeopotentialModelParameters(
                typeof(PropagateCommand).Assembly.GetManifestResourceStream("IO.Astrodynamics.CLI.Data.EGM2008_to100_TideFree.txt"), 10),
                new Atmosphere.EarthStandardAtmosphere());
            var s = tle.ToOsculating(t0).ToFrame(Frames.Frame.ICRF).ToStateVector();
            var twin = Simulate(new OrbitalParameters.StateVector(s.Position, s.Velocity, cliEarth, t0, Frames.Frame.ICRF), cliEarth, -1001, window);
            var pointMass = Simulate(tle, (Body.CelestialBody)tle.Observer, -1002, window);

            var ephemeris = new FileInfo(Directory.GetFiles(outputDirectory, "P675.spk", SearchOption.AllDirectories).Single());
            try
            {
                SpiceAPI.Instance.LoadKernels(ephemeris);
                foreach (var epoch in epochs)
                {
                    var fromSpk = SpiceAPI.Instance.ReadEphemeris(epoch, 399, -1000, Frames.Frame.ICRF, Aberration.None).ToStateVector();
                    var expected = twin.GetEphemeris(epoch, cliEarth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
                    worst = System.Math.Max(worst, (fromSpk.Position - expected.Position).Magnitude());
                }

                var end = SpiceAPI.Instance.ReadEphemeris(epochs[^1], 399, -1000, Frames.Frame.ICRF, Aberration.None).ToStateVector();
                fromPointMass = (end.Position - pointMass.GetEphemeris(epochs[^1], cliEarth, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position)
                    .Magnitude();
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(ephemeris);
            }
        }

        // Assert: the SPK of the command is the library run on the Earth of the CLI, within the rounding of the SPK, and
        // more than 1 km from the run on the point-mass Earth of the TLE, without geopotential nor atmosphere
        _output.WriteLine($"MEASURE #675 PropagateFromTle_RunsOnTheCliEarth: SPK against the library run {worst:E3} m, " +
                          $"{fromPointMass:F1} m from the point-mass run at the end");
        Assert.Contains("Propagation completed", sb.ToString());
        Assert.InRange(worst, 0.0, SpkAgainstManaged);
        Assert.True(fromPointMass > 1000.0, $"{fromPointMass:F1} m");
    }

    /// <summary>The library run of the command: a scenario of the spacecraft and its centre, drag and SRP, Velocity-Verlet 1 s.</summary>
    private static Body.Spacecraft.Spacecraft Simulate(OrbitalParameters.OrbitalParameters orbit, Body.CelestialBody center, int naifId,
        TimeSystem.Window window)
    {
        var spacecraft = new Body.Spacecraft.Spacecraft(naifId, $"P675R{-naifId}", 100.0, 100.0, new Body.Spacecraft.Clock($"clockP675R{-naifId}", 65536),
            orbit, 1.0, 0.3);
        var scenario = new Mission.Scenario($"ScenarioP675R{-naifId}", new Mission.Mission($"MissionP675R{-naifId}"), window);
        scenario.AddSpacecraft(spacecraft);
        scenario.AddCelestialItem(center);
        scenario.SimulateAsync(true, true, TimeSpan.FromSeconds(1.0)).GetAwaiter().GetResult();
        return spacecraft;
    }

    [Fact]
    public async Task PropagateFromTle_RejectsAnotherCentre()
    {
        // Arrange: -t about the Moon; a TLE is an Earth orbit by definition (SGP4, WGS-72)
        const string outputDirectory = "P675M";
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, true);
        }

        var command = new PropagateCommand();
        var sb = new StringBuilder();
        var sw = new StringWriter(sb);
        ArgumentException exception;

        using (await Configuration.LockAsync())
        {
            Console.SetOut(sw);

            // Act
            exception = await Assert.ThrowsAsync<ArgumentException>(() => command.Propagate("Data", "P675M", new Commands.Parameters.OrbitalParameters
            {
                CenterOfMotionId = 301, OrbitalParametersEpoch = "2021-01-20T13:00:00Z", Frame = "ICRF",
                OrbitalParametersValues = $"{IssLine1},{IssLine2}", FromTLE = true
            }, new WindowParameters { Begin = "2021-01-20T13:00:00Z", End = "2021-01-20T13:30:00Z" }, outputDirectory, false, false));
        }

        // Assert: the rule named, no SPK written
        Assert.Contains("399", exception.Message);
        Assert.DoesNotContain("inertial frame", exception.Message);
        Assert.False(Directory.Exists(outputDirectory) && Directory.GetFiles(outputDirectory, "*.spk", SearchOption.AllDirectories).Any());
    }
}
