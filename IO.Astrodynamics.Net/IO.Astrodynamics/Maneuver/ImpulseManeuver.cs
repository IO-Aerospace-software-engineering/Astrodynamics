using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.OPM;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.Propagator.Events;
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

        /// <summary>
        /// The execution error of the maneuver (model of Gates, <see cref="ManeuverExecutionError"/>), added to the
        /// velocity covariance at the burn when the propagation carries a covariance
        /// (<see cref="Propagator.Variational.CovariancePropagationMode.Covariance"/>); ignored otherwise. Null (the
        /// default) for none. Set it with an object initializer, for example
        /// <c>new ApogeeHeightManeuver(…) { ExecutionError = new ManeuverExecutionError { ShutoffSigma = 0.002 } }</c>.
        /// </summary>
        /// <remarks>
        /// A ΔV of exactly zero adds no error; any non-zero ΔV takes the whole model, the fixed terms included.
        /// </remarks>
        public ManeuverExecutionError ExecutionError { get; init; }

        /// <summary>
        /// Industry-standard g-function. Zero-crossing triggers the maneuver.
        /// State is relative to ManeuverCenter (already transformed by ManeuverEventDetector).
        /// Must be continuous, smooth, and stateless — suitable for root-finding.
        /// </summary>
        public abstract double ComputeEventValue(StateVector localState);

        /// <summary>
        /// Which zero-crossing direction triggers this maneuver.
        /// </summary>
        public abstract CrossingDirection EventCrossingDirection { get; }

        /// <summary>
        /// The g-function at a state from which the search for the crossing starts, the state at the minimum epoch, and at
        /// the states of that search (#569): <see cref="ComputeEventValue"/> by default.
        /// </summary>
        /// <remarks>
        /// A maneuver whose first evaluation decides which crossing it waits for overrides it with the rule of a search
        /// start, where a crossing just passed does not count (<see cref="PlaneAlignmentManeuver"/>);
        /// <see cref="ComputeEventValue"/> keeps the rule of a state handed to the maneuver at its crossing.
        /// </remarks>
        internal virtual double ComputeEventValueAtSearchStart(StateVector localState) => ComputeEventValue(localState);

        /// <summary>
        /// Optional precondition check at event time (default: true).
        /// Override for maneuvers with additional constraints (e.g., CombinedManeuver).
        /// </summary>
        /// <remarks>
        /// When it returns false, the propagation skips this occurrence of the event and continues: the maneuver fires at
        /// the first later occurrence whose precondition holds. If none does within the propagation window, the
        /// maneuver is not executed: its <see cref="Maneuver.ThrustWindow"/> stays null and it remains the standby
        /// maneuver of the spacecraft.
        /// </remarks>
        public virtual bool CheckPreconditions(StateVector state) => true;

        protected ImpulseManeuver(CelestialItem maneuverCenter, Time minimumEpoch, TimeSpan maneuverHoldDuration, Engine engine) : base(maneuverCenter, minimumEpoch, maneuverHoldDuration, engine)
        {
        }

        /// <exception cref="ArgumentNullException"><paramref name="targetOrbit"/> or <paramref name="engine"/> is null.</exception>
        /// <exception cref="ArgumentException">The target orbit is defined neither in the frame of the initial orbit of the
        /// spacecraft of the engine nor in the frame it is propagated in (ICRF for a TLE or a TEME state, #675).</exception>
        /// <remarks>
        /// On a spacecraft whose initial orbit is a TLE or a TEME state, the propagation integrates in ICRF and takes the
        /// target in ICRF: a target in TEME is accepted here, as before, and rejected when the propagation is built.
        /// </remarks>
        protected ImpulseManeuver(Time minimumEpoch, TimeSpan maneuverHoldDuration, OrbitalParameters.OrbitalParameters targetOrbit, Engine engine) : this(
            RequireTargetOrbit(targetOrbit).Observer as CelestialItem, minimumEpoch, maneuverHoldDuration, engine)
        {
            var initial = engine.FuelTank.Spacecraft?.InitialOrbitalParameters;
            if (initial != null && initial.Frame != targetOrbit.Frame && PropagatorBase.PropagationFrame(initial) != targetOrbit.Frame)
            {
                throw new ArgumentException("Spacecraft and target orbit must be defined in the same frame");
            }

            TargetOrbit = targetOrbit;
        }

        /// <summary>
        /// The target orbit, checked before the constructor chain dereferences it (#552).
        /// </summary>
        private static OrbitalParameters.OrbitalParameters RequireTargetOrbit(OrbitalParameters.OrbitalParameters targetOrbit)
        {
            return targetOrbit ?? throw new ArgumentNullException(nameof(targetOrbit), "Target orbit must be defined");
        }

        /// <summary>
        /// Executes the impulse from <paramref name="stateVector"/>: computes its delta-V, burns its fuel, sets its windows in
        /// TDB seconds from the epoch of the state and raises the gate of the next maneuver of the chain to the end of its
        /// maneuver window (<see cref="Spacecraft.ChainStandbyManeuver"/>).
        /// </summary>
        /// <param name="stateVector">The state at the impulse. It is left unchanged.</param>
        /// <returns>
        /// The state after the impulse, a new state: the position, epoch, frame, observer and covariance of
        /// <paramref name="stateVector"/>, its velocity plus the delta-V; and the attitude of the impulse, its front along
        /// the delta-V, in ICRF.
        /// </returns>
        /// <remarks>
        /// <para>
        /// In a propagation, its detector calls it at the event; about the Earth the propagation then counts the windows in
        /// TT seconds from the time argument of the event and sets the gate of the next maneuver to that end (#547), and it
        /// holds the attitude from the start of the thrust window to the end of the maneuver window (#539). Called
        /// directly, the windows stay in TDB seconds.
        /// </para>
        /// <para>
        /// Before 11.0.0 the delta-V was added to the velocity of <paramref name="stateVector"/> itself, which was returned
        /// (#588): a spacecraft whose initial orbital parameters were that state, or Keplerian elements whose cached state it
        /// was, then started its next propagation from the state after the burn. The values returned are unchanged.
        /// </para>
        /// </remarks>
        public override (StateVector sv, StateOrientation so) TryExecute(StateVector stateVector)
        {
            var localSv = stateVector.RelativeTo(ManeuverCenter, Aberration.None).ToStateVector();

            // Ensure g-function state is initialized (e.g., PlaneAlignmentManeuver.IsAscendingNode).
            // In the propagator loop, ComputeEventValue is called by the event detector before TryExecute.
            // This call handles direct invocations (e.g., unit tests).
            ComputeEventValue(localSv);

            //Compute the deltaV
            DeltaV = Execute(localSv);

            //Burn required fuel, if not enought fuel an exception will be thrown
            FuelBurned = Engine.Ignite(DeltaV);

            //The state after the impulse, a new state: the given one is left unchanged (#588), since it may be the initial
            //orbital parameters of the spacecraft or the state that Keplerian elements cache
            var after = new StateVector(stateVector.Position, stateVector.Velocity + DeltaV, stateVector.Observer, stateVector.Epoch,
                stateVector.Frame, stateVector.Covariance);

            //Compute thrust windows and maneuver windows.
            //The impulse is applied at the event epoch t. The equivalent finite burn of duration dt is placed on
            //[t - 0.666 dt, t + 0.334 dt]: two thirds of the burn before the impulse, one third after.
            //This value dates back to the initial import of the .NET project and no derivation of it is on record.
            //The usual impulsive approximation of a finite burn centres the burn on the impulse (50 %).
            //The propagated state receives the full delta-V at t. The windows are reported, and the end of the
            //maneuver window sets the earliest epoch of the next maneuver in the chain.
            //They count TDB seconds from the epoch of the state; a propagation about the Earth counts them again in TT
            //seconds from the time argument of its event, and replaces the gate of the next maneuver with that end (#547).
            var thrustDuration = Tsiolkovski.DeltaT(Engine.ISP, Engine.FuelTank.Spacecraft.GetTotalMass() + FuelBurned, Engine.FuelFlow, DeltaV.Magnitude());
            ThrustWindow = new Window(stateVector.Epoch - thrustDuration * 0.666, thrustDuration);
            ManeuverWindow = new Window(ThrustWindow.Value.StartDate, ManeuverHoldDuration).Merge(ThrustWindow.Value);

            //Set next maneuver
            Engine.FuelTank.Spacecraft.ChainStandbyManeuver(this.NextManeuver, ManeuverWindow.Value.EndDate);

            //return computed state vector and state orientation
            //The front along the delta-V, in ICRF whatever the frame of the state (#529, #535)
            var deltaVInIcrf = stateVector.Frame == Frames.Frame.ICRF
                ? DeltaV
                : DeltaV.Rotate(stateVector.Frame.ToFrame(Frames.Frame.ICRF, stateVector.Epoch).Rotation);
            return (after, new StateOrientation(GetBodyFront().To(deltaVInIcrf), Vector3.Zero, stateVector.Epoch, Frames.Frame.ICRF));
        }

        internal override void Reset()
        {
            DeltaV = Vector3.Zero;
            base.Reset();
        }

        /// <summary>
        /// Converts this executed maneuver to CCSDS OPM maneuver parameters.
        /// </summary>
        /// <param name="referenceFrame">The reference frame label for delta-V components. Defaults to "EME2000".
        /// Note: Delta-V is stored in the inertial frame of the propagated state, not in a local orbital frame.
        /// The components are written unchanged whatever the label: labelling an <see cref="Frames.Frame.ICRF"/>
        /// delta-V as EME2000 ignores the ~23 mas frame bias between the two (about 1.1e-7 relative on the components).</param>
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
        /// <para>
        /// <c>MAN_EPOCH_IGNITION</c> is written in UTC, the time system of the OPM the library writes, whatever the time
        /// frame of the thrust window (TDB in a propagation, #479).
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
                manEpochIgnition: ThrustWindow.Value.StartDate.ToUTC().DateTime,
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