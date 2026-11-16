// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/tutorials/mission-design.md, compiled (#705). Each csharp block of the page is the region
// "docs/tutorials/mission-design.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

/// <summary>The C# examples of docs/tutorials/mission-design.md, one method per block (#705).</summary>
internal static class MissionDesignPage
{
    // docs/tutorials/mission-design.md, "Lambert solver"
    internal static LambertSolution Block01(StateVector departure, StateVector arrival, CelestialBody earth)
    {
        #region docs/tutorials/mission-design.md:1
        // Inputs: departure and arrival (StateVector), earth (CelestialBody)
        var solver = new LambertSolver();

        // Solve for the transfer from departure to arrival position
        var result = solver.Solve(
            isRetrograde: false,   // prograde transfer
            departure,             // StateVector at departure
            arrival,               // StateVector at arrival
            earth,                 // central body
            maxRevolution: 0);     // zero full revolutions

        // Extract the zero-revolution solution
        var solution = result.GetZeroRevolutionSolution();

        Console.WriteLine($"Departure V: {solution.V1.Magnitude():F2} m/s");
        Console.WriteLine($"Arrival   V: {solution.V2.Magnitude():F2} m/s");
        #endregion
        return solution;
    }

    // docs/tutorials/mission-design.md, "Multi-revolution solutions"
    internal static LambertResult Block02(LambertSolver solver, StateVector departure, StateVector arrival, CelestialBody earth)
    {
        #region docs/tutorials/mission-design.md:2
        // Inputs: solver (LambertSolver), departure and arrival (StateVector), earth (CelestialBody)
        var result = solver.Solve(false, departure, arrival, earth, maxRevolution: 2);

        foreach (var sol in result.Solutions)
        {
            Console.WriteLine($"Revolutions: {sol.Revolutions} {sol.Branch}, " +
                              $"DV: {sol.DeltaV:F2} m/s");
        }
        #endregion
        return result;
    }

    // docs/tutorials/mission-design.md, "Launch window computation"
    internal static void Block03(LaunchSite launchSite, Site recoverySite, OrbitalParameters targetOrbit, Window searchWindow)
    {
        #region docs/tutorials/mission-design.md:3
        // Inputs: launchSite (LaunchSite), recoverySite (Site), targetOrbit (OrbitalParameters), searchWindow (Window)
        var launch = new Launch(
            launchSite,              // LaunchSite of the pad
            recoverySite,            // Site for abort/recovery
            targetOrbit,             // desired orbital parameters
            Constants.CivilTwilight, // minimum solar elevation for launch
            true);                   // ascending node launch

        var windows = launch.FindLaunchWindows(searchWindow);

        foreach (var w in windows)
        {
            Console.WriteLine($"Launch at {w.InertialAzimuth * Constants.Rad2Deg:F1} deg " +
                              $"from {w.Window.StartDate} to {w.Window.EndDate}");
        }
        #endregion
    }

    // docs/tutorials/mission-design.md, "Mission and Scenario"
    internal static async Task Block04(Time start, Time end, CelestialBody earth, CelestialBody moon, Spacecraft spacecraft)
    {
        #region docs/tutorials/mission-design.md:4
        // Inputs: start and end (Time), earth and moon (CelestialBody), spacecraft (Spacecraft)
        // Create mission
        var mission = new Mission("LunarExplorer");

        // Define scenario with time window
        var scenario = new Scenario("Phase1", mission, new Window(start, end));

        // Add celestial bodies to the environment: one per NAIF identifier (a second body of an identifier is ignored when its
        // models equal those of the first, and throws ArgumentException otherwise)
        scenario.AddCelestialItem(earth);
        scenario.AddCelestialItem(moon);

        // Add spacecraft (with maneuvers already configured)
        scenario.AddSpacecraft(spacecraft);

        // Run simulation
        var summary = await scenario.SimulateAsync(
            includeAtmosphericDrag: false,
            includeSolarRadiationPressure: false,
            TimeSpan.FromSeconds(1.0));
        #endregion
    }

