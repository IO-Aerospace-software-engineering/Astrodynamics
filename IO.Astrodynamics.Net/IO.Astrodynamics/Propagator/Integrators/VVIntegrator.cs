// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Integrators;

/// <summary>
/// Velocity-Verlet (symplectic) fixed-step integrator.
/// Produces a PropagationSegment with all accepted steps for dense output interpolation.
/// </summary>
/// <remarks>
/// <para>
/// The acceleration at the end of a step is evaluated at the new position with the predicted end velocity
/// v(t) + h a(t), so that a force that depends on the velocity, as drag, keeps the integrator of second order (#439; with
/// the half-step velocity, as before 11.0.0, it was of first order with drag). Forces that do not depend on the velocity
/// give the same accelerations bit for bit.
/// </para>
/// <para>
/// An event fires at the end of the step that holds its crossing, a g-value exactly zero there included (#580); of
/// several crossings in one step, the earliest, each root estimated on the cubic Hermite of the step (#578). The step is
/// then integrated again from its start to the time of the label of its end, up to half a tick from it, so that the
/// event time, the state reported and the base of the next segment are the same instant (#572). In the step that holds
/// the gate of a gated detector (the minimum epoch of a maneuver, #569), the g-value at the gate, on the cubic Hermite of
/// the step, replaces the previous g-value (<see cref="EventGates"/>).
/// </para>
/// </remarks>
public sealed class VVIntegrator : Integrator
{
    public TimeSpan DeltaT { get; }
    // The step in seconds, and its half (internal since 11.0.0, R06 of #668: DeltaT is the step)
    internal double DeltaTs { get; }
    internal double HalfDeltaTs { get; }

    /// <summary>
    /// A Velocity-Verlet integrator with its forces, initialized at <paramref name="initialState"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaT"/> is not positive.</exception>
    public VVIntegrator(IEnumerable<ForceBase> forces, TimeSpan deltaT, StateVector initialState) : base(forces)
    {
        DeltaT = PropagatorBase.RequirePositiveStep(deltaT, nameof(deltaT));
        DeltaTs = DeltaT.TotalSeconds;
        HalfDeltaTs = DeltaTs * 0.5;
        Initialize(initialState);
    }

    /// <summary>
    /// A Velocity-Verlet integrator of step <paramref name="deltaT"/>, without forces.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaT"/> is not positive.</exception>
    public VVIntegrator(TimeSpan deltaT) : base()
    {
        DeltaT = PropagatorBase.RequirePositiveStep(deltaT, nameof(deltaT));
        DeltaTs = DeltaT.TotalSeconds;
        HalfDeltaTs = DeltaTs * 0.5;
    }

    /// <inheritdoc />
    internal override Integrator WithSameSettings() => new VVIntegrator(DeltaT);

    public override IntegrationResult IntegrateSegment(
        Vector3 startPosition, Vector3 startVelocity,
        Time baseEpoch, double durationSeconds,
        IReadOnlyList<IEventDetector> eventDetectors = null)
    {
        int estimatedSteps = (int)(durationSeconds / DeltaTs) + 2;
        var segment = new PropagationSegment(baseEpoch, estimatedSteps);

        var pos = startPosition;
        var vel = startVelocity;
        double t = 0.0;

        // Compute initial acceleration
        UpdateEvalState(pos, vel, baseEpoch);
        var accel = ComputeAcceleration(EvalState);

        // A segment of no duration, the state after an impulse at the end of the propagation (#575): one step of zero
        // length, which holds the state
        if (!(durationSeconds > 0.0))
        {
            SkipRoot = false;
            segment.AddStep(new AcceptedStep(0.0, 0.0, pos, vel, pos, vel, accel, accel));
            return new IntegrationResult(segment, null);
        }

        // Initialize event detector g-values: a gated detector open at the base from the start of its search (#581), the
        // detector of the event just handled from its g-value past that root
        double[] prevG = null;
        double[] currG = null;
        int[] crossed = null;
        IEventDetector justHandled = null;
        if (eventDetectors != null && eventDetectors.Count > 0)
        {
            (prevG, justHandled) = EventValuesAtBase(eventDetectors, EvalState);
            currG = new double[eventDetectors.Count];
            crossed = new int[eventDetectors.Count];
        }
        else
        {
            SkipRoot = false;
        }

        // The gates still closed at the base (#569), but that of the detector of the event just handled, which it decided
        var gates = EventGates.Create(eventDetectors, baseEpoch, prevG, justHandled);

        while (t < durationSeconds)
        {
            double remaining = durationSeconds - t;
            double h = System.Math.Min(DeltaTs, remaining);
            var startAccel = accel;
            var (newPos, newVel, endAccel) = Step(pos, vel, startAccel, baseEpoch, t, h);
            accel = endAccel;

            // Store accepted step
            var accepted = new AcceptedStep(t, h, pos, vel, newPos, newVel, startAccel, accel);
            segment.AddStep(accepted);

            // The token of the run, at each accepted step (R28 of #668)
            Cancellation.ThrowIfCancellationRequested();

            t += h;
            pos = newPos;
            vel = newVel;

            // Check event detectors
            if (eventDetectors != null && eventDetectors.Count > 0)
            {
                var endEpoch = TimeArgument.EpochAt(baseEpoch, t);
                gates?.AfterStep(accepted, baseEpoch, endEpoch, prevG, this);
                UpdateEvalState(pos, vel, endEpoch);
                int count = CrossingsInStep(eventDetectors, EvalState, prevG, currG, gates, crossed);

                if (count > 0)
                {
                    // The earliest crossing of the step (#578), reported at the end of the step that holds it
                    int detIdx = count == 1 ? crossed[0] : EarliestCrossing(accepted, baseEpoch, eventDetectors, crossed, count, prevG, currG, gates);
                    RecordEvent(eventDetectors[detIdx], currG[detIdx]);
                    return new IntegrationResult(segment, EventAtLabel(segment, accepted, baseEpoch, detIdx));
                }

                // Swap g-value arrays
                (prevG, currG) = (currG, prevG);
            }
        }

        return new IntegrationResult(segment, null);
    }

