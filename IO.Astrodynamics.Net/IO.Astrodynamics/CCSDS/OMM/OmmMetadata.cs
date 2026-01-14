// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.CCSDS.Common.Enums;

namespace IO.Astrodynamics.CCSDS.OMM;

/// <summary>
/// Represents the metadata section of an OMM message.
/// </summary>
public class OmmMetadata
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
    /// Gets the reference frame for the orbital elements (e.g., "TEME").
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
    /// Gets the time system used (e.g., "UTC").
    /// </summary>
    public string TimeSystem { get; }

    /// <summary>
    /// Gets the time system as an enum value (if recognized).
    /// </summary>
    public CcsdsTimeSystem? TimeSystemEnum { get; }

    /// <summary>
    /// Gets the mean element theory used (e.g., "SGP4").
    /// </summary>
    public string MeanElementTheory { get; }

    /// <summary>
    /// Gets the mean element theory as an enum value (if recognized).
    /// </summary>
    public MeanElementTheory? MeanElementTheoryEnum { get; }

    /// <summary>
    /// Gets optional comments associated with the metadata.
    /// </summary>
    public IReadOnlyList<string> Comments { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmmMetadata"/> class.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="objectId">The object ID (international designator).</param>
    /// <param name="centerName">The orbit center name.</param>
    /// <param name="referenceFrame">The reference frame.</param>
    /// <param name="timeSystem">The time system.</param>
    /// <param name="meanElementTheory">The mean element theory.</param>
    /// <param name="referenceFrameEpoch">Optional reference frame epoch.</param>
    /// <param name="comments">Optional comments.</param>
    public OmmMetadata(
        string objectName,
        string objectId,
        string centerName,
        string referenceFrame,
        string timeSystem,
        string meanElementTheory,
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
        if (string.IsNullOrWhiteSpace(meanElementTheory))
            throw new ArgumentException("Mean element theory is required.", nameof(meanElementTheory));

        ObjectName = objectName;
        ObjectId = objectId;
        CenterName = centerName;
        ReferenceFrame = referenceFrame;
        ReferenceFrameEnum = TryParseReferenceFrame(referenceFrame);
        ReferenceFrameEpoch = referenceFrameEpoch;
        TimeSystem = timeSystem;
        TimeSystemEnum = TryParseTimeSystem(timeSystem);
        MeanElementTheory = meanElementTheory;
        MeanElementTheoryEnum = TryParseMeanElementTheory(meanElementTheory);
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmmMetadata"/> class using enum values.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="objectId">The object ID (international designator).</param>
    /// <param name="centerName">The orbit center name.</param>
    /// <param name="referenceFrame">The reference frame enum.</param>
    /// <param name="timeSystem">The time system enum.</param>
    /// <param name="meanElementTheory">The mean element theory enum.</param>
    /// <param name="referenceFrameEpoch">Optional reference frame epoch.</param>
    /// <param name="comments">Optional comments.</param>
    public OmmMetadata(
        string objectName,
        string objectId,
        string centerName,
        CcsdsReferenceFrame referenceFrame,
        CcsdsTimeSystem timeSystem,
        MeanElementTheory meanElementTheory,
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
        MeanElementTheory = GetEnumDescription(meanElementTheory);
        MeanElementTheoryEnum = meanElementTheory;
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Creates an OmmMetadata for a standard SGP4/TEME configuration.
    /// </summary>
    public static OmmMetadata CreateForSgp4(
        string objectName,
        string objectId,
        IReadOnlyList<string> comments = null)
    {
        return new OmmMetadata(
            objectName,
            objectId,
            "EARTH",
            CcsdsReferenceFrame.TEME,
            CcsdsTimeSystem.UTC,
            Common.Enums.MeanElementTheory.SGP4,
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

    private static MeanElementTheory? TryParseMeanElementTheory(string value)
    {
        foreach (MeanElementTheory theory in Enum.GetValues<MeanElementTheory>())
        {
            if (GetEnumDescription(theory).Equals(value, StringComparison.OrdinalIgnoreCase))
                return theory;
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
        return $"OmmMetadata[{ObjectName}, {ObjectId}, {ReferenceFrame}, {TimeSystem}, {MeanElementTheory}]";
    }
}
