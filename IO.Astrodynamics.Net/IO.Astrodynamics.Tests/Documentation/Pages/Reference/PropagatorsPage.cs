// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/reference/propagators.md, compiled (#705). Each csharp block of the page is the region
// "docs/reference/propagators.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
// give the inputs of a block that continues an earlier one, as the comment opening the block names them
// ("// Inputs: ..."). DocumentationPagesExamplesTests checks that each block equals its region (the block's using
// directives aside, which are this file's) and runs the blocks that need only Data/SolarSystem. The namespace is
// outside IO.Astrodynamics, as an application's; the file imports every namespace of the library, so
// DocumentationPagesExamplesTests also compiles every block of a page that declares using directives, whether the block
// declares them or not, with its own, the earlier blocks' and a console application's implicit ones alone, as a reader
// who copies it does (#716). Do not edit a region alone: change the page and copy the block.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IO.Astrodynamics;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.Common.Enums;
using IO.Astrodynamics.CCSDS.OMM;
using IO.Astrodynamics.CCSDS.OPM;
using IO.Astrodynamics.Converters;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Cosmographia;
using IO.Astrodynamics.Cosmographia.Model;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Exceptions;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Helpers;
using IO.Astrodynamics.Kernels;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Maneuver.Lambert;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.Occultation;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using TLE = IO.Astrodynamics.OrbitalParameters.TLE.TLE;

namespace DocumentationPageSamples.Reference;

/// <summary>The C# examples of docs/reference/propagators.md, one method per block (#705).</summary>
internal static class PropagatorsPage
{
    // docs/reference/propagators.md, "Propagation Options"
    internal static void Block01(Spacecraft spacecraft, Window window)
    {
        #region docs/reference/propagators.md:1
        // Inputs: spacecraft (Spacecraft), window (Window)
        var rk = new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 };
        var options = PropagationOptions.Default with
        {
            Integrator = rk,
            ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
            AtmosphericDrag = true,
            SolarRadiationPressure = true,
            Geopotential = GeopotentialModelParameters.Egm2008(10)
        };
        var solution = spacecraft.Propagate(window, options);
        Console.WriteLine($"{solution.Options.ThirdBodies.Count} third bodies, {solution.Options.Atmosphere.GetType().Name}");
        #endregion
    }

    // docs/reference/propagators.md, "TLEPropagator"
    internal static void Block02(Window window, Spacecraft spacecraft, TimeSpan deltaT)
    {
        #region docs/reference/propagators.md:2
        // Inputs: window (Window), spacecraft (Spacecraft), deltaT (TimeSpan)
        var propagator = new TLEPropagator(window, spacecraft, deltaT);
        var solution = propagator.Propagate();
        #endregion
    }

    // docs/reference/propagators.md, "BatchPropagator"
    internal static void Block03(Spacecraft spacecraft1, Window window, Spacecraft spacecraft2)
    {
        #region docs/reference/propagators.md:3
        // Inputs: spacecraft1 and spacecraft2 (Spacecraft), window (Window)
        var options = new PropagationOptions
        {
            Integrator = new RK78IntegratorOptions
            {
                AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10, InitialStep = TimeSpan.FromSeconds(30.0)
            },
            ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }
        };
        var tasks = new List<PropagationTask>
        {
            new PropagationTask(spacecraft1, window, options),
            new PropagationTask(spacecraft2, window, options)
        };

        var result = BatchPropagator.Propagate(tasks, new BatchOptions
        {
            MaxDegreeOfParallelism = 4
        });

        foreach (var r in result.Results)
        {
            if (r.Succeeded)
                Console.WriteLine($"{r.Spacecraft.Name}: {r.Solution.StateVectors.Count} states");
            else
                Console.WriteLine($"{r.Spacecraft.Name}: FAILED — {r.Error.Message}");
        }
        #endregion
    }

    // docs/reference/propagators.md, "Example"
    internal static void Block04(Spacecraft spacecraft)
    {
        #region docs/reference/propagators.md:4
        // Inputs: spacecraft (Spacecraft)
        var config = new MonteCarloConfiguration
        {
            RunCount = 100,
            Seed = 42,
            TemplateSpacecraft = spacecraft,
            Window = new Window(Time.J2000TDB, Time.J2000TDB.AddHours(2)),
            Propagation = new PropagationOptions
            {
                Integrator = new RK78IntegratorOptions
                {
                    AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10, InitialStep = TimeSpan.FromSeconds(30.0)
                },
                ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }
            }
        };

        var result = MonteCarloPropagator.Propagate(config);

        Console.WriteLine($"Success: {result.SuccessCount}/{result.TotalCount}");
        var last = result.EpochStatistics[^1];
        Console.WriteLine($"Position 95th percentile RSS: {last.RssPositionPercentiles.P95:F1} m");
        #endregion
    }
}
