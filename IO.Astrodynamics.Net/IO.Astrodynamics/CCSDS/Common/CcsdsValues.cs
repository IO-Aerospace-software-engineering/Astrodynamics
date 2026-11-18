// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.CCSDS.Common;

/// <summary>
/// A keyword of a message read into a parameter of a data-model constructor: the parameter's name, the keyword, the text
/// of its element and, when the rule on its value depends on another keyword, that keyword and its text.
/// </summary>
/// <param name="Parameter">The name of the constructor parameter (<c>eccentricity</c>, <c>objectName</c>, ...).</param>
/// <param name="Keyword">The keyword (<c>ECCENTRICITY</c>, <c>OBJECT_NAME</c>, ...).</param>
/// <param name="Text">The text of its element, as read.</param>
/// <param name="With">The keyword the rule also depends on, with its text (<c>ECCENTRICITY '1.5'</c>), or null.</param>
internal readonly record struct CcsdsKeyword(string Parameter, string Keyword, string Text, string With = null);

/// <summary>
/// Builds the data model of a message from the values a reader parsed, so that a value the data model refuses throws the
/// reader's parse exception naming its keyword and its text, not the <see cref="ArgumentException"/> of a constructor,
/// which names its parameter (fourth round of #668).
/// </summary>
/// <remarks>
/// The public constructors keep their argument checks, and their <see cref="ArgumentException"/> for a programmatic caller;
/// only the readers translate it. The rules are the data model's, not repeated here.
/// </remarks>
internal static class CcsdsValues
{
    /// <summary>
    /// The text the OPM, OMM and CDM writers write for a real value: its shortest round-trip form (<c>"R"</c>, invariant
    /// culture), which reads back as <c>xsd:double</c> (the IEEE double nearest the decimal number) bit for bit, in the
    /// lexical space of <c>xsd:double</c> for every finite value (the data model holds no other).
    /// </summary>
    /// <remarks>
    /// Before #728 the OPM and OMM writers kept <c>G17</c> only in scientific form and otherwise wrote
    /// <c>0.##############</c>, at most 14 decimals and 15 significant digits: a value below 1e-3 lost up to 4.9e-11 of
    /// itself (an eccentricity of 0.00012345678901234567 written 0.00012345678901), a written message no longer read back
    /// as the values written. The CDM writer wrote <c>G17</c>, exact but not the shortest form (0.1 written
    /// 0.10000000000000001).
    /// </remarks>
    internal static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    /// Whether an optional parameter of the data model is absent or a finite value of 0 or more: the nonNegativeDouble of
    /// NDM/XML 4.0.0 (minInclusive 0.0, which excludes NaN but not +INF, XML Schema Part 2, 3.2.5) taken as a real number,
    /// false for NaN and ±Infinity (#708).
    /// </summary>
    /// <param name="value">The value, or null when the keyword is absent.</param>
    /// <returns>True when <paramref name="value"/> is null or finite and not below 0.</returns>
    internal static bool IsNullOrFiniteNonNegative(double? value) =>
        !value.HasValue || (double.IsFinite(value.Value) && value.Value >= 0);

