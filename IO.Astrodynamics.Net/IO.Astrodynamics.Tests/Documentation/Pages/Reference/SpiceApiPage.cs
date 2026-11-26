// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/reference/spice-api.md, compiled (#705). Each csharp block of the page is the region
// "docs/reference/spice-api.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

/// <summary>The C# examples of docs/reference/spice-api.md, one method per block (#705).</summary>
internal static class SpiceApiPage
{
    // docs/reference/spice-api.md, "SPICE errors"
    internal static void Block01()
    {
        #region docs/reference/spice-api.md:1
        try
        {
            var moon = SpiceApi.Instance.ReadEphemeris(new Time(3000, 1, 1, TimeScale.TDB), 399, 301, Frame.ICRF, Aberration.None);
        }
        catch (SpiceException e) when (e.ShortMessage == "SPICE(SPKINSUFFDATA)")
        {
            // past the coverage of the loaded SPKs: load one that covers the epoch
        }
        #endregion
    }

    // docs/reference/spice-api.md, "Example"
    internal static void Block02()
    {
        #region docs/reference/spice-api.md:2
        SpiceApi.Instance.LoadKernels(new DirectoryInfo("kernels/"));

        foreach (var kernel in SpiceApi.Instance.GetLoadedKernels())
            Console.WriteLine(kernel);
        #endregion
    }

    // docs/reference/spice-api.md, "MemoryDataProvider"
    internal static void Block03()
    {
        #region docs/reference/spice-api.md:3
        var memoryProvider = new MemoryDataProvider();
        memoryProvider.AddCelestialBodyInfo(new CelestialBodyInfo(NaifId: 399, Name: "EARTH", CenterOfMotionId: 10,
            BarycenterOfMotionId: 3, Radii: new Vector3(6378137.0, 6378137.0, 6356752.0), GM: 3.986004418e14,
            FrameName: "ITRF93", FrameId: 13000));
        Configuration.Instance.SetDataProvider(memoryProvider);
        #endregion
    }
}
