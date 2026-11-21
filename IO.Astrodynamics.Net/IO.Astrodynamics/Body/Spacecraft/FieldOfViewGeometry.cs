// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Occultation;

namespace IO.Astrodynamics.Body.Spacecraft;

/// <summary>
/// The field of view of an instrument at an instant, in the axes of ICRF: its boresight b, the unit vector b1 of the
/// reference plane (the reference vector made perpendicular to the boresight, CSPICE's <c>vperp</c>), b2 = b × b1, and
/// its half-angles: REF in the plane (b, b1), the IK's <c>FOV_REF_ANGLE</c>, and CROSS in the plane (b, b2), the IK's
/// <c>FOV_CROSS_ANGLE</c> (<c>getfov</c>, N0067).
/// </summary>
internal readonly record struct FieldOfViewAxes(Vector3 Boresight, Vector3 Ref, Vector3 Cross);

/// <summary>
/// The continuous margin of a target in a field of view, positive when the target is in view, zero at its contact with
/// the boundary of the field: the condition of CSPICE's <c>gftfov</c> (N0067, <c>zzgffvu</c>, read from its source).
/// </summary>
/// <remarks>
/// <para>
/// <b>Point target.</b> In view when its direction d is within the field: for a circle the angle of d from b at most
/// the half-angle (<c>vsep</c>); for an ellipse the ray through the plane at unit distance along b inside the ellipse of
/// semi-axes tan REF along b1 and tan CROSS along b2; for a rectangle the ray inside the rectangle of half-sides tan REF
/// along b1 and tan CROSS along b2. The margins: circle and ellipse through the cone body below, mapped to the unit
/// sphere (REF − the angle of d from b for the circle, a true angle; zero on (x / tan CROSS)² + (y / tan REF)² = 1 for
/// the ellipse, exact); rectangle min(REF − |atan2(d·b1, d·b)|, CROSS − |atan2(d·b2, d·b)|), the angles of d from the
/// two pairs of side planes, whose set is that of <c>gftfov</c>'s winding number.
/// </para>
/// <para>
/// <b>Ellipsoid target.</b> In view when the apparent disc of the ellipsoid overlaps the field, partly or whole, either
/// way round. For a circle or an ellipse, as <c>zzgffvu</c>: the field is the limb of an ellipsoid inscribed in its cone
/// (the sphere of radius D sin REF at D b, tangent to the circular cone of half-angle REF, stretched along b2 by
/// tan CROSS / tan REF; D the distance of the target, which keeps both bodies at one scale), and the margin is the
/// overlap margin of the occultation of #495 between that body and the target (<see cref="OccultationGeometry.Evaluate"/>,
/// the method of <c>zzocced</c>), with the target mapped to the unit sphere: an angle of that space, a true angle for a
/// spherical target. <c>zzocced</c> maps the body of larger apparent size; the target is mapped whatever its size, so
/// that the limb searched is the field's own, at the field's own aspect ratio: a natural body is close to a sphere (the
/// Earth's aspect ratio is 1.0034), and the 64 samples of the limb then resolve fields of any aspect ratio (measured to
/// 10 311, #515). An elongated target stretches the field's limb by its own aspect ratio. For a rectangle, where <c>zzgffvu</c> tests the
/// limb against the pyramid (<c>zzelvupy</c>, a true or false state), the margin is the largest, over the rectangle of the
/// plane at unit distance along b, of the discriminant h(v) = (w·c')² − |w|²(|c'|² − 1) of the ray v through the
/// ellipsoid, with w = A⁻¹v and c' = A⁻¹c in the space of its unit sphere, on the side where w·c' ≥ 0 (the rays that
/// meet the ellipsoid in front of the observer): h is a quadratic in the plane, and its largest value over that convex
/// polygon is at a vertex, at the critical point of an edge or at the interior critical point, all in closed form.
/// It is scaled by K |A⁻¹b|², K = |c'|² − 1, to keep its order of magnitude.
/// </para>
/// </remarks>
internal static class FieldOfViewGeometry
{
    /// <summary>The axes of a field of view from its boresight and reference vector, in the same frame.</summary>
    internal static FieldOfViewAxes Axes(in Vector3 boresight, in Vector3 refVector)
    {
        var b = boresight.NormalizeOrZero();
        var b1 = (refVector - b * refVector.Dot(b)).NormalizeOrZero();
        return new FieldOfViewAxes(b, b1, b.Cross(b1));
    }

