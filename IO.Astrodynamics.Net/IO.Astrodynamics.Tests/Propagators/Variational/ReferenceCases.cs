// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// A reference case of the validation of the state transition matrix: an initial state, the bodies of the propagation,
/// the non-conservative forces and the duration.
/// </summary>
/// <param name="Name">R1 to R5, as in the specification of phase 2, feature 1 (lot F).</param>
/// <param name="InitialState">Initial state, relative to the central body (an Earth instance), in ICRF.</param>
/// <param name="Bodies">The bodies of the propagation, the central body first.</param>
/// <param name="Duration">Duration of the propagation.</param>
/// <param name="AtmosphericDrag">Whether the case includes atmospheric drag.</param>
/// <param name="SolarRadiationPressure">Whether the case includes solar radiation pressure.</param>
internal sealed record ReferenceCase(string Name, StateVector InitialState, IReadOnlyList<CelestialItem> Bodies,
    TimeSpan Duration, bool AtmosphericDrag, bool SolarRadiationPressure)
{
    /// <summary>
    /// The same case without drag and without SRP: two-body, geopotential and third-body forces only, which derive from a
    /// potential, so that its flow is Hamiltonian (F2).
    /// </summary>
    internal ReferenceCase Conservative => this with { AtmosphericDrag = false, SolarRadiationPressure = false };

    /// <summary>Gravitational parameter of the central body, m³/s².</summary>
    internal double GravitationalParameter => ((CelestialItem)InitialState.Observer).GM;

    /// <summary>Position step of the central differences of F1, m (<c>WholePropagationStepStudyTests</c>).</summary>
    internal double PositionStep { get; init; } = 1.0;

    /// <summary>Velocity step of the central differences of F1, m/s.</summary>
    internal double VelocityStep { get; init; } = 1e-3;

    /// <summary>Step of the central differences of Ψ_Cd, relative to Cd.</summary>
    internal double DragCoefficientStep { get; init; } = 0.01;

    /// <summary>Step of the central differences of Ψ_Cr, relative to Cr.</summary>
    internal double ReflectivityCoefficientStep { get; init; } = 0.01;

    /// <summary>The window of the case.</summary>
    internal TimeSystem.Window Window => new(InitialState.Epoch, InitialState.Epoch.Add(Duration));

    /// <summary>The coefficients of the non-conservative forces of the case: the columns of Ψ.</summary>
    internal ForceParameters Parameters => (AtmosphericDrag ? ForceParameters.DragCoefficient : ForceParameters.None) |
                                           (SolarRadiationPressure
                                               ? ForceParameters.ReflectivityCoefficient
                                               : ForceParameters.None);

    /// <summary>
    /// The spacecraft of the reference cases, 100 kg, 10 m², starting from <paramref name="start"/>.
    /// </summary>
    internal static Spacecraft NewSpacecraft(int naifId, StateVector start, double dragCoefficient = ReferenceCases.DragCoefficient,
        double reflectivityCoefficient = ReferenceCases.ReflectivityCoefficient)
    {
        return new Spacecraft(naifId, "REFERENCE" + -naifId, 100.0, 1000.0, new Clock("reference" + -naifId, 65536), start,
            10.0, dragCoefficient, null, reflectivityCoefficient);
    }

    /// <summary>
    /// The case propagated by RK7(8) at <paramref name="tolerance"/> with the variational equations of
    /// <paramref name="options"/> (none when null), through the public path of the spacecraft.
    /// </summary>
    internal PropagationSolution Propagate(int naifId, double tolerance, VariationalOptions options)
    {
        return NewSpacecraft(naifId, InitialState).PropagateWithVariationalEquations(Window, Bodies,
            new RK78Integrator(tolerance, tolerance), AtmosphericDrag, SolarRadiationPressure,
            TimeSpan.FromSeconds(3600.0), options);
    }

    /// <summary>
    /// A propagation of the reference of F1: from exactly <paramref name="start"/> (#398), without the variational
    /// equations, with the coefficients given.
    /// </summary>
    internal PropagationSolution PropagateReference(int naifId, StateVector start, double dragCoefficient,
        double reflectivityCoefficient, double tolerance)
    {
        var propagator = new WholePropagationDifferences.ExactStartPropagator(Window,
            NewSpacecraft(naifId, start, dragCoefficient, reflectivityCoefficient), new RK78Integrator(tolerance, tolerance),
            Bodies, AtmosphericDrag, SolarRadiationPressure, TimeSpan.FromSeconds(3600.0), start);
        return propagator.Propagate();
    }

    public override string ToString() => Name;
}

