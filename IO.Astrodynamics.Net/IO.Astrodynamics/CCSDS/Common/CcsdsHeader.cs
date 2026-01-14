// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;

namespace IO.Astrodynamics.CCSDS.Common;

/// <summary>
/// Represents the header section of a CCSDS Navigation Data Message.
/// </summary>
/// <remarks>
/// As defined in CCSDS 505.0-B-3 (NDM/XML Blue Book).
/// The header contains metadata about the message itself.
/// </remarks>
public class CcsdsHeader
{
    /// <summary>
    /// Gets the comments associated with the header.
    /// </summary>
    public IReadOnlyList<string> Comments { get; }

    /// <summary>
    /// Gets or sets the security classification of the message.
    /// </summary>
    /// <remarks>
    /// Optional field. Examples: "UNCLASSIFIED", "RESTRICTED", etc.
    /// </remarks>
    public string Classification { get; init; }

    /// <summary>
    /// Gets the creation date and time of the message.
    /// </summary>
    /// <remarks>
    /// Required field. Format: yyyy-MM-ddTHH:mm:ss.fff or yyyy-dddTHH:mm:ss.fff
    /// </remarks>
    public DateTime CreationDate { get; }

    /// <summary>
    /// Gets the originator (creator) of the message.
    /// </summary>
    /// <remarks>
    /// Required field. Typically an organization name or identifier.
    /// </remarks>
    public string Originator { get; }

    /// <summary>
    /// Gets or sets the unique message identifier.
    /// </summary>
    /// <remarks>
    /// Optional field. Used for tracking and referencing messages.
    /// </remarks>
    public string MessageId { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CcsdsHeader"/> class with required fields.
    /// </summary>
    /// <param name="creationDate">The creation date and time of the message.</param>
    /// <param name="originator">The originator (creator) of the message.</param>
    /// <exception cref="ArgumentNullException">Thrown when originator is null.</exception>
    /// <exception cref="ArgumentException">Thrown when originator is empty or whitespace.</exception>
    public CcsdsHeader(DateTime creationDate, string originator)
        : this(creationDate, originator, Array.Empty<string>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CcsdsHeader"/> class with all fields.
    /// </summary>
    /// <param name="creationDate">The creation date and time of the message.</param>
    /// <param name="originator">The originator (creator) of the message.</param>
    /// <param name="comments">Comments associated with the header.</param>
    /// <exception cref="ArgumentNullException">Thrown when originator or comments is null.</exception>
    /// <exception cref="ArgumentException">Thrown when originator is empty or whitespace.</exception>
    public CcsdsHeader(DateTime creationDate, string originator, IReadOnlyList<string> comments)
    {
        if (originator == null)
            throw new ArgumentNullException(nameof(originator));
        if (string.IsNullOrWhiteSpace(originator))
            throw new ArgumentException("Originator cannot be empty or whitespace.", nameof(originator));

        CreationDate = creationDate;
        Originator = originator;
        Comments = comments ?? throw new ArgumentNullException(nameof(comments));
    }

    /// <summary>
    /// Creates a new header with the current date/time and IO.Astrodynamics as originator.
    /// </summary>
    /// <returns>A new CcsdsHeader instance.</returns>
    public static CcsdsHeader CreateDefault()
    {
        return new CcsdsHeader(DateTime.UtcNow, "IO.Astrodynamics");
    }

    /// <summary>
    /// Creates a new header with the specified originator and current date/time.
    /// </summary>
    /// <param name="originator">The originator (creator) of the message.</param>
    /// <returns>A new CcsdsHeader instance.</returns>
    public static CcsdsHeader Create(string originator)
    {
        return new CcsdsHeader(DateTime.UtcNow, originator);
    }

    public override string ToString()
    {
        return $"Header[CreationDate={CreationDate:O}, Originator={Originator}]";
    }
}
