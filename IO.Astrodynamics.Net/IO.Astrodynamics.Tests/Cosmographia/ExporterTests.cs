// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Cosmographia;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Cosmographia;

public class ExporterTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public ExporterTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        TestKernels.EnsureSolarSystem();
    }

    [Fact]
    public async Task ExportSimplePropagation()
    {
        Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("Cosmographia");
        Scenario scenario = new Scenario("export1", mission, new Window(TimeSystem.Time.J2000TDB.AddYears(21), TimeSystem.Time.J2000TDB.AddYears(21).AddDays(1.0)));
        scenario.AddCelestialItem(TestHelpers.EarthAtJ2000);
        scenario.AddCelestialItem(TestHelpers.Sun);
        scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);
        Spacecraft spacecraft = new Spacecraft(-333, "spc1", 1000.0, 2000.0, new Clock("clockspc1", 256),
            new KeplerianElements(6800000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
        scenario.AddSpacecraft(spacecraft);
        await scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(1.0));

        CosmographiaExporter exporter = new CosmographiaExporter();
        await exporter.ExportAsync(scenario, new DirectoryInfo("CosmographiaExport"));
    }

    [Fact]
    public async Task ExportSimplePropagationWithoutManeuver()
    {
        Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("CosmographiaWM");
        Scenario scenario = new Scenario("exportWM", mission, new Window(TimeSystem.Time.J2000TDB.AddYears(21), TimeSystem.Time.J2000TDB.AddYears(21).AddHours(2.0)));
        scenario.AddCelestialItem(TestHelpers.EarthAtJ2000);
        scenario.AddCelestialItem(TestHelpers.Sun);
        scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);
        Spacecraft spacecraft = new Spacecraft(-334, "spcWM", 1000.0, 2000.0, new Clock("clockspcWM", 256),
            new KeplerianElements(6800000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
        scenario.AddSpacecraft(spacecraft);
        scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);
        scenario.AddCelestialItem(TestHelpers.EarthWithAtmAndGeoAtJ2000);

        await scenario.SimulateAsync( false, false, TimeSpan.FromSeconds(1.0));

        CosmographiaExporter exporter = new CosmographiaExporter();
        await exporter.ExportAsync(scenario, new DirectoryInfo("CosmographiaExport"));
    }

    [Fact]
    public async Task ExportSimpleLongPropagationWithoutManeuver()
    {
        Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("CosmographiaLongExport");
        Scenario scenario = new Scenario("LongExport", mission, new Window(TimeSystem.Time.J2000TDB.AddYears(21), TimeSystem.Time.J2000TDB.AddYears(21).AddHours(4.0)));
        scenario.AddCelestialItem(TestHelpers.EarthAtJ2000);
        scenario.AddCelestialItem(TestHelpers.Sun);
        scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);
        Spacecraft spacecraft = new Spacecraft(-337, "spcLongMission", 1000.0, 2000.0, new Clock("clockSpcLongMission", 65536),
            new KeplerianElements(6800000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
        var a = spacecraft.InitialOrbitalParameters.ToStateVector();
        var b = a.RelativeTo(TestHelpers.Sun, Aberration.None);
        scenario.AddSpacecraft(spacecraft);

        await scenario.SimulateAsync( false, false, TimeSpan.FromSeconds(1.0));

        CosmographiaExporter exporter = new CosmographiaExporter();
        await exporter.ExportAsync(scenario, new DirectoryInfo("CosmographiaExport"));
    }

    [Fact]
    public async Task ExportWithObservationPropagation()
    {
        //Create mission
        Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("Cosmographia");

        //Create a scenario for the mission
        Scenario scenario = new Scenario("EarthObservation", mission, new Window(TimeSystem.Time.J2000TDB.AddYears(21), TimeSystem.Time.J2000TDB.AddYears(21).AddDays(1.0)));
        scenario.AddCelestialItem(TestHelpers.EarthAtJ2000);
        scenario.AddCelestialItem(TestHelpers.Sun);
        scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);

        //Configure a spacecraft
        Spacecraft spacecraft = new Spacecraft(-334, "EarthExplorer", 1000.0, 2000.0, new Clock("clockspc1", 256),
            new KeplerianElements(6800000.0, 0.0, 1.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));

        //Configure and attach an instrument to the spacecraft
        spacecraft.AddRectangularInstrument(-334100, "camera_hires", "camdeluxe", 0.03, 0.048, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);

        //Add a fuel tank
        spacecraft.AddFuelTank(new FuelTank("fuelTank1", "fuelTankModel", "456", 2000.0, 2000.0));

        //Add an engine
        spacecraft.AddEngine(new Engine("engine1", "engineModel", "1234", 450, 50.0, spacecraft.FuelTanks.First()));

        //Attach the spacecraft to the scenario
        scenario.AddSpacecraft(spacecraft);

        //Configure the first maneuver
        var initialManeuver = new InstrumentPointingToAttitude(scenario.Window.StartDate.AddMinutes(10.0), TimeSpan.FromHours(1.0), spacecraft.Instruments.First(),
            spacecraft.InitialOrbitalParameters.Observer, spacecraft.Engines.First());

        //Configure the second maneuver and link it to the first maneuver
        initialManeuver.SetNextManeuver(new InstrumentPointingToAttitude(scenario.Window.StartDate.AddHours(2.0), TimeSpan.FromHours(1.0), spacecraft.Instruments.First(),
            spacecraft.InitialOrbitalParameters.Observer, spacecraft.Engines.First()));

        //Set the first maneuver in standby
        spacecraft.SetStandbyManeuver(initialManeuver);

        //Todo fix why state orientation during maneuver returns Nan
        //Run the simulation
        await scenario.SimulateAsync( false, false, TimeSpan.FromSeconds(1.0));

        //Export scenario to Cosmographia
        CosmographiaExporter exporter = new CosmographiaExporter();
        await exporter.ExportAsync(scenario, new DirectoryInfo("CosmographiaExport"));
    }

    [Fact]
    public async Task ExportWithObservationAndSitePropagation()
    {
        Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("Cosmographia");
        Scenario scenario = new Scenario("SiteObservation", mission, new Window(TimeSystem.Time.J2000TDB.AddYears(21), TimeSystem.Time.J2000TDB.AddYears(21).AddDays(1.0)));
        Site site3 = new Site(14, "DSS-14", TestHelpers.EarthAtJ2000);
        Site site = new Site(34, "DSS-34", TestHelpers.EarthAtJ2000);
        Site site2 = new Site(65, "DSS-65", TestHelpers.EarthAtJ2000);

        scenario.AddSite(site);
        scenario.AddSite(site2);
        scenario.AddSite(site3);

        scenario.AddCelestialItem(TestHelpers.Sun);
        scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);
        scenario.AddCelestialItem(TestHelpers.EarthAtJ2000);
        Spacecraft spacecraft = new Spacecraft(-334, "Spacecraft", 1000.0, 2000.0, new Clock("clockspc1", 256),
            new KeplerianElements(11800000.0, 0.3, 1.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
        spacecraft.AddCircularInstrument(-334100, "Antenna", "antdeluxe", 0.2, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);
        spacecraft.AddFuelTank(new FuelTank("fuelTank1", "fuelTankModel", "456", 2000.0, 2000.0));
        spacecraft.AddEngine(new Engine("engine1", "engineModel", "1234", 450, 50.0, spacecraft.FuelTanks.First()));
        scenario.AddSpacecraft(spacecraft);
        var initialManeuver = new InstrumentPointingToAttitude(scenario.Window.StartDate.AddHours(7.25), TimeSpan.FromHours(0.5), spacecraft.Instruments.First(), site,
            spacecraft.Engines.First());
        spacecraft.SetStandbyManeuver(initialManeuver);
        await scenario.SimulateAsync( false, false, TimeSpan.FromSeconds(1.0));

        CosmographiaExporter exporter = new CosmographiaExporter();
        await exporter.ExportAsync(scenario, new DirectoryInfo("CosmographiaExport"));
    }

    [Fact]
    public async Task ExportSpacecraftReachTarget()
    {
        TimeSystem.Time start = TimeSystem.Time.CreateUTC(667915269.18539762).ToTDB(); //3/2/2021 00:02:18
        TimeSystem.Time startPropagator = TimeSystem.Time.CreateUTC(668085555.829810).ToTDB(); //3/3/2021 23:20:25
        TimeSystem.Time end = TimeSystem.Time.CreateUTC(668174400.000000).ToTDB(); // 3/5/2021 00:01:09

        Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("ReachTarget");
        Scenario scenario = new Scenario("Scenario1", mission, new Window(startPropagator, end));
        scenario.AddCelestialItem(TestHelpers.EarthAtJ2000);
        scenario.AddCelestialItem(TestHelpers.Sun);
        scenario.AddCelestialItem(TestHelpers.MoonAtJ2000);

        //Define parking orbit
        StateVector parkingOrbit = new StateVector(
            new Vector3(5056554.1874925727, 4395595.4942363985, 0.0),
            new Vector3(-3708.6305608890916, 4266.2914313011433, 6736.8538488755494), TestHelpers.EarthAtJ2000,
            start, Frames.Frame.ICRF);

        //Define target orbit
        StateVector targetOrbit = new StateVector(
            new Vector3(4390853.7278876612, 5110607.0005866792, 917659.86391987884),
            new Vector3(-4979.4693432656513, 3033.2639866911495, 6933.1803797017265), TestHelpers.EarthAtJ2000,
            start, Frames.Frame.ICRF);

        //Create and configure spacecraft1
        Clock clock1 = new Clock("clk1", 65536);
        Spacecraft spacecraft1 =
            new Spacecraft(-1790, "Target", 1000.0, 10000.0, clock1, targetOrbit);

        FuelTank fuelTank1 = new FuelTank("ft1", "model1", "sn1", 9000.0, 9000.0);
        Engine engine1 = new Engine("engine1", "model1", "sn1", 450.0, 50.0, fuelTank1);
        spacecraft1.AddFuelTank(fuelTank1);
        spacecraft1.AddEngine(engine1);
        spacecraft1.AddPayload(new Payload("payload1", 50.0, "pay01"));
        spacecraft1.AddCircularInstrument(-1790601, "CAM601", "mod1", 10.0 * IO.Astrodynamics.Constants.Deg2Rad, Vector3.VectorZ, Vector3.VectorX, Vector3.VectorX);
        scenario.AddSpacecraft(spacecraft1);

        //Create and configure spacecraft2
        Clock clock2 = new Clock("clk2", 65536);
        Spacecraft spacecraft2 =
            new Spacecraft(-1791, "Chaser", 1000.0, 10000.0, clock2, parkingOrbit);

        FuelTank fuelTank2 = new FuelTank("ft1", "model1", "sn1", 9000.0, 9000.0);
        Engine engine2 = new Engine("engine1", "model1", "sn1", 450.0, 50.0, fuelTank2);
        spacecraft2.AddFuelTank(fuelTank2);
        spacecraft2.AddEngine(engine2);
        spacecraft2.AddPayload(new Payload("payload1", 50.0, "pay01"));
        spacecraft2.AddCircularInstrument(-1791602, "CAM602", "mod1", 10.0 * IO.Astrodynamics.Constants.Deg2Rad, Vector3.VectorY, Vector3.VectorX, Vector3.Zero);

        var planeAlignmentManeuver = new PlaneAlignmentManeuver(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero,
            targetOrbit, spacecraft2.Engines.First());
        planeAlignmentManeuver.SetNextManeuver(new ApsidalAlignmentManeuver(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
                TimeSpan.Zero, targetOrbit, spacecraft2.Engines.First()))
            .SetNextManeuver(new PhasingManeuver(new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, targetOrbit, 1,
                spacecraft2.Engines.First()))
            .SetNextManeuver(new ApogeeHeightManeuver(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, 15866666.666666666,
                spacecraft2.Engines.First()));

        spacecraft2.SetStandbyManeuver(planeAlignmentManeuver);
        scenario.AddSpacecraft(spacecraft2);
        var summary = await scenario.SimulateAsync( false, false, TimeSpan.FromSeconds(1.0));
        CosmographiaExporter cosmographiaExporter = new CosmographiaExporter();
        await cosmographiaExporter.ExportAsync(scenario, new DirectoryInfo("CosmographiaExport"));
    }

    /// <summary>
    /// The export checks the length of its kernel paths before it touches the output directory (#673): FURNSH stores a file
    /// name of at most 255 characters, and <see cref="SpiceAPI.LoadKernels"/> hands it the full path. Before, the export
    /// deleted the previous export and wrote kernels that could not be loaded.
    /// </summary>
    [Fact]
    public async Task Export_ToADirectoryTooLongForItsKernels_ThrowsAndLeavesItUntouched()
    {
        // Arrange: one spacecraft and one star under NAIF ids no other test uses (-8674, 1000000674), and no site
        // (Data/SolarSystem defines the DSN frames)
        const string name = "EXP8674";
        var mission = new Astrodynamics.Mission.Mission("PathLimit");
        var start = TimeSystem.Time.J2000TDB.AddYears(21);
        var scenario = new Scenario("Export673", mission, new Window(start, start.AddMinutes(10.0)));
        scenario.AddCelestialItem(TestHelpers.EarthAtJ2000);
        var spacecraft = new Spacecraft(-8674, name, 1000.0, 2000.0, new Clock("EXP8674_CLK", 65536),
            new KeplerianElements(6800000.0, 0.0, 0.0, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
        scenario.AddSpacecraft(spacecraft);
        scenario.AddStar(new Astrodynamics.Body.Star(674, "star674", 1E+30, "spec", 2, 0.3792, new Astrodynamics.Coordinates.Equatorial(1, 1, start), 0.1, 0.1, 0, 0, 0, 0, start));
        await scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(1.0));
        var root = Directory.CreateTempSubdirectory("export-length-");
        // The longest kernel of the layout, <output>/<Mission>_<Scenario>/Spacecrafts/<sc>/Orientation/<sc>.ck
        string Longest(string output) => Path.Combine(output, "PathLimit_Export673", "Spacecrafts", name, "Orientation", $"{name}.ck");
        string Output(int longest)
        {
            int pad = longest - Longest(root.FullName).Length - 1;
            var output = Path.Combine(root.FullName, new string('o', pad));
            Assert.Equal(longest, Longest(output).Length);
            return output;
        }

        try
        {
            // An existing export in the directory too long by one character
            var tooLong = new DirectoryInfo(Output(256));
            var existing = Directory.CreateDirectory(Path.Combine(tooLong.FullName, "PathLimit_Export673"));
            var marker = Path.Combine(existing.FullName, "marker.txt");
            await File.WriteAllTextAsync(marker, "previous export");

            // Act and assert: nothing deleted, nothing written
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => new CosmographiaExporter().ExportAsync(scenario, tooLong));
            Assert.Equal("outputDirectory", exception.ParamName);
            Assert.Equal([marker], Directory.EnumerateFileSystemEntries(existing.FullName, "*", SearchOption.AllDirectories));

            // Act: one character shorter
            var atLimit = new DirectoryInfo(Output(255));
            await new CosmographiaExporter().ExportAsync(scenario, atLimit);

            // Assert: every kernel of spice.json loads, the longest at 255 characters
            var exported = Path.Combine(atLimit.FullName, "PathLimit_Export673");
            var spice = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(exported, "spice.json")));
            var kernels = spice.RootElement.GetProperty("spiceKernels").EnumerateArray().Select(k => new FileInfo(Path.Combine(exported, k.GetString()!))).ToArray();
            Assert.Contains(kernels, k => k.FullName == Longest(atLimit.FullName));
            Assert.Contains(kernels, k => k.FullName == Path.Combine(exported, "Stars", "star674.spk"));
            Assert.Equal(255, kernels.Max(k => k.FullName.Length));
            try
            {
                foreach (var kernel in kernels)
                {
                    SpiceAPI.Instance.LoadKernels(kernel);
                }
            }
            finally
            {
                foreach (var kernel in kernels)
                {
                    SpiceAPI.Instance.UnloadKernels(kernel);
                }
            }
        }
        finally
        {
            root.Delete(true);
        }
    }
}