    /// <summary>The margin of a target in the field of view: positive in view, zero at the contact, continuous.</summary>
    /// <param name="shape">The shape of the field.</param>
    /// <param name="refAngle">Its half-angle in the reference plane, rad.</param>
    /// <param name="crossAngle">Its half-angle across the reference plane, rad; ignored for a circle.</param>
    /// <param name="axes">Its axes.</param>
    /// <param name="target">The target as the observer sees it.</param>
    /// <exception cref="InvalidOperationException">For any shape, a point target at the observer (distance 0: it has no
    /// direction; the rectangle took the angles of the zero vector as 0 and reported it in view before 11.0.0, #721). For a
    /// circle or an ellipse: an ellipsoid target centred on the observer (it holds the observer), or the cone of the field
    /// cannot be built (<see cref="Cone"/>). For a rectangle and an ellipsoid target: the observer is inside the
    /// target.</exception>
    internal static double Margin(InstrumentShape shape, double refAngle, double crossAngle, in FieldOfViewAxes axes, in OccultationBody target)
    {
        double distance = target.Center.Magnitude();
        if (target.IsPoint && !(distance > 0.0))
        {
            // A point at the observer has no direction, whatever the shape of the field; the field itself is not at fault
            // (review of #715; the rectangle, whose margin took atan2(0, 0) = 0, since #721)
            throw new InvalidOperationException(
                $"The target is at the observer (distance {distance} m): it has no direction, so it is neither in nor out of the field of view.");
        }

        if (shape == InstrumentShape.Rectangular)
        {
            return target.IsPoint ? RectanglePointMargin(refAngle, crossAngle, axes, target.Center) : RectangleEllipsoidMargin(refAngle, crossAngle, axes, target);
        }

        double cross = shape == InstrumentShape.Circular ? refAngle : crossAngle;
        if (!(distance > 0.0))
        {
            // The cone at distance 0 has no extent: the observer is at the center of an ellipsoid target; the field itself is
            // not at fault (review of #715)
            throw new InvalidOperationException($"The observer is inside the target, or on its surface (at its center, distance {distance} m).");
        }

        var cone = Cone(refAngle, cross, axes, distance);
        // The target is the unit sphere, unless it is a point: then the cone body is
        return OccultationGeometry.Evaluate(cone, target, target.IsPoint, OccultationMargin.Overlap, false).Overlap;
    }

    /// <summary>
    /// An ellipsoid whose limb, seen from the vertex, is the elliptic cone of half-angles <paramref name="refAngle"/>
    /// along b1 and <paramref name="crossAngle"/> along b2, centered at <paramref name="distance"/> along b.
    /// </summary>
    /// <param name="refAngle">The half-angle REF of the field in the plane (b, b1), rad.</param>
    /// <param name="crossAngle">The half-angle CROSS of the field in the plane (b, b2), rad.</param>
    /// <param name="axes">The axes of the field in ICRF.</param>
    /// <param name="distance">The distance D of the target, positive (<see cref="Margin"/> rejects a target at the observer).</param>
    /// <exception cref="InvalidOperationException">The square of a semi-axis, D sin REF or D sin REF · tan CROSS / tan REF,
    /// underflows to 0 (a half-angle below about 6e-171 rad at the Moon's distance): the field cannot be searched. Before
    /// #715 the ellipsoid of the occultation search threw an unrelated <see cref="ArgumentException"/>.</exception>
    internal static OccultationBody Cone(double refAngle, double crossAngle, in FieldOfViewAxes axes, double distance)
    {
        double radius = distance * System.Math.Sin(refAngle);
        double stretch = System.Math.Tan(crossAngle) / System.Math.Tan(refAngle);
        var axis1 = axes.Ref * radius;
        var axis2 = axes.Cross * (radius * stretch);
        var axis3 = axes.Boresight * radius;
        // OccultationBody's own test, on the same vectors: a field the search cannot represent is an InvalidOperationException
        // of the field-of-view path, as the other fields it rejects (#715)
        if (!(axis1.MagnitudeSquared() > 0.0 && axis2.MagnitudeSquared() > 0.0 && axis3.MagnitudeSquared() > 0.0))
        {
            throw new InvalidOperationException(
                $"The field of view cannot be searched: its half-angles {refAngle} and {crossAngle} rad give the cone of the field, at the target's distance {distance} m, semi-axes of {radius} m and {radius * stretch} m whose squares underflow to 0.");
        }

        return OccultationBody.Ellipsoid(axes.Boresight * distance, axis1, axis2, axis3);
    }

    private static double RectanglePointMargin(double refAngle, double crossAngle, in FieldOfViewAxes axes, in Vector3 direction)
    {
        double z = direction.Dot(axes.Boresight);
        double y = direction.Dot(axes.Ref);
        double x = direction.Dot(axes.Cross);
        return System.Math.Min(refAngle - System.Math.Abs(System.Math.Atan2(y, z)), crossAngle - System.Math.Abs(System.Math.Atan2(x, z)));
    }

