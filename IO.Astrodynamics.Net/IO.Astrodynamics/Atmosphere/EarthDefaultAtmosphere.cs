// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// The atmosphere of an Earth built without an atmospheric model: NRLMSISE-00 at nominal space weather
/// (<see cref="SpaceWeather.Nominal"/>, standard flags), the model that <see cref="Body.CelestialBody.GetAtmosphere(AtmosphericContext)"/>
/// gives such an Earth with a full context and that its drag uses (#674).
/// </summary>
/// <remarks>
/// One instance per thread: <see cref="Nrlmsise00Model"/> serializes its calls on a lock of its instance, and one shared
/// instance made 4 threads computing 200 000 densities each take 5768 ms, against 910 ms with one per thread (Linux,
/// 2026-11-12). The model is a pure function of its inputs, so every thread's instance gives the values of
/// <c>new Nrlmsise00Model()</c>, bit for bit. It reads no SPICE.
/// </remarks>
internal static class EarthDefaultAtmosphere
{
    [ThreadStatic] private static Nrlmsise00Model _model;

    /// <summary>The NRLMSISE-00 at nominal space weather of the calling thread.</summary>
    internal static Nrlmsise00Model Model => _model ??= new Nrlmsise00Model(SpaceWeather.Nominal);
}
