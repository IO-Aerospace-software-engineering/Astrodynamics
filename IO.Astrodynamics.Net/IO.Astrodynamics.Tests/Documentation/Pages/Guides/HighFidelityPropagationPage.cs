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
        var earth = new CelestialBody(PlanetsAndMoons.Earth,
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
            ThirdBodies = new CelestialItem[] { Stars.SunBody, PlanetsAndMoons.MoonBody },
            AtmosphericDrag = true,
            SolarRadiationPressure = true,
            AlbedoRadiationPressure = true,
            ThermalRadiationPressure = true
        };

        var solution = spacecraft.Propagate(window, options);
        #endregion
    }

    // docs/guides/high-fidelity-propagation.md, "Deep Space: The Moon As A Probe"
    internal static (Spacecraft Spacecraft, StateVector MoonState, Time Start, Time End) Block02()
    {
        #region docs/guides/high-fidelity-propagation.md:2
        var moon = new CelestialBody(PlanetsAndMoons.Moon, Frame.ICRF, Time.J2000Tdb);
        var sun = new CelestialBody(Stars.Sun);

        // The Moon moves about the Earth under the attraction of both masses, so the Earth's central term is GM_Earth + GM_Moon:
        // a field of degree 0 with the GM of the model is a point mass of that GM. With the Earth's GM alone, a massless probe
        // started at the Moon's state would end about 54 310 km from the Moon after 25 days
        double gmEarthMoon = PlanetsAndMoons.EarthBody.GM + moon.GM;
        var earthMoonField = new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 0, gmEarthMoon, 6378136.3)
        {
            CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
        };
        var earth = new CelestialBody(PlanetsAndMoons.Earth, Frame.ICRF, Time.J2000Tdb, earthMoonField);

        var start = Time.J2000Tdb;
        var end = start.AddDays(25);

        var mission = new Mission("DeepSpaceTest");
        var scenario = new Scenario("LunarOrbit", mission, new Window(start, end));

        // Start at the Moon's position and velocity
        var moonState = moon.GetEphemeris(start, earth, Frame.ICRF, Aberration.None);

        var clock = new Clock("Clock", 256);
        var spacecraft = new Spacecraft(-1001, "Probe", 100.0, moonState, 10000.0, clock);

        scenario.AddSpacecraft(spacecraft);

        // Add gravitational perturbations from multiple bodies
        scenario.AddCelestialItem(sun);
        scenario.AddCelestialItem(earth);
        scenario.AddCelestialItem(new Barycenter(5));  // Jupiter barycenter
        scenario.AddCelestialItem(new Barycenter(6));  // Saturn barycenter

        // Propagate at a 5-minute step. Velocity-Verlet is of the second order: it ends about 470 m from an exact integration of
        // these forces after 25 days
        var summary = scenario.Simulate(new PropagationOptions
        {
            Integrator = new VVIntegratorOptions { Step = TimeSpan.FromMinutes(5) },
            OutputStep = TimeSpan.FromMinutes(5)
        });

        // Compare the propagated Moon with the Moon of the ephemeris (DE440)
        var propagatedMoon = spacecraft.GetEphemeris(end, earth, Frame.ICRF, Aberration.None);
        var ephemerisMoon = moon.GetEphemeris(end, earth, Frame.ICRF, Aberration.None);

        var positionDifference = (propagatedMoon.Position - ephemerisMoon.Position).Magnitude();
        var velocityDifference = (propagatedMoon.Velocity - ephemerisMoon.Velocity).Magnitude();

        Console.WriteLine($"Propagated Moon - DE440 Moon after 25 days:");
        Console.WriteLine($"  Position difference: {positionDifference:F1} m");
        Console.WriteLine($"  Velocity difference: {velocityDifference:F4} m/s");
        #endregion
        return (spacecraft, moonState, start, end);
    }
}
