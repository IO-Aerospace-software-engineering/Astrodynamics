// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of docs/tutorials/covariance-propagation.md, compiled (#705). Each csharp block of the page is the region
// "docs/tutorials/covariance-propagation.md:<n>" of one method, n its rank among the page's csharp blocks; the parameters of a method
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
using TLE = IO.Astrodynamics.OrbitalParameters.TLE.TLE;

namespace DocumentationPageSamples.Tutorials;

/// <summary>The C# examples of docs/tutorials/covariance-propagation.md, one method per block (#705).</summary>
internal static class CovariancePropagationPage
{
    // docs/tutorials/covariance-propagation.md, "The case"
    internal static void Block01()
    {
        #region docs/tutorials/covariance-propagation.md:1
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));

        var epoch = new Time(2021, 3, 20, 12, 0, 0, TimeScale.TDB);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
            new Nrlmsise00Model(SpaceWeather.Nominal));
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, epoch);
        var sun = new CelestialBody(Stars.Sun);
        var bodies = new CelestialItem[] { earth, moon, sun };
        #endregion
    }

    // docs/tutorials/covariance-propagation.md, "A state with its covariance"
    internal static void Block02(CelestialBody earth, Time epoch)
    {
        #region docs/tutorials/covariance-propagation.md:2
        // Inputs: earth (CelestialBody), epoch (Time)
        var orbit = new KeplerianElements(6778137.0, 0.0005, 51.6 * Constants.Deg2Rad, 20.0 * Constants.Deg2Rad,
            15.0 * Constants.Deg2Rad, 0.0, earth, epoch, Frame.ICRF).ToStateVector();

        double[] sigma = { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 };   // R, T, N (m), then their rates (m/s)
        var rtn = Matrix.Create(6, 6, (i, j) => i == j ? sigma[i] * sigma[i] : 0.0)
            .With(1, 3, -0.9 * sigma[1] * sigma[3])   // along-track position and radial velocity
            .With(3, 1, -0.9 * sigma[1] * sigma[3]);

        var initialState = new StateVector(orbit.Position, orbit.Velocity, earth, epoch, Frame.ICRF,
            orbit.RotateCovarianceFromRtn(rtn));

        var spacecraft = new Spacecraft(-1001, "SAT", 100.0, initialState, 1000.0, new Clock("SAT_CLK", 65536),
            sectionalArea: 10.0, dragCoefficient: 2.2, solarRadiationCoefficient: 1.5);
        var window = new Window(epoch, epoch.AddHours(6.0));
        var t = epoch.AddHours(6.0);
        #endregion
    }

    // docs/tutorials/covariance-propagation.md, "The state transition matrix only"
    internal static void Block03(Spacecraft spacecraft, Window window, CelestialItem[] bodies, Time t)
    {
        #region docs/tutorials/covariance-propagation.md:3
        // Inputs: spacecraft (Spacecraft), window (Window), bodies (CelestialItem[]), t (Time)
        var settings = new PropagationOptions
        {
            Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-11, RelativeTolerance = 1e-11 },
            OutputStep = TimeSpan.FromSeconds(600.0),
            ThirdBodies = bodies,
            AtmosphericDrag = true,
            SolarRadiationPressure = true
        };
        var stm = spacecraft.Propagate(window, settings with
        {
            CovariancePropagation = new CovariancePropagationOptions { Mode = CovariancePropagationMode.StateTransitionOnly }
        });

        Matrix phi = stm.GetStateTransitionMatrix(t);   // Φ(t, t0), 6×6
        Matrix psi = stm.GetParameterSensitivity(t);    // Ψ(t), 6×2: ∂x/∂Cd, ∂x/∂Cr
        Console.WriteLine(string.Join(", ", stm.SensitivityParameters));   // DragCoefficient, ReflectivityCoefficient
        #endregion
    }

    // docs/tutorials/covariance-propagation.md, "The covariance"
    internal static void Block04(Spacecraft spacecraft, Window window, CelestialItem[] bodies, Time t)
    {
        #region docs/tutorials/covariance-propagation.md:4
        // Inputs: spacecraft (Spacecraft), window (Window), bodies (CelestialItem[]), t (Time)
        var options = new CovariancePropagationOptions
        {
            Mode = CovariancePropagationMode.Covariance,
            ProcessNoise = new RtnProcessNoise(radial: 1e-12, transverse: 1e-11, normal: 1e-12),   // m²/s³
            ConsiderParameters = new ConsiderParameters(dragCoefficientSigma: 0.1, reflectivityCoefficientSigma: 0.05),
            IncludeInStepControl = true   // Ψ_Cr through the shadow crossings
        };
        var solution = spacecraft.Propagate(window, new PropagationOptions
        {
            Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-11, RelativeTolerance = 1e-11 },
            OutputStep = TimeSpan.FromSeconds(600.0),
            ThirdBodies = bodies,
            AtmosphericDrag = true,
            SolarRadiationPressure = true,
            CovariancePropagation = options
        });

        Matrix p = solution.GetCovariance(t);                 // 6×6, m², m²/s, m²/s²
        StateVector state = solution.GetStateVector(t);       // the state, with P in state.Covariance
        Matrix extended = solution.GetExtendedCovariance(t);  // 8×8: x, y, z, vx, vy, vz, Cd, Cr
        #endregion
    }

    // docs/tutorials/covariance-propagation.md, "An impulsive maneuver with its execution error"
    internal static void Block05(StateVector initialState, CelestialBody earth, Time epoch, Window window, CelestialItem[] bodies)
    {
        #region docs/tutorials/covariance-propagation.md:5
        // Inputs: initialState (StateVector), earth (CelestialBody), epoch (Time), window (Window), bodies (CelestialItem[])
        var tank = new FuelTank("TANK", "model", "sn1", 50.0, 50.0);
        var engine = new Engine("ENGINE", "model", "sn1", 300.0, 1.0, tank);
        var maneuvering = new Spacecraft(-1002, "SAT2", 100.0, initialState, 1000.0, new Clock("SAT2_CLK", 65536),
            sectionalArea: 10.0, dragCoefficient: 2.2, solarRadiationCoefficient: 1.5);
        maneuvering.AddFuelTank(tank);
        maneuvering.AddEngine(engine);
        var raise = new ApogeeHeightManeuver(earth, epoch.AddHours(1.0), TimeSpan.Zero, 6878137.0, engine)
        {
            ExecutionError = new ManeuverExecutionError
            {
                ResolutionSigma = 0.005,   // m/s, along ΔV
                ShutoffSigma = 0.001,      // proportional to |ΔV|
                AutopilotSigma = 0.002,    // m/s, across ΔV
                PointingSigma = 0.001      // rad, proportional to |ΔV|
            }
        };
        maneuvering.SetManeuvers(raise);

        var withBurn = maneuvering.Propagate(window, new PropagationOptions
        {
            Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-11, RelativeTolerance = 1e-11 },
            OutputStep = TimeSpan.FromSeconds(600.0),
            ThirdBodies = bodies,
            AtmosphericDrag = true,
            SolarRadiationPressure = true,
            CovariancePropagation = new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance }
        });

        Time burn = raise.ThrustWindow.Value.StartDate;
        Matrix before = withBurn.GetCovariance(burn.AddSeconds(-1.0));
        Matrix after = withBurn.GetCovariance(burn);   // the value after the burn, its execution error included
        #endregion
    }

    // docs/tutorials/covariance-propagation.md, "A covariance indefinite from rounding"
    internal static void Block06(StateVector initialState, CelestialBody earth, Time epoch, Window window, CelestialItem[] bodies)
    {
        #region docs/tutorials/covariance-propagation.md:6
        // Inputs: initialState (StateVector), earth (CelestialBody), epoch (Time), window (Window), bodies (CelestialItem[])
        var rounded = Matrix.Diagonal(100.0 * 100.0, 100.0 * 100.0, 100.0 * 100.0, 0.1 * 0.1, 0.1 * 0.1, 0.1 * 0.1)
            .With(1, 3, -1.0001 * 100.0 * 0.1)   // a correlation of −1 written as −1.0001
            .With(3, 1, -1.0001 * 100.0 * 0.1);
        var indefinite = new StateVector(initialState.Position, initialState.Velocity, earth, epoch, Frame.ICRF, rounded);

        var remediation = indefinite.WithRemediatedCovariance();
        if (remediation.Remediated)
            Console.WriteLine($"Smallest eigenvalue of the correlation matrix: {remediation.SmallestCorrelationEigenvalue:E3}");

        var remediated = new Spacecraft(-1003, "SAT3", 100.0, remediation.State, 1000.0, new Clock("SAT3_CLK", 65536),
            sectionalArea: 10.0, dragCoefficient: 2.2, solarRadiationCoefficient: 1.5);
        var fromRemediated = remediated.Propagate(window, new PropagationOptions
        {
            Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-11, RelativeTolerance = 1e-11 },
            OutputStep = TimeSpan.FromSeconds(600.0),
            ThirdBodies = bodies,
            AtmosphericDrag = true,
            SolarRadiationPressure = true,
            CovariancePropagation = new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance }
        });
        #endregion
    }

    // docs/tutorials/covariance-propagation.md, "To a collision probability"
    internal static void Block07(StateVector protectedOrbit, StateVector secondaryOrbit)
    {
        #region docs/tutorials/covariance-propagation.md:7
        // Inputs: protectedOrbit and secondaryOrbit (StateVector)
        var start = new Time(2024, 3, 1, 0, 0, 0, TimeScale.TDB);
        var tca = start.AddHours(24.0);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, start);
        // protectedOrbit, a LEO at 700 km about earth; secondaryOrbit, 120 m radial and 160 m normal from it at tca, its
        // velocity turned by 70° about the radial direction (see the smoke test for the geometry); each given its covariance in
        // RTN, as above, by this helper
        static StateVector WithRtnCovariance(StateVector orbit, double[] sigma)
        {
            var rtn = Matrix.Create(6, 6, (i, j) => i == j ? sigma[i] * sigma[i] : 0.0);
            return new StateVector(orbit.Position, orbit.Velocity, orbit.Observer, orbit.Epoch, orbit.Frame,
                orbit.RotateCovarianceFromRtn(rtn));
        }

        var protectedCraft = new Spacecraft(-2001, "PROTECTED", 100.0, WithRtnCovariance(protectedOrbit, new[] { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 }), 1000.0,
            new Clock("P_CLK", 65536), sectionalArea: 10.0);
        var secondaryCraft = new Spacecraft(-2002, "SECONDARY", 100.0, WithRtnCovariance(secondaryOrbit, new[] { 80.0, 300.0, 60.0, 0.3, 0.08, 0.05 }), 1000.0,
            new Clock("S_CLK", 65536), sectionalArea: 10.0);

        // Both propagated in RK7(8), without the variational equations (None: Auto would propagate the covariance given at the
        // start of the window)
        var propagation = new Window(start, start.AddHours(26.0));
        foreach (var craft in new[] { protectedCraft, secondaryCraft })
            craft.Propagate(propagation, new PropagationOptions
            {
                Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 },
                CovariancePropagation = CovariancePropagationOptions.None
            });

        var profile = new ProtectedSpacecraftProfile(protectedCraft);
        var window = new Window(tca.AddHours(-1.0), tca.AddHours(1.0));
        var encounter = ConjunctionAssessment.Analyze(profile, secondaryCraft, window);
        var held = ConjunctionAssessment.Analyze(profile, secondaryCraft, window,
            new ConjunctionAnalysisOptions { CovariancePropagation = CovariancePropagationOptions.None });

        var source = encounter.EncounterState.ProtectedCovariance.Source;   // CovarianceSource.Propagated
        #endregion
    }

    // docs/tutorials/covariance-propagation.md, "The CDM"
    internal static void Block08(EncounterCase encounter)
    {
        #region docs/tutorials/covariance-propagation.md:8
        // Inputs: encounter (EncounterCase)
        var cdm = encounter.ToCdm(new CdmExportOptions { Originator = "MyOpsCenter", MessageId = "CDM-001" });
        cdm.WriteToFile("encounter.cdm.xml");
        #endregion
    }
}
