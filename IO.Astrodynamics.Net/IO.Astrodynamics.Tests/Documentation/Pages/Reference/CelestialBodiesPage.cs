// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/reference/celestial-bodies.md, compiled (#705). Each csharp block of the page is the region
// "docs/reference/celestial-bodies.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

/// <summary>The C# examples of docs/reference/celestial-bodies.md, one method per block (#705).</summary>
internal static class CelestialBodiesPage
{
    // docs/reference/celestial-bodies.md, "Example"
    internal static void Block01(Time epoch)
    {
        #region docs/reference/celestial-bodies.md:1
        // Inputs: epoch (Time)
        var earth = PlanetsAndMoons.EarthBody;
        var moon = new CelestialBody(PlanetsAndMoons.Moon);
        var state = moon.GetEphemeris(epoch, earth, Frame.ICRF, Aberration.None);
        #endregion
    }

    // docs/reference/celestial-bodies.md, "Atmospheric Context"
    internal static double Block02(Time epoch)
    {
        #region docs/reference/celestial-bodies.md:2
        // Inputs: epoch (Time)
        var earth = PlanetsAndMoons.EarthBody;
        var context = AtmosphericContext.FromPlanetodetic(
            new Planetodetic(longitude: 2.35 * Constants.Deg2Rad, latitude: 48.85 * Constants.Deg2Rad, altitude: 400e3), epoch);
        double density = earth.GetAirDensity(context); // kg/m³, NRLMSISE-00 at nominal space weather
        #endregion
        return density;
    }
}
