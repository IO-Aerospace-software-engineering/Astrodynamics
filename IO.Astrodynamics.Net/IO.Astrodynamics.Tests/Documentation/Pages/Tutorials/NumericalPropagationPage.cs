// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/tutorials/numerical-propagation.md, compiled (#705). Each csharp block of the page is the region
// "docs/tutorials/numerical-propagation.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

/// <summary>The C# examples of docs/tutorials/numerical-propagation.md, one method per block (#705).</summary>
internal static class NumericalPropagationPage
{
    // docs/tutorials/numerical-propagation.md, "Basic Propagation"
    internal static void Block01()
    {
        #region docs/tutorials/numerical-propagation.md:1
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));

        var earth = PlanetsAndMoons.EARTH_BODY;
        var epoch = new Time(2024, 6, 21, 12, 0, 0);

        var orbit = new StateVector(
            new Vector3(6800000.0, 0.0, 0.0),
            new Vector3(0.0, 7700.0, 0.0),
            earth, epoch, Frame.ICRF);

        var clock = new Clock("DemoClock", 65536);  // 65536 ticks per second
        var spacecraft = new Spacecraft(-1001, "DemoSat", 100.0, 10000.0, clock, orbit);

        var solution = spacecraft.Propagate(
            new Window(epoch, epoch.AddDays(1)),
            new[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
            includeAtmosphericDrag: false,
            includeSolarRadiationPressure: false,
            TimeSpan.FromSeconds(60));
        #endregion
    }

    // docs/tutorials/numerical-propagation.md, "Adaptive RK7(8) Integrator"
    internal static void Block02()
    {
        #region docs/tutorials/numerical-propagation.md:2
        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-10,
            relativeTolerance: 1e-10,
            initialStepSize: 30.0,
            minStepSize: 1e-3,
            maxStepSize: 300.0);
        #endregion
    }

    // docs/tutorials/numerical-propagation.md, "Direct Propagator Construction"
    internal static void Block03(Time epoch, Spacecraft spacecraft, Integrator integrator, CelestialBody earth)
    {
        #region docs/tutorials/numerical-propagation.md:3
        // Inputs: epoch (Time), spacecraft (Spacecraft), integrator (Integrator), earth (CelestialBody)
        using var propagator = new CentralBodyPropagator(
            new Window(epoch, epoch.AddHours(2)),
            spacecraft,
            integrator,
            new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
            includeAtmosphericDrag: false,
            includeSolarRadiationPressure: false,
            deltaT: TimeSpan.FromSeconds(10.0));

        var solution = propagator.Propagate();
        #endregion
    }

    // docs/tutorials/numerical-propagation.md, "Third-Body Perturbations"
    internal static void Block04(CelestialBody earth)
    {
        #region docs/tutorials/numerical-propagation.md:4
        // Inputs: earth (CelestialBody)
        var bodies = new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY,
            Barycenters.JUPITER_BARYCENTER };
        #endregion
    }

    // docs/tutorials/numerical-propagation.md, "Geopotential Gravity (EGM2008)"
    internal static void Block05(Time epoch)
    {
        #region docs/tutorials/numerical-propagation.md:5
        // Inputs: epoch (Time)
        var earthWithGeo = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
        #endregion
    }

    // docs/tutorials/numerical-propagation.md, "Atmospheric Drag and Solar Radiation Pressure"
    internal static void Block06(Window window, Spacecraft spacecraft, Integrator integrator, CelestialBody earth)
    {
        #region docs/tutorials/numerical-propagation.md:6
        // Inputs: window (Window), spacecraft (Spacecraft), integrator (Integrator), earth (CelestialBody)
        using var propagator = new CentralBodyPropagator(
            window, spacecraft, integrator,
            new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
            includeAtmosphericDrag: true,
            includeSolarRadiationPressure: true,
            deltaT: TimeSpan.FromSeconds(10.0));
        #endregion
    }

    // docs/tutorials/numerical-propagation.md, "Fluent Builder with Additional Force Models"
    internal static void Block07(Time epoch, Spacecraft spacecraft, Integrator integrator)
    {
        #region docs/tutorials/numerical-propagation.md:7
        // Inputs: epoch (Time), spacecraft (Spacecraft), integrator (Integrator)
        var earth = new CelestialBody(PlanetsAndMoons.EARTH,
            albedo: 0.306,
            thermalEffectiveTemperature: 254.0,
            thermalEmissivity: 0.95);

        using var propagator = new CentralBodyPropagatorBuilder(
                new Window(epoch, epoch.AddHours(2)), spacecraft, integrator,
                TimeSpan.FromSeconds(10))
            .WithPerturbingBody(Stars.SUN_BODY)
            .WithPerturbingBody(PlanetsAndMoons.MOON_BODY)
            .IncludeAlbedo(earth)
            .IncludeThermalRadiation(earth)
            .Build();

        var solution = propagator.Propagate();
        #endregion
    }

    // docs/tutorials/numerical-propagation.md, "Dense Output"
    internal static void Block08(PropagationSolution solution, Time epoch)
    {
        #region docs/tutorials/numerical-propagation.md:8
        // Inputs: solution (PropagationSolution), epoch (Time)
        var (position, velocity) = solution.InterpolateAt(epoch.AddHours(12));

        Console.WriteLine($"Position: {position}");
        Console.WriteLine($"Velocity: {velocity}");
        #endregion
    }
}
