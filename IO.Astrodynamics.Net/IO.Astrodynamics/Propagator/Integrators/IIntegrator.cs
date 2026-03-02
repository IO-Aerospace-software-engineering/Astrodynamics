// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Integrators;

/// <summary>
/// Defines the contract for numerical integrators used by propagators.
/// Integrators own the time window and produce a PropagationSegment with
/// all accepted steps, enabling dense output interpolation.
/// </summary>
public interface IIntegrator
{
    /// <summary>
    /// Initialize the integrator with the initial state.
    /// Sets the observer and frame used for constructing intermediate StateVectors.
    /// Called by the propagator after forces have been added.
    /// </summary>
    void Initialize(StateVector initialState);

    /// <summary>
    /// Integrate from the given start state for the specified duration.
    /// The integrator steps through the duration using its own step-size strategy,
    /// evaluating event detectors after each step. On event detection, the segment
    /// is terminated early and event information is returned.
    /// </summary>
    /// <param name="startPosition">Initial position.</param>
    /// <param name="startVelocity">Initial velocity.</param>
    /// <param name="baseEpoch">Absolute start time of the segment.</param>
    /// <param name="duration">Duration in seconds to integrate.</param>
    /// <param name="eventDetectors">Optional event detectors to evaluate each step.</param>
    /// <returns>The propagation segment and optional event info.</returns>
    IntegrationResult IntegrateSegment(
        Vector3 startPosition, Vector3 startVelocity,
        Time baseEpoch, double duration,
        IReadOnlyList<IEventDetector> eventDetectors = null);
}
