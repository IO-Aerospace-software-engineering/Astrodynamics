using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cocona;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Cosmographia;
using IO.Astrodynamics.Mission;

namespace IO.Astrodynamics.CLI.Commands;

public class PropagateCommand
{
    public PropagateCommand()
    {
    }

    [Command("propagate", Description = "Propagate a small body")]
    public async Task Propagate(
        [Argument(Description = "Kernels directory path")]
        string kernelsPath,
        [Argument(Description = "Body name")] string bodyName,
        Parameters.OrbitalParameters orbitalParameters,
        WindowParameters windowParameters,
        [Argument(Description = "Output directory")]
        string outputDirectory,
        [Option('r', Description = "Include solar radiation pressure perturbation")]
        bool useSolarRadiationPressure,
        [Option('a', Description = "Include atmospheric drag perturbation (Earth and Mars only)")]
        bool useAtmosphericDrag,
        [Option('n', Description = "Number of degrees used by earth geopotential model (Max. 100")]
        ushort earthGeopotentialDegree = 10,
        [Argument(Description = "Body mass")] double bodyMass = 100.0,
        [Argument(Description = "Body projected area")]
        double area = 1.0,
        [Argument(Description = "Drag coefficient")]
        double cd = 0.3,
        [Argument(Description = "Celestial bodies involved into the propagation in addition to the center of motion")]
        int[] celestialBodies = null
    )
    {
        //Load kernels
        API.Instance.LoadKernels(new DirectoryInfo(kernelsPath));

        //Initialize date
        Clock clock = new Clock($"clock{bodyName}", 65536);
        Spacecraft spacecraft = new Spacecraft(-1000, bodyName, bodyMass, bodyMass, clock, Helpers.ConvertToOrbitalParameters(orbitalParameters), area, cd);
        List<CelestialBody> bodies = new List<CelestialBody>([spacecraft.InitialOrbitalParameters.Observer as CelestialBody]);
        bodies.AddRange(celestialBodies?.Select(x => new CelestialBody(x)) ?? Array.Empty<CelestialBody>());

        //Build scenario
        Scenario scenario = new Scenario($"Scenario{bodyName}", new Mission.Mission($"Mission{bodyName}"), Helpers.ConvertWindowInput(windowParameters));
        scenario.AddSpacecraft(spacecraft);
        bodies.ForEach((b) => scenario.AddCelestialItem(b));

        //Simulate
        var qualifiedOutputDirectory = new DirectoryInfo(outputDirectory);
        Console.WriteLine("Orbit propagation in progress. This operation could take a long time. Duration expected for one orbit 7000km x 7000km on mid range computer with geopotential N=30 = 600ms");
        await scenario.SimulateAsync(qualifiedOutputDirectory, useAtmosphericDrag, useSolarRadiationPressure,TimeSpan.FromSeconds(1.0));

        //Export to cosmographia
        CosmographiaExporter cosmographiaExporter = new CosmographiaExporter();

        await cosmographiaExporter.ExportAsync(scenario, qualifiedOutputDirectory);
        spacecraft.Dispose();
        scenario.RootDirectory.Parent?.Delete(true);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(
            $"Propagation completed. Now you can use generated kernels or visualize simulation in cosmographia.{Environment.NewLine}Output location : {qualifiedOutputDirectory.FullName}");
        Console.ResetColor();
    }
}