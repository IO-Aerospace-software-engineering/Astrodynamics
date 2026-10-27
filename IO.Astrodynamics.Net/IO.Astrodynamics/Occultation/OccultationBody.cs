// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.Occultation;

/// <summary>
/// A body of an occultation at an instant, as the observer sees it: a triaxial ellipsoid or a point, its center
/// relative to the observer, in the axes of ICRF, in meters. Immutable.
/// </summary>
/// <remarks>
/// The ellipsoid is given by its semi-axis vectors, the columns of A = R diag(a, b, c), where R rotates the body-fixed
/// frame into ICRF: a point x relative to the center is on the surface when |A⁻¹ x| = 1. A⁻¹ maps the ellipsoid to the
/// unit sphere; since the columns of A are orthogonal, (A⁻¹ y)ᵢ = (aᵢ · y) / |aᵢ|².
/// </remarks>
internal readonly struct OccultationBody
{
    private readonly Vector3 _axis1;
    private readonly Vector3 _axis2;
    private readonly Vector3 _axis3;
    private readonly double _squaredLength1;
    private readonly double _squaredLength2;
    private readonly double _squaredLength3;

    private OccultationBody(in Vector3 center, in Vector3 axis1, in Vector3 axis2, in Vector3 axis3, bool isPoint)
    {
        Center = center;
        _axis1 = axis1;
        _axis2 = axis2;
        _axis3 = axis3;
        IsPoint = isPoint;
        _squaredLength1 = axis1.MagnitudeSquared();
        _squaredLength2 = axis2.MagnitudeSquared();
        _squaredLength3 = axis3.MagnitudeSquared();
        MaxRadius = isPoint
            ? 0.0
            : System.Math.Sqrt(System.Math.Max(_squaredLength1, System.Math.Max(_squaredLength2, _squaredLength3)));
    }

    /// <summary>The center relative to the observer, m, ICRF.</summary>
    internal Vector3 Center { get; }

    /// <summary>Whether the body is a point.</summary>
    internal bool IsPoint { get; }

    /// <summary>The longest semi-axis, m; 0 for a point.</summary>
    internal double MaxRadius { get; }

    /// <summary>A point body.</summary>
    internal static OccultationBody Point(in Vector3 center)
    {
        return new OccultationBody(center, Vector3.Zero, Vector3.Zero, Vector3.Zero, true);
    }

    /// <summary>An ellipsoid, from its three orthogonal semi-axis vectors in ICRF, of positive lengths.</summary>
    internal static OccultationBody Ellipsoid(in Vector3 center, in Vector3 axis1, in Vector3 axis2, in Vector3 axis3)
    {
        if (!(axis1.MagnitudeSquared() > 0.0 && axis2.MagnitudeSquared() > 0.0 && axis3.MagnitudeSquared() > 0.0))
        {
            throw new ArgumentException("The semi-axes of an ellipsoid must have positive lengths.");
        }

        return new OccultationBody(center, axis1, axis2, axis3, false);
    }

    /// <summary>
    /// A⁻¹ y: the coordinates of the vector <paramref name="y"/> in the space where the ellipsoid is the unit sphere.
    /// </summary>
    internal Vector3 ToUnitSphere(in Vector3 y)
    {
        return new Vector3((_axis1 * y) / _squaredLength1, (_axis2 * y) / _squaredLength2, (_axis3 * y) / _squaredLength3);
    }

    /// <summary>A x: the vector of ICRF whose coordinates in the space of the unit sphere are <paramref name="x"/>.</summary>
    private Vector3 FromUnitSphere(in Vector3 x)
    {
        return _axis1 * x.X + _axis2 * x.Y + _axis3 * x.Z;
    }

    /// <summary>
    /// The limb of the ellipsoid as the observer sees it: the ellipse along which the rays from the observer touch it.
    /// </summary>
    /// <remarks>
    /// The method of CSPICE's <c>edlimb</c>: in the space of the unit sphere, where the observer is at w = −A⁻¹ c, the
    /// limb is the circle of center w / |w|², of radius √(1 − 1/|w|²), in the plane normal to w; A maps it back to the
    /// ellipse of the ellipsoid, since a linear map keeps the tangency of the rays.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The observer is inside the ellipsoid or on its surface.</exception>
    internal LimbEllipse Limb()
    {
        var w = ToUnitSphere(Center) * -1.0;
        double level = w.MagnitudeSquared();
        if (!(level > 1.0))
        {
            throw new InvalidOperationException(
                $"The observer is inside a body of the occultation, or on its surface (level {level}).");
        }

        var center = w / level;
        double radius = System.Math.Sqrt((level - 1.0) / level);
        OrthonormalBasis(w / System.Math.Sqrt(level), out var e1, out var e2);
        return new LimbEllipse(Center + FromUnitSphere(center), FromUnitSphere(e1) * radius, FromUnitSphere(e2) * radius);
    }

    /// <summary>
    /// The parameter t at which the ray t <paramref name="direction"/> (t ≥ 0) from the observer enters the ellipsoid,
    /// or NaN when it misses it.
    /// </summary>
    internal double RayEntry(in Vector3 direction)
    {
        var d = ToUnitSphere(direction);
        var c = ToUnitSphere(Center);
        double a = d * d;
        double b = d * c;
        double excess = c * c - 1.0;
        double discriminant = b * b - a * excess;
        if (discriminant < 0.0 || b <= 0.0)
        {
            return double.NaN;
        }

        // The smaller root of a t² - 2 b t + excess = 0, in the form without cancellation
        return excess / (b + System.Math.Sqrt(discriminant));
    }

    /// <summary>
    /// The parameter t of the point of the ray t <paramref name="direction"/> closest to the center, in the space of the
    /// unit sphere: where the ray would enter the ellipsoid if it grazed it.
    /// </summary>
    internal double RayClosestApproach(in Vector3 direction)
    {
        var d = ToUnitSphere(direction);
        return (d * ToUnitSphere(Center)) / (d * d);
    }

    /// <summary>Two unit vectors orthogonal to the unit vector <paramref name="n"/> and to each other.</summary>
    internal static void OrthonormalBasis(in Vector3 n, out Vector3 e1, out Vector3 e2)
    {
        double x = System.Math.Abs(n.X);
        double y = System.Math.Abs(n.Y);
        double z = System.Math.Abs(n.Z);
        var helper = x <= y && x <= z ? Vector3.VectorX : y <= z ? Vector3.VectorY : Vector3.VectorZ;
        e1 = helper.Cross(n).Normalize();
        e2 = n.Cross(e1);
    }
}

/// <summary>
/// An ellipse C + U cos θ + V sin θ, positions relative to the observer, with U and V conjugate semi-diameters.
/// </summary>
internal readonly record struct LimbEllipse(Vector3 Center, Vector3 U, Vector3 V)
{
    /// <summary>The point of parameter θ.</summary>
    internal Vector3 At(double theta)
    {
        return Center + U * System.Math.Cos(theta) + V * System.Math.Sin(theta);
    }
}
