// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
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
/// Four constructors, all with <see cref="IAttitudeTarget"/> targets (decision 69 of #668, decided by S. Guillet,
/// 2026-11-23): two <see cref="BodyAxis"/> axes of the spacecraft, resolved on the spacecraft of the plan at each use as
/// the single-vector attitudes resolve theirs (<see cref="Maneuver.GetBodyAxis"/>, #677); two literal body vectors (an
/// off-axis antenna, a solar-array normal); an instrument's boresight and reference vector; or the boresights of two
/// instruments. The factories <see cref="CreateLvlh"/> and <see cref="CreateProgradeWithSunTracking"/> are the
/// <see cref="BodyAxis"/> shape. A celestial target is a <see cref="CelestialAttitudeTarget"/>, which carries its
/// correction; the constructors of <c>ILocalizable</c> targets, and <c>PrimaryAttitudeTarget</c> and
/// <c>SecondaryAttitudeTarget</c>, are removed in lot L5b.
/// </para>
/// <para>
/// Example usage with spacecraft directions:
/// <code>
/// // Point spacecraft Front at Moon, keep Up toward Sun
/// new TriadAttitude(BodyAxis.Front, new CelestialAttitudeTarget(moon), BodyAxis.Up, new CelestialAttitudeTarget(sun));
/// </code>
/// </para>
/// </remarks>
public class TriadAttitude : Attitude
{
    /// <summary>
    /// The target of the primary body vector: an orbital direction or a celestial target, never null. An
    /// <c>ILocalizable</c> before lot L5b, beside <c>PrimaryAttitudeTarget</c> (decision 69 of #668).
    /// </summary>
    public IAttitudeTarget PrimaryTarget { get; }

    /// <summary>
    /// The target of the secondary body vector, which constrains the roll about the primary one; never null.
    /// </summary>
    public IAttitudeTarget SecondaryTarget { get; }

    /// <summary>
    /// Primary direction in spacecraft body frame that should point toward <see cref="PrimaryTarget"/>.
    /// </summary>
    /// <remarks>
    /// The vector given to the constructor, or the instrument's; for the <see cref="BodyAxis"/> shape (the factories
    /// included), the axis of the spacecraft whose plan last held the attitude, read at each call, or the static axis
    /// before any plan (#677; decisions 46 and 69 of #668).
    /// </remarks>
    public Vector3 PrimaryBodyVector => _primaryAxis.HasValue ? GetBodyAxis(_primaryAxis.Value) : _primaryBodyVector;

    /// <summary>
    /// Secondary direction in spacecraft body frame used to constrain roll, toward <see cref="SecondaryTarget"/>.
    /// </summary>
    /// <remarks>
    /// The vector given to the constructor, or the instrument's; for the <see cref="BodyAxis"/> shape, the axis of the
    /// spacecraft whose plan last held the attitude, read at each call, or the static axis before any plan.
    /// </remarks>
    public Vector3 SecondaryBodyVector => _secondaryAxis.HasValue ? GetBodyAxis(_secondaryAxis.Value) : _secondaryBodyVector;

    private readonly Vector3 _primaryBodyVector;
    private readonly Vector3 _secondaryBodyVector;

    // The axes of the BodyAxis shape, read on the spacecraft of the last plan (Maneuver.GetBodyAxis); null for the other
    // shapes
    private readonly BodyAxis? _primaryAxis;
    private readonly BodyAxis? _secondaryAxis;

    // The instruments of the Instrument forms, null for the other forms: the plan and the run check that they are
    // instruments of the spacecraft propagated (#797, decision 66 of #668, decided by S. Guillet, 2026-11-23); before,
    // the forms copied their vectors and kept no reference, so no check could see whose instrument it was. The body
    // vectors stay those copied at construction, the instrument's vectors being fixed once it is built
    private readonly Instrument _primaryInstrument;
    private readonly Instrument _secondaryInstrument;

    internal override System.Collections.Generic.IEnumerable<Instrument> PointedInstruments
    {
        get
        {
            if (_primaryInstrument != null) yield return _primaryInstrument;
            if (_secondaryInstrument != null && !ReferenceEquals(_secondaryInstrument, _primaryInstrument)) yield return _secondaryInstrument;
        }
    }

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
    /// <c>MinimumVectorSeparation</c> before 11.0.0 (D2 choice 10 and decision 70 of #668, in lot L5b, decided by
    /// S. Guillet, 2026-11-23).
    /// </remarks>
    public double MinVectorSeparation { get; }

    private const double DefaultMinimumSeparation = 5.0 * Constants.Deg2Rad;

