// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Ssa;

/// <summary>
/// The search that found the epoch of an encounter (<see cref="EncounterCase.Search"/>; R18 of #668, decided by
/// S. Guillet, 2026-11-19). See the conjunction assessment reference, "The two searches".
/// </summary>
public enum CloseApproachSearch
{
    /// <summary>
    /// The range rate sampled over the window every <see cref="ConjunctionAnalysisOptions.SampleStep"/> at most and
    /// bisected: the <see cref="Body.ILocalizable"/> overloads, the screening, the avoidance options, and two trajectories
    /// that do not share their observer and frame.
    /// </summary>
    Sampled,

    /// <summary>
    /// The minima of the cubic Hermite of the stored steps of two propagations that share their observer and frame,
    /// refined on the range rate of the states (the <see cref="Propagator.PropagationSolution"/> overloads of
    /// <c>Analyze</c> and <c>AnalyzeAll</c>).
    /// </summary>
    Polynomial,

    /// <summary>
    /// No search: the encounter was given at its TCA, by the states of its objects there
    /// (<see cref="Ccsds.Cdm.ToEncounter"/>, decision 110 of #668, decided by S. Guillet, 2026-11-26).
    /// </summary>
    None
}
