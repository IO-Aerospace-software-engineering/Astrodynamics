// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// The record of one maneuver a propagation executed, on <see cref="PropagationSolution.Maneuvers"/> in execution order:
/// the values of that run, which a later run of the same maneuver does not change (decision 57 of #668, decided by S.
/// Guillet, 2026-11-23). The definition itself holds the values of its last run only (decision 52).
/// </summary>
public sealed record ExecutedManeuver
{
    internal ExecutedManeuver(Maneuver.Maneuver definition, Time epoch, Window thrustWindow, Window maneuverWindow,
        Vector3 deltaV, double fuelBurned, double massBefore, double massAfter)
    {
        Definition = definition;
        Epoch = epoch;
        ThrustWindow = thrustWindow;
        ManeuverWindow = maneuverWindow;
        DeltaV = deltaV;
        FuelBurned = fuelBurned;
        MassBefore = massBefore;
        MassAfter = massAfter;
    }

    /// <summary>The maneuver of the plan that was executed.</summary>
    public Maneuver.Maneuver Definition { get; }

    /// <summary>
    /// The TDB label of the execution: the epoch of the event of an impulse, which carries its whole delta-V, or the
    /// activation of an attitude.
    /// </summary>
    public Time Epoch { get; }

    /// <summary>
    /// The thrust window of the run, unclipped: for an impulse the equivalent constant-thrust burn
    /// [t − f dt, t + (1 − f) dt] around its epoch (#335), which may open before the propagation window; for an attitude
    /// its activation, of zero length. As <see cref="Maneuver.Maneuver.ThrustWindow"/> after the run.
    /// </summary>
    public Window ThrustWindow { get; }

    /// <summary>
    /// The maneuver window of the run, whose end gated the next maneuver of the plan; as
    /// <see cref="Maneuver.Maneuver.ManeuverWindow"/> after the run.
    /// </summary>
    public Window ManeuverWindow { get; }

    /// <summary>
    /// The delta-V of an impulse in the frame of the solution (<see cref="PropagationSolution.Frame"/>), m/s, added to
    /// the velocity at <see cref="Epoch"/>; <see cref="Vector3.Zero"/> for an attitude.
    /// </summary>
    public Vector3 DeltaV { get; }

    /// <summary>The fuel the impulse burned, kg; 0 for an attitude.</summary>
    public double FuelBurned { get; }

    /// <summary>The total mass of the spacecraft before the execution (<c>GetTotalMass</c>, the child included), kg.</summary>
    public double MassBefore { get; }

    /// <summary>The total mass of the spacecraft after the execution, kg; <see cref="MassBefore"/> for an attitude.</summary>
    public double MassAfter { get; }
}
