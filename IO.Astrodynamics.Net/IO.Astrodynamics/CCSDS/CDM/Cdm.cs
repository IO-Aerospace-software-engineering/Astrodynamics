// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using IO.Astrodynamics.CCSDS.Common;

namespace IO.Astrodynamics.CCSDS.CDM;

public sealed class Cdm
{
    private static readonly CdmReader DefaultReader = new();
    private static readonly CdmValidator DefaultValidator = new();

    public const string Version = "1.0";
    public const string FormatId = "CCSDS_CDM_VERS";

    public Cdm(CdmHeader header, CdmRelativeMetadataData relativeMetadataData, IReadOnlyList<CdmSegment> segments)
    {
        Header = header ?? throw new ArgumentNullException(nameof(header));
        RelativeMetadataData = relativeMetadataData ?? throw new ArgumentNullException(nameof(relativeMetadataData));
        Segments = segments ?? throw new ArgumentNullException(nameof(segments));

        if (Segments.Count != 2)
        {
            throw new ArgumentException("CDM must contain exactly two segments.", nameof(segments));
        }
    }

    public CdmHeader Header { get; }

    public CdmRelativeMetadataData RelativeMetadataData { get; }

    public IReadOnlyList<CdmSegment> Segments { get; }

    #region Read and write (R22 of #668)

    // The one verb set of the CCSDS messages over the internal reader and validator
    private static readonly CcsdsMessageIO<Cdm, CdmValidationResult> Format = new("CDM",
        DefaultValidator.ValidateSchema, DefaultValidator.ValidateSchemaFromXml, DefaultReader.Read,
        DefaultValidator.Validate, result => result.IsValid, (message, result) => new CdmValidationException(message, result),
        (message, inner) => new CdmParseException(message, inner));

    /// <summary>
    /// Reads the one CDM of a file: a standalone <c>cdm</c> or an <c>ndm</c> that holds one (R22 of #668).
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="options">The checks, every one by default (<see cref="CcsdsReadOptions.Default"/>): the NDM/XML 4.0.0
    /// schema in the form of the document, then the content.</param>
    /// <returns>The CDM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="CdmValidationException">The schema or the content check fails.</exception>
    /// <exception cref="CdmParseException">The document cannot be read: no CDM, several (<see cref="LoadAll"/> reads them), or a
    /// keyword whose value the data model refuses, naming the keyword and its value.</exception>
    public static Cdm Load(string path, CcsdsReadOptions? options = null) => Format.Load(path, options, false)[0];

    /// <summary>
    /// Reads every CDM of a file, in document order, as <see cref="Load"/> reads one: the schema checked once for the
    /// document, the content of each message (R22 of #668).
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The messages; a document that holds none throws.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="CdmValidationException">The schema or the content check of a message fails.</exception>
    /// <exception cref="CdmParseException">The document holds no CDM or one that cannot be read.</exception>
    public static IReadOnlyList<Cdm> LoadAll(string path, CcsdsReadOptions? options = null) => Format.Load(path, options, true);

    /// <summary>
    /// Reads the one CDM of an XML text, as <see cref="Load"/> reads a file.
    /// </summary>
    /// <param name="xml">The XML text.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The CDM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="xml"/> is null.</exception>
    /// <exception cref="CdmValidationException">The schema or the content check fails.</exception>
    /// <exception cref="CdmParseException">The text is blank or cannot be read, or holds no CDM or several
    /// (<see cref="ParseAll(string, CcsdsReadOptions)"/> reads them).</exception>
    public static Cdm Parse(string xml, CcsdsReadOptions? options = null) => Format.Parse(xml, options, false)[0];

    /// <summary>
    /// Reads the one CDM of a reader, read to its end, as <see cref="Parse(string, CcsdsReadOptions)"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The CDM.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="CdmValidationException">The schema or the content check fails.</exception>
    /// <exception cref="CdmParseException">The text cannot be read, or holds no CDM or several.</exception>
    public static Cdm Parse(TextReader reader, CcsdsReadOptions? options = null) => Format.Parse(reader, options, false)[0];

