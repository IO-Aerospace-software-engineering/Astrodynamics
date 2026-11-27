// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/reference/attitudes.md, compiled (#705). Each csharp block of the page is the region
// "docs/reference/attitudes.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

/// <summary>The C# examples of docs/reference/attitudes.md, one method per block (#705).</summary>
internal static class AttitudesPage
{
    // docs/reference/attitudes.md, "Single-Vector Attitudes"
    internal static void Block01(CelestialItem maneuverCenter, Time minEpoch, TimeSpan holdDuration)
    {
        #region docs/reference/attitudes.md:1
        // Inputs: maneuverCenter (CelestialItem), minEpoch (Time), holdDuration (TimeSpan)
        new ProgradeAttitude(minEpoch, holdDuration, maneuverCenter);
        new RetrogradeAttitude(minEpoch, holdDuration, maneuverCenter);
        new NadirAttitude(minEpoch, holdDuration, maneuverCenter);
        new ZenithAttitude(minEpoch, holdDuration, maneuverCenter);
        new NormalAttitude(minEpoch, holdDuration, maneuverCenter);
        new AntiNormalAttitude(minEpoch, holdDuration, maneuverCenter);

        // The centre the run infers, at once, and another axis
        var down = new NadirAttitude(bodyAxis: BodyAxis.Down);
        #endregion
    }

    // docs/reference/attitudes.md, "InstrumentPointingToAttitude"
    internal static void Block02(Time minEpoch, TimeSpan holdDuration, Instrument camera, CelestialBody moon)
    {
        #region docs/reference/attitudes.md:2
        // Inputs: minEpoch (Time), holdDuration (TimeSpan), camera (Instrument), moon (CelestialBody)
        var pointing = new InstrumentPointingToAttitude(
            camera, new CelestialAttitudeTarget(moon, Aberration.LT), minEpoch, holdDuration);

        // The geometric direction
        var geometric = new InstrumentPointingToAttitude(
            camera, new CelestialAttitudeTarget(moon), minEpoch, holdDuration);
        #endregion
    }

    // docs/reference/attitudes.md, "Constructors"
    internal static void Block03(CelestialBody moon, CelestialBody sun, Time minEpoch, TimeSpan holdDuration)
    {
        #region docs/reference/attitudes.md:3
        // Inputs: moon and sun (CelestialBody), minEpoch (Time), holdDuration (TimeSpan)
        var attitude = new TriadAttitude(
            BodyAxis.Front, new CelestialAttitudeTarget(moon),   // Primary: Front points at Moon
            BodyAxis.Up, new CelestialAttitudeTarget(sun),       // Secondary: Up toward Sun
            minEpoch, holdDuration);
        #endregion
    }

    // docs/reference/attitudes.md, "Constructors"
    internal static void Block04(Time minEpoch, TimeSpan holdDuration, Instrument camera, ILocalizable primaryTarget, ILocalizable secondaryTarget)
    {
        #region docs/reference/attitudes.md:4
        // Inputs: minEpoch (Time), holdDuration (TimeSpan), camera (Instrument), primaryTarget and secondaryTarget
        //   (ILocalizable)
        var attitude = new TriadAttitude(
            camera,
            new CelestialAttitudeTarget(primaryTarget),
            new CelestialAttitudeTarget(secondaryTarget),
            minEpoch,
            holdDuration);
        #endregion
    }

    // docs/reference/attitudes.md, "Constructors"
    internal static void Block05(Instrument primaryInstrument, ILocalizable primaryTarget, Instrument secondaryInstrument, ILocalizable secondaryTarget, Time minEpoch, TimeSpan holdDuration)
    {
        #region docs/reference/attitudes.md:5
        // Inputs: primaryInstrument and secondaryInstrument (Instrument), primaryTarget and secondaryTarget (ILocalizable),
        //   minEpoch (Time), holdDuration (TimeSpan)
        var attitude = new TriadAttitude(
            primaryInstrument,
            new CelestialAttitudeTarget(primaryTarget),
            secondaryInstrument,
            new CelestialAttitudeTarget(secondaryTarget),
            minEpoch,
            holdDuration);
        #endregion
    }

