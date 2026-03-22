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
///   <item><description><see cref="Aberration.None"/> (default): geometric instantaneous direction — consistent with the attitude pipeline and industry standard (GMAT, STK, OREKIT).</description></item>
///   <item><description><see cref="Aberration.LT"/>: light-time corrected apparent direction — use when modeling photon-based scenarios (optical sensors observing distant targets).</description></item>
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

    public Vector3 GetDirection(StateVector observerState)
    {
        if (observerState == null) throw new ArgumentNullException(nameof(observerState));

        var ephemeris = Target.GetEphemeris(observerState.Epoch, observerState.Observer, observerState.Frame, Aberration);
        return (ephemeris.ToStateVector().Position - observerState.Position).Normalize();
    }
}
