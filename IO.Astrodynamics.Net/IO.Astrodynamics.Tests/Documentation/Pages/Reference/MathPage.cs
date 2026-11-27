// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/reference/math.md, compiled (#705). Each csharp block of the page is the region
// "docs/reference/math.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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

/// <summary>The C# examples of docs/reference/math.md, one method per block (#705).</summary>
internal static class MathPage
{
    // docs/reference/math.md, "Vector3"
    internal static void Block01()
    {
        #region docs/reference/math.md:1
        var r = new Vector3(7000e3, 0.0, 0.0);
        var v = new Vector3(0.0, 7.5e3, 0.0);
        double radialVelocity = r.Dot(v) / r.Magnitude();   // 0
        Vector3 opposite = -r;                               // (−7000e3, 0, 0)
        Vector3 doubled = 2.0 * r;                           // (14000e3, 0, 0)
        Vector3 turned = Matrix.CreateRotationMatrixZ(Constants.HalfPi) * r;   // (0, 7000e3, 0) to rounding
        #endregion
    }

    // docs/reference/math.md, "Matrix"
    internal static void Block02()
    {
        #region docs/reference/math.md:2
        var cov = Matrix.Diagonal(100.0, 100.0, 100.0, 1.0, 1.0, 1.0);           // m², m²/s²
        var correlated = cov.With(1, 3, -5.0).With(3, 1, -5.0);                  // cov unchanged
        var block = Matrix.Create(3, 3, (i, j) => correlated[i + 3, j + 3]);
        double sigmaX = System.Math.Sqrt(cov[0, 0]);                            // 10 m
        #endregion
    }

    // docs/reference/math.md, "Tsiolkovsky"
    internal static void Block03()
    {
        #region docs/reference/math.md:3
        double dv = Tsiolkovsky.DeltaV(isp: 300, initialMass: 1000, finalMass: 800);
        TimeSpan burnTime = Tsiolkovsky.DeltaT(isp: 300, initialMass: 1000, fuelFlow: 0.5, deltaV: dv);
        double fuelUsed = Tsiolkovsky.DeltaM(isp: 300, initialMass: 1000, deltaV: dv);
        #endregion
    }
}
