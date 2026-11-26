// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Globalization;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Represents one conjunction case, including geometry, risk metrics, and optional participant snapshots at TCA.
/// </summary>
/// <remarks>
/// A result of the library: <see cref="ConjunctionAssessment"/> and <see cref="IO.Astrodynamics.CCSDS.CDM.Cdm.ToEncounter"/> build
/// it, its constructors internal (R17 of #668, choice 4, lot L7; public before 11.0.0).
/// </remarks>
public sealed class EncounterCase
{
    /// <summary>
    /// Initializes an encounter case without retaining per-object state snapshots.
    /// </summary>
    internal EncounterCase(
        Spacecraft protectedSpacecraft,
        ILocalizable secondaryObject,
        Window screeningWindow,
        EncounterState encounterState,
        CollisionRisk collisionRisk)
        : this(
            protectedSpacecraft,
            secondaryObject,
            screeningWindow,
            encounterState,
            collisionRisk,
            null,
            null)
    {
    }

    /// <summary>
    /// Initializes an encounter case and retains the participant state vectors evaluated at the encounter epoch.
    /// </summary>
    internal EncounterCase(
        Spacecraft protectedSpacecraft,
        ILocalizable secondaryObject,
        Window screeningWindow,
        EncounterState encounterState,
        CollisionRisk collisionRisk,
        StateVector protectedState,
        StateVector secondaryState)
    {
        ProtectedSpacecraft = protectedSpacecraft ?? throw new ArgumentNullException(nameof(protectedSpacecraft));
        SecondaryObject = secondaryObject ?? throw new ArgumentNullException(nameof(secondaryObject));
        ScreeningWindow = screeningWindow;
        EncounterState = encounterState ?? throw new ArgumentNullException(nameof(encounterState));
        CollisionRisk = collisionRisk;
        ProtectedState = protectedState;
        SecondaryState = secondaryState;
    }

    /// <summary>
    /// Gets the protected spacecraft that was analyzed (R17 of #668: the <c>ProtectedAsset</c> profile before 11.0.0).
    /// </summary>
    public Spacecraft ProtectedSpacecraft { get; }

    /// <summary>
    /// Gets the secondary object that was analyzed against the protected asset.
    /// </summary>
    public ILocalizable SecondaryObject { get; }

    /// <summary>
    /// Gets the screening window used to search this encounter.
    /// </summary>
    public Window ScreeningWindow { get; }

    /// <summary>
    /// Gets the encounter geometry and covariance state evaluated at the reported epoch.
    /// </summary>
    public EncounterState EncounterState { get; }

    /// <summary>
    /// Gets the collision-risk products derived from the encounter geometry.
    /// </summary>
    public CollisionRisk CollisionRisk { get; }

    /// <summary>
    /// Gets the protected spacecraft state at the encounter epoch when that snapshot was retained.
    /// </summary>
    public StateVector ProtectedState { get; }

    /// <summary>
    /// Gets the secondary object state at the encounter epoch when that snapshot was retained.
    /// </summary>
    public StateVector SecondaryState { get; }

    /// <summary>
    /// The covariance of the protected participant used by the analysis, with its source and, when propagated, the
    /// solution it was read from; null for a case built by hand. Read by the CDM export (E5).
    /// </summary>
    internal EncounterCovariance ProtectedCovariance { get; init; }

    /// <summary>The covariance of the secondary participant used by the analysis; see <see cref="ProtectedCovariance"/>.</summary>
    internal EncounterCovariance SecondaryCovariance { get; init; }

    /// <summary>
    /// The propagation of the protected participant the analysis sampled: the solution passed to it, or the last
    /// propagation of the spacecraft in RK7(8); null otherwise. The avoidance options are propagated with its force model
    /// (#402).
    /// </summary>
    internal PropagationSolution ProtectedSolution { get; init; }

