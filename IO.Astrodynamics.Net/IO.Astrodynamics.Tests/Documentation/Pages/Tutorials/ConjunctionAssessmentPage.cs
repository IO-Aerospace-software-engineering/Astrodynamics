// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/tutorials/conjunction-assessment.md, compiled (#705). Each csharp block of the page is the region
// "docs/tutorials/conjunction-assessment.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

namespace DocumentationPageSamples.Tutorials;

/// <summary>The C# examples of docs/tutorials/conjunction-assessment.md, one method per block (#705).</summary>
internal static class ConjunctionAssessmentPage
{
    // docs/tutorials/conjunction-assessment.md, "Setting up spacecraft with covariance"
    internal static void Block01(CelestialBody earth, Time epoch)
    {
        #region docs/tutorials/conjunction-assessment.md:1
        // Inputs: earth (CelestialBody), epoch (Time)
        // Build a 6x6 covariance matrix (diagonal for simplicity)
        var protectedCov = Matrix.Diagonal(
            25.0, 25.0, 25.0,    // sigma_x^2, sigma_y^2, sigma_z^2 (m^2)
            0.01, 0.01, 0.01);   // sigma_vx^2, sigma_vy^2, sigma_vz^2 (m^2/s^2)

        var protectedState = new StateVector(
            new Vector3(6800000.0, 0.0, 0.0),
            new Vector3(0.0, 7656.2204182967143, 0.0),
            earth, epoch, Frame.ICRF, protectedCov);

        var protectedSpacecraft = new Spacecraft(
            -1001, "Protected", 120.0, 150.0,
            new Clock("Protected_CLK", 256), protectedState,
            hardBodyRadius: 8.0);
        #endregion
    }

    // docs/tutorials/conjunction-assessment.md, "Setting up spacecraft with covariance"
    internal static void Block02(CelestialBody earth, Time epoch)
    {
        #region docs/tutorials/conjunction-assessment.md:2
        // Inputs: earth (CelestialBody), epoch (Time)
        var secondaryCov = Matrix.Diagonal(50.0, 50.0, 50.0, 0.02, 0.02, 0.02);

        var secondaryState = new StateVector(
            new Vector3(6800500.0, 100.0, 50.0),
            new Vector3(-50.0, 7656.0, 10.0),
            earth, epoch, Frame.ICRF, secondaryCov);

        var secondarySpacecraft = new Spacecraft(
            -2001, "Secondary", 80.0, 100.0,
            new Clock("Secondary_CLK", 256), secondaryState,
            hardBodyRadius: 3.0);
        #endregion
    }

    // docs/tutorials/conjunction-assessment.md, "Basic conjunction analysis"
    internal static void Block03(Spacecraft protectedSpacecraft, Spacecraft secondarySpacecraft, Time epoch)
    {
        #region docs/tutorials/conjunction-assessment.md:3
        // Inputs: protectedSpacecraft and secondarySpacecraft (Spacecraft), epoch (Time)
        var encounter = ConjunctionAssessment.Analyze(
            new ProtectedSpacecraftProfile(protectedSpacecraft),
            secondarySpacecraft,
            new Window(epoch, epoch.AddMinutes(10.0)));

        Console.WriteLine($"TCA:            {encounter.EncounterState.Epoch}");
        Console.WriteLine($"Miss distance:  {encounter.EncounterState.MissDistanceMeters:F1} m");
        Console.WriteLine($"Probability:    {encounter.CollisionRisk.ProbabilityOfCollision:E3}");
        Console.WriteLine($"Relative speed: {encounter.EncounterState.RelativeState.RelativeVelocityInertial.Magnitude():F1} m/s");
        #endregion
    }

    // docs/tutorials/conjunction-assessment.md, "Analyzing all encounters"
    internal static void Block04(Spacecraft protectedSpacecraft, Spacecraft secondarySpacecraft, Time epoch)
    {
        #region docs/tutorials/conjunction-assessment.md:4
        // Inputs: protectedSpacecraft and secondarySpacecraft (Spacecraft), epoch (Time)
        var allEncounters = ConjunctionAssessment.AnalyzeAll(
            new ProtectedSpacecraftProfile(protectedSpacecraft),
            secondarySpacecraft,
            new Window(epoch, epoch.AddHours(24.0)));

        foreach (var enc in allEncounters)
        {
            Console.WriteLine($"TCA: {enc.EncounterState.Epoch}, Miss: {enc.EncounterState.MissDistanceMeters:F1} m, " +
                              $"Pc: {enc.CollisionRisk.ProbabilityOfCollision:E3}");
        }
        #endregion
    }

