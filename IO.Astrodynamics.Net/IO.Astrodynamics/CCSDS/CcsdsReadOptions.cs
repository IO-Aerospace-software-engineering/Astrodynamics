// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Ccsds;

/// <summary>
/// How <c>Load</c>, <c>LoadAll</c>, <c>Parse</c>, <c>ParseAll</c>, <c>TryLoad</c> and <c>TryParse</c> of
/// <see cref="Omm"/>, <see cref="Opm"/> and <see cref="Cdm"/> read a document (R22 of #668, D1's options of the
/// CCSDS read, decided by S. Guillet, 2026-11-19).
/// </summary>
/// <remarks>
/// Both checks run by default for the three messages, with every check of the content (the three validator switches
/// null). Before 11.0.0 the OMM and OPM loaders took them as two bools and the
/// CDM readers ran neither.
/// </remarks>
public sealed record CcsdsReadOptions
{
    /// <summary>The options of every check, as <c>new()</c>.</summary>
    public static CcsdsReadOptions Default { get; } = new();

    /// <summary>
    /// Whether the document is validated against the CCSDS NDM/XML 4.0.0 schemas (CCSDS 505.0-B-3) in its form, before it is
    /// read, once per document; true by default. A failure throws the validation exception of the message type
    /// (<see cref="OmmValidationException"/>, <see cref="OpmValidationException"/>,
    /// <see cref="CdmValidationException"/>).
    /// </summary>
    public bool ValidateSchema { get; init; } = true;

    /// <summary>
    /// Whether each message read is validated for content (required values, ranges, consistency) as its <c>Validate()</c>
    /// does; true by default. An error throws the validation exception of the message type; warnings are not reported.
    /// </summary>
    public bool ValidateContent { get; init; } = true;

    /// <summary>
    /// Whether the content check of an OMM or an OPM checks the physical ranges of the elements and parameters; null, the
    /// default, checks them. The validator switch of the former public <c>OmmValidator</c> and <c>OpmValidator</c> (annex R22
    /// of #668); read only when <see cref="ValidateContent"/> is true.
    /// </summary>
    /// <remarks>false throws <see cref="System.ArgumentException"/> naming <c>options</c> on a CDM read, which has no such
    /// check.</remarks>
    public bool? ValidatePhysicalConstraints { get; init; }

    /// <summary>
    /// Whether the content check of an OMM that holds TLE parameters checks them (a positive <c>NORAD_CAT_ID</c>, the element
    /// set and revolution numbers); null, the default, checks them. The switch of the former public <c>OmmValidator</c>
    /// (annex R22 of #668); read only when <see cref="ValidateContent"/> is true.
    /// </summary>
    /// <remarks>false throws <see cref="System.ArgumentException"/> naming <c>options</c> on an OPM or a CDM read, which
    /// have no such check.</remarks>
    public bool? ValidateTleRequirements { get; init; }

    /// <summary>
    /// Whether the content check of an OMM or an OPM adds a warning for each recommended field that is missing; null, the
    /// default, adds them. Warnings never refuse a read: they appear in the <c>ValidationResult</c> of the validation
    /// exception of a refused one. The switch of the former public <c>OmmValidator</c> and <c>OpmValidator</c> (annex R22
    /// of #668); read only when <see cref="ValidateContent"/> is true.
    /// </summary>
    /// <remarks>false throws <see cref="System.ArgumentException"/> naming <c>options</c> on a CDM read, which has no such
    /// check.</remarks>
    public bool? WarnOnMissingOptionalFields { get; init; }
}
