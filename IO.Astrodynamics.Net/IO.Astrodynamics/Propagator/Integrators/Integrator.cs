// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Integrators;

public abstract class Integrator : IIntegrator
{
    private readonly List<ForceBase> _forces;
    public IReadOnlyCollection<ForceBase> Forces => _forces;

    // The same list, indexable without an enumerator, for the RK7(8) stepper
    internal IReadOnlyList<ForceBase> ForceList => _forces;

    // Reference state info (from initial state, for constructing intermediate StateVectors)
    protected ILocalizable Observer { get; private set; }
    protected Frame ReferenceFrame { get; private set; }

    // Reusable StateVector for force evaluation (avoids per-step allocation)
    protected StateVector EvalState { get; private set; }

    // The mass and coefficients the forces are evaluated with, or null to read the spacecraft each force was built for
    private ForceEvaluationContext? _forceContext;

    /// <summary>
    /// The time argument of the integration variable, which maps t to the epoch of the forces (#501): set by the
    /// propagator from its central body (<see cref="PropagatorBase"/>), TDB by default. A subclass outside the library
    /// does not read it and builds its own epochs (<see cref="IIntegrator.IntegrateSegment"/>).
    /// </summary>
    internal virtual PropagationTimeArgument TimeArgument { get; set; } = PropagationTimeArgument.Tdb;

    public Integrator(IEnumerable<ForceBase> forces)
    {
        _forces = new List<ForceBase>(forces);
    }

    public Integrator()
    {
        _forces = new List<ForceBase>();
    }

    /// <summary>
    /// Add a force to the integrator's force collection.
    /// </summary>
    public void AddForce(ForceBase force)
    {
        _forces.Add(force);
    }

    // 1 once a propagation that builds its forces took this integrator (#674)
    private int _claimed;

    /// <summary>
    /// Takes this integrator for a propagation that builds its forces (the constructors of
    /// <see cref="CentralBodyPropagator"/> with celestial bodies, the builder, <c>Spacecraft.Propagate</c>, the batch, Monte
    /// Carlo and scenario paths), atomically: two threads given one instance cannot both take it (#674).
    /// </summary>
    /// <exception cref="ArgumentException">The integrator already holds forces, or a propagation already took it.</exception>
    internal void ClaimForForces()
    {
        if (_forces.Count > 0 || Interlocked.CompareExchange(ref _claimed, 1, 0) != 0)
        {
            throw new ArgumentException(
                "The integrator already holds forces or was already used by a propagation that builds its forces, which " +
                "would add its forces to those it holds: pass a new integrator to each propagation (for example " +
                "new RK78Integrator(...)), or give an integrator whose forces you added to the constructor of " +
                "CentralBodyPropagator without celestial bodies.", "integrator");
        }
    }

    /// <summary>
    /// Initialize the integrator with the initial state.
    /// Sets the observer and frame used for constructing intermediate StateVectors.
    /// Called by the propagator at construction, once the forces are added, and after each event.
    /// </summary>
    public virtual void Initialize(StateVector initialState)
    {
        SkipRoot = false;
        Observer = initialState.Observer;
        ReferenceFrame = initialState.Frame;
        EvalState = new StateVector(Vector3.Zero, Vector3.Zero, Observer, initialState.Epoch, ReferenceFrame);
    }

    /// <summary>
    /// Integrate a full segment from the given start state.
    /// </summary>
    public abstract IntegrationResult IntegrateSegment(
        Vector3 startPosition, Vector3 startVelocity,
        Time baseEpoch, double duration,
        IReadOnlyList<IEventDetector> eventDetectors = null);

    /// <summary>
    /// Compute the total acceleration at the given state by summing all forces.
    /// </summary>
    public Vector3 ComputeAcceleration(StateVector stateVector)
    {
        Vector3 res = Vector3.Zero;
        if (_forceContext.HasValue)
        {
            var context = _forceContext.Value;
            foreach (var force in Forces)
            {
                res += force.Apply(stateVector, context);
            }

            return res;
        }

        foreach (var force in Forces)
        {
            res += force.Apply(stateVector);
        }

        return res;
    }

    /// <summary>
    /// Evaluate the forces with this mass and these coefficients at every step, instead of the live spacecraft each force
    /// was built for (the default). For the propagation of an avoidance option, which reuses the forces of the
    /// trajectory it leaves: they would read the protected spacecraft as it is now, not the option after its burn
    /// (#415). A propagation without maneuver only: the context does not change along it.
    /// </summary>
    /// <param name="context">Mass and coefficients of the propagated spacecraft.</param>
    internal virtual void EvaluateForcesWith(in ForceEvaluationContext context)
    {
        _forceContext = context;
    }

