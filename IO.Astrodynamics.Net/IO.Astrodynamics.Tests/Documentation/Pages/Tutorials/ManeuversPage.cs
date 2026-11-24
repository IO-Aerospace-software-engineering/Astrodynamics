// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/tutorials/maneuvers.md, compiled (#705). Each csharp block of the page is the region
// "docs/tutorials/maneuvers.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

namespace DocumentationPageSamples.Tutorials;

/// <summary>The C# examples of docs/tutorials/maneuvers.md, one method per block (#705).</summary>
internal static class ManeuversPage
{
    // docs/tutorials/maneuvers.md, "Single-vector attitudes"
    internal static void Block01(CelestialBody earth, Time epoch, Engine engine)
    {
        #region docs/tutorials/maneuvers.md:1
        // Inputs: earth (CelestialBody), epoch (Time), engine (Engine)
        // Point the thrust axis along the velocity vector; the next maneuver of the chain starts 30 minutes later at the earliest
        var prograde = new ProgradeAttitude(earth, epoch, TimeSpan.FromMinutes(30));

        // Point the instrument axis toward the sub-satellite point, also held 30 minutes
        var nadir = new NadirAttitude(earth, epoch, TimeSpan.FromMinutes(30));
        #endregion
    }

    // docs/tutorials/maneuvers.md, "TriadAttitude --- fully-constrained pointing"
    internal static void Block02(Time epoch, CelestialBody earth, CelestialBody sun, Engine engine)
    {
        #region docs/tutorials/maneuvers.md:2
        // Inputs: epoch (Time), earth and sun (CelestialBody), engine (Engine)
        // Primary: spacecraft -Z toward Earth (nadir pointing)
        // Secondary: spacecraft +X toward Sun, which sets the roll about nadir (+Z, opposite to -Z, would leave the roll
        // undetermined: the body vectors must be at least 5 degrees apart)
        var triad = new TriadAttitude(epoch, TimeSpan.FromHours(1.5),
            Spacecraft.Down, earth,    // Primary: nadir pointing
            Spacecraft.Right, sun);
        #endregion
    }

    // docs/tutorials/maneuvers.md, "TriadAttitude --- fully-constrained pointing"
    internal static void Block03(Engine engine, CelestialBody sun, CelestialBody moon, CelestialBody earth, Time epoch, TimeSpan duration)
    {
        #region docs/tutorials/maneuvers.md:3
        // Inputs: engine (Engine), sun, moon and earth (CelestialBody), epoch (Time), duration (TimeSpan)
        // Default geometric direction
        var sunTarget = new CelestialAttitudeTarget(sun);

        // Light-time corrected direction for sensor pointing
        var moonTarget = new CelestialAttitudeTarget(moon, Aberration.LT);

        var triad = new TriadAttitude(earth, epoch, duration,
            Spacecraft.Front, OrbitalDirectionTarget.Prograde,
            Spacecraft.Up, moonTarget);
        #endregion
    }

    // docs/tutorials/maneuvers.md, "TriadAttitude --- fully-constrained pointing"
    internal static void Block04(CelestialBody earth, Time epoch, TimeSpan duration, Engine engine, CelestialBody sun)
    {
        #region docs/tutorials/maneuvers.md:4
        // Inputs: earth and sun (CelestialBody), epoch (Time), duration (TimeSpan), engine (Engine)
        // LVLH frame (the spacecraft's down axis to nadir, its front axis toward the velocity)
        var lvlh = TriadAttitude.CreateLVLH(earth, epoch, duration);

        // Prograde thrust with solar panels tracking the Sun
        var sunTrack = TriadAttitude.CreateProgradeWithSunTracking(
            earth, epoch, duration, sun);
        #endregion
    }