    // docs/tutorials/conjunction-assessment.md, "Ranked screening"
    internal static void Block05(Spacecraft protectedSpacecraft, Spacecraft secondary1, Spacecraft secondary2, Spacecraft secondary3, Window window)
    {
        #region docs/tutorials/conjunction-assessment.md:5
        // Inputs: protectedSpacecraft, secondary1, secondary2 and secondary3 (Spacecraft), window (Window)
        var encounters = ConjunctionAssessment.Screen(
            new ProtectedSpacecraftProfile(protectedSpacecraft),
            new ILocalizable[] { secondary1, secondary2, secondary3 },
            window,
            new ScreeningOptions
            {
                MaxMissDistanceMeters = 10000.0,
                MaxResults = 20
            });

        foreach (var enc in encounters)
        {
            Console.WriteLine($"{enc.SecondaryObject.Name}: " +
                              $"Miss={enc.EncounterState.MissDistanceMeters:F1} m, " +
                              $"Pc={enc.CollisionRisk.ProbabilityOfCollision:E3}");
        }
        #endregion
    }

    // docs/tutorials/conjunction-assessment.md, "Reusing propagated trajectories"
    internal static void Block06(TimeSpan step, Spacecraft protectedSpacecraft, Window window, CelestialItem[] perturbingBodies, Spacecraft secondarySpacecraft)
    {
        #region docs/tutorials/conjunction-assessment.md:6
        // Inputs: step (TimeSpan), protectedSpacecraft and secondarySpacecraft (Spacecraft), window (Window),
        //   perturbingBodies (CelestialItem[])
        var protectedTrajectory = protectedSpacecraft.Propagate(window, perturbingBodies, false, false, step);
        var secondaryTrajectory = secondarySpacecraft.Propagate(window, perturbingBodies, false, false, step);

        var encounter = ConjunctionAssessment.Analyze(
            new ProtectedSpacecraftProfile(protectedSpacecraft), protectedTrajectory,
            secondarySpacecraft, secondaryTrajectory,
            window);
        #endregion
    }

    // docs/tutorials/conjunction-assessment.md, "With the propagated covariance"
    internal static void Block07(Window window, CelestialItem[] perturbingBodies, TimeSpan step, Spacecraft protectedSpacecraft, Spacecraft secondarySpacecraft)
    {
        #region docs/tutorials/conjunction-assessment.md:7
        // Inputs: window (Window), perturbingBodies (CelestialItem[]), step (TimeSpan), protectedSpacecraft and
        //   secondarySpacecraft (Spacecraft)
        var integrator = new RK78Integrator(1e-11, 1e-11);
        protectedSpacecraft.Propagate(window, perturbingBodies, integrator, true, true, step, null);
        secondarySpacecraft.Propagate(window, perturbingBodies, new RK78Integrator(1e-11, 1e-11), true, true, step, null);

        var encounter = ConjunctionAssessment.Analyze(
            new ProtectedSpacecraftProfile(protectedSpacecraft), secondarySpacecraft, window,
            new ConjunctionAnalysisOptions
            {
                // The default is the covariance without process noise: add the noise and the consider parameters you trust
                CovariancePropagation = new CovariancePropagationOptions
                {
                    Mode = CovariancePropagationMode.Covariance,
                    ProcessNoise = new RtnProcessNoise(1e-12, 1e-11, 1e-12),
                    ConsiderParameters = new ConsiderParameters(dragCoefficientSigma: 0.2)
                },

                // Off by default: twelve propagations of the state transition matrix per participant
                ComputeNonlinearityIndex = true
            });

        var source = encounter.EncounterState.ProtectedCovariance;
        Console.WriteLine($"{source.Source}, process noise {source.ProcessNoiseApplied}, ν {source.NonlinearityIndex:E2}");
        #endregion
    }

