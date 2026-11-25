// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver;

/// <summary>
/// An attitude law of the maneuver chain: a rotation from the spacecraft frame to ICRF computed on the state
/// (<see cref="ComputeOrientation"/>).
/// </summary>
/// <remarks>
/// In a propagation, the law is in force from its activation, at its <see cref="Maneuver.MinEpoch"/> or at the end of
/// the window of the previous maneuver of the chain, whichever is later, to the next activation of the chain: it is
/// sampled on the solution over that whole interval, and its hold delays the next maneuver (#539,
/// docs/reference/attitudes.md). An attitude that would activate after the end of the propagation is not executed.
/// </remarks>
public abstract class Attitude : Maneuver
{
    /// <summary>
    /// An attitude law activated at or after <paramref name="minEpoch"/>, held for <paramref name="holdDuration"/>, about
    /// <paramref name="maneuverCenter"/>.
    /// </summary>
    /// <param name="minEpoch">The earliest epoch of its activation; default(Time) for none.</param>
    /// <param name="holdDuration">Its hold, zero or positive; zero by default.</param>
    /// <param name="maneuverCenter">The centre the law reads; null for the centre the run infers (decision 61 of #668),
    /// required in SSB mode, where it cannot be inferred: a user law reads it, as far as the library knows (decision 99 of
    /// #668, decided by S. Guillet, 2026-11-24).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="holdDuration"/> is negative.</exception>
    /// <remarks>
    /// An attitude takes no engine since lot L5b (decisions 48 and 93 of #668, decided by S. Guillet, 2026-11-23): it
    /// burns nothing, and orients the spacecraft whose plan holds it (<see cref="Spacecraft.SetManeuvers"/>), whose axes
    /// <see cref="Maneuver.GetBodyAxis"/> reads; before, a satellite without a thruster needed a tank and an engine for
    /// it. Its parameters follow D1 rule 8 (the targets of a law, then the optional <c>minEpoch</c>,
    /// <c>holdDuration</c> and <c>maneuverCenter</c>); before 11.0.0 <c>(maneuverCenter, minimumEpoch,
    /// maneuverHoldDuration, engine)</c>.
    /// </remarks>
    public Attitude(Time minEpoch = default, TimeSpan holdDuration = default, CelestialItem maneuverCenter = null) : base(maneuverCenter, minEpoch, holdDuration)
    {
    }

    public abstract Quaternion ComputeOrientation(StateVector stateVector);

    /// <summary>
    /// The attitude of the law at <paramref name="stateVector"/>: the rotation from the spacecraft frame to ICRF, without
    /// executing the attitude (decision 50 of #668, decided by S. Guillet, 2026-11-23).
    /// </summary>
    /// <param name="stateVector">The state, in any frame and about any observer: it is taken about the centre the maneuver
    /// was given, as the execution takes it, or about its own observer when it was given none, never about the centre a
    /// run resolved (<see cref="Maneuver.ManeuverCenter"/> after a run), then rotated to ICRF when it is in another
    /// frame.</param>
    /// <returns>The rotation from the spacecraft frame to ICRF (<see cref="ComputeOrientation"/> on that state in ICRF), at
    /// the epoch of the state, with a zero angular velocity, labelled ICRF.</returns>
    /// <remarks>
    /// Pure: it sets no window and moves no chain, so it is the same before and after a run. At the same state it is the
    /// orientation of the execution: for a maneuver given its centre, whatever the observer of the state; for an inferred
    /// centre, at a state about the centre the run resolves, as the states of its solution are (IC-6 of lot L5b of #668). Before 11.0.0 the preview was the public
    /// <c>TryExecute</c>, which set the windows of the attitude, could move the standby chain of a finished run, and kept
    /// its result in <c>StateOrientation</c>, which no re-arm cleared (#802); in a run, the attitude at its activation is
    /// in the attitude record (<see cref="SpacecraftFrame.GetStateOrientationToICRF"/>).
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="stateVector"/> is null.</exception>
    public StateOrientation ComputeStateOrientation(StateVector stateVector)
    {
        ArgumentNullException.ThrowIfNull(stateVector);
        var local = PreviewState(stateVector);
        if (local.Frame != Frames.Frame.ICRF)
        {
            local = local.ToFrame(Frames.Frame.ICRF).ToStateVector();
        }

        return new StateOrientation(ComputeOrientation(local), Vector3.Zero, stateVector.Epoch, Frames.Frame.ICRF);
    }

    internal override (StateVector sv, StateOrientation so) TryExecute(StateVector stateVector)
    {
        //Compute maneuver window: a run sets it at the activation, before this call (the chain advances on the attitude
        //timeline, PropagatorBase.ScheduleAttitudes)
        if (!ThrustWindow.HasValue || !ManeuverWindow.HasValue)
        {
            ThrustWindow = new Window(stateVector.Epoch, TimeSpan.Zero);
            ManeuverWindow = new Window(stateVector.Epoch, HoldDuration);
        }

        // Computed in ICRF, whatever the frame of the state (#529, #535): the rotation from the spacecraft frame to ICRF
        var localSv = stateVector.RelativeTo(ManeuverCenter, Aberration.None).ToStateVector();
        if (localSv.Frame != Frames.Frame.ICRF)
        {
            localSv = localSv.ToFrame(Frames.Frame.ICRF).ToStateVector();
        }

        //Return state vector and computed state orientation
        return (stateVector, new StateOrientation(ComputeOrientation(localSv), Vector3.Zero, stateVector.Epoch, Frames.Frame.ICRF));
    }
}