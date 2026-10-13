// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// An atmospheric model that gives the density at a geodetic position and an epoch without an
/// <see cref="IAtmosphericContext"/>, and without allocating.
/// </summary>
/// <remarks>
/// For the forces evaluated at every stage of an integration (drag and its partials): the value is that of
/// <see cref="IAtmosphericModel.GetDensity"/> for the same context, the same double.
/// </remarks>
internal interface IGeodeticDensityModel
{
    /// <summary>
    /// Density at a geodetic position and an epoch.
    /// </summary>
    /// <param name="altitude">Altitude, in m.</param>
    /// <param name="geodeticLatitude">Geodetic latitude, in rad.</param>
    /// <param name="geodeticLongitude">Geodetic longitude, in rad.</param>
    /// <param name="epoch">Epoch.</param>
    /// <returns>Density in kg/m³.</returns>
    double GetDensity(double altitude, double geodeticLatitude, double geodeticLongitude, in Time epoch);
}
