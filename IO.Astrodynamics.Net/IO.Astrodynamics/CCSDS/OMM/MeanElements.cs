// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;

namespace IO.Astrodynamics.CCSDS.OMM;

/// <summary>
/// Represents the mean Keplerian elements in an OMM message.
/// </summary>
/// <remarks>
/// Mean elements are averaged orbital parameters used with analytical propagators
/// like SGP4/SDP4. Either semi-major axis OR mean motion must be specified.
/// </remarks>
public class MeanElements
{
    /// <summary>
    /// Gets the epoch of the orbital elements.
    /// </summary>
    public DateTime Epoch { get; }

    /// <summary>
    /// Gets the semi-major axis in kilometers (optional, mutually exclusive with MeanMotion).
    /// </summary>
    public double? SemiMajorAxis { get; }

    /// <summary>
    /// Gets the mean motion in revolutions per day (optional, mutually exclusive with SemiMajorAxis).
    /// </summary>
    public double? MeanMotion { get; }

    /// <summary>
    /// Gets the eccentricity (dimensionless, 0 to less than 1 for elliptical orbits).
    /// </summary>
    public double Eccentricity { get; }

    /// <summary>
    /// Gets the inclination in degrees (0 to 180).
    /// </summary>
    public double Inclination { get; }

    /// <summary>
    /// Gets the right ascension of the ascending node in degrees (0 to 360).
    /// </summary>
    public double RightAscensionOfAscendingNode { get; }

    /// <summary>
    /// Gets the argument of pericenter in degrees (0 to 360).
    /// </summary>
    public double ArgumentOfPericenter { get; }

    /// <summary>
    /// Gets the mean anomaly in degrees (0 to 360).
    /// </summary>
    public double MeanAnomaly { get; }

    /// <summary>
    /// Gets the gravitational parameter (GM) in km³/s² (optional).
    /// </summary>
    public double? GravitationalParameter { get; }

