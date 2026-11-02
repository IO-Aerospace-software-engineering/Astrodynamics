// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver;

/// <summary>
/// An attitude that points the boresight of an instrument at a target: the minimal rotation that takes the boresight, in
/// the spacecraft frame (<see cref="Instrument.GetBoresightInSpacecraftFrame"/>, the Euler angles of the frame kernel,
/// #524), to the direction of the target. The roll about the boresight is not constrained.
/// </summary>
public class InstrumentPointingToAttitude : Attitude
{
    public Instrument Instrument { get; }
    public ILocalizable Target { get; }

    public InstrumentPointingToAttitude(Time minimumEpoch, TimeSpan maneuverHoldDuration, Instrument instrument, ILocalizable target, Engine engine) : base(
        target is CelestialItem t ? t : (target as Site)?.CelestialBody, minimumEpoch, maneuverHoldDuration, engine)
    {
        Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    /// <summary>
    /// The rotation from the spacecraft frame to the frame of <paramref name="stateVector"/> that takes the boresight to
    /// the target, <c>direction = boresight.Rotate(q)</c>, as the other attitudes take their body vector to theirs.
    /// </summary>
    public override Quaternion ComputeOrientation(StateVector stateVector)
    {
        var ephemeris = Target.GetEphemeris(stateVector.Epoch, stateVector.Observer, stateVector.Frame, Aberration.LT);
        var targetVector = ephemeris.ToStateVector().Position - stateVector.Position;
        return Instrument.GetBoresightInSpacecraftFrame().To(targetVector);
    }
}