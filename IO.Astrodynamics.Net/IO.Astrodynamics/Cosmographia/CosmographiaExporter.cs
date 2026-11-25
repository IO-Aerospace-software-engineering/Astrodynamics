// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Cosmographia.Model;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Cosmographia;

public class CosmographiaExporter
{
    private readonly Dictionary<Spacecraft, double[]> _spacecraftColors = new Dictionary<Spacecraft, double[]>();
    private readonly Dictionary<Instrument, double[]> _instrumentColors = new Dictionary<Instrument, double[]>();

    /// <summary>
    /// The directory under which the export writes its kernels before it copies them to the output directory; the system
    /// temporary directory when null. Tests set it to measure the check of its kernel paths (#673).
    /// </summary>
    internal DirectoryInfo ScratchRoot { get; init; }

    // A new directory under ScratchRoot, or under the system temporary directory
    private DirectoryInfo CreateScratchDirectory() => ScratchRoot == null
        ? Directory.CreateTempSubdirectory()
        : Directory.CreateDirectory(Path.Combine(ScratchRoot.FullName, Path.GetRandomFileName()));

    /// <summary>
    /// Exports the simulated scenario to <c>&lt;outputDirectory&gt;/&lt;Mission&gt;_&lt;Scenario&gt;</c>, which it replaces:
    /// its kernels under <c>Spacecrafts</c>, <c>Sites</c> and <c>Stars</c>, and the Cosmographia catalog files.
    /// </summary>
    /// <remarks>
    /// The export checks its names first, then writes its kernels and its catalog into a new scratch directory, and only
    /// then replaces the previous export, so a check, a writer or the catalog that throws leaves the previous export and
    /// the output directory as they were (#799(b), decision 74 of #668, decided by S. Guillet, 2026-11-23; before, the
    /// previous export was deleted first). A failure while the previous export is deleted or the new one copied leaves part
    /// of either. The scratch directory, under the system temporary directory, is deleted in every case; the clock kernel
    /// of each spacecraft is loaded from it while its CK is written and unloaded after (the sequence of
    /// <see cref="Spacecraft.WriteKernels"/>, R14 of #668; its frame kernel too before 11.0.0, which changed no byte of
    /// the CK), so the export leaves the kernels SPICE holds as they were.
    /// </remarks>
    /// <param name="scenario">The simulated scenario.</param>
    /// <param name="outputDirectory">The directory of the export.</param>
    /// <exception cref="InvalidOperationException">The scenario has not been simulated. Or a name is not a portable file
    /// name (none of <c>/ \ : * ? " &lt; &gt; |</c> nor a control character, not <c>.</c> or <c>..</c>, no trailing dot or
    /// space, not a reserved device name of Windows such as CON, NUL or COM1): a spacecraft, instrument, site or star name,
    /// or the export directory <c>&lt;Mission&gt;_&lt;Scenario&gt;</c> the mission and scenario names form (#799(a), #822,
    /// decisions 74 and 105 of #668, decided by S. Guillet, 2026-11-23 and 2026-11-25: before, a name holding a '/' was
    /// written into nested directories, and a scenario named <c>/..</c> made the export delete the whole output directory).
    /// Or a kernel writer or the catalog throws (a kernel variable SPICE cannot read, a spacecraft without stored states or
    /// attitude record, a star of fewer than three stored states, the period of an open orbit). Nothing is deleted.</exception>
    /// <exception cref="ArgumentException">A kernel of the export would have a full path longer than the 255 characters
    /// (UTF-8 bytes) of a kernel name SPICE takes, which <see cref="SpiceAPI.LoadKernels(FileSystemInfo)"/> could not load: under the output
    /// directory (parameter <c>outputDirectory</c>), or under the new directory of the system temporary directory
    /// (<see cref="Path.GetTempPath"/>) where the export writes its kernels and loads the clock kernel of each
    /// spacecraft before it copies them (no parameter). The longest is usually the CK of a spacecraft,
    /// <c>/&lt;Mission&gt;_&lt;Scenario&gt;/Spacecrafts/&lt;name&gt;/Orientation/&lt;name&gt;.ck</c> after the output
    /// directory, 31 characters plus the mission and scenario names and twice the spacecraft name. Nothing is deleted or
    /// written (#673). A writer whose argument is at fault keeps its <see cref="ArgumentException"/> (the window of a site
    /// shorter than its ephemeris needs, an arc of one stored state), nothing deleted either.</exception>
    public async Task ExportAsync(Scenario scenario, DirectoryInfo outputDirectory)
    {
        if (!scenario.IsSimulated)
        {
            throw new InvalidOperationException("The scenario has not yet been simulated. Call the Simulate method on the scenario before calling the cosmographia exporter.");
        }

        // The names first: a name that leaves its directory would make every path below meaningless (#799(a), #822)
        var exportName = ExportName(scenario);
        CheckNames(scenario, exportName);
        var missionOutputPath = Path.Combine(outputDirectory.FullName, exportName);
        foreach (var kernel in KernelPaths(scenario))
        {
            Kernels.KernelPath.EnsureLoadable(Path.Combine(missionOutputPath, kernel), nameof(outputDirectory));
        }

        // The kernels and the catalog are written under a new scratch directory first, their kernel paths there checked too;
        // the previous export is replaced only once everything is written (#799(b))
        var scratch = CreateScratchDirectory();
        try
        {
            foreach (var kernel in KernelPaths(scenario))
            {
                Kernels.KernelPath.EnsureLoadable(Path.Combine(scratch.FullName, kernel), null);
            }

            ExportRawData(scenario, scratch);

            await ExportSpiceKernelsAsync(scenario, scratch);

            await ExportSites(scenario, scratch);

            await ExportSpacecraftsAsync(scenario, scratch);

            await ExportSensors(scenario, scratch);

            await ExportObservations(scenario, scratch);

            await ExportLoader(scenario, scratch);

            var missionOutputDirectory = new DirectoryInfo(missionOutputPath);
            if (missionOutputDirectory.Exists)
            {
                missionOutputDirectory.Delete(true);
            }

            CopyDirectory(scratch, missionOutputDirectory, true);
        }
        finally
        {
            scratch.Delete(true);
        }
    }

