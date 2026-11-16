// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/tutorials/batch-propagation.md, compiled (#705). Each csharp block of the page is the region
// "docs/tutorials/batch-propagation.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
// give the inputs of a block that continues an earlier one, as the comment opening the block names them
// ("// Inputs: ..."). DocumentationPagesExamplesTests checks that each block equals its region (the block's using
// directives aside, which are this file's) and runs the blocks that need only Data/SolarSystem. The namespace is
// outside IO.Astrodynamics, as an application's; the file imports every namespace of the library, so
// DocumentationPagesExamplesTests also compiles each block that declares using directives with those directives, the
// earlier blocks' and a console application's implicit ones alone, as a reader who copies it does. Do not edit a region
// alone: change the page and copy the block.

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
using IO.Astrodynamics.TimeSystem.Frames;
using TLE = IO.Astrodynamics.OrbitalParameters.TLE.TLE;

namespace DocumentationPageSamples.Tutorials;

/// <summary>The C# examples of docs/tutorials/batch-propagation.md, one method per block (#705).</summary>
internal static class BatchPropagationPage
{
    // docs/tutorials/batch-propagation.md, "Setting up propagation tasks"
    internal static List<PropagationTask> Block01(Spacecraft spacecraft1, Window window, CelestialBody earth, Spacecraft spacecraft2)
    {
        #region docs/tutorials/batch-propagation.md:1
        // Inputs: spacecraft1 and spacecraft2 (Spacecraft), window (Window), earth (CelestialBody)
        var tasks = new List<PropagationTask>
        {
            new PropagationTask(
                spacecraft1, window,
                new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
                IncludeAtmosphericDrag: false,
                IncludeSolarRadiationPressure: false,
                DeltaT: TimeSpan.FromSeconds(60),
                IntegratorFactory: () => new RK78Integrator(1e-10, 1e-10, 30.0)),
            new PropagationTask(
                spacecraft2, window,
                new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
                IncludeAtmosphericDrag: true,
                IncludeSolarRadiationPressure: true,
                DeltaT: TimeSpan.FromSeconds(60),
                IntegratorFactory: () => new RK78Integrator(1e-10, 1e-10, 30.0)),
        };
        #endregion
        return tasks;
    }

    // docs/tutorials/batch-propagation.md, "Running the batch"
    internal static BatchResult Block02(List<PropagationTask> tasks)
    {
        #region docs/tutorials/batch-propagation.md:2
        // Inputs: tasks (List<PropagationTask>)
        var result = BatchPropagator.Propagate(tasks, new BatchOptions
        {
            MaxDegreeOfParallelism = 4
        });
        #endregion
        return result;
    }

    // docs/tutorials/batch-propagation.md, "Processing results"
    internal static void Block03(BatchResult result)
    {
        #region docs/tutorials/batch-propagation.md:3
        // Inputs: result (BatchResult)
        foreach (var r in result.Results)
        {
            if (r.Succeeded)
            {
                Console.WriteLine($"{r.Spacecraft.Name}: " +
                                  $"{r.Solution.StateVectors.Count} states");
            }
            else
            {
                Console.WriteLine($"{r.Spacecraft.Name}: FAILED - {r.Error.Message}");
            }
        }

        Console.WriteLine($"Batch: {result.ElapsedTime.TotalSeconds:F1} s, all succeeded: {result.AllSucceeded}");
        #endregion
    }

    // docs/tutorials/batch-propagation.md, "Progress reporting"
    internal static void Block04(List<PropagationTask> tasks)
    {
        #region docs/tutorials/batch-propagation.md:4
        // Inputs: tasks (List<PropagationTask>)
        var result = BatchPropagator.Propagate(tasks, new BatchOptions
        {
            MaxDegreeOfParallelism = 4,
            Progress = new Progress<BatchProgress>(p =>
            {
                Console.WriteLine($"Progress: {p.Completed}/{p.Total}");
            })
        });
        #endregion
    }

    // docs/tutorials/batch-propagation.md, "Cancellation"
    internal static void Block05(List<PropagationTask> tasks)
    {
        #region docs/tutorials/batch-propagation.md:5
        // Inputs: tasks (List<PropagationTask>)
        var cts = new CancellationTokenSource();

        // Cancel after 30 seconds
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var result = BatchPropagator.Propagate(tasks, new BatchOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = cts.Token
        });
        #endregion
    }

    // docs/tutorials/batch-propagation.md, "Thread safety requirements"
    internal static void Block06(List<Spacecraft> spacecraftList, Window window)
    {
        #region docs/tutorials/batch-propagation.md:6
        // Inputs: spacecraftList (List<Spacecraft>), window (Window)
        var tasks = Enumerable.Range(0, spacecraftList.Count).Select(i =>
        {
            // Fresh CelestialBody per task
            var taskEarth = new CelestialBody(PlanetsAndMoons.EARTH);

            return new PropagationTask(
                spacecraftList[i], window,
                new CelestialItem[] { taskEarth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
                IncludeAtmosphericDrag: false,
                IncludeSolarRadiationPressure: false,
                DeltaT: TimeSpan.FromSeconds(60),
                IntegratorFactory: () => new RK78Integrator(1e-10, 1e-10, 30.0));
        }).ToList();
        #endregion
    }
}
