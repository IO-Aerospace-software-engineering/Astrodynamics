// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// The Gaussian of the encounter plane in its principal axes: the major and minor variances and the direction of the
/// major axis, (cos, sin) in the encounter-plane frame of the miss.
/// </summary>
/// <remarks>
/// The Pc integral takes the eigenvalues and the rotation as they are, so that a minor variance far below the major one
/// times ε, CARA's clip (1e-4 R)² included, is never lost by a rebuild of the matrix and a second decomposition (#612).
/// </remarks>
internal readonly struct EncounterPlaneGaussian
{
    internal EncounterPlaneGaussian(double majorVariance, double minorVariance, double cosAngle, double sinAngle)
    {
        MajorVariance = majorVariance;
        MinorVariance = minorVariance;
        CosAngle = cosAngle;
        SinAngle = sinAngle;
    }

    internal double MajorVariance { get; }
    internal double MinorVariance { get; }
    internal double CosAngle { get; }
    internal double SinAngle { get; }

    /// <summary>
    /// Decomposes a symmetric 2×2 covariance (its off-diagonal terms averaged): λ1 = (tr + √disc)/2, λ2 = det/λ1,
    /// θ = ½ atan2(2b, a − c); null when an element or an eigenvalue is not finite.
    /// </summary>
    internal static EncounterPlaneGaussian? FromCovariance(Matrix covariance)
    {
        if (covariance.Rows != 2 || covariance.Columns != 2)
        {
            return null;
        }

        double a = covariance.Get(0, 0);
        double b = 0.5 * (covariance.Get(0, 1) + covariance.Get(1, 0));
        double c = covariance.Get(1, 1);
        double trace = a + c;
        double sqrtDisc = System.Math.Sqrt(System.Math.Max((a - c) * (a - c) + 4.0 * b * b, 0.0));
        double lambda1 = 0.5 * (trace + sqrtDisc);
        // The minor eigenvalue as det / λ1, the determinant by Kahan's fused form, so that a λ2 below λ1·ε is not lost
        // to the cancellation of (tr − √disc)/2 (#612)
        double lambda2 = lambda1 > 0.0 ? KahanDeterminant(a, b, c) / lambda1 : 0.5 * (trace - sqrtDisc);
        if (!double.IsFinite(lambda1) || !double.IsFinite(lambda2))
        {
            return null;
        }

        double theta = 0.5 * System.Math.Atan2(2.0 * b, a - c);
        return new EncounterPlaneGaussian(lambda1, lambda2, System.Math.Cos(theta), System.Math.Sin(theta));
    }

    // ac − b², the rounding error of b² recovered by a fused multiply-add (Kahan): accurate to a few ulps of the result
    private static double KahanDeterminant(double a, double b, double c)
    {
        double bb = b * b;
        double error = System.Math.FusedMultiplyAdd(-b, b, bb);
        return System.Math.FusedMultiplyAdd(a, c, -bb) + error;
    }

    /// <summary>
    /// Raises both variances to at least <paramref name="floor"/>, CARA's eigenvalue clip.
    /// </summary>
    internal EncounterPlaneGaussian Clip(double floor, out bool clipped)
    {
        double major = System.Math.Max(MajorVariance, floor);
        double minor = System.Math.Max(MinorVariance, floor);
        clipped = major != MajorVariance || minor != MinorVariance;
        return new EncounterPlaneGaussian(major, minor, CosAngle, SinAngle);
    }

    /// <summary>
    /// The coordinates of an encounter-plane vector along the major and the minor axes.
    /// </summary>
    internal (double major, double minor) ToPrincipal(double x, double y)
    {
        return (x * CosAngle + y * SinAngle, -x * SinAngle + y * CosAngle);
    }

    /// <summary>
    /// The covariance R diag(λ1, λ2) Rᵀ, for the projected covariance a <see cref="CollisionRisk"/> reports.
    /// </summary>
    internal Matrix ToMatrix()
    {
        var rotation = new Matrix(2, 2);
        rotation.Set(0, 0, CosAngle);
        rotation.Set(0, 1, -SinAngle);
        rotation.Set(1, 0, SinAngle);
        rotation.Set(1, 1, CosAngle);
        var diagonal = new Matrix(2, 2);
        diagonal.Set(0, 0, MajorVariance);
        diagonal.Set(1, 1, MinorVariance);
        return rotation * diagonal * rotation.Transpose();
    }

    /// <summary>
    /// Frisbee's maximum-probability covariance with this covariance known (CARA's <c>FrisbeeMaxPc</c>): this covariance
    /// plus V_c ûûᵀ, û along the miss, V_c = m²(k² − 1)/k², k² the Mahalanobis distance of the miss; this covariance
    /// unchanged when k² ≤ 1 or the miss is zero; null when the miss is not finite or a variance is not positive.
    /// </summary>
    /// <remarks>
    /// Built in the principal axes of the known covariance, where its determinant λ1λ2 + V_c(λ1 u2² + λ2 u1²) is a sum
    /// of positive terms, so that the minor variance of the sum is λ1λ2-accurate (its determinant over the major one).
    /// </remarks>
    internal EncounterPlaneGaussian? WithFrisbeeCriticalCovariance(double missX, double missY)
    {
        if (!(MajorVariance > 0.0) || !(MinorVariance > 0.0))
        {
            return null;
        }

        var (x, y) = ToPrincipal(missX, missY);
        double missSquared = x * x + y * y;
        if (!double.IsFinite(missSquared))
        {
            return null;
        }

        if (missSquared <= 0.0)
        {
            return this;
        }

        double kSquared = x * x / MajorVariance + y * y / MinorVariance;
        if (!double.IsFinite(kSquared) || kSquared <= 1.0)
        {
            return this;
        }

        double criticalVariance = missSquared * (kSquared - 1.0) / kSquared;
        double miss = System.Math.Sqrt(missSquared);
        double u1 = x / miss;
        double u2 = y / miss;
        double a = MajorVariance + criticalVariance * u1 * u1;
        double c = MinorVariance + criticalVariance * u2 * u2;
        double b = criticalVariance * u1 * u2;
        double determinant = MajorVariance * MinorVariance + criticalVariance * (MajorVariance * u2 * u2 + MinorVariance * u1 * u1);
        double major = 0.5 * (a + c + System.Math.Sqrt((a - c) * (a - c) + 4.0 * b * b));
        double minor = determinant / major;
        double phi = 0.5 * System.Math.Atan2(2.0 * b, a - c);
        double cosPhi = System.Math.Cos(phi);
        double sinPhi = System.Math.Sin(phi);
        return new EncounterPlaneGaussian(
            major,
            minor,
            CosAngle * cosPhi - SinAngle * sinPhi,
            SinAngle * cosPhi + CosAngle * sinPhi);
    }
}

