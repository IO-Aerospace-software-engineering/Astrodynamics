// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// A force written as outside the library, overriding only <see cref="ForceBase.Apply(StateVector)"/>, with a known
/// Jacobian: a = −μ r / |r|³ + Ω × v, non-linear in r and linear in v.
/// </summary>
internal class KnownJacobianForce : ForceBase
{
    internal const double Mu = 3.986004418e14;
    internal static readonly Vector3 Omega = new(2e-4, -1e-4, 3e-4);

    public override Vector3 Apply(StateVector stateVector)
    {
        var r = stateVector.Position;
        double rMagnitude = r.Magnitude();
        return r * (-Mu / (rMagnitude * rMagnitude * rMagnitude)) + Omega.Cross(stateVector.Velocity);
    }

    /// <summary>∂a/∂r = μ (3 r rᵀ / |r|⁵ − I / |r|³), row-major.</summary>
    internal static double[] PositionJacobian(in Vector3 r)
    {
        double[] p = { r.X, r.Y, r.Z };
        double r2 = r.MagnitudeSquared();
        double r3 = r2 * System.Math.Sqrt(r2);
        var jacobian = new double[9];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                jacobian[3 * i + j] = Mu * (3.0 * p[i] * p[j] / (r3 * r2) - (i == j ? 1.0 / r3 : 0.0));
            }
        }

        return jacobian;
    }

    /// <summary>∂a/∂v = [Ω×], row-major.</summary>
    internal static double[] VelocityJacobian()
    {
        return new[]
        {
            0.0, -Omega.Z, Omega.Y,
            Omega.Z, 0.0, -Omega.X,
            -Omega.Y, Omega.X, 0.0
        };
    }
}

/// <summary>
/// The position-only part of <see cref="KnownJacobianForce"/>, declaring that it does not depend on the velocity.
/// </summary>
internal sealed class KnownPositionOnlyForce : KnownJacobianForce
{
    public override Vector3 Apply(StateVector stateVector)
    {
        var r = stateVector.Position;
        double rMagnitude = r.Magnitude();
        return r * (-Mu / (rMagnitude * rMagnitude * rMagnitude));
    }

    internal override bool DependsOnVelocity => false;
}
