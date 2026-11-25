using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cocona;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CLI.Cli;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Cosmographia;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;

namespace IO.Astrodynamics.CLI.Commands;

public class PropagateCommand
{
    /// <summary>The NAIF identifier of the spacecraft the command propagates, which it writes.</summary>
    internal const int SpacecraftId = -1000;

    [Command("propagate", Description = "Propagates a spacecraft about its centre and writes its kernels and a Cosmographia scenario")]
    public int Propagate(
        [Argument("name", Description = "Name of the spacecraft")]
        string name,
        [Option("output", ValueName = "directory", Description = "Directory of the kernels and the Cosmographia scenario written")]
        string output,
        OrbitOptions orbit,
        KernelOptions kernels,
        PropagationWindowOptions window,
        VerboseOptions outputOptions,
        [Option("integrator", ValueName = "rk78|vv", Description = "Integrator: rk78, RK7(8) at 1e-9 (default), or vv, Velocity-Verlet at 1 s")]
        string integrator = null,
        [Option("drag", Description = "Atmospheric drag, about the Earth or Mars")]
        bool drag = false,
        [Option("srp", Description = "Solar radiation pressure")]
        bool srp = false,
        [Option("atmosphere", ValueName = "nrlmsise00|standard", Description = "Atmosphere of the Earth for --drag: nrlmsise00, NRLMSISE-00 at nominal space weather (default), or standard, the U.S. Standard Atmosphere 1976; Mars takes its standard atmosphere")]
        string atmosphere = null,
        [Option("degree", ValueName = "degree", Description = "Degree of the geopotential of the Earth (EGM2008), 10 by default, 100 at most")]
        string degree = null,
        [Option("mass", ValueName = "mass", Description = "Mass of the spacecraft, kg")]
        string mass = "100",
        [Option("area", ValueName = "area", Description = "Cross-sectional area of the spacecraft, m2")]
        string area = "1",
        [Option("cd", ValueName = "coefficient", Description = "Drag coefficient (2.2 by default)")]
        string cd = null,
        [Option("body", ValueName = "body", Description = "A celestial body whose gravity acts as a third body, in addition to the centre: a NAIF identifier or a name SPICE knows; repeatable")]
        string[] body = null,
        [Argument("unexpected", Description = "Nothing: a value with spaces is quoted, everything but the objects is an option")]
        string[] unexpected = null)
    {
        return CliContext.Execute(outputOptions.Verbose, () =>
        {
            CliValues.NoMore(unexpected);
            var settings = PropagationSettings.Read(integrator, drag, atmosphere, degree, mass, area, cd);
            try
            {
                // The name of the spacecraft names its kernels: checked before anything runs, as its frame kernel checks it
                Kernels.TextKernelNames.Check("FRAME_" + (name ?? string.Empty).ToUpper() + "_FRAME", $"the spacecraft '{name}'");
            }
            catch (InvalidOperationException e)
            {
                throw new CliInputException($"name: {e.Message}", e);
            }

            CliOrbit.Kind(orbit);
            var propagationWindow = CliValues.Window(window.Begin, window.End);
            var step = CliValues.Step(window.Step, "--step");
            if (string.IsNullOrWhiteSpace(output))
            {
                throw new CliInputException("--output: a directory is required.");
            }

            CliKernels.Load(kernels.Kernels);
            // The centre without models: its geopotential and atmosphere are the fallbacks of the options (implementer
            // choice 16 of decision 100), and a TLE starts from its SGP4 state at the window start on its own Earth (#675),
            // which the options fill
            var initialOrbit = CliOrbit.Read(orbit, id => new CelestialBody(id));
            var centerId = initialOrbit is TLE ? PlanetsAndMoons.EARTH.NaifId : initialOrbit.Observer.NaifId;
            settings.CheckCenter(centerId);

            var bodyIds = (body ?? []).Select(x => CliValues.Object(x, "--body")).ToArray();
            if (bodyIds.Contains(centerId))
            {
                throw new CliInputException($"--body: {centerId} is the centre of the orbit, which acts already; give the other bodies only.");
            }

            var bodies = bodyIds.Select(x => new CelestialBody(x)).ToList();

            var clock = new Clock($"clock{name}", 65536);
            var spacecraft = new Spacecraft(SpacecraftId, name, settings.Mass, initialOrbit, settings.Mass, clock, settings.Area, settings.DragCoefficient);

            var scenario = new Scenario($"Scenario{name}", new Mission.Mission($"Mission{name}"), propagationWindow);
            scenario.AddSpacecraft(spacecraft);
            scenario.AddCelestialItem((CelestialBody)spacecraft.InitialOrbitalParameters.Observer);
            bodies.ForEach(scenario.AddCelestialItem);

            if (outputOptions.Verbose)
            {
                CliContext.Progress.WriteLine("Propagation in progress.");
            }

            scenario.Simulate(new PropagationOptions
            {
                Integrator = settings.Integrator,
                Geopotential = centerId == PlanetsAndMoons.EARTH.NaifId ? Helpers.EarthGeopotential(settings.Degree) : null,
                Atmosphere = centerId switch
                {
                    _ when centerId == PlanetsAndMoons.EARTH.NaifId => settings.Atmosphere,
                    _ when centerId == PlanetsAndMoons.MARS.NaifId => new MarsStandardAtmosphere(),
                    _ => null
                },
                OutputStep = step,
                AtmosphericDrag = drag,
                SolarRadiationPressure = srp
            });

            var directory = new DirectoryInfo(output);
            new CosmographiaExporter().ExportAsync(scenario, directory).GetAwaiter().GetResult();

            CliContext.Out.WriteLine($"NAIF id : {SpacecraftId}");
            CliContext.Out.WriteLine($"Output : {directory.FullName}");
        });
    }