/// <summary>
/// The 2D probability of collision of CARA's <c>Pc2D</c> (Foster's integral): the integral of the Gaussian of the
/// encounter plane over the disc of the combined hard-body radius centred on the miss.
/// </summary>
/// <remarks>
/// <para>
/// In the principal axes, with x = x_c + R sin θ and the chord half-length h = R cos θ, the integral is
/// P = ∫ h φ(x; λ1) · ½[erf((|y_c| + h)/√(2λ2)) − erf((|y_c| − h)/√(2λ2))] dθ over θ ∈ [−π/2, π/2], smooth at the
/// edges of the disc. The θ range is split at the Gaussian's mean and its ±1, 2, 4, 8 and 16 σ in x, at the chords that
/// reach |y_c| + kσ in y, and at the disc's centre, so that the quadrature always samples the Gaussian inside the disc,
/// however narrow against R it is (#610); the parts of the disc beyond ±8σ are their own segments, so that nothing is
/// dropped. Each segment is integrated by adaptive Simpson under its share, by width, of a tolerance of 1e-12 of the whole
/// integral, halved at each level; a difference at the noise of the integrand, the rounding of x_c + R sin θ against σ and
/// the cancellation of the erfc difference when σ ≫ the chord, is accepted, and the evaluations are bounded (#611). Within
/// that noise, about ε σy/R relative, the Pc of a covariance far wider than the disc is as accurate as its double inputs
/// allow, not to 1e-12: 5.0e-11 for σ = 1e6 R with the miss 1 σ off the major axis (lot 6 of #457, PC-1).
/// </para>
/// <para>
/// The chord probability is an erfc difference on the side away from zero (CARA's <c>erf_vec_dif</c>), and erf where the
/// chord straddles the minor axis, with the erf and erfc of fdlibm, accurate to about one ulp (#611). The public
/// <see cref="SpecialFunctions.ErrorFunction"/> (Abramowitz and Stegun 7.1.26, 1.5e-7 absolute) is not used here.
/// </para>
/// </remarks>
internal static class EncounterPlaneProbability
{
    // Tolerance of the quadrature, relative to the whole integral
    private const double RelativeTolerance = 1.0e-12;

