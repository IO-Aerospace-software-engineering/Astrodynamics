// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/guides/thread-safety.md, compiled (#705). Each csharp block of the page is the region
// "docs/guides/thread-safety.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

namespace DocumentationPageSamples.Guides;

/// <summary>The C# examples of docs/guides/thread-safety.md, one method per block (#705).</summary>
internal static class ThreadSafetyPage
{
    // docs/guides/thread-safety.md, "Shared Bodies, One Options Value"
    internal static BatchResult Block01(int taskCount, StateVector orbit, Window window)
    {
        #region docs/guides/thread-safety.md:1
        // Inputs: taskCount (int), orbit (StateVector), window (Window)
        // One Earth with geopotential, shared by every task
        var earth = new CelestialBody(PlanetsAndMoons.EARTH,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
        var options = new PropagationOptions
        {
            Integrator = new RK78IntegratorOptions
            {
                AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10, InitialStep = TimeSpan.FromSeconds(30.0)
            },
            ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }
        };

        var tasks = new List<PropagationTask>();
        for (int i = 0; i < taskCount; i++)
        {
            // The same state about the shared Earth
            var spacecraft = new Spacecraft(-(1000 + i), $"Sat-{i}", 100.0, 10000.0,
                new Clock($"clk-{i}", 256), new StateVector(orbit.Position, orbit.Velocity, earth, orbit.Epoch, orbit.Frame));
            tasks.Add(new PropagationTask(spacecraft, window, options));
        }

        var result = BatchPropagator.Propagate(tasks);
        #endregion
        return result;
    }

    // docs/guides/thread-safety.md, "Monte Carlo Pattern"
    internal static void Block02(Spacecraft spacecraft, Window window)
    {
        #region docs/guides/thread-safety.md:2
        // Inputs: spacecraft (Spacecraft), window (Window)
        var config = new MonteCarloConfiguration
        {
            RunCount = 100,
            Seed = 42,
            TemplateSpacecraft = spacecraft,   // its initial state carries the covariance that is sampled
            Window = window,
            Propagation = new PropagationOptions
            {
                Integrator = new RK78IntegratorOptions
                {
                    AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10, InitialStep = TimeSpan.FromSeconds(30.0)
                },
                ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }
            }
        };
        #endregion
    }
}
