// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// The U.S. Standard Atmosphere 1976 from 86 to 1000 km of geometric altitude, from the computational equations of its
/// part 1, section 1.3 (U.S. Standard Atmosphere, 1976, NOAA, NASA and USAF (1976), NASA-TM-X-74335) (#437).
/// </summary>
/// <remarks>
/// <para>
/// The number densities of N₂, O, O₂, Ar and He from their values at 86 km (its table 9) by the diffusion equation,
/// eq. (35) to (38), with the eddy-diffusion coefficient of eq. (7), the molecular-diffusion coefficients of eq. (8) and
/// the flux terms of eq. (37) (its tables 6 and 7); that of H from 150 km by eq. (39) and (40), referenced at 500 km.
/// The kinetic temperature in closed form, eq. (25) to (32): isothermal at 186.8673 K to 91 km, an ellipse to 110 km,
/// +12 K/km to 120 km and T∞ − (T∞ − T₁₀) exp(−λξ) above. ρ = Σ nᵢ Mᵢ / N_A, p = Σ nᵢ k T, eq. (33c) and (42).
/// </para>
/// <para>
/// The integrals are evaluated once, when the type loads, on a grid of <see cref="Step"/> by Simpson's rule (converged
/// to 2.4e-8 of the density against a grid of half the step), and ln ρ and ln p are interpolated per call by a cubic
/// spline clamped to their slopes from the equations at 86 and 1000 km, without allocation. Two adjustments keep the
/// density C² so that RK7(8) with the variational equations in the step control does not stop: the mean molecular
/// weight of eq. (38), which switches from M₀ to that of the background gas at 100 km, goes from one to the other by a
/// smootherstep over ±100 m, symmetric about 100 km so that the densities above are unchanged to second order; and
/// atomic hydrogen, defined from 150 km, enters by a smootherstep over 200 m above it (its mass fraction is 3.0e-7
/// there). Against table I of the standard the density is within 4.2e-4, the rounding of the table, from 86 to 1000 km.
/// </para>
/// </remarks>
internal static class Thermosphere1976
{
    /// <summary>
    /// Base of the thermosphere model, m: Z₇ = 86 km.
    /// </summary>
    internal const double Base = 86000.0;

    /// <summary>
    /// Top of the tables of the standard, m: Z₁₂ = 1000 km. Above it, ln ρ and ln p are continued with their slopes.
    /// </summary>
    internal const double Top = 1000000.0;

    /// <summary>
    /// Step of the grid of the integrals, m.
    /// </summary>
    internal const double Step = 100.0;

    // Constants of table 2 (k, N_A, R*, g₀, r₀) and molecular weights (kg/kmol) of table 3; O and H are not in table 3:
    // the atomic weights of the carbon-12 scale the standard adopts (to be verified by S. Guillet)
    private const double Boltzmann = 1.380622e-23;
    private const double Avogadro = 6.022169e26;
    private const double GasConstant = 8.31432e3;
    private const double G0 = 9.80665;
    private const double EarthRadiusKm = 6356.766;
    private const double SeaLevelMolecularWeight = 28.9644;

    private const int N2 = 0, O = 1, O2 = 2, Ar = 3, He = 4, H = 5;
    private static readonly double[] MolecularWeight = { 28.0134, 15.9994, 31.9988, 39.948, 4.0026, 1.00797 };

    // Number densities at 86 km, m⁻³ (table 9)
    private static readonly double[] BaseNumberDensity = { 1.129794e20, 8.6e16, 3.030898e19, 1.351400e18, 7.5817e14 };

    // Thermal-diffusion coefficients α, and a (m⁻¹ s⁻¹), b of the molecular-diffusion coefficient (table 6)
    private static readonly double[] Alpha = { 0.0, 0.0, 0.0, 0.0, -0.40, -0.25 };
    private static readonly double[] DiffusionA = { 0.0, 6.986e20, 4.863e20, 4.487e20, 1.700e21, 3.305e21 };
    private static readonly double[] DiffusionB = { 0.0, 0.750, 0.750, 0.870, 0.691, 0.500 };

