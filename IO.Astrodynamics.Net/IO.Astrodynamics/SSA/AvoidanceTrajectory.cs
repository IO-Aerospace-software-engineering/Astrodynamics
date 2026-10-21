// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.SSA;

/// <summary>
/// How the trajectory of an avoidance option after its burn is computed (<see cref="AvoidanceOption.Trajectory"/>).
/// </summary>
public enum AvoidanceTrajectory
{
    /// <summary>
    /// Propagated numerically from the state after the burn with the force model of the protected participant's own
    /// propagation (its forces, integrator and step settings), in RK7(8) or by Velocity-Verlet.
    /// </summary>
    Propagated,

    /// <summary>
    /// The two-body orbit of the state after the burn: the protected participant has no numerical propagation whose
    /// force model the option could follow (not propagated, TLE, SPICE kernel). For a participant given by Keplerian
    /// elements it is its own model; for a TLE or a kernel it leaves out the perturbations of that model.
    /// </summary>
    TwoBody
}
