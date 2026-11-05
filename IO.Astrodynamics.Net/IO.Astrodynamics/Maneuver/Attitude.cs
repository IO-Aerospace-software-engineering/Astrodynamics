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
/// In a propagation, the law is in force from its activation, at its <see cref="Maneuver.MinimumEpoch"/> or at the end of
/// the window of the previous maneuver of the chain, whichever is later, to the next activation of the chain: it is
/// sampled on the solution over that whole interval, and its hold delays the next maneuver (#539,
/// docs/reference/attitudes.md). An attitude that would activate after the end of the propagation is not executed.
/// </remarks>
public abstract class Attitude : Maneuver
{
    public StateOrientation StateOrientation { get; private set; }
    public Attitude(CelestialItem maneuverCenter, Time minimumEpoch, TimeSpan maneuverHoldDuration, Engine engine) : base(maneuverCenter, minimumEpoch, maneuverHoldDuration, engine)
    {
    }

    protected override Vector3 Execute(StateVector vector)
    {
        return Vector3.Zero;
    }

    public abstract Quaternion ComputeOrientation(StateVector stateVector);

    public override (StateVector sv, StateOrientation so) TryExecute(StateVector stateVector)
    {
        //Compute maneuver window
        if (!ThrustWindow.HasValue || !ManeuverWindow.HasValue)
        {
            ThrustWindow = new Window(stateVector.Epoch, TimeSpan.Zero);
            ManeuverWindow = new Window(stateVector.Epoch, ManeuverHoldDuration);
        }

        //If state vector is outside maneuver windows the next maneuver can be set
        if (stateVector.Epoch > ManeuverWindow.Value.EndDate)
        {
            Engine.FuelTank.Spacecraft.ChainStandbyManeuver(this.NextManeuver, ManeuverWindow.Value.EndDate);
        }

        // Computed in ICRF, whatever the frame of the state (#529, #535): the rotation from the spacecraft frame to ICRF
        var localSv = stateVector.RelativeTo(ManeuverCenter, Aberration.None).ToStateVector();
        if (localSv.Frame != Frames.Frame.ICRF)
        {
            localSv = localSv.ToFrame(Frames.Frame.ICRF).ToStateVector();
        }

        StateOrientation = new StateOrientation(ComputeOrientation(localSv), Vector3.Zero, stateVector.Epoch, Frames.Frame.ICRF);
        
        //Return state vector and computed state orientation
        return (stateVector, StateOrientation);
    }
}