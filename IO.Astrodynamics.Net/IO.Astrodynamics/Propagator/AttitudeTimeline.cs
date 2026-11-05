// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// The attitude timeline of a propagation (#539): the activations of its maneuver chain in epoch order, each in force from
/// its epoch to the next one. An attitude law (<see cref="Attitude"/>) is sampled on the solution over its whole interval
/// once the integration is done; the attitude of an impulse (its front along ΔV) is discrete, held over its interval.
/// </summary>
/// <remarks>
/// The epochs come from the maneuver chain: an attitude activates at its <see cref="Maneuver.Maneuver.MinimumEpoch"/> or
/// at the end of the previous maneuver's <see cref="Maneuver.Maneuver.ManeuverWindow"/>, whichever is later; the attitude
/// of an impulse at the opening of its thrust window, no earlier than the start of the propagation or the last activation;
/// after the <see cref="Maneuver.Maneuver.ManeuverWindow"/> of an impulse, the law in force before it resumes, unless an
/// attitude chained after the impulse activates there, later in the chain, at the same epoch.
/// </remarks>
internal sealed class AttitudeTimeline
{
    /// <summary>An activation: an attitude law (with <see cref="Law"/>), or a discrete attitude (with
    /// <see cref="Discrete"/>, the rotation to ICRF at its epoch). <see cref="Resumed"/>: the law in force before an
    /// impulse, resumed at the end of its maneuver window.</summary>
    internal readonly record struct Activation(Time Epoch, Attitude Law, StateOrientation Discrete, bool Resumed);

    private readonly List<Activation> _activations = new();

    /// <summary>The activations, in the order of the chain, which is the order of their epochs.</summary>
    internal IReadOnlyList<Activation> Activations => _activations;

    /// <summary>The attitude law activated last, null before any.</summary>
    internal Attitude CurrentLaw { get; private set; }

    /// <summary>The epoch of the last activation, null before any.</summary>
    internal Time? LastEpoch => _activations.Count > 0 ? _activations[^1].Epoch : null;

    /// <summary>Activates <paramref name="law"/> at <paramref name="epoch"/>, resumed after an impulse or not.</summary>
    internal void ActivateLaw(in Time epoch, Attitude law, bool resumed = false)
    {
        _activations.Add(new Activation(epoch, law, null, resumed));
        CurrentLaw = law;
    }

    /// <summary>Activates the discrete attitude <paramref name="orientation"/> at <paramref name="epoch"/>; the current law
    /// is kept, to resume after it.</summary>
    internal void ActivateDiscrete(in Time epoch, StateOrientation orientation)
    {
        _activations.Add(new Activation(epoch, null, orientation with { Epoch = epoch }, false));
    }
}
