// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System.Globalization;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Stores the collision-risk products derived from an encounter.
/// </summary>
public sealed class CollisionRisk
{
    /// <summary>
    /// Initializes a collision-risk result.
    /// </summary>
    public CollisionRisk(
        double combinedHardBodyRadiusMeters,
        double? probabilityOfCollision,
        Matrix projectedCovariance,
        double radialSigmaMeters,
        double inTrackSigmaMeters,
        double crossTrackSigmaMeters)
    {
        CombinedHardBodyRadiusMeters = combinedHardBodyRadiusMeters;
        ProbabilityOfCollision = probabilityOfCollision;
        _projectedCovariance = projectedCovariance.Copy();
        RadialSigmaMeters = radialSigmaMeters;
        InTrackSigmaMeters = inTrackSigmaMeters;
        CrossTrackSigmaMeters = crossTrackSigmaMeters;
    }

    /// <summary>
    /// Gets the combined hard-body radius used for collision-probability calculations, in meters.
    /// </summary>
    public double CombinedHardBodyRadiusMeters { get; }

    /// <summary>
    /// Gets the probability of collision when it could be computed, otherwise <see langword="null"/>.
    /// </summary>
    public double? ProbabilityOfCollision { get; }

    /// <summary>
    /// Gets the encounter-plane covariance used by the 2D collision-probability model: a copy of the one the result holds,
    /// which the constructor copied too, so that a term set on the matrix given or returned does not reach it (#722).
    /// </summary>
    public Matrix ProjectedCovariance => _projectedCovariance.Copy();

    private readonly Matrix _projectedCovariance;

    /// <summary>
    /// Gets the radial 1-sigma value from the combined RTN covariance, in meters.
    /// </summary>
    public double RadialSigmaMeters { get; }

    /// <summary>
    /// Gets the in-track 1-sigma value from the combined RTN covariance, in meters.
    /// </summary>
    public double InTrackSigmaMeters { get; }

    /// <summary>
    /// Gets the cross-track 1-sigma value from the combined RTN covariance, in meters.
    /// </summary>
    public double CrossTrackSigmaMeters { get; }

    /// <summary>
    /// The probability (4 significant digits, "Pc n/a" without one), the combined radius and the RTN sigmas, in invariant
    /// culture, for instance "Pc 2.715E-04, HBR 13 m, sigma R/T/N 94.3/360.6/67.1 m" (R17 of #668).
    /// </summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"Pc {FormatProbability(ProbabilityOfCollision)}, HBR {CombinedHardBodyRadiusMeters} m, sigma R/T/N {RadialSigmaMeters:F1}/{InTrackSigmaMeters:F1}/{CrossTrackSigmaMeters:F1} m");

    // A probability with 4 significant digits, "n/a" without one
    internal static string FormatProbability(double? probability) =>
        probability?.ToString("G4", CultureInfo.InvariantCulture) ?? "n/a";
}
