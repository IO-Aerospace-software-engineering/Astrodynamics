// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/tutorials/tle-and-omm.md, compiled (#705). Each csharp block of the page is the region
// "docs/tutorials/tle-and-omm.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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
using TleFitOptions = IO.Astrodynamics.OrbitalParameters.TleFitOptions;

namespace DocumentationPageSamples.Tutorials;

/// <summary>The C# examples of docs/tutorials/tle-and-omm.md, one method per block (#705).</summary>
internal static class TleAndOmmPage
{
    // docs/tutorials/tle-and-omm.md, "Parsing a TLE"
    internal static void Block01()
    {
        #region docs/tutorials/tle-and-omm.md:1
        SpiceApi.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));

        var tle = new Tle("ISS (ZARYA)",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        Console.WriteLine($"Name:         {tle.Name}");
        Console.WriteLine($"NORAD ID:     {tle.NoradCatalogId}");
        Console.WriteLine($"Epoch:        {tle.Epoch}");
        Console.WriteLine($"Inclination:  {tle.MeanInclination * Constants.Rad2Deg:F4} deg");  // the mean inclination of line 2
        #endregion
    }

    // docs/tutorials/tle-and-omm.md, "Mean vs Osculating Elements"
    internal static void Block02(Tle tle)
    {
        #region docs/tutorials/tle-and-omm.md:2
        // Inputs: tle (TLE)
        // Correct: osculating state vector at TLE epoch
        var sv = tle.ToOsculating();

        // Correct: mean Keplerian elements (for display or comparison only)
        var meanKep = tle.ToMeanKeplerianElements();

        // Correct: SGP4-propagated state vector at a specific time
        var svAtTime = tle.ToStateVector(new Time(2021, 1, 21, 12, 0, 0, TimeScale.TDB));
        #endregion
    }

    // docs/tutorials/tle-and-omm.md, "SGP4 Propagation"
    internal static void Block03(Tle tle)
    {
        #region docs/tutorials/tle-and-omm.md:3
        // Inputs: tle (TLE)
        var futureEpoch = new Time(2021, 1, 25, 0, 0, 0, TimeScale.TDB);
        var futureSv = tle.ToStateVector(futureEpoch);

        Console.WriteLine($"Position at {futureEpoch}: {futureSv.Position}");
        Console.WriteLine($"Velocity at {futureEpoch}: {futureSv.Velocity}");
        #endregion
    }

    // docs/tutorials/tle-and-omm.md, "Numerical Propagation of a TLE"
    internal static void Block04(Tle tle)
    {
        #region docs/tutorials/tle-and-omm.md:4
        // Inputs: tle (TLE)
        var window = new Window(tle.Epoch, tle.Epoch.AddHours(6.0));
        var start = window.StartDate.ToTdb();
        var earth = new CelestialBody(PlanetsAndMoons.Earth, Frame.ICRF, start,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
            new Nrlmsise00Model(SpaceWeather.Nominal));
        var sgp4AtStart = tle.ToStateVector(start);
        var numerical = new Spacecraft(-1001, "ISS-NUM", 420000.0, new StateVector(sgp4AtStart.Position, sgp4AtStart.Velocity, earth, start, Frame.TEME), 450000.0,
            new Clock("issnum", 65536), 1600.0, 2.2);
        var solution = numerical.Propagate(window, new PropagationOptions { AtmosphericDrag = true });

        var sampled = new Spacecraft(-1002, "ISS-SGP4", 420000.0, tle, 450000.0, new Clock("issgp4", 65536));
        new TlePropagator(window, sampled, TimeSpan.FromSeconds(60.0)).Propagate();

        var end = solution.StateVectors[^1];
        var sgp4AtEnd = tle.ToStateVector(end.Epoch).ToFrame(Frame.ICRF);
        Console.WriteLine($"Numerical - SGP4 after 6 h: {(end.Position - sgp4AtEnd.Position).Magnitude():F0} m");
        #endregion
    }

    // docs/tutorials/tle-and-omm.md, "Creating a TLE from a State Vector"
    internal static void Block05(StateVector sv)
    {
        #region docs/tutorials/tle-and-omm.md:5
        // Inputs: sv (StateVector)
        var newTle = sv.ToTle("ISS (ZARYA)", 25544, "98067A",
            new TleFitOptions { RevolutionNumberAtEpoch = 25703 });
        #endregion
    }

    // docs/tutorials/tle-and-omm.md, "Creating a TLE from a State Vector"
    internal static void Block06(StateVector sv)
    {
        #region docs/tutorials/tle-and-omm.md:6
        // Inputs: sv (StateVector)
        var alpha5Tle = sv.ToTle("MY_SAT", 275544, "24001A");

        Console.WriteLine(alpha5Tle.Line1.Substring(2, 5));  // T5544
        Console.WriteLine(alpha5Tle.NoradCatalogId);         // 275544
        #endregion
    }

    // docs/tutorials/tle-and-omm.md, "OMM: Orbit Mean-Elements Message"
    internal static void Block07()
    {
        #region docs/tutorials/tle-and-omm.md:7
        var omm = Omm.Load("satellite.omm");

        Console.WriteLine($"Object name: {omm.ObjectName}");
        Console.WriteLine($"Object ID:   {omm.ObjectId}");
        Console.WriteLine($"Epoch:       {omm.Epoch}");
        #endregion
    }

    // docs/tutorials/tle-and-omm.md, "OMM to TLE"
    internal static void Block08(Omm omm)
    {
        #region docs/tutorials/tle-and-omm.md:8
        // Inputs: omm (Omm)
        if (omm.IsTleCompatible)
        {
            var tleFromOmm = omm.ToTle();
            Console.WriteLine($"TLE line 1: {tleFromOmm.Line1}");
            Console.WriteLine($"TLE line 2: {tleFromOmm.Line2}");
        }
        #endregion
    }

    // docs/tutorials/tle-and-omm.md, "TLE to OMM"
    internal static void Block09(Tle tle)
    {
        #region docs/tutorials/tle-and-omm.md:9
        // Inputs: tle (TLE)
        var newOmm = tle.ToOmm(originator: "My Organization");
        #endregion
    }

    // docs/tutorials/tle-and-omm.md, "OPM: Orbit Parameter Message"
    internal static void Block10()
    {
        #region docs/tutorials/tle-and-omm.md:10
        var earth = PlanetsAndMoons.EarthBody;

        var opm = Opm.Load("spacecraft.opm");
        var stateVector = opm.ToStateVector(earth);

        Console.WriteLine($"Position: {stateVector.Position}");
        Console.WriteLine($"Velocity: {stateVector.Velocity}");
        Console.WriteLine($"Epoch:    {stateVector.Epoch}");
        #endregion
    }

    // docs/tutorials/tle-and-omm.md, "Tracking from a ground station"
    internal static void Block11()
    {
        #region docs/tutorials/tle-and-omm.md:11
        var earth = PlanetsAndMoons.EarthBody;

        // Parse ISS TLE
        var issTle = new Tle(
            "ISS (ZARYA)",
            "1 25544U 98067A   24153.17509025  .00020162  00000+0  35104-3 0  9990",
            "2 25544  51.6393  34.6631 0005642 260.2910 238.1766 15.50732314456064"
        );

        // Create spacecraft from TLE
        var clock = new Clock("ISSClock", 65536);
        var iss = new Spacecraft(-25544, "ISS", 420000.0, issTle, 450000.0, clock);

        // Track from a ground station
        var goldstone = new Site(13, "DSS-13", earth);

        var trackStart = new Time(2024, 6, 3, TimeScale.TDB);
        var trackEnd = trackStart.AddDays(1);

        // Get ISS position relative to ground station over time
        Console.WriteLine("ISS tracking from Goldstone:");
        for (var t = trackStart; t < trackEnd; t = t.AddMinutes(10))
        {
            var horizontal = goldstone.GetHorizontalCoordinates(t, iss, Aberration.LT);

            if (horizontal.Elevation > 0)  // Above horizon
            {
                Console.WriteLine($"{t.ToUtc()}: " +
                    $"Az={horizontal.Azimuth * Constants.Rad2Deg:F1}° " +
                    $"El={horizontal.Elevation * Constants.Rad2Deg:F1}° " +
                    $"Range={horizontal.Range / 1000:F0} km");
            }
        }

        // Find visibility windows from ground station: the distance of the station from the ISS (the observer)
        var visibilityWindows = iss.FindDistanceWindows(
            new Window(trackStart, trackEnd),
            goldstone,
            RelationalOperator.LessThan,
            2000000,  // Within 2000 km
            new WindowSearchOptions { Step = TimeSpan.FromMinutes(1), Aberration = Aberration.LT }
        );
        #endregion
    }
}