    // Depth cap of the adaptive Simpson recursion; the tolerance halves at each level
    private const int MaximumDepth = 40;

    private const double Epsilon = 2.220446049250313e-16;

    // Integrand evaluations after which every interval is accepted as it stands: a bound on the time of a call
    private const int EvaluationBudget = 200000;

    private static readonly double[] SigmaMultiples = { 0.0, 1.0, -1.0, 2.0, -2.0, 4.0, -4.0, 8.0, -8.0, 16.0, -16.0 };

    /// <summary>
    /// The integral of the Gaussian N(0, diag(λ1, λ2)) over the disc of radius <paramref name="radius"/> centred on
    /// (<paramref name="xCenter"/>, <paramref name="yCenter"/>), in the principal axes; 0 when the radius or a variance is
    /// not positive or an input is not finite. Clamped to [0, 1].
    /// </summary>
    internal static double Integrate(double xCenter, double yCenter, double majorVariance, double minorVariance, double radius)
    {
        if (!(radius > 0.0) || !(majorVariance > 0.0) || !(minorVariance > 0.0) || !double.IsFinite(radius) ||
            !double.IsFinite(majorVariance) || !double.IsFinite(minorVariance) || !double.IsFinite(xCenter) || !double.IsFinite(yCenter))
        {
            return 0.0;
        }

        double sigmaX = System.Math.Sqrt(majorVariance);
        double sigmaY = System.Math.Sqrt(minorVariance);
        double yAbs = System.Math.Abs(yCenter);
        double normalizer = 1.0 / (System.Math.Sqrt(2.0 * System.Math.PI) * sigmaX);
        double erfScale = 1.0 / (System.Math.Sqrt(2.0) * sigmaY);

        // The relative noise of the integrand: x = x_c + R sin θ and h = R cos θ carry an absolute rounding of
        // ε (|x_c| + R) and ε R, which the Gaussian turns into a relative error of up to 16 ε (|x_c| + R)/σx and the chord
        // probability into 16 ε (|y_c| + R)/σy within 16 σ of the mass; no refinement lowers a Simpson difference below it.
        // The cancellation of the erfc difference adds its own noise at each sample (ChordProbability)
        double noiseFloor = 64.0 * Epsilon + 16.0 * Epsilon * ((System.Math.Abs(xCenter) + radius) / sigmaX + (yAbs + radius) / sigmaY);
        int evaluations = 0;
        double sampleNoise = 0.0;

        double Integrand(double theta)
        {
            evaluations++;
            // θ in [−π/2, π/2]: the half chord is non-negative (cos(±π/2) is 6e-17 in double)
            double halfChord = radius * System.Math.Cos(theta);
            double x = xCenter + radius * System.Math.Sin(theta);
            double weight = System.Math.Exp(-(x * x) / (2.0 * majorVariance));
            if (weight == 0.0)
            {
                sampleNoise = 0.0;
                return 0.0;
            }

            return halfChord * normalizer * weight *
                   ChordProbability((yAbs - halfChord) * erfScale, (yAbs + halfChord) * erfScale, out sampleNoise);
        }

        var breakpoints = Breakpoints(xCenter, yAbs, sigmaX, sigmaY, radius);
        int segments = breakpoints.Count - 1;
        var midpoints = new double[segments];
        var values = new double[2 * segments + 1];
        var wholes = new double[segments];
        values[0] = Integrand(breakpoints[0]);
        double coarse = 0.0;
        for (int i = 0; i < segments; i++)
        {
            midpoints[i] = 0.5 * (breakpoints[i] + breakpoints[i + 1]);
            values[2 * i + 1] = Integrand(midpoints[i]);
            values[2 * i + 2] = Integrand(breakpoints[i + 1]);
            wholes[i] = SimpsonEstimate(breakpoints[i], breakpoints[i + 1], values[2 * i], values[2 * i + 1], values[2 * i + 2]);
            coarse += wholes[i];
        }

        if (!(coarse > 0.0))
        {
            return 0.0;
        }

        // The tolerance shared out over the segments by their width, so that the errors add to at most 1e-12 of the whole
        double tolerancePerRadian = RelativeTolerance * coarse / System.Math.PI;
        double integral = 0.0;

        double AdaptiveSimpson(double lower, double upper, double tolerance, double whole, double fLower, double fMid, double fUpper, int depth)
        {
            double midpoint = 0.5 * (lower + upper);
            double fLeftMid = Integrand(0.5 * (lower + midpoint));
            double leftMidNoise = sampleNoise;
            double fRightMid = Integrand(0.5 * (midpoint + upper));
            // The noise of the erfc difference at the interval's new samples, an upper bound of its four roundings
            double noise = noiseFloor + System.Math.Max(leftMidNoise, sampleNoise);
            double left = SimpsonEstimate(lower, midpoint, fLower, fLeftMid, fMid);
            double right = SimpsonEstimate(midpoint, upper, fMid, fRightMid, fUpper);
            double delta = left + right - whole;
            if (depth <= 0 || evaluations >= EvaluationBudget || System.Math.Abs(delta) <= 15.0 * tolerance ||
                System.Math.Abs(delta) <= noise * System.Math.Abs(left + right))
            {
                return left + right + delta / 15.0;
            }

            return AdaptiveSimpson(lower, midpoint, 0.5 * tolerance, left, fLower, fLeftMid, fMid, depth - 1)
                   + AdaptiveSimpson(midpoint, upper, 0.5 * tolerance, right, fMid, fRightMid, fUpper, depth - 1);
        }

        for (int i = 0; i < segments; i++)
        {
            double tolerance = tolerancePerRadian * (breakpoints[i + 1] - breakpoints[i]);
            integral += AdaptiveSimpson(breakpoints[i], breakpoints[i + 1], tolerance, wholes[i],
                values[2 * i], values[2 * i + 1], values[2 * i + 2], MaximumDepth);
        }

        return System.Math.Clamp(integral, 0.0, 1.0);
    }

