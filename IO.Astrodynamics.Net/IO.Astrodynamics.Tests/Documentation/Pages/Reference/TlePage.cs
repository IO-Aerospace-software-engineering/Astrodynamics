// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/reference/tle.md, compiled (#705). Each csharp block of the page is the region
// "docs/reference/tle.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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
using Tle = IO.Astrodynamics.OrbitalParameters.TLE.Tle;

namespace DocumentationPageSamples.Reference;

/// <summary>The C# examples of docs/reference/tle.md, one method per block (#705).</summary>
internal static class TlePage
{
    // docs/reference/tle.md, "Constructor"
    internal static void Block01()
    {
        #region docs/reference/tle.md:1
        var tle = new Tle("ISS (ZARYA)",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        #endregion
    }

    // docs/reference/tle.md, "Numerical propagation of a TLE"
    internal static void Block02(Window window, Tle tle)
    {
        #region docs/reference/tle.md:2
        // Inputs: window (Window), tle (TLE)
        var start = window.StartDate.ToTdb();
        var earth = new CelestialBody(PlanetsAndMoons.Earth, Frame.ICRF, start,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10), new Nrlmsise00Model(SpaceWeather.Nominal));
        var sgp4 = tle.ToStateVector(start);
        var spacecraft = new Spacecraft(-1001, "ISS", 420000.0, new StateVector(sgp4.Position, sgp4.Velocity, earth, start, Frame.TEME), 450000.0,
            new Clock("iss", 65536), 1600.0, 2.2);
        spacecraft.Propagate(window, new PropagationOptions { AtmosphericDrag = true, SolarRadiationPressure = true });
        #endregion
    }

    // docs/reference/tle.md, "Numerical propagation of a TLE", the options
    internal static void Block03(Window window, Tle tle)
    {
        #region docs/reference/tle.md:3
        // Inputs: window (Window), tle (TLE)
        var spacecraft = new Spacecraft(-1002, "ISS", 420000.0, tle, 450000.0, new Clock("iss2", 65536), 1600.0, 2.2);
        spacecraft.Propagate(window, PropagationOptions.Default with
        {
            Geopotential = GeopotentialModelParameters.Egm2008(10),
            Atmosphere = new Nrlmsise00Model(SpaceWeather.Nominal),
            AtmosphericDrag = true,
            SolarRadiationPressure = true
        });
        #endregion
    }

    // docs/reference/tle.md, "Reading TLE text"
    internal static void Block04(string catalogPath)
    {
        #region docs/reference/tle.md:4
        // Inputs: catalogPath (string)
        var catalog = Tle.LoadAll(catalogPath);
        var iss = Tle.Parse("ISS (ZARYA)\n" +
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054\n" +
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        // The lines again, from the properties, without slicing them
        var again = Tle.Create(iss.ToMeanKeplerianElements(), iss.Name, iss.NoradCatalogId, iss.CosparId, iss.RevolutionNumberAtEpoch,
            iss.Classification, iss.BStar, iss.MeanMotionDot, iss.MeanMotionDDot,
            iss.ElementSetNumber);
        #endregion
    }
}
