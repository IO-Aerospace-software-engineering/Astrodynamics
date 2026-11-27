// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Xml.Linq;

namespace IO.Astrodynamics.Ccsds;

/// <summary>
/// Finds the child elements of a message that the NDM/XML schema bounds to one (the header, the body, the segment, its
/// metadata and data sections, the blocks of the data), so that a reader does not drop a second one silently.
/// </summary>
internal static class CcsdsElements
{
    /// <summary>
    /// The one child of <paramref name="parent"/> named <paramref name="localName"/>, compared ignoring case, or null when
    /// it has none.
    /// </summary>
    /// <param name="parent">The parent element.</param>
    /// <param name="localName">The local name of the child (<c>header</c>, <c>segment</c>, <c>meanElements</c>, ...).</param>
    /// <param name="messageType">The type of the message, for the error (<c>OMM</c>, <c>OPM</c>, <c>CDM</c>).</param>
    /// <param name="parseException">Builds the reader's parse exception from a message.</param>
    /// <returns>The child, or null when there is none.</returns>
    /// <exception cref="Exception">
    /// The reader's parse exception when <paramref name="parent"/> holds several such children: the NDM/XML schema allows
    /// one, and a reader of one would drop the others, a whole segment for a second segment.
    /// </exception>
    internal static XElement AtMostOne(XElement parent, string localName, string messageType,
        Func<string, Exception> parseException)
    {
        XElement found = null;
        int count = 0;
        foreach (var element in parent.Elements())
        {
            if (!element.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))
                continue;

            count++;
            found ??= element;
        }

        if (count > 1)
            throw parseException(Several(parent, localName, messageType, count));

        return found;
    }

    /// <summary>
    /// The message of a parent holding <paramref name="count"/> children of a name the schema bounds to one.
    /// </summary>
    internal static string Several(XElement parent, string localName, string messageType, int count)
    {
        var holder = $"The {messageType} {parent.Name.LocalName} holds {count} {localName} elements";
        return localName.Equals("segment", StringComparison.OrdinalIgnoreCase)
            ? $"{holder}; an {messageType} holds one segment (CCSDS 502.0-B-3; NDM/XML {messageType.ToLowerInvariant()}Body). " +
              "Split it into one message per segment."
            : $"{holder}; NDM/XML allows one {localName} per {parent.Name.LocalName} (CCSDS 505.0-B-3).";
    }
}