    // docs/tutorials/maneuvers.md, "Moving the apogee"
    internal static void Block05(CelestialBody earth, Time epoch, Engine engine)
    {
        #region docs/tutorials/maneuvers.md:5
        // Inputs: earth (CelestialBody), epoch (Time), engine (Engine)
        // An apogee 1 000 km above the equator: R + 1 000 km from the Earth's centre
        double targetApogeeRadius = earth.EquatorialRadius + 1000e3;
        var raiseApogee = new ApogeeHeightManeuver(
            targetApogeeRadius, engine, epoch, TimeSpan.Zero, earth);
        #endregion
    }

    // docs/tutorials/maneuvers.md, "Moving the perigee"
    internal static void Block06(CelestialBody earth, Time epoch, double targetPerigeeRadius, Engine engine)
    {
        #region docs/tutorials/maneuvers.md:6
        // Inputs: earth (CelestialBody), epoch (Time), targetPerigeeRadius (double), engine (Engine)
        var lowerPerigee = new PerigeeHeightManeuver(
            targetPerigeeRadius, engine, epoch, TimeSpan.Zero, earth);
        #endregion
    }

    // docs/tutorials/maneuvers.md, "Phasing maneuver"
    internal static void Block07(Time epoch, OrbitalParameters targetOrbit, uint revolutions, Engine engine)
    {
        #region docs/tutorials/maneuvers.md:7
        // Inputs: epoch (Time), targetOrbit (OrbitalParameters), revolutions (uint), engine (Engine)
        var phasing = new PhasingManeuver(
            targetOrbit, revolutions, engine, epoch);
        #endregion
    }

    // docs/tutorials/maneuvers.md, "Plane alignment"
    internal static void Block08(Time epoch, OrbitalParameters targetOrbit, Engine engine)
    {
        #region docs/tutorials/maneuvers.md:8
        // Inputs: epoch (Time), targetOrbit (OrbitalParameters), engine (Engine)
        var planeChange = new PlaneAlignmentManeuver(
            targetOrbit, engine, epoch, TimeSpan.Zero);
        #endregion
    }

    // docs/tutorials/maneuvers.md, "Apsidal alignment"
    internal static void Block09(Time epoch, OrbitalParameters targetOrbit, Engine engine)
    {
        #region docs/tutorials/maneuvers.md:9
        // Inputs: epoch (Time), targetOrbit (OrbitalParameters), engine (Engine)
        var apsidalAlign = new ApsidalAlignmentManeuver(
            targetOrbit, engine, epoch, TimeSpan.Zero);
        #endregion
    }

    // docs/tutorials/maneuvers.md, "Combined maneuver"
    internal static void Block10(CelestialBody earth, Time epoch, double targetPerigeeRadius, double targetInclination, Engine engine)
    {
        #region docs/tutorials/maneuvers.md:10
        // Inputs: earth (CelestialBody), epoch (Time), targetPerigeeRadius and targetInclination (double), engine (Engine)
        var combined = new CombinedManeuver(
            targetPerigeeRadius, targetInclination, engine, epoch, TimeSpan.Zero, earth);
        #endregion
    }

    // docs/tutorials/maneuvers.md, "Maneuver chaining"
    internal static void Block11(OrbitalParameters targetOrbit, Engine engine, uint revolutions, CelestialBody earth, double targetApogeeRadius, Spacecraft spacecraft)
    {
        #region docs/tutorials/maneuvers.md:11
        // Inputs: targetOrbit (OrbitalParameters), engine (Engine), revolutions (uint), earth (CelestialBody),
        //   targetApogeeRadius (double), spacecraft (Spacecraft)
        var maneuver1 = new PlaneAlignmentManeuver(
            targetOrbit,
            engine,
            new Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.Zero);

        // The plan, in order; each maneuver takes its own arguments, as in the sections above
        spacecraft.SetManeuvers(maneuver1,
            new ApsidalAlignmentManeuver(
                targetOrbit, engine, new Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero),
            new PhasingManeuver(
                targetOrbit, revolutions, engine, new Time(DateTime.MinValue, TimeScale.TDB)),
            new ApogeeHeightManeuver(
                targetApogeeRadius, engine, new Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero, earth));

        #endregion
    }
}
