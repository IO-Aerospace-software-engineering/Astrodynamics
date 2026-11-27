// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/reference/opm.md, compiled (#705). Each csharp block of the page is the region
// "docs/reference/opm.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Ccsds;
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
using IO.Astrodynamics.Ssa;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Tle = IO.Astrodynamics.OrbitalParameters.Tle;

namespace DocumentationPageSamples.Reference;

/// <summary>The C# examples of docs/reference/opm.md, one method per block (#705).</summary>
internal static class OpmPage
{
    // docs/reference/opm.md, "Example"
    internal static void Block01(Spacecraft spacecraft, Time epoch, CelestialBody earth)
    {
        #region docs/reference/opm.md:1
        // Inputs: spacecraft (Spacecraft), epoch (Time), earth (CelestialBody)
        // Export spacecraft state to OPM, in the frame of its state
        var opm = spacecraft.ToOpm(epoch: epoch);
        opm.Save("state.opm.xml");

        // Load and convert back
        var loaded = Opm.Load("state.opm.xml");
        var sv = loaded.ToStateVector(earth);
        #endregion
    }

    // docs/reference/opm.md, "Building an OPM"
    internal static void Block02(DateTime epoch, double x, double y, double z, double vx, double vy, double vz)
    {
        #region docs/reference/opm.md:2
        // Inputs: epoch (DateTime), the epoch of the state; x, y, z (double, km) and vx, vy, vz (double, km/s), its components
        var header = CcsdsHeader.Create("My Organization");
        var metadata = new OpmMetadata("SAT-1", "2024-001A", "EARTH", "ICRF", "UTC");

        // State vector (required) - units: km, km/s
        var stateVector = new OpmStateVector(epoch, x, y, z, vx, vy, vz);

        // Keplerian elements (optional) - units: km, degrees
        var keplerianElements = OpmKeplerianElements.CreateWithTrueAnomaly(
            semiMajorAxis: 6878.137,   // km
            eccentricity: 0.001,
            inclination: 51.6,         // degrees
            raan: 120.0,               // degrees
            aop: 90.0,                 // degrees
            trueAnomaly: 45.0,         // degrees
            gm: 398600.4418);          // km³/s²

        // Covariance matrix (optional) - units: km², km²/s, km²/s²
        var covariance = new CovarianceMatrix(
            cxX: 1.0e-6, cyX: 0.0, cyY: 1.0e-6, czX: 0.0, czY: 0.0, czZ: 1.0e-6,
            cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
            cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
            czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9,
            referenceFrame: "ICRF");

        // Maneuver parameters (optional) - units: s, kg, km/s
        var maneuver = new OpmManeuverParameters(
            manEpochIgnition: epoch.AddHours(2),
            manDurationSeconds: 120.0,        // seconds
            manDeltaMass: -30.0,       // kg (negative = expelled)
            manRefFrame: "RSW",
            manDv1: 0.05,              // km/s (radial)
            manDv2: 0.0,               // km/s (along-track)
            manDv3: 0.01);             // km/s (cross-track)

        // User-defined parameters (optional)
        var userParams = new OpmUserDefinedParameters(
            new Dictionary<string, string>
            {
                ["MISSION_ID"] = "MISSION-001",
                ["OPERATOR"] = "Space Agency"
            });

        // Assemble OPM data
        var data = new OpmData(
            stateVector: stateVector,
            keplerianElements: keplerianElements,
            mass: 2000.0,                         // kg
            dragArea: 30.0,                       // m²
            dragCoefficient: 2.2,
            solarRadiationPressureArea: 35.0,     // m²
            solarRadiationCoefficient: 1.5,
            covariance: covariance,
            maneuvers: new[] { maneuver },
            userDefinedParameters: userParams);

        var opm = new Opm(header, metadata, data);
        opm.Save("complete_opm.xml");
        #endregion
    }

    // docs/reference/opm.md, "The maneuvers of a propagation"
    internal static void Block03(Spacecraft spacecraft)
    {
        #region docs/reference/opm.md:3
        // Inputs: spacecraft (Spacecraft), propagated with impulse maneuvers
        foreach (var maneuver in spacecraft.ExecutedManeuvers)
        {
            if (maneuver is ImpulseManeuver impulseManeuver && impulseManeuver.ThrustWindow.HasValue)
            {
                var opmManeuver = impulseManeuver.ToOpmManeuverParameters(
                    referenceFrame: "EME2000",
                    comments: new[] { "Orbit raising maneuver" });

                Console.WriteLine($"Epoch: {opmManeuver.ManEpochIgnition}");
                Console.WriteLine($"Duration: {opmManeuver.ManDurationSeconds} s");
                Console.WriteLine($"Delta-V: {opmManeuver.DeltaVMagnitude:F6} km/s");
                Console.WriteLine($"Delta-Mass: {opmManeuver.ManDeltaMass} kg");
            }
        }
        #endregion
    }
}
