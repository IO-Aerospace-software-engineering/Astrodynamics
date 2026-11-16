// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/tutorials/ephemeris-queries.md, compiled (#705). Each csharp block of the page is the region
// "docs/tutorials/ephemeris-queries.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
// give the inputs of a block that continues an earlier one, as the comment opening the block names them
// ("// Inputs: ..."). DocumentationPagesExamplesTests checks that each block equals its region (the block's using
// directives aside, which are this file's) and runs the blocks that need only Data/SolarSystem. The namespace is outside
// IO.Astrodynamics, as an application's, so a name resolves here as it does for a reader of the page. Do not edit a
// region alone: change the page and copy the block.

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

/// <summary>The C# examples of docs/tutorials/ephemeris-queries.md, one method per block (#705).</summary>
internal static class EphemerisQueriesPage
{
    // docs/tutorials/ephemeris-queries.md, "Prerequisites"
    internal static void Block01()
    {
        #region docs/tutorials/ephemeris-queries.md:1
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));
        #endregion
    }

    // docs/tutorials/ephemeris-queries.md, "Setting Up Bodies and Epochs"
    internal static void Block02()
    {
        #region docs/tutorials/ephemeris-queries.md:2
        var earth = PlanetsAndMoons.EARTH_BODY;
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var sun = new CelestialBody(Stars.Sun);
        var epoch = new Time(2024, 6, 21, 12, 0, 0);
        #endregion
    }

    // docs/tutorials/ephemeris-queries.md, "Single-Epoch Query"
    internal static void Block03(CelestialBody earth, Time epoch, CelestialBody sun)
    {
        #region docs/tutorials/ephemeris-queries.md:3
        // Inputs: earth and sun (CelestialBody), epoch (Time)
        var stateVector = earth.GetEphemeris(epoch, sun, Frame.ICRF, Aberration.None)
            .ToStateVector();

        Console.WriteLine($"Position: {stateVector.Position} m");
        Console.WriteLine($"Velocity: {stateVector.Velocity} m/s");
        #endregion
    }

    // docs/tutorials/ephemeris-queries.md, "Time Series"
    internal static void Block04(Time epoch, CelestialBody moon, CelestialBody earth)
    {
        #region docs/tutorials/ephemeris-queries.md:4
        // Inputs: epoch (Time), moon and earth (CelestialBody)
        var window = new Window(epoch, epoch.AddDays(1));
        var states = moon.GetEphemeris(window, earth, Frame.ICRF, Aberration.LT,
            TimeSpan.FromHours(1));

        foreach (var sv in states)
        {
            Console.WriteLine($"{sv.Epoch}  range = {sv.ToStateVector().Position.Magnitude():F0} m");
        }
        #endregion
    }

    // docs/tutorials/ephemeris-queries.md, "Aberration Corrections"
    internal static void Block05(CelestialBody earth, Time epoch, CelestialBody moon)
    {
        #region docs/tutorials/ephemeris-queries.md:5
        // Inputs: earth and moon (CelestialBody), epoch (Time)
        var geometric = earth.GetEphemeris(epoch, moon, Frame.ICRF, Aberration.None);
        var apparent = earth.GetEphemeris(epoch, moon, Frame.ICRF, Aberration.LTS);
        #endregion
    }

    // docs/tutorials/ephemeris-queries.md, "Angular Separation"
    internal static void Block06(CelestialBody earth, Time epoch, CelestialBody moon, CelestialBody sun)
    {
        #region docs/tutorials/ephemeris-queries.md:6
        // Inputs: earth, moon and sun (CelestialBody), epoch (Time)
        double angSep = earth.AngularSeparation(epoch, moon, sun, Aberration.None);
        Console.WriteLine($"Moon-Sun angular separation: {angSep * Constants.Rad2Deg:F4} deg");
        #endregion
    }
}
