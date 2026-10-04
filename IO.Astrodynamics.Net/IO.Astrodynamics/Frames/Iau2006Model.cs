// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.Frames;

/// <summary>
/// IAU 2006 precession with IAU 2000B nutation, CIO based.
/// Provides the CIO-based chain GCRS → CIRS → TIRS used by <see cref="GcrfFrame"/>,
/// <see cref="CirsFrame"/> and <see cref="TirsFrame"/>.
///
/// Uses IAU 2006 precession (Fukushima-Williams angles) with IAU 2000B nutation (77 luni-solar terms).
/// Accuracy: ~1 mas for nutation, sub-µas for precession and frame bias. Against SOFA (IAU 2006/2000A)
/// from 1990 to 2040, CIP X and Y agree within 3.6e-9 rad (0.73 mas) and the GCRS to TIRS matrix within
/// 4.3e-9 rad (Iau2006SofaSweepTests).
/// </summary>
/// <remarks>
/// The chain stops at TIRS. The terrestrial step TIRS → ITRS (polar motion) is not part of any frame yet:
/// <see cref="PolarMotionMatrix"/> and <see cref="TioLocator"/> are validated against SOFA but used by no frame.
/// </remarks>
public static class Iau2006Model
{
    private const double DAS2R = System.Math.PI / (180.0 * 3600.0);
    private const double TwoPi = 2.0 * System.Math.PI;

    // Mean obliquity at J2000.0 (arcseconds) — used for reference
    private const double EPS0_ARCSEC = 84381.406;
    private static readonly double EPS0 = EPS0_ARCSEC * DAS2R;

    // Cached frame bias matrix (NPB at t=0 with dpsi=deps=0)
    private static readonly Matrix _frameBias = ComputeFrameBias();

    /// <summary>
    /// Returns the constant frame bias matrix B (GCRS → mean equator/equinox J2000).
    /// This represents the ~23 mas rotation between ICRS and the dynamical mean frame.
    /// </summary>
    public static Matrix FrameBias() => _frameBias;

    private static Matrix ComputeFrameBias()
    {
        // Frame bias is the F-W matrix at t=0 with no nutation
        var (gamb, phib, psib, epsa) = FukushimaWilliamsAngles(0.0);
        return FukushimaWilliamsMatrix(gamb, phib, psib, epsa);
    }

    /// <summary>
    /// Computes the Fukushima-Williams precession angles in radians.
    /// These include the frame bias in their constant terms.
    /// From SOFA iauPfw06.
    /// </summary>
    public static (double gamb, double phib, double psib, double epsa) FukushimaWilliamsAngles(double t)
    {
        double gamb = ((-0.052928
            + (10.556378
            + (-0.4932044
            + (-0.00031238
            + (-0.000002788
            + 0.0000000260
            * t) * t) * t) * t) * t)) * DAS2R;

        double phib = (84381.412819
            + (-46.811016
            + (0.0511268
            + (0.00053289
            + (-0.000000440
            - 0.0000000176
            * t) * t) * t) * t) * t) * DAS2R;

        double psib = (-0.041775
            + (5038.481484
            + (1.5584175
            + (-0.00018522
            + (-0.000026452
            - 0.0000000148
            * t) * t) * t) * t) * t) * DAS2R;

        double epsa = (84381.406
            + (-46.836769
            + (-0.0001831
            + (0.00200340
            + (-0.000000576
            - 0.0000000434
            * t) * t) * t) * t) * t) * DAS2R;

        return (gamb, phib, psib, epsa);
    }

