// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// A linear force with closed-form solutions: a = −ω² r − 2ζω v + Cd u + Cr w, a damped harmonic oscillator on each axis,
/// forced by the two coefficients along fixed directions. Its partials are analytic: G = −ω² I, D = −2ζω I,
/// ∂a/∂Cd = u, ∂a/∂Cr = w.
/// </summary>
internal sealed class LinearTestForce : ForceBase
{
    private readonly double _omega;
    private readonly double _damping;
    private readonly Vector3 _dragDirection;
    private readonly Vector3 _reflectivityDirection;

    /// <param name="omega">ω, in rad/s.</param>
    /// <param name="damping">ζ, dimensionless.</param>
    /// <param name="dragDirection">u, in m/s² per unit of Cd.</param>
    /// <param name="reflectivityDirection">w, in m/s² per unit of Cr.</param>
    internal LinearTestForce(double omega, double damping = 0.0, Vector3 dragDirection = default,
        Vector3 reflectivityDirection = default)
    {
        _omega = omega;
        _damping = damping;
        _dragDirection = dragDirection;
        _reflectivityDirection = reflectivityDirection;
    }

    internal override bool DependsOnVelocity => _damping != 0.0;

    internal override ForceParameters Parameters =>
        (_dragDirection != Vector3.Zero ? ForceParameters.DragCoefficient : ForceParameters.None)
        | (_reflectivityDirection != Vector3.Zero ? ForceParameters.ReflectivityCoefficient : ForceParameters.None);

    /// <summary>The live path, with Cd = Cr = 0.</summary>
    public override Vector3 Apply(StateVector stateVector)
    {
        return Apply(stateVector, new ForceEvaluationContext(1.0, 0.0, 0.0));
    }

    internal override Vector3 Apply(StateVector stateVector, in ForceEvaluationContext context)
    {
        return stateVector.Position * (-_omega * _omega) + stateVector.Velocity * (-2.0 * _damping * _omega)
                                                         + _dragDirection * context.DragCoefficient
                                                         + _reflectivityDirection * context.ReflectivityCoefficient;
    }

    private protected override void AccumulateStatePartialsCore(StateVector stateVector,
        in ForceEvaluationContext context, Span<double> dadr, Span<double> dadv)
    {
        for (int i = 0; i < 3; i++)
        {
            dadr[4 * i] += -_omega * _omega;
            dadv[4 * i] += -2.0 * _damping * _omega;
        }
    }

    private protected override void AccumulateParameterPartialsCore(StateVector stateVector,
        in ForceEvaluationContext context, Span<double> dadCd, Span<double> dadCr)
    {
        dadCd[0] += _dragDirection.X;
        dadCd[1] += _dragDirection.Y;
        dadCd[2] += _dragDirection.Z;
        dadCr[0] += _reflectivityDirection.X;
        dadCr[1] += _reflectivityDirection.Y;
        dadCr[2] += _reflectivityDirection.Z;
    }
}
