// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Ccsds;

/// <summary>
/// How <c>Save</c> and <c>ToXml</c> of <see cref="Omm"/>, <see cref="Opm"/> and <see cref="Cdm"/> write a
/// message (R22 of #668, D1's options of the CCSDS write, decided by S. Guillet, 2026-11-19).
/// </summary>
public sealed record CcsdsWriteOptions
{
    /// <summary>The options of the default output, as <c>new()</c>.</summary>
    public static CcsdsWriteOptions Default { get; } = new();

    /// <summary>
    /// Whether the message is validated for content, as its <c>Validate()</c> does, before it is written; true by default.
    /// An error throws the validation exception of the message type and writes nothing. Before 11.0.0 <c>ToXml</c> and the
    /// CDM writers did not validate.
    /// </summary>
    public bool ValidateContent { get; init; } = true;

    /// <summary>Whether the message is wrapped in an <c>&lt;ndm&gt;</c> container; true by default.</summary>
    public bool WrapInNdmContainer { get; init; } = true;

    /// <summary>Whether the XML is indented; true by default.</summary>
    public bool Indent { get; init; } = true;
}