    /// <summary>
    /// Builds a rotation matrix from Fukushima-Williams angles.
    /// M = R1(-eps) * R3(-psi) * R1(phi) * R3(gam)
    /// From SOFA iauFw2m.
    /// </summary>
    public static Matrix FukushimaWilliamsMatrix(double gamb, double phib, double psi, double eps)
    {
        // SOFA iauFw2m left-accumulates: Rz(gamb), Rx(phib), Rz(-psi), Rx(-eps)
        // Product: M_sofa = iauRx(-eps) * iauRz(-psi) * iauRx(phib) * iauRz(gamb)
        // SOFA rotation matrices are transposed relative to community: iauR(a) = comm_R(-a)
        // So negate all angles. Outermost (last SOFA op) is leftmost in chain.
        return Matrix.CreateRotationMatrixX(eps)
            .Multiply(Matrix.CreateRotationMatrixZ(psi))
            .Multiply(Matrix.CreateRotationMatrixX(-phib))
            .Multiply(Matrix.CreateRotationMatrixZ(-gamb));
    }

    /// <summary>
    /// Computes IAU 2006 precession angles (Lieske parameterization) in radians.
    /// Returns (psiA, omegaA, epsilonA, chiA).
    /// </summary>
    public static (double psiA, double omegaA, double epsilonA, double chiA) PrecessionAngles(double t)
    {
        double psiA = (((((-0.0000000951 * t
            + 0.000132851) * t
            - 0.00114045) * t
            - 1.0790069) * t
            + 5038.481507) * t) * DAS2R;

        double omegaA = (((((0.0000003337 * t
            - 0.000000467) * t
            - 0.00772503) * t
            + 0.0512623) * t
            - 0.025754) * t) * DAS2R + EPS0;

        double epsilonA = (((((-0.0000000434 * t
            - 0.000000576) * t
            + 0.00200340) * t
            - 0.0001831) * t
            - 46.836769) * t) * DAS2R + EPS0;

        double chiA = (((((- 0.0000000560 * t
            + 0.000170663) * t
            - 0.00121197) * t
            - 2.3814292) * t
            + 10.556403) * t) * DAS2R;

        return (psiA, omegaA, epsilonA, chiA);
    }

    /// <summary>
    /// Computes IAU 2000B nutation (dpsi, deps) in radians, with the IAU 2006 (P03) adjustments.
    /// Uses 77 luni-solar terms plus the fixed planetary offsets, evaluated with the linear Delaunay
    /// arguments of Simon et al. (1994) that define IAU 2000B, as in SOFA <c>iauNut00b</c>.
    /// The P03 adjustments are those of SOFA <c>iauNut06a</c>, including the secular J2 factor
    /// <c>-2.7774e-6 t</c>.
    /// </summary>
    /// <param name="t">Julian centuries TT since J2000.0.</param>
    public static (double dpsi, double deps) Nutation(double t)
    {
        // IAU 2000B is defined with linear arguments (Simon et al. 1994), not with the full IERS 2003
        // polynomials of Iau2006FundamentalArguments. The two differ by up to ~1.6e-10 rad of nutation
        // by 2024; using the defining arguments keeps the model identical to SOFA iauNut00b.
        double l = SimonArgument(485868.249036, 1717915923.2178, t);
        double lp = SimonArgument(1287104.79305, 129596581.0481, t);
        double f = SimonArgument(335779.526232, 1739527262.8478, t);
        double d = SimonArgument(1072260.70369, 1602961601.2090, t);
        double om = SimonArgument(450160.398036, -6962890.5431, t);

        double dpsi = 0.0;
        double deps = 0.0;

        for (int i = Iau2006NutationData.LuniSolarTerms.Length - 1; i >= 0; i--)
        {
            var term = Iau2006NutationData.LuniSolarTerms[i];
            double arg = (term.nl * l + term.nlp * lp + term.nf * f + term.nd * d + term.nom * om) % TwoPi;
            double sinArg = System.Math.Sin(arg);
            double cosArg = System.Math.Cos(arg);

            dpsi += (term.sp + term.spt * t) * sinArg + term.cp * cosArg;
            deps += (term.ce + term.cet * t) * cosArg + term.se * sinArg;
        }

        // Convert from 0.1 µas to radians and add planetary bias
        dpsi = dpsi * Iau2006NutationData.U + Iau2006NutationData.DpsiPlanetaryBias * DAS2R * 1e-6;
        deps = deps * Iau2006NutationData.U + Iau2006NutationData.DepsPlanetaryBias * DAS2R * 1e-6;

        // Apply IAU 2006 corrections to IAU 2000 nutation (P03 compatibility, from SOFA iauNut06a).
        // fj2 corrects for the secular variation of J2 and grows linearly with time.
        double fj2 = -2.7774e-6 * t;
        dpsi += dpsi * (0.4697e-6 + fj2);
        deps += deps * fj2;

        return (dpsi, deps);
    }

