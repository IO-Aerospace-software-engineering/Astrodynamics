// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.Frames;

/// <summary>
/// IAU 2006/2000A precession-nutation model.
/// Implements the CIO-based transformation chain: ICRF → GCRF → CIRS → TIRS → ITRF.
///
/// Uses IAU 2006 precession (Fukushima-Williams angles) with IAU 2000B nutation (77 luni-solar terms).
/// Accuracy: ~1 mas for nutation, sub-µas for precession and frame bias.
/// </summary>
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
    /// Computes IAU 2000B nutation (dpsi, deps) in radians.
    /// Uses 77 luni-solar terms plus planetary bias corrections.
    /// </summary>
    public static (double dpsi, double deps) Nutation(double t)
    {
        double l = Iau2006FundamentalArguments.MoonMeanAnomaly(t);
        double lp = Iau2006FundamentalArguments.SunMeanAnomaly(t);
        double f = Iau2006FundamentalArguments.MoonMeanArgumentOfLatitude(t);
        double d = Iau2006FundamentalArguments.MeanElongation(t);
        double om = Iau2006FundamentalArguments.MoonAscendingNodeLongitude(t);

        double dpsi = 0.0;
        double deps = 0.0;

        for (int i = Iau2006NutationData.LuniSolarTerms.Length - 1; i >= 0; i--)
        {
            var term = Iau2006NutationData.LuniSolarTerms[i];
            double arg = term.nl * l + term.nlp * lp + term.nf * f + term.nd * d + term.nom * om;
            double sinArg = System.Math.Sin(arg);
            double cosArg = System.Math.Cos(arg);

            dpsi += (term.sp + term.spt * t) * sinArg + term.cp * cosArg;
            deps += (term.ce + term.cet * t) * cosArg + term.se * sinArg;
        }

        // Convert from 0.1 µas to radians and add planetary bias
        dpsi = dpsi * Iau2006NutationData.U + Iau2006NutationData.DpsiPlanetaryBias * DAS2R * 1e-6;
        deps = deps * Iau2006NutationData.U + Iau2006NutationData.DepsPlanetaryBias * DAS2R * 1e-6;

        // Apply IAU 2006 corrections to IAU 2000 nutation (P03 compatibility, from SOFA iauNut06a)
        double fj2 = -2.7774e-6;
        dpsi += dpsi * (0.4697e-6 + fj2);
        deps += deps * fj2;

        return (dpsi, deps);
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
    /// Computes the CIO locator s in radians.
    /// Uses the polynomial approximation with the dominant -XY/2 term.
    /// </summary>
    public static double CioLocator(double t, double x, double y)
    {
        // s + XY/2 approximation (polynomial terms from IERS Conventions 2010)
        double s = -x * y / 2.0;

        // Add polynomial terms (microarcseconds, from SOFA iauS06)
        s += (94.0 + 3808.65 * t - 122.68 * t * t
              - 72574.11 * t * t * t
              + 27.98 * t * t * t * t
              + 15.62 * t * t * t * t * t) * DAS2R * 1e-6;

        return s;
    }

    /// <summary>
    /// Builds the CIO-based precession-nutation matrix Q(t) from CIP coordinates X, Y
    /// and CIO locator s. Transforms from GCRS (≈ ICRF/J2000) to CIRS.
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
