// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/reference/force-models.md, compiled (#705). Each csharp block of the page is the region
// "docs/reference/force-models.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

/// <summary>The C# examples of docs/reference/force-models.md, one method per block (#705).</summary>
internal static class ForceModelsPage
{
    // docs/reference/force-models.md, "GeopotentialGravitationalField"
    internal static void Block01(Time epoch)
    {
        #region docs/reference/force-models.md:1
        // Inputs: epoch (Time)
        // A model other than EGM2008, in the same file format, with its own constants
        var mars = new CelestialBody(PlanetsAndMoons.Mars, Frame.ICRF, epoch,
            new GeopotentialModelParameters("mars_model", 20, 4.282837362069909e13, 3396000.0));
        #endregion
    }

    // docs/reference/force-models.md, "The central term with the GM of the model"
    internal static void Block02(Time epoch)
    {
        #region docs/reference/force-models.md:2
        // Inputs: epoch (Time)
        // EGM2008 with its own GM in the central term too, as GMAT evaluates EGM2008 from a .cof header
        var earth = new CelestialBody(PlanetsAndMoons.Earth, Frame.ICRF, epoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10)
            {
                CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
            });
        #endregion
    }

    // docs/reference/force-models.md, "Example"
    internal static void Block03()
    {
        #region docs/reference/force-models.md:3
        var earth = new CelestialBody(PlanetsAndMoons.Earth,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
            albedo: 0.306, thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);
        #endregion
    }

    // docs/reference/force-models.md, "The Atmosphere API"
    internal static void Block04()
    {
        #region docs/reference/force-models.md:4
        var model = new EarthStandardAtmosphere();
        var context = AtmosphericContext.FromAltitude(10000);  // 10 km

        // Get individual values
        var temp = model.GetTemperature(context);     // °C
        var pressure = model.GetPressure(context);    // kPa
        var density = model.GetDensity(context);      // kg/m³

        // Or get complete atmospheric data
        var atmosphere = model.GetAtmosphere(context);
        Console.WriteLine($"T={atmosphere.Temperature:F1}°C, P={atmosphere.Pressure:F3} kPa, ρ={atmosphere.Density:E3} kg/m³");
        #endregion
    }

    // docs/reference/force-models.md, "NRLMSISE-00"
    internal static void Block05()
    {
        #region docs/reference/force-models.md:5
        // Use nominal conditions
        var model = new Nrlmsise00Model();

        // Or specify space weather
        var spaceWeather = new SpaceWeather { F107 = 150, F107A = 150, Ap = 4 };
        var weatherModel = new Nrlmsise00Model(spaceWeather);

        // Or use preset conditions
        var solarMinModel = new Nrlmsise00Model(SpaceWeather.SolarMinimum);
        var solarMaxModel = new Nrlmsise00Model(SpaceWeather.SolarMaximum);

        // Full context required
        var context = AtmosphericContext.FromPlanetodetic(
            new Planetodetic(0, 0, 400000),  // prime meridian, equator, 400 km
            new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB));

        var atmosphere = model.GetAtmosphere(context);
        Console.WriteLine($"Density at 400 km: {atmosphere.Density:E3} kg/m³");

        // Access model-specific details via pattern matching
        if (atmosphere.Details is NrlmsiseDetails details)
        {
            Console.WriteLine($"Atomic Oxygen: {details.AtomicOxygenDensity:E3} m⁻³");
            Console.WriteLine($"Molecular Nitrogen: {details.NitrogenDensity:E3} m⁻³");
            Console.WriteLine($"Exospheric Temp: {details.ExosphericTemperature:F0} K");
        }
        #endregion
    }

    // docs/reference/force-models.md, "The atmosphere of a body"
    internal static void Block06()
    {
        #region docs/reference/force-models.md:6
        var earth = PlanetsAndMoons.EarthBody;

        // Altitude only: the U.S. Standard Atmosphere 1976, the base atmosphere of an Earth without a model
        var atm1 = earth.GetAtmosphere(10000);  // 10 km altitude

        // Full context: NRLMSISE-00 at nominal space weather for an Earth without a model
        var context = AtmosphericContext.FromPlanetodetic(
            new Planetodetic(-75 * Constants.Deg2Rad, 45 * Constants.Deg2Rad, 400000),  // longitude, latitude (rad), altitude (m)
            new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB));
        var atm2 = earth.GetAtmosphere(context);

        // Access NRLMSISE-00 specific data
        if (atm2.Details is NrlmsiseDetails details)
        {
            Console.WriteLine($"O₂ density: {details.MolecularOxygenDensity:E3} m⁻³");
        }
        #endregion
    }
}