    // Flux coefficients of eq. (37), table 7: Q (km⁻³), U (km), W (km⁻³); q, u, w for O below 97 km
    private static readonly double[] FluxQ = { 0.0, -5.809644e-4, 1.366212e-4, 9.434079e-5, -2.457369e-4 };
    private static readonly double[] FluxU = { 0.0, 56.90311, 86.0, 86.0, 86.0 };
    private static readonly double[] FluxW = { 0.0, 2.706240e-5, 8.333333e-5, 8.333333e-5, 6.666667e-4 };
    private const double OxygenQ = -3.416248e-3, OxygenU = 97.0, OxygenW = 5.008765e-4;

    // Temperature profile (table 5 and eq. (25) to (32)), km and K
    private const double T7 = 186.8673;
    private const double EllipseTc = 263.1905, EllipseA = -76.3232, EllipseSemiAxis = -19.9429;
    private const double T10 = 360.0, TInfinity = 1000.0, Lambda = 0.01875;

    // Atomic hydrogen (table 2): number density at Z₁₁ = 500 km, m⁻³, and vertical flux φ, m⁻² s⁻¹
    private const double HydrogenReferenceKm = 500.0;
    private const double HydrogenReferenceDensity = 8.0e10;
    private const double HydrogenFlux = 7.2e11;
    private const double HydrogenBaseKm = 150.0;

    // Half-width of the switch of the mean molecular weight at 100 km and width of the entry of H at 150 km, km
    private const double MolecularWeightSwitchKm = 100.0;
    private const double SmoothingKm = 0.1;

    private static readonly double[] LogDensity;
    private static readonly double[] LogDensityCurvature;
    private static readonly double[] LogPressure;
    private static readonly double[] LogPressureCurvature;
    private static readonly double LogDensityTopSlope, LogPressureTopSlope;

    static Thermosphere1976()
    {
        int count = (int)System.Math.Round((Top - Base) / Step) + 1;
        double stepKm = Step / 1000.0;
        var z = new double[count];
        for (int k = 0; k < count; k++)
        {
            z[k] = Base / 1000.0 + k * stepKm;
        }

        var n = new double[6][];
        var logSlope = new double[6][];

        // N₂, eq. (38): M₀ to 100 km, M(N₂) above
        n[N2] = new double[count];
        logSlope[N2] = new double[count];
        var integrand = new double[count];
        for (int k = 0; k < count; k++)
        {
            var (t, _) = Temperature(z[k]);
            integrand[k] = MeanMolecularWeight(z[k], MolecularWeight[N2]) * Gravity(z[k]) / (GasConstant * t) * 1000.0;
        }

        Fill(N2, z, integrand, n, logSlope);

        // O and O₂ diffusing through N₂; Ar and He through N₂ + O + O₂ (section 1.3.2)
        var background = new double[count];
        var backgroundWeight = new double[count];
        for (int k = 0; k < count; k++)
        {
            background[k] = n[N2][k];
            backgroundWeight[k] = MolecularWeight[N2];
        }

        Diffuse(O, z, background, backgroundWeight, n, logSlope, integrand);
        Diffuse(O2, z, background, backgroundWeight, n, logSlope, integrand);
        for (int k = 0; k < count; k++)
        {
            background[k] = n[N2][k] + n[O][k] + n[O2][k];
            backgroundWeight[k] = (n[N2][k] * MolecularWeight[N2] + n[O][k] * MolecularWeight[O] +
                                   n[O2][k] * MolecularWeight[O2]) / background[k];
        }

        Diffuse(Ar, z, background, backgroundWeight, n, logSlope, integrand);
        Diffuse(He, z, background, backgroundWeight, n, logSlope, integrand);
        Hydrogen(z, n, logSlope);

        // ρ and p on the grid, and their logarithmic slopes at both ends from the equations
        LogDensity = new double[count];
        LogPressure = new double[count];
        double densitySlopeBase = 0.0, pressureSlopeBase = 0.0, densitySlopeTop = 0.0, pressureSlopeTop = 0.0;
        for (int k = 0; k < count; k++)
        {
            double mass = 0.0, number = 0.0, massSlope = 0.0, numberSlope = 0.0;
            for (int i = 0; i < 6; i++)
            {
                mass += n[i][k] * MolecularWeight[i];
                number += n[i][k];
                massSlope += n[i][k] * MolecularWeight[i] * logSlope[i][k];
                numberSlope += n[i][k] * logSlope[i][k];
            }

            var (t, dt) = Temperature(z[k]);
            LogDensity[k] = System.Math.Log(mass / Avogadro);
            LogPressure[k] = System.Math.Log(number * Boltzmann * t / 1000.0);
            if (k == 0)
            {
                densitySlopeBase = massSlope / mass;
                pressureSlopeBase = numberSlope / number + dt / t;
            }
            else if (k == count - 1)
            {
                densitySlopeTop = massSlope / mass;
                pressureSlopeTop = numberSlope / number + dt / t;
            }
        }

        // Slopes per m
        LogDensityCurvature = ClampedSpline(LogDensity, densitySlopeBase / 1000.0, densitySlopeTop / 1000.0);
        LogPressureCurvature = ClampedSpline(LogPressure, pressureSlopeBase / 1000.0, pressureSlopeTop / 1000.0);
        LogDensityTopSlope = densitySlopeTop / 1000.0;
        LogPressureTopSlope = pressureSlopeTop / 1000.0;
    }

