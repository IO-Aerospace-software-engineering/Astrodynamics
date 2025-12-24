// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// Encapsulates all contextual information needed for atmospheric calculations.
/// </summary>
/// <remarks>
/// Provides altitude, position, time, and other environmental data to atmospheric models.
/// Simple models may only use altitude, while complex models can access all available context.
/// </remarks>
public interface IAtmosphericContext
{
    /// <summary>
    /// Altitude above reference surface in meters.
    /// </summary>
    double Altitude { get; }

    /// <summary>
    /// Geodetic latitude in radians, if available.
    /// </summary>
    double? GeodeticLatitude { get; }

    /// <summary>
    /// Geodetic longitude in radians, if available.
    /// </summary>
    double? GeodeticLongitude { get; }

    /// <summary>
    /// Epoch time, if available.
    /// </summary>
    Time? Epoch { get; }
}
