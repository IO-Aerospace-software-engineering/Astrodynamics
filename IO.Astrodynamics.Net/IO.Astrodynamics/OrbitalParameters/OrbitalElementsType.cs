namespace IO.Astrodynamics.OrbitalParameters;

/// <summary>
/// Specifies the type of orbital elements.
/// </summary>
public enum OrbitalElementsType
{
    /// <summary>
    /// Osculating elements represent the instantaneous Keplerian orbit
    /// that would result if all perturbations were removed at the epoch.
    /// These are typically obtained from state vectors or direct observations.
    /// </summary>
    Osculating,

    /// <summary>
    /// Mean elements are averaged over short-periodic perturbations and
    /// represent a smoothed orbit. These are used in TLE/SGP4 and OMM formats.
    /// Mean elements should not be used directly for position calculations
    /// without an appropriate propagator (e.g., SGP4/SDP4).
    /// </summary>
    Mean
}
