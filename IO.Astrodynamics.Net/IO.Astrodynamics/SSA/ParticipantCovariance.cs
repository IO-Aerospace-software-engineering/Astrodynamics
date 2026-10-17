// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// The covariance of one participant at the encounter epoch: where it comes from, and how far it can be trusted.
/// </summary>
/// <param name="Source">Where the covariance comes from.</param>
/// <param name="Age">Time between the epoch of the covariance and the encounter epoch: zero when it is given or
/// propagated to the encounter epoch, the distance to the initial state when it is held in RTN, null without a
/// covariance.</param>
/// <param name="ProcessNoiseApplied">For a <see cref="CovarianceSource.Propagated"/> covariance, whether a process
/// noise was added to it (<see cref="Propagator.Variational.CovariancePropagationOptions.ProcessNoise"/>). Without it,
/// the covariance is the linear propagation of the initial covariance and of the consider parameters only, which is
/// often optimistic. False for the other sources.</param>
/// <param name="NonlinearityIndex">For a <see cref="CovarianceSource.Propagated"/> covariance, the non-linearity index
/// of the propagation from the initial state to the encounter epoch (Junkins and Singla, 2004; see the conjunction
/// assessment reference), dimensionless: 0 for a linear flow, larger as the linear propagation of the covariance departs
/// from the spread of the trajectories. Null for the other sources, and when it is not computed
/// (<see cref="ConjunctionAnalysisOptions.ComputeNonlinearityIndex"/>).</param>
public sealed record ParticipantCovariance(
    CovarianceSource Source,
    TimeSpan? Age,
    bool ProcessNoiseApplied = false,
    double? NonlinearityIndex = null)
{
    /// <summary>
    /// The participant without a covariance.
    /// </summary>
    public static ParticipantCovariance None { get; } = new(CovarianceSource.None, null);

    /// <summary>
    /// The source inferred from the age alone, as the constructors of <see cref="EncounterState"/> without the source
    /// do: null for none, zero for a covariance given at the encounter epoch, otherwise the initial covariance held in
    /// RTN.
    /// </summary>
    internal static ParticipantCovariance FromAge(TimeSpan? age) => age switch
    {
        null => None,
        { } a when a == TimeSpan.Zero => new ParticipantCovariance(CovarianceSource.ProvidedAtEncounter, a),
        { } a => new ParticipantCovariance(CovarianceSource.InitialStateHeldInRtn, a)
    };
}
