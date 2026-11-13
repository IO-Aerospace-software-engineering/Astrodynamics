// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// Whether two atmospheric models give the same values: the comparison of the models of two bodies of one NAIF
/// identifier, which a propagation or a scenario accepts only when they are equal (#674).
/// </summary>
internal static class AtmosphericModelComparer
{
    /// <summary>
    /// True when both are null, the same instance, two instances of the stateless <see cref="EarthStandardAtmosphere"/>
    /// or <see cref="MarsStandardAtmosphere"/> (the exact types: a derived class compares by
    /// <see cref="object.Equals(object)"/>), or two <see cref="Nrlmsise00Model"/> with the same switches and space weather,
    /// compared field by field; any other model compares by <see cref="object.Equals(object)"/>.
    /// </summary>
    /// <remarks>
    /// The record equality of <see cref="SpaceWeather"/> cannot be used: it compares <see cref="ApArray.A"/> by reference,
    /// so that <c>SpaceWeather.Nominal == SpaceWeather.Nominal</c> is false.
    /// </remarks>
    internal static bool AreEquivalent(IAtmosphericModel first, IAtmosphericModel second)
    {
        if (ReferenceEquals(first, second)) return true;
        if (first == null || second == null) return false;
        var type = first.GetType();
        if (type != second.GetType()) return false;
        if (type == typeof(EarthStandardAtmosphere) || type == typeof(MarsStandardAtmosphere)) return true;
        if (type == typeof(Nrlmsise00Model)) return ((Nrlmsise00Model)first).HasSameParameters((Nrlmsise00Model)second);
        return first.Equals(second);
    }
}