    /// <summary>
    /// Reads every CDM of an XML text, as <see cref="LoadAll"/> reads a file.
    /// </summary>
    /// <param name="xml">The XML text.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The messages, in document order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="xml"/> is null.</exception>
    /// <exception cref="CdmValidationException">The schema or the content check of a message fails.</exception>
    /// <exception cref="CdmParseException">The text is blank or cannot be read, or holds no CDM.</exception>
    public static IReadOnlyList<Cdm> ParseAll(string xml, CcsdsReadOptions? options = null) => Format.Parse(xml, options, true);

    /// <summary>
    /// Reads every CDM of a reader, read to its end, as <see cref="ParseAll(string, CcsdsReadOptions)"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>The messages, in document order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="CdmValidationException">The schema or the content check of a message fails.</exception>
    /// <exception cref="CdmParseException">The text cannot be read, or holds no CDM.</exception>
    public static IReadOnlyList<Cdm> ParseAll(TextReader reader, CcsdsReadOptions? options = null) => Format.Parse(reader, options, true);

    /// <summary>
    /// <see cref="Load"/> without an exception for a file that does not exist or that the checks or the reader refuse.
    /// </summary>
    /// <param name="path">The path of the file, or null.</param>
    /// <param name="result">The CDM, or null.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>Whether the file exists and holds one CDM that passes the checks; an error reading an existing file
    /// throws, as <see cref="Load"/> does. Use <see cref="Load"/> for the reason of a refusal.</returns>
    public static bool TryLoad(string? path, [NotNullWhen(true)] out Cdm? result, CcsdsReadOptions? options = null) => Format.TryLoad(path, options, out result);

    /// <summary>
    /// <see cref="Parse(string, CcsdsReadOptions)"/> without an exception for a text that the checks or the reader refuse.
    /// </summary>
    /// <param name="xml">The XML text, or null.</param>
    /// <param name="result">The CDM, or null.</param>
    /// <param name="options">The checks, every one by default.</param>
    /// <returns>Whether the text holds one CDM that passes the checks.</returns>
    public static bool TryParse(string? xml, [NotNullWhen(true)] out Cdm? result, CcsdsReadOptions? options = null) => Format.TryParse(xml, options, out result);

    /// <summary>
    /// Writes the CDM to a file, in UTF-8, replacing it (R22 of #668).
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="options">The output, by default validated, wrapped in an <c>ndm</c> and indented
    /// (<see cref="CcsdsWriteOptions.Default"/>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or blank.</exception>
    /// <exception cref="CdmValidationException">The content check fails; nothing is written.</exception>
    public void Save(string path, CcsdsWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        options ??= CcsdsWriteOptions.Default;
        Format.CheckBeforeWrite(this, options);
        new CdmWriter { IndentOutput = options.Indent }.WriteToFile(this, path, options.WrapInNdmContainer);
    }

    /// <summary>
    /// Writes the CDM to a stream, in UTF-8, as <see cref="Save(string, CcsdsWriteOptions)"/>; the stream stays open.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="options">The output, by default validated, wrapped in an <c>ndm</c> and indented.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="CdmValidationException">The content check fails; nothing is written.</exception>
    public void Save(Stream stream, CcsdsWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= CcsdsWriteOptions.Default;
        Format.CheckBeforeWrite(this, options);
        new CdmWriter { IndentOutput = options.Indent }.WriteToStream(this, stream, options.WrapInNdmContainer);
    }

    /// <summary>
    /// The CDM as an XML text, as <see cref="Save(string, CcsdsWriteOptions)"/> writes it (R22 of #668).
    /// </summary>
    /// <param name="options">The output, by default validated, wrapped in an <c>ndm</c> and indented; before 11.0.0
    /// the text was not validated.</param>
    /// <returns>The XML text.</returns>
    /// <exception cref="CdmValidationException">The content check fails.</exception>
    public string ToXml(CcsdsWriteOptions? options = null)
    {
        options ??= CcsdsWriteOptions.Default;
        Format.CheckBeforeWrite(this, options);
        return new CdmWriter { IndentOutput = options.Indent }.WriteToString(this, options.WrapInNdmContainer);
    }

    /// <summary>
    /// Validates the content of this CDM: required values, ranges, consistency.
    /// </summary>
    /// <returns>The errors, warnings and notes.</returns>
    public CdmValidationResult Validate() => DefaultValidator.Validate(this);

    /// <summary>
    /// Whether <see cref="Validate"/> reports no error.
    /// </summary>
    /// <returns>True when the content holds no error.</returns>
    public bool IsValid() => Validate().IsValid;

    #endregion
}

