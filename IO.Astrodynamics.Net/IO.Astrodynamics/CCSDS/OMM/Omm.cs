// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using IO.Astrodynamics.CCSDS.Common;

namespace IO.Astrodynamics.CCSDS.OMM;

/// <summary>
/// Represents a CCSDS Orbit Mean-elements Message (OMM).
/// </summary>
/// <remarks>
/// OMM is a CCSDS Navigation Data Message format for exchanging mean orbital elements.
/// The framework supports SGP4 mean element theory (which includes SDP4 for deep space).
/// See CCSDS 502.0-B-3 (ODM Blue Book) for the complete specification.
/// </remarks>
public class Omm
{
    private static readonly OmmReader DefaultReader = new();
    private static readonly OmmWriter DefaultWriter = new();
    private static readonly OmmValidator DefaultValidator = new();
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

    #region Factory Methods (Load)

    /// <summary>
    /// Loads an OMM from a file with optional validation.
    /// </summary>
    /// <param name="filePath">The path to the OMM XML file.</param>
    /// <param name="validateSchema">If true, validates the XML against the CCSDS OMM schema.</param>
    /// <param name="validateContent">If true, validates the content for physical constraints and consistency.</param>
    /// <returns>The loaded and validated OMM instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="OmmValidationException">Thrown when validation fails with errors.</exception>
    public static Omm LoadFromFile(string filePath, bool validateSchema = true, bool validateContent = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"OMM file not found: {filePath}", filePath);

        // Schema validation
        if (validateSchema)
        {
            var schemaResult = DefaultValidator.ValidateSchema(filePath);
            if (!schemaResult.IsValid)
            {
                throw new OmmValidationException("Schema validation failed.", schemaResult);
            }
        }

        // Parse the file
        var omm = DefaultReader.ReadFromFile(filePath);

        // Content validation
        if (validateContent)
        {
            var contentResult = DefaultValidator.Validate(omm);
            if (!contentResult.IsValid)
            {
                throw new OmmValidationException("Content validation failed.", contentResult);
            }
        }

