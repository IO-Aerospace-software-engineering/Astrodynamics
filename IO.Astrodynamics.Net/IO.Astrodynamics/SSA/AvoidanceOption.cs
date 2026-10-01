// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Describes one candidate impulsive avoidance maneuver and the encounter that would result after applying it.
/// </summary>
public sealed class AvoidanceOption
{
    /// <summary>
    /// Initializes a new avoidance option.
    /// </summary>
    /// <param name="burnEpoch">Epoch at which the maneuver would be executed.</param>
    /// <param name="deltaVInertial">Impulsive maneuver vector in the inertial frame.</param>
    /// <param name="fuelCostEstimateKilograms">Estimated fuel cost associated with the maneuver.</param>
    /// <param name="postManeuverEncounter">Encounter produced by reanalyzing the post-maneuver state.</param>
    /// <param name="rankingScore">Internal ranking score used to sort candidate maneuvers. Lower is better.</param>
    public AvoidanceOption(
        Time burnEpoch,
        Vector3 deltaVInertial,
        double fuelCostEstimateKilograms,
        EncounterCase postManeuverEncounter,
        double rankingScore)
    {
        BurnEpoch = burnEpoch;
        DeltaVInertial = deltaVInertial;
        FuelCostEstimateKilograms = fuelCostEstimateKilograms;
        PostManeuverEncounter = postManeuverEncounter ?? throw new ArgumentNullException(nameof(postManeuverEncounter));
        RankingScore = rankingScore;
    }

    /// <summary>
    /// Gets the epoch at which the maneuver would be executed.
    /// </summary>
    public Time BurnEpoch { get; }

    /// <summary>
    /// Gets the inertial impulsive maneuver vector.
    /// </summary>
    public Vector3 DeltaVInertial { get; }

    /// <summary>
    /// Gets the estimated fuel required by the maneuver, in kilograms.
    /// </summary>
    public double FuelCostEstimateKilograms { get; }

    /// <summary>
    /// Gets the encounter that remains after the maneuver is applied and the case is reanalyzed.
    /// </summary>
    public EncounterCase PostManeuverEncounter { get; }

    /// <summary>
    /// Gets the internal ranking score used to sort options. Lower values indicate a better candidate.
    /// </summary>
    public double RankingScore { get; }
}
