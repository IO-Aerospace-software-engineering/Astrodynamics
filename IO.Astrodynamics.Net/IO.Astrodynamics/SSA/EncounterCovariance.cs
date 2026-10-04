// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Covariance of one participant at the encounter epoch, as used by the conjunction analysis and the CDM export.
/// </summary>
/// <param name="CovarianceInertial">6x6 covariance in the inertial frame of the encounter state, or null when none is available.</param>
/// <param name="Age">Time between the epoch of the covariance and the encounter epoch; null when no covariance is available.</param>
/// <param name="FromInitialState">True when the covariance was taken from the participant's initial state.</param>
internal sealed record EncounterCovariance(Matrix? CovarianceInertial, TimeSpan? Age, bool FromInitialState)
{
    /// <summary>
    /// Resolves the covariance of a participant at the encounter epoch.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    /// <item>the covariance carried by the state at the encounter epoch, if any (age zero);</item>
    /// <item>otherwise the covariance of the participant's initial state, held fixed in the RTN frame: rotated into
    /// RTN with the initial state, then back into the inertial frame with the encounter state. Covariance is not
    /// propagated; holding it in RTN keeps its orientation relative to the orbit (large along-track, small radial);</item>
    /// <item>otherwise no covariance.</item>
    /// </list>
    /// </remarks>
    internal static EncounterCovariance Resolve(ILocalizable source, StateVector encounterState)
    {
        if (encounterState.Covariance.HasValue)
        {
            return new EncounterCovariance(encounterState.Covariance.Value, TimeSpan.Zero, false);
        }

        if (source.InitialOrbitalParameters is StateVector initialState && initialState.Covariance.HasValue)
        {
            var covarianceRtn = initialState.RotateCovarianceToRtn(initialState.Covariance.Value);
            var covarianceInertial = encounterState.RotateCovarianceFromRtn(covarianceRtn);
            var age = (encounterState.Epoch - initialState.Epoch).Duration();
            return new EncounterCovariance(covarianceInertial, age, true);
        }

        return new EncounterCovariance(null, null, false);
    }

    /// <summary>
    /// True when the covariance comes from the initial state and is older than the given threshold.
    /// </summary>
    internal bool IsStale(TimeSpan threshold)
    {
        return FromInitialState && Age > threshold;
    }
}
