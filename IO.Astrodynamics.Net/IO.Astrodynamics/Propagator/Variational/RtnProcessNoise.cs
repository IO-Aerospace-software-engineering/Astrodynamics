// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Propagator.Variational;

/// <summary>
/// State noise compensation (SNC): a white acceleration noise of spectral densities q_R, q_T, q_N along the radial,
/// transverse and normal axes of the state, Qc = R_rtnᵀ diag(q_R, q_T, q_N) R_rtn.
/// </summary>
/// <remarks>
/// <para>
/// The unmodelled accelerations are a white noise whose spectral density is given in RTN (B. D. Tapley, B. E. Schutz,
/// G. H. Born, <i>Statistical Orbit Determination</i>, Elsevier, 2004, section 4.9, confirmed by S. Guillet, 2026-10-14).
/// The rows of R_rtn are the R, T and N unit vectors of the state, R = r̂, N = (r × v) / |r × v|, T = N × R, as in
/// <see cref="StateVector.CreateRtnRotation"/>; Qc is written as q_R R Rᵀ + q_T T Tᵀ + q_N N Nᵀ, the same matrix,
/// without allocation. It is evaluated at each stage state of the integration, relative to the central body of the
/// propagation, so the axes follow the orbit. When the central body is the Sun (initial orbital parameters relative to
/// a star or a barycenter), the axes are heliocentric, even for a spacecraft near a planet.
/// </para>
/// <para>
/// Thread-safe: the model holds its three densities only. A record: two models with the same densities are equal.
/// </para>
/// </remarks>
public sealed record RtnProcessNoise : IProcessNoiseModel
{
    /// <summary>
    /// Create the noise from its spectral densities.
    /// </summary>
    /// <param name="radial">q_R, in m²/s³.</param>
    /// <param name="transverse">q_T, in m²/s³.</param>
    /// <param name="normal">q_N, in m²/s³.</param>
    /// <exception cref="ArgumentOutOfRangeException">A density is negative or not finite.</exception>
    public RtnProcessNoise(double radial, double transverse, double normal)
    {
        Radial = Validate(radial, nameof(radial));
        Transverse = Validate(transverse, nameof(transverse));
        Normal = Validate(normal, nameof(normal));
    }

    /// <summary>q_R, the radial spectral density, in m²/s³.</summary>
    public double Radial { get; }

    /// <summary>q_T, the transverse spectral density, in m²/s³.</summary>
    public double Transverse { get; }

    /// <summary>q_N, the normal spectral density, in m²/s³.</summary>
    public double Normal { get; }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The state is rectilinear (r × v = 0): its RTN axes are not defined.
    /// </exception>
    void IProcessNoiseModel.Accumulate(StateVector state, Span<double> qc)
    {
        var position = state.Position;
        var momentum = position.Cross(state.Velocity);
        double r = position.Magnitude();
        double h = momentum.Magnitude();
        if (!(h > 0.0) || !(r > 0.0))
        {
            throw new InvalidOperationException(
                "The RTN process noise needs the RTN axes of the state, which a rectilinear state (r × v = 0) does not " +
                "define.");
        }

        var radial = position / r;
        var normal = momentum / h;
        var transverse = normal.Cross(radial);
        Span<double> rr = stackalloc double[] { radial.X, radial.Y, radial.Z };
        Span<double> tt = stackalloc double[] { transverse.X, transverse.Y, transverse.Z };
        Span<double> nn = stackalloc double[] { normal.X, normal.Y, normal.Z };
        for (int i = 0; i < 3; i++)
        {
            for (int j = i; j < 3; j++)
            {
                double value = Radial * rr[i] * rr[j] + Transverse * tt[i] * tt[j] + Normal * nn[i] * nn[j];
                qc[3 * i + j] += value;
                if (j > i)
                {
                    qc[3 * j + i] += value;
                }
            }
        }
    }

    private static double Validate(double density, string name)
    {
        if (!(density >= 0.0) || double.IsPositiveInfinity(density))
        {
            throw new ArgumentOutOfRangeException(name, density,
                "A spectral density must be finite and non-negative, in m²/s³.");
        }

        return density;
    }
}
