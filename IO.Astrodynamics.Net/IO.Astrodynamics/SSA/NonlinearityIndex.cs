// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
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
/// orbit and attitude dynamics</i>, Journal of the Astronautical Sciences 52, 2004, to be verified by S. Guillet):
/// ν(t) = max over i of ‖Φ(t, t0; x̄0 + δx_i) − Φ(t, t0; x̄0)‖_F / ‖Φ(t, t0; x̄0)‖_F, the δx_i on the 1σ boundary of
/// the initial covariance P0. It measures how much the linear map that carries P0 to the encounter changes across the
/// initial uncertainty: zero for a linear flow, so that the linear propagation P = Φ P0 Φᵀ holds; larger as the
/// trajectories of the uncertainty region depart from the tangent map.
/// </para>
/// <para>
/// The points are the twelve ±√λ_k e_k on the principal axes of P0 (its eigenvectors e_k and eigenvalues λ_k), and
/// Φ(t, t0; x̄0 + δx_i) the state transition matrix of the RK7(8) propagation from the perturbed state, with the forces,
/// the integrator settings, the mass and the coefficients of the nominal propagation. Φ and its Frobenius norm are taken
/// in SI units, as the state transition matrix of the library. Computed for a propagation without maneuvers only: the
/// perturbed trajectories would need the maneuvers of the nominal one.
/// </para>
/// <para>
/// The twelve propagations run once per participant and per analysis, to its last encounter epoch, under the lock of
/// the dynamics of the nominal propagation, whose forces they share.
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
    internal static NonlinearityIndex Create(PropagationSolution nominal, ILocalizable participant, in Time end)
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
        var p0 = initial.Covariance.Value;
        var matrix = Matrix<double>.Build.Dense(6, 6, (i, j) => 0.5 * (p0.Get(i, j) + p0.Get(j, i)));
        var evd = matrix.Evd(MathNet.Numerics.LinearAlgebra.Symmetricity.Symmetric);

        var perturbed = new PropagationSolution[12];
        var window = new Window(start, end);
        var options = new CovariancePropagationOptions();
        lock (dynamics.SyncRoot)
        {
            for (int k = 0; k < 6; k++)
            {
                double scale = System.Math.Sqrt(System.Math.Max(evd.EigenValues[k].Real, 0.0));
                for (int sign = 0; sign < 2; sign++)
                {
                    double s = sign == 0 ? scale : -scale;
                    var offsetPosition = new Math.Vector3(evd.EigenVectors[0, k], evd.EigenVectors[1, k], evd.EigenVectors[2, k]) * s;
                    var offsetVelocity = new Math.Vector3(evd.EigenVectors[3, k], evd.EigenVectors[4, k], evd.EigenVectors[5, k]) * s;
                    var state = new StateVector(x0Position + offsetPosition, x0Velocity + offsetVelocity, dynamics.Observer,
                        start, dynamics.Frame);
                    perturbed[2 * k + sign] = Propagate(dynamics, context, state, window, options);
                }
            }
        }

        return new NonlinearityIndex(perturbed);
    }

    /// <summary>
    /// ν at <paramref name="epoch"/>, against Φ of the nominal propagation there.
    /// </summary>
    internal double At(PropagationSolution nominal, in Time epoch)
    {
        var phi = nominal.GetStateTransitionMatrix(epoch);
        double norm = Frobenius(phi, null);
        double largest = 0.0;
        foreach (var solution in _perturbed)
        {
            largest = System.Math.Max(largest, Frobenius(solution.GetStateTransitionMatrix(epoch), phi) / norm);
        }

        return largest;
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
            dynamics.OrientationCache);
        return propagator.Propagate();
    }

    private static int _nextId = -9_500_000;

    private static int NextId() => System.Threading.Interlocked.Decrement(ref _nextId);

    // ‖A − B‖_F, or ‖A‖_F when B is null
    private static double Frobenius(Math.Matrix a, Math.Matrix? b)
    {
        double sum = 0.0;
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double value = a.Get(i, j) - (b?.Get(i, j) ?? 0.0);
                sum += value * value;
            }
        }

        return System.Math.Sqrt(sum);
    }
}