    private static double RectangleEllipsoidMargin(double refAngle, double crossAngle, in FieldOfViewAxes axes, in OccultationBody target)
    {
        // The space of the target's unit sphere: the observer at the origin, the center at c'
        var c = target.ToUnitSphere(target.Center);
        double k = c.Dot(c) - 1.0;
        if (!(k > 0.0))
        {
            throw new InvalidOperationException($"The observer is inside the target, or on its surface (level {c.Dot(c)}).");
        }

        // The ray v = b + X b2 + Y b1: w = w0 + X wx + Y wy
        var w0 = target.ToUnitSphere(axes.Boresight);
        var wx = target.ToUnitSphere(axes.Cross);
        var wy = target.ToUnitSphere(axes.Ref);
        double l0 = w0.Dot(c), lx = wx.Dot(c), ly = wy.Dot(c);
        // h = hxx X² + 2 hxy XY + hyy Y² + 2 hx X + 2 hy Y + h0
        double hxx = lx * lx - k * wx.Dot(wx);
        double hyy = ly * ly - k * wy.Dot(wy);
        double hxy = lx * ly - k * wx.Dot(wy);
        double hx = l0 * lx - k * w0.Dot(wx);
        double hy = l0 * ly - k * w0.Dot(wy);
        double h0 = l0 * l0 - k * w0.Dot(w0);
        double H(double px, double py) => hxx * px * px + 2.0 * hxy * px * py + hyy * py * py + 2.0 * hx * px + 2.0 * hy * py + h0;
        double scale = k * w0.Dot(w0);

        // The rectangle clipped by the half-plane l0 + lx X + ly Y ≥ 0 (Sutherland-Hodgman, one edge)
        double tx = System.Math.Tan(crossAngle), ty = System.Math.Tan(refAngle);
        Span<(double X, double Y)> corners = stackalloc (double, double)[] { (tx, ty), (-tx, ty), (-tx, -ty), (tx, -ty) };
        Span<(double X, double Y)> polygon = stackalloc (double, double)[5];
        int n = 0;
        for (int i = 0; i < 4; i++)
        {
            var p = corners[i];
            var q = corners[(i + 1) % 4];
            double lp = l0 + lx * p.X + ly * p.Y;
            double lq = l0 + lx * q.X + ly * q.Y;
            if (lp >= 0.0)
            {
                polygon[n++] = p;
            }

            if ((lp >= 0.0) != (lq >= 0.0))
            {
                double s = lp / (lp - lq);
                polygon[n++] = (p.X + s * (q.X - p.X), p.Y + s * (q.Y - p.Y));
            }
        }

        if (n == 0)
        {
            // No ray of the field meets the ellipsoid in front of the observer
            return -1.0;
        }

        double best = double.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            var p = polygon[i];
            var q = polygon[(i + 1) % n];
            best = System.Math.Max(best, H(p.X, p.Y));
            // h along the edge p + s (q − p): a s² + b s + h(p), critical at s = −b / 2a
            double dx = q.X - p.X, dy = q.Y - p.Y;
            double a = hxx * dx * dx + 2.0 * hxy * dx * dy + hyy * dy * dy;
            double b = 2.0 * (hxx * p.X * dx + hxy * (p.X * dy + p.Y * dx) + hyy * p.Y * dy + hx * dx + hy * dy);
            if (a != 0.0)
            {
                double s = -b / (2.0 * a);
                if (s > 0.0 && s < 1.0)
                {
                    best = System.Math.Max(best, H(p.X + s * dx, p.Y + s * dy));
                }
            }
        }

        // The interior critical point, if inside the polygon
        double det = hxx * hyy - hxy * hxy;
        if (det != 0.0)
        {
            double px = (-hx * hyy + hy * hxy) / det;
            double py = (-hy * hxx + hx * hxy) / det;
            if (Inside(polygon[..n], px, py))
            {
                best = System.Math.Max(best, H(px, py));
            }
        }

        return best / scale;
    }

    private static bool Inside(ReadOnlySpan<(double X, double Y)> polygon, double px, double py)
    {
        // A convex polygon, counter-clockwise (the rectangle's order, kept by the clip)
        for (int i = 0; i < polygon.Length; i++)
        {
            var p = polygon[i];
            var q = polygon[(i + 1) % polygon.Length];
            if ((q.X - p.X) * (py - p.Y) - (q.Y - p.Y) * (px - p.X) < 0.0)
            {
                return false;
            }
        }

        return true;
    }
}
