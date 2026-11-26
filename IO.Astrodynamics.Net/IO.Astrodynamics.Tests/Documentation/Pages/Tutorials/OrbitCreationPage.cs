// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/tutorials/orbit-creation.md, compiled (#705). Each csharp block of the page is the region
// "docs/tutorials/orbit-creation.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

namespace DocumentationPageSamples.Tutorials;

/// <summary>The C# examples of docs/tutorials/orbit-creation.md, one method per block (#705).</summary>
internal static class OrbitCreationPage
{
    // docs/tutorials/orbit-creation.md, "Cartesian State Vector"
    internal static void Block01()
    {
        #region docs/tutorials/orbit-creation.md:1
        var earth = PlanetsAndMoons.EARTH_BODY;
        var epoch = new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB);

        var sv = new StateVector(
            new Vector3(6800000.0, 0.0, 0.0),
            new Vector3(0.0, 8000.0, 0.0),
            earth, epoch, Frame.ICRF);
        #endregion
    }

    // docs/tutorials/orbit-creation.md, "Orbital Properties"
    internal static void Block02(StateVector sv, CelestialBody earth)
    {
        #region docs/tutorials/orbit-creation.md:2
        // Inputs: sv (StateVector), earth (CelestialBody)
        Console.WriteLine($"Semi-major axis: {sv.SemiMajorAxis():F0} m");
        Console.WriteLine($"Eccentricity:    {sv.Eccentricity():F6}");
        Console.WriteLine($"Inclination:     {sv.Inclination() * Constants.Rad2Deg:F4} deg");
        Console.WriteLine($"Period:          {sv.Period().TotalMinutes:F2} min");
        Console.WriteLine($"Specific energy: {sv.SpecificOrbitalEnergy():F2} J/kg");
        Console.WriteLine($"Apogee altitude: {sv.ApogeeVector().Magnitude() - earth.EquatorialRadius:F0} m");
        #endregion
    }

    // docs/tutorials/orbit-creation.md, "Keplerian Elements"
    internal static void Block03(CelestialBody earth, Time epoch)
    {
        #region docs/tutorials/orbit-creation.md:3
        // Inputs: earth (CelestialBody), epoch (Time)
        var kep = new KeplerianElements(
            7000000.0,                 // semi-major axis, m
            0.001,                     // eccentricity
            51.6 * Constants.Deg2Rad,  // inclination, rad
            100.0 * Constants.Deg2Rad, // right ascension of the ascending node, rad
            90.0 * Constants.Deg2Rad,  // argument of periapsis, rad
            0.0,                       // mean anomaly, rad
            observer: earth,
            epoch: epoch,
            frame: Frame.ICRF);

        // Convert to Cartesian
        var svFromKep = kep.ToStateVector();
        Console.WriteLine($"Position: {svFromKep.Position}");
        #endregion
    }

    // docs/tutorials/orbit-creation.md, "Two-Body Propagation with AtEpoch"
    internal static void Block04(KeplerianElements kep, Time epoch)
    {
        #region docs/tutorials/orbit-creation.md:4
        // Inputs: kep (KeplerianElements), epoch (Time)
        var laterKep = kep.AtEpoch(epoch.AddDays(1));

        Console.WriteLine($"Mean anomaly after 1 day: {laterKep.MeanAnomaly() * Constants.Rad2Deg:F4} deg");
        #endregion
    }

    // docs/tutorials/orbit-creation.md, "Equinoctial Elements"
    internal static void Block05(StateVector sv)
    {
        #region docs/tutorials/orbit-creation.md:5
        // Inputs: sv (StateVector)
        var equinoctial = sv.ToEquinoctial();

        Console.WriteLine($"Semi-major axis: {equinoctial.SemiMajorAxis():F0} m");
        Console.WriteLine($"p, semi-latus rectum: {equinoctial.P:F3} m");
        Console.WriteLine($"f = e cos(ω + Ω): {equinoctial.F:F6}");
        Console.WriteLine($"g = e sin(ω + Ω): {equinoctial.G:F6}");
        Console.WriteLine($"h = tan(i/2) cos Ω: {equinoctial.H:F6}");
        Console.WriteLine($"k = tan(i/2) sin Ω: {equinoctial.K:F6}");
        Console.WriteLine($"L = Ω + ω + ν: {equinoctial.L0:F6} rad");
        #endregion
    }

    // docs/tutorials/orbit-creation.md, "Geosynchronous Orbit"
    internal static void Block06(CelestialBody earth, Time epoch)
    {
        #region docs/tutorials/orbit-creation.md:6
        // Inputs: earth (CelestialBody), epoch (Time)
        var geoOrbit = earth.GeosynchronousOrbit(
            longitude: 0.0,
            latitude: 0.0,
            epoch: epoch);

        Console.WriteLine($"GEO semi-major axis: {geoOrbit.SemiMajorAxis():F0} m");
        Console.WriteLine($"GEO period: {geoOrbit.Period().TotalHours:F4} hours");
        #endregion
    }

    // docs/tutorials/orbit-creation.md, "Geosynchronous Orbit"
    internal static void Block07(CelestialBody earth, Time epoch)
    {
        #region docs/tutorials/orbit-creation.md:7
        // Inputs: earth (CelestialBody), epoch (Time)
        var geoOrbit = earth.GeosynchronousOrbit(0.0, 0.0, epoch, TimeSpan.FromSeconds(86164.0998101));
        #endregion
    }

    // docs/tutorials/orbit-creation.md, "Sun-Synchronous Orbit"
    internal static void Block08(CelestialBody earth, Time epoch)
    {
        #region docs/tutorials/orbit-creation.md:8
        // Inputs: earth (CelestialBody), epoch (Time)
        var ssoOrbit = earth.HelioSynchronousOrbit(
            semiMajorAxis: 7000000.0,
            eccentricity: 0.001,
            descendingNodeEpoch: epoch);

        Console.WriteLine($"SSO inclination: {ssoOrbit.Inclination() * Constants.Rad2Deg:F4} deg");
        #endregion
    }
}
