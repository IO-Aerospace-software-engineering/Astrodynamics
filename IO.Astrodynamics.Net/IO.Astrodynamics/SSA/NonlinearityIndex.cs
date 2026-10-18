// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.TimeSystem;
using MathNet.Numerics.LinearAlgebra;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// The non-linearity index of the propagation of a covariance from the initial state to an encounter epoch (the guard
/// decided on 2026-10-16 for step 10).
/// </summary>
/// <remarks>
/// <para>
/// The index of Junkins and Singla (J. L. Junkins, P. Singla, <i>How nonlinear is it? A tutorial on nonlinearity of
/// orbit and attitude dynamics</i>, Journal of the Astronautical Sciences 52, 2004, confirmed by S. Guillet, 2026-10-18):
/// ν(t) = sup over δx on the 1σ boundary of the initial covariance P0 of
/// ‖Φ(t, t0; x̄0 + δx) − Φ(t, t0; x̄0)‖_F / ‖Φ(t, t0; x̄0)‖_F. It measures how much the linear map that carries P0 to
/// the encounter changes across the initial uncertainty: zero for a linear flow, so that the linear propagation
/// P = Φ P0 Φᵀ holds; larger as the trajectories of the uncertainty region depart from the tangent map.
/// </para>
/// <para>
/// The supremum is taken over the whole boundary, δx = S u with ‖u‖ = 1 and S = P0^(1/2) the symmetric square root
/// (V Λ^(1/2) Vᵀ, unique even when P0 has repeated eigenvalues), on the first-order variation of Φ:
/// Φ(x̄0 + S u) − Φ(x̄0) ≈ M u, the columns of M (36 × 6) being m_k = [Φ(x̄0 + s_k) − Φ(x̄0 − s_k)] / 2, central
/// differences along the columns s_k of S. Then ν = σ_max(M) / ‖Φ(x̄0)‖_F, the largest singular value of M. It does not
/// depend on the basis: along the columns of S Q, Q orthogonal, M becomes M Q, of the same singular values (H3 review of
/// step 10, N1, decision of S. Guillet, 2026-10-18; the twelve points ±√λ_k e_k on the principal axes, used before,
/// depended on the axes the eigensolver returned within a repeated eigenspace). The second-order part of the variation
/// cancels in the central differences and is not in the supremum.
/// </para>
/// <para>
/// Φ(t, t0; x̄0 ± s_k) is the state transition matrix of the RK7(8) propagation from the perturbed state, with the
/// forces, the integrator settings, the mass and the coefficients of the nominal propagation. Φ and its Frobenius norm
/// are taken in SI units, as the state transition matrix of the library.
/// </para>
/// <para>
/// The twelve propagations run once per participant and per analysis, to its last encounter epoch, under the lock of
/// the dynamics of the nominal propagation, whose forces they share; their solutions evaluate Φ under that lock too.
/// The index is computed for a propagation of a single segment only (no maneuver, no event that restarts the
/// integration): the perturbed trajectories would need the maneuvers of the nominal one.
/// </para>
/// </remarks>
internal sealed class NonlinearityIndex
{
    private readonly PropagationSolution[] _perturbed;

    private NonlinearityIndex(PropagationSolution[] perturbed)
    {
        _perturbed = perturbed;
    }

    /// <summary>
    /// The twelve perturbed propagations of <paramref name="nominal"/>, from its start to <paramref name="end"/>, for the
    /// index at any epoch up to <paramref name="end"/>; null when the index cannot be computed (a propagation with
    /// maneuvers, without the dynamics of RK7(8), or an initial state without a covariance).
    /// </summary>
    /// <param name="nominal">The propagation of the participant, or the replay of its variational equations.</param>
    /// <param name="participant">The participant, whose initial state carries P0.</param>
    /// <param name="end">The last encounter epoch.</param>
    /// <param name="initialCovariance">P0 in place of the covariance of the initial state (a remediated one); null for
    /// that covariance.</param>
    internal static NonlinearityIndex Create(PropagationSolution nominal, ILocalizable participant, in Time end,
        Math.Matrix? initialCovariance = null)
    {
        var dynamics = nominal.Dynamics;
        if (dynamics?.Integrator == null || nominal.Segments.Count != 1 || nominal.Segments[0].Context is not { } context ||
            participant.InitialOrbitalParameters is not StateVector { Covariance: not null } initial)
        {
            return null;
        }

        var segment = nominal.Segments[0];
        var start = segment.BaseEpoch;
        var x0Position = segment.Steps[0].StartPosition;
        var x0Velocity = segment.Steps[0].StartVelocity;
        var root = SquareRoot(initialCovariance ?? initial.Covariance.Value);

        var perturbed = new PropagationSolution[12];
        var window = new Window(start, end);
        var options = new CovariancePropagationOptions();
        lock (dynamics.SyncRoot)
        {
            for (int k = 0; k < 6; k++)
            {
                var offsetPosition = new Math.Vector3(root[0, k], root[1, k], root[2, k]);
                var offsetVelocity = new Math.Vector3(root[3, k], root[4, k], root[5, k]);
                for (int sign = 0; sign < 2; sign++)
                {
                    var state = sign == 0
                        ? new StateVector(x0Position + offsetPosition, x0Velocity + offsetVelocity, dynamics.Observer,
                            start, dynamics.Frame)
                        : new StateVector(x0Position - offsetPosition, x0Velocity - offsetVelocity, dynamics.Observer,
                            start, dynamics.Frame);
                    perturbed[2 * k + sign] = Propagate(dynamics, context, state, window, options);
                }
            }
        }

        return new NonlinearityIndex(perturbed);
    }