    // docs/tutorials/conjunction-assessment.md, "Avoidance trade study"
    internal static void Block08(EncounterCase encounter)
    {
        #region docs/tutorials/conjunction-assessment.md:8
        // Inputs: encounter (EncounterCase)
        var options = ConjunctionAssessment.EvaluateAvoidance(
            encounter,
            new AvoidanceSearchOptions
            {
                LeadTimes = new[]
                {
                    TimeSpan.FromHours(1),
                    TimeSpan.FromHours(3),
                    TimeSpan.FromHours(6)
                },
                DeltaVMagnitudesMetersPerSecond = new[] { 0.05, 0.10, 0.25 }
            });

        foreach (var opt in options)
        {
            var after = opt.PostManeuverEncounter;
            Console.WriteLine($"Burn at {opt.BurnEpoch}, " +
                              $"DV={opt.DeltaVInertial.Magnitude():F3} m/s => " +
                              $"Miss={after.EncounterState.MissDistanceMeters:F0} m, " +
                              $"Pc={after.CollisionRisk.ProbabilityOfCollision:E3}");
        }
        #endregion
    }

    // docs/tutorials/conjunction-assessment.md, "Exporting a CDM"
    internal static void Block09(EncounterCase encounter)
    {
        #region docs/tutorials/conjunction-assessment.md:9
        // Inputs: encounter (EncounterCase)
        var cdm = encounter.ToCdm(new CdmExportOptions
        {
            Originator = "MyOpsCenter",
            MessageId = "CDM-001"
        });

        // Write to XML file
        cdm.WriteToFile("encounter.cdm.xml");
        #endregion
    }

    // docs/tutorials/conjunction-assessment.md, "Validating a CDM"
    internal static void Block10()
    {
        #region docs/tutorials/conjunction-assessment.md:10
        var validation = Cdm.ValidateSchema("encounter.cdm.xml");

        if (validation.IsValid)
            Console.WriteLine("CDM is schema-valid.");
        else
            foreach (var error in validation.Errors)
                Console.WriteLine($"Validation error: {error.Message}");
        #endregion
    }

    // docs/tutorials/conjunction-assessment.md, "Recommended workflow"
    internal static void Block11(ProtectedSpacecraftProfile profile, IEnumerable<ILocalizable> catalog, Window window, ScreeningOptions screeningOptions)
    {
        #region docs/tutorials/conjunction-assessment.md:11
        // Inputs: profile (ProtectedSpacecraftProfile), catalog (IEnumerable<ILocalizable>), window (Window),
        //   screeningOptions (ScreeningOptions)
        var encounters = ConjunctionAssessment.Screen(
            profile, catalog, window, screeningOptions);
        #endregion
    }

    // docs/tutorials/conjunction-assessment.md, "Recommended workflow"
    internal static void Block12(ProtectedSpacecraftProfile profile, Spacecraft highPrioritySecondary, Window refinedWindow)
    {
        #region docs/tutorials/conjunction-assessment.md:12
        // Inputs: profile (ProtectedSpacecraftProfile), highPrioritySecondary (Spacecraft), refinedWindow (Window)
        var details = ConjunctionAssessment.Analyze(
            profile, highPrioritySecondary, refinedWindow);
        #endregion
    }

    // docs/tutorials/conjunction-assessment.md, "Recommended workflow"
    internal static void Block13(EncounterCase details, AvoidanceSearchOptions avoidanceOptions)
    {
        #region docs/tutorials/conjunction-assessment.md:13
        // Inputs: details (EncounterCase), avoidanceOptions (AvoidanceSearchOptions)
        var trades = ConjunctionAssessment.EvaluateAvoidance(
            details, avoidanceOptions);
        #endregion
    }

    // docs/tutorials/conjunction-assessment.md, "Recommended workflow"
    internal static void Block14(EncounterCase details, CdmExportOptions exportOptions)
    {
        #region docs/tutorials/conjunction-assessment.md:14
        // Inputs: details (EncounterCase), exportOptions (CdmExportOptions)
        var cdm = details.ToCdm(exportOptions);
        cdm.WriteToFile("cdm_report.xml");
        #endregion
    }
}