    /// <summary>
    /// A value of the data model without a range: a real number, the <c>xsd:double</c> of NDM/XML 4.0.0 the readers read
    /// (#708), so that NaN and ±Infinity, which the writers would write as <c>NaN</c>, <c>Infinity</c> or <c>-Infinity</c>
    /// and the readers refuse, are refused where the data model receives them (#717).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="parameter">The name of the parameter or property that receives it.</param>
    /// <returns><paramref name="value"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is NaN or ±Infinity.</exception>
    internal static double RequireFinite(double value, string parameter)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(parameter, value, $"{parameter} must be a finite number.");
        return value;
    }

    /// <summary>
    /// A value of the data model in a CCSDS unit (km, km/s, km², km²/s, km²/s²) that the library converts to SI units: a
    /// real number whose product by <paramref name="factor"/>, its value in SI units, is a real number too (#729), so that
    /// the conversion does not round to ±Infinity (IEEE 754), as the CDM reader refuses a value in km whose value in m is
    /// beyond the range of a double (#708).
    /// </summary>
    /// <param name="value">The value in its CCSDS unit.</param>
    /// <param name="factor">The factor of the conversion to SI units (1000 for km and km/s, 1e6 for km², km²/s and km²/s²).</param>
    /// <param name="units">The CCSDS unit, for the message.</param>
    /// <param name="parameter">The name of the parameter that receives it.</param>
    /// <returns><paramref name="value"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is NaN or ±Infinity, or its product by
    /// <paramref name="factor"/> is not finite (above about 1.8e305 km or 1.8e302 km²).</exception>
    internal static double RequireFiniteInSiUnits(double value, double factor, string units, string parameter)
    {
        RequireFinite(value, parameter);
        if (!double.IsFinite(value * factor))
            throw new ArgumentOutOfRangeException(parameter, value,
                $"{parameter} must be finite in SI units: {value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)} {units} times {factor.ToString("R", System.Globalization.CultureInfo.InvariantCulture)} is beyond the range of a double.");
        return value;
    }

    /// <summary>
    /// An optional value of the data model without a range: null (the keyword absent) or a real number (#717).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is NaN or ±Infinity.</exception>
    internal static double? RequireNullOrFinite(double? value, string parameter)
    {
        if (value.HasValue)
            RequireFinite(value.Value, parameter);
        return value;
    }

    /// <summary>
    /// A vector of the data model: three real numbers (#717).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A component of <paramref name="value"/> is NaN or ±Infinity.</exception>
    internal static Vector3 RequireFinite(Vector3 value, string parameter)
    {
        if (!(double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z)))
            throw new ArgumentOutOfRangeException(parameter, value, $"{parameter} must have finite components.");
        return value;
    }

    /// <summary>
    /// A matrix of the data model: real numbers in every term, whatever its size (#717). The terms are read once, as given;
    /// the matrix is not copied, so a record that holds it receives a copy (<see cref="Copy"/>, review of #717).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A term of <paramref name="value"/> is NaN or ±Infinity.</exception>
    internal static Matrix RequireFinite(Matrix value, string parameter)
    {
        for (int row = 0; row < value.Rows; row++)
        {
            for (int column = 0; column < value.Columns; column++)
            {
                var term = value.Get(row, column);
                if (!double.IsFinite(term))
                    throw new ArgumentOutOfRangeException(parameter, term,
                        $"{parameter} must have finite terms; the term ({row}, {column}) is not.");
            }
        }

        return value;
    }

    /// <summary>
    /// A copy of a matrix that shares no array with it, since <see cref="Matrix.Set"/> writes the array a
    /// <see cref="Matrix"/> wraps: a record of the data model holds and returns copies, so that a term set on the matrix
    /// it was given or on the one it returned does not reach it (review of #717). A matrix without terms
    /// (<c>default(Matrix)</c> included, whose array is null) is returned as it is.
    /// </summary>
    /// <param name="value">The matrix.</param>
    /// <returns>A matrix of the same size and terms over a new array.</returns>
    internal static Matrix Copy(Matrix value) => value.Copy();

    /// <summary>
    /// Runs <paramref name="construct"/>; an <see cref="ArgumentException"/> it throws (an
    /// <see cref="ArgumentOutOfRangeException"/> or <see cref="ArgumentNullException"/> included) becomes the reader's parse
    /// exception, naming the keyword whose parameter it names, with its text, and holding it as inner exception.
    /// </summary>
    /// <typeparam name="T">The type of the data-model object.</typeparam>
    /// <param name="construct">Builds the object from the parsed values.</param>
    /// <param name="block">The NDM/XML element that holds the keywords (<c>meanElements</c>, <c>metadata</c>, ...).</param>
    /// <param name="parseException">Builds the reader's parse exception from a message and an inner exception.</param>
    /// <param name="keywords">The keywords read into the parameters of the constructor.</param>
    /// <returns>The object built.</returns>
    /// <exception cref="Exception">The reader's parse exception when the data model refuses a value.</exception>
    internal static T Construct<T>(Func<T> construct, string block, Func<string, Exception, Exception> parseException,
        params CcsdsKeyword[] keywords)
    {
        try
        {
            return construct();
        }
        catch (ArgumentException ex)
        {
            throw parseException(Message(ex, block, keywords), ex);
        }
    }

    /// <summary>
    /// The message of a value the data model refused: the keyword whose parameter <paramref name="ex"/> names, its text and
    /// the data model's reason; the block when no keyword is mapped to that parameter.
    /// </summary>
    private static string Message(ArgumentException ex, string block, CcsdsKeyword[] keywords)
    {
        var reason = Reason(ex);
        foreach (var keyword in keywords)
        {
            if (!string.Equals(keyword.Parameter, ex.ParamName, StringComparison.Ordinal))
                continue;

            var with = keyword.With == null ? string.Empty : $" with {keyword.With}";
            return $"{keyword.Keyword} '{keyword.Text}' in {block}{with} is invalid: {reason}";
        }

        return $"The {block} is invalid: {reason}";
    }

    // The data model's message without the " (Parameter 'name')" that ArgumentException appends
    private static string Reason(ArgumentException ex)
    {
        var message = ex.Message;
        if (ex.ParamName != null)
        {
            var suffix = $" (Parameter '{ex.ParamName}')";
            var index = message.IndexOf(suffix, StringComparison.Ordinal);
            if (index >= 0)
                message = message.Remove(index, suffix.Length);
        }

        return message;
    }
}
