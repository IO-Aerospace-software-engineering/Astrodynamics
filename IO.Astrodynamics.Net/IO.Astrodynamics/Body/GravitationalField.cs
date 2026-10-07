// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
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

        return position.Normalize() * (-centerOfMotion.GM / System.Math.Pow(position.Magnitude(), 2.0));
    }

    /// <summary>
    /// Adds the analytic ∂a/∂r of <see cref="ComputeGravitationalAcceleration"/> to <paramref name="dadr"/>, if this
    /// field has them.
    /// </summary>
    /// <remarks>
    /// The point mass has them, with the same gravitational parameter as the acceleration: that of the observer of
    /// <paramref name="stateVector"/>. A field without analytic partials returns false and leaves
    /// <paramref name="dadr"/> untouched, so that the caller falls back to finite differences. A subclass that does not
    /// override this method gets that fallback, not the point-mass partials: its acceleration is not the point mass.
    /// </remarks>
    /// <param name="stateVector">The state, relative to the attracting body.</param>
    /// <param name="dadr">3×3 row-major block the partials are added to, in 1/s², in the frame of the state.</param>
    /// <returns>Whether the partials were added.</returns>
    internal virtual bool TryAccumulatePositionPartials(StateVector stateVector, Span<double> dadr)
    {
        if (GetType() != typeof(GravitationalField))
        {
            return false;
        }

        CelestialItem centerOfMotion = stateVector.Observer as CelestialItem;
        PointMassPartials.Accumulate(stateVector.Position, centerOfMotion.GM, dadr);
        return true;
    }
}