    /// <summary>
    /// ½[erf(<paramref name="upper"/>) − erf(<paramref name="lower"/>)] for <paramref name="upper"/> ≥ |<paramref name="lower"/>|:
    /// an erfc difference when both are non-negative, a sum of erf otherwise, so that no difference of values near 1 is taken.
    /// </summary>
    /// <param name="lower">The lower end of the chord, scaled by 1/(√2 σy).</param>
    /// <param name="upper">The upper end of the chord, scaled by 1/(√2 σy).</param>
    /// <param name="relativeNoise">
    /// The relative noise of the erfc difference, 0 for the sum of erf (the noise floor of the integral covers it).
    /// </param>
    /// <remarks>
    /// When σy ≫ the chord, erfc(lo) − erfc(up) cancels: each erfc carries about one ulp, and the rounding of lo and up,
    /// about ε|x| each, moves erfc(x) by 2|x|e^(−x²)/√π · ε, at most 2(1 + x²) erfc(x) · ε for x ≥ 0. Their sum over the
    /// difference is the relative noise of the chord probability, about ε σy/h (lot 6 of #457, PC-1): without it, the
    /// recursion chases that noise to its evaluation budget.
    /// </remarks>
    private static double ChordProbability(double lower, double upper, out double relativeNoise)
    {
        if (lower >= 0.0)
        {
            double erfcLower = Erfc(lower);
            double erfcUpper = Erfc(upper);
            double difference = erfcLower - erfcUpper;
            relativeNoise = difference > 0.0
                ? Epsilon * (erfcLower * (3.0 + 2.0 * lower * lower) + erfcUpper * (3.0 + 2.0 * upper * upper)) / difference
                : 0.0;
            return 0.5 * difference;
        }

        relativeNoise = 0.0;
        return 0.5 * (Erf(upper) + Erf(-lower));
    }

