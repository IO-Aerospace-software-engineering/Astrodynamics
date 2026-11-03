// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// An impulse of a propagation: its epoch, the state of the integrator at the event before the maneuver executes and the
/// state the maneuver gives, relative to the central body, in the frame of the propagation (#540). In covariance mode each
/// carries its covariance: before, the one the propagation evaluates at the event; after, the one the next segment starts
/// from, with the execution error of the burn. The stored states of the spacecraft are split into arcs at each impulse, so
/// that no reader interpolates across the jump of the velocity. For the stored states, <see cref="After"/> is null when no
/// segment of the solution carries the impulse (located at the end of the propagation): the states end with the state
/// before it, as the solution does.
/// </summary>
internal readonly record struct ImpulseBoundary(Time Epoch, StateVector Before, StateVector After);
