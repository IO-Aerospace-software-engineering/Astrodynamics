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
    /// Called by the propagator: by every constructor of <see cref="CentralBodyPropagator"/>, once the forces are added
    /// (with an integrator that already has its forces, at construction too), and after each event.
    /// </summary>
    void Initialize(StateVector initialState);

    /// <summary>
    /// Integrate from the given start state for the specified duration.
    /// The integrator steps through the duration using its own step-size strategy,
    /// evaluating event detectors after each step. On event detection, the segment
    /// is terminated early and event information is returned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One event per call: the earliest crossing of the step that holds crossings, ties in list order (#578).
    /// <see cref="RK78Integrator"/> locates the root of each detector that crossed and keeps the earliest;
    /// <see cref="VVIntegrator"/> estimates each root on the cubic Hermite of the step, keeps the earliest and reports it at
    /// the end of the step (carried onto the tick of its label, #572), so another crossing of the same step falls before
    /// the event: a caller that restarts from the event starts that detector from its g-value before the step, or loses
    /// that crossing (decided by S. Guillet, 2026-11-17). A g-value exactly zero at a step end after a non-zero one is a
    /// crossing there; a zero at the base followed by a non-zero value is none (Hipparchus's convention), so a restart from
    /// an event with the same detector does not find it again (#580).
    /// </para>
    /// <para>
    /// A <paramref name="duration"/> of zero gives a segment of one step of zero length, which holds the start state: the
    /// propagator asks for it after an impulse executed with no time left (#575). For an implementation outside the library
    /// that returns no step there, the propagator adds that step itself, with the acceleration of the forces of an
    /// <see cref="Integrator"/> subclass at the start state, or the end acceleration of the previous segment for a bare
    /// <see cref="IIntegrator"/> (a step of zero length is never interpolated), so that the solution ends with the state
    /// after the impulse on every path.
    /// </para>
    /// <para>
    /// The integrators of the library evaluate the forces at the TDB label of each instant of the time argument. An
    /// integrator implemented outside the library, a bare <see cref="IIntegrator"/> or a subclass of
    /// <see cref="Integrator"/>, builds its own epochs, usually <c>baseEpoch.AddSeconds(t)</c>, in TDB seconds: about
    /// the Earth, its epochs then drift from those of the propagation by at most 3.4e-10 of the elapsed time (the rate of
    /// TDB − TT).
    /// </para>
    /// </remarks>
    /// <param name="startPosition">Initial position.</param>
    /// <param name="startVelocity">Initial velocity.</param>
    /// <param name="baseEpoch">Absolute start time of the segment.</param>
    /// <param name="duration">Duration to integrate, in seconds of the time argument of the propagation: TT seconds
    /// about the Earth, TDB seconds about any other central body (#501).</param>
    /// <param name="eventDetectors">Optional event detectors to evaluate each step.</param>
    /// <returns>The propagation segment and optional event info.</returns>
    IntegrationResult IntegrateSegment(
        Vector3 startPosition, Vector3 startVelocity,
        Time baseEpoch, double duration,
        IReadOnlyList<IEventDetector> eventDetectors = null);
}