    private static List<double> Breakpoints(double xCenter, double yAbs, double sigmaX, double sigmaY, double radius)
    {
        var points = new List<double> { -0.5 * System.Math.PI, 0.0, 0.5 * System.Math.PI };
        foreach (double k in SigmaMultiples)
        {
            double u = (k * sigmaX - xCenter) / radius;
            if (u > -1.0 && u < 1.0)
            {
                points.Add(System.Math.Asin(u));
            }

            double reach = (yAbs + k * sigmaY) / radius;
            if (reach > 0.0 && reach < 1.0)
            {
                double theta = System.Math.Acos(reach);
                points.Add(theta);
                points.Add(-theta);
            }
        }

        points.Sort();
        var distinct = new List<double>(points.Count) { points[0] };
        for (int i = 1; i < points.Count; i++)
        {
            if (points[i] > distinct[^1])
            {
                distinct.Add(points[i]);
            }
        }

        return distinct;
    }

    private static double SimpsonEstimate(double lower, double upper, double fLower, double fMid, double fUpper)
    {
        return (upper - lower) * (fLower + 4.0 * fMid + fUpper) / 6.0;
    }

    // fdlibm s_erf.c 1.3 95/01/18, for x >= 0 (the callers pass |x|), ported to C#:
    // ====================================================
    // Copyright (C) 1993 by Sun Microsystems, Inc. All rights reserved.
    //
    // Developed at SunSoft, a Sun Microsystems, Inc. business.
    // Permission to use, copy, modify, and distribute this
    // software is freely granted, provided that this notice
    // is preserved.
    // ====================================================
    private const double Erx = 8.45062911510467529297e-01;
    private const double Efx = 1.28379167095512586316e-01;
    private const double Pp0 = 1.28379167095512558561e-01;
    private const double Pp1 = -3.25042107247001499370e-01;
    private const double Pp2 = -2.84817495755985104766e-02;
    private const double Pp3 = -5.77027029648944159157e-03;
    private const double Pp4 = -2.37630166566501626084e-05;
    private const double Qq1 = 3.97917223959155352819e-01;
    private const double Qq2 = 6.50222499887672944485e-02;
    private const double Qq3 = 5.08130628187576562776e-03;
    private const double Qq4 = 1.32494738004321644526e-04;
    private const double Qq5 = -3.96022827877536812320e-06;
    private const double Pa0 = -2.36211856075265944077e-03;
    private const double Pa1 = 4.14856118683748331666e-01;
    private const double Pa2 = -3.72207876035701323847e-01;
    private const double Pa3 = 3.18346619901161753674e-01;
    private const double Pa4 = -1.10894694282396677476e-01;
    private const double Pa5 = 3.54783043256182359371e-02;
    private const double Pa6 = -2.16637559486879084300e-03;
    private const double Qa1 = 1.06420880400844228286e-01;
    private const double Qa2 = 5.40397917702171048937e-01;
    private const double Qa3 = 7.18286544141962662868e-02;
    private const double Qa4 = 1.26171219808761642112e-01;
    private const double Qa5 = 1.36370839120290507362e-02;
    private const double Qa6 = 1.19844998467991074170e-02;
    private const double Ra0 = -9.86494403484714822705e-03;
    private const double Ra1 = -6.93858572707181764372e-01;
    private const double Ra2 = -1.05586262253232909814e+01;
    private const double Ra3 = -6.23753324503260060396e+01;
    private const double Ra4 = -1.62396669462573470355e+02;
    private const double Ra5 = -1.84605092906711035994e+02;
    private const double Ra6 = -8.12874355063065934246e+01;
    private const double Ra7 = -9.81432934416914548592e+00;
    private const double Sa1 = 1.96512716674392571292e+01;
    private const double Sa2 = 1.37657754143519042600e+02;
    private const double Sa3 = 4.34565877475229228821e+02;
    private const double Sa4 = 6.45387271733267880336e+02;
    private const double Sa5 = 4.29008140027567833386e+02;
    private const double Sa6 = 1.08635005541779435134e+02;
    private const double Sa7 = 6.57024977031928170135e+00;
    private const double Sa8 = -6.04244152148580987438e-02;
    private const double Rb0 = -9.86494292470009928597e-03;
    private const double Rb1 = -7.99283237680523006574e-01;
    private const double Rb2 = -1.77579549177547519889e+01;
    private const double Rb3 = -1.60636384855821916062e+02;
    private const double Rb4 = -6.37566443368389627722e+02;
    private const double Rb5 = -1.02509513161107724954e+03;
    private const double Rb6 = -4.83519191608651397019e+02;
    private const double Sb1 = 3.03380607434824582924e+01;
    private const double Sb2 = 3.25792512996573918826e+02;
    private const double Sb3 = 1.53672958608443695994e+03;
    private const double Sb4 = 3.19985821950859553908e+03;
    private const double Sb5 = 2.55305040643316442583e+03;
    private const double Sb6 = 4.74528541206955367215e+02;
    private const double Sb7 = -2.24409524465858183362e+01;

