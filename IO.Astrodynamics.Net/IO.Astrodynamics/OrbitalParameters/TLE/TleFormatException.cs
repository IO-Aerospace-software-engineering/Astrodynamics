// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.OrbitalParameters.TLE;

/// <summary>
/// The text of a Two-Line Element set does not follow the format: a line of another length or line number, a catalog number
/// that is not one or differs between the lines, a wrong checksum, or a field whose text its columns cannot hold.
/// </summary>
/// <remarks>
/// R21 of #668: the <see cref="Tle"/> constructor throws <see cref="ArgumentException"/> naming <c>line1</c> or
/// <c>line2</c> with this exception as its <see cref="Exception.InnerException"/>.
/// </remarks>
public class TleFormatException : FormatException
{
    /// <summary>
    /// Creates the exception of a field at a line and a column.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="line">The line of the text, 1-based (1 or 2 for the lines of one set).</param>
    /// <param name="column">The first column of the field at fault, 1-based in TLE columns.</param>
    public TleFormatException(string message, int line, int column) : base(message)
    {
        Line = line;
        Column = column;
    }

    /// <summary>The line of the text, 1-based: 1 or 2 for the lines of one set.</summary>
    public int Line { get; }

    /// <summary>The first column of the field at fault, 1-based in TLE columns (69 for the checksum).</summary>
    public int Column { get; }
}
