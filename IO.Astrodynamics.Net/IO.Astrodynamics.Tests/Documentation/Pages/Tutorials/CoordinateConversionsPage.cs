// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/tutorials/coordinate-conversions.md, compiled (#705). Each csharp block of the page is the region
// "docs/tutorials/coordinate-conversions.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

namespace DocumentationPageSamples.Tutorials;

/// <summary>The C# examples of docs/tutorials/coordinate-conversions.md, one method per block (#705).</summary>
internal static class CoordinateConversionsPage
{
    // docs/tutorials/coordinate-conversions.md, "Transforming Between Inertial Frames"
    internal static (StateVector SvIcrf, StateVector SvEcliptic) Block01()
    {
        #region docs/tutorials/coordinate-conversions.md:1
        var epoch = new Time(2024, 6, 21, 12, 0, 0);
        var earth = PlanetsAndMoons.EARTH_BODY;

        var svICRF = new StateVector(
            new Vector3(6800000.0, 0.0, 0.0),
            new Vector3(0.0, 7700.0, 0.0),
            earth, epoch, Frame.ICRF);

        var svEcliptic = svICRF.ToFrame(Frame.ECLIPTIC_J2000).ToStateVector();

        Console.WriteLine($"ICRF position:     {svICRF.Position}");
        Console.WriteLine($"Ecliptic position: {svEcliptic.Position}");
        #endregion
        return (svICRF, svEcliptic);
    }

    // docs/tutorials/coordinate-conversions.md, "Body-Fixed Frames"
    internal static void Block02(StateVector svICRF)
    {
        #region docs/tutorials/coordinate-conversions.md:2
        // Inputs: svICRF (StateVector)
        var earthFrame = new Frame("IAU_EARTH");
        var svBodyFixed = svICRF.ToFrame(earthFrame).ToStateVector();

        Console.WriteLine($"Body-fixed position: {svBodyFixed.Position}");
        #endregion
    }

    // docs/tutorials/coordinate-conversions.md, "Earth Orientation Frames"
    internal static (Vector3 PositionIcrf, Vector3 PositionCirs, Vector3 PositionBack) Block03(StateVector svICRF, Time epoch)
    {
        #region docs/tutorials/coordinate-conversions.md:3
        // Inputs: svICRF (StateVector), epoch (Time)
        // Get the CIRS-to-ICRF orientation at a given epoch
        var cirsToIcrf = Frame.CIRS.GetStateOrientationToICRF(epoch);

        // Rotate a position from ICRF to CIRS (use the conjugate)
        var positionIcrf = svICRF.Position;
        var positionCirs = positionIcrf.Rotate(cirsToIcrf.Rotation.Conjugate());

        // Rotate it back from CIRS to ICRF
        var positionBack = positionCirs.Rotate(cirsToIcrf.Rotation);
        #endregion
        return (positionIcrf, positionCirs, positionBack);
    }

    // docs/tutorials/coordinate-conversions.md, "Equatorial Coordinates"
    internal static void Block04(StateVector svICRF)
    {
        #region docs/tutorials/coordinate-conversions.md:4
        // Inputs: svICRF (StateVector)
        var eq = svICRF.ToEquatorial();

        Console.WriteLine($"RA:  {eq.RightAscension * Constants.Rad2Deg:F4} deg");
        Console.WriteLine($"Dec: {eq.Declination * Constants.Rad2Deg:F4} deg");
        Console.WriteLine($"Distance: {eq.Distance:F0} m");
        #endregion
    }

    // docs/tutorials/coordinate-conversions.md, "Planetodetic Coordinates"
    internal static void Block05()
    {
        #region docs/tutorials/coordinate-conversions.md:5
        var coords = new Planetodetic(
            Longitude: -122.0 * Constants.Deg2Rad,
            Latitude: 37.0 * Constants.Deg2Rad,
            Altitude: 100.0);

        Console.WriteLine($"Longitude: {coords.Longitude * Constants.Rad2Deg:F2} deg");
        Console.WriteLine($"Latitude:  {coords.Latitude * Constants.Rad2Deg:F2} deg");
        Console.WriteLine($"Altitude:  {coords.Altitude:F0} m");
        #endregion
    }

    // docs/tutorials/coordinate-conversions.md, "Planetodetic Coordinates"
    internal static void Block06(Planetodetic coords)
    {
        #region docs/tutorials/coordinate-conversions.md:6
        // Inputs: coords (Planetodetic)
        var earth = PlanetsAndMoons.EARTH_BODY;
        var planetocentric = coords.ToPlanetocentric(earth.Flattening, earth.EquatorialRadius);
        var back = planetocentric.ToPlanetodetic(earth.Flattening, earth.EquatorialRadius); // coords, to about 1e-15
        #endregion
    }
}