    // The smallest separation: 20 times the bound (3 + √2) u = 4.9e-16 of |û × ŵ| for vectors collinear to the rounding
    // of their components (u = 2^-53 each for the input, each normalization and the cross product)
    private const double LowestMinimumSeparation = 1e-14;

    // A separation is in [1e-14, π/2] rad (fourth round of #668): 0, a negative one or NaN switched off both collinearity
    // checks, one below the rounding of the sine let collinear vectors that round apart through, and one above π/2
    // rejected every pair, perpendicular ones included
    private static void CheckMinVectorSeparation(double minVectorSeparation)
    {
        if (!(minVectorSeparation >= LowestMinimumSeparation && minVectorSeparation <= System.Math.PI / 2.0))
        {
            throw new ArgumentOutOfRangeException(nameof(minVectorSeparation), minVectorSeparation,
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
        return sine >= System.Math.Sin(MinVectorSeparation);
    }

    // The common part of the constructors: the targets and the separation, checked first
    private TriadAttitude(IAttitudeTarget primaryTarget, IAttitudeTarget secondaryTarget, Time minEpoch, TimeSpan holdDuration,
        CelestialItem maneuverCenter, double minVectorSeparation) : base(minEpoch, holdDuration, maneuverCenter)
    {
        CheckMinVectorSeparation(minVectorSeparation);
        PrimaryTarget = primaryTarget ?? throw new ArgumentNullException(nameof(primaryTarget));
        SecondaryTarget = secondaryTarget ?? throw new ArgumentNullException(nameof(secondaryTarget));
        MinVectorSeparation = minVectorSeparation;
    }

    /// <summary>
    /// A TRIAD attitude that points the axis <paramref name="primaryBodyAxis"/> of the spacecraft at
    /// <paramref name="primaryTarget"/> and turns <paramref name="secondaryBodyAxis"/> toward
    /// <paramref name="secondaryTarget"/>.
    /// </summary>
    /// <param name="primaryBodyAxis">The axis of the spacecraft toward the primary target, resolved on the spacecraft of
    /// the plan at each use (<see cref="Maneuver.GetBodyAxis"/>).</param>
    /// <param name="primaryTarget">The primary target, an orbital direction or a celestial target.</param>
    /// <param name="secondaryBodyAxis">The axis that constrains the roll.</param>
    /// <param name="secondaryTarget">The secondary target.</param>
    /// <param name="minEpoch">Earliest epoch when the maneuver can execute; default(Time) for none.</param>
    /// <param name="holdDuration">Duration to hold the attitude; zero by default.</param>
    /// <param name="maneuverCenter">The central body; null for the centre the run infers (decisions 61, 62 and 99 of
    /// #668: required in SSB mode unless both targets are library <see cref="CelestialAttitudeTarget"/>s, which take the
    /// central body of the run).</param>
    /// <param name="minVectorSeparation">Minimum angle between vectors in radians, in [1e-14, π/2] (default: 5 degrees).</param>
    /// <exception cref="ArgumentNullException">A target is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minVectorSeparation"/> is not in [1e-14, π/2], an
    /// axis is not a value of <see cref="BodyAxis"/>, or <paramref name="holdDuration"/> is negative.</exception>
    /// <exception cref="ArgumentException">The two axes are the same or opposite, checked on the static axes, which every
    /// spacecraft's instance axes repeat (orthonormal, right-handed).</exception>
    public TriadAttitude(BodyAxis primaryBodyAxis, IAttitudeTarget primaryTarget, BodyAxis secondaryBodyAxis, IAttitudeTarget secondaryTarget,
        Time minEpoch = default, TimeSpan holdDuration = default, CelestialItem maneuverCenter = null,
        double minVectorSeparation = DefaultMinimumSeparation)
        : this(primaryTarget, secondaryTarget, minEpoch, holdDuration, maneuverCenter, minVectorSeparation)
    {
        _primaryAxis = CheckBodyAxis(primaryBodyAxis, nameof(primaryBodyAxis));
        _secondaryAxis = CheckBodyAxis(secondaryBodyAxis, nameof(secondaryBodyAxis));
        // The static axes, before any plan: the check of the axes
        _primaryBodyVector = StaticAxis(primaryBodyAxis);
        _secondaryBodyVector = StaticAxis(secondaryBodyAxis);
        ValidateBodyVectors(_primaryBodyVector, _secondaryBodyVector);
    }

    /// <summary>
    /// A TRIAD attitude that points the literal body vector <paramref name="primaryBodyVector"/> at
    /// <paramref name="primaryTarget"/> and turns <paramref name="secondaryBodyVector"/> toward
    /// <paramref name="secondaryTarget"/>: a body direction that is neither an axis of the spacecraft nor an instrument's.
    /// </summary>
    /// <param name="primaryBodyVector">Primary direction in spacecraft body frame.</param>
    /// <param name="primaryTarget">The primary target, an orbital direction or a celestial target.</param>
    /// <param name="secondaryBodyVector">Secondary direction in body frame for roll constraint.</param>
    /// <param name="secondaryTarget">The secondary target.</param>
    /// <param name="minEpoch">Earliest epoch when the maneuver can execute; default(Time) for none.</param>
    /// <param name="holdDuration">Duration to hold the attitude; zero by default.</param>
    /// <param name="maneuverCenter">The central body; null for the centre the run infers (decisions 61, 62 and 99 of
    /// #668).</param>
    /// <param name="minVectorSeparation">Minimum angle between vectors in radians, in [1e-14, π/2] (default: 5 degrees).</param>
    /// <exception cref="ArgumentNullException">A target is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minVectorSeparation"/> is not in [1e-14, π/2] (fourth
    /// round of #668), or <paramref name="holdDuration"/> is negative.</exception>
    /// <exception cref="ArgumentException">The body vectors are closer than the separation to collinear, or one of them is
    /// zero or has a component that is not finite.</exception>
    public TriadAttitude(Vector3 primaryBodyVector, IAttitudeTarget primaryTarget, Vector3 secondaryBodyVector, IAttitudeTarget secondaryTarget,
        Time minEpoch = default, TimeSpan holdDuration = default, CelestialItem maneuverCenter = null,
        double minVectorSeparation = DefaultMinimumSeparation)
        : this(primaryTarget, secondaryTarget, minEpoch, holdDuration, maneuverCenter, minVectorSeparation)
    {
        if (primaryBodyVector.Magnitude() < double.Epsilon)
            throw new ArgumentException("Primary body vector cannot be zero.", nameof(primaryBodyVector));
        if (secondaryBodyVector.Magnitude() < double.Epsilon)
            throw new ArgumentException("Secondary body vector cannot be zero.", nameof(secondaryBodyVector));

        _primaryBodyVector = primaryBodyVector;
        _secondaryBodyVector = secondaryBodyVector;
        ValidateBodyVectors(_primaryBodyVector, _secondaryBodyVector);
    }

    /// <summary>
    /// A TRIAD attitude that points the boresight of <paramref name="instrument"/> at <paramref name="primaryTarget"/> and
    /// turns its reference vector toward <paramref name="secondaryTarget"/>.
    /// </summary>
    /// <param name="instrument">The instrument, of the spacecraft propagated (#797).</param>
    /// <param name="primaryTarget">The target of the boresight.</param>
    /// <param name="secondaryTarget">The target of the reference vector, for the roll.</param>
    /// <param name="minEpoch">Earliest epoch when the maneuver can execute; default(Time) for none.</param>
    /// <param name="holdDuration">Duration to hold the attitude; zero by default.</param>
    /// <param name="maneuverCenter">The central body; null for the centre the run infers (decisions 61, 62 and 99 of
    /// #668; before lot L5b, the primary target, or a site's body).</param>
    /// <param name="minVectorSeparation">Minimum angle between vectors in radians, in [1e-14, π/2] (default: 5 degrees).</param>
    /// <exception cref="ArgumentNullException">The instrument or a target is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minVectorSeparation"/> is not in [1e-14, π/2] (fourth
    /// round of #668), or <paramref name="holdDuration"/> is negative.</exception>
    /// <exception cref="ArgumentException">The body vectors are closer than the separation to collinear, or one of them is
    /// zero or has a component that is not finite.</exception>
    public TriadAttitude(Instrument instrument, IAttitudeTarget primaryTarget, IAttitudeTarget secondaryTarget,
        Time minEpoch = default, TimeSpan holdDuration = default, CelestialItem maneuverCenter = null,
        double minVectorSeparation = DefaultMinimumSeparation)
        : this(primaryTarget, secondaryTarget, minEpoch, holdDuration, maneuverCenter, minVectorSeparation)
    {
        _primaryInstrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
        _primaryBodyVector = instrument.GetBoresightInSpacecraftFrame();
        _secondaryBodyVector = instrument.GetRefVectorInSpacecraftFrame();
        ValidateBodyVectors(_primaryBodyVector, _secondaryBodyVector);
    }

    /// <summary>
    /// A TRIAD attitude that points the boresight of <paramref name="primaryInstrument"/> at
    /// <paramref name="primaryTarget"/> and turns the boresight of <paramref name="secondaryInstrument"/> toward
    /// <paramref name="secondaryTarget"/>.
    /// </summary>
    /// <param name="primaryInstrument">The instrument whose boresight points at the primary target.</param>
    /// <param name="primaryTarget">The target of the primary boresight.</param>
    /// <param name="secondaryInstrument">The instrument whose boresight constrains the roll.</param>
    /// <param name="secondaryTarget">The target of the secondary boresight.</param>
    /// <param name="minEpoch">Earliest epoch when the maneuver can execute; default(Time) for none.</param>
    /// <param name="holdDuration">Duration to hold the attitude; zero by default.</param>
    /// <param name="maneuverCenter">The central body; null for the centre the run infers (decisions 61, 62 and 99 of
    /// #668; before lot L5b, the primary target, or a site's body).</param>
    /// <param name="minVectorSeparation">Minimum angle between vectors in radians, in [1e-14, π/2] (default: 5 degrees).</param>
    /// <exception cref="ArgumentNullException">An instrument or a target is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minVectorSeparation"/> is not in [1e-14, π/2] (fourth
    /// round of #668), or <paramref name="holdDuration"/> is negative.</exception>
    /// <exception cref="ArgumentException">The body vectors are closer than the separation to collinear, or one of them is
    /// zero or has a component that is not finite.</exception>
    public TriadAttitude(Instrument primaryInstrument, IAttitudeTarget primaryTarget, Instrument secondaryInstrument, IAttitudeTarget secondaryTarget,
        Time minEpoch = default, TimeSpan holdDuration = default, CelestialItem maneuverCenter = null,
        double minVectorSeparation = DefaultMinimumSeparation)
        : this(primaryTarget, secondaryTarget, minEpoch, holdDuration, maneuverCenter, minVectorSeparation)
    {
        _primaryInstrument = primaryInstrument ?? throw new ArgumentNullException(nameof(primaryInstrument));
        _secondaryInstrument = secondaryInstrument ?? throw new ArgumentNullException(nameof(secondaryInstrument));
        _primaryBodyVector = primaryInstrument.GetBoresightInSpacecraftFrame();
        _secondaryBodyVector = secondaryInstrument.GetBoresightInSpacecraftFrame();
        ValidateBodyVectors(_primaryBodyVector, _secondaryBodyVector);
    }

    /// <summary>
    /// Creates an LVLH (Local Vertical Local Horizontal) attitude: the spacecraft's down axis toward nadir, its front axis
    /// toward prograde; the <see cref="BodyAxis"/> shape of <see cref="BodyAxis.Down"/> and <see cref="BodyAxis.Front"/>.
    /// </summary>
    /// <param name="minEpoch">Earliest epoch when the maneuver can execute; default(Time) for none.</param>
    /// <param name="holdDuration">Duration to hold the attitude; zero by default.</param>
    /// <param name="maneuverCenter">The central body; null for the centre the run infers (required in SSB mode: the
    /// orbital directions read it).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="holdDuration"/> is negative.</exception>
    /// <remarks>
    /// The axes are those of the spacecraft whose plan last held the attitude, <see cref="Spacecraft.BodyDown"/> and
    /// <see cref="Spacecraft.BodyFront"/>, read at execution as the single-vector attitudes read theirs
    /// (<see cref="Maneuver.GetBodyAxis"/>); the static <see cref="Spacecraft.Down"/> and <see cref="Spacecraft.Front"/>
    /// before any plan (#677), outside a propagation only: a run propagates the spacecraft of the plan. For the static
    /// axes on any spacecraft, use the <see cref="Vector3"/> shape with <see cref="Spacecraft.Down"/> and
    /// <see cref="Spacecraft.Front"/>. <c>CreateLVLH(maneuverCenter, minimumEpoch, maneuverHoldDuration, engine)</c> before
    /// 11.0.0 (D2's casing of acronyms and D1 rule 8 of #668, in lot L5b, decided by S. Guillet, 2026-11-23).
    /// </remarks>
    public static TriadAttitude CreateLvlh(Time minEpoch = default, TimeSpan holdDuration = default, CelestialItem maneuverCenter = null)
    {
        return new TriadAttitude(BodyAxis.Down, OrbitalDirectionTarget.Nadir, BodyAxis.Front, OrbitalDirectionTarget.Prograde, minEpoch,
            holdDuration, maneuverCenter);
    }

    /// <summary>
    /// Creates a prograde attitude with sun tracking for solar panel orientation: the spacecraft's front axis toward
    /// prograde, its up axis toward the Sun, geometric (<c>new CelestialAttitudeTarget(sun)</c>).
    /// </summary>
    /// <param name="sun">The Sun the up axis turns toward.</param>
    /// <param name="minEpoch">Earliest epoch when the maneuver can execute; default(Time) for none.</param>
    /// <param name="holdDuration">Duration to hold the attitude; zero by default.</param>
    /// <param name="maneuverCenter">The central body; null for the centre the run infers (required in SSB mode: the
    /// prograde direction reads it).</param>
    /// <exception cref="ArgumentNullException"><paramref name="sun"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="holdDuration"/> is negative.</exception>
    /// <remarks>
    /// The axes are those of the spacecraft whose plan last held the attitude, <see cref="Spacecraft.BodyFront"/> and
    /// <see cref="Spacecraft.BodyUp"/>, read at execution (<see cref="Maneuver.GetBodyAxis"/>); the static
    /// <see cref="Spacecraft.Front"/> and <see cref="Spacecraft.Up"/> before any plan (#677). The Sun stays a required
    /// argument (decision 70 of #668): a Sun built in the factory would read the data provider at the call and ignore the
    /// caller's. Before 11.0.0 <c>(maneuverCenter, minimumEpoch, maneuverHoldDuration, sun, engine)</c>.
    /// </remarks>
    public static TriadAttitude CreateProgradeWithSunTracking(ILocalizable sun, Time minEpoch = default, TimeSpan holdDuration = default,
        CelestialItem maneuverCenter = null)
    {
        if (sun == null) throw new ArgumentNullException(nameof(sun));
        return new TriadAttitude(BodyAxis.Front, OrbitalDirectionTarget.Prograde, BodyAxis.Up, new CelestialAttitudeTarget(sun), minEpoch,
            holdDuration, maneuverCenter);
    }

    // The static axis of a BodyAxis, the axis before any plan (Spacecraft.Front, ...)
    private static Vector3 StaticAxis(BodyAxis axis) => axis switch
    {
        BodyAxis.Front => Spacecraft.Front,
        BodyAxis.Back => Spacecraft.Back,
        BodyAxis.Right => Spacecraft.Right,
        BodyAxis.Left => Spacecraft.Left,
        BodyAxis.Up => Spacecraft.Up,
        _ => Spacecraft.Down
    };

    // Whether the law reads its centre (decision 99 of #668, decided by S. Guillet, 2026-11-24): not when both targets are
    // library CelestialAttitudeTargets, which then take the central body of the run when the centre cannot be inferred;
    // an orbital-direction or user target reads it
    internal override bool ReadsCenter => !(IsCelestial(PrimaryTarget) && IsCelestial(SecondaryTarget));

    // A library CelestialAttitudeTarget, not a subclass
    private static bool IsCelestial(IAttitudeTarget target) => target.GetType() == typeof(CelestialAttitudeTarget);

    private void ValidateBodyVectors(in Vector3 primary, in Vector3 secondary)
    {
        if (!AreSeparated(primary, secondary))
        {
            double angle = primary.Angle(secondary);
            throw new ArgumentException(
                $"Body vectors are too close to collinear. Angle: {angle * Constants.Rad2Deg:F2} degrees. " +
                $"Minimum separation required: {MinVectorSeparation * Constants.Rad2Deg:F2} degrees.");
        }
    }

    /// <summary>
    /// Computes the spacecraft orientation using the TRIAD algorithm.
    /// </summary>
    /// <param name="stateVector">Current spacecraft state vector.</param>
    /// <returns>Quaternion representing the spacecraft orientation.</returns>
    /// <exception cref="InvalidOperationException">The reference vectors are closer than
    /// <see cref="MinVectorSeparation"/> to collinear, or one of them is zero or not finite.</exception>
    public override Quaternion ComputeOrientation(StateVector stateVector)
    {
        // The directions of the targets, orbital or celestial (IAttitudeTarget; the ILocalizable path, the same expression
        // as CelestialAttitudeTarget's geometric one, left with its constructors in lot L5b, decision 69 of #668)
        var primaryRefVector = PrimaryTarget.GetDirection(stateVector);
        var secondaryRefVector = SecondaryTarget.GetDirection(stateVector);

        // Validate reference vectors are not collinear
        if (!AreSeparated(primaryRefVector, secondaryRefVector))
        {
            double refAngle = primaryRefVector.Angle(secondaryRefVector);
            throw new InvalidOperationException(
                $"Reference vectors are too close to collinear at epoch {stateVector.Epoch}. " +
                $"Angle: {refAngle * Constants.Rad2Deg:F2} degrees. " +
                $"Minimum separation required: {MinVectorSeparation * Constants.Rad2Deg:F2} degrees.");
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