    // The name of the export directory, and of its loader file
    private static string ExportName(Scenario scenario) => $"{scenario.Mission.Name}_{scenario.Name}";

    /// <summary>
    /// Checks that every name the export makes into a path is a portable file name (internal
    /// <see cref="Kernels.PortableFileNames"/>): the export directory the mission and scenario names form, then each
    /// spacecraft and its instruments, each site and each star.
    /// </summary>
    private static void CheckNames(Scenario scenario, string exportName)
    {
        Kernels.PortableFileNames.Check(exportName,
            $"the export directory '{exportName}' of the mission '{scenario.Mission.Name}' and the scenario '{scenario.Name}'");
        foreach (var spacecraft in scenario.Spacecrafts)
        {
            Kernels.PortableFileNames.Check(spacecraft.Name, $"the spacecraft '{spacecraft.Name}'");
            foreach (var instrument in spacecraft.Instruments)
            {
                Kernels.PortableFileNames.Check(instrument.Name, $"the instrument '{instrument.Name}' of the spacecraft '{spacecraft.Name}'");
            }
        }

        foreach (var site in scenario.Sites)
        {
            Kernels.PortableFileNames.Check(site.Name, $"the site '{site.Name}'");
        }

        foreach (var star in scenario.Stars)
        {
            Kernels.PortableFileNames.Check(star.Name, $"the star '{star.Name}'");
        }
    }

