// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Integrators;

/// <summary>
/// The 13 stages of one Prince-Dormand 8(7) step (<see cref="RK78ButcherTableau"/>): the 8th-order solution and the
/// difference between the 8th- and 7th-order solutions, from which the integrator builds its error estimate.
/// </summary>
/// <remarks>
/// <para>
/// The step is shared by <see cref="RK78Integrator"/> and by the evaluations made after a propagation, so that both
/// compute exactly the same doubles: same stage states, same operation order.
/// </para>
/// <para>
/// After <see cref="Step"/>, <see cref="StageStates"/> holds the 13 stage states of that step, each at its stage epoch.
/// They stay valid until the next call to <see cref="Step"/> or <see cref="Reset"/>.
/// </para>
/// <para>
/// Not thread-safe: the stage states and derivatives are reused from one step to the next.
/// </para>
/// </remarks>
internal sealed class RK78Stepper
{
    private readonly IReadOnlyList<ForceBase> _forces;
    private readonly Vector3[] _kPos = new Vector3[RK78ButcherTableau.Stages]; // velocity at stage s (dr/dt)
    private readonly Vector3[] _kVel = new Vector3[RK78ButcherTableau.Stages]; // acceleration at stage s (dv/dt)
    private StateVector[] _stageStates;

    /// <summary>
    /// Create a stepper that sums the accelerations of <paramref name="forces"/>, in their order.
    /// </summary>
    /// <param name="forces">The forces. The list is read at each step, so forces added to it later are included.</param>
    /// <exception cref="ArgumentNullException"><paramref name="forces"/> is null.</exception>
    internal RK78Stepper(IReadOnlyList<ForceBase> forces)
    {
        _forces = forces ?? throw new ArgumentNullException(nameof(forces));
    }

    /// <summary>
    /// The context passed to <see cref="ForceBase.Apply(StateVector, in ForceEvaluationContext)"/>, or null to call
    /// <see cref="ForceBase.Apply(StateVector)"/>, which reads the live spacecraft.
    /// </summary>
    internal ForceEvaluationContext? Context { get; set; }

    /// <summary>The 13 stage states of the last step, in stage order.</summary>
    internal IReadOnlyList<StateVector> StageStates => _stageStates;

    /// <summary>Acceleration at the first stage of the last step, at its start.</summary>
    internal Vector3 StartAcceleration => _kVel[0];

    /// <summary>Acceleration at the last stage of the last step (c = 1), used as the end acceleration for dense output.</summary>
    internal Vector3 EndAcceleration => _kVel[RK78ButcherTableau.Stages - 1];

    /// <summary>
    /// Create the stage states, relative to <paramref name="observer"/> and in <paramref name="frame"/>.
    /// </summary>
    /// <param name="observer">Observer of the integrated state.</param>
    /// <param name="frame">Frame of the integrated state.</param>
    /// <param name="epoch">Initial epoch of the stage states, overwritten by each step.</param>
    internal void Reset(ILocalizable observer, Frame frame, in Time epoch)
    {
        _stageStates = new StateVector[RK78ButcherTableau.Stages];
        for (int i = 0; i < RK78ButcherTableau.Stages; i++)
        {
            _stageStates[i] = new StateVector(Vector3.Zero, Vector3.Zero, observer, epoch, frame);
        }
    }

    /// <summary>
    /// Compute one step from (<paramref name="pos0"/>, <paramref name="vel0"/>) at
    /// <paramref name="baseEpoch"/> + <paramref name="tOffset"/>, advancing by <paramref name="h"/> seconds.
    /// </summary>
    /// <param name="pos0">Position at the start of the step.</param>
    /// <param name="vel0">Velocity at the start of the step.</param>
    /// <param name="baseEpoch">Base epoch of the segment.</param>
    /// <param name="tOffset">Start of the step, in seconds from <paramref name="baseEpoch"/>.</param>
    /// <param name="h">Step size, in seconds.</param>
    /// <param name="posNew">Position at the end of the step (8th-order solution).</param>
    /// <param name="velNew">Velocity at the end of the step (8th-order solution).</param>
    /// <param name="errPos">Difference between the 8th- and 7th-order positions.</param>
    /// <param name="errVel">Difference between the 8th- and 7th-order velocities.</param>
    internal void Step(in Vector3 pos0, in Vector3 vel0, in Time baseEpoch, double tOffset, double h,
        out Vector3 posNew, out Vector3 velNew, out Vector3 errPos, out Vector3 errVel)
    {
        var a = RK78ButcherTableau.A;
        var c = RK78ButcherTableau.C;
        var b = RK78ButcherTableau.B;
        var e = RK78ButcherTableau.E;

        // Stage 0: evaluate at current point
        Integrator.UpdateStateVector(_stageStates[0], pos0, vel0, baseEpoch.AddSeconds(tOffset));
        _kPos[0] = vel0;
        _kVel[0] = ComputeAcceleration(_stageStates[0]);

        // Stages 1..12
        for (int s = 1; s < RK78ButcherTableau.Stages; s++)
        {
            var rS = pos0;
            var vS = vel0;
            var aS = a[s];

            for (int j = 0; j < s; j++)
            {
                double aCoeff = aS[j];
                if (aCoeff != 0.0) // skip zero coefficients for performance
                {
                    double hA = h * aCoeff;
                    rS += _kPos[j] * hA;
                    vS += _kVel[j] * hA;
                }
            }

            Integrator.UpdateStateVector(_stageStates[s], rS, vS, baseEpoch.AddSeconds(tOffset + c[s] * h));
            _kPos[s] = vS;
            _kVel[s] = ComputeAcceleration(_stageStates[s]);
        }

        // 8th-order solution
        posNew = pos0;
        velNew = vel0;

        for (int j = 0; j < RK78ButcherTableau.Stages; j++)
        {
            if (b[j] != 0.0)
            {
                double hB = h * b[j];
                posNew += _kPos[j] * hB;
                velNew += _kVel[j] * hB;
            }
        }

        // Error estimate (difference between 8th and 7th order)
        errPos = Vector3.Zero;
        errVel = Vector3.Zero;

        for (int j = 0; j < RK78ButcherTableau.Stages; j++)
        {
            if (e[j] != 0.0)
            {
                double hE = h * e[j];
                errPos += _kPos[j] * hE;
                errVel += _kVel[j] * hE;
            }
        }
    }

    // Same sum, in the same order, as Integrator.ComputeAcceleration, without the enumerator of the collection.
    private Vector3 ComputeAcceleration(StateVector stateVector)
    {
        Vector3 res = Vector3.Zero;
        if (Context is { } context)
        {
            for (int i = 0; i < _forces.Count; i++)
            {
                res += _forces[i].Apply(stateVector, context);
            }
        }
        else
        {
            for (int i = 0; i < _forces.Count; i++)
            {
                res += _forces[i].Apply(stateVector);
            }
        }

        return res;
    }
}
