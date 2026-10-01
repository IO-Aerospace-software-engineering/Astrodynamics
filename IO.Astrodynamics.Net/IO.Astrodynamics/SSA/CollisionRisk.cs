// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
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
        ProjectedCovariance = projectedCovariance;
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
    /// Gets the encounter-plane covariance used by the 2D collision-probability model.
    /// </summary>
    public Matrix ProjectedCovariance { get; }

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
}
