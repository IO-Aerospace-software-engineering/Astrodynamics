// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// U.S. Standard Atmosphere 1976 model for Earth.
/// </summary>
/// <remarks>
/// Simple analytical model valid for altitudes up to approximately 86 km.
/// Uses only altitude - ignores time, position, and space weather.
/// For more accurate modeling at higher altitudes or with time-varying conditions,
/// use Nrlmsise00Model.
/// Atmospheric drag reads the density from the altitude without building a context
/// (<see cref="IGeodeticDensityModel"/>), so a derived class that re-implements <see cref="IAtmosphericModel"/> is not
/// used by drag, as for <see cref="NRLMSISE_00.Nrlmsise00Model"/>.
/// </remarks>
public class EarthStandardAtmosphere : IAtmosphericModel, IGeodeticDensityModel
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

    private static double Temperature(double altitude)
    {
        if (altitude < 11000.0)
        {
            return 15.04 - 0.00649 * altitude;
        }

        if (altitude < 25000.0)
        {
            return -56.46;
        }

        return double.Min(-131.21 + 0.00299 * altitude, 2200.0);
    }

    private static double Pressure(double altitude)
    {
        if (altitude < 11000.0)
        {
            return 101.29 * System.Math.Pow(((Temperature(altitude) + Constants.Kelvin) / 288.08), 5.256);
        }

        if (altitude < 25000.0)
        {
            return 22.65 * System.Math.Exp(1.73 - .000157 * altitude);
        }

        return 2.488 * System.Math.Pow(((Temperature(altitude) + Constants.Kelvin) / 216.6), -11.388);
    }

    private static double Density(double altitude)
    {
        return Pressure(altitude) / (0.2869 * (Temperature(altitude) + Constants.Kelvin));
    }
}
