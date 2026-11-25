// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Cosmographia;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Cosmographia;

/// <summary>
/// The checks of <see cref="CosmographiaExporter.ExportAsync"/> before it replaces a previous export (#799(a)(b), #822,
/// decisions 74 and 105 of #668): a name that is not a portable file name, the mission and scenario names included, is
/// refused with <see cref="InvalidOperationException"/> before anything is deleted or written; the kernels and the catalog
/// are written into the scratch directory first, so a writer or the catalog that throws leaves the previous export as it
/// was. Before, the export deleted <c>&lt;Mission&gt;_&lt;Scenario&gt;</c> first, wrote a name holding a '/' into nested
/// directories, and a scenario named <c>/..</c> deleted the whole output directory.
/// </summary>
public class ExportCheckTests
{
    // NAIF ids no other test uses: the spacecraft -8799, its instrument -8799001, the site 799 of the Earth, the star 799
    private const int SpacecraftId = -8799;
    private const string Mission = "Check799";
    private const string ScenarioName = "Export";

    public ExportCheckTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    // A scenario of ten minutes with a spacecraft and its camera, a site from coordinates on the Earth and a star, named as
    // given, simulated with Velocity-Verlet at 1 s
    private static Scenario Simulated(string mission = Mission, string scenarioName = ScenarioName, string spacecraftName = "SC799",
        string instrumentName = "CAM799", string siteName = "SITE799", string starName = "STAR799", Astrodynamics.OrbitalParameters.OrbitalParameters orbit = null)
    {
        var earth = TestHelpers.EarthAtJ2000;
        var start = TimeSystem.Time.J2000TDB.AddYears(21);
        var scenario = new Scenario(scenarioName, new Astrodynamics.Mission.Mission(mission), new Window(start, start.AddMinutes(10.0)));
        scenario.AddCelestialItem(earth);
        var spacecraft = new Spacecraft(SpacecraftId, spacecraftName, 1000.0,
            orbit ?? new KeplerianElements(6800000.0, 0.0, 0.5, 0.0, 0.0, 0.0, earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF), 2000.0,
            new Clock("CLK799", 65536));
        spacecraft.AddCircularInstrument(instrumentName, 0.2, Vector3.VectorZ, Vector3.VectorX);
        scenario.AddSpacecraft(spacecraft);
        scenario.AddSite(new Site(799, siteName, earth, new Planetodetic(0.4, -0.6, 120.0)));
        scenario.AddStar(new Star(799, starName, 1E+30, "spec", 2, 0.3792, Equatorial.FromRightAscensionDeclination(1, 1, start), 0.1, 0.1, 0, 0, 0, 0, start));
        scenario.Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) }, OutputStep = TimeSpan.FromSeconds(60.0) });
        return scenario;
    }

    // An output directory holding a previous export (Check799_Export/marker.txt) and a file of the user beside it
    private static (DirectoryInfo Output, string[] Entries) OutputWithPreviousExport(DirectoryInfo root)
    {
        var output = Directory.CreateDirectory(Path.Combine(root.FullName, "output"));
        var previous = Directory.CreateDirectory(Path.Combine(output.FullName, $"{Mission}_{ScenarioName}"));
        File.WriteAllText(Path.Combine(previous.FullName, "marker.txt"), "previous export");
        File.WriteAllText(Path.Combine(output.FullName, "keep.txt"), "a file of the user");
        return (output, Entries(output));
    }

    // The export leaves loaded no kernel of its own, under its output directory or its scratch root (other classes load and
    // unload theirs meanwhile, so the whole list is not compared)
    private static void AssertNothingLoadedUnder(DirectoryInfo root) =>
        Assert.DoesNotContain(SpiceAPI.Instance.GetLoadedKernels(), x => x.FullName.StartsWith(root.FullName, StringComparison.Ordinal));

    private static string[] Entries(DirectoryInfo directory) =>
        Directory.EnumerateFileSystemEntries(directory.FullName, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();

    // Runs the export of the scenario into an output directory holding a previous export, with a scratch root of its own,
    // asserts it throws InvalidOperationException, that the output directory and the scratch root are unchanged and that no
    // kernel of the export is left loaded; returns the exception
    private static async Task<InvalidOperationException> RefusedAsync(Scenario scenario)
    {
        var root = Directory.CreateTempSubdirectory("export-check-");
        try
        {
            var (output, before) = OutputWithPreviousExport(root);
            var scratchRoot = Directory.CreateDirectory(Path.Combine(root.FullName, "scratch"));
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new CosmographiaExporter { ScratchRoot = scratchRoot }.ExportAsync(scenario, output));

            Assert.Equal(before, Entries(output));
            Assert.Equal("previous export", File.ReadAllText(Path.Combine(output.FullName, $"{Mission}_{ScenarioName}", "marker.txt")));
            Assert.Empty(scratchRoot.EnumerateFileSystemInfos());
            AssertNothingLoadedUnder(root);
            return exception;
        }
        finally
        {
            root.Delete(true);
        }
    }

    /// <summary>
    /// A spacecraft, instrument, site or star name that is not a portable file name is refused before anything is deleted
    /// or written (#799(a)): a separator, a character Windows refuses, a reserved device name, a trailing dot or space.
    /// </summary>
    [Theory]
    [InlineData("spacecraft", "SC/799", "/")]
    [InlineData("spacecraft", "CON", "CON")]
    [InlineData("instrument", "CAM:799", ":")]
    [InlineData("instrument", "nul.cam", "NUL")]
    [InlineData("site", "SITE799.", "dot")]
    [InlineData("site", "SITE\\799", "\\")]
    [InlineData("star", "STAR 799 ", "space")]
    [InlineData("star", "STAR*799", "*")]
    public async Task Export_OfANameThatIsNotAPortableFileName_ThrowsAndLeavesTheOutputUntouched(string owner, string name, string reason)
    {
        var scenario = owner switch
        {
            "spacecraft" => Simulated(spacecraftName: name),
            "instrument" => Simulated(instrumentName: name),
            "site" => Simulated(siteName: name),
            _ => Simulated(starName: name)
        };

        var exception = await RefusedAsync(scenario);

        Assert.Contains($"the {owner} '{name}'", exception.Message);
        Assert.Contains(reason, exception.Message);
    }

    /// <summary>
    /// A scenario named <c>/..</c> made <c>&lt;Mission&gt;_&lt;Scenario&gt;</c> the output directory itself, which the
    /// export deleted whole (#822, decision 105 of #668); the mission and scenario names are checked as the name of the
    /// export directory they form, before anything is deleted.
    /// </summary>
    [Theory]
    [InlineData(Mission, "/..")]
    [InlineData(Mission, "a/b")]
    [InlineData("Check|799", ScenarioName)]
    [InlineData(Mission, "Export.")]
    public async Task Export_OfAMissionOrScenarioNameThatIsNotAPortableFileName_ThrowsAndDeletesNothing(string mission, string scenarioName)
    {
        var scenario = Simulated(mission: mission, scenarioName: scenarioName);

        var exception = await RefusedAsync(scenario);

        Assert.Contains($"'{mission}_{scenarioName}'", exception.Message);
        Assert.Contains($"the mission '{mission}' and the scenario '{scenarioName}'", exception.Message);
    }

    /// <summary>
    /// A writer that throws once the names passed (here the spacecraft frame kernel, whose variable cannot hold a blank,
    /// #676) leaves the previous export as it was: the kernels are written into the scratch directory before the previous
    /// export is replaced (#799(b)). Before, the previous export was deleted first.
    /// </summary>
    [Fact]
    public async Task Export_WhoseKernelWriterThrows_LeavesThePreviousExport()
    {
        var exception = await RefusedAsync(Simulated(spacecraftName: "SC 799"));

        Assert.Contains("cannot be written", exception.Message);
    }

    /// <summary>
    /// The catalog that throws (the trajectory plot of a spacecraft on an open orbit reads its period, #824, not fixed)
    /// leaves the previous export as it was: the catalog is written into the scratch directory too, after the kernels and
    /// before the previous export is replaced (#799(b)).
    /// </summary>
    [Fact]
    public async Task Export_WhoseCatalogThrows_LeavesThePreviousExport()
    {
        var earth = TestHelpers.EarthAtJ2000;
        var hyperbola = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 12000.0, 0.0), earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

        await RefusedAsync(Simulated(orbit: hyperbola));
    }

    /// <summary>
    /// An export that passes its checks replaces the previous export, the file beside it kept, and leaves nothing in its
    /// scratch root nor loaded.
    /// </summary>
    [Fact]
    public async Task Export_ReplacesThePreviousExport()
    {
        var scenario = Simulated();
        var root = Directory.CreateTempSubdirectory("export-check-");
        try
        {
            var (output, _) = OutputWithPreviousExport(root);
            var scratchRoot = Directory.CreateDirectory(Path.Combine(root.FullName, "scratch"));
            await new CosmographiaExporter { ScratchRoot = scratchRoot }.ExportAsync(scenario, output);

            var export = Path.Combine(output.FullName, $"{Mission}_{ScenarioName}");
            Assert.False(File.Exists(Path.Combine(export, "marker.txt")));
            Assert.True(File.Exists(Path.Combine(output.FullName, "keep.txt")));
            foreach (var file in new[]
                     {
                         "spice.json", "spacecrafts.json", "sensors.json", "observations.json", "earth-features.json", $"{Mission}_{ScenarioName}.json",
                         Path.Combine("Spacecrafts", "SC799", "Clocks", "SC799.tsc"), Path.Combine("Spacecrafts", "SC799", "Frames", "SC799.tf"),
                         Path.Combine("Spacecrafts", "SC799", "Orientation", "SC799.ck"), Path.Combine("Spacecrafts", "SC799", "Ephemeris", "SC799.spk"),
                         Path.Combine("Spacecrafts", "SC799", "Instruments", "CAM799.tf"), Path.Combine("Spacecrafts", "SC799", "Instruments", "CAM799.ti"),
                         Path.Combine("Sites", "SITE799", "SITE799.tf"), Path.Combine("Sites", "SITE799", "SITE799.spk"), Path.Combine("Stars", "STAR799.spk")
                     })
            {
                Assert.True(File.Exists(Path.Combine(export, file)), file);
            }

            // spice.json lists the kernels, not the catalog
            using var spice = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Path.Combine(export, "spice.json")));
            Assert.Equal(9, spice.RootElement.GetProperty("spiceKernels").GetArrayLength());
            Assert.Empty(scratchRoot.EnumerateFileSystemInfos());
            AssertNothingLoadedUnder(root);
        }
        finally
        {
            root.Delete(true);
        }
    }

    /// <summary>
    /// The kernels of a spacecraft in the export are byte for byte those of the manual sequence of its writers, the sequence
    /// <see cref="Spacecraft.WriteKernels"/> shares with the export (R14 of #668: the export loads the clock kernel alone
    /// while it writes the CK, the frame kernel too before, which changed no byte).
    /// </summary>
    [Fact]
    public async Task Export_WritesTheSpacecraftKernelsOfTheManualSequence()
    {
        var scenario = Simulated();
        var spacecraft = scenario.Spacecrafts.Single();
        var instrument = spacecraft.Instruments.Single();
        var root = Directory.CreateTempSubdirectory("export-bytes-");
        try
        {
            var output = Directory.CreateDirectory(Path.Combine(root.FullName, "output"));
            await new CosmographiaExporter().ExportAsync(scenario, output);
            var exported = Path.Combine(output.FullName, $"{Mission}_{ScenarioName}", "Spacecrafts", spacecraft.Name);

            var manual = Directory.CreateDirectory(Path.Combine(root.FullName, "manual"));
            FileInfo File(string name) => new(Path.Combine(manual.FullName, name));
            spacecraft.Clock.Write(File("sc.tsc"));
            spacecraft.Frame.Write(File("sc.tf"));
            instrument.WriteFrame(File("cam.tf"));
            instrument.WriteKernel(File("cam.ti"));
            spacecraft.WriteEphemeris(File("sc.spk"));
            SpiceAPI.Instance.LoadKernels(File("sc.tsc"));
            try
            {
                spacecraft.WriteOrientation(File("sc.ck"));
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(File("sc.tsc"));
            }

            foreach (var (mine, theirs) in new[]
                     {
                         ("sc.tsc", Path.Combine("Clocks", spacecraft.Name + ".tsc")), ("sc.tf", Path.Combine("Frames", spacecraft.Name + ".tf")),
                         ("cam.tf", Path.Combine("Instruments", instrument.Name + ".tf")), ("cam.ti", Path.Combine("Instruments", instrument.Name + ".ti")),
                         ("sc.spk", Path.Combine("Ephemeris", spacecraft.Name + ".spk")), ("sc.ck", Path.Combine("Orientation", spacecraft.Name + ".ck"))
                     })
            {
                Assert.True(System.IO.File.ReadAllBytes(File(mine).FullName).AsSpan().SequenceEqual(System.IO.File.ReadAllBytes(Path.Combine(exported, theirs))),
                    $"{theirs} differs from the manual sequence");
            }

            AssertNothingLoadedUnder(root);
        }
        finally
        {
            root.Delete(true);
        }
    }

    /// <summary>
    /// The export refuses to replace a previous export SPICE holds, before anything is deleted (#799(c), decision 103 of
    /// #668, decided by S. Guillet, 2026-11-25): the export directory loaded, a loaded directory above it, or a file of it a
    /// meta-kernel loaded. Before, it deleted the export under SPICE's loads: the text kernels stayed in the pool with the
    /// old content, and a later unload that read them again failed.
    /// </summary>
    [Theory]
    [InlineData("export")]
    [InlineData("output")]
    [InlineData("meta-kernel")]
    public async Task Export_OverAPreviousExportSpiceHolds_IsRefused_AndDeletesNothing(string held)
    {
        var scenario = Simulated();
        var root = Directory.CreateTempSubdirectory("export-held-");
        try
        {
            var output = Directory.CreateDirectory(Path.Combine(root.FullName, "output"));
            await new CosmographiaExporter().ExportAsync(scenario, output);
            var export = new DirectoryInfo(Path.Combine(output.FullName, $"{Mission}_{ScenarioName}"));
            var clock = new FileInfo(Path.Combine(export.FullName, "Spacecrafts", "SC799", "Clocks", "SC799.tsc"));
            FileSystemInfo loaded = held switch
            {
                "export" => export,
                "output" => output,
                _ => MetaKernel(new FileInfo(Path.Combine(root.FullName, "mission.tm")), clock)
            };
            var before = Entries(output);
            SpiceAPI.Instance.LoadKernels(loaded);
            try
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => new CosmographiaExporter().ExportAsync(scenario, output));

                Assert.Contains(held == "meta-kernel" ? clock.FullName : loaded.FullName, exception.Message);
                Assert.Equal(before, Entries(output));
                Assert.True(SpiceAPI.Instance.HoldsKernelFile(clock));
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(loaded);
            }

            // Once unloaded, the export replaces it
            await new CosmographiaExporter().ExportAsync(scenario, output);
            AssertNothingLoadedUnder(root);
        }
        finally
        {
            root.Delete(true);
        }
    }

    /// <summary>
    /// A first export, with no previous export to replace, under a directory loaded with LoadKernels is written and loads
    /// nothing (decision 103 of #668 refuses to replace an export SPICE holds, not to write a new one: the pool is unchanged);
    /// the next export, which would replace the first under the loaded entry, is refused and deletes nothing.
    /// </summary>
    [Fact]
    public async Task FirstExport_UnderALoadedDirectory_IsWritten_AndTheNextOneIsRefused()
    {
        var scenario = Simulated();
        var root = Directory.CreateTempSubdirectory("export-first-");
        try
        {
            var kernel = new FileInfo(Path.Combine(root.FullName, "user.tk"));
            File.WriteAllText(kernel.FullName, "KPL/PCK\n\\begindata\nCHECK799_FIRST_EXPORT = 1\n\\begintext\n");
            var output = new DirectoryInfo(Path.Combine(root.FullName, "output"));
            SpiceAPI.Instance.LoadKernels(root);
            try
            {
                await new CosmographiaExporter().ExportAsync(scenario, output);

                Assert.True(File.Exists(Path.Combine(output.FullName, $"{Mission}_{ScenarioName}", "spice.json")));
                Assert.Equal([root.FullName],
                    SpiceAPI.Instance.GetLoadedKernels().Select(x => x.FullName).Where(x => x.StartsWith(root.FullName, StringComparison.Ordinal)));

                var before = Entries(output);
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => new CosmographiaExporter().ExportAsync(scenario, output));
                Assert.Contains(root.FullName, exception.Message);
                Assert.Equal(before, Entries(output));
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(root);
            }

            AssertNothingLoadedUnder(root);
        }
        finally
        {
            root.Delete(true);
        }
    }

    // A meta-kernel naming one file by its absolute path, in pieces of 60 characters at most
    private static FileInfo MetaKernel(FileInfo metaKernel, FileInfo member)
    {
        var path = member.FullName;
        var value = new System.Text.StringBuilder();
        for (int i = 0; i < path.Length; i += 60)
        {
            value.Append('\'').Append(path.Substring(i, System.Math.Min(60, path.Length - i))).Append(i + 60 < path.Length ? "+'\n" : "'");
        }

        File.WriteAllText(metaKernel.FullName, $"KPL/MK\n\\begindata\nKERNELS_TO_LOAD = ( {value} )\n\\begintext\n");
        return metaKernel;
    }
}
