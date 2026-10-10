// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

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
            new CelestialItem[] { _pointMassEarth }, day, false, false);

        var earthR2 = Earth(10, true);
        R2 = new ReferenceCase("R2",
            State(EarthEquatorialRadius + 400.0e3, 0.0005, 51.6, 20.0, 15.0, 0.0, earthR2),
            new CelestialItem[] { earthR2, sun, moon }, day, true, false);

        // 98.19° is the sun-synchronous inclination at 700 km (a J2 nodal regression of 360° per year); the node at the
        // right ascension of the Sun, near 0 at the equinox, puts the orbit at noon and midnight
        var earthR3 = Earth(10, false);
        R3 = new ReferenceCase("R3",
            State(EarthEquatorialRadius + 700.0e3, 0.001, 98.19, 0.0, 90.0, 0.0, earthR3),
            new CelestialItem[] { earthR3, sun, moon }, day, false, true);

        var earthR4 = Earth(70, false);
        R4 = new ReferenceCase("R4",
            State(42164137.0, 0.0002, 0.05, 0.0, 30.0, 0.0, earthR4),
            new CelestialItem[]
            {
                earthR4, sun, moon, Barycenters.MERCURY_BARYCENTER, Barycenters.VENUS_BARYCENTER,
                Barycenters.MARS_BARYCENTER, Barycenters.JUPITER_BARYCENTER, Barycenters.SATURN_BARYCENTER,
                Barycenters.URANUS_BARYCENTER, Barycenters.NEPTUNE_BARYCENTER
            }, day, false, true);

        // Perigee 300 km and apogee 36 000 km above the equatorial radius
        double perigee = EarthEquatorialRadius + 300.0e3;
        double apogee = EarthEquatorialRadius + 36000.0e3;
        double semiMajorAxis = 0.5 * (perigee + apogee);
        var earthR5 = Earth(10, true);
        double period = 2.0 * System.Math.PI * System.Math.Sqrt(System.Math.Pow(semiMajorAxis, 3) / earthR5.GM);
        R5 = new ReferenceCase("R5",
            State(semiMajorAxis, (apogee - perigee) / (apogee + perigee), 63.4, 20.0, 270.0, 0.0, earthR5),
            new CelestialItem[] { earthR5, sun, moon }, TimeSpan.FromSeconds(2.0 * period), true, false);
    }

    internal ReferenceCase R1 { get; }

    internal ReferenceCase R2 { get; }

    internal ReferenceCase R3 { get; }

    internal ReferenceCase R4 { get; }

    internal ReferenceCase R5 { get; }

    /// <summary>The case named <paramref name="name"/>.</summary>
    internal ReferenceCase this[string name] => name switch
    {
        "R1" => R1,
        "R2" => R2,
        "R3" => R3,
        "R4" => R4,
        "R5" => R5,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    /// <summary>
    /// The conservative variant of <paramref name="referenceCase"/> around a point-mass Earth: the same state and third
    /// bodies, without the geopotential, whose partials are central differences until step 5a.
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
