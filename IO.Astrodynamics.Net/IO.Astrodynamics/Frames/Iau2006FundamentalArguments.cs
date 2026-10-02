// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;

namespace IO.Astrodynamics.Frames;

/// <summary>
/// Fundamental arguments for the IAU 2006/2000A nutation model.
/// Each method returns the argument in radians, given t in Julian centuries TT from J2000.
/// Polynomial coefficients from IERS Conventions 2010, Table 5.2 / SOFA.
/// </summary>
public static class Iau2006FundamentalArguments
{
    private const double Arcsec2Rad = System.Math.PI / (180.0 * 3600.0);
    private const double TwoPi = 2.0 * System.Math.PI;

    private static double Normalize(double angle)
    {
        angle %= TwoPi;
        if (angle < 0) angle += TwoPi;
        return angle;
    }

    /// <summary>Mean anomaly of the Moon (l) — IERS 2003 (Fal03)</summary>
    public static double MoonMeanAnomaly(double t)
    {
        return Normalize(
            (485868.249036 + t * (1717915923.2178
            + t * (31.8792 + t * (0.051635 + t * -0.00024470)))) * Arcsec2Rad);
    }

    /// <summary>Mean anomaly of the Sun (l') — IERS 2003 (Falp03)</summary>
    public static double SunMeanAnomaly(double t)
    {
        return Normalize(
            (1287104.793048 + t * (129596581.0481
            + t * (-0.5532 + t * (0.000136 + t * -0.00001149)))) * Arcsec2Rad);
    }

    /// <summary>Mean argument of the latitude of the Moon (F) — IERS 2003 (Faf03)</summary>
    public static double MoonMeanArgumentOfLatitude(double t)
    {
        return Normalize(
            (335779.526232 + t * (1739527262.8478
            + t * (-12.7512 + t * (-0.001037 + t * 0.00000417)))) * Arcsec2Rad);
    }

    /// <summary>Mean elongation of the Moon from the Sun (D) — IERS 2003 (Fad03)</summary>
    public static double MeanElongation(double t)
    {
        return Normalize(
            (1072260.703692 + t * (1602961601.2090
            + t * (-6.3706 + t * (0.006593 + t * -0.00003169)))) * Arcsec2Rad);
    }

    /// <summary>Mean longitude of the ascending node of the Moon (Omega) — IERS 2003 (Faom03)</summary>
    public static double MoonAscendingNodeLongitude(double t)
    {
        return Normalize(
            (450160.398036 + t * (-6962890.5431
            + t * (7.4722 + t * (0.007702 + t * -0.00005939)))) * Arcsec2Rad);
    }

    /// <summary>Mean longitude of Mercury — IERS 2003 (Fame03)</summary>
    public static double LambdaMercury(double t)
    {
        return Normalize(4.402608842 + 2608.7903141574 * t);
    }

    /// <summary>Mean longitude of Venus — IERS 2003 (Fave03)</summary>
    public static double LambdaVenus(double t)
    {
        return Normalize(3.176146697 + 1021.3285546211 * t);
    }

    /// <summary>Mean longitude of Earth — IERS 2003 (Fae03)</summary>
    public static double LambdaEarth(double t)
    {
        return Normalize(1.753470314 + 628.3075849991 * t);
    }

    /// <summary>Mean longitude of Mars — IERS 2003 (Fama03)</summary>
    public static double LambdaMars(double t)
    {
        return Normalize(6.203480913 + 334.0612426700 * t);
    }

    /// <summary>Mean longitude of Jupiter — IERS 2003 (Faju03)</summary>
    public static double LambdaJupiter(double t)
    {
        return Normalize(0.599546497 + 52.9690962641 * t);
    }

    /// <summary>Mean longitude of Saturn — IERS 2003 (Fasa03)</summary>
    public static double LambdaSaturn(double t)
    {
        return Normalize(0.874016757 + 21.3299104960 * t);
    }

    /// <summary>Mean longitude of Uranus — IERS 2003 (Faur03)</summary>
    public static double LambdaUranus(double t)
    {
        return Normalize(5.481293872 + 7.4781598567 * t);
    }

    /// <summary>Mean longitude of Neptune — IERS 2003 (Fane03)</summary>
    public static double LambdaNeptune(double t)
    {
        return Normalize(5.311886287 + 3.8133035638 * t);
    }

    /// <summary>General accumulated precession in longitude — IERS 2003 (Fapa03)</summary>
    public static double GeneralPrecession(double t)
    {
        return 0.024381750 * t + 0.00000538691 * t * t;
    }
}