    /// <summary>
    /// Gets optional comments associated with the mean elements.
    /// </summary>
    public IReadOnlyList<string> Comments { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MeanElements"/> class with semi-major axis.
    /// </summary>
    /// <param name="epoch">The epoch of the orbital elements.</param>
    /// <param name="semiMajorAxis">The semi-major axis in kilometers.</param>
    /// <param name="eccentricity">The eccentricity.</param>
    /// <param name="inclination">The inclination in degrees.</param>
    /// <param name="rightAscensionOfAscendingNode">The RAAN in degrees.</param>
    /// <param name="argumentOfPericenter">The argument of pericenter in degrees.</param>
    /// <param name="meanAnomaly">The mean anomaly in degrees.</param>
    /// <param name="gravitationalParameter">Optional GM in km³/s².</param>
    /// <param name="comments">Optional comments.</param>
    public MeanElements(
        DateTime epoch,
        double semiMajorAxis,
        double eccentricity,
        double inclination,
        double rightAscensionOfAscendingNode,
        double argumentOfPericenter,
        double meanAnomaly,
        double? gravitationalParameter = null,
        IReadOnlyList<string> comments = null)
    {
        if (semiMajorAxis <= 0)
            throw new ArgumentOutOfRangeException(nameof(semiMajorAxis), "Semi-major axis must be positive.");

        ValidateAngles(eccentricity, inclination, rightAscensionOfAscendingNode, argumentOfPericenter, meanAnomaly);
        ValidateGravitationalParameter(gravitationalParameter);

        Epoch = epoch;
        SemiMajorAxis = semiMajorAxis;
        MeanMotion = null;
        Eccentricity = eccentricity;
        Inclination = inclination;
        RightAscensionOfAscendingNode = rightAscensionOfAscendingNode;
        ArgumentOfPericenter = argumentOfPericenter;
        MeanAnomaly = meanAnomaly;
        GravitationalParameter = gravitationalParameter;
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MeanElements"/> class with mean motion.
    /// </summary>
    /// <param name="epoch">The epoch of the orbital elements.</param>
    /// <param name="meanMotion">The mean motion in revolutions per day.</param>
    /// <param name="eccentricity">The eccentricity.</param>
    /// <param name="inclination">The inclination in degrees.</param>
    /// <param name="rightAscensionOfAscendingNode">The RAAN in degrees.</param>
    /// <param name="argumentOfPericenter">The argument of pericenter in degrees.</param>
    /// <param name="meanAnomaly">The mean anomaly in degrees.</param>
    /// <param name="useMeanMotion">Disambiguator parameter (must be true).</param>
    /// <param name="gravitationalParameter">Optional GM in km³/s².</param>
    /// <param name="comments">Optional comments.</param>
    public MeanElements(
        DateTime epoch,
        double meanMotion,
        double eccentricity,
        double inclination,
        double rightAscensionOfAscendingNode,
        double argumentOfPericenter,
        double meanAnomaly,
        bool useMeanMotion,
        double? gravitationalParameter = null,
        IReadOnlyList<string> comments = null)
    {
        if (!useMeanMotion)
            throw new ArgumentException("useMeanMotion must be true when using this constructor.", nameof(useMeanMotion));

        if (meanMotion <= 0)
            throw new ArgumentOutOfRangeException(nameof(meanMotion), "Mean motion must be positive.");

        ValidateAngles(eccentricity, inclination, rightAscensionOfAscendingNode, argumentOfPericenter, meanAnomaly);
        ValidateGravitationalParameter(gravitationalParameter);

        Epoch = epoch;
        SemiMajorAxis = null;
        MeanMotion = meanMotion;
        Eccentricity = eccentricity;
        Inclination = inclination;
        RightAscensionOfAscendingNode = rightAscensionOfAscendingNode;
        ArgumentOfPericenter = argumentOfPericenter;
        MeanAnomaly = meanAnomaly;
        GravitationalParameter = gravitationalParameter;
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Gets a value indicating whether this instance uses mean motion (true) or semi-major axis (false).
    /// </summary>
    public bool UsesMeanMotion => MeanMotion.HasValue;

    /// <summary>
    /// Creates a new MeanElements instance with semi-major axis.
    /// </summary>
    public static MeanElements CreateWithSemiMajorAxis(
        DateTime epoch,
        double semiMajorAxis,
        double eccentricity,
        double inclination,
        double raan,
        double argOfPericenter,
        double meanAnomaly,
        double? gm = null,
        IReadOnlyList<string> comments = null)
    {
        return new MeanElements(epoch, semiMajorAxis, eccentricity, inclination, raan, argOfPericenter, meanAnomaly, gm, comments);
    }

    /// <summary>
    /// Creates a new MeanElements instance with mean motion.
    /// </summary>
    public static MeanElements CreateWithMeanMotion(
        DateTime epoch,
        double meanMotion,
        double eccentricity,
        double inclination,
        double raan,
        double argOfPericenter,
        double meanAnomaly,
        double? gm = null,
        IReadOnlyList<string> comments = null)
    {
        return new MeanElements(epoch, meanMotion, eccentricity, inclination, raan, argOfPericenter, meanAnomaly, true, gm, comments);
    }

    private static void ValidateAngles(double eccentricity, double inclination, double raan, double argOfPericenter, double meanAnomaly)
    {
        if (eccentricity < 0)
            throw new ArgumentOutOfRangeException(nameof(eccentricity), "Eccentricity must be non-negative.");

        if (inclination < 0 || inclination > 180)
            throw new ArgumentOutOfRangeException(nameof(inclination), "Inclination must be between 0 and 180 degrees.");

        if (raan < -360 || raan >= 360)
            throw new ArgumentOutOfRangeException(nameof(raan), "RAAN must be between -360 and 360 degrees.");

        if (argOfPericenter < -360 || argOfPericenter >= 360)
            throw new ArgumentOutOfRangeException(nameof(argOfPericenter), "Argument of pericenter must be between -360 and 360 degrees.");

        if (meanAnomaly < -360 || meanAnomaly >= 360)
            throw new ArgumentOutOfRangeException(nameof(meanAnomaly), "Mean anomaly must be between -360 and 360 degrees.");
    }

    private static void ValidateGravitationalParameter(double? gm)
    {
        if (gm.HasValue && gm.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(gm), "Gravitational parameter must be positive.");
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var elementType = UsesMeanMotion ? $"n={MeanMotion:F8} rev/day" : $"a={SemiMajorAxis:F3} km";
        return $"MeanElements[{Epoch:O}, {elementType}, e={Eccentricity:F7}, i={Inclination:F4}°]";
    }
}