    // docs/tutorials/mission-design.md, "Choosing the integrator"
    internal static async Task Block05(Scenario scenario)
    {
        #region docs/tutorials/mission-design.md:5
        // Inputs: scenario (Scenario)
        var summary = await scenario.SimulateAsync(
            includeAtmosphericDrag: false,
            includeSolarRadiationPressure: false,
            TimeSpan.FromSeconds(10.0),
            () => new RK78Integrator(absoluteTolerance: 1e-11, relativeTolerance: 1e-11));
        #endregion
    }

    // docs/tutorials/mission-design.md, "Multiple scenarios"
    internal static async Task Block06(Window nominalWindow, Spacecraft spacecraft, Window backupWindow, Spacecraft spacecraftBackup)
    {
        #region docs/tutorials/mission-design.md:6
        // Inputs: nominalWindow and backupWindow (Window), spacecraft and spacecraftBackup (Spacecraft)
        var mission = new Mission("TransferStudy");

        var nominalScenario = new Scenario("Nominal", mission, nominalWindow);
        nominalScenario.AddSpacecraft(spacecraft);

        var backupScenario = new Scenario("Backup", mission, backupWindow);
        backupScenario.AddSpacecraft(spacecraftBackup);

        // Simulate each independently
        var nominalResult = await nominalScenario.SimulateAsync(false, false, TimeSpan.FromSeconds(10));
        var backupResult = await backupScenario.SimulateAsync(false, false, TimeSpan.FromSeconds(10));
        #endregion
    }

    // docs/tutorials/mission-design.md, "Spacecraft rendezvous"
    internal static async Task Block07(OrbitalParameters targetOrbit, Engine engine, Spacecraft spacecraft, Mission mission, Window window, CelestialBody earth)
    {
        #region docs/tutorials/mission-design.md:7
        // Inputs: targetOrbit (OrbitalParameters), engine (Engine), spacecraft (Spacecraft), mission (Mission), window
        //   (Window), earth (CelestialBody)
        // Build the maneuver chain
        var maneuver1 = new PlaneAlignmentManeuver(
            new Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero,
            targetOrbit, engine);

        maneuver1
            .SetNextManeuver(new ApsidalAlignmentManeuver(
                new Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero,
                targetOrbit, engine))
            .SetNextManeuver(new PhasingManeuver(
                new Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero,
                targetOrbit, 3, engine))
            .SetNextManeuver(new ApogeeHeightManeuver(
                new Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero,
                targetOrbit, engine));

        // Arm the maneuver chain on the spacecraft
        spacecraft.SetStandbyManeuver(maneuver1);

        // Propagate --- maneuvers fire automatically via event detection
        var scenario = new Scenario("Rendezvous", mission, window);
        scenario.AddCelestialItem(earth);
        scenario.AddSpacecraft(spacecraft);
        var result = await scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(1));
        #endregion
    }

    // docs/tutorials/mission-design.md, "Adding attitude maneuvers"
    internal static void Block08(CelestialBody earth, Engine engine, Maneuver maneuver1, Spacecraft spacecraft)
    {
        #region docs/tutorials/mission-design.md:8
        // Inputs: earth (CelestialBody), engine (Engine), maneuver1 (Maneuver), spacecraft (Spacecraft)
        var attitude = new ProgradeAttitude(
            earth, new Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromMinutes(5), engine);

        attitude.SetNextManeuver(maneuver1);
        spacecraft.SetStandbyManeuver(attitude);
        #endregion
    }

    // docs/tutorials/mission-design.md, "Kernel-backed spacecraft"
    internal static void Block09(Time epoch, CelestialBody moon)
    {
        #region docs/tutorials/mission-design.md:9
        // Inputs: epoch (Time), moon (CelestialBody)
        var lro = new Spacecraft(
            naifId: -85,
            name: "LRO",
            epoch: new Time(2024, 6, 1, 12, 0, 0));

        // Query ephemeris directly from SPICE kernels
        var state = lro.GetEphemeris(epoch, moon, Frame.ICRF, Aberration.LT);
        #endregion
    }
}
