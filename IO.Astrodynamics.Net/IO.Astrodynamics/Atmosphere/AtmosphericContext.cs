// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

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
    /// Creates a full context with position and time (for complex atmospheric models).
    /// </summary>
    /// <param name="altitude">Altitude in meters.</param>
    /// <param name="geodeticLatitude">Geodetic latitude in radians.</param>
    /// <param name="geodeticLongitude">Geodetic longitude in radians.</param>
    /// <param name="epoch">Epoch time.</param>
    /// <returns>Atmospheric context with full data.</returns>
    public static AtmosphericContext FromPlanetodetic(
        double altitude,
        double geodeticLatitude,
        double geodeticLongitude,
        Time epoch) => new()
    {
        Altitude = altitude,
        GeodeticLatitude = geodeticLatitude,
        GeodeticLongitude = geodeticLongitude,
        Epoch = epoch
    };
}
