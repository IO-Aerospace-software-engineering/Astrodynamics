// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// U.S. Standard Atmosphere 1976 model for Earth.
/// </summary>
/// <remarks>
/// <para>
/// U.S. Standard Atmosphere, 1976, NOAA, NASA and USAF (1976), NASA-TM-X-74335, from −5 km to 1000 km of geometric
/// altitude. Uses only altitude - ignores time, position, and space weather. For time-varying conditions, and for
/// orbits, use Nrlmsise00Model.
/// </para>
/// <para>
/// Below 86 km, its defining equations: seven layers of linear molecular-scale temperature in geopotential altitude
/// H = r₀ Z / (r₀ + Z) (its table 4), the hydrostatic pressure of eq. (33a) and (33b) from 101 325 Pa at sea level,
/// ρ = p M₀ / (R* T_M), eq. (42). From 86 to 1000 km, the number densities of its section 1.3
/// (<see cref="Thermosphere1976"/>). Above 1000 km, where the standard stops, ln ρ and ln p are continued with their
/// slopes at 1000 km (a scale height of 237 km). The temperature is the kinetic temperature: T_M times M/M₀ of its
/// table 8 between 80 and 86 km.
/// </para>
/// <para>
/// The density of the standard is continuous, but its slope jumps at the base of each layer (+23.5 % at 11 km of
/// geopotential altitude), and RK7(8) with the variational equations in the step control stops there. Over
/// ±<see cref="TransitionHalfWidth"/> (100 m) of geometric altitude around each base, ln p and T_M go from one layer to
/// the next by a quintic smootherstep, so that the density and its first two derivatives are continuous, within 2.1e-4
/// of the equations; from 86 to 86.2 km the equations below 86 km go to the thermosphere the same way (they differ by
/// 8.1e-6 at 86 km). Against table I of the standard the density is within 4.2e-4, the rounding of the table, from
/// −1.05 to 1000 km (#437). Before #437 the model was the curve fit of the NASA Glenn Research Center, "Earth
/// Atmosphere Model - Metric Units", whose upper-stratosphere line extended to every altitude gave 38.7 times the
/// density of the standard at 200 km and 80.1 times at 140 km.
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

    /// <summary>
    /// dρ/dh = ρ d ln ρ/dh in closed form: below 86 km, d ln ρ/dZ = −(g₀ M₀ / R* + L) / T_M (r₀ / (r₀ + Z))² in a layer of
    /// gradient L, and in a transition d ln p/dZ and T_M′ blended as ln p and T_M, plus w′ times the difference of the
    /// two layers; above 86 km, the derivative of the spline of ln ρ (<see cref="Thermosphere1976"/>).
    /// </summary>
    double IAltitudeDensityDerivative.GetDensityAltitudeDerivative(double altitude)
    {
        var (logDensity, slope) = LogDensity(altitude);
        return System.Math.Exp(logDensity) * slope;
    }

    // Defining constants of table 2 and the layers of table 4: g₀ (m/s²), R* (J/(kmol K)), M₀ (kg/kmol), r₀ (m), the bases
    // H_b (m') and the gradients L_M,b (K/m') of the molecular-scale temperature
    private const double G0 = 9.80665;
    private const double GasConstant = 8.31432e3;
    private const double SeaLevelMolecularWeight = 28.9644;
    private const double EarthRadius = 6356766.0;
    private const double HydrostaticConstant = G0 * SeaLevelMolecularWeight / GasConstant;
    private static readonly double[] LayerBase = { 0.0, 11000.0, 20000.0, 32000.0, 47000.0, 51000.0, 71000.0 };
    private static readonly double[] LapseRate = { -6.5e-3, 0.0, 1.0e-3, 2.8e-3, 0.0, -2.8e-3, -2.0e-3 };
    private static readonly double[] BaseTemperature = new double[7];
    private static readonly double[] BaseLogPressure = new double[7];
    private static readonly double[] GeometricLayerBase = new double[7];

    // M/M₀ of table 8 from 80 to 86 km of geometric altitude, every 500 m
    private static readonly double[] MolecularWeightRatio =
    {
        1.000000, 0.999996, 0.999989, 0.999971, 0.999941, 0.999909, 0.999870, 0.999829, 0.999786, 0.999741, 0.999694,
        0.999641, 0.999579
    };

    /// <summary>
    /// Half-width of the transitions at the bases of the layers, m of geometric altitude.
    /// </summary>
    internal const double TransitionHalfWidth = 100.0;

    /// <summary>
    /// Width of the junction above 86 km from the equations of the layers to the thermosphere, m.
    /// </summary>
    internal const double JunctionWidth = 200.0;

    static EarthStandardAtmosphere()
    {
        BaseTemperature[0] = 288.15;
        BaseLogPressure[0] = System.Math.Log(101325.0);
        for (int i = 0; i < 6; i++)
        {
            (BaseTemperature[i + 1], BaseLogPressure[i + 1]) = Layer(i, LayerBase[i + 1]);
        }

        for (int i = 0; i < 7; i++)
        {
            GeometricLayerBase[i] = EarthRadius * LayerBase[i] / (EarthRadius - LayerBase[i]);
        }
    }

    /// <summary>
    /// The geometric altitudes of the bases of the layers above the first, m: 11.019, 20.063, 32.162, 47.350, 51.413 and
    /// 71.802 km.
    /// </summary>
    internal static System.ReadOnlySpan<double> LayerBases => System.MemoryExtensions.AsSpan(GeometricLayerBase, 1);

    // T_M (K) and ln p (Pa) of layer i at the geopotential altitude h, eq. (23), (33a) and (33b)
    private static (double Temperature, double LogPressure) Layer(int i, double h)
    {
        double temperature = BaseTemperature[i] + LapseRate[i] * (h - LayerBase[i]);
        double logPressure = LapseRate[i] == 0.0
            ? BaseLogPressure[i] - HydrostaticConstant * (h - LayerBase[i]) / BaseTemperature[i]
            : BaseLogPressure[i] + HydrostaticConstant / LapseRate[i] * System.Math.Log(BaseTemperature[i] / temperature);
        return (temperature, logPressure);
    }

    // Layer i at the geometric altitude z: T_M, ln p (Pa) and their derivatives in z
    private static (double T, double LogP, double DT, double DLogP) Branch(int i, double z)
    {
        double ratio = EarthRadius / (EarthRadius + z);
        var (t, logP) = Layer(i, EarthRadius * z / (EarthRadius + z));
        return (t, logP, LapseRate[i] * ratio * ratio, -HydrostaticConstant / t * ratio * ratio);
    }

    // The layers below 86 km with their transitions, at the geometric altitude z
    private static (double T, double LogP, double DT, double DLogP) Lower(double z)
    {
        for (int i = 1; i < 7; i++)
        {
            if (z < GeometricLayerBase[i] - TransitionHalfWidth)
            {
                return Branch(i - 1, z);
            }

            if (z < GeometricLayerBase[i] + TransitionHalfWidth)
            {
                var (w, dw) = Smootherstep.At(z, GeometricLayerBase[i] - TransitionHalfWidth, 2.0 * TransitionHalfWidth);
                var below = Branch(i - 1, z);
                var above = Branch(i, z);
                return ((1.0 - w) * below.T + w * above.T, (1.0 - w) * below.LogP + w * above.LogP,
                    (1.0 - w) * below.DT + w * above.DT + dw * (above.T - below.T),
                    (1.0 - w) * below.DLogP + w * above.DLogP + dw * (above.LogP - below.LogP));
            }
        }

        return Branch(6, z);
    }

    // ln ρ (kg/m³) and its derivative in altitude
    private static (double Value, double Slope) LogDensity(double altitude)
    {
        if (altitude >= Thermosphere1976.Base + JunctionWidth)
        {
            return Thermosphere1976.Density(altitude);
        }

        var lower = Lower(altitude);
        double value = lower.LogP + System.Math.Log(SeaLevelMolecularWeight / (GasConstant * lower.T));
        double slope = lower.DLogP - lower.DT / lower.T;
        if (altitude < Thermosphere1976.Base)
        {
            return (value, slope);
        }

        var upper = Thermosphere1976.Density(altitude);
        var (w, dw) = Smootherstep.At(altitude, Thermosphere1976.Base, JunctionWidth);
        return ((1.0 - w) * value + w * upper.Value, (1.0 - w) * slope + w * upper.Slope + dw * (upper.Value - value));
    }

    private static double Density(double altitude)
    {
        return System.Math.Exp(LogDensity(altitude).Value);
    }

    // kPa
    private static double Pressure(double altitude)
    {
        if (altitude >= Thermosphere1976.Base + JunctionWidth)
        {
            return System.Math.Exp(Thermosphere1976.Pressure(altitude).Value);
        }

        double lower = Lower(altitude).LogP - System.Math.Log(1000.0);
        if (altitude < Thermosphere1976.Base)
        {
            return System.Math.Exp(lower);
        }

        var (w, _) = Smootherstep.At(altitude, Thermosphere1976.Base, JunctionWidth);
        return System.Math.Exp((1.0 - w) * lower + w * Thermosphere1976.Pressure(altitude).Value);
    }

    // °C: the kinetic temperature, T_M (M/M₀) below 86 km
    private static double Temperature(double altitude)
    {
        if (altitude >= Thermosphere1976.Base + JunctionWidth)
        {
            return Thermosphere1976.KineticTemperature(altitude) - Constants.Kelvin;
        }

        double ratio = 1.0;
        if (altitude >= 80000.0)
        {
            double position = System.Math.Min((altitude - 80000.0) / 500.0, MolecularWeightRatio.Length - 1.0);
            int k = System.Math.Min((int)position, MolecularWeightRatio.Length - 2);
            ratio = MolecularWeightRatio[k] + (position - k) * (MolecularWeightRatio[k + 1] - MolecularWeightRatio[k]);
        }

        double lower = Lower(altitude).T * ratio;
        if (altitude < Thermosphere1976.Base)
        {
            return lower - Constants.Kelvin;
        }

        var (w, _) = Smootherstep.At(altitude, Thermosphere1976.Base, JunctionWidth);
        return (1.0 - w) * lower + w * Thermosphere1976.KineticTemperature(altitude) - Constants.Kelvin;
    }
}
