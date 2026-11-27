// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;

namespace IO.Astrodynamics.Ccsds;

/// <summary>
/// Represents the optional Keplerian elements section of an OPM message.
/// </summary>
/// <remarks>
/// <para>
/// OPM Keplerian elements use TRUE_ANOMALY or MEAN_ANOMALY (choice, one required).
/// All angular values are in degrees, distances in kilometers, and GM in km³/s².
/// </para>
/// <para>
/// Use <see cref="CreateWithTrueAnomaly"/> or <see cref="CreateWithMeanAnomaly"/> factory methods
/// to create instances with the appropriate anomaly type.
/// </para>
/// </remarks>
public class OpmKeplerianElements
{
    /// <summary>
    /// Gets the semi-major axis in kilometers.
    /// </summary>
    public double SemiMajorAxis { get; }

    /// <summary>
    /// Gets the eccentricity (dimensionless, >= 0).
    /// </summary>
    public double Eccentricity { get; }

    /// <summary>
    /// Gets the inclination in degrees (0-180).
    /// </summary>
    public double Inclination { get; }

    /// <summary>
    /// Gets the right ascension of the ascending node in degrees, in [-360, 360) (angleRange of NDM/XML 4.0.0).
    /// </summary>
    public double RightAscensionOfAscendingNode { get; }

    /// <summary>
    /// Gets the argument of pericenter in degrees, in [-360, 360) (angleRange of NDM/XML 4.0.0).
    /// </summary>
    public double ArgumentOfPericenter { get; }

    /// <summary>
    /// Gets the true anomaly in degrees, in [-360, 360) (angleRange of NDM/XML 4.0.0), or null if mean anomaly is used.
    /// </summary>
    public double? TrueAnomaly { get; }

    /// <summary>
    /// Gets the mean anomaly in degrees, in [-360, 360) (angleRange of NDM/XML 4.0.0), or null if true anomaly is used.
    /// </summary>
    public double? MeanAnomaly { get; }

    /// <summary>
    /// Gets the gravitational parameter (GM) in km³/s².
    /// </summary>
    /// <remarks>
    /// This is a required field in OPM Keplerian elements.
    /// </remarks>
    public double GravitationalParameter { get; }

    /// <summary>
    /// Gets whether true anomaly is used (vs mean anomaly).
    /// </summary>
    public bool UsesTrueAnomaly => TrueAnomaly.HasValue;

    /// <summary>
    /// Gets whether mean anomaly is used (vs true anomaly).
    /// </summary>
    public bool UsesMeanAnomaly => MeanAnomaly.HasValue;

    /// <summary>
    /// Gets optional comments associated with the Keplerian elements.
    /// </summary>
    public IReadOnlyList<string> Comments { get; }