    private async Task ExportSites(Scenario scenario, DirectoryInfo outputDirectory)
    {
        var sitesByBody = scenario.Sites.GroupBy(x => x.CelestialBody);
        foreach (var siteByBody in sitesByBody)
        {
            SiteRootObject siteJson = new SiteRootObject();
            siteJson.name = $"{scenario.Mission.Name}_{scenario.Name}";
            var bodyName = CatalogBodyName(siteByBody.Key.Name);
            siteJson.version = "1.0";
            siteJson.items = new SiteItem[1];
            siteJson.items[0] = new SiteItem();
            siteJson.items[0].name = $"{bodyName} surface sites";
            siteJson.items[0].body = bodyName;
            siteJson.items[0].type = "FeatureLabels";
            siteJson.items[0].features = new SiteFeature[siteByBody.Count()];
            int idx = 0;
            foreach (var site in siteByBody)
            {
                siteJson.items[0].features[idx] = new SiteFeature();
                siteJson.items[0].features[idx].name = site.Name;
                siteJson.items[0].features[idx].code = "AA";
                siteJson.items[0].features[idx].origin = string.Empty;
                siteJson.items[0].features[idx].diameter = 1000.0;
                siteJson.items[0].features[idx].longitude = site.Planetodetic.Longitude * Constants.Rad2Deg;
                siteJson.items[0].features[idx].latitude = site.Planetodetic.Latitude * Constants.Rad2Deg;
                siteJson.items[0].features[idx].link = String.Empty;
                idx++;
            }

            await using var fileStream = File.Create(Path.Combine(outputDirectory.FullName, $"{bodyName.ToLowerInvariant()}-features.json"));
            await JsonSerializer.SerializeAsync(fileStream, siteJson, CosmographiaJsonContext.Default.SiteRootObject);
        }
    }

    private async Task ExportLoader(Scenario scenario, DirectoryInfo outputDirectory)
    {
        var siteFiles = outputDirectory.GetFiles("*-features.json", SearchOption.TopDirectoryOnly);
        LoadRootObject loaderJson = new LoadRootObject();
        loaderJson.name = $"{scenario.Mission.Name}_{scenario.Name}";
        loaderJson.version = "1.0";
        List<string> files = new List<string>()
        {
            Path.Combine("spice.json"),
            Path.Combine("spacecrafts.json"),
            Path.Combine("sensors.json"),
            Path.Combine("observations.json")
        };
        files.AddRange(siteFiles.Select(x => Path.GetRelativePath(outputDirectory.FullName, x.FullName)));
        loaderJson.require = files.ToArray();

        await using var fileStream = File.Create(Path.Combine(outputDirectory.FullName, $"{scenario.Mission.Name}_{scenario.Name}.json"));
        await JsonSerializer.SerializeAsync(fileStream, loaderJson, CosmographiaJsonContext.Default.LoadRootObject);
    }

