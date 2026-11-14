// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// Also compiled into IO.Astrodynamics.CLI.Tests, whose nullable context is enabled
#nullable disable

using System;
using System.Globalization;
using System.Linq;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The values of the lines "title : value" that <c>CelestialItem.ToString</c> and <c>CelestialBody.ToString</c> print
/// (#681), read back to compare them with the properties they print.
/// </summary>
/// <remarks>The CLI tests link this file.</remarks>
internal static class ToStringLines
{
    /// <summary>
    /// The value printed under <paramref name="title"/> (the title with its colon, e.g. <c>GM (m^3/s^2) :</c>), or throws
    /// when no line carries that title.
    /// </summary>
    internal static string Value(string text, string title)
    {
        var line = text.Split(Environment.NewLine).SingleOrDefault(l => l.TrimStart().StartsWith(title + " ", StringComparison.Ordinal));
        if (line == null)
        {
            throw new InvalidOperationException($"No line titled '{title}' in{Environment.NewLine}{text}");
        }

        return line.TrimStart().Substring(title.Length).Trim();
    }

    /// <summary>
    /// The value printed under <paramref name="title"/>, parsed as a double in the invariant culture.
    /// </summary>
    internal static double Number(string text, string title)
    {
        return double.Parse(Value(text, title), NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
