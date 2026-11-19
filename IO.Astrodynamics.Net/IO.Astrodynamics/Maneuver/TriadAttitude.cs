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
/// The constructors take literal body vectors: the static axes above, an instrument's vectors, or a spacecraft's own axes
/// (<see cref="Spacecraft.BodyFront"/>, <see cref="Spacecraft.BodyUp"/>, ...) passed explicitly. The factories
/// <see cref="CreateLVLH"/> and <see cref="CreateProgradeWithSunTracking"/> instead use the instance axes of the spacecraft
/// that carries the engine, read at each use, as the single-vector attitudes do (#677).
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
    /// <remarks>
    /// The vector given to the constructor; for an attitude of <see cref="CreateLVLH"/> or
    /// <see cref="CreateProgradeWithSunTracking"/>, the axis of the spacecraft that carries the engine's tank
    /// (<see cref="Spacecraft.BodyDown"/>, resp. <see cref="Spacecraft.BodyFront"/>), read at each call, or the static axis
    /// (<see cref="Spacecraft.Down"/>, resp. <see cref="Spacecraft.Front"/>) while the tank is on no spacecraft (#677).
    /// </remarks>
    public Vector3 PrimaryBodyVector => ResolveAxis(_primaryAxis, _primaryBodyVector);

    /// <summary>
    /// Secondary direction in spacecraft body frame used to constrain roll.
    /// </summary>
    /// <remarks>
    /// The vector given to the constructor; for an attitude of <see cref="CreateLVLH"/> or
    /// <see cref="CreateProgradeWithSunTracking"/>, the axis of the spacecraft that carries the engine's tank
    /// (<see cref="Spacecraft.BodyFront"/>, resp. <see cref="Spacecraft.BodyUp"/>), read at each call, or the static axis
    /// (<see cref="Spacecraft.Front"/>, resp. <see cref="Spacecraft.Up"/>) while the tank is on no spacecraft (#677).
    /// </remarks>
    public Vector3 SecondaryBodyVector => ResolveAxis(_secondaryAxis, _secondaryBodyVector);

    private readonly Vector3 _primaryBodyVector;
    private readonly Vector3 _secondaryBodyVector;

    // The instance axes of the factories, read on the spacecraft of the engine's tank; null for the public constructors
    private readonly Func<Spacecraft, Vector3> _primaryAxis;
    private readonly Func<Spacecraft, Vector3> _secondaryAxis;

    /// <summary>
    /// Minimum allowed angle between vectors (body or reference) in radians, in [1e-14, π/2].
    /// Default is 5 degrees. Vectors closer than this to collinear (parallel or antiparallel) are rejected,
    /// as they would result in numerical instability and poor attitude determination.
    /// </summary>
    /// <remarks>
    /// Two vectors pass when the sine of their angle, |û × ŵ| of their unit vectors, is at least sin(separation): that is
    /// an angle in [separation, π − separation], without the rounding of π − separation (which is π below about 2.2e-16
    /// rad) or the resolution of acos near ±1 (collinear vectors that round apart were measured up to 3.3e-8 rad from 0
    /// or π), and it rejects a zero vector and a vector with a component that is not finite (fourth round of #668). The
    /// smallest separation, 1e-14 rad, lies above the sine of vectors collinear to the rounding of their components, at
    /// most about (3 + √2)·2^-53 = 4.9e-16 (measured up to 2.4e-16), so collinear vectors that round apart are rejected
    /// at every separation allowed; below it such a pair passed with a roll set by rounding. A subnormal component (below
    /// about 2.2e-308) rounds by an absolute 4.9e-324, more than that bound: give body vectors of a normal magnitude.
    /// </remarks>
    public double MinimumVectorSeparation { get; }

    private const double DefaultMinimumSeparation = 5.0 * Constants.Deg2Rad;

    // The smallest separation: 20 times the bound (3 + √2) u = 4.9e-16 of |û × ŵ| for vectors collinear to the rounding
    // of their components (u = 2^-53 each for the input, each normalization and the cross product)
    private const double LowestMinimumSeparation = 1e-14;

    // A separation is in [1e-14, π/2] rad (fourth round of #668): 0, a negative one or NaN switched off both collinearity
    // checks, one below the rounding of the sine let collinear vectors that round apart through, and one above π/2
    // rejected every pair, perpendicular ones included
    private static void CheckMinimumVectorSeparation(double minimumVectorSeparation)
    {
        if (!(minimumVectorSeparation >= LowestMinimumSeparation && minimumVectorSeparation <= System.Math.PI / 2.0))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumVectorSeparation), minimumVectorSeparation,
                "The minimum vector separation must be in [1e-14, π/2] rad.");
        }
    }

    // Whether two vectors are at least the separation away from collinear: the sine of their angle, |û × ŵ|, at least
    // sin(separation), which for a separation in [1e-14, π/2] is the angle in [separation, π − separation], free of the rounding
    // of π − separation and of the resolution of acos near ±1; a zero vector (sine 0) and a vector that is not finite (NaN)
    // fail (fourth round of #668)
    private bool AreSeparated(in Vector3 a, in Vector3 b)
    {
        double sine = a.NormalizeOrZero().Cross(b.NormalizeOrZero()).Magnitude();
        return sine >= System.Math.Sin(MinimumVectorSeparation);
    }

    /// <summary>
    /// Creates a TRIAD attitude maneuver using an instrument's boresight and reference vector.
    /// The boresight points at the primary target, while the reference vector constrains roll toward the secondary target.
    /// </summary>
    /// <param name="minimumEpoch">Earliest epoch when the maneuver can execute.</param>
    /// <param name="maneuverHoldDuration">Duration to hold the attitude after achieving it.</param>
    /// <param name="instrument">The instrument whose boresight points at the primary target and whose reference vector
    /// constrains the roll toward the secondary target.</param>
    /// <param name="primaryTarget">The target of the boresight, also the maneuver's centre (a site's body for a site).</param>
    /// <param name="secondaryTarget">The target of the reference vector, for the roll.</param>
    /// <param name="engine">Engine used for the maneuver.</param>
    /// <param name="minimumVectorSeparation">Minimum angle between vectors in radians, in [1e-14, π/2] (default: 5 degrees).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minimumVectorSeparation"/> is not in [1e-14, π/2] (fourth
    /// round of #668).</exception>
    /// <exception cref="ArgumentException">The body vectors are closer than the separation to collinear, or one of them is
    /// zero or has a component that is not finite.</exception>
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
        CheckMinimumVectorSeparation(minimumVectorSeparation);
        if (instrument == null) throw new ArgumentNullException(nameof(instrument));
        PrimaryTarget = primaryTarget ?? throw new ArgumentNullException(nameof(primaryTarget));
        SecondaryTarget = secondaryTarget ?? throw new ArgumentNullException(nameof(secondaryTarget));
        MinimumVectorSeparation = minimumVectorSeparation;

        _primaryBodyVector = instrument.GetBoresightInSpacecraftFrame();
        _secondaryBodyVector = instrument.GetRefVectorInSpacecraftFrame();

        ValidateBodyVectors();
    }

    /// <summary>
    /// Creates a TRIAD attitude maneuver using two different instruments pointing at two targets.
    /// </summary>
    /// <param name="minimumEpoch">Earliest epoch when the maneuver can execute.</param>
    /// <param name="maneuverHoldDuration">Duration to hold the attitude after achieving it.</param>
    /// <param name="primaryInstrument">The instrument whose boresight points at the primary target.</param>
    /// <param name="primaryTarget">The target of the primary boresight, also the maneuver's centre (a site's body for a site).</param>
    /// <param name="secondaryInstrument">The instrument whose boresight constrains the roll toward the secondary target.</param>
    /// <param name="secondaryTarget">The target of the secondary boresight, for the roll.</param>
    /// <param name="engine">Engine used for the maneuver.</param>
    /// <param name="minimumVectorSeparation">Minimum angle between vectors in radians, in [1e-14, π/2] (default: 5 degrees).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minimumVectorSeparation"/> is not in [1e-14, π/2] (fourth
    /// round of #668).</exception>
    /// <exception cref="ArgumentException">The body vectors are closer than the separation to collinear, or one of them is
    /// zero or has a component that is not finite.</exception>
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
        CheckMinimumVectorSeparation(minimumVectorSeparation);
        if (primaryInstrument == null) throw new ArgumentNullException(nameof(primaryInstrument));
        if (secondaryInstrument == null) throw new ArgumentNullException(nameof(secondaryInstrument));
        PrimaryTarget = primaryTarget ?? throw new ArgumentNullException(nameof(primaryTarget));
        SecondaryTarget = secondaryTarget ?? throw new ArgumentNullException(nameof(secondaryTarget));
        MinimumVectorSeparation = minimumVectorSeparation;

        _primaryBodyVector = primaryInstrument.GetBoresightInSpacecraftFrame();
        _secondaryBodyVector = secondaryInstrument.GetBoresightInSpacecraftFrame();

        ValidateBodyVectors();
    }

    /// <summary>
    /// Creates a TRIAD attitude maneuver using explicit body frame vectors and ILocalizable targets.
    /// </summary>
    /// <param name="minimumEpoch">Earliest epoch when the maneuver can execute.</param>
    /// <param name="maneuverHoldDuration">Duration to hold the attitude after achieving it.</param>
    /// <param name="primaryBodyVector">Primary direction in spacecraft body frame.</param>
    /// <param name="primaryTarget">The target of the primary direction, also the maneuver's centre (a site's body for a site).</param>
    /// <param name="secondaryBodyVector">Secondary direction in body frame for roll constraint.</param>
    /// <param name="secondaryTarget">The target of the secondary direction, for the roll.</param>
    /// <param name="engine">Engine used for the maneuver.</param>
    /// <param name="minimumVectorSeparation">Minimum angle between vectors in radians, in [1e-14, π/2] (default: 5 degrees).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minimumVectorSeparation"/> is not in [1e-14, π/2] (fourth
    /// round of #668).</exception>
    /// <exception cref="ArgumentException">The body vectors are closer than the separation to collinear, or one of them is
    /// zero or has a component that is not finite.</exception>
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
        CheckMinimumVectorSeparation(minimumVectorSeparation);
        PrimaryTarget = primaryTarget ?? throw new ArgumentNullException(nameof(primaryTarget));
        SecondaryTarget = secondaryTarget ?? throw new ArgumentNullException(nameof(secondaryTarget));
        MinimumVectorSeparation = minimumVectorSeparation;

        if (primaryBodyVector.Magnitude() < double.Epsilon)
            throw new ArgumentException("Primary body vector cannot be zero.", nameof(primaryBodyVector));
        if (secondaryBodyVector.Magnitude() < double.Epsilon)
            throw new ArgumentException("Secondary body vector cannot be zero.", nameof(secondaryBodyVector));

        _primaryBodyVector = primaryBodyVector;
        _secondaryBodyVector = secondaryBodyVector;

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
    /// <param name="minimumVectorSeparation">Minimum angle between vectors in radians, in [1e-14, π/2] (default: 5 degrees).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minimumVectorSeparation"/> is not in [1e-14, π/2] (fourth
    /// round of #668).</exception>
    /// <exception cref="ArgumentException">The body vectors are closer than the separation to collinear, or one of them is
    /// zero or has a component that is not finite.</exception>
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
        CheckMinimumVectorSeparation(minimumVectorSeparation);
        PrimaryAttitudeTarget = primaryTarget ?? throw new ArgumentNullException(nameof(primaryTarget));
        SecondaryAttitudeTarget = secondaryTarget ?? throw new ArgumentNullException(nameof(secondaryTarget));
        MinimumVectorSeparation = minimumVectorSeparation;

        if (primaryBodyVector.Magnitude() < double.Epsilon)
            throw new ArgumentException("Primary body vector cannot be zero.", nameof(primaryBodyVector));
        if (secondaryBodyVector.Magnitude() < double.Epsilon)
            throw new ArgumentException("Secondary body vector cannot be zero.", nameof(secondaryBodyVector));

        _primaryBodyVector = primaryBodyVector;
        _secondaryBodyVector = secondaryBodyVector;

        // Map CelestialAttitudeTarget back to ILocalizable properties for compatibility
        if (primaryTarget is CelestialAttitudeTarget celestialPrimary)
            PrimaryTarget = celestialPrimary.Target;
        if (secondaryTarget is CelestialAttitudeTarget celestialSecondary)
            SecondaryTarget = celestialSecondary.Target;

        ValidateBodyVectors();
    }

    // The attitude of a factory: the public constructor on the static axes (its checks run on them), then the instance axes
    // that replace them at each use
    private TriadAttitude(
        CelestialItem maneuverCenter,
        Time minimumEpoch,
        TimeSpan maneuverHoldDuration,
        Vector3 primaryStaticAxis,
        Func<Spacecraft, Vector3> primaryAxis,
        IAttitudeTarget primaryTarget,
        Vector3 secondaryStaticAxis,
        Func<Spacecraft, Vector3> secondaryAxis,
        IAttitudeTarget secondaryTarget,
        Engine engine)
        : this(maneuverCenter, minimumEpoch, maneuverHoldDuration, primaryStaticAxis, primaryTarget, secondaryStaticAxis, secondaryTarget, engine)
    {
        _primaryAxis = primaryAxis;
        _secondaryAxis = secondaryAxis;
    }

    /// <summary>
    /// Creates an LVLH (Local Vertical Local Horizontal) attitude.
    /// Primary: the spacecraft's down axis toward nadir. Secondary: its front axis toward prograde.
    /// </summary>
    /// <remarks>
    /// The axes are those of the spacecraft that carries the engine's tank, <see cref="Spacecraft.BodyDown"/> and
    /// <see cref="Spacecraft.BodyFront"/>, read at execution as the single-vector attitudes read
    /// <see cref="Spacecraft.BodyFront"/>; the static <see cref="Spacecraft.Down"/> and <see cref="Spacecraft.Front"/> while
    /// the tank is on no spacecraft (#677), outside a propagation only: a propagation of the attitude on a tank that is not
    /// the propagated spacecraft's throws (#676). For the static axes on any spacecraft, use the constructor with
    /// <see cref="Spacecraft.Down"/> and <see cref="Spacecraft.Front"/>.
    /// </remarks>
    public static TriadAttitude CreateLVLH(
        CelestialItem maneuverCenter,
        Time minimumEpoch,
        TimeSpan maneuverHoldDuration,
        Engine engine)
    {
        return new TriadAttitude(
            maneuverCenter, minimumEpoch, maneuverHoldDuration,
            Spacecraft.Down, s => s.BodyDown, OrbitalDirectionTarget.Nadir,
            Spacecraft.Front, s => s.BodyFront, OrbitalDirectionTarget.Prograde,
            engine);
    }

    /// <summary>
    /// Creates a prograde attitude with sun tracking for solar panel orientation.
    /// Primary: the spacecraft's front axis toward prograde. Secondary: its up axis toward the Sun.
    /// </summary>
    /// <remarks>
    /// The axes are those of the spacecraft that carries the engine's tank, <see cref="Spacecraft.BodyFront"/> and
    /// <see cref="Spacecraft.BodyUp"/>, read at execution as the single-vector attitudes read
    /// <see cref="Spacecraft.BodyFront"/>; the static <see cref="Spacecraft.Front"/> and <see cref="Spacecraft.Up"/> while
    /// the tank is on no spacecraft (#677), outside a propagation only: a propagation of the attitude on a tank that is not
    /// the propagated spacecraft's throws (#676). For the static axes on any spacecraft, use the constructor with
    /// <see cref="Spacecraft.Front"/> and <see cref="Spacecraft.Up"/>.
    /// </remarks>
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
            Spacecraft.Front, s => s.BodyFront, OrbitalDirectionTarget.Prograde,
            Spacecraft.Up, s => s.BodyUp, new CelestialAttitudeTarget(sun),
            engine);
    }

    // The instance axis of a factory on the spacecraft of the engine's tank (Maneuver.GetBodyAxis, the rule of
    // GetBodyFront), the given vector for the public constructors
    private Vector3 ResolveAxis(Func<Spacecraft, Vector3> axis, Vector3 vector) => axis == null ? vector : GetBodyAxis(axis, vector);

    private static CelestialItem GetManeuverCenter(ILocalizable target)
    {
        return target is CelestialItem t ? t : (target as Site)?.CelestialBody;
    }

    private void ValidateBodyVectors()
    {
        if (!AreSeparated(PrimaryBodyVector, SecondaryBodyVector))
        {
            double angle = PrimaryBodyVector.Angle(SecondaryBodyVector);
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
    /// <exception cref="InvalidOperationException">The reference vectors are closer than
    /// <see cref="MinimumVectorSeparation"/> to collinear, or one of them is zero or not finite.</exception>
    public override Quaternion ComputeOrientation(StateVector stateVector)
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
            // ILocalizable ephemeris path — geometric, consistent with Attitude.TryExecute and IAttitudeTarget path
            var primaryEphemeris = PrimaryTarget.GetEphemeris(stateVector.Epoch, stateVector.Observer, stateVector.Frame, Aberration.None);
            var secondaryEphemeris = SecondaryTarget.GetEphemeris(stateVector.Epoch, stateVector.Observer, stateVector.Frame, Aberration.None);

            primaryRefVector = (primaryEphemeris.ToStateVector().Position - stateVector.Position).NormalizeOrZero();
            secondaryRefVector = (secondaryEphemeris.ToStateVector().Position - stateVector.Position).NormalizeOrZero();
        }

        // Validate reference vectors are not collinear
        if (!AreSeparated(primaryRefVector, secondaryRefVector))
        {
            double refAngle = primaryRefVector.Angle(secondaryRefVector);
            throw new InvalidOperationException(
                $"Reference vectors are too close to collinear at epoch {stateVector.Epoch}. " +
                $"Angle: {refAngle * Constants.Rad2Deg:F2} degrees. " +
                $"Minimum separation required: {MinimumVectorSeparation * Constants.Rad2Deg:F2} degrees.");
        }

        // Construct orthonormal triads using the NAIF pattern
        // Reference frame triad (where we want to point)
        var t1R = primaryRefVector;
        var t2R = t1R.Cross(secondaryRefVector).NormalizeOrZero();
        var t3R = t1R.Cross(t2R);

        // Body frame triad (spacecraft axes)
        var t1B = PrimaryBodyVector.NormalizeOrZero();
        var t2B = t1B.Cross(SecondaryBodyVector).NormalizeOrZero();
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
