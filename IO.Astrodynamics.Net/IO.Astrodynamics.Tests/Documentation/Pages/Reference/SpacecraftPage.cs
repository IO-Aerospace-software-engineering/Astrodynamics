// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/reference/spacecraft.md, compiled (#705). Each csharp block of the page is the region
// "docs/reference/spacecraft.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

/// <summary>The C# examples of docs/reference/spacecraft.md, one method per block (#705).</summary>
internal static class SpacecraftPage
{
    // docs/reference/spacecraft.md, "Standard Constructor"
    internal static void Block01(StateVector orbit)
    {
        #region docs/reference/spacecraft.md:1
        // Inputs: orbit (StateVector)
        // The NAIF id, the name, the dry mass (kg) and the initial orbit; everything else is optional
        var spacecraft = new Spacecraft(-1001, "MySat", 500.0, orbit);

        // The optional values, by name
        var detailed = new Spacecraft(-1002, "MySat2", 500.0, orbit,
            maxOperatingMass: 2000.0, clock: new Clock("OnboardClk", 256),
            sectionalArea: 10.0, dragCoefficient: 2.2,
            cosparId: "2024-001A", solarRadiationCoefficient: 1.5);
        #endregion
    }

    // docs/reference/spacecraft.md, "Kernel-Backed Constructor"
    internal static void Block02()
    {
        #region docs/reference/spacecraft.md:2
        var lro = new Spacecraft(naifId: -85, name: "LRO",
            epoch: new Time(2024, 6, 1, 12, 0, 0, TimeScale.TDB), mass: 1018.0, sectionalArea: 12.0);
        #endregion
    }

    // docs/reference/spacecraft.md, "The kernels" (files written and kernels loaded: compiled only)
    internal static void Block03(Spacecraft sat)
    {
        #region docs/reference/spacecraft.md:3
        // Inputs: sat (Spacecraft, propagated, with an instrument Cam)
        // Writes out/Sat.tsc, Sat.tf, Sat_Cam.tf, Sat_Cam.ti, Sat.bsp and Sat.bc, and loads them
        using (var kernels = sat.WriteKernels(new DirectoryInfo("out")))
        {
            Console.WriteLine(kernels.Ephemeris.FullName);
            foreach (var instrument in kernels.Instruments)
            {
                Console.WriteLine($"{instrument.Instrument.Name}: {instrument.Frame.Name}, {instrument.Kernel.Name}");
            }

            // SPICE reads the spacecraft here: a kernel-backed twin, a CSPICE search, another tool through the files
        }
        // Disposed: the files the call loaded are unloaded, and stay on disk
        #endregion
    }

    // docs/reference/spacecraft.md, "Engine"
    internal static void Block04(FuelTank tank)
    {
        #region docs/reference/spacecraft.md:4
        // Inputs: tank (FuelTank)
        var engine = new Engine("MainEngine", isp: 366.0, fuelFlow: 0.5, fuelTank: tank,
            model: "RS-25", serialNumber: "SN001");
        #endregion
    }

    // docs/reference/spacecraft.md, "FuelTank"
    internal static void Block05()
    {
        #region docs/reference/spacecraft.md:5
        var tank = new FuelTank("MainTank", capacity: 1000.0, initialQuantity: 800.0,
            model: "FT-100", serialNumber: "SN001");
        #endregion
    }

    // docs/reference/spacecraft.md, "Field-of-view search"
    internal static void Block06(Spacecraft spacecraft, Window window, CelestialBody earth)
    {
        #region docs/reference/spacecraft.md:6
        // Inputs: spacecraft (Spacecraft), window (Window), earth (CelestialBody)
        var camera = spacecraft.Instruments.First();
        var windows = camera.FindFieldOfViewWindows(window, earth, new FieldOfViewSearchOptions
        {
            Step = TimeSpan.FromSeconds(60.0), Aberration = Aberration.LT, TargetShape = ShapeType.Ellipsoid, TargetFrame = new Frame("ITRF93")
        });
        #endregion
    }

    // docs/reference/spacecraft.md, "Payload"
    internal static void Block07()
    {
        #region docs/reference/spacecraft.md:7
        var payload = new Payload("Camera", 25.0, "SN-42");
        #endregion
    }
}