    private async Task ExportObservations(Scenario scenario, DirectoryInfo outputDirectory)
    {
        ObservationRootObject observationJson = new ObservationRootObject();
        observationJson.name = $"{scenario.Mission.Name}_{scenario.Name}";
        observationJson.version = "1.0";
        foreach (var spacecraft in scenario.Spacecrafts)
        {
            var maneuvers = spacecraft.ExecutedManeuvers.OfType<InstrumentPointingToAttitude>().ToArray();
            var maneuversGroupedByInstruments = maneuvers.GroupBy(x => x.Instrument);
            var maneuverByInstruments = maneuversGroupedByInstruments as IGrouping<Instrument, InstrumentPointingToAttitude>[] ?? maneuversGroupedByInstruments.ToArray();
            observationJson.items = new ObservationItems[maneuverByInstruments.Count()];
            int idx = 0;
            foreach (var maneuverByInstrument in maneuverByInstruments)
            {
                observationJson.items[idx] = new ObservationItems();
                observationJson.items[idx].obsClass = "observation";
                observationJson.items[idx].name = $"{spacecraft.Name.ToUpperInvariant()}_{maneuverByInstrument.Key.Name.ToUpperInvariant()}_OBSERVATIONS";
                observationJson.items[idx].startTime = scenario.Window.StartDate.ToString();
                observationJson.items[idx].endTime = scenario.Window.EndDate.ToString();
                observationJson.items[idx].center = CatalogBodyName(spacecraft.InitialOrbitalParameters.Observer.Name);
                observationJson.items[idx].trajectoryFrame = new ObservationTrajectoryFrame();
                observationJson.items[idx].trajectoryFrame.type = "BodyFixed";
                observationJson.items[idx].trajectoryFrame.body = CatalogBodyName(spacecraft.InitialOrbitalParameters.Observer.Name);

                observationJson.items[idx].bodyFrame = new ObservationBodyFrame();
                observationJson.items[idx].bodyFrame.type = "BodyFixed";
                observationJson.items[idx].bodyFrame.body = CatalogBodyName(spacecraft.InitialOrbitalParameters.Observer.Name);

                observationJson.items[idx].geometry = new ObservationGeometry();
                observationJson.items[idx].geometry.type = "Observations";
                observationJson.items[idx].geometry.sensor = $"{spacecraft.Name.ToUpperInvariant()}_{maneuverByInstrument.Key.Name.ToUpperInvariant()}";
                observationJson.items[idx].geometry.footprintColor = _instrumentColors[maneuverByInstrument.Key];
                observationJson.items[idx].geometry.footprintOpacity = 0.4;
                observationJson.items[idx].geometry.showResWithColor = false;
                observationJson.items[idx].geometry.sideDivisions = 125;
                observationJson.items[idx].geometry.alongTrackDivisions = 500;
                observationJson.items[idx].geometry.shadowVolumeScaleFactor = 1.75;
                observationJson.items[idx].geometry.fillInObservations = false;

                observationJson.items[idx].geometry.groups = new ObservationGroup[maneuverByInstrument.Count()];
                int groupIdx = 0;
                foreach (var maneuver in maneuverByInstrument)
                {
                    observationJson.items[idx].geometry.groups[groupIdx] = new ObservationGroup();
                    if (maneuver.ManeuverWindow != null)
                    {
                        observationJson.items[idx].geometry.groups[groupIdx].startTime = maneuver.ManeuverWindow.Value.StartDate.ToString();
                        observationJson.items[idx].geometry.groups[groupIdx].endTime = maneuver.ManeuverWindow.Value.EndDate.ToString();
                    }

                    observationJson.items[idx].geometry.groups[groupIdx].obsRate = 0;
                    groupIdx++;
                }

                idx++;
            }

            await using var fileStream = File.Create(Path.Combine(outputDirectory.FullName, "observations.json"));
            await JsonSerializer.SerializeAsync(fileStream, observationJson, CosmographiaJsonContext.Default.ObservationRootObject);
        }
    }

    private async Task ExportSensors(Scenario scenario, DirectoryInfo outputDirectory)
    {
        SensorRootObject sensorJson = new SensorRootObject();
        sensorJson.name = $"{scenario.Mission.Name}_{scenario.Name}";
        sensorJson.version = "1.0";
        sensorJson.items = new SensorItem[scenario.Spacecrafts.Sum(x => x.Instruments.Count)];
        int idx = 0;
        foreach (var spacecraft in scenario.Spacecrafts)
        {
            var spcColor = GetSpacecraftColor(spacecraft);

            foreach (var instrument in spacecraft.Instruments)
            {
                sensorJson.items[idx] = new SensorItem();
                sensorJson.items[idx].center = spacecraft.Name.ToUpperInvariant();
                sensorJson.items[idx].name = $"{spacecraft.Name.ToUpperInvariant()}_{instrument.Name.ToUpperInvariant()}";
                sensorJson.items[idx].startTime = scenario.Window.StartDate.ToString();
                sensorJson.items[idx].endTime = scenario.Window.EndDate.ToString();
                sensorJson.items[idx].parent = spacecraft.Name.ToUpperInvariant();
                sensorJson.items[idx].sensorClass = "sensor";

                sensorJson.items[idx].geometry = new SensorGeometry();
                sensorJson.items[idx].geometry.type = "Spice";
                sensorJson.items[idx].geometry.instrName = $"{spacecraft.Name.ToUpperInvariant()}_{instrument.Name.ToUpperInvariant()}";
                sensorJson.items[idx].geometry.target = CatalogBodyName(spacecraft.InitialOrbitalParameters.Observer.Name);
                sensorJson.items[idx].geometry.range = 100000;
                sensorJson.items[idx].geometry.rangeTracking = true;

                _instrumentColors[instrument] = new[]
                {
                    spcColor[0] * (1.0 + (idx + 1) * 0.1), spcColor[1] * (1.0 + (idx + 1) * 0.1), spcColor[2] * (1.0 + (idx + 1) * 0.1)
                };
                sensorJson.items[idx].geometry.frustumColor = _instrumentColors[instrument];
                sensorJson.items[idx].geometry.frustumBaseLineWidth = 3;
                sensorJson.items[idx].geometry.frustumOpacity = 0.2;
                sensorJson.items[idx].geometry.gridOpacity = 1;
                sensorJson.items[idx].geometry.footprintOpacity = 0.8;
                sensorJson.items[idx].geometry.sideDivisions = 300;
                sensorJson.items[idx].geometry.onlyVisibleDuringObs = false;
                idx++;
            }
        }

        await using var fileStream = File.Create(Path.Combine(outputDirectory.FullName, "sensors.json"));
        await JsonSerializer.SerializeAsync(fileStream, sensorJson, CosmographiaJsonContext.Default.SensorRootObject);
    }

