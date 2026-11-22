// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Forces;

/// <summary>
/// Third-body gravitational perturbation, a = −μ_j ((r − d_j) / |r − d_j|³ + d_j / |d_j|³): Battin's numerically stable
/// formulation far from the perturbing body, the direct difference near it.
/// Computes the tidal acceleration of a perturbing body on a satellite in a central-body-centered frame.
/// </summary>
/// <remarks>
/// Battin's form is accurate where the spacecraft is near the central body relative to the perturbing body (|r| ≪ |d_j|,
/// the Moon and the Sun on an Earth orbit), where the direct difference cancels. Near the perturbing body it loses
/// digits: its 1 + q = |r − d_j|² / |d_j|² comes from terms of order 1, so its relative error is about
/// ε |d_j|² / |r − d_j|², 1e-7 in SSB mode about the Earth (the Sun the central body, the Earth the third body,
/// |r − d_j| / |d_j| = 4.6e-5), which put a noise of about 1e-6 m/s² on the Earth's attraction (#576, #544). Where
/// |r − d_j| &lt; |d_j| / 2 the acceleration is the direct difference, ρ = r − d_j formed once (each component rounded once,
/// exact when the two agree within a factor of 2): a few ε of the exact acceleration at every distance, 2.7e-15 at most
/// around the switch (before 11.0.0: 2.7e-7 at 4.6e-5, 6.3e-4 at 1e-6). Elsewhere it is Battin's, bit for bit as before.
/// </remarks>
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
    /// Compute the third-body perturbation acceleration: the direct difference where the spacecraft is closer to the
    /// perturbing body than half its distance to the central body, Battin's formula elsewhere (#576).
    /// The state vector must be central-body-relative.
    /// </summary>
    public override Vector3 Apply(StateVector stateVector)
    {
        // d_j = position of perturbing body relative to central body, in the frame of the state (the ephemeris cache is
        // built in the frame of the propagated state)
        Vector3 dj = PerturbingBodyPosition(stateVector.Epoch, stateVector.Frame);

        var r = stateVector.Position;
        double djMag = dj.Magnitude();
        double djMag2 = djMag * djMag;

        // Near the perturbing body, |r - d_j| < |d_j| / 2: the direct difference, whose 1/|r - d_j|^3 Battin's 1 + q would
        // give to about eps |d_j|^2 / |r - d_j|^2 only (#576, #544)
        var rho = r - dj;
        double rhoMag2 = rho.Dot(rho);
        if (4.0 * rhoMag2 < djMag2)
        {
            double rhoMag = System.Math.Sqrt(rhoMag2);
            return (rho * (1.0 / (rhoMag2 * rhoMag)) + dj * (1.0 / (djMag2 * djMag))) * -PerturbingBody.GM;
        }

        // Battin's q parameter: q = r . (r - 2*d_j) / |d_j|^2
        // Note: |r - d_j|^2 = |d_j|^2 * (1 + q), so q encodes the geometry stably.
        var rMinus2Dj = r - dj * 2.0;
        double q = r.Dot(rMinus2Dj) / djMag2;

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
    /// <para>
    /// Reference: Montenbruck and Gill, Satellite Orbits, Springer (2000), chapter 7 (variational equations),
    /// confirmed by S. Guillet (2026-10-07). d_j is the same as in <see cref="Apply(StateVector)"/>.
    /// </para>
    /// <para>
    /// Precondition, as for <see cref="Apply(StateVector)"/>: the state is relative to <see cref="CentralBody"/>. d_j is
    /// in the frame of the state, from the SPICE fallback or from the ephemeris cache, which the propagator builds in the
    /// frame of the propagated state, so the blocks are in that frame.
    /// </para>
    /// </remarks>
    private protected override void AccumulateStatePartialsCore(StateVector stateVector,
        in ForceEvaluationContext context, Span<double> dadr, Span<double> dadv)
    {
        var rho = stateVector.Position - PerturbingBodyPosition(stateVector.Epoch, stateVector.Frame);
        PointMassPartials.Accumulate(rho, PerturbingBody.GM, dadr);
    }

    /// <summary>
    /// d_j, position of the perturbing body relative to the central body, in <paramref name="frame"/>: from the
    /// ephemeris cache when it holds the body (the propagator builds it in the frame of the propagated state),
    /// otherwise from SPICE.
    /// </summary>
    private Vector3 PerturbingBodyPosition(in Time epoch, Frame frame)
    {
        if (EphemerisCache != null && EphemerisCache.Contains(PerturbingBody.NaifId, Aberration.None))
            return EphemerisCache.GetPosition(PerturbingBody.NaifId, Aberration.None, epoch);

        return PerturbingBody.GetEphemeris(epoch, CentralBody, frame, Aberration.None).Position;
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
