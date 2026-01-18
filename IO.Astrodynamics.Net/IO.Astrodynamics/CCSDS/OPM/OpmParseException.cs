// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.CCSDS.OPM;

/// <summary>
/// Exception thrown when parsing an OPM document fails.
/// </summary>
public class OpmParseException : Exception
{
    /// <summary>
    /// Gets the line number where the parse error occurred (if available).
    /// </summary>
    public int? LineNumber { get; }

    /// <summary>
    /// Gets the column number where the parse error occurred (if available).
    /// </summary>
    public int? LinePosition { get; }

    /// <summary>
    /// Gets the element path where the parse error occurred (if available).
    /// </summary>
    public string ElementPath { get; }

    /// <summary>
    /// Creates a new OpmParseException with the specified message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public OpmParseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new OpmParseException with the specified message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public OpmParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a new OpmParseException with location information.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="lineNumber">The line number where the error occurred.</param>
    /// <param name="linePosition">The column number where the error occurred.</param>
    /// <param name="elementPath">The element path where the error occurred.</param>
    public OpmParseException(string message, int? lineNumber, int? linePosition, string elementPath = null)
        : base(FormatMessage(message, lineNumber, linePosition, elementPath))
    {
        LineNumber = lineNumber;
        LinePosition = linePosition;
        ElementPath = elementPath;
    }

    /// <summary>
    /// Creates a new OpmParseException with location information and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="lineNumber">The line number where the error occurred.</param>
    /// <param name="linePosition">The column number where the error occurred.</param>
    /// <param name="elementPath">The element path where the error occurred.</param>
    /// <param name="innerException">The inner exception.</param>
    public OpmParseException(string message, int? lineNumber, int? linePosition, string elementPath, Exception innerException)
        : base(FormatMessage(message, lineNumber, linePosition, elementPath), innerException)
    {
        LineNumber = lineNumber;
        LinePosition = linePosition;
        ElementPath = elementPath;
    }

    private static string FormatMessage(string message, int? lineNumber, int? linePosition, string elementPath)
    {
        var parts = new System.Collections.Generic.List<string> { message };

        if (lineNumber.HasValue || linePosition.HasValue)
        {
            var location = $"at line {lineNumber?.ToString() ?? "?"}, column {linePosition?.ToString() ?? "?"}";
            parts.Add(location);
        }

        if (!string.IsNullOrEmpty(elementPath))
        {
            parts.Add($"element: {elementPath}");
        }

        return string.Join(" ", parts);
    }
}
