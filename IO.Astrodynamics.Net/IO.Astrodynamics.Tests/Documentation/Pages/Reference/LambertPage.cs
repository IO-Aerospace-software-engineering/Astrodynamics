// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/reference/lambert.md, compiled (#705). Each csharp block of the page is the region
// "docs/reference/lambert.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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
using IO.Astrodynamics.TimeSystem.Frames;
using TLE = IO.Astrodynamics.OrbitalParameters.TLE.TLE;

namespace DocumentationPageSamples.Reference;

/// <summary>The C# examples of docs/reference/lambert.md, one method per block (#705).</summary>
internal static class LambertPage
{
    // docs/reference/lambert.md, "Example"
    internal static void Block01(StateVector departureState, StateVector arrivalState, CelestialBody earth)
    {
        #region docs/reference/lambert.md:1
        // Inputs: departureState and arrivalState (StateVector), earth (CelestialBody)
        var solver = new LambertSolver();
        var result = solver.Solve(
            false,             // isRetrograde: a prograde transfer
            departureState,    // initialOrbitalParameters
            arrivalState,      // the target's orbital parameters at arrival
            earth,             // centerOfMotion
            0);                // maxRevolution

        var solution = result.GetZeroRevolutionSolution();
        var transferOrbit = new StateVector(
            departureState.ToStateVector().Position, solution.V1,
            earth, departureState.Epoch, Frame.ICRF);

        Console.WriteLine($"Total Delta-V: {solution.DeltaV:F1} m/s");
        #endregion
    }
}