/// <summary>
/// The five reference cases R1 to R5 of the validation of the state transition matrix (lot F of the specification of
/// phase 2, feature 1), shared by F2 and, later, F1 and G1.
/// </summary>
/// <remarks>
/// <para>
/// All start at 2021-03-20 12:00 UTC, close to the March equinox, with the spacecraft of the force partials (100 kg,
/// 10 m², Cd 2.2, Cr 1.5). The Earth is the observer of the state and the first of the bodies: each case with a
/// geopotential has its own Earth instance, and R1 and the point-mass variants (<see cref="WithPointMassEarth"/>) share
/// a point-mass Earth.
/// </para>
/// <list type="table">
/// <item><term>R1</term><description>LEO 700 km, two-body, 1 day.</description></item>
/// <item><term>R2</term><description>LEO 400 km, EGM2008 10×10, Sun, Moon, drag (NRLMSISE-00), 1 day.</description></item>
/// <item><term>R3</term><description>Sun-synchronous 700 km, ascending node at noon (eclipses on every
/// revolution), EGM2008 10×10, Sun, Moon, SRP, 1 day.</description></item>
/// <item><term>R4</term><description>GEO, EGM2008 70×70, Sun, Moon, the barycentres of Mercury to Neptune, SRP,
/// 1 day.</description></item>
/// <item><term>R5</term><description>HEO 300 × 36 000 km, EGM2008 10×10, Sun, Moon, drag (NRLMSISE-00),
/// 2 revolutions.</description></item>
/// <item><term>Eclipsing</term><description>LEO 400 × 1000 km from apogee, in the Earth's shadow at the start, its
/// apogee raised to 2000 km at the first perigee (0.82 h), point-mass Earth with NRLMSISE-00, albedo and thermal
/// emission, Sun, Moon, drag and SRP, 6 h (<see cref="EclipsingSpacecraft"/>). Asked by the final H3 review of lots A to
/// C for F1, the case of the integration error of Ψ_Cr across the shadow crossings.</description></item>
/// </list>
/// <para>
/// Shared by the tests of a class as an xUnit class fixture: the tests of a class run one after the other, so sharing the
/// bodies, whose geopotential field is not thread-safe, is safe.
/// </para>
/// </remarks>
public sealed class ReferenceCases
{
    private const string GeopotentialModel = "Data/SolarSystem/EGM2008_to70_TideFree";
    private const double EarthEquatorialRadius = 6378137.0;
    private const double Deg = IO.Astrodynamics.Constants.Deg2Rad;

    internal static readonly TimeSystem.Time Epoch = PartialsTestCases.Epoch;

    /// <summary>Tolerance of RK7(8) for Φ and Ψ in F1, absolute and relative (specification).</summary>
    internal const double Tolerance = 1e-13;

    /// <summary>
    /// Tolerance of the reference propagations of F1: 1e-14 rather than the 1e-13 of the specification, since their
    /// noise sets what the differences resolve (<c>WholePropagationStepStudyTests</c>; decision of S. Guillet,
    /// 2026-10-16).
    /// </summary>
    internal const double ReferenceTolerance = 1e-14;

    /// <summary>Cd of the spacecraft of the reference cases.</summary>
    internal const double DragCoefficient = 2.2;

    /// <summary>Cr of the spacecraft of the reference cases.</summary>
    internal const double ReflectivityCoefficient = 1.5;

    private readonly CelestialBody _pointMassEarth;

