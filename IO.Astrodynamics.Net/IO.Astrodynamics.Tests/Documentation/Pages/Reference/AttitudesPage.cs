// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/reference/attitudes.md, compiled (#705). Each csharp block of the page is the region
// "docs/reference/attitudes.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
// give the inputs of a block that continues an earlier one, as the comment opening the block names them
// ("// Inputs: ..."). DocumentationPagesExamplesTests checks that each block equals its region (the block's using
// directives aside, which are this file's) and runs the blocks that need only Data/SolarSystem. The namespace is
// outside IO.Astrodynamics, as an application's; the file imports every namespace of the library, so
// DocumentationPagesExamplesTests also compiles each block that declares using directives with those directives, the
// earlier blocks' and a console application's implicit ones alone, as a reader who copies it does. Do not edit a region
// alone: change the page and copy the block.

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

namespace DocumentationPageSamples.Reference;

/// <summary>The C# examples of docs/reference/attitudes.md, one method per block (#705).</summary>
internal static class AttitudesPage
{
    // docs/reference/attitudes.md, "Single-Vector Attitudes"
    internal static void Block01(CelestialItem maneuverCenter, Time minimumEpoch, TimeSpan holdDuration, Engine engine)
    {
        #region docs/reference/attitudes.md:1
        // Inputs: maneuverCenter (CelestialItem), minimumEpoch (Time), holdDuration (TimeSpan), engine (Engine)
        new ProgradeAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
        new RetrogradeAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
        new NadirAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
        new ZenithAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
        new NormalAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
        new AntiNormalAttitude(maneuverCenter, minimumEpoch, holdDuration, engine);
        #endregion
    }

    // docs/reference/attitudes.md, "InstrumentPointingToAttitude"
    internal static void Block02(Time minimumEpoch, TimeSpan holdDuration, Instrument camera, CelestialBody moon, Engine engine)
    {
        #region docs/reference/attitudes.md:2
        // Inputs: minimumEpoch (Time), holdDuration (TimeSpan), camera (Instrument), moon (CelestialBody), engine (Engine)
        var pointing = new InstrumentPointingToAttitude(
            minimumEpoch, holdDuration, camera, moon, engine);
        #endregion
    }

    // docs/reference/attitudes.md, "Constructors"
    internal static void Block03(Time minimumEpoch, TimeSpan holdDuration, Instrument camera, ILocalizable primaryTarget, ILocalizable secondaryTarget, Engine engine)
    {
        #region docs/reference/attitudes.md:3
        // Inputs: minimumEpoch (Time), holdDuration (TimeSpan), camera (Instrument), primaryTarget and secondaryTarget
        //   (ILocalizable), engine (Engine)
        var attitude = new TriadAttitude(
            minimumEpoch, holdDuration,
            camera, primaryTarget, secondaryTarget, engine);
        #endregion
    }

    // docs/reference/attitudes.md, "Constructors"
    internal static void Block04(Instrument primaryInstrument, ILocalizable primaryTarget, Instrument secondaryInstrument, ILocalizable secondaryTarget, Engine engine, Time minimumEpoch, TimeSpan holdDuration)
    {
        #region docs/reference/attitudes.md:4
        // Inputs: primaryInstrument and secondaryInstrument (Instrument), primaryTarget and secondaryTarget (ILocalizable),
        //   engine (Engine), minimumEpoch (Time), holdDuration (TimeSpan)
        var attitude = new TriadAttitude(
            minimumEpoch, holdDuration,
            primaryInstrument, primaryTarget,
            secondaryInstrument, secondaryTarget, engine);
        #endregion
    }