    /// <summary>
    /// A new integrator with the settings of this one, without forces: the integrator of the propagation of an avoidance
    /// option with the forces of the trajectory it leaves (step 10, E4; #402). Null for an integrator whose settings
    /// cannot be copied.
    /// </summary>
    internal virtual Integrator WithSameSettings() => null;

    /// <summary>
    /// Update the reusable EvalState with new position, velocity, and epoch.
    /// </summary>
    protected void UpdateEvalState(in Vector3 position, in Vector3 velocity, in Time epoch)
    {
        EvalState.UpdatePosition(position);
        EvalState.UpdateVelocity(velocity);
        EvalState.UpdateEpoch(epoch);
    }

    /// <summary>
    /// Update any StateVector with new position, velocity, and epoch.
    /// Used by subclasses that maintain their own StateVector pools (e.g., RK78 stage pool).
    /// </summary>
    protected internal static void UpdateStateVector(StateVector sv, in Vector3 position, in Vector3 velocity, in Time epoch)
    {
        sv.UpdatePosition(position);
        sv.UpdateVelocity(velocity);
        sv.UpdateEpoch(epoch);
    }

    /// <summary>
    /// A new state at <paramref name="epoch"/> in the observer and frame of the integration, for an evaluation of the event
    /// detectors.
    /// </summary>
    internal StateVector StateAt(in Vector3 position, in Vector3 velocity, in Time epoch) =>
        new(position, velocity, Observer, epoch, ReferenceFrame);

    /// <summary>
    /// The state at <paramref name="t"/>, in seconds of the time argument from <paramref name="baseEpoch"/>, inside the
    /// accepted <paramref name="step"/>: the state at a gate (#569, <see cref="EventGates"/>). The cubic Hermite of the step
    /// by default, the state <see cref="PropagationSolution.InterpolateAt"/> reports there for a Velocity-Verlet solution,
    /// with no force evaluation.
    /// </summary>
    internal virtual StateVector StateInsideAcceptedStep(in AcceptedStep step, in Time baseEpoch, double t)
    {
        var (position, velocity) = PropagationSegment.HermiteInterpolate(step, t);
        return StateAt(position, velocity, TimeArgument.EpochAt(baseEpoch, t));
    }

    /// <summary>
    /// Check event detectors for sign changes: every active detector is evaluated at <paramref name="state"/>, the end of
    /// the step, into <paramref name="currG"/>.
    /// Returns the index of the first detector, in list order, whose g-function crossed zero in its direction since
    /// <paramref name="prevG"/>, or -1 if none. A g-value exactly zero at the step end after a non-zero one is a crossing
    /// (#580), its direction read from the sign before it.
    /// </summary>
    /// <remarks>
    /// The detector returned is the first in list order, which is not always the earliest crossing of the step: an
    /// integrator that takes several detectors reports the earliest one (#578), as <see cref="VVIntegrator"/> and
    /// <see cref="RK78Integrator"/> do.
    /// </remarks>
    protected static int CheckEventDetectors(
        IReadOnlyList<IEventDetector> detectors,
        StateVector state,
        double[] prevG, double[] currG)
    {
        if (detectors == null) return -1;

        int first = -1;
        for (int i = 0; i < detectors.Count; i++)
        {
            var detector = detectors[i];
            if (!detector.IsActive) continue;

            currG[i] = detector.Evaluate(state);
            if (first < 0 && Crosses(detector.Direction, prevG[i], currG[i]))
            {
                first = i;
            }
        }

        return first;
    }

    /// <summary>
    /// Whether g crossed zero in <paramref name="direction"/> from <paramref name="gPrev"/> to <paramref name="gCurr"/>:
    /// a change of sign, or a zero after a non-zero value (#580), the direction read from the sign of
    /// <paramref name="gPrev"/>. A zero followed by a non-zero value is not a crossing: the zero was one, or, at the base
    /// of a segment, is skipped (Hipparchus's convention; a gated detector's zero at the base follows
    /// <see cref="EventGates"/>).
    /// </summary>
    internal static bool Crosses(CrossingDirection direction, double gPrev, double gCurr)
    {
        if (!(gPrev * gCurr < 0.0 || (gCurr == 0.0 && gPrev != 0.0))) return false;

        return direction switch
        {
            CrossingDirection.NegativeToPositive => gPrev < 0.0,
            CrossingDirection.PositiveToNegative => gPrev > 0.0,
            CrossingDirection.Any => true,
            _ => false
        };
    }

