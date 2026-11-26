// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.DataProvider;

/// <summary>
/// The information a data provider gives on a celestial body: its identifiers, name, radii, GM, body-fixed frame and
/// zonal harmonics, in SI units. <see cref="IDataProvider.GetCelestialBodyInfo"/> returns it, <see cref="SpiceApi.GetCelestialBodyInfo"/>
/// reads it from the kernels, and <see cref="MemoryDataProvider.AddCelestialBodyInfo"/> stores it; a natural body
/// (<c>new CelestialBody(id)</c>, the static objects of <c>PlanetsAndMoons</c>, <c>Stars</c> and <c>Barycenters</c>) is
/// built from it.
/// </summary>
/// <param name="NaifId">The NAIF identifier of the body.</param>
/// <param name="Name">Its name, as SPICE names it (<c>EARTH</c>); a body without a name cannot be built.</param>
/// <param name="CenterOfMotionId">The NAIF identifier of the body it orbits: 10 for a planet, its planet for a moon, its
/// body for a site.</param>
/// <param name="BarycenterOfMotionId">The NAIF identifier of the barycentre of its system (3 for the Earth and the Moon), 0
/// for the Sun and the barycentres.</param>
/// <param name="Radii">The radii of its ellipsoid along its body-fixed x, y and z axes, in m (SPICE's <c>BODYnnn_RADII</c>,
/// in km in the kernels); zero for a body the kernels give none, such as a barycentre.</param>
/// <param name="GM">Its gravitational parameter, in m³/s² (SPICE's <c>BODYnnn_GM</c>, in km³/s² in the kernels); 0 for a
/// body the kernels give none.</param>
/// <param name="FrameName">The name of its body-fixed frame (<c>ITRF93</c>, <c>IAU_MARS</c>), empty for a barycentre.</param>
/// <param name="FrameId">The identifier of that frame (13000 for ITRF93), 0 when not given.</param>
/// <param name="J2">Its J2 zonal harmonic, NaN when not given (as SPICE gives it for a body without <c>BODYnnn_J2</c>).</param>
/// <param name="J3">Its J3 zonal harmonic, NaN when not given.</param>
/// <param name="J4">Its J4 zonal harmonic, NaN when not given.</param>
/// <remarks>
/// New in 11.0.0 (R04 of #668): it replaces the interop struct <c>IO.Astrodynamics.DTO.CelestialBody</c>, public before,
/// whose name clashed with <see cref="Body.CelestialBody"/>. Two records are equal when every value is (<c>double.Equals</c>:
/// NaN equal to NaN).
/// </remarks>
public sealed record CelestialBodyInfo(
    int NaifId,
    string Name,
    int CenterOfMotionId,
    int BarycenterOfMotionId,
    Vector3 Radii,
    double GM,
    string FrameName,
    int FrameId = 0,
    double J2 = double.NaN,
    double J3 = double.NaN,
    double J4 = double.NaN)
{
    // The record of the native layer, field for field: its values are SI already (the proxy converts the radii to m)
    internal static CelestialBodyInfo FromNative(in DTO.CelestialBody record) =>
        new(record.Id, record.Name, record.CenterOfMotionId, record.BarycenterOfMotionId,
            new Vector3(record.Radii.X, record.Radii.Y, record.Radii.Z), record.GM, record.FrameName, record.FrameId,
            record.J2, record.J3, record.J4);
}