    /// <summary>
    /// ln ρ (ρ in kg/m³) and its derivative in altitude (/m), from 86 km up; above 1000 km, continued with its slope.
    /// </summary>
    internal static (double Value, double Slope) Density(double altitude) =>
        Evaluate(LogDensity, LogDensityCurvature, LogDensityTopSlope, altitude);

    /// <summary>
    /// ln p (p in kPa) and its derivative in altitude (/m), from 86 km up; above 1000 km, continued with its slope.
    /// </summary>
    internal static (double Value, double Slope) Pressure(double altitude) =>
        Evaluate(LogPressure, LogPressureCurvature, LogPressureTopSlope, altitude);

    /// <summary>
    /// Kinetic temperature, K, eq. (25) to (32); held at its value at 1000 km above.
    /// </summary>
    internal static double KineticTemperature(double altitude) =>
        Temperature(System.Math.Min(altitude, Top) / 1000.0).Value;

    private static (double Value, double Slope) Evaluate(double[] y, double[] curvature, double topSlope,
        double altitude)
    {
        int last = y.Length - 1;
        if (altitude >= Top)
        {
            return (y[last] + topSlope * (altitude - Top), topSlope);
        }

        double position = (altitude - Base) / Step;
        int k = System.Math.Clamp((int)position, 0, last - 1);
        double b = position - k;
        double a = 1.0 - b;
        double h2 = Step * Step / 6.0;
        double value = a * y[k] + b * y[k + 1] + ((a * a * a - a) * curvature[k] + (b * b * b - b) * curvature[k + 1]) * h2;
        double slope = (y[k + 1] - y[k]) / Step +
                       Step / 6.0 * (-(3.0 * a * a - 1.0) * curvature[k] + (3.0 * b * b - 1.0) * curvature[k + 1]);
        return (value, slope);
    }

