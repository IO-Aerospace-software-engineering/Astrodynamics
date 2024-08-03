using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.Time;

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

        protected ImpulseManeuver(CelestialItem maneuverCenter, DateTime minimumEpoch, TimeSpan maneuverHoldDuration, Engine engine) : base(maneuverCenter, minimumEpoch, maneuverHoldDuration, engine)
        {
        }

        protected ImpulseManeuver(DateTime minimumEpoch, TimeSpan maneuverHoldDuration, OrbitalParameters.OrbitalParameters targetOrbit, Engine engine) : this(targetOrbit.Observer as CelestialItem, minimumEpoch,
            maneuverHoldDuration, engine)
        {
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
    }
}