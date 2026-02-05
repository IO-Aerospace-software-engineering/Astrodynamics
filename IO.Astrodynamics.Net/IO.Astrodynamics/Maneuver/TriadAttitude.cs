// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver;

/// <summary>
/// TRIAD (TRIaxial Attitude Determination) attitude maneuver.
/// Uses two non-collinear observation vectors to fully constrain spacecraft attitude (3 DOF).
/// This eliminates the roll ambiguity present in single-vector pointing solutions.
/// </summary>
/// <remarks>
/// <para>
/// The spacecraft body frame follows these conventions (see <see cref="Spacecraft"/>):
/// <list type="bullet">
///   <item><description>Front (+Y): <see cref="Spacecraft.Front"/></description></item>
///   <item><description>Right (+X): <see cref="Spacecraft.Right"/></description></item>
///   <item><description>Up (+Z): <see cref="Spacecraft.Up"/></description></item>
/// </list>
/// </para>
/// <para>
/// Example usage with spacecraft directions:
/// <code>
/// // Point spacecraft Front at Moon, keep Up toward Sun
/// new TriadAttitude(epoch, duration, Spacecraft.Front, moon, Spacecraft.Up, sun, engine);
/// </code>
/// </para>
/// </remarks>
public class TriadAttitude : Attitude
{
    /// <summary>
    /// Primary target that the primary body vector should point at.
    /// </summary>
    public ILocalizable PrimaryTarget { get; }

    /// <summary>
    /// Secondary target used to eliminate roll ambiguity around the primary pointing axis.
    /// The secondary body vector will be oriented toward this target as much as possible
    /// while maintaining the primary pointing constraint.
    /// </summary>
    public ILocalizable SecondaryTarget { get; }

    /// <summary>
    /// Primary direction in spacecraft body frame that should point toward PrimaryTarget.
    /// </summary>
    /// <remarks>
    /// Common choices include:
    /// <list type="bullet">
    ///   <item><description><see cref="Spacecraft.Front"/> (+Y) - for forward-pointing instruments</description></item>
    ///   <item><description><see cref="Spacecraft.Down"/> (-Z) - for nadir-pointing sensors</description></item>
    ///   <item><description>Instrument boresight via <see cref="Instrument.GetBoresightInSpacecraftFrame"/></description></item>
    /// </list>
    /// </remarks>
    public Vector3 PrimaryBodyVector { get; }

    /// <summary>
    /// Secondary direction in spacecraft body frame used to constrain roll.
    /// </summary>
    /// <remarks>
    /// Common choices include:
    /// <list type="bullet">
    ///   <item><description><see cref="Spacecraft.Up"/> (+Z) - for keeping solar panels toward Sun</description></item>
    ///   <item><description><see cref="Spacecraft.Right"/> (+X) - for specific instrument alignment</description></item>
    ///   <item><description>Instrument refVector via <see cref="Instrument.GetRefVectorInSpacecraftFrame"/></description></item>
    /// </list>
    /// </remarks>
    public Vector3 SecondaryBodyVector { get; }

    /// <summary>
    /// Minimum allowed angle between vectors (body or reference) in radians.
    /// Default is 5 degrees. Vectors closer than this are considered collinear,
    /// which would result in numerical instability and poor attitude determination.
    /// </summary>
    public double MinimumVectorSeparation { get; }

    private const double DefaultMinimumSeparation = 5.0 * Constants.Deg2Rad;

    /// <summary>
    /// Creates a TRIAD attitude maneuver using an instrument's boresight and reference vector.
    /// The boresight points at the primary target, while the reference vector constrains roll toward the secondary target.
    /// </summary>
    /// <param name="minimumEpoch">Earliest epoch when the maneuver can execute.</param>
    /// <param name="maneuverHoldDuration">Duration to hold the attitude after achieving it.</param>
    /// <param name="instrument">Instrument defining boresight (primary) and refVector (secondary) body vectors.</param>
    /// <param name="primaryTarget">Target for boresight to point at.</param>
    /// <param name="secondaryTarget">Target for roll constraint (eliminates roll ambiguity).</param>
    /// <param name="engine">Engine used for the maneuver.</param>
    /// <param name="minimumVectorSeparation">Minimum angle between vectors in radians (default: 5 degrees).</param>
    public TriadAttitude(
        Time minimumEpoch,
        TimeSpan maneuverHoldDuration,
        Instrument instrument,
        ILocalizable primaryTarget,
        ILocalizable secondaryTarget,
        Engine engine,
        double minimumVectorSeparation = DefaultMinimumSeparation)
        : base(GetManeuverCenter(primaryTarget), minimumEpoch, maneuverHoldDuration, engine)
    {
        if (instrument == null) throw new ArgumentNullException(nameof(instrument));
        PrimaryTarget = primaryTarget ?? throw new ArgumentNullException(nameof(primaryTarget));
        SecondaryTarget = secondaryTarget ?? throw new ArgumentNullException(nameof(secondaryTarget));
        MinimumVectorSeparation = minimumVectorSeparation;

        PrimaryBodyVector = instrument.GetBoresightInSpacecraftFrame();
        SecondaryBodyVector = instrument.GetRefVectorInSpacecraftFrame();

        ValidateBodyVectors();
    }

