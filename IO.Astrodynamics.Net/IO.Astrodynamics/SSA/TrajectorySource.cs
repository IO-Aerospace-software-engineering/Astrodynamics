// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Ssa;

/// <summary>
/// How the trajectory of a participant of an encounter is computed (<see cref="EncounterState.ProtectedTrajectory"/>,
/// <see cref="EncounterState.SecondaryTrajectory"/>), and that of an avoidance option after its burn
/// (<see cref="AvoidanceOption.Trajectory"/>, <see cref="Propagated"/> or <see cref="TwoBody"/>). R17 of #668, decided by
/// S. Guillet, 2026-11-19: <c>AvoidanceTrajectory</c> before 11.0.0, of the avoidance options only.
/// </summary>
public enum TrajectorySource
{
    /// <summary>
    /// Propagated numerically: a <see cref="Propagator.PropagationSolution"/> given to the analysis, the states a numerical
    /// propagation stored (or <see cref="Body.Spacecraft.Spacecraft.AddPropagatedStates"/> added); for an avoidance option,
    /// propagated from the state after the burn with the force model of the protected participant's own propagation (its
    /// forces, integrator and step settings), in RK7(8) or by Velocity-Verlet.
    /// </summary>
    Propagated,

    /// <summary>
    /// The two-body orbit of the initial orbital parameters of a spacecraft that holds no stored state; for an avoidance
    /// option, that of the state after the burn, the protected participant having no numerical propagation whose force
    /// model the option could follow (not propagated, TLE, SPICE kernel). For a participant given by Keplerian elements it
    /// is its own model; for a TLE or a kernel it leaves out the perturbations of that model.
    /// </summary>
    TwoBody,

    /// <summary>
    /// SGP4 from the TLE of a spacecraft that no numerical propagation stored states for (unpropagated, or sampled by
    /// <see cref="Propagator.TlePropagator"/>).
    /// </summary>
    Sgp4,

    /// <summary>
    /// Read through the data provider (the SPICE kernels by default): a kernel-backed spacecraft, or an object that is not
    /// a spacecraft (a celestial body, a site, a star, a barycentre).
    /// </summary>
    Kernel
}
