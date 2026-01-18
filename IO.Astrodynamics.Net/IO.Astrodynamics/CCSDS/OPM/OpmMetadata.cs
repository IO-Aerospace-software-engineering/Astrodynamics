// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.CCSDS.Common.Enums;

namespace IO.Astrodynamics.CCSDS.OPM;

/// <summary>
/// Represents the metadata section of an OPM (Orbit Parameter Message).
/// </summary>
/// <remarks>
/// Unlike OMM metadata, OPM metadata does not include MEAN_ELEMENT_THEORY
/// because OPM represents osculating (instantaneous) orbital state vectors.
/// </remarks>
public class OpmMetadata
{
    /// <summary>
    /// Gets the object name (e.g., "ISS (ZARYA)").
    /// </summary>
    public string ObjectName { get; }

    /// <summary>
    /// Gets the object ID (e.g., international designator "1998-067A").
    /// </summary>
    public string ObjectId { get; }

    /// <summary>
    /// Gets the name of the orbit center (e.g., "EARTH").
    /// </summary>
    public string CenterName { get; }

    /// <summary>
    /// Gets the reference frame for the orbital elements (e.g., "ICRF", "EME2000").
    /// </summary>
    public string ReferenceFrame { get; }

    /// <summary>
    /// Gets the reference frame as an enum value (if recognized).
    /// </summary>
    public CcsdsReferenceFrame? ReferenceFrameEnum { get; }

    /// <summary>
    /// Gets the reference frame epoch (optional, for non-standard epochs).
    /// </summary>
    public DateTime? ReferenceFrameEpoch { get; }

    /// <summary>
    /// Gets the time system used (e.g., "UTC", "TDB").
    /// </summary>
    public string TimeSystem { get; }

    /// <summary>
    /// Gets the time system as an enum value (if recognized).
    /// </summary>
    public CcsdsTimeSystem? TimeSystemEnum { get; }

    /// <summary>
    /// Gets optional comments associated with the metadata.
    /// </summary>
    public IReadOnlyList<string> Comments { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpmMetadata"/> class.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="objectId">The object ID (international designator).</param>
    /// <param name="centerName">The orbit center name.</param>
    /// <param name="referenceFrame">The reference frame.</param>
    /// <param name="timeSystem">The time system.</param>
    /// <param name="referenceFrameEpoch">Optional reference frame epoch.</param>
    /// <param name="comments">Optional comments.</param>
    public OpmMetadata(
        string objectName,
        string objectId,
        string centerName,
        string referenceFrame,
        string timeSystem,
        DateTime? referenceFrameEpoch = null,
        IReadOnlyList<string> comments = null)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            throw new ArgumentException("Object name is required.", nameof(objectName));
        if (string.IsNullOrWhiteSpace(objectId))
            throw new ArgumentException("Object ID is required.", nameof(objectId));
        if (string.IsNullOrWhiteSpace(centerName))
            throw new ArgumentException("Center name is required.", nameof(centerName));
        if (string.IsNullOrWhiteSpace(referenceFrame))
            throw new ArgumentException("Reference frame is required.", nameof(referenceFrame));
        if (string.IsNullOrWhiteSpace(timeSystem))
            throw new ArgumentException("Time system is required.", nameof(timeSystem));

        ObjectName = objectName;
        ObjectId = objectId;
        CenterName = centerName;
        ReferenceFrame = referenceFrame;
        ReferenceFrameEnum = TryParseReferenceFrame(referenceFrame);
        ReferenceFrameEpoch = referenceFrameEpoch;
        TimeSystem = timeSystem;
        TimeSystemEnum = TryParseTimeSystem(timeSystem);
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpmMetadata"/> class using enum values.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="objectId">The object ID (international designator).</param>
    /// <param name="centerName">The orbit center name.</param>
    /// <param name="referenceFrame">The reference frame enum.</param>
    /// <param name="timeSystem">The time system enum.</param>
    /// <param name="referenceFrameEpoch">Optional reference frame epoch.</param>
    /// <param name="comments">Optional comments.</param>
    public OpmMetadata(
        string objectName,
        string objectId,
        string centerName,
        CcsdsReferenceFrame referenceFrame,
        CcsdsTimeSystem timeSystem,
        DateTime? referenceFrameEpoch = null,
        IReadOnlyList<string> comments = null)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            throw new ArgumentException("Object name is required.", nameof(objectName));
        if (string.IsNullOrWhiteSpace(objectId))
            throw new ArgumentException("Object ID is required.", nameof(objectId));
        if (string.IsNullOrWhiteSpace(centerName))
            throw new ArgumentException("Center name is required.", nameof(centerName));

        ObjectName = objectName;
        ObjectId = objectId;
        CenterName = centerName;
        ReferenceFrame = GetEnumDescription(referenceFrame);
        ReferenceFrameEnum = referenceFrame;
        ReferenceFrameEpoch = referenceFrameEpoch;
        TimeSystem = GetEnumDescription(timeSystem);
        TimeSystemEnum = timeSystem;
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Creates an OpmMetadata for a standard ICRF/UTC configuration.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="objectId">The object ID (international designator).</param>
    /// <param name="comments">Optional comments.</param>
    /// <returns>A new OpmMetadata instance configured for Earth-centered ICRF/UTC.</returns>
    public static OpmMetadata CreateDefault(
        string objectName,
        string objectId,
        IReadOnlyList<string> comments = null)
    {
        return new OpmMetadata(
            objectName,
            objectId,
            "EARTH",
            CcsdsReferenceFrame.ICRF,
            CcsdsTimeSystem.UTC,
            comments: comments);
    }

    /// <summary>
    /// Creates an OpmMetadata for EME2000/UTC configuration (common for Earth orbits).
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="objectId">The object ID (international designator).</param>
    /// <param name="comments">Optional comments.</param>
    /// <returns>A new OpmMetadata instance configured for Earth-centered EME2000/UTC.</returns>
    public static OpmMetadata CreateForEme2000(
        string objectName,
        string objectId,
        IReadOnlyList<string> comments = null)
    {
        return new OpmMetadata(
            objectName,
            objectId,
            "EARTH",
            CcsdsReferenceFrame.EME2000,
            CcsdsTimeSystem.UTC,
            comments: comments);
    }

    private static CcsdsReferenceFrame? TryParseReferenceFrame(string value)
    {
        foreach (CcsdsReferenceFrame frame in Enum.GetValues<CcsdsReferenceFrame>())
        {
            if (GetEnumDescription(frame).Equals(value, StringComparison.OrdinalIgnoreCase))
                return frame;
        }
        return null;
    }

    private static CcsdsTimeSystem? TryParseTimeSystem(string value)
    {
        foreach (CcsdsTimeSystem ts in Enum.GetValues<CcsdsTimeSystem>())
        {
            if (GetEnumDescription(ts).Equals(value, StringComparison.OrdinalIgnoreCase))
                return ts;
        }
        return null;
    }

    private static string GetEnumDescription<T>(T value) where T : Enum
    {
        var field = value.GetType().GetField(value.ToString());
        var attribute = field?.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            as System.ComponentModel.DescriptionAttribute[];
        return attribute?.Length > 0 ? attribute[0].Description : value.ToString();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"OpmMetadata[{ObjectName}, {ObjectId}, {ReferenceFrame}, {TimeSystem}]";
    }
}
