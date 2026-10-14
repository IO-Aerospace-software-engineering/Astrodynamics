// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.Maneuver;

/// <summary>
/// The execution error of an impulsive maneuver, by the model of Gates: four independent zero-mean Gaussian errors,
/// two along the commanded ΔV and two perpendicular to it, one of each fixed and one proportional to |ΔV|.
/// </summary>
/// <remarks>
/// <para>
/// C. R. Gates, <i>A Simplified Model of Midcourse Maneuver Execution Errors</i>, JPL Technical Report 32-504,
/// 15 October 1963, sections II and III. With v the commanded ΔV and V = |v|:
/// </para>
/// <list type="bullet">
/// <item><description>shutoff error e_s = s v, s of standard deviation σ_s (proportional magnitude error,
/// dimensionless): L_s = σ_s² L_v;</description></item>
/// <item><description>resolution error e_r = r v / V, r of standard deviation σ_r (fixed magnitude error, m/s):
/// L_r = σ_r² G;</description></item>
/// <item><description>pointing error e_p = u × v, u spherical Gaussian of standard deviation σ_p on each axis
/// (proportional pointing error, rad): L_p = σ_p² (V² I − L_v);</description></item>
/// <item><description>autopilot error e_a = w × v / V, w of standard deviation σ_a on each axis (fixed pointing error,
/// m/s): L_a = σ_a² (I − G);</description></item>
/// </list>
/// <para>
/// so L_e = (σ_s² − σ_p²) L_v + (σ_r² − σ_a²) G + (σ_p² V² + σ_a²) I, the equation that ends section III. The report
/// writes E(u_i²) = σ_p and E(w_i²) = σ_a, and its results L_p and L_a are in σ_p² and σ_a²: σ_p and σ_a are the
/// standard deviations, as here. The commanded ΔV of an open-loop maneuver is known, so L_v = v vᵀ, G = v vᵀ / V² and
/// V̄² = V².
/// </para>
/// <para>
/// A ΔV of exactly zero is not executed: the engine burns no fuel and the thrust lasts no time, and the model, through
/// G, is not defined there. Such a maneuver adds no error (decision of S. Guillet, 2026-10-14). Any non-zero ΔV takes
/// the whole model, the fixed terms included.
/// </para>
/// </remarks>
internal sealed class ManeuverExecutionError
{
    /// <summary>
    /// Create the model.
    /// </summary>
    /// <param name="resolutionSigma">σ_r, fixed magnitude error, in m/s.</param>
    /// <param name="shutoffSigma">σ_s, magnitude error proportional to |ΔV|, dimensionless.</param>
    /// <param name="autopilotSigma">σ_a, fixed pointing error, in m/s on each axis perpendicular to ΔV.</param>
    /// <param name="pointingSigma">σ_p, pointing error proportional to |ΔV|, in rad on each axis.</param>
    /// <exception cref="ArgumentOutOfRangeException">A standard deviation is negative or not finite.</exception>
    internal ManeuverExecutionError(double resolutionSigma, double shutoffSigma, double autopilotSigma,
        double pointingSigma)
    {
        ResolutionSigma = Validate(resolutionSigma, nameof(resolutionSigma));
        ShutoffSigma = Validate(shutoffSigma, nameof(shutoffSigma));
        AutopilotSigma = Validate(autopilotSigma, nameof(autopilotSigma));
        PointingSigma = Validate(pointingSigma, nameof(pointingSigma));
    }

    /// <summary>σ_r, fixed magnitude error, in m/s.</summary>
    internal double ResolutionSigma { get; }

    /// <summary>σ_s, magnitude error proportional to |ΔV|, dimensionless.</summary>
    internal double ShutoffSigma { get; }

    /// <summary>σ_a, fixed pointing error, in m/s on each axis perpendicular to ΔV.</summary>
    internal double AutopilotSigma { get; }

    /// <summary>σ_p, pointing error proportional to |ΔV|, in rad on each axis perpendicular to ΔV.</summary>
    internal double PointingSigma { get; }

    /// <summary>
    /// The covariance of the error of <paramref name="deltaV"/>, L_e of Gates, in the frame of
    /// <paramref name="deltaV"/>.
    /// </summary>
    /// <param name="deltaV">The commanded ΔV, in m/s.</param>
    /// <param name="covariance">L_e, 3×3 row-major, in m²/s²; zero for a zero ΔV.</param>
    /// <exception cref="ArgumentException"><paramref name="covariance"/> does not hold 9 values.</exception>
    internal void Covariance(in Vector3 deltaV, Span<double> covariance)
    {
        if (covariance.Length != 9)
        {
            throw new ArgumentException($"The covariance holds 9 values; got {covariance.Length}.",
                nameof(covariance));
        }

        covariance.Clear();

        // The unit vector u = v / V, by the largest component first, so that a tiny ΔV neither underflows V² nor
        // overflows (σ_r² − σ_a²) / V²: L_v = V² u uᵀ and G = u uᵀ
        double largest = System.Math.Max(System.Math.Abs(deltaV.X),
            System.Math.Max(System.Math.Abs(deltaV.Y), System.Math.Abs(deltaV.Z)));
        if (largest == 0.0)
        {
            return;
        }

        Span<double> u = stackalloc double[] { deltaV.X / largest, deltaV.Y / largest, deltaV.Z / largest };
        double norm = System.Math.Sqrt(u[0] * u[0] + u[1] * u[1] + u[2] * u[2]);
        for (int i = 0; i < 3; i++)
        {
            u[i] /= norm;
        }

        double magnitude = largest * norm;
        double v2 = magnitude * magnitude;
        double sr2 = ResolutionSigma * ResolutionSigma;
        double ss2 = ShutoffSigma * ShutoffSigma;
        double sa2 = AutopilotSigma * AutopilotSigma;
        double sp2 = PointingSigma * PointingSigma;

        // L_e = ((σ_s² − σ_p²) V² + σ_r² − σ_a²) u uᵀ + (σ_p² V² + σ_a²) I
        double outer = (ss2 - sp2) * v2 + sr2 - sa2;
        double diagonal = sp2 * v2 + sa2;
        for (int i = 0; i < 3; i++)
        {
            for (int j = i; j < 3; j++)
            {
                double value = outer * u[i] * u[j] + (i == j ? diagonal : 0.0);
                covariance[3 * i + j] = value;
                covariance[3 * j + i] = value;
            }
        }
    }

    private static double Validate(double sigma, string name)
    {
        if (!(sigma >= 0.0) || double.IsPositiveInfinity(sigma))
        {
            throw new ArgumentOutOfRangeException(name, sigma, "A standard deviation must be finite and non-negative.");
        }

        return sigma;
    }
}
