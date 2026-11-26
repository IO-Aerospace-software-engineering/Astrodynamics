// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace IO.Astrodynamics.CCSDS.Common;

/// <summary>
/// The one verb set of the CCSDS messages (R22 of #668): the read of a file or a text, one message or every message, with
/// the checks of <see cref="CcsdsReadOptions"/>, and the check before a write of <see cref="CcsdsWriteOptions"/>, shared
/// by <see cref="OMM.Omm"/>, <see cref="OPM.Opm"/> and <see cref="CDM.Cdm"/> through their internal reader and validator.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
/// <typeparam name="TResult">The validation result of the message type.</typeparam>
internal sealed class CcsdsMessageIO<TMessage, TResult>
{
    private readonly string _type;
    private readonly Func<string, TResult> _schemaOfFile;
    private readonly Func<string, TResult> _schemaOfText;
    private readonly Func<XDocument, bool, IReadOnlyList<TMessage>> _read;
    private readonly Func<TMessage, TResult> _validate;
    private readonly Func<TResult, bool> _isValid;
    private readonly Func<string, TResult, CcsdsFormatException> _invalid;
    private readonly Func<string, Exception, CcsdsFormatException> _parseError;

    /// <param name="type">The message type as CCSDS names it (OMM, OPM, CDM).</param>
    /// <param name="schemaOfFile">The schema validation of a file.</param>
    /// <param name="schemaOfText">The schema validation of a text.</param>
    /// <param name="read">The reader: the one message of a document (false) or every message (true).</param>
    /// <param name="validate">The content validation of a message.</param>
    /// <param name="isValid">Whether a validation result holds no error.</param>
    /// <param name="invalid">The validation exception of the type.</param>
    /// <param name="parseError">The parse exception of the type.</param>
    internal CcsdsMessageIO(string type, Func<string, TResult> schemaOfFile, Func<string, TResult> schemaOfText,
        Func<XDocument, bool, IReadOnlyList<TMessage>> read, Func<TMessage, TResult> validate, Func<TResult, bool> isValid,
        Func<string, TResult, CcsdsFormatException> invalid, Func<string, Exception, CcsdsFormatException> parseError)
    {
        _type = type;
        _schemaOfFile = schemaOfFile;
        _schemaOfText = schemaOfText;
        _read = read;
        _validate = validate;
        _isValid = isValid;
        _invalid = invalid;
        _parseError = parseError;
    }

    /// <summary>The messages of a file: the one message (<paramref name="all"/> false) or every message.</summary>
    internal IReadOnlyList<TMessage> Load(string path, CcsdsReadOptions options, bool all)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{_type} file not found: {path}", path);

        return Read(() => XDocument.Load(path), () => _schemaOfFile(path), options, all);
    }

    /// <summary>The messages of a text: the one message (<paramref name="all"/> false) or every message.</summary>
    internal IReadOnlyList<TMessage> Parse(string xml, CcsdsReadOptions options, bool all)
    {
        ArgumentNullException.ThrowIfNull(xml);
        if (string.IsNullOrWhiteSpace(xml))
            throw _parseError($"The text holds no {_type} XML.", null);

        return Read(() => XDocument.Parse(xml), () => _schemaOfText(xml), options, all);
    }

    /// <summary>The messages of a reader, read to its end, as <see cref="Parse(string, CcsdsReadOptions, bool)"/>.</summary>
    internal IReadOnlyList<TMessage> Parse(TextReader reader, CcsdsReadOptions options, bool all)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return Parse(reader.ReadToEnd(), options, all);
    }

    /// <summary>The one message of a file, or false for a missing file or a document the checks or the reader refuse.</summary>
    internal bool TryLoad(string path, CcsdsReadOptions options, out TMessage result)
    {
        result = default;
        if (path == null || !File.Exists(path))
            return false;

        try
        {
            result = Load(path, options, false)[0];
            return true;
        }
        catch (CcsdsFormatException)
        {
            return false;
        }
    }

    /// <summary>The one message of a text, or false for a null text or a document the checks or the reader refuse.</summary>
    internal bool TryParse(string xml, CcsdsReadOptions options, out TMessage result)
    {
        result = default;
        if (xml == null)
            return false;

        try
        {
            result = Parse(xml, options, false)[0];
            return true;
        }
        catch (CcsdsFormatException)
        {
            return false;
        }
    }

    /// <summary>The check before a write: the content validation unless <see cref="CcsdsWriteOptions.ValidateContent"/>
    /// is false; an error throws the validation exception.</summary>
    internal void CheckBeforeWrite(TMessage message, CcsdsWriteOptions options)
    {
        if (!options.ValidateContent)
            return;

        var result = _validate(message);
        if (!_isValid(result))
            throw _invalid($"Content validation failed before writing the {_type}.", result);
    }

    // The schema once per document, then the reader, then the content of each message
    private IReadOnlyList<TMessage> Read(Func<XDocument> load, Func<TResult> schema, CcsdsReadOptions options, bool all)
    {
        options ??= CcsdsReadOptions.Default;
        if (options.ValidateSchema)
        {
            var schemaResult = schema();
            if (!_isValid(schemaResult))
                throw _invalid("Schema validation failed.", schemaResult);
        }

        XDocument document;
        try
        {
            document = load();
        }
        catch (XmlException ex)
        {
            throw _parseError($"Failed to parse {_type} XML.", ex);
        }

        var messages = _read(document, all);
        if (options.ValidateContent)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                var contentResult = _validate(messages[i]);
                if (!_isValid(contentResult))
                {
                    throw _invalid(messages.Count == 1
                        ? "Content validation failed."
                        : $"Content validation failed for {_type} {i + 1} of {messages.Count}.", contentResult);
                }
            }
        }

        return messages;
    }
}