    // Second derivatives of the cubic spline through y on the grid, clamped to the given slopes at both ends
    // (W. H. Press et al., Numerical Recipes, 3rd edition, Cambridge University Press (2007), section 3.3)
    private static double[] ClampedSpline(double[] y, double slopeBase, double slopeTop)
    {
        int count = y.Length;
        var second = new double[count];
        var u = new double[count];
        second[0] = -0.5;
        u[0] = 3.0 / Step * ((y[1] - y[0]) / Step - slopeBase);
        for (int k = 1; k < count - 1; k++)
        {
            double p = 0.5 * second[k - 1] + 2.0;
            second[k] = -0.5 / p;
            u[k] = ((y[k + 1] - 2.0 * y[k] + y[k - 1]) / Step * 6.0 / (2.0 * Step) - 0.5 * u[k - 1]) / p;
        }

        double qn = 0.5;
        double un = 3.0 / Step * (slopeTop - (y[count - 1] - y[count - 2]) / Step);
        second[count - 1] = (un - qn * u[count - 2]) / (qn * second[count - 2] + 1.0);
        for (int k = count - 2; k >= 0; k--)
        {
            second[k] = second[k] * second[k + 1] + u[k];
        }

        return second;
    }

    // Kinetic temperature (K) and its derivative (K/km) at z km, eq. (25) to (32)
    private static (double Value, double Slope) Temperature(double z)
    {
        if (z < 91.0)
        {
            return (T7, 0.0);
        }

        if (z < 110.0)
        {
            double x = (z - 91.0) / EllipseSemiAxis;
            double root = System.Math.Sqrt(1.0 - x * x);
            return (EllipseTc + EllipseA * root, -EllipseA / EllipseSemiAxis * x / root);
        }

        if (z < 120.0)
        {
            return (240.0 + 12.0 * (z - 110.0), 12.0);
        }

        double ratio = (EarthRadiusKm + 120.0) / (EarthRadiusKm + z);
        double e = System.Math.Exp(-Lambda * (z - 120.0) * ratio);
        return (TInfinity - (TInfinity - T10) * e, Lambda * (TInfinity - T10) * ratio * ratio * e);
    }

    // Acceleration of gravity at z km, m/s², eq. (17)
    private static double Gravity(double z)
    {
        double ratio = EarthRadiusKm / (EarthRadiusKm + z);
        return G0 * ratio * ratio;
    }

    // Eddy-diffusion coefficient, m²/s, eq. (7)
    private static double EddyDiffusion(double z)
    {
        if (z < 95.0)
        {
            return 1.2e2;
        }

        if (z < 115.0)
        {
            return 1.2e2 * System.Math.Exp(1.0 - 400.0 / (400.0 - (z - 95.0) * (z - 95.0)));
        }

        return 0.0;
    }

    // The mean molecular weight of eq. (35) and (38): M₀ to 100 km, that of the background gas above, switched by a
    // smootherstep over ±100 m
    private static double MeanMolecularWeight(double z, double background)
    {
        var (w, _) = Smootherstep.At(z, MolecularWeightSwitchKm - SmoothingKm, 2.0 * SmoothingKm);
        return z < MolecularWeightSwitchKm - SmoothingKm
            ? SeaLevelMolecularWeight
            : (1.0 - w) * SeaLevelMolecularWeight + w * background;
    }

    // n_i from the integrand f + v/(D + K) (1/km) on the grid, eq. (35): n = n₇ (T₇ / T) exp(−∫ ...)
    private static void Fill(int species, double[] z, double[] integrand, double[][] n, double[][] logSlope)
    {
        int count = z.Length;
        n[species] = new double[count];
        logSlope[species] = new double[count];
        var integral = Cumulative(integrand, Step / 1000.0);
        for (int k = 0; k < count; k++)
        {
            var (t, dt) = Temperature(z[k]);
            n[species][k] = BaseNumberDensity[species] * T7 / t * System.Math.Exp(-integral[k]);
            logSlope[species][k] = -dt / t - integrand[k];
        }
    }

