// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.CCSDS.Common;

namespace IO.Astrodynamics.CCSDS.OPM;

/// <summary>
/// Represents the data section of an OPM message.
/// </summary>
/// <remarks>
/// <para>
/// An OPM data section contains:
/// - Required: State vector (position and velocity at epoch)
/// - Optional: Keplerian elements (alternative representation)
/// - Optional: Spacecraft parameters (mass, drag area, etc.)
/// - Optional: Covariance matrix
/// - Optional: Maneuver parameters (0..*)
/// </para>
/// </remarks>
public class OpmData
{
    /// <summary>
    /// Gets the state vector section (required).
    /// </summary>
    public OpmStateVector StateVector { get; }

    /// <summary>
    /// Gets the optional Keplerian elements section.
    /// </summary>
    public OpmKeplerianElements KeplerianElements { get; }

    /// <summary>
    /// Gets the spacecraft mass in kilograms.
    /// </summary>
    public double? Mass { get; }

    /// <summary>
    /// Gets the solar radiation pressure area in square meters.
    /// </summary>
    public double? SolarRadiationPressureArea { get; }

    /// <summary>
    /// Gets the solar radiation pressure coefficient (Cr).
    /// </summary>
    /// <remarks>
    /// Typical values range from 1.0 to 2.0. A value of 1.0 means complete absorption,
    /// 2.0 means complete reflection.
    /// </remarks>
    public double? SolarRadiationCoefficient { get; }

    /// <summary>
    /// Gets the drag area in square meters.
    /// </summary>
    public double? DragArea { get; }

    /// <summary>
    /// Gets the drag coefficient (Cd).
    /// </summary>
    /// <remarks>
    /// Typical values range from 2.0 to 2.5 for most spacecraft.
    /// </remarks>
    public double? DragCoefficient { get; }

    /// <summary>
    /// Gets the optional covariance matrix.
    /// </summary>
    public CovarianceMatrix Covariance { get; }

    /// <summary>
    /// Gets the list of maneuver parameters (may be empty).
    /// </summary>
    public IReadOnlyList<OpmManeuverParameters> Maneuvers { get; }

    /// <summary>
    /// Gets optional user-defined parameters.
    /// </summary>
    public OpmUserDefinedParameters UserDefinedParameters { get; }

    /// <summary>
    /// Gets optional comments associated with the data section.
    /// </summary>
    public IReadOnlyList<string> Comments { get; }

    /// <summary>
    /// Gets optional comments associated with the spacecraft parameters.
    /// </summary>
    public IReadOnlyList<string> SpacecraftComments { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpmData"/> class.
    /// </summary>
    /// <param name="stateVector">The state vector (required).</param>
    /// <param name="keplerianElements">Optional Keplerian elements.</param>
    /// <param name="mass">Optional spacecraft mass in kg.</param>
    /// <param name="solarRadiationPressureArea">Optional SRP area in m².</param>
    /// <param name="solarRadiationCoefficient">Optional SRP coefficient.</param>
    /// <param name="dragArea">Optional drag area in m².</param>
    /// <param name="dragCoefficient">Optional drag coefficient.</param>
    /// <param name="covariance">Optional covariance matrix.</param>
    /// <param name="maneuvers">Optional list of maneuvers.</param>
    /// <param name="userDefinedParameters">Optional user-defined parameters.</param>
    /// <param name="spacecraftComments">Optional spacecraft comments.</param>
    /// <param name="comments">Optional data-level comments.</param>
    public OpmData(
        OpmStateVector stateVector,
        OpmKeplerianElements keplerianElements = null,
        double? mass = null,
        double? solarRadiationPressureArea = null,
        double? solarRadiationCoefficient = null,
        double? dragArea = null,
        double? dragCoefficient = null,
        CovarianceMatrix covariance = null,
        IReadOnlyList<OpmManeuverParameters> maneuvers = null,
        OpmUserDefinedParameters userDefinedParameters = null,
        IReadOnlyList<string> spacecraftComments = null,
        IReadOnlyList<string> comments = null)
    {
        StateVector = stateVector ?? throw new ArgumentNullException(nameof(stateVector));

        if (mass.HasValue && mass.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(mass), "Mass must be positive.");
        if (solarRadiationPressureArea.HasValue && solarRadiationPressureArea.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(solarRadiationPressureArea), "SRP area cannot be negative.");
        if (solarRadiationCoefficient.HasValue && solarRadiationCoefficient.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(solarRadiationCoefficient), "SRP coefficient cannot be negative.");
        if (dragArea.HasValue && dragArea.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(dragArea), "Drag area cannot be negative.");
        if (dragCoefficient.HasValue && dragCoefficient.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(dragCoefficient), "Drag coefficient cannot be negative.");

        KeplerianElements = keplerianElements;
        Mass = mass;
        SolarRadiationPressureArea = solarRadiationPressureArea;
        SolarRadiationCoefficient = solarRadiationCoefficient;
        DragArea = dragArea;
        DragCoefficient = dragCoefficient;
        Covariance = covariance;
        Maneuvers = maneuvers ?? Array.Empty<OpmManeuverParameters>();
        UserDefinedParameters = userDefinedParameters;
        SpacecraftComments = spacecraftComments ?? Array.Empty<string>();
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Gets whether spacecraft parameters are present.
    /// </summary>
    public bool HasSpacecraftParameters =>
        Mass.HasValue ||
        SolarRadiationPressureArea.HasValue ||
        SolarRadiationCoefficient.HasValue ||
        DragArea.HasValue ||
        DragCoefficient.HasValue;

    /// <summary>
    /// Gets whether covariance data is present.
    /// </summary>
    public bool HasCovariance => Covariance != null;

    /// <summary>
    /// Gets whether Keplerian elements are present.
    /// </summary>
    public bool HasKeplerianElements => KeplerianElements != null;

    /// <summary>
    /// Gets whether any maneuvers are defined.
    /// </summary>
    public bool HasManeuvers => Maneuvers.Count > 0;

    /// <summary>
    /// Gets whether user-defined parameters are present.
    /// </summary>
    public bool HasUserDefinedParameters => UserDefinedParameters != null && UserDefinedParameters.HasParameters;

    /// <inheritdoc />
    public override string ToString()
    {
        var parts = new List<string> { "StateVector" };
        if (HasKeplerianElements) parts.Add("Keplerian");
        if (HasSpacecraftParameters) parts.Add("Spacecraft");
        if (HasCovariance) parts.Add("Covariance");
        if (HasManeuvers) parts.Add($"{Maneuvers.Count} Maneuvers");
        if (HasUserDefinedParameters) parts.Add("UserDefined");
        return $"OpmData[{string.Join(", ", parts)}]";
    }
}
