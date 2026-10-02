// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Represents one conjunction case, including geometry, risk metrics, and optional participant snapshots at TCA.
/// </summary>
public sealed class EncounterCase
{
    /// <summary>
    /// Initializes an encounter case without retaining per-object state snapshots.
    /// </summary>
    public EncounterCase(
        ProtectedSpacecraftProfile protectedAsset,
        ILocalizable secondaryObject,
        Window screeningWindow,
        EncounterState encounterState,
        CollisionRisk collisionRisk)
        : this(
            protectedAsset,
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
    public EncounterCase(
        ProtectedSpacecraftProfile protectedAsset,
        ILocalizable secondaryObject,
        Window screeningWindow,
        EncounterState encounterState,
        CollisionRisk collisionRisk,
        StateVector protectedState,
        StateVector secondaryState)
    {
        ProtectedAsset = protectedAsset ?? throw new ArgumentNullException(nameof(protectedAsset));
        SecondaryObject = secondaryObject ?? throw new ArgumentNullException(nameof(secondaryObject));
        ScreeningWindow = screeningWindow;
        EncounterState = encounterState ?? throw new ArgumentNullException(nameof(encounterState));
        CollisionRisk = collisionRisk;
        ProtectedState = protectedState;
        SecondaryState = secondaryState;
    }

    /// <summary>
    /// Gets the protected spacecraft profile that was analyzed.
    /// </summary>
    public ProtectedSpacecraftProfile ProtectedAsset { get; }

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
    /// Exports this encounter as a CCSDS Conjunction Data Message.
    /// </summary>
    /// <param name="options">Optional CDM export overrides.</param>
    /// <returns>A CDM representation of this encounter.</returns>
    public Cdm ToCdm(CdmExportOptions options = null)
    {
        return CdmExporter.Export(this, options);
    }
}
