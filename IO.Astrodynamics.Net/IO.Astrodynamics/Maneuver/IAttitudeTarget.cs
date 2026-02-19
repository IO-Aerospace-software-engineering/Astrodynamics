using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Maneuver;

/// <summary>
/// Represents a target direction for attitude determination.
/// Can be an orbital direction (prograde, nadir, normal, etc.) or a celestial body.
/// </summary>
public interface IAttitudeTarget
{
    /// <summary>
    /// Gets the target direction vector in the observer's reference frame.
    /// </summary>
    /// <param name="observerState">The current state vector of the observing spacecraft.</param>
    /// <returns>A unit direction vector toward the target.</returns>
    Vector3 GetDirection(StateVector observerState);

    /// <summary>
    /// Gets a human-readable name for this target.
    /// </summary>
    string Name { get; }
}
