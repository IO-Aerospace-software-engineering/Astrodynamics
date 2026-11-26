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

        /// <summary>
        /// The delta-V of the last run that executed this maneuver, in the frame of its propagation: the last run's value,
        /// cleared by the re-arm of the next run (decision 52 of #668, decided by S. Guillet, 2026-11-23); zero before. The
        /// preview of a delta-V is <see cref="ComputeDeltaV"/>.
        /// </summary>
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
        /// <remarks>
        /// Pure on every library maneuver (decision 50 of #668, decided by S. Guillet, 2026-11-23): before 11.0.0 a call
        /// locked the node of a <see cref="PlaneAlignmentManeuver"/> and wrote the intersection of an
        /// <see cref="ApsidalAlignmentManeuver"/> (#802). The propagation locks the node through its detector
        /// (<see cref="ComputeEventValueInRun"/>).
        /// </remarks>
        public abstract double ComputeEventValue(StateVector localState);

        /// <summary>
        /// The g-function as the propagation evaluates it, through its <see cref="ManeuverEventDetector"/> and the execution:
        /// <see cref="ComputeEventValue"/> by default; a maneuver whose first evaluation decides which crossing it waits for
        /// locks it here (<see cref="PlaneAlignmentManeuver"/>, decision 50 of #668).
        /// </summary>
        internal virtual double ComputeEventValueInRun(StateVector localState) => ComputeEventValue(localState);

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
        internal virtual double ComputeEventValueAtSearchStart(StateVector localState) => ComputeEventValueInRun(localState);

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

        /// <summary>
        /// The engine of the impulse: the one it was given (<see cref="GivenEngine"/>), null when it was given none, until
        /// a run resolves it; the engine the last run used, written right after its re-arm (decisions 61 and 63 of #668,
        /// decided by S. Guillet, 2026-11-23): the given one, else the only engine of the spacecraft
        /// (<see cref="Spacecraft.Engines"/>, decision 92). The re-arm of the next run restores the given value, and a
        /// stopped run keeps the resolved one.
        /// </summary>
        /// <remarks>
        /// On <see cref="ImpulseManeuver"/> since lot L5b (decisions 48 and 93 of #668, decided by S. Guillet, 2026-11-23):
        /// it was on <see cref="Maneuver"/>, and an attitude needed an engine to reach its spacecraft. A run whose impulse
        /// was given no engine throws <see cref="InvalidOperationException"/> from the propagator constructor and from
        /// <c>Propagate</c> when the spacecraft holds no engine or several (decision 64).
        /// </remarks>
        public Engine Engine { get; internal set; }

        /// <summary>
        /// The engine the impulse was given, null for one the run infers: what every check before a run reads
        /// (<see cref="Spacecraft.SetManeuvers"/>, the engine check of the run), never <see cref="Engine"/>, which a run
        /// writes.
        /// </summary>
        internal Engine GivenEngine { get; }

        /// <summary>
        /// An impulse about <paramref name="maneuverCenter"/>, burning on <paramref name="engine"/>, at or after
        /// <paramref name="minEpoch"/>, held for <paramref name="holdDuration"/>.
        /// </summary>
        /// <param name="engine">The engine; null for the only engine of the spacecraft, which the run infers.</param>
        /// <param name="minEpoch">The earliest epoch of the maneuver; default(Time) for none.</param>
        /// <param name="holdDuration">The hold, zero or positive; zero by default.</param>
        /// <param name="maneuverCenter">The centre of the maneuver; null for the centre the run infers (required in SSB
        /// mode).</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="holdDuration"/> is negative.</exception>
        /// <remarks>
        /// D1 rule 8 of #668 (lot L5b, decided by S. Guillet, 2026-11-23): the target of a library maneuver first, then
        /// the optional <c>engine</c>, <c>minEpoch</c>, <c>holdDuration</c> and <c>maneuverCenter</c>; before 11.0.0
        /// <c>(maneuverCenter, minimumEpoch, maneuverHoldDuration, engine)</c>, each required.
        /// </remarks>
        protected ImpulseManeuver(Engine engine = null, Time minEpoch = default, TimeSpan holdDuration = default,
            CelestialItem maneuverCenter = null) : base(maneuverCenter, minEpoch, holdDuration)
        {
            GivenEngine = Engine = engine;
        }

        /// <summary>
        /// The delta-V of the impulse at <paramref name="stateVector"/>: what the maneuver adds to its velocity, without
        /// executing it (decision 50 of #668, decided by S. Guillet, 2026-11-23).
        /// </summary>
        /// <param name="stateVector">The state at the impulse, in any frame and about any observer: it is taken about the
        /// centre the maneuver was given, as the execution takes it, or about its own observer when it was given none,
        /// never about the centre a run resolved (<see cref="Maneuver.ManeuverCenter"/> after a run).</param>
        /// <returns>The delta-V, in the frame of <paramref name="stateVector"/>.</returns>
        /// <remarks>
        /// Pure: it touches no tank, chain, window, gate or node lock, so it is the same before and after a run. Before
        /// 11.0.0 the preview was the public <c>TryExecute</c>, which burnt the fuel, set the windows and moved the
        /// standby chain of a finished run (#802). At the state of its event in a run, it is the delta-V the run applies
        /// (<see cref="DeltaV"/>): for a maneuver given its centre, whatever the observer of the state (an SSB-mode
        /// solution's states are about the Sun); for an inferred centre, at a state about the centre the run resolves, as
        /// the states of its solution are (IC-6 of lot L5b of #668): a
        /// <see cref="PlaneAlignmentManeuver"/> takes the node of the rule of <see cref="ComputeEventValue"/> at the state,
        /// which is the node the run locked at its crossing.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="stateVector"/> is null.</exception>
        public Vector3 ComputeDeltaV(StateVector stateVector)
        {
            ArgumentNullException.ThrowIfNull(stateVector);
            return Execute(PreviewState(stateVector));
        }

        /// <summary>
        /// The delta-V of the impulse at <paramref name="stateVector"/>, the state relative to the centre of the maneuver;
        /// pure, as <see cref="ComputeDeltaV"/>, which returns it.
        /// </summary>
        /// <remarks>
        /// On <see cref="ImpulseManeuver"/> since 11.0.0, where it was on <see cref="Maneuver"/> (decision 48 of #668,
        /// decided by S. Guillet, 2026-11-23); the library maneuvers compute it without writing anything (decision 50).
        /// </remarks>
        protected abstract Vector3 Execute(StateVector stateVector);

        /// <summary>
        /// The delta-V as the propagation executes the impulse, at its event: <see cref="Execute"/> by default; the library
        /// maneuvers that keep a side result of the execution write it here (decision 50 of #668).
        /// </summary>
        internal virtual Vector3 ExecuteInRun(StateVector localState) => Execute(localState);

        /// <summary>
        /// An impulse toward <paramref name="targetOrbit"/>, about its observer, burning on <paramref name="engine"/>, at
        /// or after <paramref name="minEpoch"/>, held for <paramref name="holdDuration"/>.
        /// </summary>
        /// <param name="targetOrbit">The target orbit; its observer is the centre of the maneuver.</param>
        /// <param name="engine">The engine; null for the only engine of the spacecraft, which the run infers.</param>
        /// <param name="minEpoch">The earliest epoch of the maneuver; default(Time) for none.</param>
        /// <param name="holdDuration">The hold, zero or positive; zero by default.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targetOrbit"/> is null.</exception>
        /// <exception cref="ArgumentException">The observer of <paramref name="targetOrbit"/> is not a
        /// <see cref="CelestialItem"/> (a site): the centre of a maneuver is a celestial item (IC-4 of lot L5b of
        /// #668).</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="holdDuration"/> is negative.</exception>
        /// <remarks>
        /// The frame of the target is checked by <see cref="Spacecraft.SetManeuvers"/> and by the propagation, against the
        /// frame the spacecraft is propagated in (#685): since lot L5b the constructor checks it no more (decision 65 of
        /// #668, decided by S. Guillet, 2026-11-23; before, it compared the target with the initial orbit of the spacecraft
        /// of the engine's tank when there was one, a weaker rule than the propagation's, which accepted a target in TEME
        /// on a TLE spacecraft that the propagation rejected).
        /// </remarks>
        protected ImpulseManeuver(OrbitalParameters.OrbitalParameters targetOrbit, Engine engine = null, Time minEpoch = default,
            TimeSpan holdDuration = default) : this(engine, minEpoch, holdDuration, CenterOf(targetOrbit))
        {
            TargetOrbit = targetOrbit;
        }

        /// <summary>
        /// The centre of a target-orbit form: the observer of <paramref name="targetOrbit"/>, checked not null (#552,
        /// #555) and a <see cref="CelestialItem"/> (IC-4 of lot L5b of #668: otherwise a null given centre would become
        /// one the run infers).
        /// </summary>
        private protected static CelestialItem CenterOf(OrbitalParameters.OrbitalParameters targetOrbit)
        {
            return RequireTargetOrbit(targetOrbit).Observer as CelestialItem ?? throw new ArgumentException(
                $"The observer of the target orbit, {(targetOrbit.Observer as INaifObject)?.Name ?? targetOrbit.Observer?.GetType().Name}, is not a celestial item: " +
                "the centre of a maneuver is a celestial body or a barycentre. Give the target orbit about a celestial body.", nameof(targetOrbit));
        }

        /// <summary>
        /// Why <paramref name="center"/> cannot be the centre of this impulse, null when it can: the rule of the apogee of
        /// <see cref="ApogeeHeightManeuver"/> (decision 68 of #668); checked by the constructor for a given centre and by
        /// the run for an inferred one.
        /// </summary>
        internal virtual string CenterError(CelestialItem center) => null;

        /// <summary>
        /// The target orbit, checked before the constructor chain dereferences it (#552); the target-orbit constructors of
        /// <see cref="ApogeeHeightManeuver"/> and <see cref="PerigeeHeightManeuver"/> too (#555).
        /// </summary>
        private protected static OrbitalParameters.OrbitalParameters RequireTargetOrbit(OrbitalParameters.OrbitalParameters targetOrbit)
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
        internal override (StateVector sv, StateOrientation so) TryExecute(StateVector stateVector)
        {
            var localSv = stateVector.RelativeTo(ManeuverCenter, Aberration.None).ToStateVector();

            // Ensure g-function state is initialized (e.g., PlaneAlignmentManeuver.IsAscendingNode).
            // In the propagator loop, the event detector evaluates it before TryExecute.
            // This call handles direct invocations (e.g., unit tests).
            ComputeEventValueInRun(localSv);

            //Compute the deltaV
            DeltaV = ExecuteInRun(localSv);

            //Burn required fuel, if not enought fuel an exception will be thrown
            FuelBurned = Engine.Ignite(DeltaV);

            //The state after the impulse, a new state: the given one is left unchanged (#588), since it may be the initial
            //orbital parameters of the spacecraft or the state that Keplerian elements cache
            var after = new StateVector(stateVector.Position, stateVector.Velocity + DeltaV, stateVector.Observer, stateVector.Epoch,
                stateVector.Frame, stateVector.Covariance);

            //Compute thrust windows and maneuver windows.
            //The impulse is applied at the event epoch t, the centroid of the acceleration of the equivalent constant-thrust
            //burn of duration dt (#335, derived from S. Guillet's remark of 2026-11-18, decided by S. Guillet, 2026-11-18): the burn is placed on [t - f dt, t + (1 - f) dt],
            //f = 1/x + 1/ln(1 - x) for the mass fraction burned x (ThrustLeadFraction), 1/2 for a small burn and more as the
            //acceleration grows while the mass drops. Before 11.0.0, f was a fixed 0.666 of no recorded derivation.
            //The propagated state receives the full delta-V at t. The windows are reported, and the end of the
            //maneuver window sets the earliest epoch of the next maneuver in the chain.
            //They count TDB seconds from the epoch of the state; a propagation about the Earth counts them again in TT
            //seconds from the time argument of its event, and replaces the gate of the next maneuver with that end (#547).
            var thrustDuration = Tsiolkovski.DeltaT(Engine.ISP, Engine.FuelTank.Spacecraft.GetTotalMass() + FuelBurned, Engine.FuelFlow, DeltaV.Magnitude());
            ThrustLead = ThrustLeadFraction(DeltaV.Magnitude(), Engine.ISP);
            ThrustWindow = new Window(stateVector.Epoch - thrustDuration * ThrustLead, thrustDuration);
            ManeuverWindow = new Window(ThrustWindow.Value.StartDate, HoldDuration).Hull(ThrustWindow.Value);

            //Set next maneuver: the plan's spacecraft, the propagated one in a run (decisions 46 and 48 of #668); none for an
            //impulse in no plan
            PlanOwner?.ChainStandbyManeuver(this.NextManeuver, ManeuverWindow.Value.EndDate);

            //return computed state vector and state orientation
            //The front along the delta-V, in ICRF whatever the frame of the state (#529, #535); a zero delta-V (a target
            //already reached) keeps the identity, as the attitude record holds for no attitude, bit for bit as before 11.0.0,
            //through the internal path of Vector3.To, which throws for a zero target (review of #680's known limitation)
            var deltaVInIcrf = stateVector.Frame == Frames.Frame.ICRF
                ? DeltaV
                : DeltaV.Rotate(stateVector.Frame.ToFrame(Frames.Frame.ICRF, stateVector.Epoch).Rotation);
            return (after, new StateOrientation(GetBodyAxis(BodyAxis.Front).ToOrIdentity(deltaVInIcrf), Vector3.Zero, stateVector.Epoch, Frames.Frame.ICRF));
        }

        /// <summary>
        /// The fraction f of the burn duration dt by which the thrust window of the last execution opens before the impulse
        /// epoch t: <see cref="Maneuver.ThrustWindow"/> is [t - f dt, t + (1 - f) dt] (<see cref="ThrustLeadFraction"/>).
        /// </summary>
        internal double ThrustLead { get; private set; } = 0.5;

        /// <summary>
        /// The fraction f of the burn of a constant thrust and fuel flow that precedes the centroid of its acceleration
        /// (#335, derived from S. Guillet's remark of 2026-11-18, decided by S. Guillet, 2026-11-18): with the mass fraction burned x = Δm/m0 = 1 − exp(−u),
        /// u = Δv / (Isp g0) (Tsiolkovsky, m0 the mass before the burn), the acceleration F / (m0 − q τ) over the burn
        /// [0, dt] has its centroid at f dt, f = 1/x + 1/ln(1 − x) = 1/(1 − exp(−u)) − 1/u. The impulse at the centroid
        /// gives the burn's velocity change and, in a field-free space, its displacement after the burn
        /// (∫ (T − s) a(s) ds = Δv (T − f dt)). f → 1/2 as x → 0 (1/2 + u/12 − u³/720 + u⁵/30240 − u⁷/1209600 +
        /// u⁹/47900160, the series of u/(1 − exp(−u)) by the Bernoulli numbers, used below u = 0.1, where its next term
        /// is below 1e-20; the closed form above, where the cancellation of its two terms leaves about 1e-14), 0.509 at
        /// x = 0.1, 0.557 at 0.5, 2/3 only at x ≈ 0.89, and f → 1 as x → 1.
        /// </summary>
        /// <param name="deltaV">The magnitude of the delta-V, m/s.</param>
        /// <param name="isp">The specific impulse of the engine, s.</param>
        /// <returns>The fraction f, in [1/2, 1).</returns>
        internal static double ThrustLeadFraction(double deltaV, double isp)
        {
            double u = deltaV / (isp * Constants.g0);
            if (u < 0.1)
            {
                double u2 = u * u;
                return 0.5 + u * (1.0 / 12.0 + u2 * (-1.0 / 720.0 + u2 * (1.0 / 30240.0 + u2 * (-1.0 / 1209600.0 +
                    u2 / 47900160.0))));
            }

            return 1.0 / (1.0 - System.Math.Exp(-u)) - 1.0 / u;
        }

        internal override void Reset()
        {
            DeltaV = Vector3.Zero;
            ThrustLead = 0.5;
            Engine = GivenEngine;
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
                manDurationSeconds: ThrustWindow.Value.Length.TotalSeconds,
                manDeltaMass: -FuelBurned, // OPM uses negative values for expelled mass
                manRefFrame: referenceFrame,
                manDv1: deltaVKmPerSec.X,
                manDv2: deltaVKmPerSec.Y,
                manDv3: deltaVKmPerSec.Z,
                comments: comments);
        }
    }
}