// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.Common
{
    /// <summary>
    /// Values whose shortest round-trip form is fixed-point with more than 14 decimals, which a writer that kept 14
    /// decimals lost (#728; a custom format keeps 15 significant digits at most, so 235.83190952229006 was lost as well), and the check of a written document against them: the text of each keyword read as
    /// <c>xsd:double</c>, the IEEE double nearest the decimal number (XML Schema Part 2, 3.2.5; .NET's
    /// <see cref="double.Parse(string, IFormatProvider)"/> rounds correctly), bit for bit the value given.
    /// </summary>
    internal static class CcsdsRoundTripValues
    {
        /// <summary>
        /// The <paramref name="index"/>-th value in [<paramref name="low"/>, <paramref name="high"/>), with 17 significant
        /// digits: low + (high − low) frac(index φ), φ the golden ratio.
        /// </summary>
        internal static double Value(int index, double low, double high)
        {
            double fraction = (index + 1) * 0.6180339887498949 % 1.0;
            return low + (high - low) * fraction;
        }

        /// <summary>
        /// Whether the form of the OPM and OMM writers before #728 loses <paramref name="value"/>: <c>G17</c> kept in scientific
        /// form, otherwise <c>0.##############</c>, at most 14 decimals and the 15 significant digits of .NET's custom format.
        /// </summary>
        internal static bool LostByTheFormerFormat(double value)
        {
            var g17 = value.ToString("G17", CultureInfo.InvariantCulture);
            return !g17.Contains('E') &&
                   double.Parse(value.ToString("0.##############", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) != value;
        }

        /// <summary>
        /// Checks that the text of each keyword of <paramref name="expected"/> in <paramref name="xml"/> reads as its value,
        /// bit for bit, and that at least <paramref name="minimumLost"/> of the values were lost by that form.
        /// </summary>
        internal static void AssertWrittenExactly(string xml, IReadOnlyDictionary<string, double> expected, int minimumLost)
        {
            var document = XDocument.Parse(xml);
            foreach (var (keyword, value) in expected)
            {
                var text = document.Descendants().Single(e => e.Name.LocalName == keyword).Value;
                var read = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                Assert.True(BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(read),
                    $"{keyword}: {value:R} written {text}, read {read:R}");
            }

            Assert.True(expected.Values.Count(LostByTheFormerFormat) >= minimumLost);
        }
    }
}