    /// <summary>The settings of a propagation of the command, read before the kernels are loaded.</summary>
    internal sealed record PropagationSettings(
        IntegratorOptions Integrator,
        ushort Degree,
        bool Drag,
        string AtmosphereName,
        double Mass,
        double Area,
        double DragCoefficient)
    {
        /// <summary>
        /// The integrator without --integrator: RK7(8) at 1e-9, the library's default (choice 12 of #668: Velocity-Verlet at
        /// 1 s before 11.0.0).
        /// </summary>
        internal const string DefaultIntegrator = "rk78";

        /// <summary>
        /// The atmosphere of the Earth without --atmosphere: NRLMSISE-00 at nominal space weather, the library's for an Earth
        /// without a model (#674; the U.S. Standard Atmosphere 1976 before 11.0.0).
        /// </summary>
        internal const string DefaultAtmosphere = "nrlmsise00";

        /// <summary>The drag coefficient without --cd: 2.2, the library's (0.3 before 11.0.0).</summary>
        internal const double DefaultDragCoefficient = 2.2;

        /// <summary>
        /// The atmosphere of the Earth the settings name: none for NRLMSISE-00, so that the Earth takes the library's,
        /// NRLMSISE-00 at nominal space weather (#674), or the U.S. Standard Atmosphere 1976.
        /// </summary>
        internal IAtmosphericModel Atmosphere => AtmosphereName == "standard" ? new EarthStandardAtmosphere() : null;

        internal static PropagationSettings Read(string integrator, bool drag, string atmosphere, string degree, string mass, string area, string cd)
        {
            var integratorName = CliValues.Choice(integrator, "--integrator", "rk78", "vv") ?? DefaultIntegrator;
            var atmosphereName = CliValues.Choice(atmosphere, "--atmosphere", "nrlmsise00", "standard");
            if (atmosphereName != null && !drag)
            {
                // Implementer choice 17 of decision 100: the atmosphere is read by the drag only
                throw new CliInputException("--atmosphere: an atmosphere is read by the drag only; give --drag.");
            }

            var degreeValue = Helpers.EarthGeopotentialDefaultDegree;
            if (degree != null)
            {
                if (!ushort.TryParse(degree.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out degreeValue) ||
                    degreeValue > Helpers.EarthGeopotentialMaximumDegree)
                {
                    throw new CliInputException(
                        $"--degree: the degree of the Earth geopotential is an integer from 0 to {Helpers.EarthGeopotentialMaximumDegree}, the degree of the embedded EGM2008 model; '{degree}' was given.");
                }
            }

            var massValue = CliValues.Quantity(mass, Dimension.Mass, "--mass");
            var areaValue = CliValues.Quantity(area, Dimension.Area, "--area");
            var cdValue = cd == null ? DefaultDragCoefficient : CliValues.Quantity(cd, Dimension.None, "--cd");
            if (massValue <= 0.0)
            {
                throw new CliInputException($"--mass: the mass is above 0 kg, '{mass}' was given.");
            }

            if (areaValue <= 0.0)
            {
                throw new CliInputException($"--area: the area is above 0 m2, '{area}' was given.");
            }

            if (cdValue < 0.0)
            {
                throw new CliInputException($"--cd: the drag coefficient is 0 or above, '{cd}' was given.");
            }
            return new PropagationSettings(
                integratorName == "rk78" ? RK78IntegratorOptions.Default : new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
                degreeValue, drag, atmosphereName ?? DefaultAtmosphere, massValue, areaValue, cdValue)
            {
                AtmosphereExplicit = atmosphereName != null
            };
        }

        /// <summary>
        /// The centre allows the settings: the drag about the Earth or Mars only, --atmosphere about the Earth only (R29 of
        /// #668, choice 3: Mars keeps its standard atmosphere).
        /// </summary>
        internal void CheckCenter(int centerId)
        {
            if (Drag && centerId != PlanetsAndMoons.EARTH.NaifId && centerId != PlanetsAndMoons.MARS.NaifId)
            {
                throw new CliInputException($"--drag: the command has an atmosphere for the Earth and Mars only, the centre is {centerId}.");
            }

            if (AtmosphereExplicit && centerId != PlanetsAndMoons.EARTH.NaifId)
            {
                throw new CliInputException($"--atmosphere: the atmosphere chosen is the Earth's, the centre is {centerId}.");
            }
        }

        /// <summary>Whether --atmosphere was given.</summary>
        internal bool AtmosphereExplicit { get; init; }
    }
}
