// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Frames;

namespace IO.Astrodynamics.Body;

/// <summary>
/// An object of the kernels read by its NAIF identifier alone, which is neither a celestial body nor a site of one: an
/// identifier of the form of a site (<see cref="SpiceApi.IsSiteId"/>, body × 1000 + site) whose body SPICE does not name
/// (<see cref="SpiceApi.IsSiteOfNamedBody"/>), as SPICE's built-in NOTO (398989) and NEW NORCIA (398990), ESA stations of
/// no body 398 (#750). Its ephemeris is SPICE's (<see cref="CelestialItem.IsSpiceBacked"/>): as observer of
/// <see cref="SpiceApi.ReadEphemeris(TimeSystem.Time, int, int, Frame, Aberration)"/> the read is <c>spkezr_c</c>'s from its
/// identifier, which gives the state when its SPK is loaded and SPICE's missing-data error otherwise
/// (<see cref="System.InvalidOperationException"/>).
/// </summary>
/// <remarks>
/// Built by the natural-body constructor of <see cref="CelestialItem"/>, which reads its information from the data
/// provider (its SPICE name, a GM of 0 without <c>BODYnnn_GM</c>) and gives it no orbit; its centre and barycentre of
/// motion are those of an identifier above 1000 (#651: 10 and 0). Before 11.0.0 the reads by identifiers built it as a
/// site of the body 398, which threw <see cref="System.ArgumentException"/> "The NAIF identifier 398 has no name in
/// SPICE", and <see cref="CelestialItem.Create"/> as a <see cref="CelestialBody"/>, which has no frame for it.
/// </remarks>
internal sealed class KernelObject : CelestialItem
{
    /// <summary>The object of <paramref name="naifId"/>, read from the data provider.</summary>
    /// <exception cref="System.ArgumentException">SPICE does not name <paramref name="naifId"/>.</exception>
    internal KernelObject(int naifId) : base(naifId, Frame.ICRF, TimeSystem.Time.J2000Tdb)
    {
        RegisterWithObserver();
    }
}