    // O, O₂, Ar, He: eq. (35) to (37) through the background gas of number density nb and molecular weight Mb
    private static void Diffuse(int species, double[] z, double[] background, double[] backgroundWeight,
        double[][] n, double[][] logSlope, double[] integrand)
    {
        for (int k = 0; k < z.Length; k++)
        {
            var (t, dt) = Temperature(z[k]);
            double g = Gravity(z[k]);
            double eddy = EddyDiffusion(z[k]);
            double diffusion = DiffusionA[species] / background[k] * System.Math.Pow(t / 273.15, DiffusionB[species]);
            double weight = MeanMolecularWeight(z[k], backgroundWeight[k]);
            double f = g / (GasConstant * t) * diffusion / (diffusion + eddy) *
                       (MolecularWeight[species] + weight * eddy / diffusion +
                        Alpha[species] * GasConstant * dt / 1000.0 / g) * 1000.0;
            double u = z[k] - FluxU[species];
            double flux = FluxQ[species] * u * u * System.Math.Exp(-FluxW[species] * u * u * u);
            if (species == O && z[k] <= OxygenU)
            {
                double v = OxygenU - z[k];
                flux += OxygenQ * v * v * System.Math.Exp(-OxygenW * v * v * v);
            }

            integrand[k] = f + flux;
        }

        Fill(species, z, integrand, n, logSlope);
    }

    // H, eq. (39) and (40), from 150 km, entering by a smootherstep over 200 m above it
    private static void Hydrogen(double[] z, double[][] n, double[][] logSlope)
    {
        int count = z.Length;
        double stepKm = Step / 1000.0;
        int reference = (int)System.Math.Round((HydrogenReferenceKm - Base / 1000.0) / stepKm);
        double referenceTemperature = Temperature(HydrogenReferenceKm).Value;
        double exponent = 1.0 + Alpha[H];
        var tauIntegrand = new double[count];
        for (int k = 0; k < count; k++)
        {
            tauIntegrand[k] = Gravity(z[k]) * MolecularWeight[H] / (GasConstant * Temperature(z[k]).Value) * 1000.0;
        }

        var tau = Cumulative(tauIntegrand, stepKm);
        double tauReference = tau[reference];
        var fluxIntegrand = new double[count];
        for (int k = 0; k < count; k++)
        {
            tau[k] -= tauReference;
            double t = Temperature(z[k]).Value;
            double others = n[N2][k] + n[O][k] + n[O2][k] + n[Ar][k] + n[He][k];
            double diffusion = DiffusionA[H] / others * System.Math.Pow(t / 273.15, DiffusionB[H]);
            fluxIntegrand[k] = HydrogenFlux / diffusion * System.Math.Pow(t / referenceTemperature, exponent) *
                               System.Math.Exp(tau[k]) * 1000.0;
        }

        var flux = Cumulative(fluxIntegrand, stepKm);
        double fluxReference = flux[reference];
        n[H] = new double[count];
        logSlope[H] = new double[count];
        for (int k = 0; k < count; k++)
        {
            if (z[k] < HydrogenBaseKm)
            {
                continue;
            }

            var (t, dt) = Temperature(z[k]);
            double bracket = HydrogenReferenceDensity - (flux[k] - fluxReference);
            var (w, dw) = Smootherstep.At(z[k], HydrogenBaseKm, 2.0 * SmoothingKm);
            double density = bracket * System.Math.Pow(referenceTemperature / t, exponent) * System.Math.Exp(-tau[k]);
            n[H][k] = w * density;
            logSlope[H][k] = -fluxIntegrand[k] / bracket - exponent * dt / t - tauIntegrand[k] + (w > 0.0 ? dw / w : 0.0);
        }
    }

    // Cumulative integral on the grid from its first node: Simpson's rule per pair of steps, and (5 f₀ + 8 f₁ − f₂) h / 12
    // for the odd node; the grid has an odd number of nodes, 9141
    private static double[] Cumulative(double[] f, double h)
    {
        int count = f.Length;
        var integral = new double[count];
        for (int k = 0; k + 2 < count; k += 2)
        {
            integral[k + 1] = integral[k] + h * (5.0 * f[k] + 8.0 * f[k + 1] - f[k + 2]) / 12.0;
            integral[k + 2] = integral[k] + h * (f[k] + 4.0 * f[k + 1] + f[k + 2]) / 3.0;
        }

        return integral;
    }
}
