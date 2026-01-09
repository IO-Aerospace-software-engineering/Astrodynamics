// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.CCSDS.Common;

namespace IO.Astrodynamics.CCSDS.OMM;

/// <summary>
/// Represents a CCSDS Orbit Mean-elements Message (OMM).
/// </summary>
/// <remarks>
/// OMM is a CCSDS Navigation Data Message format for exchanging mean orbital elements.
/// It supports various mean element theories including SGP4, SGP4-XP, DSST, and USM.
/// See CCSDS 502.0-B-3 (ODM Blue Book) for the complete specification.
/// </remarks>
public class Omm
{
    /// <summary>
    /// The CCSDS OMM format version.
    /// </summary>
    public const string Version = "3.0";

    /// <summary>
    /// The CCSDS OMM format identifier.
    /// </summary>
    public const string FormatId = "CCSDS_OMM_VERS";

    /// <summary>
    /// Gets the message header.
    /// </summary>
    public CcsdsHeader Header { get; }

    /// <summary>
    /// Gets the message metadata.
    /// </summary>
    public OmmMetadata Metadata { get; }

    /// <summary>
    /// Gets the message data.
    /// </summary>
    public OmmData Data { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Omm"/> class.
    /// </summary>
    /// <param name="header">The message header.</param>
    /// <param name="metadata">The message metadata.</param>
    /// <param name="data">The message data.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public Omm(CcsdsHeader header, OmmMetadata metadata, OmmData data)
    {
        Header = header ?? throw new ArgumentNullException(nameof(header));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>
    /// Gets the object name from the metadata.
    /// </summary>
    public string ObjectName => Metadata.ObjectName;

    /// <summary>
    /// Gets the object ID (international designator) from the metadata.
    /// </summary>
    public string ObjectId => Metadata.ObjectId;

    /// <summary>
    /// Gets the epoch of the orbital elements.
    /// </summary>
    public DateTime Epoch => Data.MeanElements.Epoch;

    /// <summary>
    /// Gets a value indicating whether this OMM contains TLE-specific parameters.
    /// </summary>
    public bool IsTleCompatible => Data.HasTleParameters;

    /// <summary>
    /// Gets a value indicating whether this OMM contains a covariance matrix.
    /// </summary>
    public bool HasCovariance => Data.HasCovarianceMatrix;

    /// <summary>
    /// Creates an OMM message suitable for TLE/SGP4 propagation.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="objectId">The object ID (international designator).</param>
    /// <param name="epoch">The epoch of the elements.</param>
    /// <param name="meanMotion">Mean motion in rev/day.</param>
    /// <param name="eccentricity">Eccentricity.</param>
    /// <param name="inclination">Inclination in degrees.</param>
    /// <param name="raan">Right ascension of ascending node in degrees.</param>
    /// <param name="argOfPericenter">Argument of pericenter in degrees.</param>
    /// <param name="meanAnomaly">Mean anomaly in degrees.</param>
    /// <param name="bstar">BSTAR drag term in 1/Earth radii.</param>
    /// <param name="meanMotionDot">First derivative of mean motion in rev/day².</param>
    /// <param name="meanMotionDDot">Second derivative of mean motion in rev/day³.</param>
    /// <param name="noradCatalogId">Optional NORAD catalog ID.</param>
    /// <param name="elementSetNumber">Optional element set number.</param>
    /// <param name="revolutionNumber">Optional revolution number at epoch.</param>
    /// <param name="originator">The message originator.</param>
    /// <returns>A new OMM instance configured for TLE/SGP4.</returns>
    public static Omm CreateForTle(
        string objectName,
        string objectId,
        DateTime epoch,
        double meanMotion,
        double eccentricity,
        double inclination,
        double raan,
        double argOfPericenter,
        double meanAnomaly,
        double bstar,
        double meanMotionDot,
        double meanMotionDDot,
        int? noradCatalogId = null,
        int? elementSetNumber = null,
        int? revolutionNumber = null,
        string originator = null)
    {
        var header = originator != null
            ? CcsdsHeader.Create(originator)
            : CcsdsHeader.CreateDefault();

        var metadata = OmmMetadata.CreateForSgp4(objectName, objectId);

        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch, meanMotion, eccentricity, inclination, raan, argOfPericenter, meanAnomaly);

        var tleParams = TleParameters.CreateWithBStarAndDDot(
            bstar, meanMotionDot, meanMotionDDot,
            noradCatalogId: noradCatalogId,
            elementSetNumber: elementSetNumber,
            revolutionNumberAtEpoch: revolutionNumber,
            classificationType: "U");

        var data = OmmData.CreateForTle(meanElements, tleParams);

        return new Omm(header, metadata, data);
    }

    /// <summary>
    /// Creates a minimal OMM message with only the required fields.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="objectId">The object ID.</param>
    /// <param name="centerName">The orbit center name.</param>
    /// <param name="referenceFrame">The reference frame.</param>
    /// <param name="timeSystem">The time system.</param>
    /// <param name="meanElementTheory">The mean element theory.</param>
    /// <param name="meanElements">The mean orbital elements.</param>
    /// <param name="originator">The message originator.</param>
    /// <returns>A new minimal OMM instance.</returns>
    public static Omm CreateMinimal(
        string objectName,
        string objectId,
        string centerName,
        string referenceFrame,
        string timeSystem,
        string meanElementTheory,
        MeanElements meanElements,
        string originator = null)
    {
        var header = originator != null
            ? CcsdsHeader.Create(originator)
            : CcsdsHeader.CreateDefault();

        var metadata = new OmmMetadata(
            objectName, objectId, centerName, referenceFrame, timeSystem, meanElementTheory);

        var data = OmmData.CreateMinimal(meanElements);

        return new Omm(header, metadata, data);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var tleMarker = IsTleCompatible ? " (TLE)" : "";
        return $"OMM[{ObjectName}, {ObjectId}, {Epoch:O}{tleMarker}]";
    }
}