    private OpmKeplerianElements(
        double semiMajorAxis,
        double eccentricity,
        double inclination,
        double raan,
        double aop,
        double? trueAnomaly,
        double? meanAnomaly,
        double gm,
        IReadOnlyList<string> comments)
    {
        // Each check refuses NaN, in no range, and the half-open ranges an infinite value (#708: NaN, and +Infinity for e,
        // a and GM, passed the checks, written as the complement of the range)
        if (!(double.IsFinite(eccentricity) && eccentricity >= 0))
            throw new ArgumentOutOfRangeException(nameof(eccentricity), "Eccentricity must be finite and cannot be negative.");
        if (!double.IsFinite(semiMajorAxis))
            throw new ArgumentOutOfRangeException(nameof(semiMajorAxis), "Semi-major axis must be a finite value.");

        // Semi-major axis validation depends on orbit type:
        // - Elliptical orbits (e < 1): SMA must be positive
        // - Parabolic orbits (e = 1): SMA is infinite, so any finite value is taken as given (not typically used in OPM)
        // - Hyperbolic orbits (e > 1): SMA is negative by convention
        if (eccentricity < 1 && semiMajorAxis <= 0)
            throw new ArgumentOutOfRangeException(nameof(semiMajorAxis), "Semi-major axis must be positive for elliptical orbits (e < 1).");
        if (eccentricity > 1 && semiMajorAxis >= 0)
            throw new ArgumentOutOfRangeException(nameof(semiMajorAxis), "Semi-major axis must be negative for hyperbolic orbits (e > 1).");

        if (!(inclination >= 0 && inclination <= 180))
            throw new ArgumentOutOfRangeException(nameof(inclination), "Inclination must be between 0 and 180 degrees.");
        if (!(double.IsFinite(gm) && gm > 0))
            throw new ArgumentOutOfRangeException(nameof(gm), "Gravitational parameter must be a finite positive value.");

        // The four angles are ndm:angleType in ndmxml-4.0.0-opm-3.0.xsd, angleRange [-360, 360), as those of MeanElements
        // (#708: unchecked before, NaN and ±Infinity included)
        if (!IsInAngleRange(raan))
            throw new ArgumentOutOfRangeException(nameof(raan), "RAAN must be in [-360, 360) degrees.");
        if (!IsInAngleRange(aop))
            throw new ArgumentOutOfRangeException(nameof(aop), "Argument of pericenter must be in [-360, 360) degrees.");
        if (trueAnomaly.HasValue && !IsInAngleRange(trueAnomaly.Value))
            throw new ArgumentOutOfRangeException(nameof(trueAnomaly), "True anomaly must be in [-360, 360) degrees.");
        if (meanAnomaly.HasValue && !IsInAngleRange(meanAnomaly.Value))
            throw new ArgumentOutOfRangeException(nameof(meanAnomaly), "Mean anomaly must be in [-360, 360) degrees.");
        if (!trueAnomaly.HasValue && !meanAnomaly.HasValue)
            throw new ArgumentException("Either true anomaly or mean anomaly must be specified.");
        if (trueAnomaly.HasValue && meanAnomaly.HasValue)
            throw new ArgumentException("Cannot specify both true anomaly and mean anomaly.");

        SemiMajorAxis = semiMajorAxis;
        Eccentricity = eccentricity;
        Inclination = inclination;
        RightAscensionOfAscendingNode = raan;
        ArgumentOfPericenter = aop;
        TrueAnomaly = trueAnomaly;
        MeanAnomaly = meanAnomaly;
        GravitationalParameter = gm;
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Creates Keplerian elements with true anomaly.
    /// </summary>
    /// <param name="semiMajorAxis">Semi-major axis in km. Positive for elliptical orbits (e &lt; 1), negative for hyperbolic orbits (e &gt; 1).</param>
    /// <param name="eccentricity">Eccentricity (finite, >= 0).</param>
    /// <param name="inclination">Inclination in degrees (0-180).</param>
    /// <param name="raan">Right ascension of ascending node in degrees, in [-360, 360).</param>
    /// <param name="aop">Argument of pericenter in degrees, in [-360, 360).</param>
    /// <param name="trueAnomaly">True anomaly in degrees, in [-360, 360).</param>
    /// <param name="gm">Gravitational parameter in km³/s².</param>
    /// <param name="comments">Optional comments.</param>
    /// <returns>A new OpmKeplerianElements instance.</returns>
    public static OpmKeplerianElements CreateWithTrueAnomaly(
        double semiMajorAxis,
        double eccentricity,
        double inclination,
        double raan,
        double aop,
        double trueAnomaly,
        double gm,
        IReadOnlyList<string> comments = null)
    {
        return new OpmKeplerianElements(
            semiMajorAxis, eccentricity, inclination, raan, aop,
            trueAnomaly, null, gm, comments);
    }

    /// <summary>
    /// Creates Keplerian elements with mean anomaly.
    /// </summary>
    /// <param name="semiMajorAxis">Semi-major axis in km. Positive for elliptical orbits (e &lt; 1), negative for hyperbolic orbits (e &gt; 1).</param>
    /// <param name="eccentricity">Eccentricity (finite, >= 0).</param>
    /// <param name="inclination">Inclination in degrees (0-180).</param>
    /// <param name="raan">Right ascension of ascending node in degrees, in [-360, 360).</param>
    /// <param name="aop">Argument of pericenter in degrees, in [-360, 360).</param>
    /// <param name="meanAnomaly">Mean anomaly in degrees, in [-360, 360).</param>
    /// <param name="gm">Gravitational parameter in km³/s².</param>
    /// <param name="comments">Optional comments.</param>
    /// <returns>A new OpmKeplerianElements instance.</returns>
    public static OpmKeplerianElements CreateWithMeanAnomaly(
        double semiMajorAxis,
        double eccentricity,
        double inclination,
        double raan,
        double aop,
        double meanAnomaly,
        double gm,
        IReadOnlyList<string> comments = null)
    {
        return new OpmKeplerianElements(
            semiMajorAxis, eccentricity, inclination, raan, aop,
            null, meanAnomaly, gm, comments);
    }

    /// <summary>
    /// Creates OpmKeplerianElements from a framework KeplerianElements.
    /// </summary>
    /// <remarks>
    /// Each angle is written in degrees as its remainder modulo 360, in [0, 360) for the angles of
    /// <see cref="OrbitalParameters.KeplerianElements"/>: an angle of 2π is written 0 (#708; 360, outside angleRange, before).
    /// </remarks>
    /// <param name="keplerianElements">The framework Keplerian elements.</param>
    /// <param name="useTrueAnomaly">If true, uses true anomaly; otherwise uses mean anomaly.</param>
    /// <param name="comments">Optional comments.</param>
    /// <returns>A new OpmKeplerianElements instance.</returns>
    public static OpmKeplerianElements FromKeplerianElements(
        OrbitalParameters.KeplerianElements keplerianElements,
        bool useTrueAnomaly = true,
        IReadOnlyList<string> comments = null)
    {
        if (keplerianElements == null)
            throw new ArgumentNullException(nameof(keplerianElements));

        double semiMajorAxisKm = keplerianElements.SemiMajorAxis() / 1000.0;
        double inclDeg = keplerianElements.Inclination() * Constants.Rad2Deg;
        double raanDeg = ToDegreesInAngleRange(keplerianElements.AscendingNode());
        double aopDeg = ToDegreesInAngleRange(keplerianElements.ArgumentOfPeriapsis());
        double gmKm3S2 = keplerianElements.Observer.GM / 1e9; // m³/s² to km³/s²

        if (useTrueAnomaly)
        {
            double trueAnomalyDeg = ToDegreesInAngleRange(keplerianElements.TrueAnomaly());
            return CreateWithTrueAnomaly(
                semiMajorAxisKm,
                keplerianElements.Eccentricity(),
                inclDeg,
                raanDeg,
                aopDeg,
                trueAnomalyDeg,
                gmKm3S2,
                comments);
        }
        else
        {
            double meanAnomalyDeg = ToDegreesInAngleRange(keplerianElements.MeanAnomaly());
            return CreateWithMeanAnomaly(
                semiMajorAxisKm,
                keplerianElements.Eccentricity(),
                inclDeg,
                raanDeg,
                aopDeg,
                meanAnomalyDeg,
                gmKm3S2,
                comments);
        }
    }

    /// <summary>
    /// Whether an angle in degrees lies in angleRange of NDM/XML 4.0.0, [-360, 360): false for NaN and ±Infinity.
    /// </summary>
    private static bool IsInAngleRange(double degrees) => degrees >= -360.0 && degrees < 360.0;

    /// <summary>
    /// An angle in radians as its remainder modulo 360 in degrees, inside angleRange: the remainder of a finite angle lies
    /// in (-360, 360), and in [0, 360) for the angles of <see cref="OrbitalParameters.KeplerianElements"/>, in [0, 2π]. An
    /// angle of 2π, which their [0, 2π) normalization gives for a negative angle smaller than half the ulp of 2π, is
    /// written 0, where its 360 is outside the range (#708). An angle whose degrees lie in [0, 360) is returned unchanged,
    /// bit for bit (IEEE 754 remainder); NaN and ±Infinity give NaN, for the range check to refuse.
    /// </summary>
    private static double ToDegreesInAngleRange(double radians) => radians * Constants.Rad2Deg % 360.0;

    /// <summary>
    /// Gets the anomaly value in degrees (either true or mean, whichever is set).
    /// </summary>
    public double AnomalyDegrees => TrueAnomaly ?? MeanAnomaly ?? 0;

    /// <inheritdoc />
    public override string ToString()
    {
        string anomalyType = UsesTrueAnomaly ? "TA" : "MA";
        double anomalyValue = AnomalyDegrees;
        return $"OpmKeplerianElements[a={SemiMajorAxis:F3}km, e={Eccentricity:F6}, i={Inclination:F4}°, {anomalyType}={anomalyValue:F4}°]";
    }
}
