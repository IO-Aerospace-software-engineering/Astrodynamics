// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/reference/covariance-propagation.md, compiled (#705). Each csharp block of the page is the region
// "docs/reference/covariance-propagation.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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
using IO.Astrodynamics.TimeSystem.Frames;
using TLE = IO.Astrodynamics.OrbitalParameters.TLE.TLE;

namespace DocumentationPageSamples.Reference;

/// <summary>The C# examples of docs/reference/covariance-propagation.md, one method per block (#705).</summary>
internal static class CovariancePropagationPage
{
    // docs/reference/covariance-propagation.md, "Example"
    internal static void Block01(Window window, Spacecraft spacecraft, CelestialBody moon, CelestialBody sun, Time tca)
    {
        #region docs/reference/covariance-propagation.md:1
        // Inputs: window (Window), spacecraft (Spacecraft), moon and sun (CelestialBody), tca (Time)
        var propagator = new CentralBodyPropagatorBuilder(window, spacecraft, new RK78Integrator(1e-11, 1e-11), TimeSpan.FromSeconds(60))
            .WithPerturbingBodies(new CelestialItem[] { moon, sun })   // the central body, the observer of the spacecraft, is added
            .IncludeAtmosphericDrag()
            .WithCovariancePropagation(new CovariancePropagationOptions
            {
                Mode = CovariancePropagationMode.Covariance,
                ProcessNoise = new RtnProcessNoise(1e-12, 1e-11, 1e-12),
                ConsiderParameters = new ConsiderParameters(dragCoefficientSigma: 0.1)
            })
            .Build();

        var solution = propagator.Propagate();
        Matrix phi = solution.GetStateTransitionMatrix(tca);  // Φ(tca, t0), 6×6
        Matrix p = solution.GetCovariance(tca);               // 6×6, inertial frame of the propagation
        #endregion
    }

    // docs/reference/covariance-propagation.md, "Execution Error of an Impulsive Maneuver"
    internal static void Block02(CelestialBody earth, Time epoch, Engine engine)
    {
        #region docs/reference/covariance-propagation.md:2
        // Inputs: earth (CelestialBody), epoch (Time), engine (Engine)
        var maneuver = new ApogeeHeightManeuver(earth, epoch, TimeSpan.Zero, 7000000.0, engine)
        {
            ExecutionError = new ManeuverExecutionError
            {
                ResolutionSigma = 0.01, ShutoffSigma = 0.002, AutopilotSigma = 0.005, PointingSigma = 0.001
            }
        };
        #endregion
    }

    // docs/reference/covariance-propagation.md, "An Initial Covariance Indefinite From Rounding"
    internal static void Block03(StateVector state, Clock clock, Window window, CelestialItem[] bodies)
    {
        #region docs/reference/covariance-propagation.md:3
        // Inputs: state (StateVector), clock (Clock), window (Window), bodies (CelestialItem[])
        var remediation = state.WithRemediatedCovariance();
        if (remediation.Remediated)
            Console.WriteLine($"Smallest eigenvalue of the correlation matrix: {remediation.SmallestCorrelationEigenvalue:E3}");

        var spacecraft = new Spacecraft(-1001, "SAT", 100.0, 1000.0, clock, remediation.State);
        var solution = spacecraft.Propagate(window, bodies, new RK78Integrator(), false, false, TimeSpan.FromSeconds(60.0),
            new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance });
        #endregion
    }
}
