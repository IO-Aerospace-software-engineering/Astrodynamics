// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.TimeSystem.Frames;

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
public class Nrlmsise00Model : IAtmosphericModel, IGeodeticDensityModel
{
    // The default 3 hr AP indices, one instance: the model only reads them.
    private static readonly ApArray DefaultApArray = ApArray.Default;

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
    /// Get complete atmospheric data at given context including molecular density details.
    /// </summary>
    /// <param name="context">Atmospheric context with altitude, position, and time.</param>
    /// <returns>Atmosphere record with temperature, pressure, density, and NRLMSISE-00 specific details.</returns>
    /// <exception cref="ArgumentException">Thrown if context lacks required epoch or geodetic position.</exception>
    public Atmosphere GetAtmosphere(IAtmosphericContext context)
    {
        var output = ComputeAtmosphere(context);
        const double kB = 1.380649e-23; // Boltzmann constant (J/K)

        // Sum number densities (particles/m³) from all species except total mass density
        double totalNumberDensity = output.D[0] + output.D[1] + output.D[2] +
                                    output.D[3] + output.D[4] + output.D[6] + output.D[7];

        double pressurePa = totalNumberDensity * kB * output.T[1];

        return new Atmosphere
        {
            Temperature = output.T[1] - Constants.Kelvin,
            Pressure = pressurePa / 1000.0,
            Density = output.D[5],
            Details = new NrlmsiseDetails
            {
                HeliumDensity = output.D[0],
                AtomicOxygenDensity = output.D[1],
                NitrogenDensity = output.D[2],
                MolecularOxygenDensity = output.D[3],
                ArgonDensity = output.D[4],
                HydrogenDensity = output.D[6],
                AtomicNitrogenDensity = output.D[7],
                AnomalousOxygenDensity = output.D[8],
                ExosphericTemperature = output.T[0]
            }
        };
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
        var output = ComputeAtmosphere(context);
        const double kB = 1.380649e-23; // Boltzmann constant (J/K)

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
        Validate(context);
        return GetDensity(context.Altitude, context.GeodeticLatitude.Value, context.GeodeticLongitude.Value,
            context.Epoch.Value);
    }

    /// <summary>
    /// Total mass density at a geodetic position and an epoch, the value of
    /// <see cref="GetDensity(IAtmosphericContext)"/> for the same context, without allocating.
    /// </summary>
    /// <param name="altitude">Altitude, in m.</param>
    /// <param name="geodeticLatitude">Geodetic latitude, in rad.</param>
    /// <param name="geodeticLongitude">Geodetic longitude, in rad.</param>
    /// <param name="epoch">Epoch.</param>
    /// <returns>Density in kg/m³.</returns>
    double IGeodeticDensityModel.GetDensity(double altitude, double geodeticLatitude, double geodeticLongitude,
        in Time epoch)
    {
        return GetDensity(altitude, geodeticLatitude, geodeticLongitude, epoch);
    }

    private double GetDensity(double altitude, double geodeticLatitude, double geodeticLongitude, in Time epoch)
    {
        var input = BuildInput(altitude, geodeticLatitude, geodeticLongitude, epoch);

        // Thread-safe call to model
        lock (_lock)
        {
            return _model.CalculateMassDensity(input, _flags); // Total mass density in kg/m³
        }
    }

    /// <summary>
    /// Computes atmosphere using NRLMSISE-00 model.
    /// </summary>
    /// <param name="context">Atmospheric context.</param>
    /// <returns>NRLMSISE-00 output.</returns>
    /// <exception cref="ArgumentException">Thrown if context lacks required data.</exception>
    private NrlmsiseOutput ComputeAtmosphere(IAtmosphericContext context)
    {
        Validate(context);
        var input = BuildInput(context.Altitude, context.GeodeticLatitude.Value, context.GeodeticLongitude.Value,
            context.Epoch.Value);
        var output = new NrlmsiseOutput();

        // Thread-safe call to model
        lock (_lock)
        {
            _model.Calculate(input, _flags, output);
        }

        return output;
    }

    /// <exception cref="ArgumentException">The context lacks the epoch or the geodetic position.</exception>
    private static void Validate(IAtmosphericContext context)
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
    }

    /// <summary>
    /// The inputs of the model, in its internal units, for a geodetic position in SI units at an epoch: day of year,
    /// seconds of the day, local solar time and the space weather of the model. The model takes universal time (the
    /// notes on the input variables of the model, <see cref="NrlmsiseInput"/>: seconds in day (UT), and lst =
    /// sec/3600 + longitude/15° for consistency; J. M. Picone, A. E. Hedin, D. P. Drob, A. C. Aikin, "NRLMSISE-00
    /// empirical model of the atmosphere: Statistical comparisons and scientific issues", J. Geophys. Res. 107(A12),
    /// 1468, 2002, to be verified by S. Guillet): the epoch is taken in UTC whatever its time scale (#409; a
    /// propagation evaluates drag at TDB epochs, 69.2 s ahead of UTC in 2021). UT1 is not used: |UT1 − UTC| stays
    /// below 0.9 s.
    /// </summary>
    private NrlmsiseModelInput BuildInput(double altitude, double geodeticLatitude, double geodeticLongitude,
        in Time epoch)
    {
        var dateTime = (epoch.Frame is UTCTimeFrame ? epoch : epoch.ToUTC()).DateTime;
        var doy = dateTime.DayOfYear;
        // The time of day to the tick (100 ns); it used to be truncated to the millisecond
        var sec = dateTime.TimeOfDay.TotalSeconds;

        // Calculate local solar time (hours)
        double lst = (sec / 3600.0) + (geodeticLongitude * Constants.Rad2Deg / 15.0);
        if (lst < 0) lst += 24.0;
        if (lst >= 24.0) lst -= 24.0;

        return NrlmsiseModelInput.FromSi(doy, sec, altitude, geodeticLatitude, geodeticLongitude, lst,
            _spaceWeather.F107A, _spaceWeather.F107, _spaceWeather.Ap, _spaceWeather.ApArray ?? DefaultApArray);
    }
}
