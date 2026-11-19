// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Maneuver;

/// <summary>
/// An attitude target that computes direction toward a celestial body or site.
/// Wraps <see cref="ILocalizable"/> for use with the <see cref="IAttitudeTarget"/> interface.
/// </summary>
/// <remarks>
/// The aberration model controls how the target direction is computed:
/// <list type="bullet">
///   <item><description><see cref="Aberration.None"/> (default): geometric instantaneous direction — consistent with the attitude pipeline and industry standard (GMAT, Orekit and equivalent tools).</description></item>
///   <item><description><see cref="Aberration.LT"/>: light-time corrected apparent direction — use when modeling photon-based scenarios (optical sensors observing distant targets). Every correction other than <see cref="Aberration.None"/> is the target as the spacecraft sees it, as <c>spkcvo_c</c> corrects it from the spacecraft's state (#740).</description></item>
/// </list>
/// </remarks>
public class CelestialAttitudeTarget : IAttitudeTarget
{
    public ILocalizable Target { get; }

    /// <summary>
    /// The aberration correction applied when computing the target direction.
    /// </summary>
    public Aberration Aberration { get; }

    public string Name => (Target as INaifObject)?.Name ?? "CelestialTarget";

    /// <summary>
    /// Creates a celestial attitude target with configurable aberration correction.
    /// </summary>
    /// <param name="target">The celestial body or site to track.</param>
    /// <param name="aberration">Aberration correction to apply (default: <see cref="Aberration.None"/>).</param>
    public CelestialAttitudeTarget(ILocalizable target, Aberration aberration = Aberration.None)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Aberration = aberration;
    }

    /// <summary>
    /// The unit direction of <see cref="Target"/> from the point of <paramref name="observerState"/>, in its frame: the
    /// geometric direction for <see cref="Aberration.None"/>; otherwise the target as that point sees it, corrected as
    /// CSPICE's <c>spkcvo_c</c> corrects it, the light time of <c>spkltc</c> from the point's position and the stellar
    /// aberration of <c>stelab</c> (<c>stlabx</c> for transmission) from its velocity relative to the solar system
    /// barycenter (internal <c>CelestialItem.ApparentPositionFromState</c>, the rule of
    /// <see cref="InstrumentPointingToAttitude"/> since #549; a <see cref="Star"/> without light time). Before 11.0.0 the
    /// correction was that of the observer of the state, the central body: its light time, the stellar aberration of its
    /// velocity, and none for a target that is the central body itself (#740: 1.0e-6 rad for the Moon with LT, 2.5e-5 rad
    /// with LT+S, 1.0e-4 rad for the Earth with LT, from a LEO).
    /// </summary>
    /// <param name="observerState">The state of the spacecraft, relative to its observer, in an inertial frame.</param>
    /// <exception cref="ArgumentNullException"><paramref name="observerState"/> is null.</exception>
    public Vector3 GetDirection(StateVector observerState)
    {
        if (observerState == null) throw new ArgumentNullException(nameof(observerState));

        if (Aberration == Aberration.None)
        {
            var ephemeris = Target.GetEphemeris(observerState.Epoch, observerState.Observer, observerState.Frame, Aberration.None);
            return (ephemeris.ToStateVector().Position - observerState.Position).NormalizeOrZero();
        }

        return CelestialItem.ApparentPositionFromState(Target, observerState, Aberration).NormalizeOrZero();
    }
}
