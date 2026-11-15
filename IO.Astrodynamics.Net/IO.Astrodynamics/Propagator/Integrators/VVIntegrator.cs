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
/// An event fires at the end of the step that holds its crossing. In the step that holds the gate of a gated detector
/// (the minimum epoch of a maneuver, #569), the g-value at the gate, on the cubic Hermite of the step, replaces the
/// previous g-value (<see cref="EventGates"/>).
/// </remarks>
public sealed class VVIntegrator : Integrator
{
    public TimeSpan DeltaT { get; }
    public double DeltaTs { get; }
    public double HalfDeltaTs { get; }

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
        Time baseEpoch, double duration,
        IReadOnlyList<IEventDetector> eventDetectors = null)
    {
        int estimatedSteps = (int)(duration / DeltaTs) + 2;
        var segment = new PropagationSegment(baseEpoch, estimatedSteps);

        var pos = startPosition;
        var vel = startVelocity;
        double t = 0.0;

        // Compute initial acceleration
        UpdateEvalState(pos, vel, baseEpoch);
        var accel = ComputeAcceleration(EvalState);

        // Initialize event detector g-values
        double[] prevG = null;
        double[] currG = null;
        if (eventDetectors != null && eventDetectors.Count > 0)
        {
            prevG = new double[eventDetectors.Count];
            currG = new double[eventDetectors.Count];
            for (int i = 0; i < eventDetectors.Count; i++)
            {
                prevG[i] = eventDetectors[i].IsActive ? eventDetectors[i].Evaluate(EvalState) : 0.0;
            }
        }

        // The gates still closed at the base (#569)
        var gates = EventGates.Create(eventDetectors, baseEpoch, prevG, null);

        while (t < duration)
        {
            double remaining = duration - t;
            double h = System.Math.Min(DeltaTs, remaining);
            double halfH = h * 0.5;

            // Velocity-Verlet step
            var startAccel = accel;
            var halfVel = vel + accel * halfH;
            var newPos = pos + halfVel * h;

            // Compute acceleration at new position
            UpdateEvalState(newPos, halfVel, TimeArgument.EpochAt(baseEpoch, t + h));
            accel = ComputeAcceleration(EvalState);
            var newVel = halfVel + accel * halfH;

            // Store accepted step
            var accepted = new AcceptedStep(t, h, pos, vel, newPos, newVel, startAccel, accel);
            segment.AddStep(accepted);

            t += h;
            pos = newPos;
            vel = newVel;

            // Check event detectors
            if (eventDetectors != null && eventDetectors.Count > 0)
            {
                var endEpoch = TimeArgument.EpochAt(baseEpoch, t);
                gates?.AfterStep(accepted, baseEpoch, endEpoch, prevG, this);
                UpdateEvalState(pos, vel, endEpoch);
                int detIdx = CheckEventDetectors(eventDetectors, EvalState, prevG, currG);

                if (detIdx >= 0)
                {
                    // Event detected — return partial segment with event info
                    // Event timing is limited to step boundaries for VV
                    return new IntegrationResult(segment,
                        new EventInfo(detIdx, t, pos, vel));
                }

                // Swap g-value arrays
                (prevG, currG) = (currG, prevG);
            }
        }

        return new IntegrationResult(segment, null);
    }
}
