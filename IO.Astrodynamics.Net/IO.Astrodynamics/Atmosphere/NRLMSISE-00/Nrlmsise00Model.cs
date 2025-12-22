// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.Atmosphere.NRLMSISE_00;

/// <summary>
/// NRLMSISE-00 atmospheric model adapter implementing the framework interface.
/// </summary>
/// <remarks>
/// This model requires full atmospheric context including time, position,
/// and space weather data (F10.7, Ap indices). Thread-safe for concurrent use.
/// NRLMSISE-00 (Naval Research Laboratory Mass Spectrometer and Incoherent Scatter Radar)
/// is an empirical model of Earth's atmosphere from ground to space.
/// </remarks>
public class Nrlmsise00Model : IAtmosphericModel
{
    private readonly NRLMSISE00 _model;
    private readonly NrlmsiseFlags _flags;
    private readonly SpaceWeather _spaceWeather;
    private readonly object _lock = new object();

    /// <summary>
    /// Creates NRLMSISE-00 model with default nominal conditions.
    /// </summary>
    /// <remarks>
    /// Uses nominal values: F10.7 = 150, F10.7A = 150, Ap = 4.
    /// These represent quiet solar and geomagnetic conditions.
    /// </remarks>
    public Nrlmsise00Model() : this(SpaceWeather.Nominal)
    {
    }

    /// <summary>
    /// Creates NRLMSISE-00 model with specified space weather data.
    /// </summary>
    /// <param name="spaceWeather">Space weather data containing F10.7, Ap indices, etc.</param>
    public Nrlmsise00Model(SpaceWeather spaceWeather)
    {
        _model = new NRLMSISE00();
        _flags = NrlmsiseFlags.CreateStandard();
        _spaceWeather = spaceWeather ?? throw new ArgumentNullException(nameof(spaceWeather));
    }

    /// <summary>
    /// Creates NRLMSISE-00 model with custom flags and space weather data.
    /// </summary>
    /// <param name="flags">NRLMSISE-00 configuration flags.</param>
    /// <param name="spaceWeather">Space weather data containing F10.7, Ap indices, etc.</param>
    public Nrlmsise00Model(NrlmsiseFlags flags, SpaceWeather spaceWeather)
    {
        _model = new NRLMSISE00();
        _flags = flags ?? throw new ArgumentNullException(nameof(flags));
        _spaceWeather = spaceWeather ?? throw new ArgumentNullException(nameof(spaceWeather));
    }

    /// <summary>
    /// Get temperature at given atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context with altitude, position, and time.</param>
    /// <returns>Temperature in Celsius.</returns>
    /// <exception cref="ArgumentException">Thrown if context lacks required epoch or geodetic position.</exception>
    public double GetTemperature(IAtmosphericContext context)
    {
        var output = ComputeAtmosphere(context);
        return output.T[1] - Constants.Kelvin; // T[1] is temperature at altitude, convert to Celsius
    }

    /// <summary>
    /// Get pressure at given atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context with altitude, position, and time.</param>
    /// <returns>Pressure in kPa.</returns>
    /// <exception cref="ArgumentException">Thrown if context lacks required epoch or geodetic position.</exception>
    public double GetPressure(IAtmosphericContext context)
    {
        // NRLMSISE-00 doesn't directly provide pressure, compute from ideal gas law
        var output = ComputeAtmosphere(context);
        const double kB = 1.380649e-23; // Boltzmann constant (J/K)

        // Sum number densities (particles/m³) from all species except total mass density
        double totalNumberDensity = output.D[0] + output.D[1] + output.D[2] +
                                   output.D[3] + output.D[4] + output.D[6] + output.D[7];

        double pressurePa = totalNumberDensity * kB * output.T[1]; // Pressure in Pascals
        return pressurePa / 1000.0; // Convert to kPa
    }

    /// <summary>
    /// Get density at given atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context with altitude, position, and time.</param>
    /// <returns>Density in kg/m³.</returns>
    /// <exception cref="ArgumentException">Thrown if context lacks required epoch or geodetic position.</exception>
    public double GetDensity(IAtmosphericContext context)
    {
        var output = ComputeAtmosphere(context);
        return output.D[5]; // Total mass density in kg/m³
    }

    /// <summary>
    /// Computes atmosphere using NRLMSISE-00 model.
    /// </summary>
    /// <param name="context">Atmospheric context.</param>
    /// <returns>NRLMSISE-00 output.</returns>
    /// <exception cref="ArgumentException">Thrown if context lacks required data.</exception>
    private NrlmsiseOutput ComputeAtmosphere(IAtmosphericContext context)
    {
        if (!context.Epoch.HasValue)
        {
            throw new ArgumentException(
                "NRLMSISE-00 requires time information. Ensure the atmospheric context includes Epoch.",
                nameof(context));
        }

        if (!context.GeodeticLatitude.HasValue || !context.GeodeticLongitude.HasValue)
        {
            throw new ArgumentException(
                "NRLMSISE-00 requires geodetic position. Ensure the atmospheric context includes GeodeticLatitude and GeodeticLongitude.",
                nameof(context));
        }

        var epoch = context.Epoch.Value;
        var dateTime = epoch.DateTime;
        var year = dateTime.Year;
        var doy = dateTime.DayOfYear;
        var sec = (dateTime.Hour * 3600.0) + (dateTime.Minute * 60.0) + dateTime.Second + (dateTime.Millisecond / 1000.0);

        // Calculate local solar time (hours)
        double lst = (sec / 3600.0) + (context.GeodeticLongitude.Value * Constants.Rad2Deg / 15.0);
        if (lst < 0) lst += 24.0;
        if (lst >= 24.0) lst -= 24.0;

        var input = new NrlmsiseInput
        {
            Year = year,
            Doy = doy,
            Sec = sec,
            Alt = context.Altitude,
            GLat = context.GeodeticLatitude.Value,
            GLong = context.GeodeticLongitude.Value,
            Lst = lst,
            F107A = _spaceWeather.F107A,
            F107 = _spaceWeather.F107,
            Ap = _spaceWeather.Ap,
            ApA = _spaceWeather.ApArray ?? ApArray.Default
        };

        var output = new NrlmsiseOutput();

        // Thread-safe call to model
        lock (_lock)
        {
            _model.Calculate(input, _flags, output);
        }

        return output;
    }
}
