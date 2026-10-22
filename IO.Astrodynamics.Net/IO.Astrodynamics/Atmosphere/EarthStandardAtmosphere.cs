// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// U.S. Standard Atmosphere 1976 model for Earth.
/// </summary>
/// <remarks>
/// <para>
/// Simple analytical model valid for altitudes up to approximately 86 km.
/// Uses only altitude - ignores time, position, and space weather.
/// For more accurate modeling at higher altitudes or with time-varying conditions,
/// use Nrlmsise00Model.
/// </para>
/// <para>
/// The curve fit of the NASA Glenn Research Center, "Earth Atmosphere Model - Metric Units" (Beginner's Guide to
/// Aeronautics), in three branches: below 11 km, T = 15.04 − 0.00649 h °C and p = 101.29 (T_K / 288.08)^5.256 kPa; from
/// 11 to 25 km, T = −56.46 °C and p = 22.65 exp(1.73 − 0.000157 h); above 25 km, T = −131.21 + 0.00299 h °C, held at
/// 2200 °C above 779.67 km, and p = 2.488 (T_K / 216.6)^−11.388; ρ = p / (0.2869 T_K). The branches do not meet: the
/// density jumped by −0.023 % at 11 km and −1.82 % at 25 km, and RK7(8) stopped there (#433). Over
/// 2 <see cref="TransitionHalfWidth"/> (500 m) centred on each boundary, ln p and T go from one branch to the next by a
/// quintic smootherstep, so that the density and its first two derivatives are continuous; the model is unchanged bit
/// for bit outside these intervals. Against the tables of the U.S. Standard Atmosphere 1976 (NASA-TM-X-74335), the
/// density is within +0.20 to +0.25 % below 11 km, +0.12 to +0.24 % in the 11 km transition and −0.96 to +1.20 % in
/// the 25 km one.
/// </para>
/// <para>
/// Atmospheric drag reads the density from the altitude without building a context
/// (<see cref="IGeodeticDensityModel"/>), so a derived class that re-implements <see cref="IAtmosphericModel"/> is not
/// used by drag, as for <see cref="NRLMSISE_00.Nrlmsise00Model"/>. Its density depends on the altitude alone, so the drag
/// partials take its derivative in closed form (<see cref="IAltitudeDensityDerivative"/>).
/// </para>
/// </remarks>
public class EarthStandardAtmosphere : IAtmosphericModel, IGeodeticDensityModel, IAltitudeDensityDerivative
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

    // Boundaries of the branches of the curve fit, m
    private const double TroposphereTop = 11000.0;
    private const double LowerStratosphereTop = 25000.0;

    /// <summary>
    /// Half-width of the transitions, m: over 500 m centred on 11 km and on 25 km, ln p and T go from one branch of the
    /// curve fit to the next by a quintic smootherstep, so that the density and its first two derivatives are
    /// continuous (#433); the branches do not meet (the density jumped by −0.023 % at 11 km and −1.82 % at 25 km).
    /// </summary>
    internal const double TransitionHalfWidth = 250.0;

    /// <summary>
    /// dρ/dh, by differentiation of the closed form of the branch the altitude falls in. With T_K the absolute
    /// temperature: below 11 km, ρ ∝ T_K^(5.256 − 1) with dT/dh = −0.00649 K/m; from 11 to 25 km, T is constant and
    /// ρ ∝ exp(−0.000157 h); above 25 km, ρ ∝ T_K^(−11.388 − 1) with dT/dh = 0.00299 K/m, and dρ/dh = 0 where the
    /// temperature is held at 2200 °C (above 779.67 km), where the density is constant. In a transition between branches
    /// 0 and 1: d ln p/dh = (1 − w) s₀ + w s₁ + w′ (ln p₁ − ln p₀) and T_K′ = (1 − w) T₀′ + w T₁′ + w′ (T₁ − T₀), so
    /// dρ/dh = ρ (d ln p/dh − T_K′ / T_K).
    /// </summary>
    double IAltitudeDensityDerivative.GetDensityAltitudeDerivative(double altitude)
    {
        int transition = Transition(altitude);
        if (transition >= 0)
        {
            var (w, dw) = Weight(altitude, transition);
            double lowerLogPressure = System.Math.Log(BranchPressure(transition, altitude));
            double upperLogPressure = System.Math.Log(BranchPressure(transition + 1, altitude));
            double lowerTemperature = BranchTemperature(transition, altitude);
            double upperTemperature = BranchTemperature(transition + 1, altitude);
            double logPressureSlope = (1.0 - w) * BranchLogPressureSlope(transition, altitude) +
                                      w * BranchLogPressureSlope(transition + 1, altitude) +
                                      dw * (upperLogPressure - lowerLogPressure);
            double temperatureSlope = (1.0 - w) * BranchTemperatureSlope(transition, altitude) +
                                      w * BranchTemperatureSlope(transition + 1, altitude) +
                                      dw * (upperTemperature - lowerTemperature);
            double temperature = (1.0 - w) * lowerTemperature + w * upperTemperature + Constants.Kelvin;
            return Density(altitude) * (logPressureSlope - temperatureSlope / temperature);
        }

        double absoluteTemperature = Temperature(altitude) + Constants.Kelvin;
        double logarithmicDerivative;
        if (altitude < TroposphereTop)
        {
            logarithmicDerivative = (5.256 - 1.0) * -0.00649 / absoluteTemperature;
        }
        else if (altitude < LowerStratosphereTop)
        {
            logarithmicDerivative = -0.000157;
        }
        else
        {
            logarithmicDerivative = -131.21 + 0.00299 * altitude < 2200.0
                ? (-11.388 - 1.0) * 0.00299 / absoluteTemperature
                : 0.0;
        }

        return Density(altitude) * logarithmicDerivative;
    }

    // The transition the altitude falls in: 0 around 11 km, 1 around 25 km, −1 outside both
    private static int Transition(double altitude)
    {
        if (altitude >= TroposphereTop - TransitionHalfWidth && altitude < TroposphereTop + TransitionHalfWidth)
        {
            return 0;
        }

        if (altitude >= LowerStratosphereTop - TransitionHalfWidth && altitude < LowerStratosphereTop + TransitionHalfWidth)
        {
            return 1;
        }

        return -1;
    }

    // Weight of the upper branch in a transition and its derivative in altitude
    private static (double Weight, double Slope) Weight(double altitude, int transition)
    {
        double boundary = transition == 0 ? TroposphereTop : LowerStratosphereTop;
        return Smootherstep.At(altitude, boundary - TransitionHalfWidth, 2.0 * TransitionHalfWidth);
    }

    // The branches of the curve fit at any altitude: 0 the troposphere, 1 the lower stratosphere, 2 the upper
    // stratosphere; the temperature in °C and its derivative, the pressure in kPa and the derivative of its logarithm
    private static double BranchTemperature(int branch, double altitude) => branch switch
    {
        0 => 15.04 - 0.00649 * altitude,
        1 => -56.46,
        _ => double.Min(-131.21 + 0.00299 * altitude, 2200.0)
    };

    private static double BranchTemperatureSlope(int branch, double altitude) => branch switch
    {
        0 => -0.00649,
        1 => 0.0,
        _ => -131.21 + 0.00299 * altitude < 2200.0 ? 0.00299 : 0.0
    };

    private static double BranchPressure(int branch, double altitude) => branch switch
    {
        0 => 101.29 * System.Math.Pow((BranchTemperature(0, altitude) + Constants.Kelvin) / 288.08, 5.256),
        1 => 22.65 * System.Math.Exp(1.73 - .000157 * altitude),
        _ => 2.488 * System.Math.Pow((BranchTemperature(2, altitude) + Constants.Kelvin) / 216.6, -11.388)
    };

    private static double BranchLogPressureSlope(int branch, double altitude) => branch switch
    {
        0 => 5.256 * -0.00649 / (BranchTemperature(0, altitude) + Constants.Kelvin),
        1 => -0.000157,
        _ => -11.388 * BranchTemperatureSlope(2, altitude) / (BranchTemperature(2, altitude) + Constants.Kelvin)
    };

    private static int Branch(double altitude) => altitude < TroposphereTop ? 0 : altitude < LowerStratosphereTop ? 1 : 2;

    private static double Temperature(double altitude)
    {
        int transition = Transition(altitude);
        if (transition < 0)
        {
            return BranchTemperature(Branch(altitude), altitude);
        }

        var (w, _) = Weight(altitude, transition);
        return (1.0 - w) * BranchTemperature(transition, altitude) + w * BranchTemperature(transition + 1, altitude);
    }

    private static double Pressure(double altitude)
    {
        int transition = Transition(altitude);
        if (transition < 0)
        {
            return BranchPressure(Branch(altitude), altitude);
        }

        var (w, _) = Weight(altitude, transition);
        return System.Math.Exp((1.0 - w) * System.Math.Log(BranchPressure(transition, altitude)) +
                               w * System.Math.Log(BranchPressure(transition + 1, altitude)));
    }

    private static double Density(double altitude)
    {
        return Pressure(altitude) / (0.2869 * (Temperature(altitude) + Constants.Kelvin));
    }
}
