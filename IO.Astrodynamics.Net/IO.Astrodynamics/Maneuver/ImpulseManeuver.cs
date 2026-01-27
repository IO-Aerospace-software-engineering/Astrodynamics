using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.OPM;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver
{
    public abstract class ImpulseManeuver : Maneuver
    {
        /// <summary>
        /// Gets the target orbital parameters.
        /// </summary>
        /// <returns>The target orbital parameters.</returns>
        public OrbitalParameters.OrbitalParameters TargetOrbit { get; protected set; }

        public Vector3 DeltaV { get; internal set; }

        protected ImpulseManeuver(CelestialItem maneuverCenter, Time minimumEpoch, TimeSpan maneuverHoldDuration, Engine engine) : base(maneuverCenter, minimumEpoch, maneuverHoldDuration, engine)
        {
        }

        protected ImpulseManeuver(Time minimumEpoch, TimeSpan maneuverHoldDuration, OrbitalParameters.OrbitalParameters targetOrbit, Engine engine) : this(targetOrbit.Observer as CelestialItem, minimumEpoch,
            maneuverHoldDuration, engine)
        {
            if(engine.FuelTank.Spacecraft!=null && engine.FuelTank.Spacecraft.InitialOrbitalParameters.Frame!=targetOrbit.Frame)
            {
                throw new ArgumentException("Spacecraft and target orbit must be defined in the same frame");
            }
            
            if (targetOrbit == null)
            {
                throw new ArgumentException("Target orbit must be define");
            }

            TargetOrbit = targetOrbit;
        }

        public override (StateVector sv, StateOrientation so) TryExecute(StateVector stateVector)
        {
            var localSv = stateVector.RelativeTo(ManeuverCenter, Aberration.None).ToStateVector();
            //Compute the deltaV
            DeltaV = Execute(localSv);

            //Burn required fuel, if not enought fuel an exception will be thrown
            FuelBurned = Engine.Ignite(DeltaV);

            //Update DeltaV
            stateVector.UpdateVelocity(stateVector.Velocity + DeltaV);

            //Compute thrust windows and maneuver windows
            var thrustDuration = Tsiolkovski.DeltaT(Engine.ISP, Engine.FuelTank.Spacecraft.GetTotalMass() + FuelBurned, Engine.FuelFlow, DeltaV.Magnitude());
            ThrustWindow = new Window(stateVector.Epoch - thrustDuration * 0.666, thrustDuration);
            ManeuverWindow = new Window(ThrustWindow.Value.StartDate, ManeuverHoldDuration).Merge(ThrustWindow.Value);

            //Set next maneuver
            Engine.FuelTank.Spacecraft.SetStandbyManeuver(this.NextManeuver, ManeuverWindow.Value.EndDate);

            //return computed state vector and state orientation
            return (stateVector, new StateOrientation(Spacecraft.Front.To(DeltaV), Vector3.Zero, stateVector.Epoch, stateVector.Frame));
        }

        internal override void Reset()
        {
            DeltaV = Vector3.Zero;
            base.Reset();
        }

        /// <summary>
        /// Converts this executed maneuver to CCSDS OPM maneuver parameters.
        /// </summary>
        /// <param name="referenceFrame">The reference frame for delta-V components. Defaults to "EME2000".
        /// Note: Delta-V is stored in the inertial frame (e.g., ICRF/EME2000), not in a local orbital frame.</param>
        /// <param name="comments">Optional comments to include with the maneuver.</param>
        /// <returns>An OpmManeuverParameters instance representing this maneuver.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the maneuver has not been executed (ThrustWindow not set).</exception>
        /// <remarks>
        /// <para>
        /// This method exports the executed maneuver data for CCSDS OPM archival. The delta-V components
        /// are converted from the framework's m/s to CCSDS km/s units.
        /// </para>
        /// <para>
        /// The delta-V is expressed in the inertial frame (EME2000/ICRF) by default. For local orbital frames
        /// (RSW, RTN, TNW), additional transformation would be needed based on the spacecraft state at the
        /// maneuver epoch.
        /// </para>
        /// </remarks>
        public OpmManeuverParameters ToOpmManeuverParameters(string referenceFrame = "EME2000", IReadOnlyList<string> comments = null)
        {
            if (!ThrustWindow.HasValue)
            {
                throw new InvalidOperationException("Cannot convert to OPM maneuver parameters: maneuver has not been executed (ThrustWindow not set).");
            }

            // Convert delta-V from m/s to km/s
            var deltaVKmPerSec = DeltaV / 1000.0;

            return new OpmManeuverParameters(
                manEpochIgnition: ThrustWindow.Value.StartDate.DateTime,
                manDuration: ThrustWindow.Value.Length.TotalSeconds,
                manDeltaMass: -FuelBurned, // OPM uses negative values for expelled mass
                manRefFrame: referenceFrame,
                manDv1: deltaVKmPerSec.X,
                manDv2: deltaVKmPerSec.Y,
                manDv3: deltaVKmPerSec.Z,
                comments: comments);
        }
    }
}