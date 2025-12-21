// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// Simple Mars atmospheric model.
/// </summary>
/// <remarks>
/// Simplified analytical model. Uses only altitude - ignores time, position, and seasonal variations.
/// </remarks>
public class MarsStandardAtmosphere : IAtmosphericModel
{
    /// <summary>
    /// Get temperature at given atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context (only altitude is used).</param>
    /// <returns>Temperature in Celsius.</returns>
    public double GetTemperature(IAtmosphericContext context)
    {
        double altitude = context.Altitude;

        if (altitude < 7000.0)
        {
            return -31.0 - 0.000998 * altitude;
        }

        return -23.4 - 0.00222 * altitude;
    }

    /// <summary>
    /// Get pressure at given atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context (only altitude is used).</param>
    /// <returns>Pressure in kPa.</returns>
    public double GetPressure(IAtmosphericContext context)
    {
        return 0.699 * System.Math.Exp(-0.00009 * context.Altitude);
    }

    /// <summary>
    /// Get density at given atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context (only altitude is used).</param>
    /// <returns>Density in kg/m³.</returns>
    public double GetDensity(IAtmosphericContext context)
    {
        return GetPressure(context) / (0.1921 * (GetTemperature(context) + Constants.Kelvin));
    }
}
