// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// Also compiled into IO.Astrodynamics.CLI.Tests, whose nullable context is enabled
#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The values of a text kernel read from its text, independently of the kernel pool of CSPICE (#681): the assignments
/// <c>NAME = ( v1 v2 … )</c> of its data blocks, between <c>\begindata</c> and <c>\begintext</c> (Kernel Required Reading,
/// "Text Kernel Format"), a value written with the exponent letter <c>D</c> or <c>E</c>; the last assignment of a name
/// is its value, as the kernel pool keeps it.
/// </summary>
/// <remarks>
/// Also the reader of the lines of <c>CelestialItem.ToString</c> and <c>CelestialBody.ToString</c>, "title : value".
/// The CLI tests link this file.
/// </remarks>
internal static class TextKernelReference
{
    private static readonly Regex Assignment = new(@"(?<name>[A-Za-z0-9_]+)\s*=\s*\((?<values>[^)]*)\)", RegexOptions.Compiled);

    /// <summary>
    /// The values of <paramref name="name"/> in the data blocks of the text kernel <paramref name="path"/>.
    /// </summary>
    internal static double[] Read(string path, string name)
    {
        var data = new List<string>();
        var inData = false;
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed == @"\begindata")
            {
                inData = true;
                continue;
            }

            if (trimmed == @"\begintext")
            {
                inData = false;
                continue;
            }

            if (inData)
            {
                data.Add(line);
            }
        }

        var match = Assignment.Matches(string.Join(" ", data)).LastOrDefault(m => m.Groups["name"].Value == name);
        if (match == null)
        {
            throw new InvalidOperationException($"{name} is not assigned in the data blocks of {path}");
        }

        return match.Groups["values"].Value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => double.Parse(v.Replace('D', 'E').Replace('d', 'e'), NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
    }

    /// <summary>
    /// The value printed under <paramref name="title"/> (the title with its colon, e.g. <c>GM (m^3/s^2) :</c>) by a
    /// ToString of the body types, or throws when no line carries that title.
    /// </summary>
    internal static string PrintedValue(string text, string title)
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
    internal static double PrintedNumber(string text, string title)
    {
        return double.Parse(PrintedValue(text, title), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The distance in units in the last place between two finite doubles of the same sign.
    /// </summary>
    internal static long Ulps(double a, double b)
    {
        return System.Math.Abs(BitConverter.DoubleToInt64Bits(a) - BitConverter.DoubleToInt64Bits(b));
    }
}