    /// <summary>
    /// ν at <paramref name="epoch"/>: σ_max(M) / ‖Φ(x̄0)‖_F, M of the central differences of Φ along the columns of
    /// P0^(1/2).
    /// </summary>
    internal double At(PropagationSolution nominal, in Time epoch)
    {
        double norm = Frobenius(nominal.GetStateTransitionMatrix(epoch));
        var m = Matrix<double>.Build.Dense(36, 6);
        for (int k = 0; k < 6; k++)
        {
            var plus = _perturbed[2 * k].GetStateTransitionMatrix(epoch);
            var minus = _perturbed[2 * k + 1].GetStateTransitionMatrix(epoch);
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    m[6 * i + j, k] = 0.5 * (plus.Get(i, j) - minus.Get(i, j));
                }
            }
        }

        // σ_max(M)² is the largest eigenvalue of Mᵀ M, 6 × 6
        double largest = m.TransposeThisAndMultiply(m).Evd(Symmetricity.Symmetric).EigenValues.Max(e => e.Real);
        return System.Math.Sqrt(System.Math.Max(largest, 0.0)) / norm;
    }

    // The symmetric square root V Λ^(1/2) Vᵀ of P0, unique whatever the eigenvectors within a repeated eigenspace; the
    // negative eigenvalues of rounding clipped to zero
    private static Matrix<double> SquareRoot(Math.Matrix p0)
    {
        var matrix = Matrix<double>.Build.Dense(6, 6, (i, j) => 0.5 * (p0.Get(i, j) + p0.Get(j, i)));
        var evd = matrix.Evd(Symmetricity.Symmetric);
        var roots = Vector<double>.Build.Dense(6, k => System.Math.Sqrt(System.Math.Max(evd.EigenValues[k].Real, 0.0)));
        return evd.EigenVectors * Matrix<double>.Build.DenseOfDiagonalVector(roots) * evd.EigenVectors.Transpose();
    }

    // The STM-only propagation from a perturbed initial state, with the forces, settings and context of the nominal one
    private static PropagationSolution Propagate(PropagationDynamics dynamics, in ForceEvaluationContext context,
        StateVector state, in Window window, CovariancePropagationOptions options)
    {
        var spacecraft = new Spacecraft(NextId(), "NONLINEARITY_INDEX", System.Math.Max(context.TotalMass, 1e-9),
            System.Math.Max(context.TotalMass, 1e-9), new Clock("NONLINEARITY_INDEX", 65536), state, 1.0,
            context.DragCoefficient, null, context.ReflectivityCoefficient);
        var integrator = dynamics.Integrator.WithSameSettings();
        foreach (var force in dynamics.Forces)
        {
            integrator.AddForce(force);
        }

        using var propagator = new CentralBodyPropagator(window, spacecraft, integrator, window.Length, options,
            dynamics.OrientationCache, dynamics.SyncRoot);
        return propagator.Propagate();
    }

    private static int _nextId = -9_500_000;

    private static int NextId() => System.Threading.Interlocked.Decrement(ref _nextId);

    // ‖A‖_F
    private static double Frobenius(Math.Matrix a)
    {
        double sum = 0.0;
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                sum += a.Get(i, j) * a.Get(i, j);
            }
        }

        return System.Math.Sqrt(sum);
    }
}
