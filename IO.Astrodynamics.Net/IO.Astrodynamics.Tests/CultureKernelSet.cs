// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using System.IO;
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
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The objects whose text kernels <c>KernelWriterCultureTests</c> compares between the test process, which runs with
/// invariant globalization (<c>Directory.Build.props</c>), and the child process <c>IO.Astrodynamics.Tests.Culture</c>,
/// which runs in a named culture (#821, #798): a spacecraft with a circular, a rectangular and an elliptical instrument
/// and a site from coordinates on the Earth. Built the same way in both processes, so that the writers' bytes differ only
/// by the culture. The file is compiled into both projects.
/// </summary>
internal static class CultureKernelSet
{
    /// <summary>The kernels of the set, by file name, in the order <see cref="Write"/> writes them.</summary>
    internal static readonly string[] Files =
    [
        "clock.tsc", "spacecraft.tf", "circular.tf", "circular.ti", "rectangular.tf", "rectangular.ti", "elliptical.tf",
        "elliptical.ti", "site.tf"
    ];

    /// <summary>
    /// The spacecraft <paramref name="spacecraftId"/> named <paramref name="spacecraftName"/>, about the Earth, with an
    /// instrument of each shape named after <paramref name="instrumentNames"/> (circular, rectangular, elliptical; ids
    /// NaifId × 1000 − 1 to − 3), and the site <paramref name="siteId"/> of the Earth named <paramref name="siteName"/>.
    /// The solar system kernels must be loaded.
    /// </summary>
    internal static (Spacecraft Spacecraft, Site Site) Build(int spacecraftId, string spacecraftName, string[] instrumentNames,
        int siteId, string siteName)
    {
        var earth = new CelestialBody(PlanetsAndMoons.Earth, Frames.Frame.ICRF, TimeSystem.Time.J2000Tdb);
        var spacecraft = new Spacecraft(spacecraftId, spacecraftName, 1000.0,
            new KeplerianElements(6800000.0, 0.0, 0.5, 0.0, 0.0, 0.0, earth, TimeSystem.Time.J2000Tdb, Frames.Frame.ICRF), 2000.0,
            new Clock(spacecraftName + "_clock", 256));
        spacecraft.AddCircularInstrument(instrumentNames[0], 0.25, Vector3.VectorZ, Vector3.VectorX, new Vector3(0.1, -0.2, 0.3));
        spacecraft.AddRectangularInstrument(instrumentNames[1], 0.3, 0.15, Vector3.VectorZ, Vector3.VectorY, new Vector3(-0.1, 0.2, -0.3));
        spacecraft.AddEllipticalInstrument(instrumentNames[2], 0.35, 0.2, Vector3.VectorY, Vector3.VectorZ);
        var site = new Site(siteId, siteName, earth, new Planetodetic(0.4, -0.6, 120.0));
        return (spacecraft, site);
    }

    /// <summary>The mission and the scenario of <see cref="ExportAsync"/>: the export is <c>Culture_Export</c>.</summary>
    internal const string ExportName = "Culture_Export";

    /// <summary>
    /// Simulates the spacecraft and the site of <see cref="Build"/>, with a second site of the same id named
    /// <c>&lt;site&gt;_J</c> on Jupiter, for an hour from 2021-01-01 TDB (Velocity-Verlet at 10 s, outputs every 10 s) and
    /// exports the scenario to Cosmographia into <paramref name="directory"/>. The Jupiter site gives the catalog a body
    /// whose name holds an I (<c>jupiter-features.json</c>, #798).
    /// </summary>
    internal static async Task ExportAsync(Spacecraft spacecraft, Site site, DirectoryInfo directory)
    {
        var start = TimeSystem.Time.J2000Tdb.AddYears(21);
        var scenario = new Scenario("Export", new IO.Astrodynamics.Mission.Mission("Culture"), new TimeSystem.Window(start, start.AddHours(1.0)));
        scenario.AddCelestialItem((CelestialBody)spacecraft.InitialOrbitalParameters.Observer);
        scenario.AddSpacecraft(spacecraft);
        scenario.AddSite(site);
        var jupiter = new CelestialBody(PlanetsAndMoons.Jupiter, Frames.Frame.ICRF, TimeSystem.Time.J2000Tdb);
        scenario.AddSite(new Site(site.Id, site.Name + "_J", jupiter, new Planetodetic(1.0, 0.2, 0.0)));
        scenario.Simulate(new PropagationOptions
        {
            Integrator = new VVIntegratorOptions { Step = System.TimeSpan.FromSeconds(10.0) }, OutputStep = System.TimeSpan.FromSeconds(10.0)
        });
        await new CosmographiaExporter().ExportAsync(scenario, directory);
    }

    /// <summary>Writes the kernels of <see cref="Files"/> into <paramref name="directory"/>.</summary>
    internal static void Write(Spacecraft spacecraft, Site site, DirectoryInfo directory)
    {
        FileInfo File(string name) => new(Path.Combine(directory.FullName, name));
        spacecraft.Clock.Write(File("clock.tsc"));
        spacecraft.Frame.Write(File("spacecraft.tf"));
        var shapes = new[] { "circular", "rectangular", "elliptical" };
        var instruments = new List<Instrument>(spacecraft.Instruments);
        for (int i = 0; i < shapes.Length; i++)
        {
            instruments[i].WriteFrame(File(shapes[i] + ".tf"));
            instruments[i].WriteKernel(File(shapes[i] + ".ti"));
        }

        site.WriteFrame(File("site.tf"));
    }
}