    /// <summary>
    /// The detectors whose g-function crossed zero in their direction over the accepted step, in list order: every active
    /// detector is evaluated at <paramref name="state"/>, the end of the step, into <paramref name="currG"/>. A zero at a
    /// gate on the step end that the next step end judges (<see cref="EventGates"/>) is not a crossing here.
    /// </summary>
    /// <returns>The number of crossings, their indices first in <paramref name="crossed"/>.</returns>
    internal static int CrossingsInStep(IReadOnlyList<IEventDetector> detectors, StateVector state, double[] prevG,
        double[] currG, EventGates gates, int[] crossed)
    {
        int count = 0;
        for (int i = 0; i < detectors.Count; i++)
        {
            var detector = detectors[i];
            if (!detector.IsActive) continue;

            currG[i] = detector.Evaluate(state);
            if (gates != null && currG[i] == 0.0 && gates.JudgedAtNextStep(i)) continue;
            if (Crosses(detector.Direction, prevG[i], currG[i]))
            {
                crossed[count++] = i;
            }
        }

        return count;
    }

    // The detector of the last event and its g-value at the end of the step that contained the root, on the side past
    // the root; the next segment starts from that value when the propagation continues after the event (#355)
    private protected IEventDetector LastEventDetector;
    private protected double LastEventGPastRoot;
    private protected bool SkipRoot;

    /// <summary>
    /// Records the event just located: its detector and its g-value at the end of the step that holds its root, past the
    /// root, from which the next segment starts it when the propagation continues past the event
    /// (<see cref="SkipRootOfLastEvent"/>).
    /// </summary>
    private protected void RecordEvent(IEventDetector detector, double gPastRoot)
    {
        LastEventDetector = detector;
        LastEventGPastRoot = gPastRoot;
    }

    /// <summary>
    /// The next segment starts at the root of the last event, which the propagation continues past (an
    /// <see cref="EventAction.Continue"/>): its detector starts from its g-value past that root, so the root is not
    /// found again, and its gate, which that event decided, is not judged again. Call it after <see cref="Initialize"/>.
    /// </summary>
    /// <remarks>
    /// The event time is moved to the 100 ns grid of the epochs (in RK7(8) the midpoint of the last bisection bracket,
    /// in Velocity-Verlet the end of the step, #572), so the state the segment starts from can be just before the root,
    /// on the side the g-function crossed from. Its g-value would then make the first step cross the same root again.
    /// Starting from the sign past the root is the rule Hipparchus applies to a zero of a g-function at the start of a
    /// step (its <c>EventState</c>; confirmed by S. Guillet, 2026-10-13). A later crossing in the same direction needs a
    /// crossing in the other direction first, which a step of the integrator does not hold without being detected, so no
    /// event is lost.
    /// </remarks>
    internal void SkipRootOfLastEvent()
    {
        SkipRoot = LastEventDetector != null;
    }

    /// <summary>
    /// The g-values of the detectors at the base of a segment, at <paramref name="state"/>: 0 for an inactive detector;
    /// for a gated detector open at the base, its g-function from the start of a search
    /// (<see cref="IGatedEventDetector.EvaluateOpen"/>: a <see cref="Maneuver.PlaneAlignmentManeuver"/> takes the node
    /// it moves toward, #581), but for the detector of the event just handled, which starts from its g-value past that
    /// event's root (<see cref="SkipRootOfLastEvent"/>); <see cref="IEventDetector.Evaluate"/> otherwise.
    /// </summary>
    /// <returns>The g-values, and the detector of the event just handled, or null; the skip is consumed.</returns>
    internal (double[] Values, IEventDetector JustHandled) EventValuesAtBase(IReadOnlyList<IEventDetector> detectors,
        StateVector state)
    {
        var justHandled = SkipRoot ? LastEventDetector : null;
        SkipRoot = false;
        var values = new double[detectors.Count];
        for (int i = 0; i < detectors.Count; i++)
        {
            var detector = detectors[i];
            if (!detector.IsActive) continue;
            if (ReferenceEquals(detector, justHandled))
            {
                values[i] = LastEventGPastRoot;
            }
            else if (detector is IGatedEventDetector gated && !(state.Epoch < gated.Gate))
            {
                values[i] = gated.EvaluateOpen(state);
            }
            else
            {
                values[i] = detector.Evaluate(state);
            }
        }

        return (values, justHandled);
    }
}
