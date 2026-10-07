// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.Body;

/// <summary>
/// Position partials of a point-mass attraction, shared by the central body and the third bodies.
/// </summary>
internal static class PointMassPartials
{
    /// <summary>
    /// Adds ∂a/∂ρ = μ (3 ρ ρᵀ / |ρ|⁵ − I / |ρ|³) of the acceleration a = −μ ρ / |ρ|³ to <paramref name="dadr"/>.
    /// </summary>
    /// <remarks>
    /// Reference: Montenbruck and Gill, Satellite Orbits, Springer (2000), chapter 7 (variational equations, partials
    /// of the point-mass acceleration), to be verified by Sylvain.
    /// </remarks>
    /// <param name="relativePosition">ρ, position of the attracted point relative to the attracting mass, in m.</param>
    /// <param name="gm">μ, gravitational parameter of the attracting mass, in m³/s².</param>
    /// <param name="dadr">3×3 row-major block the partials are added to, in 1/s².</param>
    internal static void Accumulate(in Vector3 relativePosition, double gm, Span<double> dadr)
    {
        double x = relativePosition.X;
        double y = relativePosition.Y;
        double z = relativePosition.Z;
        double r2 = x * x + y * y + z * z;
        double r = System.Math.Sqrt(r2);
        double muOverR3 = gm / (r2 * r);
        double threeMuOverR5 = 3.0 * muOverR3 / r2;

        double xy = threeMuOverR5 * x * y;
        double xz = threeMuOverR5 * x * z;
        double yz = threeMuOverR5 * y * z;

        dadr[0] += threeMuOverR5 * x * x - muOverR3;
        dadr[1] += xy;
        dadr[2] += xz;
        dadr[3] += xy;
        dadr[4] += threeMuOverR5 * y * y - muOverR3;
        dadr[5] += yz;
        dadr[6] += xz;
        dadr[7] += yz;
        dadr[8] += threeMuOverR5 * z * z - muOverR3;
    }
}
