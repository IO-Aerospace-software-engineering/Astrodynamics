// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// Model files built from EGM2008_to70_TideFree for the tests of the degree check of the geopotential (#689): the file
/// with explicit zero rows beyond degree 70, or with rows removed.
/// </summary>
internal static class GeopotentialTestModels
{
    /// <summary>The path of EGM2008_to70_TideFree in the test data.</summary>
    internal const string To70Path = "Data/SolarSystem/EGM2008_to70_TideFree";

    /// <summary>The highest degree of EGM2008_to70_TideFree.</summary>
    internal const int To70Degree = 70;

    /// <summary>
    /// EGM2008_to70_TideFree followed by explicit zero rows for every (n, m) with 70 &lt; n ≤ <paramref name="degree"/>:
    /// a complete model file of that degree whose harmonics beyond 70 are zeros.
    /// </summary>
    internal static MemoryStream PaddedTo(int degree)
    {
        var text = new StringBuilder(File.ReadAllText(To70Path));
        if (text.Length > 0 && text[^1] != '\n')
        {
            text.Append('\n');
        }

        for (int n = To70Degree + 1; n <= degree; n++)
        {
            for (int m = 0; m <= n; m++)
            {
                text.Append(string.Format(CultureInfo.InvariantCulture,
                    "{0,5}{1,5}    0.000000000000000D+00    0.000000000000000D+00    0.0000000000D+00    0.0000000000D+00\n",
                    n, m));
            }
        }

        return new MemoryStream(Encoding.ASCII.GetBytes(text.ToString()));
    }

    /// <summary>EGM2008_to70_TideFree without the rows of the given (n, m) pairs.</summary>
    internal static MemoryStream Without(params (int n, int m)[] removed)
    {
        var lines = File.ReadAllLines(To70Path).Where(line =>
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return fields.Length < 2 || !removed.Contains((int.Parse(fields[0], CultureInfo.InvariantCulture),
                int.Parse(fields[1], CultureInfo.InvariantCulture)));
        });
        return new MemoryStream(Encoding.ASCII.GetBytes(string.Join("\n", lines) + "\n"));
    }
}
