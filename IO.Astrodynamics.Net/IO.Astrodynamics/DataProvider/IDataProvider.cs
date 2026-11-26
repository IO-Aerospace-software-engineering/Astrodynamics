using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.DataProvider;

/// <summary>
/// The source of the data the library reads about the solar system: the orientation of frames, the states of bodies and
/// the information of celestial bodies, in domain types. <see cref="SpiceDataProvider"/> reads the loaded SPICE kernels,
/// <see cref="MemoryDataProvider"/> what was stored in it; <see cref="Configuration.SetDataProvider"/> sets the provider
/// of the process.
/// </summary>
/// <remarks>
/// <para>
/// The exception contract, which the library's handlers of missing data rely on (#680, R04 of #668): an unknown name (a
/// frame) or identifier (a body) throws <see cref="ArgumentException"/>, naming the parameter; a known item without data
/// at the date (outside its coverage, a state the kernels do not chain) throws <see cref="InvalidOperationException"/>,
/// <see cref="Exceptions.SpiceException"/> on SPICE. A body whose barycentre's data the provider lacks then builds without
/// an orbit, as on SPICE.
/// </para>
/// <para>
/// Changed in 11.0.0 (R04 with R20 of #668, so that a custom provider changes once): <see cref="GetEphemeris"/> returns a
/// <see cref="StateVector"/>, <see cref="OrbitalParameters.OrbitalParameters"/> before, and
/// <see cref="GetCelestialBodyInfo"/> a <see cref="CelestialBodyInfo"/>, the interop struct
/// <c>IO.Astrodynamics.DTO.CelestialBody</c> before. A provider runs under the SPICE lock on some paths (an ephemeris read
/// in TEME, CIRS or TIRS): it must not read the static classes <c>PlanetsAndMoons</c>, <c>Stars</c> or <c>Barycenters</c>
/// there (#623).
/// </para>
/// </remarks>
public interface IDataProvider
{
    /// <summary>The orientation of <paramref name="source"/> relative to ICRF at <paramref name="epoch"/>: the rotation from
    /// the frame to ICRF and the angular velocity of the frame, labelled with <paramref name="source"/>.</summary>
    /// <param name="epoch">The epoch of the orientation.</param>
    /// <param name="source">The frame to orient.</param>
    /// <exception cref="ArgumentException">The provider knows no frame of that name.</exception>
    /// <exception cref="InvalidOperationException">The provider has no orientation of the frame at that epoch.</exception>
    StateOrientation FrameTransformationToICRF(in Time epoch, Frame source);

    /// <summary>The state of <paramref name="target"/> relative to <paramref name="observer"/> in <paramref name="frame"/>
    /// at <paramref name="epoch"/>, corrected by <paramref name="aberration"/>, labelled with <paramref name="epoch"/>, the
    /// observer and the frame.</summary>
    /// <param name="epoch">The epoch of the state.</param>
    /// <param name="target">The object whose state is read.</param>
    /// <param name="observer">The origin of the state.</param>
    /// <param name="frame">The frame of the state.</param>
    /// <param name="aberration">The aberration correction.</param>
    /// <exception cref="ArgumentException">The provider knows no frame of the name of <paramref name="frame"/>.</exception>
    /// <exception cref="InvalidOperationException">The provider has no state of the target or the observer at that epoch.</exception>
    StateVector GetEphemeris(in Time epoch, ILocalizable target, ILocalizable observer, Frame frame, Aberration aberration);

    /// <summary>The information of the celestial body of <paramref name="naifId"/>, never null.</summary>
    /// <param name="naifId">The NAIF identifier of the body.</param>
    /// <exception cref="ArgumentException">The provider has no information on that identifier (<c>naifId</c>).</exception>
    CelestialBodyInfo GetCelestialBodyInfo(int naifId);
}
