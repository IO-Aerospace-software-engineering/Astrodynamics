// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Body;

/// <summary>
/// The regime of the shadow of an occulting disc on the disc of a light source, from their angles.
/// </summary>
internal enum ShadowRegime
{
    /// <summary>The discs do not overlap: full illumination.</summary>
    None,

    /// <summary>The discs overlap partially: the penumbra.</summary>
    Partial,

    /// <summary>The occulting disc lies inside the source disc: an annular eclipse.</summary>
    Annular,

    /// <summary>The occulting disc covers the source disc: a total eclipse.</summary>
    Total
}

/// <summary>
/// The overlap of the disc of a light source of angular radius a and of an occulting disc of angular radius b whose
/// centres are c apart, in a partial eclipse (|a − b| &lt; c &lt; a + b): the geometry shared by the shadow fraction
/// (<see cref="CelestialItem.ShadowFraction(double, double, double)"/>) and by its derivative in the SRP partials, so
/// that the partials are the derivative of the shadow fraction they scale (#434).
/// </summary>
/// <remarks>
/// <para>
/// The overlap is the sum of the two circular segments cut by the common chord, A = a² (α − sin α cos α) +
/// b² (β − sin β cos β), α and β the half angles the chord subtends at the centres of the two discs: E. W. Weisstein,
/// "Circle-Circle Intersection", MathWorld, eqs. 10 and 13 (read on 2026-10-22); the same area as eq. 3.150 of the GMAT
/// Mathematical Specification, R2026a, §3.2.28, and as the occulted area of Montenbruck and Gill, Satellite Orbits,
/// §3.4.2 (confirmed by S. Guillet, 2026-10-24). The closed form of these references, a² acos(c₁/a) + b² acos((c − c₁)/b) − c c₂,
/// cancels near the contacts of the penumbra: its terms, of order b² a in LEO, cancel down to the covered part of the
/// solar disc, and its acos take arguments within rounding of 1. Each segment here is r² (u − sin u) / 2 with u = 2θ,
/// non-negative, so the sum cannot cancel.
/// </para>
/// <para>
/// The half chord c₂ = 2Δ / c comes from the area Δ of the triangle of the two centres and an intersection point,
/// whose sides are a, b and c, by Heron's product written as by W. Kahan, "Miscalculating Area and Angles of a
/// Needle-like Triangle" (2014), §2 (read on 2026-10-22): the sides sorted x ≥ y ≥ z,
/// 16Δ² = (x + (y + z)) (z − (x − y)) (z + (x − y)) (x + (y − z)). The factor that vanishes at a contact then subtracts
/// its two nearly equal sides first, exactly, and the product is not negative for the sides of a triangle. The half
/// angles come from atan2, α = atan2(c₂, c₁) and β = atan2(c₂, c − c₁), c₁ = ((c − b)(c + b) + a²) / (2c) (GMAT eq.
/// 3.151), each in [0, π].
/// </para>
/// </remarks>
internal readonly struct DiscOverlap
{
    private readonly double _a;
    private readonly double _b;

    /// <summary>
    /// The overlap of the discs of angular radii <paramref name="a"/> (the light source) and <paramref name="b"/> (the
    /// occulting body) whose centres are <paramref name="c"/> apart, in rad, in the regime
    /// <see cref="ShadowRegime.Partial"/>.
    /// </summary>
    internal DiscOverlap(double a, double b, double c)
    {
        _a = a;
        _b = b;

        // Kahan's sorted sides x >= y >= z
        double x = a;
        double y = b;
        double z = c;
        if (x < y)
        {
            (x, y) = (y, x);
        }

        if (y < z)
        {
            (y, z) = (z, y);
        }

        if (x < y)
        {
            (x, y) = (y, x);
        }

        double heron = (x + (y + z)) * (z - (x - y)) * (z + (x - y)) * (x + (y - z));
        HalfChord = System.Math.Sqrt(System.Math.Max(heron, 0.0)) / (2.0 * c);
        double c1 = ((c - b) * (c + b) + a * a) / (2.0 * c);
        Alpha = System.Math.Atan2(HalfChord, c1);
        Beta = System.Math.Atan2(HalfChord, c - c1);
    }

    /// <summary>The half chord c₂ of the two circles, in rad.</summary>
    internal double HalfChord { get; }

    /// <summary>The half angle α subtended by the chord at the centre of the source disc, in [0, π].</summary>
    internal double Alpha { get; }

    /// <summary>The half angle β subtended by the chord at the centre of the occulting disc, in [0, π].</summary>
    internal double Beta { get; }

    /// <summary>
    /// The regime of the shadow from the angles, sunlit when c ≥ a + b, total when c ≤ b − a, annular when c ≤ a − b
    /// (GMAT Mathematical Specification, R2026a, eqs. 3.148 and 3.149), the penumbra otherwise: the comparisons of
    /// <see cref="CelestialItem.ShadowFraction(double, double, double)"/> before #434, unchanged.
    /// </summary>
    internal static ShadowRegime Regime(double a, double b, double c)
    {
        if (c >= a + b)
        {
            return ShadowRegime.None;
        }

        if (c <= b - a && b >= a)
        {
            return ShadowRegime.Total;
        }

        if (c <= a - b && b < a)
        {
            return ShadowRegime.Annular;
        }

        return ShadowRegime.Partial;
    }

    /// <summary>
    /// The covered fraction of the source disc, A / (π a²) = (S(2α) + (b / a)² S(2β)) / (2π), S(u) = u − sin u, clamped
    /// to [0, 1]: the clamp removes only the rounding of 1 near the inner contact.
    /// </summary>
    internal double Fraction
    {
        get
        {
            double ratio = _b / _a;
            double fraction = (UMinusSinU(2.0 * Alpha) + ratio * ratio * UMinusSinU(2.0 * Beta)) / (2.0 * System.Math.PI);
            return System.Math.Clamp(fraction, 0.0, 1.0);
        }
    }

    /// <summary>
    /// The partial derivatives of the area A of the overlap: ∂A/∂c = −2 c₂, ∂A/∂a = 2 a α, ∂A/∂b = 2 b β, the reduced form
    /// of eq. 4.100 of the GMAT Mathematical Specification, R2026a, §4.2.5 (confirmed by S. Guillet, 2026-10-24; sympy check in
    /// <c>tools/shadow_function/area_derivatives.py</c>), in rad.
    /// </summary>
    internal (double DAdc, double DAda, double DAdb) AreaDerivatives =>
        (-2.0 * HalfChord, 2.0 * _a * Alpha, 2.0 * _b * Beta);

    // u − sin u, by its series below u = 0.1, where the difference would cancel: at u = 0.1 the first term left out is 5e-33
    // of the first; above it the direct difference keeps a relative error of about 6ε / u², at most 1.3e-13
    private static double UMinusSinU(double u)
    {
        if (u >= 0.1)
        {
            return u - System.Math.Sin(u);
        }

        double u2 = u * u;
        double term = u * u2 / 6.0;
        double sum = 0.0;
        for (int k = 1; k <= 8; k++)
        {
            sum += term;
            term *= -u2 / ((2.0 * k + 2.0) * (2.0 * k + 3.0));
        }

        return sum;
    }
}
