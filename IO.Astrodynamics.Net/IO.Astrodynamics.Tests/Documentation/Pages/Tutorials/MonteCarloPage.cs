// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/tutorials/monte-carlo.md, compiled (#705). Each csharp block of the page is the region
// "docs/tutorials/monte-carlo.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

/// <summary>The C# examples of docs/tutorials/monte-carlo.md, one method per block (#705).</summary>
internal static class MonteCarloPage
{
    // docs/tutorials/monte-carlo.md, "Initial state with covariance"
    internal static void Block01(double r0, double v0, CelestialBody earth)
    {
        #region docs/tutorials/monte-carlo.md:1
        // Inputs: r0 and v0 (double), earth (CelestialBody)
        var cov = new Matrix(6, 6);
        cov.Set(0, 0, 100.0);   // sigma_x^2  (m^2)
        cov.Set(1, 1, 100.0);   // sigma_y^2
        cov.Set(2, 2, 100.0);   // sigma_z^2
        cov.Set(3, 3, 0.01);    // sigma_vx^2 (m^2/s^2)
        cov.Set(4, 4, 0.01);    // sigma_vy^2
        cov.Set(5, 5, 0.01);    // sigma_vz^2

        var orbit = new StateVector(
            new Vector3(r0, 0, 0),
            new Vector3(0, v0, 0),
            earth, Time.J2000TDB, Frame.ICRF, cov);
        #endregion
    }

    // docs/tutorials/monte-carlo.md, "MonteCarloConfiguration"
    internal static void Block02(Spacecraft spacecraft)
    {
        #region docs/tutorials/monte-carlo.md:2
        // Inputs: spacecraft (Spacecraft)
        var config = new MonteCarloConfiguration
        {
            RunCount = 100,
            Seed = 42,
            TemplateSpacecraft = spacecraft,
            Window = new Window(Time.J2000TDB, Time.J2000TDB.AddHours(2)),
            CelestialBodiesFactory = () => [
                new CelestialBody(PlanetsAndMoons.EARTH),
                Stars.SUN_BODY,
                PlanetsAndMoons.MOON_BODY
            ],
            DeltaT = TimeSpan.FromSeconds(60),
            IntegratorFactory = () => new RK78Integrator(1e-10, 1e-10, 30.0),
        };
        #endregion
    }

    // docs/tutorials/monte-carlo.md, "Thread safety with CelestialBodiesFactory"
    internal static void Block03()
    {
        #region docs/tutorials/monte-carlo.md:3
        // The configuration's factory, as a variable: CelestialBodiesFactory = celestialBodiesFactory
        Func<IEnumerable<CelestialItem>> celestialBodiesFactory = () =>
        [
            new CelestialBody(PlanetsAndMoons.EARTH),   // fresh Earth per thread
            Stars.SUN_BODY,
            PlanetsAndMoons.MOON_BODY
        ];
        #endregion
    }

    // docs/tutorials/monte-carlo.md, "Running the campaign"
    internal static void Block04(MonteCarloConfiguration config)
    {
        #region docs/tutorials/monte-carlo.md:4
        // Inputs: config (MonteCarloConfiguration)
        var result = MonteCarloPropagator.Propagate(config);

        Console.WriteLine($"Completed {result.SuccessCount} / {config.RunCount} runs");
        Console.WriteLine($"Failed:   {result.FailureCount}");
        #endregion
    }

    // docs/tutorials/monte-carlo.md, "Per-epoch statistics"
    internal static void Block05(MonteCarloResult result)
    {
        #region docs/tutorials/monte-carlo.md:5
        // Inputs: result (MonteCarloResult)
        // Statistics at the final epoch
        var last = result.EpochStatistics[^1];

        Console.WriteLine($"Epoch:           {last.Epoch}");
        Console.WriteLine($"Sigma position:  {last.StandardDeviation.Position} m");  // per ICRF axis
        Console.WriteLine($"RSS pos P50:     {last.RssPositionPercentiles.P50:F1} m");
        Console.WriteLine($"RSS pos P95:     {last.RssPositionPercentiles.P95:F1} m");
        Console.WriteLine($"RSS pos P99:     {last.RssPositionPercentiles.P99:F1} m");
        #endregion
    }

    // docs/tutorials/monte-carlo.md, "Dispersion growth over time"
    internal static void Block06(MonteCarloResult result)
    {
        #region docs/tutorials/monte-carlo.md:6
        // Inputs: result (MonteCarloResult)
        foreach (var stat in result.EpochStatistics)
        {
            Console.WriteLine($"{stat.Epoch}, " +
                              $"{stat.RssPositionPercentiles.P50:F1}, " +
                              $"{stat.RssPositionPercentiles.P95:F1}, " +
                              $"{stat.RssPositionPercentiles.P99:F1}");
        }
        #endregion
    }
}