    /// <summary>
    /// Creates a TRIAD attitude maneuver using two different instruments pointing at two targets.
    /// </summary>
    /// <param name="minimumEpoch">Earliest epoch when the maneuver can execute.</param>
    /// <param name="maneuverHoldDuration">Duration to hold the attitude after achieving it.</param>
    /// <param name="primaryInstrument">Instrument whose boresight should point at primary target.</param>
    /// <param name="primaryTarget">Target for primary instrument boresight.</param>
    /// <param name="secondaryInstrument">Instrument whose boresight constrains roll.</param>
    /// <param name="secondaryTarget">Target for secondary instrument boresight.</param>
    /// <param name="engine">Engine used for the maneuver.</param>
    /// <param name="minimumVectorSeparation">Minimum angle between vectors in radians (default: 5 degrees).</param>
    public TriadAttitude(
        Time minimumEpoch,
        TimeSpan maneuverHoldDuration,
        Instrument primaryInstrument,
        ILocalizable primaryTarget,
        Instrument secondaryInstrument,
        ILocalizable secondaryTarget,
        Engine engine,
        double minimumVectorSeparation = DefaultMinimumSeparation)
        : base(GetManeuverCenter(primaryTarget), minimumEpoch, maneuverHoldDuration, engine)
    {
        if (primaryInstrument == null) throw new ArgumentNullException(nameof(primaryInstrument));
        if (secondaryInstrument == null) throw new ArgumentNullException(nameof(secondaryInstrument));
        PrimaryTarget = primaryTarget ?? throw new ArgumentNullException(nameof(primaryTarget));
        SecondaryTarget = secondaryTarget ?? throw new ArgumentNullException(nameof(secondaryTarget));
        MinimumVectorSeparation = minimumVectorSeparation;

        PrimaryBodyVector = primaryInstrument.GetBoresightInSpacecraftFrame();
        SecondaryBodyVector = secondaryInstrument.GetBoresightInSpacecraftFrame();

        ValidateBodyVectors();
    }

    /// <summary>
    /// Creates a TRIAD attitude maneuver using explicit body frame vectors.
    /// </summary>
    /// <param name="minimumEpoch">Earliest epoch when the maneuver can execute.</param>
    /// <param name="maneuverHoldDuration">Duration to hold the attitude after achieving it.</param>
    /// <param name="primaryBodyVector">
    /// Primary direction in spacecraft body frame that will point at the primary target.
    /// Use spacecraft directions like <see cref="Spacecraft.Front"/> (+Y), <see cref="Spacecraft.Down"/> (-Z),
    /// or custom vectors.
    /// </param>
    /// <param name="primaryTarget">Target for primary body vector to point at.</param>
    /// <param name="secondaryBodyVector">
    /// Secondary direction in body frame for roll constraint.
    /// Use spacecraft directions like <see cref="Spacecraft.Up"/> (+Z), <see cref="Spacecraft.Right"/> (+X),
    /// or custom vectors. Must not be collinear with primaryBodyVector.
    /// </param>
    /// <param name="secondaryTarget">Target for secondary body vector constraint.</param>
    /// <param name="engine">Engine used for the maneuver.</param>
    /// <param name="minimumVectorSeparation">Minimum angle between vectors in radians (default: 5 degrees).</param>
    /// <example>
    /// <code>
    /// // Point spacecraft Front at Moon, keep Up toward Sun
    /// var attitude = new TriadAttitude(
    ///     epoch, TimeSpan.FromMinutes(10),
    ///     Spacecraft.Front, moon,
    ///     Spacecraft.Up, sun,
    ///     engine);
    /// </code>
    /// </example>
    public TriadAttitude(
        Time minimumEpoch,
        TimeSpan maneuverHoldDuration,
        Vector3 primaryBodyVector,
        ILocalizable primaryTarget,
        Vector3 secondaryBodyVector,
        ILocalizable secondaryTarget,
        Engine engine,
        double minimumVectorSeparation = DefaultMinimumSeparation)
        : base(GetManeuverCenter(primaryTarget), minimumEpoch, maneuverHoldDuration, engine)
    {
        PrimaryTarget = primaryTarget ?? throw new ArgumentNullException(nameof(primaryTarget));
        SecondaryTarget = secondaryTarget ?? throw new ArgumentNullException(nameof(secondaryTarget));
        MinimumVectorSeparation = minimumVectorSeparation;

        if (primaryBodyVector.Magnitude() < double.Epsilon)
            throw new ArgumentException("Primary body vector cannot be zero.", nameof(primaryBodyVector));
        if (secondaryBodyVector.Magnitude() < double.Epsilon)
            throw new ArgumentException("Secondary body vector cannot be zero.", nameof(secondaryBodyVector));

        PrimaryBodyVector = primaryBodyVector;
        SecondaryBodyVector = secondaryBodyVector;

        ValidateBodyVectors();
    }