    /// <summary>
    /// erf(x) for x ≥ 0, fdlibm's, accurate to about one ulp.
    /// </summary>
    internal static double Erf(double x)
    {
        if (x < 0.84375)
        {
            if (x < 3.7252902984e-09)
            {
                return x + Efx * x;
            }

            return x + x * SmallArgumentRatio(x * x);
        }

        if (x < 1.25)
        {
            return Erx + NearOneRatio(x - 1.0);
        }

        if (x >= 6.0)
        {
            return 1.0;
        }

        return 1.0 - LargeArgumentErfc(x);
    }

    /// <summary>
    /// erfc(x) for x ≥ 0, fdlibm's, accurate to about one ulp; 0 from x = 28 on.
    /// </summary>
    internal static double Erfc(double x)
    {
        if (x < 0.84375)
        {
            if (x < 1.3877787807814457e-17)
            {
                return 1.0 - x;
            }

            double y = SmallArgumentRatio(x * x);
            if (x < 0.25)
            {
                return 1.0 - (x + x * y);
            }

            return 0.5 - (x * y + (x - 0.5));
        }

        if (x < 1.25)
        {
            return 1.0 - Erx - NearOneRatio(x - 1.0);
        }

        if (x < 28.0)
        {
            return LargeArgumentErfc(x);
        }

        return 0.0;
    }

    // (erf(x) − x)/x on [0, 0.84375], z = x²
    private static double SmallArgumentRatio(double z)
    {
        double r = Pp0 + z * (Pp1 + z * (Pp2 + z * (Pp3 + z * Pp4)));
        double s = 1.0 + z * (Qq1 + z * (Qq2 + z * (Qq3 + z * (Qq4 + z * Qq5))));
        return r / s;
    }

    // erf(1 + s) − erx on [0.84375, 1.25]
    private static double NearOneRatio(double s)
    {
        double p = Pa0 + s * (Pa1 + s * (Pa2 + s * (Pa3 + s * (Pa4 + s * (Pa5 + s * Pa6)))));
        double q = 1.0 + s * (Qa1 + s * (Qa2 + s * (Qa3 + s * (Qa4 + s * (Qa5 + s * Qa6)))));
        return p / q;
    }

    // erfc(x) on [1.25, 28): exp(−x² − 0.5625 + R/S)/x, −x² split as −z² + (z − x)(z + x), z = x with its low word cleared
    private static double LargeArgumentErfc(double x)
    {
        double s = 1.0 / (x * x);
        double r;
        double q;
        if (x < 1.0 / 0.35)
        {
            r = Ra0 + s * (Ra1 + s * (Ra2 + s * (Ra3 + s * (Ra4 + s * (Ra5 + s * (Ra6 + s * Ra7))))));
            q = 1.0 + s * (Sa1 + s * (Sa2 + s * (Sa3 + s * (Sa4 + s * (Sa5 + s * (Sa6 + s * (Sa7 + s * Sa8)))))));
        }
        else
        {
            r = Rb0 + s * (Rb1 + s * (Rb2 + s * (Rb3 + s * (Rb4 + s * (Rb5 + s * Rb6)))));
            q = 1.0 + s * (Sb1 + s * (Sb2 + s * (Sb3 + s * (Sb4 + s * (Sb5 + s * (Sb6 + s * Sb7))))));
        }

        double z = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(x) & unchecked((long)0xFFFFFFFF00000000UL));
        return System.Math.Exp(-z * z - 0.5625) * System.Math.Exp((z - x) * (z + x) + r / q) / x;
    }
}