        return omm;
    }

    /// <summary>
    /// Loads an OMM from an XML string with optional validation.
    /// </summary>
    /// <param name="xml">The OMM XML content.</param>
    /// <param name="validateSchema">If true, validates the XML against the CCSDS OMM schema.</param>
    /// <param name="validateContent">If true, validates the content for physical constraints and consistency.</param>
    /// <returns>The loaded and validated OMM instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when xml is null or empty.</exception>
    /// <exception cref="OmmValidationException">Thrown when validation fails with errors.</exception>
    public static Omm LoadFromString(string xml, bool validateSchema = true, bool validateContent = true)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentNullException(nameof(xml));

        // Schema validation
        if (validateSchema)
        {
            var schemaResult = DefaultValidator.ValidateSchemaFromXml(xml);
            if (!schemaResult.IsValid)
            {
                throw new OmmValidationException("Schema validation failed.", schemaResult);
            }
        }

        // Parse the string
        var omm = DefaultReader.ReadFromString(xml);

        // Content validation
        if (validateContent)
        {
            var contentResult = DefaultValidator.Validate(omm);
            if (!contentResult.IsValid)
            {
                throw new OmmValidationException("Content validation failed.", contentResult);
            }
        }

        return omm;
    }

    /// <summary>
    /// Loads an OMM from a stream with optional validation.
    /// </summary>
    /// <param name="stream">The stream containing OMM XML content.</param>
    /// <param name="validateSchema">If true, validates the XML against the CCSDS OMM schema.</param>
    /// <param name="validateContent">If true, validates the content for physical constraints and consistency.</param>
    /// <returns>The loaded and validated OMM instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    /// <exception cref="OmmValidationException">Thrown when validation fails with errors.</exception>
    public static Omm LoadFromStream(Stream stream, bool validateSchema = true, bool validateContent = true)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        // For stream validation, we need to read the content first
        // then validate and parse from the string
        using var reader = new StreamReader(stream, leaveOpen: true);
        var xml = reader.ReadToEnd();

        return LoadFromString(xml, validateSchema, validateContent);
    }

    /// <summary>
    /// Tries to load an OMM from a file, returning validation result instead of throwing.
    /// </summary>
    /// <param name="filePath">The path to the OMM XML file.</param>
    /// <param name="omm">The loaded OMM instance if successful, or null if validation failed.</param>
    /// <param name="validationResult">The validation result containing any errors or warnings.</param>
    /// <param name="validateSchema">If true, validates the XML against the CCSDS OMM schema.</param>
    /// <param name="validateContent">If true, validates the content for physical constraints and consistency.</param>
    /// <returns>True if the OMM was loaded successfully with no errors, false otherwise.</returns>
    public static bool TryLoadFromFile(
        string filePath,
        out Omm omm,
        out OmmValidationResult validationResult,
        bool validateSchema = true,
        bool validateContent = true)
    {
        omm = null;
        validationResult = new OmmValidationResult();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            validationResult.AddError(
                "FILE_NOT_FOUND",
                $"File not found: {filePath}",
                "FilePath");
            return false;
        }

        // Schema validation
        if (validateSchema)
        {
            var schemaResult = DefaultValidator.ValidateSchema(filePath);
            if (!schemaResult.IsValid)
            {
                validationResult = schemaResult;
                return false;
            }
        }

        // Parse the file
        try
        {
            omm = DefaultReader.ReadFromFile(filePath);
        }
        catch (Exception ex)
        {
            validationResult.AddError(
                "PARSE_ERROR",
                $"Failed to parse OMM: {ex.Message}",
                "File");
            return false;
        }

        // Content validation
        if (validateContent)
        {
            validationResult = DefaultValidator.Validate(omm);
            if (!validationResult.IsValid)
            {
                omm = null;
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Instance Methods (Save/Validate)

    /// <summary>
    /// Saves the OMM to a file with optional validation.
    /// </summary>
    /// <param name="filePath">The path to save the OMM XML file.</param>
    /// <param name="validateBeforeSave">If true, validates the OMM before saving.</param>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="OmmValidationException">Thrown when validation fails with errors.</exception>
    public void SaveToFile(string filePath, bool validateBeforeSave = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));

        if (validateBeforeSave)
        {
            var result = Validate();
            if (!result.IsValid)
            {
                throw new OmmValidationException("Validation failed before save.", result);
            }
        }

        DefaultWriter.WriteToFile(this, filePath);
    }

    /// <summary>
    /// Saves the OMM to a stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="validateBeforeSave">If true, validates the OMM before saving.</param>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    /// <exception cref="OmmValidationException">Thrown when validation fails with errors.</exception>
    public void SaveToStream(Stream stream, bool validateBeforeSave = true)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        if (validateBeforeSave)
        {
            var result = Validate();
            if (!result.IsValid)
            {
                throw new OmmValidationException("Validation failed before save.", result);
            }
        }

        DefaultWriter.WriteToStream(this, stream);
    }

    /// <summary>
    /// Converts the OMM to an XML string.
    /// </summary>
    /// <returns>The OMM as an XML string.</returns>
    public string ToXml()
    {
        return DefaultWriter.WriteToString(this);
    }

    /// <summary>
    /// Validates the OMM and returns the validation result.
    /// </summary>
    /// <returns>The validation result containing any errors or warnings.</returns>
    public OmmValidationResult Validate()
    {
        return DefaultValidator.Validate(this);
    }

    /// <summary>
    /// Validates the OMM and returns whether it is valid.
    /// </summary>
    /// <returns>True if the OMM has no validation errors, false otherwise.</returns>
    public bool IsValid()
    {
        return Validate().IsValid;
    }

    #endregion
}
