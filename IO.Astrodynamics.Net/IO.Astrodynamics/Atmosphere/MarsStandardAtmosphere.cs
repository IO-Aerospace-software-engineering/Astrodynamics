// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// Simple Mars atmospheric model.
/// </summary>
/// <remarks>
/// <para>
/// Simplified analytical model. Uses only altitude - ignores time, position, and seasonal variations.
/// Below <see cref="IsothermalLayerBase"/> (49.887 km), the curve fit of the NASA Glenn Research Center, "Mars
/// Atmosphere Model - Metric Units" (Beginner's Guide to Aeronautics): T = −31 − 0.000998 h °C below 7 km,
/// T = −23.4 − 0.00222 h °C above, p = 0.699 exp(−0.00009 h) kPa, ρ = p / (0.1921 T_K). Above it, where that
/// temperature reaches 139 K, the isothermal layer of A. Seiff, "Post-Viking models for the structure of the summer
/// atmosphere of Mars", in The Mars Reference Atmosphere, A. J. Kliore (ed.), Advances in Space Research 2(2) (1982),
/// isothermal at 139 K from 72 to 100 km: the temperature held at 139 K and the hydrostatic pressure of Seiff's
/// equation at constant temperature with the inverse-square gravity, p = p_b exp(−(GM / (R T_b)) (h − h_b) /
/// ((R_M + h_b)(R_M + h))), R = 192.1 J/(kg K) the gas constant of the curve fit, GM and R_M the gravitational
/// parameter and the volumetric mean radius of Mars of the NSSDC Mars fact sheet (to be verified by S. Guillet). The
/// curve fit is not hydrostatic, so the layer is reached over <see cref="TransitionWidth"/> (5 km), where ln p and T go
/// from one to the other by a quintic smootherstep: the density and its first two derivatives are continuous.
/// Without it the absolute temperature reached 0 K at 112.5 km, the density was infinite there and negative above,
/// and drag pushed a spacecraft along its velocity (#422).
/// </para>
/// <para>
/// Domain: the density is positive and continuous at every altitude, and decreasing except at 7 km, where the two
/// branches of the curve fit do not meet (+0.41 %). Against Seiff's nominal northern
/// summer mid-latitude model it is within 0.96 to 1.11 up to 20 km and within a factor 2.81 from 20 to 100 km. Above
/// about 100 km the layer extrapolates a 139 K mesosphere: the thermosphere, warmer and denser, is not modelled, so
/// drag in orbit above about 120 km is underestimated by orders of magnitude.
/// </para>
/// <para>
/// Atmospheric drag reads the density from the altitude without building a context
/// (<see cref="IGeodeticDensityModel"/>), so a derived class that re-implements <see cref="IAtmosphericModel"/> is not
/// used by drag, as for <see cref="NRLMSISE_00.Nrlmsise00Model"/>. Its density depends on the altitude alone, so the drag
/// partials take its derivative in closed form (<see cref="IAltitudeDensityDerivative"/>).
/// </para>
/// </remarks>
public class MarsStandardAtmosphere : IAtmosphericModel, IGeodeticDensityModel, IAltitudeDensityDerivative
{
    // Temperature of the isothermal layer, K: Seiff (1982), 139 K from 72 to 100 km
    private const double IsothermalLayerTemperature = 139.0;

    /// <summary>
    /// Altitude of the base of the isothermal layer, m: where the temperature of the curve fit, −23.4 − 0.00222 h °C,
    /// reaches <see cref="IsothermalLayerTemperature"/>, about 49 887.387 m.
    /// </summary>
    internal const double IsothermalLayerBase = (Constants.Kelvin - 23.4 - IsothermalLayerTemperature) / 0.00222;

    // GM (m³/s²) and volumetric mean radius (m) of Mars, NSSDC Mars fact sheet (to be verified by S. Guillet)
    private const double MarsGravitationalParameter = 4.2828e13;
    private const double MarsMeanRadius = 3389.5e3;

    // Gas constant of the curve fit, J/(kg K): ρ = p / (0.1921 T_K) with p in kPa
    private const double GasConstant = 192.1;

    // GM / (R T_b), m: the exponent of the hydrostatic pressure of the layer
    private const double HydrostaticLength = MarsGravitationalParameter / (GasConstant * IsothermalLayerTemperature);

    // Radius of the base of the layer, m
    private const double LayerBaseRadius = MarsMeanRadius + IsothermalLayerBase;

    // Pressure of the curve fit at the base of the layer, kPa
    private static readonly double LayerBasePressure = 0.699 * System.Math.Exp(-0.00009 * IsothermalLayerBase);

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
    /// Width of the transition from the curve fit to the layer, m: above <see cref="IsothermalLayerBase"/>, ln p and T go
    /// from the curve fit to the layer over 5 km, so that the density and its first two derivatives are continuous; the
    /// slope of the curve fit, d ln ρ/dh = −7.4e-5 /m, and that of the layer, −1.36e-4 /m, differ by 83 %, which stopped
    /// RK7(8) with the variational equations in the step control on the base (#422).
    /// </summary>
    internal const double TransitionWidth = 5000.0;