    /// <summary>
    /// Linear Delaunay argument of Simon et al. (1994), as used by IAU 2000B, in radians.
    /// </summary>
    /// <param name="constantArcsec">Value at J2000.0, in arcseconds.</param>
    /// <param name="rateArcsecPerCentury">Rate, in arcseconds per Julian century.</param>
    /// <param name="t">Julian centuries TT since J2000.0.</param>
    private static double SimonArgument(double constantArcsec, double rateArcsecPerCentury, double t)
    {
        const double turnArcsec = 1296000.0;
        return (constantArcsec + rateArcsecPerCentury * t) % turnArcsec * DAS2R;
    }

    /// <summary>
    /// Computes the CIP coordinates X, Y in radians from the NPB matrix.
    /// The NPB matrix includes the frame bias (via Fukushima-Williams angles).
    /// </summary>
    public static (double X, double Y) CipXY(double t)
    {
        var npb = NpbMatrix(t);

        // CIP coordinates: X = NPB[2][0], Y = NPB[2][1]
        return (npb.Get(2, 0), npb.Get(2, 1));
    }

    /// <summary>
    /// Builds the complete bias-precession-nutation matrix (GCRS to true equator/equinox).
    /// Uses Fukushima-Williams angles (which include the frame bias).
    /// NPB = R1(-(epsa+deps)) * R3(-(psib+dpsi)) * R1(phib) * R3(gamb)
    /// Following SOFA iauPn06a → iauPn06 → iauFw2m.
    /// </summary>
    internal static Matrix NpbMatrix(double t)
    {
        var (gamb, phib, psib, epsa) = FukushimaWilliamsAngles(t);
        var (dpsi, deps) = Nutation(t);

        return FukushimaWilliamsMatrix(gamb, phib, psib + dpsi, epsa + deps);
    }

    /// <summary>
    /// Computes the CIO locator s in radians, given the CIP coordinates X, Y.
    /// Evaluates the full IAU 2006 series of <c>s + XY/2</c> (IERS Conventions 2010, Table 5.2d), as
    /// SOFA <c>iauS06</c> does: a polynomial plus 66 periodic terms, the largest being 2640.73 µas in sin Ω.
    /// </summary>
    /// <param name="t">Julian centuries TT since J2000.0.</param>
    /// <param name="x">CIP X coordinate, in radians.</param>
    /// <param name="y">CIP Y coordinate, in radians.</param>
    public static double CioLocator(double t, double x, double y)
    {
        // Fundamental arguments from the IERS Conventions 2003, in the order of the series multipliers.
        Span<double> fa = stackalloc double[8];
        fa[0] = Iau2006FundamentalArguments.MoonMeanAnomaly(t);
        fa[1] = Iau2006FundamentalArguments.SunMeanAnomaly(t);
        fa[2] = Iau2006FundamentalArguments.MoonMeanArgumentOfLatitude(t);
        fa[3] = Iau2006FundamentalArguments.MeanElongation(t);
        fa[4] = Iau2006FundamentalArguments.MoonAscendingNodeLongitude(t);
        fa[5] = Iau2006FundamentalArguments.LambdaVenus(t);
        fa[6] = Iau2006FundamentalArguments.LambdaEarth(t);
        fa[7] = Iau2006FundamentalArguments.GeneralPrecession(t);

        // w[k] is the coefficient of t^k, in microarcseconds: polynomial part plus periodic terms.
        Span<double> w = stackalloc double[6];
        Iau2006CioLocatorData.Polynomial.CopyTo(w);
        for (int k = 0; k < Iau2006CioLocatorData.Series.Length; k++)
        {
            var terms = Iau2006CioLocatorData.Series[k];
            // Smallest terms first, as SOFA does, to limit rounding.
            for (int i = terms.Length - 1; i >= 0; i--)
            {
                var term = terms[i];
                double a = term.l * fa[0] + term.lp * fa[1] + term.f * fa[2] + term.d * fa[3]
                           + term.om * fa[4] + term.lve * fa[5] + term.le * fa[6] + term.pa * fa[7];
                w[k] += term.s * System.Math.Sin(a) + term.c * System.Math.Cos(a);
            }
        }

        double sPlusHalfXy = w[0] + (w[1] + (w[2] + (w[3] + (w[4] + w[5] * t) * t) * t) * t) * t;
        return sPlusHalfXy * DAS2R * 1e-6 - x * y / 2.0;
    }

