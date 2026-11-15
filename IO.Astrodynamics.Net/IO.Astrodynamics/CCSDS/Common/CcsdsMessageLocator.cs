// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Xml.Linq;

namespace IO.Astrodynamics.CCSDS.Common;

/// <summary>
/// Finds the one message element of a type (<c>omm</c>, <c>opm</c>, <c>cdm</c>) that a single-message reader reads.
/// </summary>
internal static class CcsdsMessageLocator
{
    /// <summary>
    /// Finds the message element: the root itself when it is the message, otherwise the message child of an
    /// <c>&lt;ndm&gt;</c> root, or the message descendant of any other root, compared ignoring case.
    /// </summary>
    /// <remarks>
    /// The messages are counted over every descendant of the root, an <c>&lt;ndm&gt;</c> included: a message below a child
    /// of an NDM (a wrapper, an inner NDM), which the schema rejects, counts, so such a document is not read as its first
    /// message. The message read from an <c>&lt;ndm&gt;</c> is still one of its children, as before 11.0.0: an NDM whose
    /// only message is below another child holds none.
    /// </remarks>
    /// <param name="root">The root element of the document.</param>
    /// <param name="messageName">The local name of the message element (<c>omm</c>, <c>opm</c>, <c>cdm</c>).</param>
    /// <param name="parseException">Builds the reader's parse exception from a message.</param>
    /// <returns>The message element, or null when the document holds none.</returns>
    /// <exception cref="Exception">
    /// The reader's parse exception when the document holds several messages of the type (#679): a single-message reader
    /// does not drop the others silently. Messages of other types (an OPM beside an OMM) are not counted.
    /// </exception>
    internal static XElement FindSingle(XElement root, string messageName, Func<string, Exception> parseException)
    {
        if (root.Name.LocalName.Equals(messageName, StringComparison.OrdinalIgnoreCase))
            return root;

        bool IsMessage(XElement e) => e.Name.LocalName.Equals(messageName, StringComparison.OrdinalIgnoreCase);

        var messages = root.Descendants().Where(IsMessage);
        if (messages.Skip(1).Any())
        {
            var type = messageName.ToUpperInvariant();
            throw parseException(
                $"The document holds {messages.Count()} {type} messages; this reader reads a single message. Split the NDM " +
                $"into one {type} message per document.");
        }

        return root.Name.LocalName.Equals("ndm", StringComparison.OrdinalIgnoreCase)
            ? root.Elements().FirstOrDefault(IsMessage)
            : messages.FirstOrDefault();
    }
}
