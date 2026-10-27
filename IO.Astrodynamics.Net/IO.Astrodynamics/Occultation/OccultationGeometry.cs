// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.Occultation;

/// <summary>The continuous margins of an occultation at an instant, each positive when its condition holds.</summary>
[Flags]
internal enum OccultationMargin
{
    None = 0,

    /// <summary>The apparent discs of the two bodies overlap: <see cref="OccultationGeometry.Overlap"/>.</summary>
    Overlap = 1,

    /// <summary>The disc of the back body lies within the disc of the front body: <see cref="OccultationGeometry.BackInside"/>.</summary>
    BackInside = 2,

    /// <summary>The disc of the front body lies within the disc of the back body: <see cref="OccultationGeometry.FrontInside"/>.</summary>
    FrontInside = 4
}

/// <summary>
/// The occultation of a back body by a front body as the observer sees them at an instant: three continuous margins,
/// whose zeros are the contacts of the apparent discs, and which body is in front.
/// </summary>
/// <remarks>
/// <para>
/// The method of CSPICE's <c>zzocced</c> (N0067, read from its source), turned into continuous functions so that a
/// root finder locates the contacts: a linear map A⁻¹ turns one ellipsoid, "the sphere", into the unit sphere. It maps
/// the observer to itself (the origin), rays from the observer to rays, and the limb of the other ellipsoid to the limb
/// of its image, so it keeps every incidence and containment of the apparent discs, though not the angles. In that
/// space the sphere's disc is the cap of angular radius α = asin(1/ρ) around u, the direction of its center at the
/// distance ρ, and the other body's disc is bounded by its limb, an ellipse. With s the angular separation of u from
/// that limb, signed negative when u passes through the plane region the limb bounds (the ray to the sphere's center
/// meets the other body), and d the largest separation of u from a point of the limb:
/// </para>
/// <list type="bullet">
/// <item>the discs overlap when s ≤ α: margin α − s;</item>
/// <item>the sphere's disc lies within the other's when s ≤ −α: margin −s − α;</item>
/// <item>the other's disc lies within the sphere's when d ≤ α: margin α − d.</item>
/// </list>
/// <para>
/// These are the tests 4, 5 and 8 of <c>zzocced</c>, written as differences; its bounding cones only skip work and are
/// not needed. The extremes of the separation over the limb are found as <c>zzasryel</c> does (samples of the ellipse,
/// then a one-dimensional search around the best), with Brent's <c>localmin</c> instead of a golden section. When the
/// sphere is a sphere (the Sun of <c>pck00011</c>), the map is a uniform scaling and the margins are true angles.
/// </para>
/// <para>
/// The sphere is the body of larger apparent size, as in <c>zzocced</c>, so that the limb is the smaller ellipse, but
/// the caller fixes the choice for a whole search: the margins change units with it, not their zeros.
/// </para>
/// <para>
/// A point body has no disc: a point back body is hidden when the ray to it meets the front body (margin α − θ, where θ
/// is the separation of the ray from the axis of the front body's cone, in the space of its unit sphere), a point front
/// body transits when the ray to it meets the back body, the condition that CSPICE's <c>zzgfocu</c> tests for the
/// shape <c>POINT</c>.
/// </para>
/// </remarks>
internal static class OccultationGeometry
{
    // Samples of the limb before the search of an extreme: zzasryel takes 320 or 400; 64 resolve a limb whose
    // separation from the ray has a single minimum and a single maximum (the ray outside the limb, Brent then refines),
    // and the samples are not the precision of the result
    private const int LimbSamples = 64;

    private static readonly double SampleSpacing = Constants._2PI / LimbSamples;
    private static readonly double[] SampleCos = BuildTable(System.Math.Cos);
    private static readonly double[] SampleSin = BuildTable(System.Math.Sin);

    // The absolute tolerance of Brent's localmin on the offset of θ from its best sample, rad; the method adds
    // √ε |offset| to it. The extreme is flat, so its value is exact to rounding well before
    private const double ThetaTolerance = 1e-12;

    private static double[] BuildTable(Func<double, double> function)
    {
        var table = new double[LimbSamples];
        for (int i = 0; i < LimbSamples; i++)
        {
            table[i] = function(i * Constants._2PI / LimbSamples);
        }

        return table;
    }

    /// <summary>
    /// Whether the front body is the one to map to the unit sphere: the body of larger apparent size (largest semi-axis
    /// over distance), as in <c>zzocced</c>, or the extended one when the other is a point.
    /// </summary>
    internal static bool SphereIsFront(in OccultationBody front, in OccultationBody back)
    {
        if (front.IsPoint)
        {
            return false;
        }

        if (back.IsPoint)
        {
            return true;
        }

        return front.MaxRadius / front.Center.Magnitude() >= back.MaxRadius / back.Center.Magnitude();
    }

