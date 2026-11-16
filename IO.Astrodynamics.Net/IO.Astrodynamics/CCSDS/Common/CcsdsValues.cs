// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

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
    /// Whether an optional parameter of the data model is absent or a finite value of 0 or more (nonNegativeDouble of
    /// NDM/XML 4.0.0, a real number): false for NaN and ±Infinity (#708).
    /// </summary>
    /// <param name="value">The value, or null when the keyword is absent.</param>
    /// <returns>True when <paramref name="value"/> is null or finite and not below 0.</returns>
    internal static bool IsNullOrFiniteNonNegative(double? value) =>
        !value.HasValue || (double.IsFinite(value.Value) && value.Value >= 0);

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