    private async Task ExportSpacecraftsAsync(Scenario scenario, DirectoryInfo outputDirectory)
    {
        SpacecraftRootObject spacecraftJson = new SpacecraftRootObject();
        spacecraftJson.name = $"{scenario.Mission.Name}_{scenario.Name}";
        spacecraftJson.version = "1.0";
        spacecraftJson.items = new SpacecraftItem[scenario.Spacecrafts.Count];
        int idx = 0;
        foreach (var spacecraft in scenario.Spacecrafts)
        {
            spacecraftJson.items[idx] = new SpacecraftItem();
            spacecraftJson.items[idx].name = spacecraft.Name.ToUpperInvariant();
            spacecraftJson.items[idx].center = CatalogBodyName(spacecraft.InitialOrbitalParameters.Observer.Name);
            spacecraftJson.items[idx].spacecraftClass = "spacecraft";
            spacecraftJson.items[idx].startTime = scenario.Window.StartDate.ToString();
            spacecraftJson.items[idx].endTime = scenario.Window.EndDate.ToString();

            spacecraftJson.items[idx].trajectory = new SpacecraftTrajectory();
            spacecraftJson.items[idx].trajectory.center = spacecraftJson.items[idx].center;
            spacecraftJson.items[idx].trajectory.target = spacecraft.Name.ToUpperInvariant();
            spacecraftJson.items[idx].trajectory.type = "Spice";

            spacecraftJson.items[idx].bodyFrame = new SpacecraftBodyFrame();
            spacecraftJson.items[idx].bodyFrame.name = spacecraft.Frame.Name.ToUpperInvariant();
            spacecraftJson.items[idx].bodyFrame.type = "Spice";

            spacecraftJson.items[idx].geometry = new SpacecraftGeometry();
            spacecraftJson.items[idx].geometry.type = "Globe";
            spacecraftJson.items[idx].geometry.radii = new[] { 0.02, 0.02, 0.02 };

            spacecraftJson.items[idx].label = new SpacecraftLabel();

            spacecraftJson.items[idx].label.showText = true;

            spacecraftJson.items[idx].label.color = GetSpacecraftColor(spacecraft);
            spacecraftJson.items[idx].trajectoryPlot = new SpacecraftTrajectoryPlot();
            spacecraftJson.items[idx].trajectoryPlot.color = spacecraftJson.items[idx].label.color;
            spacecraftJson.items[idx].trajectoryPlot.duration = string.Create(CultureInfo.InvariantCulture, $"{spacecraft.InitialOrbitalParameters.Period().TotalDays} d");
            spacecraftJson.items[idx].trajectoryPlot.fade = 1;
            spacecraftJson.items[idx].trajectoryPlot.lead = string.Create(CultureInfo.InvariantCulture, $"{spacecraft.InitialOrbitalParameters.Period().TotalDays * 0.1} d");
            spacecraftJson.items[idx].trajectoryPlot.visible = true;
            spacecraftJson.items[idx].trajectoryPlot.lineWidth = 3;

            idx++;
        }

        await using var fileStream = File.Create(Path.Combine(outputDirectory.FullName, "spacecrafts.json"));
        await JsonSerializer.SerializeAsync(fileStream, spacecraftJson, CosmographiaJsonContext.Default.SpacecraftRootObject);
    }

