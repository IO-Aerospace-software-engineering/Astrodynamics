using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using Xunit;

namespace IO.Astrodynamics.Tests.Frame;

public class Iau2006ModelTests
{
    private const double MAS2RAD = System.Math.PI / (180.0 * 3600.0 * 1000.0);

    #region Frame Bias

    [Fact]
    public void FrameBiasMatrixIsOrthogonal()
    {
        var b = Iau2006Model.FrameBias();
        var btb = b.Transpose().Multiply(b);

        // Should be identity within 1e-14
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                double expected = i == j ? 1.0 : 0.0;
                Assert.Equal(expected, btb.Get(i, j), 1e-14);
            }
        }
    }

    [Fact]
    public void FrameBiasMatchesSofaValues()
    {
        var b = Iau2006Model.FrameBias();

        // SOFA rb matrix values
        Assert.Equal(9.999999999999941158e-01, b.Get(0, 0), 1e-14);
        Assert.Equal(-7.078368960971556128e-08, b.Get(0, 1), 1e-16);
        Assert.Equal(8.056213977613186084e-08, b.Get(0, 2), 1e-16);
        Assert.Equal(7.078368694637676268e-08, b.Get(1, 0), 1e-16);
        Assert.Equal(9.999999999999968914e-01, b.Get(1, 1), 1e-14);
        Assert.Equal(3.305943735432137487e-08, b.Get(1, 2), 1e-16);
    }

    #endregion

    #region Precession Angles

    [Fact]
    public void PrecessionAnglesAtJ2000AreZeroExceptOmegaAndEpsilon()
    {
        var (psiA, omegaA, epsilonA, chiA) = Iau2006Model.PrecessionAngles(0.0);

        Assert.Equal(0.0, psiA, 1e-20);
        Assert.Equal(0.0, chiA, 1e-20);
        // omegaA and epsilonA should equal eps0 at J2000
        Assert.Equal(4.09092600600582890e-01, omegaA, 1e-14);
        Assert.Equal(4.09092600600582890e-01, epsilonA, 1e-14);
    }

    [Fact]
    public void PrecessionAnglesAtSofaTestDate()
    {
        var (psiA, omegaA, epsilonA, chiA) = Iau2006Model.PrecessionAngles(0.06);

        Assert.Equal(1.46561602655760907e-03, psiA, 1e-14);
        Assert.Equal(4.09092593995654230e-01, omegaA, 1e-14);
        Assert.Equal(4.09078976335651046e-01, epsilonA, 1e-14);
        Assert.Equal(3.02916811973115992e-06, chiA, 1e-14);
    }

    [Fact]
    public void PrecessionAnglesAt2024()
    {
        double t = 2.40013689253935653e-01;
        var (psiA, omegaA, epsilonA, chiA) = Iau2006Model.PrecessionAngles(t);

        Assert.Equal(5.86257240640978335e-03, psiA, 1e-13);
        Assert.Equal(4.09092584431670403e-01, omegaA, 1e-13);
        Assert.Equal(4.09038100519958703e-01, epsilonA, 1e-13);
        Assert.Equal(1.16184583869946668e-05, chiA, 1e-13);
    }

    #endregion

    #region CIP X, Y

    [Fact]
    public void CipXYAtJ2000MatchesSofaWithinNutationAccuracy()
    {
        var (x, y) = Iau2006Model.CipXY(0.0);

        // SOFA: X = -2.69463795685740364e-05, Y = -2.80047228228128159e-05
        // With IAU 2000B nutation, expect ~1 mas (5e-9 rad) accuracy
        Assert.Equal(-2.69463795685740364e-05, x, 5e-9);
        Assert.Equal(-2.80047228228128159e-05, y, 5e-9);
    }

    [Fact]
    public void CipXYAtSofaTestDateMatchesWithinNutationAccuracy()
    {
        var (x, y) = Iau2006Model.CipXY(0.06);

        // SOFA: X = 5.79130848670600775e-04, Y = 4.02057981673294767e-05
        Assert.Equal(5.79130848670600775e-04, x, 5e-9);
        Assert.Equal(4.02057981673294767e-05, y, 5e-9);
    }

    [Fact]
    public void CipXYAt2024MatchesWithinNutationAccuracy()
    {
        double t = 2.40013689253935653e-01;
        var (x, y) = Iau2006Model.CipXY(t);

        // SOFA: X = 2.32171139820248028e-03, Y = 3.30361811421811229e-05
        Assert.Equal(2.32171139820248028e-03, x, 5e-9);
        Assert.Equal(3.30361811421811229e-05, y, 5e-9);
    }

    #endregion

    #region Q(t) Matrix

    [Fact]
    public void PrecessionNutationMatrixIsOrthogonal()
    {
        var (x, y) = Iau2006Model.CipXY(0.06);
        double s = Iau2006Model.CioLocator(0.06, x, y);
        var qt = Iau2006Model.PrecessionNutationMatrix(x, y, s);

        var qtqt = qt.Transpose().Multiply(qt);
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                double expected = i == j ? 1.0 : 0.0;
                Assert.Equal(expected, qtqt.Get(i, j), 1e-14);
            }
        }
    }

    [Fact]
    public void PrecessionNutationMatrixAtSofaTestDateMatchesSofa()
    {
        var (x, y) = Iau2006Model.CipXY(0.06);
        double s = Iau2006Model.CioLocator(0.06, x, y);
        var qt = Iau2006Model.PrecessionNutationMatrix(x, y, s);

        // SOFA rc2i matrix (GCRS to CIRS) — compare with 1 mas tolerance
        double tol = 5e-9;
        Assert.Equal(9.999998323037155856e-01, qt.Get(0, 0), tol);
        Assert.Equal(-5.791308491611245205e-04, qt.Get(0, 2), tol);
        Assert.Equal(5.791308486706008831e-04, qt.Get(2, 0), tol);
        Assert.Equal(4.020579816732947667e-05, qt.Get(2, 1), tol);
    }

    #endregion

    #region Earth Rotation Angle

    [Fact]
    public void EraAtJ2000MatchesSofa()
    {
        double era = Iau2006Model.EarthRotationAngle(2451545.0, 0.0);
        Assert.Equal(4.89496121282375629e+00, era, 1e-12);
    }

    [Fact]
    public void EraAtJ2000Plus1DayMatchesSofa()
    {
        double era = Iau2006Model.EarthRotationAngle(2451545.0, 1.0);
        Assert.Equal(4.91216339239900002e+00, era, 1e-12);
    }

    [Fact]
    public void EraLinearGrowthRate()
    {
        double era0 = Iau2006Model.EarthRotationAngle(2451545.0, 0.0);
        double era1 = Iau2006Model.EarthRotationAngle(2451545.0, 1.0);

        // ERA should increase by 2*pi * 1.00273781191135448 per day
        double expectedDelta = 2.0 * System.Math.PI * 1.00273781191135448;
        double actualDelta = era1 - era0;
        if (actualDelta < 0) actualDelta += 2.0 * System.Math.PI;

        Assert.Equal(expectedDelta % (2.0 * System.Math.PI), actualDelta, 1e-10);
    }

    #endregion

    #region CIO Locator s

    [Fact]
    public void CioLocatorAtSofaTestDate()
    {
        var (x, y) = Iau2006Model.CipXY(0.06);
        double s = Iau2006Model.CioLocator(0.06, x, y);

        // SOFA (2000A): s = -1.22003221307645991e-08
        // Our s uses IAU 2000B X,Y (77-term nutation, ~1 mas accuracy).
        // The dominant -XY/2 term propagates the nutation truncation into s.
        // Tolerance: ~3e-9 accounts for 2000B vs 2000A differences.
        Assert.Equal(-1.22003221307645991e-08, s, 3e-9);
    }

    /// <summary>
    /// The periodic terms of s + XY/2 peak where |sin Omega| = 1 (the 2640.73 uas sin Omega term):
    /// a polynomial-only locator is off by 1.27e-8 rad (2.6 mas) there. Given SOFA's X, Y, the full
    /// series must reproduce iauS06. Reference: tools/sofa_reference/sofa_ref.c, epochs
    /// 2011_Feb_14_TT_sin_om_minus_1 and 2020_Jun_14_TT_sin_om_plus_1.
    /// </summary>
    [Theory]
    [InlineData(0.11117043121149897, 0.001115607505323693, -5.981399643273296e-07, 1.5092970507704093e-08)]
    [InlineData(0.20425735797399042, 0.0019498452577781959, -6.578588181375801e-06, -5.022023971233836e-09)]
    public void CioLocatorMatchesSofaS06WhereSinOmegaPeaks(double t, double x, double y, double expectedS)
    {
        double s = Iau2006Model.CioLocator(t, x, y);

        Assert.Equal(expectedS, s, 1e-11);
    }

    #endregion

    #region TIO Locator

    [Fact]
    public void TioLocatorAtSofaTestDate()
    {
        double sp = Iau2006Model.TioLocator(0.06);
        // SOFA: sp = -1.36717458072889130e-11
        Assert.Equal(-1.36717458072889130e-11, sp, 1e-15);
    }

    #endregion

    #region Polar Motion Matrix

    [Fact]
    public void PolarMotionMatrixWithZeroIsIdentity()
    {
        var w = Iau2006Model.PolarMotionMatrix(0.0, 0.0, 0.0);
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                double expected = i == j ? 1.0 : 0.0;
                Assert.Equal(expected, w.Get(i, j), 1e-15);
            }
        }
    }

    [Fact]
    public void PolarMotionMatrixMatchesSofa()
    {
        double xp = 2.55060238e-7;
        double yp = 1.860359247e-6;
        double sp = Iau2006Model.TioLocator(0.06);

        var w = Iau2006Model.PolarMotionMatrix(xp, yp, sp);

        // SOFA rpom values
        Assert.Equal(9.999999999999674705e-01, w.Get(0, 0), 1e-14);
        Assert.Equal(2.550602379999972723e-07, w.Get(0, 2), 1e-16);
        Assert.Equal(-1.860359246998866338e-06, w.Get(1, 2), 1e-16);
    }

    #endregion

    #region Nutation

    [Fact]
    public void NutationAtJ2000MatchesSofaWithin1Mas()
    {
        var (dpsi, deps) = Iau2006Model.Nutation(0.0);

        // SOFA 2000A: dpsi = -6.75442559896951151e-05, deps = -2.79708311923741366e-05
        // SOFA 2000B: dpsi = -6.75426125399223470e-05, deps = -2.79709233109856526e-05
        // IAU 2000B accuracy: ~300-600 µas (1.5-3e-9 rad) for dpsi
        double dpsiTol = 5e-9;  // ~1 mas
        double depsTol = 5e-9;
        Assert.Equal(-6.75442559896951151e-05, dpsi, dpsiTol);
        Assert.Equal(-2.79708311923741366e-05, deps, depsTol);
    }

    /// <summary>
    /// Nutation is IAU 2000B with the P03 adjustments of iauNut06a, so it must reproduce SOFA
    /// iauNut00b with those adjustments to rounding level. The secular factor fj2 = -2.7774e-6 t
    /// changes dpsi by up to 1.9e-10 rad: this test catches a constant or missing factor.
    /// Reference values: tools/sofa_reference/extract_nut00b.c (dpsi_2000B_P03, deps_2000B_P03).
    /// </summary>
    [Theory]
    [InlineData(0.0, -6.75426442646874552e-05, -2.79709233109856526e-05)] // J2000.0
    [InlineData(5.99999999999999978e-02, -9.63255521035108483e-06, 4.06319642951374308e-05)] // MJD 53736 TT
    [InlineData(2.40013689253935653e-01, -2.61510163946240930e-05, 3.93017153735647972e-05)] // 2024-01-01 12h TT
    [InlineData(-1.00000000000000006e-01, 5.74092078610876616e-05, 3.10604204797782922e-05)] // 1990-01-01 12h TT
    [InlineData(5.00000000000000000e-01, 7.35528403568220829e-05, -2.58410693765859737e-05)] // 2050-01-01 12h TT
    public void NutationMatchesSofaNut00bWithP03Adjustments(double t, double expectedDpsi, double expectedDeps)
    {
        var (dpsi, deps) = Iau2006Model.Nutation(t);

        const double tolerance = 1e-13;
        Assert.Equal(expectedDpsi, dpsi, tolerance);
        Assert.Equal(expectedDeps, deps, tolerance);
    }

    #endregion
}
