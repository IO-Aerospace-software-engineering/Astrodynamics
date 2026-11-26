// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/guides/high-fidelity-propagation.md, compiled (#705). Each csharp block of the page is the region
// "docs/guides/high-fidelity-propagation.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

namespace DocumentationPageSamples.Guides;

/// <summary>The C# examples of docs/guides/high-fidelity-propagation.md, one method per block (#705).</summary>
internal static class HighFidelityPropagationPage
{
    // docs/guides/high-fidelity-propagation.md, "Full Force Model Assembly"
    internal static void Block01(Window window, StateVector orbit, TimeSpan outputStep)
    {
        #region docs/guides/high-fidelity-propagation.md:1
        // Inputs: window (Window), orbit (StateVector), outputStep (TimeSpan)
        // Central body with geopotential, atmosphere, albedo, and thermal properties
        var earth = new CelestialBody(PlanetsAndMoons.EARTH,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
            new Nrlmsise00Model(SpaceWeather.Nominal),
            albedo: 0.306, thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);

        // The orbit about this Earth: the central body carries the models and the coefficients
        var spacecraft = new Spacecraft(-1001, "HiFiSat", 100.0, new StateVector(orbit.Position, orbit.Velocity, earth, orbit.Epoch, orbit.Frame), 10000.0,
            new Clock("HiFiClock", 65536));

        // The adaptive integrator and every perturbation
        var options = new PropagationOptions
        {
            Integrator = new RK78IntegratorOptions
            {
                AbsoluteTolerance = 1e-10,
                RelativeTolerance = 1e-10,
                InitialStep = TimeSpan.FromSeconds(30.0)
            },
            OutputStep = outputStep,
            ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
            AtmosphericDrag = true,
            SolarRadiationPressure = true,
            AlbedoRadiationPressure = true,
            ThermalRadiationPressure = true
        };

        var solution = spacecraft.Propagate(window, options);
        #endregion
    }
}
