// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;

namespace IO.Astrodynamics.CCSDS.Common;

/// <summary>
/// Represents spacecraft physical parameters used in CCSDS Navigation Data Messages.
/// </summary>
/// <remarks>
/// As defined in CCSDS 502.0-B-3 (ODM Blue Book).
/// All parameters are optional and may be null if not specified.
/// </remarks>
public class SpacecraftParameters
{
    /// <summary>
    /// Gets the comments associated with the spacecraft parameters.
    /// </summary>
    public IReadOnlyList<string> Comments { get; }

    /// <summary>
    /// Gets the spacecraft mass in kilograms.
    /// </summary>
    /// <remarks>
    /// Must be non-negative if specified.
    /// </remarks>
    public double? Mass { get; }

    /// <summary>
    /// Gets the solar radiation pressure area in square meters.
    /// </summary>
    /// <remarks>
    /// Cross-sectional area exposed to solar radiation pressure.
    /// Must be non-negative if specified.
    /// </remarks>
    public double? SolarRadArea { get; }

    /// <summary>
    /// Gets the solar radiation pressure coefficient (dimensionless).
    /// </summary>
    /// <remarks>
    /// Typical values range from 1.0 (absorption) to 2.0 (specular reflection).
    /// Must be non-negative if specified.
    /// </remarks>
    public double? SolarRadCoeff { get; }

    /// <summary>
    /// Gets the atmospheric drag area in square meters.
    /// </summary>
    /// <remarks>
    /// Cross-sectional area for atmospheric drag calculations.
    /// Must be non-negative if specified.
    /// </remarks>
    public double? DragArea { get; }

    /// <summary>
    /// Gets the atmospheric drag coefficient (dimensionless).
    /// </summary>
    /// <remarks>
    /// Typical values range from 2.0 to 2.5 for most spacecraft.
    /// Must be non-negative if specified.
    /// </remarks>
    public double? DragCoeff { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpacecraftParameters"/> class.
    /// </summary>
    /// <param name="mass">Spacecraft mass in kilograms.</param>
    /// <param name="solarRadArea">Solar radiation pressure area in square meters.</param>
    /// <param name="solarRadCoeff">Solar radiation pressure coefficient.</param>
    /// <param name="dragArea">Atmospheric drag area in square meters.</param>
    /// <param name="dragCoeff">Atmospheric drag coefficient.</param>
    /// <param name="comments">Comments associated with the parameters.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is negative.</exception>
    public SpacecraftParameters(
        double? mass = null,
        double? solarRadArea = null,
        double? solarRadCoeff = null,
        double? dragArea = null,
        double? dragCoeff = null,
        IReadOnlyList<string> comments = null)
    {
        if (mass.HasValue && mass.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(mass), "Mass must be non-negative.");
        if (solarRadArea.HasValue && solarRadArea.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(solarRadArea), "Solar radiation area must be non-negative.");
        if (solarRadCoeff.HasValue && solarRadCoeff.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(solarRadCoeff), "Solar radiation coefficient must be non-negative.");
        if (dragArea.HasValue && dragArea.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(dragArea), "Drag area must be non-negative.");
        if (dragCoeff.HasValue && dragCoeff.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(dragCoeff), "Drag coefficient must be non-negative.");

        Mass = mass;
        SolarRadArea = solarRadArea;
        SolarRadCoeff = solarRadCoeff;
        DragArea = dragArea;
        DragCoeff = dragCoeff;
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Returns true if any parameter is set.
    /// </summary>
    public bool HasAnyValue => Mass.HasValue || SolarRadArea.HasValue || SolarRadCoeff.HasValue ||
                               DragArea.HasValue || DragCoeff.HasValue;

    public override string ToString()
    {
        return $"SpacecraftParameters[Mass={Mass?.ToString() ?? "N/A"} kg, DragArea={DragArea?.ToString() ?? "N/A"} m², Cd={DragCoeff?.ToString() ?? "N/A"}]";
    }
}
