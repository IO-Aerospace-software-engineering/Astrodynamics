// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;

namespace IO.Astrodynamics.CCSDS.OPM;

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
    /// Gets the right ascension of the ascending node in degrees (-360 to 360).
    /// </summary>
    public double RightAscensionOfAscendingNode { get; }

    /// <summary>
    /// Gets the argument of pericenter in degrees (-360 to 360).
    /// </summary>
    public double ArgumentOfPericenter { get; }

    /// <summary>
    /// Gets the true anomaly in degrees (null if mean anomaly is used).
    /// </summary>
    public double? TrueAnomaly { get; }

    /// <summary>
    /// Gets the mean anomaly in degrees (null if true anomaly is used).
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
        if (semiMajorAxis <= 0)
            throw new ArgumentOutOfRangeException(nameof(semiMajorAxis), "Semi-major axis must be positive.");
        if (eccentricity < 0)
            throw new ArgumentOutOfRangeException(nameof(eccentricity), "Eccentricity cannot be negative.");
        if (inclination < 0 || inclination > 180)
            throw new ArgumentOutOfRangeException(nameof(inclination), "Inclination must be between 0 and 180 degrees.");
        if (gm <= 0)
            throw new ArgumentOutOfRangeException(nameof(gm), "Gravitational parameter must be positive.");
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
    /// <param name="semiMajorAxis">Semi-major axis in km.</param>
    /// <param name="eccentricity">Eccentricity (>= 0).</param>
    /// <param name="inclination">Inclination in degrees (0-180).</param>
    /// <param name="raan">Right ascension of ascending node in degrees.</param>
    /// <param name="aop">Argument of pericenter in degrees.</param>
    /// <param name="trueAnomaly">True anomaly in degrees.</param>
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
    /// <param name="semiMajorAxis">Semi-major axis in km.</param>
    /// <param name="eccentricity">Eccentricity (>= 0).</param>
    /// <param name="inclination">Inclination in degrees (0-180).</param>
    /// <param name="raan">Right ascension of ascending node in degrees.</param>
    /// <param name="aop">Argument of pericenter in degrees.</param>
    /// <param name="meanAnomaly">Mean anomaly in degrees.</param>
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
        double raanDeg = keplerianElements.AscendingNode() * Constants.Rad2Deg;
        double aopDeg = keplerianElements.ArgumentOfPeriapsis() * Constants.Rad2Deg;
        double gmKm3S2 = keplerianElements.Observer.GM / 1e9; // m³/s² to km³/s²

        if (useTrueAnomaly)
        {
            double trueAnomalyDeg = keplerianElements.TrueAnomaly() * Constants.Rad2Deg;
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
            double meanAnomalyDeg = keplerianElements.MeanAnomaly() * Constants.Rad2Deg;
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
