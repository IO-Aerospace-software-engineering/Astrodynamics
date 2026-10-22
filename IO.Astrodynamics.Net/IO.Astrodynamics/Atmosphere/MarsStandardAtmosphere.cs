// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// Simple Mars atmospheric model.
/// </summary>
/// <remarks>
/// Simplified analytical model. Uses only altitude - ignores time, position, and seasonal variations.
/// Atmospheric drag reads the density from the altitude without building a context
/// (<see cref="IGeodeticDensityModel"/>), so a derived class that re-implements <see cref="IAtmosphericModel"/> is not
/// used by drag, as for <see cref="NRLMSISE_00.Nrlmsise00Model"/>. Its density depends on the altitude alone, so the drag
/// partials take its derivative in closed form (<see cref="IAltitudeDensityDerivative"/>).
/// </remarks>
public class MarsStandardAtmosphere : IAtmosphericModel, IGeodeticDensityModel, IAltitudeDensityDerivative
{
    /// <summary>
    /// Get complete atmospheric data at given context.
    /// </summary>
    /// <param name="context">Atmospheric context (only altitude is used).</param>
    /// <returns>Atmosphere record with temperature, pressure, and density.</returns>
    public Atmosphere GetAtmosphere(IAtmosphericContext context)
    {
        return new Atmosphere
        {
            Temperature = GetTemperature(context),
            Pressure = GetPressure(context),
            Density = GetDensity(context)
        };
    }

    /// <summary>
    /// Get temperature at given atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context (only altitude is used).</param>
    /// <returns>Temperature in Celsius.</returns>
    public double GetTemperature(IAtmosphericContext context)
    {
        return Temperature(context.Altitude);
    }

    /// <summary>
    /// Get pressure at given atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context (only altitude is used).</param>
    /// <returns>Pressure in kPa.</returns>
    public double GetPressure(IAtmosphericContext context)
    {
        return Pressure(context.Altitude);
    }

    /// <summary>
    /// Get density at given atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context (only altitude is used).</param>
    /// <returns>Density in kg/m³.</returns>
    public double GetDensity(IAtmosphericContext context)
    {
        return Density(context.Altitude);
    }

    /// <summary>
    /// Density at an altitude, without allocating: the value of <see cref="GetDensity(IAtmosphericContext)"/> for the
    /// same altitude, the same double. The position and the epoch are not used.
    /// </summary>
    double IGeodeticDensityModel.GetDensity(double altitude, double geodeticLatitude, double geodeticLongitude,
        in TimeSystem.Time epoch)
    {
        return Density(altitude);
    }

    /// <summary>
    /// dρ/dh, by differentiation of the closed form: ρ = P / (0.1921 T_K), P ∝ exp(−0.00009 h), so
    /// dρ/dh = ρ (−0.00009 − T_K′ / T_K), with T_K′ = −0.000998 K/m below 7 km and −0.00222 K/m above.
    /// </summary>
    double IAltitudeDensityDerivative.GetDensityAltitudeDerivative(double altitude)
    {
        double temperatureSlope = altitude < 7000.0 ? -0.000998 : -0.00222;
        return Density(altitude) * (-0.00009 - temperatureSlope / (Temperature(altitude) + Constants.Kelvin));
    }

    private static double Temperature(double altitude)
    {
        if (altitude < 7000.0)
        {
            return -31.0 - 0.000998 * altitude;
        }

        return -23.4 - 0.00222 * altitude;
    }

    private static double Pressure(double altitude)
    {
        return 0.699 * System.Math.Exp(-0.00009 * altitude);
    }

    private static double Density(double altitude)
    {
        return Pressure(altitude) / (0.1921 * (Temperature(altitude) + Constants.Kelvin));
    }
}
