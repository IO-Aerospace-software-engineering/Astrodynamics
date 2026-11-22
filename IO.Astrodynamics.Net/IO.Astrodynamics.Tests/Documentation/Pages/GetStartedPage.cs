// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/get-started.md, compiled (#705). Each csharp block of the page is the region
// "docs/get-started.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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
using IO.Astrodynamics.OrbitalParameters.TLE;
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

namespace DocumentationPageSamples;

/// <summary>The C# examples of docs/get-started.md, one method per block (#705).</summary>
internal static class GetStartedPage
{
    // docs/get-started.md, "Load SPICE Kernels"
    internal static void Block01()
    {
        #region docs/get-started.md:1
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));
        #endregion
    }

    // docs/get-started.md, "Load SPICE Kernels"
    internal static void Block02()
    {
        #region docs/get-started.md:2
        SpiceAPI.Instance.LoadKernels(new FileInfo("de440.bsp"));
        #endregion
    }

    // docs/get-started.md, "Compute A Basic Ephemeris"
    internal static void Block03()
    {
        #region docs/get-started.md:3
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));

        var earth = PlanetsAndMoons.EARTH_BODY;
        var sun = new CelestialBody(Stars.Sun);
        var epoch = new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB);

        var stateVector = earth.GetEphemeris(epoch, sun, Frame.ICRF, Aberration.None);

        Console.WriteLine($"Position: {stateVector.Position}");
        Console.WriteLine($"Velocity: {stateVector.Velocity}");
        #endregion
    }

    // docs/get-started.md, "Create An Orbit"
    internal static void Block04()
    {
        #region docs/get-started.md:4
        var earth = PlanetsAndMoons.EARTH_BODY;
        var epoch = new Time(2024, 1, 1, 0, 0, 0, TimeScale.TDB);

        var orbit = new KeplerianElements(
            7000000.0,                 // semi-major axis, m
            0.001,                     // eccentricity
            51.6 * Constants.Deg2Rad,  // inclination, rad
            100.0 * Constants.Deg2Rad, // right ascension of the ascending node, rad
            90.0 * Constants.Deg2Rad,  // argument of periapsis, rad
            0.0,                       // mean anomaly, rad
            observer: earth,
            epoch: epoch,
            frame: Frame.ICRF
        );

        Console.WriteLine($"Period: {orbit.Period().TotalHours:F2} hours");
        #endregion
    }

    // docs/get-started.md, "Parse A TLE"
    internal static void Block05()
    {
        #region docs/get-started.md:5
        var tle = new TLE("ISS (ZARYA)",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        var epoch = new Time(2021, 1, 21, 12, 0, 0, TimeScale.TDB);
        var sv = tle.ToOsculating(epoch);
        #endregion
    }

    // docs/get-started.md, "Propagate An Orbit"
    internal static void Block06(StateVector orbit, Time epoch, CelestialBody earth)
    {
        #region docs/get-started.md:6
        // Inputs: orbit (StateVector), epoch (Time), earth (CelestialBody)
        var spacecraft = new Spacecraft(
            -1001,                         // NAIF id
            "MySat",
            500.0,                         // mass, kg
            1000.0,                        // maximum operating mass, kg
            new Clock("MySatClock", 256),  // on-board clock, 256 ticks per second
            orbit);

        var window = new Window(epoch, epoch.AddHours(2));

        using var propagator = new CentralBodyPropagator(
            window,
            spacecraft,
            new VVIntegrator(TimeSpan.FromSeconds(10.0)),
            new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
            includeAtmosphericDrag: false,
            includeSolarRadiationPressure: false,
            deltaT: TimeSpan.FromSeconds(60.0));

        var solution = propagator.Propagate();
        var (position, velocity) = solution.InterpolateAt(window.EndDate);
        #endregion
    }

    // docs/get-started.md, "Propagate An Orbit"
    internal static void Block07(Time epoch, Spacecraft spacecraft)
    {
        #region docs/get-started.md:7
        // Inputs: epoch (Time), spacecraft (Spacecraft)
        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-10,
            relativeTolerance: 1e-10,
            initialStepSize: 30.0);

        var window = new Window(epoch, epoch.AddHours(2));

        using var propagator = new CentralBodyPropagatorBuilder(
                window, spacecraft, integrator, TimeSpan.FromSeconds(60.0))
            .WithPerturbingBody(Stars.SUN_BODY)
            .WithPerturbingBody(PlanetsAndMoons.MOON_BODY)
            .IncludeAtmosphericDrag()
            .IncludeSolarRadiationPressure()
            .Build();

        var solution = propagator.Propagate();
        var (position, velocity) = solution.InterpolateAt(window.EndDate);
        #endregion
    }
}