    // docs/reference/attitudes.md, "Constructors"
    internal static void Block05(Time minimumEpoch, TimeSpan holdDuration, CelestialBody moon, CelestialBody sun, Engine engine)
    {
        #region docs/reference/attitudes.md:5
        // Inputs: minimumEpoch (Time), holdDuration (TimeSpan), moon and sun (CelestialBody), engine (Engine)
        var attitude = new TriadAttitude(
            minimumEpoch, holdDuration,
            Spacecraft.Front, moon,       // Primary: Front points at Moon
            Spacecraft.Up, sun,           // Secondary: Up toward Sun
            engine);
        #endregion
    }

    // docs/reference/attitudes.md, "Constructors"
    internal static void Block06(CelestialBody earth, Time minimumEpoch, TimeSpan holdDuration, CelestialBody sun, Engine engine)
    {
        #region docs/reference/attitudes.md:6
        // Inputs: earth and sun (CelestialBody), minimumEpoch (Time), holdDuration (TimeSpan), engine (Engine)
        var attitude = new TriadAttitude(
            earth, minimumEpoch, holdDuration,
            Spacecraft.Front, OrbitalDirectionTarget.Prograde,
            Spacecraft.Up, new CelestialAttitudeTarget(sun),
            engine);
        #endregion
    }

    // docs/reference/attitudes.md, "Factory Methods"
    internal static void Block07(CelestialBody earth, Time epoch, TimeSpan duration, Engine engine, CelestialBody sun)
    {
        #region docs/reference/attitudes.md:7
        // Inputs: earth and sun (CelestialBody), epoch (Time), duration (TimeSpan), engine (Engine)
        var lvlh = TriadAttitude.CreateLVLH(earth, epoch, duration, engine);

        var sunTrack = TriadAttitude.CreateProgradeWithSunTracking(
            earth, epoch, duration, sun, engine);
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
    internal static void Block09(CelestialBody earth, Time epoch, Engine engine, Spacecraft spacecraft)
    {
        #region docs/reference/attitudes.md:9
        // Inputs: earth (CelestialBody), epoch (Time), engine (Engine), spacecraft (Spacecraft)
        var nadir = new NadirAttitude(earth, epoch, TimeSpan.FromMinutes(30), engine);
        spacecraft.SetStandbyManeuver(nadir);
        #endregion
    }

    // docs/reference/attitudes.md, "Instrument pointing"
    internal static void Block10(Time epoch, Instrument telescope, ILocalizable targetStar, Engine engine, Spacecraft spacecraft)
    {
        #region docs/reference/attitudes.md:10
        // Inputs: epoch (Time), telescope (Instrument), targetStar (ILocalizable), engine (Engine), spacecraft (Spacecraft)
        var pointing = new InstrumentPointingToAttitude(
            epoch, TimeSpan.FromHours(2), telescope, targetStar, engine);
        spacecraft.SetStandbyManeuver(pointing);
        #endregion
    }

    // docs/reference/attitudes.md, "TRIAD with orbital directions and celestial target"
    internal static void Block11(CelestialBody earth, Time epoch, CelestialBody sun, Engine engine, Spacecraft spacecraft)
    {
        #region docs/reference/attitudes.md:11
        // Inputs: earth and sun (CelestialBody), epoch (Time), engine (Engine), spacecraft (Spacecraft)
        var attitude = new TriadAttitude(
            earth, epoch, TimeSpan.FromHours(1.5),
            Spacecraft.Front, OrbitalDirectionTarget.Prograde,
            Spacecraft.Up, new CelestialAttitudeTarget(sun),
            engine);
        spacecraft.SetStandbyManeuver(attitude);
        #endregion
    }

    // docs/reference/attitudes.md, "LVLH factory"
    internal static void Block12(CelestialBody earth, Time epoch, TimeSpan duration, Engine engine, Spacecraft spacecraft)
    {
        #region docs/reference/attitudes.md:12
        // Inputs: earth (CelestialBody), epoch (Time), duration (TimeSpan), engine (Engine), spacecraft (Spacecraft)
        var lvlh = TriadAttitude.CreateLVLH(earth, epoch, duration, engine);
        spacecraft.SetStandbyManeuver(lvlh);
        #endregion
    }
}
