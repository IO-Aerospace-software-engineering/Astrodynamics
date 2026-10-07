// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Forces;

/// <summary>
/// Third-body gravitational perturbation using Battin's numerically stable formulation.
/// Computes the tidal acceleration of a perturbing body on a satellite in a central-body-centered frame.
/// </summary>
public class ThirdBodyPerturbation : ForceBase
{
    public CelestialItem PerturbingBody { get; }
    public CelestialItem CentralBody { get; }

    public ThirdBodyPerturbation(CelestialItem perturbingBody, CelestialItem centralBody)
    {
        PerturbingBody = perturbingBody ?? throw new ArgumentNullException(nameof(perturbingBody));
        CentralBody = centralBody ?? throw new ArgumentNullException(nameof(centralBody));
    }

    /// <summary>
    /// Compute third-body perturbation acceleration using Battin's formula.
    /// The state vector must be central-body-relative.
    /// </summary>
    public override Vector3 Apply(StateVector stateVector)
    {
        // d_j = position of perturbing body relative to central body
        Vector3 dj = PerturbingBodyPosition(stateVector.Epoch);

        var r = stateVector.Position;
        double djMag = dj.Magnitude();
        double djMag2 = djMag * djMag;

        // Battin's q parameter: q = r . (r - 2*d_j) / |d_j|^2
        // Note: |r - d_j|^2 = |d_j|^2 * (1 + q), so q encodes the geometry stably.
        var rMinus2Dj = r - dj * 2.0;
        double q = (r * rMinus2Dj) / djMag2;

        // Battin's f(q) stably computes (1+q)^(3/2) - 1
        double fq = BattinF(q);

        // |r - d_j|^3 = |d_j|^3 * (1+q)^(3/2), computed without forming (r - d_j) explicitly.
        double onePlusQ32 = System.Math.Pow(1.0 + q, 1.5);
        double rMinusDjMag3 = djMag2 * djMag * onePlusQ32;

        // a = -mu_j / |r - d_j|^3 * (r + f(q) * d_j)
        // Equivalent to: -mu_j / (|d_j|^3 * (1+q)^(3/2)) * (r + f(q) * d_j)
        var acceleration = (r + dj * fq) * (-PerturbingBody.GM / rMinusDjMag3);

        return acceleration;
    }

    internal override bool DependsOnVelocity => false;

    /// <summary>
    /// Analytic ∂a/∂r = μ_j (3 ρ ρᵀ / |ρ|⁵ − I / |ρ|³), with ρ = r − d_j: the partials of the direct term. The indirect
    /// term, the attraction of the perturbing body on the central body, does not depend on r.
    /// </summary>
    /// <remarks>
    /// Reference: Montenbruck and Gill, Satellite Orbits, Springer (2000), chapter 7 (variational equations),
    /// confirmed by S. Guillet (2026-10-07). d_j is the same as in <see cref="Apply(StateVector)"/>.
    /// </remarks>
    private protected override void AccumulateStatePartialsCore(StateVector stateVector,
        in ForceEvaluationContext context, Span<double> dadr, Span<double> dadv)
    {
        var rho = stateVector.Position - PerturbingBodyPosition(stateVector.Epoch);
        PointMassPartials.Accumulate(rho, PerturbingBody.GM, dadr);
    }

    /// <summary>
    /// d_j, position of the perturbing body relative to the central body: from the ephemeris cache when it holds the
    /// body, otherwise from SPICE in ICRF.
    /// </summary>
    private Vector3 PerturbingBodyPosition(in Time epoch)
    {
        if (EphemerisCache != null && EphemerisCache.Contains(PerturbingBody.NaifId, Aberration.None))
            return EphemerisCache.GetPosition(PerturbingBody.NaifId, Aberration.None, epoch);

        return PerturbingBody.GetEphemeris(epoch, CentralBody, Frame.ICRF, Aberration.None)
            .ToStateVector().Position;
    }

    /// <summary>
    /// Battin's f(q) function: f(q) = q * (3 + 3q + q^2) / (1 + (1+q)^(3/2))
    /// This avoids catastrophic cancellation when computing third-body tidal acceleration.
    /// Reference: Battin, "An Introduction to the Methods of Astrodynamics", Section 8.6
    /// </summary>
    public static double BattinF(double q)
    {
        double numerator = q * (3.0 + 3.0 * q + q * q);
        double denominator = 1.0 + System.Math.Pow(1.0 + q, 1.5);
        return numerator / denominator;
    }
}