    /// <summary>
    /// The margins and the order of the bodies at an instant.
    /// </summary>
    /// <param name="front">The front (occulting) body.</param>
    /// <param name="back">The back (occulted) body.</param>
    /// <param name="sphereIsFront">Whether the front body is mapped to the unit sphere; must be the extended body when
    /// the other is a point.</param>
    /// <param name="margins">The margins to compute; the others are NaN.</param>
    /// <param name="order">Whether to compute <see cref="OccultationState.FrontIsInFront"/>.</param>
    /// <exception cref="ArgumentException">Both bodies are points, or the sphere is a point.</exception>
    /// <exception cref="InvalidOperationException">The observer is inside a body.</exception>
    internal static OccultationState Evaluate(in OccultationBody front, in OccultationBody back, bool sphereIsFront,
        OccultationMargin margins, bool order)
    {
        if (front.IsPoint && back.IsPoint)
        {
            throw new ArgumentException("The front and back bodies cannot both be points.");
        }

        var sphere = sphereIsFront ? front : back;
        var other = sphereIsFront ? back : front;
        if (sphere.IsPoint)
        {
            throw new ArgumentException("The body mapped to the unit sphere must be extended.");
        }

        // The sphere: its center at the distance rho from the observer, its angular radius alpha around u
        var c = sphere.ToUnitSphere(sphere.Center);
        double rho = c.Magnitude();
        if (!(rho > 1.0))
        {
            throw new InvalidOperationException($"The observer is inside a body of the occultation, or on its surface (level {rho * rho}).");
        }

        double alpha = System.Math.Atan2(1.0, System.Math.Sqrt((rho - 1.0) * (rho + 1.0)));
        var u = c / rho;

        return other.IsPoint
            ? EvaluatePoint(sphere, other, sphereIsFront, u, alpha, order)
            : EvaluateEllipsoids(sphere, other, sphereIsFront, u, alpha, margins, order);
    }

    private static OccultationState EvaluatePoint(in OccultationBody sphere, in OccultationBody point, bool sphereIsFront,
        in Vector3 u, double alpha, bool order)
    {
        // The point is behind (or in front of) the sphere's disc when the ray to it is within alpha of u
        double margin = alpha - Separation(u, sphere.ToUnitSphere(point.Center));
        double overlap = margin;
        double backInside = sphereIsFront ? margin : double.NegativeInfinity;
        double frontInside = sphereIsFront ? double.NegativeInfinity : margin;

        bool frontFirst = false;
        if (order)
        {
            // Along the ray to the point: the sphere's surface before the point means the sphere is in front
            double pointDistance = point.Center.Magnitude();
            var direction = point.Center / pointDistance;
            double sphereDistance = EntryOrClosestApproach(sphere, direction);
            bool sphereFirst = sphereDistance < pointDistance;
            frontFirst = sphereIsFront ? sphereFirst : !sphereFirst;
        }

        return new OccultationState(overlap, backInside, frontInside, frontFirst);
    }

    private static OccultationState EvaluateEllipsoids(in OccultationBody sphere, in OccultationBody other,
        bool sphereIsFront, in Vector3 u, double alpha, OccultationMargin margins, bool order)
    {
        // The margins of the sphere inside the other's disc and of the overlap need s, that of the other inside the
        // sphere's disc needs d
        bool needSeparation = order || (margins & OccultationMargin.Overlap) != 0 ||
                              (margins & (sphereIsFront ? OccultationMargin.FrontInside : OccultationMargin.BackInside)) != 0;
        bool needMaximum = (margins & (sphereIsFront ? OccultationMargin.BackInside : OccultationMargin.FrontInside)) != 0;

        var limb = other.Limb();
        var mappedLimb = new LimbEllipse(sphere.ToUnitSphere(limb.Center), sphere.ToUnitSphere(limb.U), sphere.ToUnitSphere(limb.V));

        // Whether the ray to the sphere's center meets the other body: u is then inside the other's disc
        var towardSphere = sphere.Center.Normalize();
        double entryOther = other.RayEntry(towardSphere);
        bool inside = !double.IsNaN(entryOther);

        double signedSeparation = double.NaN;
        double minimumTheta = double.NaN;
        if (needSeparation)
        {
            double minimum = MinimumSeparation(u, mappedLimb, out minimumTheta);
            signedSeparation = inside ? -minimum : minimum;
        }

        double maximum = needMaximum ? MaximumSeparation(u, mappedLimb) : double.NaN;

        double overlap = (margins & OccultationMargin.Overlap) != 0 ? alpha - signedSeparation : double.NaN;
        double sphereInside = -signedSeparation - alpha;
        double otherInside = alpha - maximum;
        double backInside = (margins & OccultationMargin.BackInside) != 0 ? (sphereIsFront ? otherInside : sphereInside) : double.NaN;
        double frontInside = (margins & OccultationMargin.FrontInside) != 0 ? (sphereIsFront ? sphereInside : otherInside) : double.NaN;

        bool frontFirst = false;
        if (order)
        {
            // Along a ray that meets both bodies when their discs overlap: the ray to the sphere's center when it meets
            // the other body, otherwise the tangent ray through the limb point closest to u, which lies within alpha of u
            double sphereDistance;
            double otherDistance;
            if (inside)
            {
                sphereDistance = EntryOrClosestApproach(sphere, towardSphere);
                otherDistance = entryOther;
            }
            else
            {
                var limbPoint = limb.At(minimumTheta);
                otherDistance = limbPoint.Magnitude();
                sphereDistance = EntryOrClosestApproach(sphere, limbPoint / otherDistance);
            }

            bool sphereFirst = sphereDistance < otherDistance;
            frontFirst = sphereIsFront ? sphereFirst : !sphereFirst;
        }

        return new OccultationState(overlap, backInside, frontInside, frontFirst);
    }

