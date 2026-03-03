// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
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

    // Reference state info (from initial state, for constructing intermediate StateVectors)
    protected ILocalizable Observer { get; private set; }
    protected Frame ReferenceFrame { get; private set; }

    // Reusable StateVector for force evaluation (avoids per-step allocation)
    protected StateVector EvalState { get; private set; }

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

    /// <summary>
    /// Initialize the integrator with the initial state.
    /// Sets the observer and frame used for constructing intermediate StateVectors.
    /// Called by the propagator after forces have been added.
    /// </summary>
    public virtual void Initialize(StateVector initialState)
    {
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
        foreach (var force in Forces)
        {
            res += force.Apply(stateVector);
        }

        return res;
    }

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
    protected static void UpdateStateVector(StateVector sv, in Vector3 position, in Vector3 velocity, in Time epoch)
    {
        sv.UpdatePosition(position);
        sv.UpdateVelocity(velocity);
        sv.UpdateEpoch(epoch);
    }

    /// <summary>
    /// Check event detectors for sign changes.
    /// Returns the detector index and g-values if an event is detected, or -1 if none.
    /// </summary>
    protected static int CheckEventDetectors(
        IReadOnlyList<IEventDetector> detectors,
        StateVector state,
        double[] prevG, double[] currG)
    {
        if (detectors == null) return -1;

        for (int i = 0; i < detectors.Count; i++)
        {
            var detector = detectors[i];
            if (!detector.IsActive) continue;

            currG[i] = detector.Evaluate(state);
            double gPrev = prevG[i];
            double gCurr = currG[i];

            // Check for sign change in the correct direction
            if (gPrev * gCurr < 0.0)
            {
                bool match = detector.Direction switch
                {
                    CrossingDirection.NegativeToPositive => gPrev < 0.0 && gCurr > 0.0,
                    CrossingDirection.PositiveToNegative => gPrev > 0.0 && gCurr < 0.0,
                    CrossingDirection.Any => true,
                    _ => false
                };

                if (match) return i;
            }
        }

        return -1;
    }
}
