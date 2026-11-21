// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// Standard implementation of atmospheric context.
/// </summary>
public record AtmosphericContext : IAtmosphericContext
{
    /// <summary>
    /// Altitude above reference surface in meters.
    /// </summary>
    public required double Altitude { get; init; }

    /// <summary>
    /// Geodetic latitude in radians, if available.
    /// </summary>
    public double? GeodeticLatitude { get; init; }

    /// <summary>
    /// Geodetic longitude in radians, if available.
    /// </summary>
    public double? GeodeticLongitude { get; init; }

    /// <summary>
    /// Epoch time, if available.
    /// </summary>
    public Time? Epoch { get; init; }

    /// <summary>
    /// Creates a simple context with only altitude (for simple atmospheric models).
    /// </summary>
    /// <param name="altitude">Altitude in meters.</param>
    /// <returns>Atmospheric context with altitude only.</returns>
    public static AtmosphericContext FromAltitude(double altitude) => new()
    {
        Altitude = altitude
    };

    /// <summary>
    /// Creates a full context with position and time (for complex atmospheric models): the altitude, the geodetic latitude
    /// and the longitude of <paramref name="planetodetic"/>. It replaces <c>FromPlanetodetic(altitude, geodeticLatitude,
    /// geodeticLongitude, epoch)</c> of 10.x, whose three doubles ran in the order opposite to that of
    /// <see cref="Planetodetic"/> (R20 of #668).
    /// </summary>
    /// <param name="planetodetic">The geodetic coordinates of the point: longitude and latitude in radians, altitude in
    /// meters.</param>
    /// <param name="epoch">Epoch time.</param>
    /// <returns>Atmospheric context with full data.</returns>
    public static AtmosphericContext FromPlanetodetic(Planetodetic planetodetic, Time epoch) => new()
    {
        Altitude = planetodetic.Altitude,
        GeodeticLatitude = planetodetic.Latitude,
        GeodeticLongitude = planetodetic.Longitude,
        Epoch = epoch
    };
}
