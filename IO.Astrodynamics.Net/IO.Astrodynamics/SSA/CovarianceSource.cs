// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Where the covariance of one participant at the encounter epoch comes from, in the order the conjunction analysis
/// resolves it.
/// </summary>
public enum CovarianceSource
{
    /// <summary>
    /// The participant has no covariance.
    /// </summary>
    None,

    /// <summary>
    /// The state of the participant at the encounter epoch carried its covariance (age zero), given by its ephemeris.
    /// </summary>
    ProvidedAtEncounter,

    /// <summary>
    /// The covariance of the initial state of the participant, held fixed in the RTN frame: rotated into RTN with the
    /// initial state, then back into the inertial frame with the encounter state. Not propagated: its age is the time
    /// between the initial state and the encounter epoch (<see cref="EncounterQualityFlags.StaleCovarianceUsed"/>). The
    /// fallback for a participant that the library does not propagate in RK7(8) (Velocity-Verlet, two-body, TLE, SPICE),
    /// or when <see cref="ConjunctionAnalysisOptions.CovariancePropagation"/> is null.
    /// </summary>
    InitialStateHeldInRtn,

    /// <summary>
    /// The covariance of the initial state propagated to the encounter epoch with the trajectory, by the variational
    /// equations of RK7(8): P = Φ P0 Φᵀ + Ψc Pp Ψcᵀ + Q (see the covariance propagation reference). Linear: see
    /// <see cref="ParticipantCovariance.ProcessNoiseApplied"/> and <see cref="ParticipantCovariance.NonlinearityIndex"/>.
    /// </summary>
    Propagated
}
