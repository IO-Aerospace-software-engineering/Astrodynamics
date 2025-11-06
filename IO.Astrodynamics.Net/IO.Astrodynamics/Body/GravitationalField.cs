// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Body;

/// <summary>
/// Represents a gravitational field and provides methods to compute gravitational acceleration.
/// </summary>
public class GravitationalField
{
    /// <summary>
    /// Computes the gravitational acceleration at a given state vector.
    /// </summary>
    /// <param name="stateVector">The state vector containing the position and observer.</param>
    /// <returns>The gravitational acceleration as a <see cref="Vector3"/>.</returns>
    public virtual Vector3 ComputeGravitationalAcceleration(StateVector stateVector)
    {
        CelestialItem centerOfMotion = stateVector.Observer as CelestialItem;
        var position = stateVector.Position;

        // Optimized: compute magnitude once instead of twice (once in Normalize, once in Pow)
        // Formula: a = -GM * r / |r|^3
        var magnitudeSquared = position.MagnitudeSquared();
        var magnitude = System.Math.Sqrt(magnitudeSquared);
        var magnitude3 = magnitude * magnitudeSquared;

        return position * (-centerOfMotion.GM / magnitude3);
    }
}