    /// <summary>
    /// The name of a body in the catalog, from its SPICE name: its first letter upper-cased and the others lower-cased, in
    /// the invariant culture (<c>JUPITER</c> gives <c>Jupiter</c>, whatever the culture of the thread: under tr-TR the
    /// current culture gave <c>Jupıter</c>, #798, decision 101 of #668, decided by S. Guillet, 2026-11-25).
    /// </summary>
    internal static string CatalogBodyName(string spiceName) =>
        char.ToUpperInvariant(spiceName[0]) + spiceName.Substring(1).ToLowerInvariant();

    private double[] GetSpacecraftColor(Spacecraft spacecraft)
    {
        _spacecraftColors.TryGetValue(spacecraft, out double[] color);

        if (color == null)
        {
            Random rnd = new Random();
            color = new[]
            {
                rnd.NextDouble() * 1.3, rnd.NextDouble() * 1.3, rnd.NextDouble() * 1.3
            };
            _spacecraftColors[spacecraft] = color;
        }

        return color;
    }

    private async Task ExportSpiceKernelsAsync(Scenario scenario, DirectoryInfo outputDirectory)
    {
        var files = outputDirectory.GetFiles("*.*", SearchOption.AllDirectories);
        SpiceRootObject spiceJson = new SpiceRootObject();
        spiceJson.version = "1.0";
        spiceJson.name = $"{scenario.Mission.Name}_{scenario.Name}";
        spiceJson.spiceKernels = files.Select(x => Path.GetRelativePath(outputDirectory.FullName, x.FullName)).ToArray();
        await using var fileStream = File.Create(Path.Combine(outputDirectory.FullName, "spice.json"));
        await JsonSerializer.SerializeAsync(fileStream, spiceJson, CosmographiaJsonContext.Default.SpiceRootObject);
    }

    // The kernels of the export, relative to <Mission>_<Scenario>: the layout ExportRawData writes
    private static string ClockPath(Spacecraft spacecraft) => Path.Combine("Spacecrafts", spacecraft.Name, "Clocks", $"{spacecraft.Name}.tsc");
    private static string FramePath(Spacecraft spacecraft) => Path.Combine("Spacecrafts", spacecraft.Name, "Frames", $"{spacecraft.Name}.tf");
    private static string OrientationPath(Spacecraft spacecraft) => Path.Combine("Spacecrafts", spacecraft.Name, "Orientation", $"{spacecraft.Name}.ck");
    private static string EphemerisPath(Spacecraft spacecraft) => Path.Combine("Spacecrafts", spacecraft.Name, "Ephemeris", $"{spacecraft.Name}.spk");
    private static string InstrumentFramePath(Spacecraft spacecraft, Instrument instrument) => Path.Combine("Spacecrafts", spacecraft.Name, "Instruments", $"{instrument.Name}.tf");
    private static string InstrumentKernelPath(Spacecraft spacecraft, Instrument instrument) => Path.Combine("Spacecrafts", spacecraft.Name, "Instruments", $"{instrument.Name}.ti");
    private static string SiteFramePath(Site site) => Path.Combine("Sites", site.Name, $"{site.Name}.tf");
    private static string SiteEphemerisPath(Site site) => Path.Combine("Sites", site.Name, $"{site.Name}.spk");
    private static string StarEphemerisPath(Star star) => Path.Combine("Stars", $"{star.Name}.spk");

