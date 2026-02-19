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
    /// Primary target that the primary body vector should point at (celestial body).
    /// Null when using IAttitudeTarget-based targeting.
    /// </summary>
    public ILocalizable PrimaryTarget { get; }

    /// <summary>
    /// Secondary target used to eliminate roll ambiguity around the primary pointing axis (celestial body).
    /// Null when using IAttitudeTarget-based targeting.
    /// </summary>
    public ILocalizable SecondaryTarget { get; }

    /// <summary>
    /// Primary attitude target (orbital direction or celestial body).
    /// Null when using ILocalizable-based targeting.
    /// </summary>
    public IAttitudeTarget PrimaryAttitudeTarget { get; }

    /// <summary>
    /// Secondary attitude target (orbital direction or celestial body).
    /// Null when using ILocalizable-based targeting.
    /// </summary>
    public IAttitudeTarget SecondaryAttitudeTarget { get; }

    /// <summary>
    /// Primary direction in spacecraft body frame that should point toward PrimaryTarget.
    /// </summary>
    public Vector3 PrimaryBodyVector { get; }

    /// <summary>
    /// Secondary direction in spacecraft body frame used to constrain roll.
    /// </summary>
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
    /// Creates a TRIAD attitude maneuver using explicit body frame vectors and ILocalizable targets.
    /// </summary>
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

    /// <summary>
    /// Creates a TRIAD attitude maneuver using explicit body frame vectors and IAttitudeTarget targets.
    /// This constructor supports orbital directions (prograde, nadir, normal, etc.) as well as celestial bodies.
    /// </summary>
    /// <param name="maneuverCenter">The central body for this maneuver (required for orbital direction computation).</param>
    /// <param name="minimumEpoch">Earliest epoch when the maneuver can execute.</param>
    /// <param name="maneuverHoldDuration">Duration to hold the attitude after achieving it.</param>
    /// <param name="primaryBodyVector">Primary direction in spacecraft body frame.</param>
    /// <param name="primaryTarget">Primary attitude target (orbital direction or celestial body).</param>
    /// <param name="secondaryBodyVector">Secondary direction in body frame for roll constraint.</param>
    /// <param name="secondaryTarget">Secondary attitude target (orbital direction or celestial body).</param>
    /// <param name="engine">Engine used for the maneuver.</param>
    /// <param name="minimumVectorSeparation">Minimum angle between vectors in radians (default: 5 degrees).</param>
    /// <example>
    /// <code>
    /// // Prograde with sun tracking
    /// var attitude = new TriadAttitude(
    ///     earth, epoch, TimeSpan.FromMinutes(30),
    ///     Spacecraft.Front, OrbitalDirectionTarget.Prograde,
    ///     Spacecraft.Up, new CelestialAttitudeTarget(sun),
    ///     engine);
    /// </code>
    /// </example>
    public TriadAttitude(
        CelestialItem maneuverCenter,
        Time minimumEpoch,
        TimeSpan maneuverHoldDuration,
        Vector3 primaryBodyVector,
        IAttitudeTarget primaryTarget,
        Vector3 secondaryBodyVector,
        IAttitudeTarget secondaryTarget,
        Engine engine,
        double minimumVectorSeparation = DefaultMinimumSeparation)
        : base(maneuverCenter ?? throw new ArgumentNullException(nameof(maneuverCenter)), minimumEpoch, maneuverHoldDuration, engine)
    {
        PrimaryAttitudeTarget = primaryTarget ?? throw new ArgumentNullException(nameof(primaryTarget));
        SecondaryAttitudeTarget = secondaryTarget ?? throw new ArgumentNullException(nameof(secondaryTarget));
        MinimumVectorSeparation = minimumVectorSeparation;

        if (primaryBodyVector.Magnitude() < double.Epsilon)
            throw new ArgumentException("Primary body vector cannot be zero.", nameof(primaryBodyVector));
        if (secondaryBodyVector.Magnitude() < double.Epsilon)
            throw new ArgumentException("Secondary body vector cannot be zero.", nameof(secondaryBodyVector));

        PrimaryBodyVector = primaryBodyVector;
        SecondaryBodyVector = secondaryBodyVector;

        // Map CelestialAttitudeTarget back to ILocalizable properties for compatibility
        if (primaryTarget is CelestialAttitudeTarget celestialPrimary)
            PrimaryTarget = celestialPrimary.Target;
        if (secondaryTarget is CelestialAttitudeTarget celestialSecondary)
            SecondaryTarget = celestialSecondary.Target;

        ValidateBodyVectors();
    }

    /// <summary>
    /// Creates an LVLH (Local Vertical Local Horizontal) attitude.
    /// Primary: spacecraft Down toward nadir. Secondary: spacecraft Front toward prograde.
    /// </summary>
    public static TriadAttitude CreateLVLH(
        CelestialItem maneuverCenter,
        Time minimumEpoch,
        TimeSpan maneuverHoldDuration,
        Engine engine)
    {
        return new TriadAttitude(
            maneuverCenter, minimumEpoch, maneuverHoldDuration,
            Spacecraft.Down, OrbitalDirectionTarget.Nadir,
            Spacecraft.Front, OrbitalDirectionTarget.Prograde,
            engine);
    }

    /// <summary>
    /// Creates a prograde attitude with sun tracking for solar panel orientation.
    /// Primary: spacecraft Front toward prograde. Secondary: spacecraft Up toward Sun.
    /// </summary>
    public static TriadAttitude CreateProgradeWithSunTracking(
        CelestialItem maneuverCenter,
        Time minimumEpoch,
        TimeSpan maneuverHoldDuration,
        ILocalizable sun,
        Engine engine)
    {
        if (sun == null) throw new ArgumentNullException(nameof(sun));
        return new TriadAttitude(
            maneuverCenter, minimumEpoch, maneuverHoldDuration,
            Spacecraft.Front, OrbitalDirectionTarget.Prograde,
            Spacecraft.Up, new CelestialAttitudeTarget(sun),
            engine);
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
        Vector3 primaryRefVector;
        Vector3 secondaryRefVector;

        if (PrimaryAttitudeTarget != null && SecondaryAttitudeTarget != null)
        {
            // IAttitudeTarget path — supports orbital directions and celestial bodies
            primaryRefVector = PrimaryAttitudeTarget.GetDirection(stateVector);
            secondaryRefVector = SecondaryAttitudeTarget.GetDirection(stateVector);
        }
        else
        {
            // ILocalizable ephemeris path — original behavior
            var primaryEphemeris = PrimaryTarget.GetEphemeris(stateVector.Epoch, stateVector.Observer, stateVector.Frame, Aberration.LT);
            var secondaryEphemeris = SecondaryTarget.GetEphemeris(stateVector.Epoch, stateVector.Observer, stateVector.Frame, Aberration.LT);

            primaryRefVector = (primaryEphemeris.ToStateVector().Position - stateVector.Position).Normalize();
            secondaryRefVector = (secondaryEphemeris.ToStateVector().Position - stateVector.Position).Normalize();
        }

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