    public ReferenceCases()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Epoch);
        var sun = new CelestialBody(Stars.Sun);
        var day = TimeSpan.FromDays(1.0);

        _pointMassEarth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);
        R1 = new ReferenceCase("R1",
            State(EarthEquatorialRadius + 700.0e3, 0.001, 45.0, 30.0, 60.0, 10.0, _pointMassEarth),
            new CelestialItem[] { _pointMassEarth }, day, false, false) { PositionStep = 10.0, VelocityStep = 1e-2 };

        var earthR2 = Earth(10, true);
        R2 = new ReferenceCase("R2",
            State(EarthEquatorialRadius + 400.0e3, 0.0005, 51.6, 20.0, 15.0, 0.0, earthR2),
            new CelestialItem[] { earthR2, sun, moon }, day, true, false)
        {
            PositionStep = 10.0, VelocityStep = 1e-2, DragCoefficientStep = 0.01
        };

        // 98.19° is the sun-synchronous inclination at 700 km (a J2 nodal regression of 360° per year); the node at the
        // right ascension of the Sun, near 0 at the equinox, puts the orbit at noon and midnight
        var earthR3 = Earth(10, false);
        R3 = new ReferenceCase("R3",
            State(EarthEquatorialRadius + 700.0e3, 0.001, 98.19, 0.0, 90.0, 0.0, earthR3),
            new CelestialItem[] { earthR3, sun, moon }, day, false, true)
        {
            PositionStep = 10.0, VelocityStep = 1e-2, ReflectivityCoefficientStep = 0.1
        };

        var earthR4 = Earth(70, false);
        R4 = new ReferenceCase("R4",
            State(42164137.0, 0.0002, 0.05, 0.0, 30.0, 0.0, earthR4),
            new CelestialItem[]
            {
                earthR4, sun, moon, Barycenters.MERCURY_BARYCENTER, Barycenters.VENUS_BARYCENTER,
                Barycenters.MARS_BARYCENTER, Barycenters.JUPITER_BARYCENTER, Barycenters.SATURN_BARYCENTER,
                Barycenters.URANUS_BARYCENTER, Barycenters.NEPTUNE_BARYCENTER
            }, day, false, true) { PositionStep = 10.0, VelocityStep = 1e-2, ReflectivityCoefficientStep = 0.1 };

        // Perigee 300 km and apogee 36 000 km above the equatorial radius
        double perigee = EarthEquatorialRadius + 300.0e3;
        double apogee = EarthEquatorialRadius + 36000.0e3;
        double semiMajorAxis = 0.5 * (perigee + apogee);
        var earthR5 = Earth(10, true);
        double period = 2.0 * System.Math.PI * System.Math.Sqrt(System.Math.Pow(semiMajorAxis, 3) / earthR5.GM);
        R5 = new ReferenceCase("R5",
            State(semiMajorAxis, (apogee - perigee) / (apogee + perigee), 63.4, 20.0, 270.0, 0.0, earthR5),
            new CelestialItem[] { earthR5, sun, moon }, TimeSpan.FromSeconds(2.0 * period), true, false)
        {
            PositionStep = 1.0, VelocityStep = 1e-3, DragCoefficientStep = 0.05
        };

        // The Earth of the force partials (PartialsTestCases.Earth), so that the tests that used it keep their figures
        var earthEclipsing = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, null,
            new Nrlmsise00Model(SpaceWeather.Nominal), albedo: 0.3, thermalEffectiveTemperature: 254.0,
            thermalEmissivity: 0.95);
        Eclipsing = new ReferenceCase("Eclipsing",
            new StateVector(new Vector3(-7378137.0, 0.0, 0.0), new Vector3(0.0, -5509.9, -4623.4), earthEclipsing,
                Epoch, Frames.Frame.ICRF),
            new CelestialItem[] { earthEclipsing, moon, sun }, TimeSpan.FromHours(6.0), true, true);
    }

    internal ReferenceCase R1 { get; }

    internal ReferenceCase R2 { get; }

    internal ReferenceCase R3 { get; }

    internal ReferenceCase R4 { get; }

    internal ReferenceCase R5 { get; }

    /// <summary>The eclipsing LEO with a maneuver; its spacecraft comes from <see cref="EclipsingSpacecraft"/>.</summary>
    internal ReferenceCase Eclipsing { get; }

    /// <summary>Apogee radius targeted by the maneuver of <see cref="Eclipsing"/>, m.</summary>
    internal const double EclipsingApogeeRadius = 8378137.0;

    /// <summary>
    /// The spacecraft of <see cref="Eclipsing"/> (1000 kg dry, 1000 kg of fuel, 10 m², Cd 2.2, Cr 1.5, an engine of
    /// 300 s), starting from <paramref name="orbit"/>, with the maneuver that <paramref name="maneuver"/> builds for its
    /// engine: by default the apogee raise to <see cref="EclipsingApogeeRadius"/> at the first perigee.
    /// </summary>
    internal Spacecraft EclipsingSpacecraft(int naifId, StateVector orbit, Func<Engine, ImpulseManeuver> maneuver = null)
    {
        var spacecraft = new Spacecraft(naifId, "ECLIPSING" + -naifId, 1000.0, 3000.0,
            new Clock("eclipsing" + -naifId, 65536), orbit, 10.0, 2.2, null, 1.5);
        var tank = new FuelTank("tank" + -naifId, "model", "sn1", 1000.0, 1000.0);
        var engine = new Engine("engine" + -naifId, "model", "sn1", 300.0, 10.0, tank);
        spacecraft.AddFuelTank(tank);
        spacecraft.AddEngine(engine);
        spacecraft.SetStandbyManeuver(maneuver?.Invoke(engine) ?? new ApogeeHeightManeuver(
            (CelestialItem)Eclipsing.InitialState.Observer, Epoch, TimeSpan.Zero, EclipsingApogeeRadius, engine));
        return spacecraft;
    }

    /// <summary>The case named <paramref name="name"/>.</summary>
    internal ReferenceCase this[string name] => name switch
    {
        "R1" => R1,
        "R2" => R2,
        "R3" => R3,
        "R4" => R4,
        "R5" => R5,
        "Eclipsing" => Eclipsing,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    /// <summary>
    /// The conservative variant of <paramref name="referenceCase"/> around a point-mass Earth: the same state and third
    /// bodies, without the geopotential.
    /// </summary>
    internal ReferenceCase WithPointMassEarth(ReferenceCase referenceCase)
    {
        var state = referenceCase.InitialState;
        var bodies = new List<CelestialItem> { _pointMassEarth };
        for (int i = 1; i < referenceCase.Bodies.Count; i++)
        {
            bodies.Add(referenceCase.Bodies[i]);
        }

        return referenceCase.Conservative with
        {
            InitialState = new StateVector(state.Position, state.Velocity, _pointMassEarth, state.Epoch, state.Frame),
            Bodies = bodies
        };
    }

    /// <summary>
    /// The two-body variant of <paramref name="referenceCase"/>: the same state around a point-mass Earth, without third
    /// bodies, drag or SRP, so that the closed-form Keplerian STM is its exact Φ.
    /// </summary>
    internal ReferenceCase TwoBody(ReferenceCase referenceCase)
    {
        return WithPointMassEarth(referenceCase) with
        {
            Name = referenceCase.Name + " two-body", Bodies = new CelestialItem[] { _pointMassEarth }
        };
    }

    /// <summary>
    /// The conservative variant of <paramref name="referenceCase"/> without its third bodies: the geopotential of its
    /// Earth only.
    /// </summary>
    internal static ReferenceCase GeopotentialOnly(ReferenceCase referenceCase)
    {
        return referenceCase.Conservative with { Bodies = new[] { referenceCase.Bodies[0] } };
    }

    private static CelestialBody Earth(ushort degree, bool atmosphere)
    {
        var geopotential = new GeopotentialModelParameters(GeopotentialModel, degree);
        return atmosphere
            ? new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, geopotential,
                new Nrlmsise00Model(SpaceWeather.Nominal))
            : new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, geopotential);
    }

    private static StateVector State(double semiMajorAxis, double eccentricity, double inclination, double node,
        double argumentOfPerigee, double meanAnomaly, CelestialBody earth)
    {
        return new KeplerianElements(semiMajorAxis, eccentricity, inclination * Deg, node * Deg,
            argumentOfPerigee * Deg, meanAnomaly * Deg, earth, Epoch, Frames.Frame.ICRF).ToStateVector();
    }
}