    /// <summary>Every kernel of the export, relative to <c>&lt;Mission&gt;_&lt;Scenario&gt;</c>.</summary>
    private static IEnumerable<string> KernelPaths(Scenario scenario)
    {
        foreach (var spacecraft in scenario.Spacecrafts)
        {
            yield return ClockPath(spacecraft);
            yield return FramePath(spacecraft);
            yield return OrientationPath(spacecraft);
            yield return EphemerisPath(spacecraft);
            foreach (var instrument in spacecraft.Instruments)
            {
                yield return InstrumentFramePath(spacecraft, instrument);
                yield return InstrumentKernelPath(spacecraft, instrument);
            }
        }

        foreach (var site in scenario.Sites)
        {
            yield return SiteFramePath(site);
            yield return SiteEphemerisPath(site);
        }

        foreach (var star in scenario.Stars)
        {
            yield return StarEphemerisPath(star);
        }
    }

    // Writes the kernels under kernelsTemp, whose kernel paths ExportAsync checked; the clock kernel of each spacecraft is
    // loaded while its CK is written, and unloaded after, whether the writing succeeded
    private static void ExportRawData(Scenario scenario, DirectoryInfo kernelsTemp)
    {
        FileInfo Kernel(string relativePath) => new(Path.Combine(kernelsTemp.FullName, relativePath));
        var spacecraftFolder = kernelsTemp.CreateSubdirectory("Spacecrafts");
        var sitesFolder = kernelsTemp.CreateSubdirectory("Sites");
        kernelsTemp.CreateSubdirectory("Stars");
        foreach (var spacecraft in scenario.Spacecrafts)
        {
            var spacecraftOutputDirectory = spacecraftFolder.CreateSubdirectory(spacecraft.Name);
            spacecraftOutputDirectory.CreateSubdirectory("Clocks");
            spacecraftOutputDirectory.CreateSubdirectory("Ephemeris");
            spacecraftOutputDirectory.CreateSubdirectory("Frames");
            spacecraftOutputDirectory.CreateSubdirectory("Instruments");
            spacecraftOutputDirectory.CreateSubdirectory("Orientation");

            // The sequence of Spacecraft.WriteKernels in the layout of the export (R14 of #668): the content first, as the
            // writers would refuse it, then the files, the clock kernel loaded while the CK is written and unloaded after
            var orientation = spacecraft.Frame.CkRecords();
            var ephemeris = spacecraft.EphemerisKernelBytes(asState: false);
            spacecraft.WriteKernelSet(new SpacecraftKernels(Kernel(ClockPath(spacecraft)), Kernel(FramePath(spacecraft)),
                Kernel(EphemerisPath(spacecraft)), Kernel(OrientationPath(spacecraft)),
                spacecraft.Instruments.Select(x => new InstrumentKernelFiles(x, Kernel(InstrumentFramePath(spacecraft, x)),
                    Kernel(InstrumentKernelPath(spacecraft, x)))).ToArray(), []), ephemeris, orientation);
        }

        foreach (var site in scenario.Sites)
        {
            sitesFolder.CreateSubdirectory(site.Name);
            site.WriteFrame(Kernel(SiteFramePath(site)));
            site.WriteEphemeris(scenario.Window, Kernel(SiteEphemerisPath(site)));
        }

        foreach (var star in scenario.Stars)
        {
            star.WriteEphemeris(Kernel(StarEphemerisPath(star)));
        }
    }

    static void CopyDirectory(DirectoryInfo sourceDir, DirectoryInfo destinationDir, bool recursive = false)
    {
        // Check if the source directory exists
        if (!sourceDir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir.FullName}");

        // Cache directories before we start copying
        DirectoryInfo[] dirs = sourceDir.GetDirectories();

        // Create the destination directory
        destinationDir.Create();

        // Get the files in the source directory and copy to the destination directory
        foreach (FileInfo file in sourceDir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir.FullName, file.Name);
            file.CopyTo(targetFilePath);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                var newDestinationDir = new DirectoryInfo(Path.Combine(destinationDir.FullName, subDir.Name));
                CopyDirectory(subDir, newDestinationDir, true);
            }
        }
    }
}