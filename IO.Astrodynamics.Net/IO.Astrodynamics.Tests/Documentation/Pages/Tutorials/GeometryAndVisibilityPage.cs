// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/tutorials/geometry-and-visibility.md, compiled (#705). Each csharp block of the page is the region
// "docs/tutorials/geometry-and-visibility.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

namespace DocumentationPageSamples.Tutorials;

/// <summary>The C# examples of docs/tutorials/geometry-and-visibility.md, one method per block (#705).</summary>
internal static class GeometryAndVisibilityPage
{
    // docs/tutorials/geometry-and-visibility.md, "Occultation and eclipse windows"
    internal static IEnumerable<Window> Block01(CelestialBody moon, Window searchWindow, CelestialBody sun, CelestialBody earth)
    {
        #region docs/tutorials/geometry-and-visibility.md:1
        // Inputs: moon, sun and earth (CelestialBody), searchWindow (Window)
        // Find lunar eclipses (the Earth occults the Moon as seen from the Sun)
        var lunarEclipses = sun.FindOccultationWindows(
            searchWindow,
            target: moon,
            front: earth,
            new OccultationSearchOptions
            {
                Step = TimeSpan.FromHours(1),
                TargetShape = ShapeType.Ellipsoid,
                FrontShape = ShapeType.Ellipsoid,
                OccultationType = OccultationType.Any,
                Aberration = Aberration.None
            });
        #endregion
        return lunarEclipses;
    }

    // docs/tutorials/geometry-and-visibility.md, "Spacecraft eclipse windows"
    internal static void Block02(CelestialBody sun, Window searchWindow, Spacecraft spacecraft, CelestialBody earth)
    {
        #region docs/tutorials/geometry-and-visibility.md:2
        // Inputs: sun and earth (CelestialBody), searchWindow (Window), spacecraft (Spacecraft)
        var eclipseWindows = spacecraft.FindOccultationWindows(
            searchWindow,
            target: sun,
            front: earth,
            new OccultationSearchOptions { Step = TimeSpan.FromMinutes(5) });
        #endregion
    }

    // docs/tutorials/geometry-and-visibility.md, "Distance constraints"
    internal static void Block03(CelestialBody earth, Window searchWindow, CelestialBody moon)
    {
        #region docs/tutorials/geometry-and-visibility.md:3
        // Inputs: earth and moon (CelestialBody), searchWindow (Window)
        // Find when the Moon is farther than 400,000 km from Earth
        var farMoonWindows = earth.FindDistanceWindows(
            searchWindow,
            moon,
            RelationalOperator.GreaterThan,
            400000000,          // meters
            new WindowSearchOptions { Step = TimeSpan.FromHours(24) });
        #endregion
    }

    // docs/tutorials/geometry-and-visibility.md, "Instrument field of view"
    internal static void Block04(Spacecraft spacecraft, Window window, CelestialBody earth)
    {
        #region docs/tutorials/geometry-and-visibility.md:4
        // Inputs: spacecraft (Spacecraft), window (Window), earth (CelestialBody)
        var visibilityWindows = spacecraft.Instruments.First()
            .FindFieldOfViewWindows(
                window,
                earth,
                new FieldOfViewSearchOptions
                {
                    Step = TimeSpan.FromSeconds(60),
                    Aberration = Aberration.LT,
                    TargetShape = ShapeType.Ellipsoid,
                    TargetFrame = earth.Frame
                });
        #endregion
    }

    // docs/tutorials/geometry-and-visibility.md, "Setting up an instrument"
    internal static void Block05(Spacecraft spacecraft)
    {
        #region docs/tutorials/geometry-and-visibility.md:5
        // Inputs: spacecraft (Spacecraft)
        spacecraft.AddCircularInstrument(
            naifId: -1001001,
            name: "Camera",
            model: "Imager",
            fieldOfView: 5.0 * Constants.Deg2Rad,       // half-angle
            boresight: Vector3.VectorZ,                 // +Z of the instrument frame
            refVector: Vector3.VectorX,
            orientation: new Vector3(0.0, Constants.HalfPi, 0.0));
        #endregion
    }

    // docs/tutorials/geometry-and-visibility.md, "Kernel-backed sites"
    internal static void Block06(CelestialBody earth)
    {
        #region docs/tutorials/geometry-and-visibility.md:6
        // Inputs: earth (CelestialBody)
        var goldstone = new Site(13, "DSS-13", earth);
        #endregion
    }

    // docs/tutorials/geometry-and-visibility.md, "Custom sites"
    internal static void Block07(CelestialBody earth)
    {
        #region docs/tutorials/geometry-and-visibility.md:7
        // Inputs: earth (CelestialBody)
        var mySite = new Site(100, "MySite", earth,
            new Planetodetic(
                -117.0 * Constants.Deg2Rad,   // longitude (radians)
                 34.0 * Constants.Deg2Rad,    // latitude  (radians)
                 1000.0));                    // altitude  (meters)
        #endregion
    }

    // docs/tutorials/geometry-and-visibility.md, "Horizontal coordinates"
    internal static void Block08(Site goldstone, Time epoch, CelestialBody moon)
    {
        #region docs/tutorials/geometry-and-visibility.md:8
        // Inputs: goldstone (Site), epoch (Time), moon (CelestialBody)
        var horizontal = goldstone.GetHorizontalCoordinates(
            epoch, moon, Aberration.LT);

        Console.WriteLine($"Azimuth:   {horizontal.Azimuth * Constants.Rad2Deg:F2} deg");
        Console.WriteLine($"Elevation: {horizontal.Elevation * Constants.Rad2Deg:F2} deg");
        Console.WriteLine($"Range:     {horizontal.Range / 1000.0:F1} km");
        #endregion
    }

    // docs/tutorials/geometry-and-visibility.md, "Site visibility windows"
    internal static void Block09(Spacecraft spacecraft, Window searchWindow, Site mySite)
    {
        #region docs/tutorials/geometry-and-visibility.md:9
        // Inputs: spacecraft (Spacecraft), searchWindow (Window), mySite (Site)
        var visibleWindows = mySite.FindHorizontalWindows(
            searchWindow,
            spacecraft,
            HorizontalCoordinate.Elevation,
            RelationalOperator.GreaterThan,
            10.0 * Constants.Deg2Rad,
            new WindowSearchOptions { Step = TimeSpan.FromMinutes(1), Aberration = Aberration.LT });
        #endregion
    }

    // docs/tutorials/geometry-and-visibility.md, "TLE-based satellite tracking"
    internal static void Block10(Site mySite, Time epoch)
    {
        #region docs/tutorials/geometry-and-visibility.md:10
        // Inputs: mySite (Site), epoch (Time)
        // Parse TLE
        var tle = new Tle(
            "ISS (ZARYA)",
            "1 25544U 98067A   24100.50000000  .00016717  00000-0  10270-3 0  9005",
            "2 25544  51.6416 247.4627 0006703 130.5360 325.0288 15.49750667  1007");

        // Create spacecraft from TLE
        var iss = new Spacecraft(-25544, "ISS", 420000.0, tle, 450000.0, new Clock("ISS_CLK", 65536));

        // Track from ground site
        var coords = mySite.GetHorizontalCoordinates(epoch, iss, Aberration.None);
        #endregion
    }
}
