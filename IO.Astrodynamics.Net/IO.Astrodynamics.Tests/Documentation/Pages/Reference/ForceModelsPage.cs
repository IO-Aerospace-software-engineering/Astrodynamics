// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/reference/force-models.md, compiled (#705). Each csharp block of the page is the region
// "docs/reference/force-models.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

/// <summary>The C# examples of docs/reference/force-models.md, one method per block (#705).</summary>
internal static class ForceModelsPage
{
    // docs/reference/force-models.md, "GeopotentialGravitationalField"
    internal static void Block01(Time epoch)
    {
        #region docs/reference/force-models.md:1
        // Inputs: epoch (Time)
        // A model other than EGM2008, in the same file format, with its own constants
        var mars = new CelestialBody(PlanetsAndMoons.MARS, Frame.ICRF, epoch,
            new GeopotentialModelParameters("mars_model", 20, 4.282837362069909e13, 3396000.0));
        #endregion
    }

    // docs/reference/force-models.md, "The central term with the GM of the model"
    internal static void Block02(Time epoch)
    {
        #region docs/reference/force-models.md:2
        // Inputs: epoch (Time)
        // EGM2008 with its own GM in the central term too, as GMAT evaluates EGM2008 from a .cof header
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10)
            {
                CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
            });
        #endregion
    }

    // docs/reference/force-models.md, "Example"
    internal static void Block03()
    {
        #region docs/reference/force-models.md:3
        var earth = new CelestialBody(PlanetsAndMoons.EARTH,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
            albedo: 0.306, thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);
        #endregion
    }
}
