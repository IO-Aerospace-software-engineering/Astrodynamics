// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.CCSDS;

/// <summary>
/// How <c>Load</c>, <c>LoadAll</c>, <c>Parse</c>, <c>ParseAll</c>, <c>TryLoad</c> and <c>TryParse</c> of
/// <see cref="OMM.Omm"/>, <see cref="OPM.Opm"/> and <see cref="CDM.Cdm"/> read a document (R22 of #668, D1's options of the
/// CCSDS read, decided by S. Guillet, 2026-11-19).
/// </summary>
/// <remarks>
/// Both checks run by default for the three messages. Before 11.0.0 the OMM and OPM loaders took them as two bools and the
/// CDM readers ran neither.
/// </remarks>
public sealed record CcsdsReadOptions
{
    /// <summary>The options of every check, as <c>new()</c>.</summary>
    public static CcsdsReadOptions Default { get; } = new();

    /// <summary>
    /// Whether the document is validated against the CCSDS NDM/XML 4.0.0 schemas (CCSDS 505.0-B-3) in its form, before it is
    /// read, once per document; true by default. A failure throws the validation exception of the message type
    /// (<see cref="OMM.OmmValidationException"/>, <see cref="OPM.OpmValidationException"/>,
    /// <see cref="CDM.CdmValidationException"/>).
    /// </summary>
    public bool ValidateSchema { get; init; } = true;

    /// <summary>
    /// Whether each message read is validated for content (required values, ranges, consistency) as its <c>Validate()</c>
    /// does; true by default. An error throws the validation exception of the message type; warnings are not reported.
    /// </summary>
    public bool ValidateContent { get; init; } = true;
}