    // Weight of the layer in the transition and its derivative in altitude: the quintic smootherstep
    // w = 10x³ − 15x⁴ + 6x⁵, x = (h − h_b) / width, 0 at the base and 1 at its end, with w′ and w″ zero at both ends
    // (K. Perlin, "Improving noise", ACM Transactions on Graphics 21(3) (2002); to be verified by S. Guillet)
    private static (double Weight, double Slope) Blend(double altitude)
    {
        double x = (altitude - IsothermalLayerBase) / TransitionWidth;
        if (x >= 1.0)
        {
            return (1.0, 0.0);
        }

        return (x * x * x * (10.0 + x * (-15.0 + 6.0 * x)), 30.0 * x * x * (1.0 - x) * (1.0 - x) / TransitionWidth);
    }

    private static double CurveFitLogPressure(double altitude) => System.Math.Log(0.699) - 0.00009 * altitude;

    private static double LayerLogPressure(double altitude) =>
        System.Math.Log(LayerBasePressure) - HydrostaticLength * (altitude - IsothermalLayerBase) /
        (LayerBaseRadius * (MarsMeanRadius + altitude));

    // Absolute temperature of the curve fit, K
    private static double CurveFitTemperature(double altitude) => Constants.Kelvin - 23.4 - 0.00222 * altitude;

    /// <summary>
    /// dρ/dh, by differentiation of the closed form of the branch the altitude falls in. Below
    /// <see cref="IsothermalLayerBase"/>: ρ = P / (0.1921 T_K), P ∝ exp(−0.00009 h), so dρ/dh = ρ (−0.00009 − T_K′ / T_K),
    /// with T_K′ = −0.000998 K/m below 7 km and −0.00222 K/m above. In the transition: d ln p/dh = (1 − w) s_fit + w s_layer
    /// + w′ (ln p_layer − ln p_fit) and T_K′ = (1 − w)(−0.00222) + w′ (139 − T_fit), so dρ/dh = ρ (d ln p/dh − T_K′ / T_K).
    /// In the isothermal layer: d ln p/dh = −(GM / (R T_b)) / (R_M + h)², so dρ/dh = −ρ (GM / (R T_b)) / (R_M + h)².
    /// </summary>
    double IAltitudeDensityDerivative.GetDensityAltitudeDerivative(double altitude)
    {
        if (altitude >= IsothermalLayerBase)
        {
            double radius = MarsMeanRadius + altitude;
            double layerSlope = -HydrostaticLength / (radius * radius);
            var (w, dw) = Blend(altitude);
            if (w >= 1.0)
            {
                return Density(altitude) * layerSlope;
            }

            double logPressureSlope = (1.0 - w) * -0.00009 + w * layerSlope +
                                      dw * (LayerLogPressure(altitude) - CurveFitLogPressure(altitude));
            double temperature = (1.0 - w) * CurveFitTemperature(altitude) + w * IsothermalLayerTemperature;
            double temperatureSlope = (1.0 - w) * -0.00222 +
                                      dw * (IsothermalLayerTemperature - CurveFitTemperature(altitude));
            return Density(altitude) * (logPressureSlope - temperatureSlope / temperature);
        }

        double slope = altitude < 7000.0 ? -0.000998 : -0.00222;
        return Density(altitude) * (-0.00009 - slope / (Temperature(altitude) + Constants.Kelvin));
    }

    private static double Temperature(double altitude)
    {
        if (altitude < 7000.0)
        {
            return -31.0 - 0.000998 * altitude;
        }

        if (altitude < IsothermalLayerBase)
        {
            return -23.4 - 0.00222 * altitude;
        }

        var (w, _) = Blend(altitude);
        return w >= 1.0
            ? IsothermalLayerTemperature - Constants.Kelvin
            : (1.0 - w) * CurveFitTemperature(altitude) + w * IsothermalLayerTemperature - Constants.Kelvin;
    }

    private static double Pressure(double altitude)
    {
        if (altitude < IsothermalLayerBase)
        {
            return 0.699 * System.Math.Exp(-0.00009 * altitude);
        }

        var (w, _) = Blend(altitude);
        if (w >= 1.0)
        {
            // 1 / r_b − 1 / r written (h − h_b) / (r_b r), without the cancellation of the difference of two inverses
            return LayerBasePressure * System.Math.Exp(-HydrostaticLength * (altitude - IsothermalLayerBase) /
                                                       (LayerBaseRadius * (MarsMeanRadius + altitude)));
        }

        return System.Math.Exp((1.0 - w) * CurveFitLogPressure(altitude) + w * LayerLogPressure(altitude));
    }

    private static double Density(double altitude)
    {
        return Pressure(altitude) / (0.1921 * (Temperature(altitude) + Constants.Kelvin));
    }
}
