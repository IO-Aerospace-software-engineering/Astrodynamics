// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Ccsds;

/// <summary>
/// The blocks <see cref="Body.Spacecraft.Spacecraft.ToOpm"/> writes besides the state (D1 of #668, lot L7: the three bools
/// of <c>ToOpm</c> before 11.0.0), every one by default.
/// </summary>
public sealed record OpmExportOptions
{
    /// <summary>The options of every block, as <c>new()</c>.</summary>
    public static OpmExportOptions Default { get; } = new();

    /// <summary>Whether the optional Keplerian elements of the state are written; true by default.</summary>
    public bool IncludeKeplerianElements { get; init; } = true;

    /// <summary>Whether the spacecraft parameters (mass, areas, drag and radiation coefficients) are written; true by
    /// default.</summary>
    public bool IncludeSpacecraftParameters { get; init; } = true;

    /// <summary>Whether the maneuvers ignited by the epoch are written (all those executed for a spacecraft without
    /// propagated states); true by default.</summary>
    public bool IncludeManeuvers { get; init; } = true;
}
