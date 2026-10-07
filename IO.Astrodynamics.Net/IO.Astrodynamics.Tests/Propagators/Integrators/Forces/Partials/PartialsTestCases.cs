// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// The four reference states of the force partials (B7 in the specification of phase 2, feature 1) and the bodies and
/// spacecraft around them.
/// </summary>
/// <remarks>
/// <para>
/// The states are at 2021-03-20 12:00 UTC, close to the March equinox, so the Sun is near +X of ICRF; every state is on
/// the day side, away from the subsolar direction and from any shadow, so that SRP, albedo and thermal pressure are
/// smooth there.
/// </para>
/// <para>
/// Shared by the tests of a class as an xUnit class fixture: the tests of a class run one after the other, so sharing the
/// bodies, whose geopotential field is not thread-safe, is safe.
/// </para>
/// </remarks>
public sealed class PartialsTestCases
{
    internal const string Leo400 = "LEO 400 km";
    internal const string Leo800 = "LEO 800 km";
    internal const string Geo = "GEO";
    internal const string HeoPerigee = "HEO perigee";

    internal static readonly string[] StateNames = { Leo400, Leo800, Geo, HeoPerigee };

    internal static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);

    internal static TheoryData<string> States => new(StateNames);

    public PartialsTestCases()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        Earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, null,
            new Nrlmsise00Model(SpaceWeather.Nominal), albedo: 0.3, thermalEffectiveTemperature: 254.0,
            thermalEmissivity: 0.95);
        EarthWithGeopotential = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
        Moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Epoch);
        Sun = new CelestialBody(Stars.Sun);
    }

    /// <summary>Point-mass Earth with NRLMSISE-00, albedo and thermal emission.</summary>
    internal CelestialBody Earth { get; }

    /// <summary>Earth with EGM2008 to degree and order 10.</summary>
    internal CelestialBody EarthWithGeopotential { get; }

    internal CelestialBody Moon { get; }

    internal CelestialBody Sun { get; }

    /// <summary>
    /// The state named <paramref name="name"/>, relative to <paramref name="observer"/> (an Earth), in ICRF.
    /// </summary>
    internal static StateVector State(string name, CelestialBody observer)
    {
        const double deg = IO.Astrodynamics.Constants.Deg2Rad;
        KeplerianElements elements = name switch
        {
            Leo400 => new KeplerianElements(6778137.0, 0.0005, 51.6 * deg, 20.0 * deg, 15.0 * deg, 0.0, observer,
                Epoch, Frames.Frame.ICRF),
            Leo800 => new KeplerianElements(7178137.0, 0.001, 98.6 * deg, 40.0 * deg, 10.0 * deg, 0.0, observer,
                Epoch, Frames.Frame.ICRF),
            Geo => new KeplerianElements(42164137.0, 0.0002, 0.05 * deg, 0.0, 30.0 * deg, 0.0, observer, Epoch,
                Frames.Frame.ICRF),
            // Molniya-like, 500 x 40000 km
            HeoPerigee => new KeplerianElements(26628137.0, 39500000.0 / 53256274.0, 63.4 * deg, 20.0 * deg,
                270.0 * deg, 0.0, observer, Epoch, Frames.Frame.ICRF),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
        return elements.ToStateVector();
    }

    /// <summary>
    /// A spacecraft of 100 kg, 10 m², Cd 2.2, Cr 1.5 at the state.
    /// </summary>
    internal static Spacecraft Spacecraft(StateVector state)
    {
        return new Spacecraft(-1961, "PARTIALS", 100.0, 1000.0, new Clock("partials", 65536), state, 10.0, 2.2, null,
            1.5);
    }

    /// <summary>
    /// An ephemeris cache of <paramref name="bodies"/> relative to <paramref name="observer"/> around the epoch, as the
    /// propagator builds it.
    /// </summary>
    internal static PropagationEphemerisCache Cache(ILocalizable observer, params CelestialItem[] bodies)
    {
        return Cache(Frames.Frame.ICRF, observer, bodies);
    }

    /// <summary>
    /// The same cache, in <paramref name="frame"/>, as the propagator builds it for a state in that frame.
    /// </summary>
    internal static PropagationEphemerisCache Cache(Frames.Frame frame, ILocalizable observer,
        params CelestialItem[] bodies)
    {
        var window = new TimeSystem.Window(Epoch.ToTDB().AddHours(-1.0), Epoch.ToTDB().AddHours(1.0));
        var entries = new System.Collections.Generic.List<(CelestialItem, Aberration)>();
        foreach (var body in bodies)
        {
            entries.Add((body, Aberration.None));
            entries.Add((body, Aberration.LT));
        }

        return new PropagationEphemerisCache(window, entries, observer, frame, TimeSpan.FromSeconds(60.0));
    }
}