    private static CelestialItem GetManeuverCenter(ILocalizable target)
    {
        return target is CelestialItem t ? t : (target as Site)?.CelestialBody;
    }

    private void ValidateBodyVectors()
    {
        double angle = PrimaryBodyVector.Angle(SecondaryBodyVector);
        if (angle < MinimumVectorSeparation || angle > System.Math.PI - MinimumVectorSeparation)
        {
            throw new ArgumentException(
                $"Body vectors are too close to collinear. Angle: {angle * Constants.Rad2Deg:F2} degrees. " +
                $"Minimum separation required: {MinimumVectorSeparation * Constants.Rad2Deg:F2} degrees.");
        }
    }

    /// <summary>
    /// Computes the spacecraft orientation using the TRIAD algorithm.
    /// </summary>
    /// <param name="stateVector">Current spacecraft state vector.</param>
    /// <returns>Quaternion representing the spacecraft orientation.</returns>
    protected override Quaternion ComputeOrientation(StateVector stateVector)
    {
        // Get reference frame vectors from ephemeris
        var primaryEphemeris = PrimaryTarget.GetEphemeris(stateVector.Epoch, stateVector.Observer, stateVector.Frame, Aberration.LT);
        var secondaryEphemeris = SecondaryTarget.GetEphemeris(stateVector.Epoch, stateVector.Observer, stateVector.Frame, Aberration.LT);

        var primaryRefVector = (primaryEphemeris.ToStateVector().Position - stateVector.Position).Normalize();
        var secondaryRefVector = (secondaryEphemeris.ToStateVector().Position - stateVector.Position).Normalize();

        // Validate reference vectors are not collinear
        double refAngle = primaryRefVector.Angle(secondaryRefVector);
        if (refAngle < MinimumVectorSeparation || refAngle > System.Math.PI - MinimumVectorSeparation)
        {
            throw new InvalidOperationException(
                $"Reference vectors are too close to collinear at epoch {stateVector.Epoch}. " +
                $"Angle: {refAngle * Constants.Rad2Deg:F2} degrees. " +
                $"Minimum separation required: {MinimumVectorSeparation * Constants.Rad2Deg:F2} degrees.");
        }

        // Construct orthonormal triads using the NAIF pattern
        // Reference frame triad (where we want to point)
        var t1R = primaryRefVector;
        var t2R = t1R.Cross(secondaryRefVector).Normalize();
        var t3R = t1R.Cross(t2R);

        // Body frame triad (spacecraft axes)
        var t1B = PrimaryBodyVector.Normalize();
        var t2B = t1B.Cross(SecondaryBodyVector).Normalize();
        var t3B = t1B.Cross(t2B);

        // Build rotation matrices: columns are the triad vectors
        var mRef = Matrix.FromColumnVectors(t1R, t2R, t3R);
        var mBody = Matrix.FromColumnVectors(t1B, t2B, t3B);

        // Compute attitude: R = M_ref * M_body^T
        // This rotation takes vectors from body frame to reference/inertial frame
        // Such that: primaryBodyVector.Rotate(R) = primaryRefVector
        var attitude = mRef.Multiply(mBody.Transpose());

        // Convert to quaternion
        return attitude.ToQuaternion();
    }
}
