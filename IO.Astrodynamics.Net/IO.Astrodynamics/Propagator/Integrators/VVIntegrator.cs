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
public sealed class VVIntegrator : Integrator
{
    public TimeSpan DeltaT { get; }
    public double DeltaTs { get; }
    public double HalfDeltaTs { get; }

    public VVIntegrator(IEnumerable<ForceBase> forces, TimeSpan deltaT, StateVector initialState) : base(forces)
    {
        DeltaT = deltaT;
        DeltaTs = DeltaT.TotalSeconds;
        HalfDeltaTs = DeltaTs * 0.5;
        Initialize(initialState);
    }

    public VVIntegrator(TimeSpan deltaT) : base()
    {
        DeltaT = deltaT;
        DeltaTs = DeltaT.TotalSeconds;
        HalfDeltaTs = DeltaTs * 0.5;
    }

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
            UpdateEvalState(newPos, halfVel, baseEpoch.AddSeconds(t + h));
            accel = ComputeAcceleration(EvalState);
            var newVel = halfVel + accel * halfH;

            // Store accepted step
            segment.AddStep(new AcceptedStep(t, h, pos, vel, newPos, newVel, startAccel, accel));

            t += h;
            pos = newPos;
            vel = newVel;

            // Check event detectors
            if (eventDetectors != null && eventDetectors.Count > 0)
            {
                UpdateEvalState(pos, vel, baseEpoch.AddSeconds(t));
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