    private static double EntryOrClosestApproach(in OccultationBody body, in Vector3 direction)
    {
        double entry = body.RayEntry(direction);
        return double.IsNaN(entry) ? body.RayClosestApproach(direction) : entry;
    }

    /// <summary>The angle between two vectors, in [0, π], accurate at every angle (atan2 of |a × b| and a · b).</summary>
    internal static double Separation(in Vector3 a, in Vector3 b)
    {
        return System.Math.Atan2(a.Cross(b).Magnitude(), a * b);
    }

    /// <summary>
    /// The smallest angle between the unit vector <paramref name="u"/> and the rays to the points of the ellipse.
    /// </summary>
    /// <param name="u">The ray, a unit vector.</param>
    /// <param name="ellipse">The ellipse, relative to the vertex of the ray.</param>
    /// <param name="theta">The parameter of the point of the ellipse where the smallest angle is reached.</param>
    internal static double MinimumSeparation(in Vector3 u, in LimbEllipse ellipse, out double theta)
    {
        return Extreme(u, ellipse, true, out theta);
    }

    /// <summary>The largest angle between the unit vector <paramref name="u"/> and the rays to the points of the ellipse.</summary>
    internal static double MaximumSeparation(in Vector3 u, in LimbEllipse ellipse)
    {
        return Extreme(u, ellipse, false, out _);
    }

    private static double Extreme(in Vector3 u, in LimbEllipse ellipse, bool minimum, out double theta)
    {
        // The proxy of zzasryel: the cosine of the angle, without the arc function, has its extremes where the angle has
        var center = ellipse.Center;
        var semiU = ellipse.U;
        var semiV = ellipse.V;
        int best = 0;
        double bestCosine = minimum ? double.NegativeInfinity : double.PositiveInfinity;
        for (int i = 0; i < LimbSamples; i++)
        {
            var point = center + semiU * SampleCos[i] + semiV * SampleSin[i];
            double cosine = (u * point) / point.Magnitude();
            if (minimum ? cosine > bestCosine : cosine < bestCosine)
            {
                bestCosine = cosine;
                best = i;
            }
        }

        // Brent's localmin on the offset from the best sample, within one sample on each side
        double theta0 = best * SampleSpacing;
        var uu = u;
        double sign = minimum ? 1.0 : -1.0;
        Func<double, double> angle = offset =>
        {
            double t = theta0 + offset;
            return sign * Separation(uu, center + semiU * System.Math.Cos(t) + semiV * System.Math.Sin(t));
        };
        double offset = Brent.Minimize(angle, -SampleSpacing, SampleSpacing, ThetaTolerance, out double value);

        // localmin never evaluates its ends nor, in general, its start: keep the sample if it is better
        double sampleValue = angle(0.0);
        if (sampleValue < value)
        {
            value = sampleValue;
            offset = 0.0;
        }

        theta = theta0 + offset;
        return sign * value;
    }
}

/// <summary>
/// The margins of an occultation at an instant (NaN when not computed; −∞ when the condition cannot hold, a point having
/// no disc) and whether the front body is the nearer one where the discs overlap.
/// </summary>
/// <param name="Overlap">Positive when the discs overlap.</param>
/// <param name="BackInside">Positive when the back body's disc lies within the front body's (a full occultation when
/// the front body is in front).</param>
/// <param name="FrontInside">Positive when the front body's disc lies within the back body's (an annular occultation
/// when the front body is in front).</param>
/// <param name="FrontIsInFront">Whether the front body is the nearer one; meaningful only where the discs overlap.</param>
internal readonly record struct OccultationState(double Overlap, double BackInside, double FrontInside, bool FrontIsInFront)
{
    /// <summary>The margin of a kind.</summary>
    internal double this[OccultationMargin margin] => margin switch
    {
        OccultationMargin.Overlap => Overlap,
        OccultationMargin.BackInside => BackInside,
        OccultationMargin.FrontInside => FrontInside,
        _ => throw new ArgumentOutOfRangeException(nameof(margin))
    };

    /// <summary>
    /// The type of occultation of the back body by the front body: <see cref="OccultationType.None"/> when the discs do
    /// not overlap or the back body is in front (a transit of it). Needs every margin and the order.
    /// </summary>
    internal OccultationType Type
    {
        get
        {
            if (!(Overlap >= 0.0) || !FrontIsInFront)
            {
                return OccultationType.None;
            }

            if (BackInside >= 0.0)
            {
                return OccultationType.Full;
            }

            return FrontInside >= 0.0 ? OccultationType.Annular : OccultationType.Partial;
        }
    }
}