    /// <summary>
    /// Builds the CIO-based precession-nutation matrix Q(t) from CIP coordinates X, Y
    /// and CIO locator s. Transforms from GCRS (the ICRF axes) to CIRS.
    /// IERS Conventions 2010, Eq. 5.1.
    /// </summary>
    public static Matrix PrecessionNutationMatrix(double x, double y, double s)
    {
        double r2 = x * x + y * y;
        double e = (r2 > 0.0) ? System.Math.Atan2(y, x) : 0.0;
        double d = System.Math.Atan(System.Math.Sqrt(r2 / (1.0 - r2)));

        // SOFA iauC2ixys left-accumulates: Rz(e), Ry(d), Rz(-(e+s))
        // Product: M_sofa = iauRz(-(e+s)) * iauRy(d) * iauRz(e)
        // Negate all angles. Outermost (last SOFA op) is leftmost in chain.
        return Matrix.CreateRotationMatrixZ(e + s)
            .Multiply(Matrix.CreateRotationMatrixY(-d))
            .Multiply(Matrix.CreateRotationMatrixZ(-e));
    }

    /// <summary>
    /// Computes Earth Rotation Angle (ERA) in radians from UT1 Julian Date.
    /// ERA = 2π (0.7790572732640 + 1.00273781191135448 * Du)
    /// where Du = JD(UT1) - 2451545.0
    /// </summary>
    public static double EarthRotationAngle(double jdUt1High, double jdUt1Low)
    {
        double du = (jdUt1High - 2451545.0) + jdUt1Low;
        double f = (jdUt1High % 1.0) + (jdUt1Low % 1.0);

        double era = TwoPi * (f + 0.7790572732640 + 0.00273781191135448 * du);

        era %= TwoPi;
        if (era < 0.0) era += TwoPi;

        return era;
    }

    /// <summary>
    /// Computes the TIO locator s' in radians (secular approximation).
    /// s' = -47 * t microarcseconds.
    /// </summary>
    public static double TioLocator(double t)
    {
        return -47.0 * t * DAS2R * 1e-6;
    }

    /// <summary>
    /// Builds the polar motion matrix W(t).
    /// W = R3(-s') * R2(xp) * R1(yp)
    /// </summary>
    public static Matrix PolarMotionMatrix(double xp, double yp, double sp)
    {
        // SOFA iauPom00 left-accumulates: Rz(sp), Ry(-xp), Rx(-yp)
        // Product: M_sofa = iauRx(-yp) * iauRy(-xp) * iauRz(sp)
        // Negate all angles. Outermost (last SOFA op) is leftmost in chain.
        return Matrix.CreateRotationMatrixX(yp)
            .Multiply(Matrix.CreateRotationMatrixY(xp))
            .Multiply(Matrix.CreateRotationMatrixZ(-sp));
    }
}
