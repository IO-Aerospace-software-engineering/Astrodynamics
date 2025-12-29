// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// Provides atmospheric properties at a given context.
/// </summary>
/// <remarks>
/// All atmospheric models implement this interface to provide temperature,
/// pressure, and density values. Simple models may only use altitude from
/// the context, while complex models can use time, position, and environmental data.
/// </remarks>
public interface IAtmosphericModel
{
    /// <summary>
    /// Get complete atmospheric data at given context.
    /// </summary>
    /// <param name="context">Atmospheric context containing position, time, and environmental data.</param>
    /// <returns>Atmosphere record with temperature, pressure, density, and optional model-specific details.</returns>
    Atmosphere GetAtmosphere(IAtmosphericContext context);

    /// <summary>
    /// Get temperature at given atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context containing position, time, and environmental data.</param>
    /// <returns>Temperature in Celsius.</returns>
    double GetTemperature(IAtmosphericContext context);

    /// <summary>
    /// Get pressure at given atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context containing position, time, and environmental data.</param>
    /// <returns>Pressure in kPa.</returns>
    double GetPressure(IAtmosphericContext context);

    /// <summary>
    /// Get density at given atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context containing position, time, and environmental data.</param>
    /// <returns>Density in kg/m³.</returns>
    double GetDensity(IAtmosphericContext context);
}