    // docs/reference/attitudes.md, "Constructors"
    internal static void Block06(CelestialBody earth, CelestialBody sun, Time minEpoch, TimeSpan holdDuration)
    {
        #region docs/reference/attitudes.md:6
        // Inputs: earth and sun (CelestialBody), minEpoch (Time), holdDuration (TimeSpan)
        var antenna = new Vector3(0.0, 0.6, 0.8);
        var attitude = new TriadAttitude(
            antenna, OrbitalDirectionTarget.Nadir,
            Spacecraft.Front, new CelestialAttitudeTarget(sun),
            minEpoch, holdDuration, earth);
        #endregion
    }

    // docs/reference/attitudes.md, "Factory Methods"
    internal static void Block07(CelestialBody earth, CelestialBody sun, Time minEpoch, TimeSpan holdDuration)
    {
        #region docs/reference/attitudes.md:7
        // Inputs: earth and sun (CelestialBody), minEpoch (Time), holdDuration (TimeSpan)
        var lvlh = TriadAttitude.CreateLvlh(minEpoch, holdDuration, earth);

        var sunTrack = TriadAttitude.CreateProgradeWithSunTracking(sun, minEpoch, holdDuration, earth);
        #endregion
    }

    // docs/reference/attitudes.md, "CelestialAttitudeTarget"
    internal static void Block08(CelestialBody sun, CelestialBody moon)
    {
        #region docs/reference/attitudes.md:8
        // Inputs: sun and moon (CelestialBody)
        // Default: geometric (Aberration.None)
        var sunTarget = new CelestialAttitudeTarget(sun);

        // Explicit light-time correction
        var moonTarget = new CelestialAttitudeTarget(moon, Aberration.LT);
        #endregion
    }

    // docs/reference/attitudes.md, "Single-vector attitude"
    internal static void Block09(CelestialBody earth, Time epoch, Spacecraft spacecraft)
    {
        #region docs/reference/attitudes.md:9
        // Inputs: earth (CelestialBody), epoch (Time), spacecraft (Spacecraft)
        var nadir = new NadirAttitude(epoch, TimeSpan.FromMinutes(30), earth);
        // Or the centre the run infers, the observer of the spacecraft's initial orbit
        var inferred = new NadirAttitude(epoch, TimeSpan.FromMinutes(30));
        spacecraft.SetManeuvers(nadir);
        #endregion
    }

    // docs/reference/attitudes.md, "Instrument pointing"
    internal static void Block10(Time epoch, Instrument telescope, ILocalizable targetStar, Spacecraft spacecraft)
    {
        #region docs/reference/attitudes.md:10
        // Inputs: epoch (Time), telescope (Instrument), targetStar (ILocalizable), spacecraft (Spacecraft)
        var pointing = new InstrumentPointingToAttitude(
            telescope, new CelestialAttitudeTarget(targetStar, Aberration.LT), epoch, TimeSpan.FromHours(2));
        spacecraft.SetManeuvers(pointing);
        #endregion
    }

    // docs/reference/attitudes.md, "TRIAD with orbital directions and celestial target"
    internal static void Block11(CelestialBody earth, Time epoch, CelestialBody sun, Spacecraft spacecraft)
    {
        #region docs/reference/attitudes.md:11
        // Inputs: earth and sun (CelestialBody), epoch (Time), spacecraft (Spacecraft)
        var attitude = new TriadAttitude(
            BodyAxis.Front, OrbitalDirectionTarget.Prograde,
            BodyAxis.Up, new CelestialAttitudeTarget(sun),
            epoch, TimeSpan.FromHours(1.5), earth);
        spacecraft.SetManeuvers(attitude);
        #endregion
    }

    // docs/reference/attitudes.md, "LVLH factory"
    internal static void Block12(CelestialBody earth, Time epoch, TimeSpan duration, Spacecraft spacecraft)
    {
        #region docs/reference/attitudes.md:12
        // Inputs: earth (CelestialBody), epoch (Time), duration (TimeSpan), spacecraft (Spacecraft)
        var lvlh = TriadAttitude.CreateLvlh(epoch, duration, earth);
        spacecraft.SetManeuvers(lvlh);
        #endregion
    }
}