    // One Velocity-Verlet step of h from (pos, vel) at t, its start acceleration given: the end state and acceleration
    private (Vector3 Position, Vector3 Velocity, Vector3 Acceleration) Step(in Vector3 pos, in Vector3 vel,
        in Vector3 startAccel, in Time baseEpoch, double t, double h)
    {
        double halfH = h * 0.5;
        var halfVel = vel + startAccel * halfH;
        var newPos = pos + halfVel * h;

        // Acceleration at the new position, with the predicted end velocity v + h a (#439): a force that depends on the
        // velocity (drag) then errs by O(h²) there, and the integrator stays of second order; the half-step velocity
        // made it of first order with drag
        UpdateEvalState(newPos, halfVel + startAccel * halfH, TimeArgument.EpochAt(baseEpoch, t + h));
        var endAccel = ComputeAcceleration(EvalState);
        return (newPos, halfVel + endAccel * halfH, endAccel);
    }

    /// <summary>
    /// The event at the end of the last accepted step, carried onto the tick of its label (#572): the event time is the
    /// time of the label <see cref="PropagationTimeArgument.EpochAt"/> gives the step end, up to half a tick from it, and the
    /// last step is integrated again to that time, so that the state the next segment starts from is the state of its
    /// label, as RK7(8) does. A step end on the grid of the ticks keeps its step, bit for bit.
    /// </summary>
    private EventInfo EventAtLabel(PropagationSegment segment, in AcceptedStep step, in Time baseEpoch, int detectorIndex)
    {
        double t0 = step.CumulativeTime;
        double t1 = t0 + step.StepSizeSeconds;
        var label = TimeArgument.EpochAt(baseEpoch, t1);
        double tL = TimeArgument.OnEpochGrid(baseEpoch, label, t0, t1 + PropagationSolution.EpochTick);
        if (tL == t1 || !(tL > t0))
        {
            return new EventInfo(detectorIndex, t1, step.EndPosition, step.EndVelocity);
        }

        double h = tL - t0;
        var (position, velocity, acceleration) = Step(step.StartPosition, step.StartVelocity, step.StartAcceleration,
            baseEpoch, t0, h);
        segment.ReplaceLastStep(new AcceptedStep(t0, h, step.StartPosition, step.StartVelocity, position, velocity,
            step.StartAcceleration, acceleration));
        return new EventInfo(detectorIndex, tL, position, velocity);
    }

    /// <summary>
    /// The earliest of the crossings of the accepted step (#578): the root of each detector estimated on the cubic Hermite
    /// of the step, from its gate when the gate opened in the step, the earliest kept, ties in list order.
    /// </summary>
    private int EarliestCrossing(in AcceptedStep step, in Time baseEpoch, IReadOnlyList<IEventDetector> detectors,
        int[] crossed, int count, double[] prevG, double[] currG, EventGates gates)
    {
        int earliest = crossed[0];
        double earliestT = RootOnHermite(step, baseEpoch, detectors, earliest, prevG, currG, gates);
        for (int k = 1; k < count; k++)
        {
            int i = crossed[k];
            double root = RootOnHermite(step, baseEpoch, detectors, i, prevG, currG, gates);
            if (root < earliestT)
            {
                earliestT = root;
                earliest = i;
            }
        }

        return earliest;
    }

    private double RootOnHermite(AcceptedStep step, Time baseEpoch, IReadOnlyList<IEventDetector> detectors, int index,
        double[] prevG, double[] currG, EventGates gates)
    {
        double t0 = step.CumulativeTime;
        double t1 = t0 + step.StepSizeSeconds;
        var detector = detectors[index];
        Func<StateVector, double> g = detector.Evaluate;
        if (gates != null && gates.OpenedInLastStep(index, out double gateT, out bool zeroAtGate))
        {
            if (zeroAtGate) return gateT;
            t0 = gateT;
            g = ((IGatedEventDetector)detector).EvaluateOpen;
        }

        if (currG[index] == 0.0) return t1;
        return BisectionEventFinder.FindRoot(t0, t1, t => g(StateInsideAcceptedStep(step, baseEpoch, t)), prevG[index],
            currG[index]);
    }
}