    /// <summary>
    /// Gets the options the analysis used, the radius of the secondary resolved (#683): the options given, or the defaults,
    /// with <see cref="ConjunctionAnalysisOptions.SecondaryHardBodyRadiusMeters"/> the radius the analysis took for the
    /// secondary. <see cref="ConjunctionAssessment.EvaluateAvoidance"/> re-analyzes its options with them unless
    /// <see cref="AvoidanceSearchOptions.Analysis"/> gives others, and <c>AnalysisOptions with { … }</c> is the start of
    /// such options (R17 of #668, decided by S. Guillet, 2026-11-19; internal before 11.0.0).
    /// </summary>
    public ConjunctionAnalysisOptions AnalysisOptions { get; internal init; }

    /// <summary>
    /// Gets the search that found the epoch of the encounter: <see cref="CloseApproachSearch.Polynomial"/> on two
    /// propagations that share their observer and frame (the <see cref="PropagationSolution"/> overloads),
    /// <see cref="CloseApproachSearch.Sampled"/> otherwise (R18 of #668, decided by S. Guillet, 2026-11-19). The two
    /// searches differ in cost and in their rule at the window ends; see the conjunction assessment reference.
    /// </summary>
    public CloseApproachSearch Search { get; internal init; }

    /// <summary>Gets the time of closest approach, <see cref="EncounterState"/>'s <see cref="EncounterState.Epoch"/>.</summary>
    public Time Tca => EncounterState.Epoch;

    /// <summary>Gets the miss distance at TCA, in m, <see cref="EncounterState"/>'s <see cref="EncounterState.MissDistanceMeters"/>.</summary>
    public double MissDistanceMeters => EncounterState.MissDistanceMeters;

    /// <summary>
    /// Gets the relative speed at TCA, in m/s: the norm of the relative velocity
    /// (<see cref="RelativeState.RelativeVelocityInertial"/>), the <c>RELATIVE_SPEED</c> of the CDM.
    /// </summary>
    public double RelativeSpeedMetersPerSecond => EncounterState.RelativeState.RelativeVelocityInertial.Magnitude();

    /// <summary>
    /// Gets the probability of collision, <see cref="CollisionRisk"/>'s <see cref="SSA.CollisionRisk.ProbabilityOfCollision"/>;
    /// null when it could not be computed.
    /// </summary>
    public double? ProbabilityOfCollision => CollisionRisk?.ProbabilityOfCollision;

    /// <summary>
    /// The TCA, the miss, the probability ("Pc n/a" without one), the combined hard-body radius and the quality flags, in
    /// invariant culture, for instance "TCA 2024-01-01T12:00:00.0000000 TDB, miss 250.000 m, Pc 0.0002715, HBR 13 m, None"
    /// (R17 of #668).
    /// </summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"TCA {Tca}, miss {MissDistanceMeters:F3} m, Pc {SSA.CollisionRisk.FormatProbability(ProbabilityOfCollision)}, HBR {CollisionRisk?.CombinedHardBodyRadiusMeters} m, {EncounterState.QualityFlags}");

    /// <summary>
    /// Exports this encounter as a CCSDS Conjunction Data Message.
    /// </summary>
    /// <param name="options">Optional CDM export overrides.</param>
    /// <returns>A CDM representation of this encounter.</returns>
    /// <remarks>
    /// <c>COLLISION_PROBABILITY_METHOD</c> is <c>FOSTER-1992</c> unless <see cref="CdmExportOptions.CollisionProbabilityMethod"/>
    /// names another (#618).
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The case holds no participant states, a participant has no covariance, the encounter's Pc is the maximum of a
    /// single covariance (<see cref="EncounterQualityFlags.SingleCovarianceMaximumPcUsed"/>, which no registered method
    /// names, #618), or its covariance is stale and <see cref="CdmExportOptions.AllowStaleCovariance"/> is not set.
    /// </exception>
    public Cdm ToCdm(CdmExportOptions options = null)
    {
        return CdmExporter.Export(this, options);
    }
}
