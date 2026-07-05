using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace IO.Astrodynamics.CLI.Tests;

/// <summary>
/// Assertion helpers for CLI console output that embeds full-precision numbers.
/// </summary>
internal static class OutputAssert
{
    // Matches integers, decimals and scientific-notation numbers (with optional sign).
    private static readonly Regex NumberRx =
        new(@"-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?", RegexOptions.Compiled);

    /// <summary>
    /// Asserts two console outputs are equal, allowing embedded numbers to differ within a
    /// small relative tolerance. Round-trip <c>double.ToString()</c> output differs in its last
    /// digit(s) between architectures (e.g. x64 vs arm64) because libm rounds differently, so an
    /// exact string compare is not portable. This compares the non-numeric skeleton exactly and
    /// every number pairwise to <paramref name="relativeTolerance"/> (default ~11 significant digits).
    /// </summary>
    public static void EqualApprox(string expected, string actual, double relativeTolerance = 1e-9)
    {
        // The skeleton (every number blanked) must match exactly: same structure, labels,
        // punctuation, number count and positions.
        Assert.Equal(NumberRx.Replace(expected, "#"), NumberRx.Replace(actual, "#"));

        var expectedNumbers = NumberRx.Matches(expected);
        var actualNumbers = NumberRx.Matches(actual);
        Assert.Equal(expectedNumbers.Count, actualNumbers.Count);

        for (int i = 0; i < expectedNumbers.Count; i++)
        {
            double e = double.Parse(expectedNumbers[i].Value, CultureInfo.InvariantCulture);
            double a = double.Parse(actualNumbers[i].Value, CultureInfo.InvariantCulture);
            Assert.True(System.Math.Abs(e - a) <= relativeTolerance * System.Math.Max(1.0, System.Math.Abs(e)),
                $"Number #{i} differs beyond tolerance: expected {e}, actual {a}");
        }
    }
}
