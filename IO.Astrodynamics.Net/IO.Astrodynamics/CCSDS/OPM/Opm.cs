// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.CCSDS.OPM;

/// <summary>
/// Represents a CCSDS Orbit Parameter Message (OPM).
/// </summary>
/// <remarks>
/// OPM is a CCSDS Navigation Data Message format for exchanging osculating (instantaneous) orbital state vectors.
/// Unlike OMM which uses mean elements, OPM contains actual position and velocity at a specific epoch.
/// See CCSDS 502.0-B-3 (ODM Blue Book) for the complete specification.
/// </remarks>
public class Opm
{
    private static readonly OpmReader DefaultReader = new();
    private static readonly OpmValidator DefaultValidator = new();

    /// <summary>
    /// The CCSDS OPM format version.
    /// </summary>
    public const string Version = "3.0";

    /// <summary>
    /// The CCSDS OPM format identifier.
    /// </summary>
    public const string FormatId = "CCSDS_OPM_VERS";

    /// <summary>
    /// Gets the message header.
    /// </summary>
    public CcsdsHeader Header { get; }

    /// <summary>
    /// Gets the message metadata.
    /// </summary>
    public OpmMetadata Metadata { get; }

    /// <summary>
    /// Gets the message data.
    /// </summary>
    public OpmData Data { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Opm"/> class.
    /// </summary>
    /// <param name="header">The message header.</param>
    /// <param name="metadata">The message metadata.</param>
    /// <param name="data">The message data.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public Opm(CcsdsHeader header, OpmMetadata metadata, OpmData data)
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
    /// Gets the epoch of the state vector.
    /// </summary>
    public DateTime Epoch => Data.StateVector.Epoch;

    /// <summary>
    /// Gets a value indicating whether this OPM contains Keplerian elements.
    /// </summary>
    public bool HasKeplerianElements => Data.HasKeplerianElements;

    /// <summary>
    /// Gets a value indicating whether this OPM contains spacecraft parameters.
    /// </summary>
    public bool HasSpacecraftParameters => Data.HasSpacecraftParameters;

    /// <summary>
    /// Gets a value indicating whether this OPM contains a covariance matrix.
    /// </summary>
    public bool HasCovariance => Data.HasCovariance;

    /// <summary>
    /// Gets a value indicating whether this OPM contains maneuver parameters.
    /// </summary>
    public bool HasManeuvers => Data.HasManeuvers;

    /// <summary>
    /// Creates an OPM from a state vector.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="objectId">The object ID (international designator).</param>
    /// <param name="stateVector">The state vector.</param>
    /// <param name="originator">The message originator.</param>
    /// <returns>A new OPM instance.</returns>
    public static Opm CreateFromStateVector(
        string objectName,
        string objectId,
        StateVector stateVector,
        string originator = null)
    {
        if (stateVector == null)
            throw new ArgumentNullException(nameof(stateVector));

        var header = originator != null
            ? CcsdsHeader.Create(originator)
            : CcsdsHeader.CreateDefault();

        var metadata = new OpmMetadata(
            objectName,
            objectId,
            stateVector.Observer.Name,
            stateVector.Frame.Name,
            "UTC");

        var opmStateVector = OpmStateVector.FromStateVector(stateVector);

        // Include covariance if present (convert from framework units m² to CCSDS units km²)
        CovarianceMatrix covariance = null;
        if (stateVector.Covariance.HasValue)
        {
            covariance = CovarianceMatrix.FromMatrixWithUnitConversion(stateVector.Covariance.Value);
        }

        var data = new OpmData(opmStateVector, covariance: covariance);

        return new Opm(header, metadata, data);
    }

    /// <summary>
    /// Creates a minimal OPM with only the required fields.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="objectId">The object ID.</param>
    /// <param name="centerName">The orbit center name.</param>
    /// <param name="referenceFrame">The reference frame.</param>
    /// <param name="timeSystem">The time system.</param>
    /// <param name="stateVector">The state vector.</param>
    /// <param name="originator">The message originator.</param>
    /// <returns>A new minimal OPM instance.</returns>
    public static Opm CreateMinimal(
        string objectName,
        string objectId,
        string centerName,
        string referenceFrame,
        string timeSystem,
        OpmStateVector stateVector,
        string originator = null)
    {
        var header = originator != null
            ? CcsdsHeader.Create(originator)
            : CcsdsHeader.CreateDefault();

        var metadata = new OpmMetadata(
            objectName, objectId, centerName, referenceFrame, timeSystem);

        var data = new OpmData(stateVector);

        return new Opm(header, metadata, data);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"OPM[{ObjectName}, {ObjectId}, {Epoch:O}]";
    }

    #region Framework Integration

    /// <summary>
    /// Converts this OPM to a framework StateVector.
    /// </summary>
    /// <param name="observer">The central body (observer). If null, Earth is used.</param>
    /// <returns>A new StateVector instance with the OPM data.</returns>
    /// <remarks>
    /// <para>
    /// If the OPM contains a covariance matrix with a reference frame that differs from the state vector
    /// reference frame (e.g., covariance in RTN while state vector in ICRF), the covariance is included
    /// as-is without frame transformation. The caller is responsible for frame transformation if needed.
    /// </para>
    /// <para>
    /// Common covariance frame combinations:
    /// <list type="bullet">
    /// <item>State in ICRF, Covariance in ICRF: No transformation needed</item>
    /// <item>State in ICRF, Covariance in RTN/RSW: Requires transformation for proper uncertainty propagation</item>
    /// </list>
    /// </para>
    /// </remarks>
    public StateVector ToStateVector(ILocalizable observer = null)
    {
        // Default to Earth if no observer specified
        if (observer == null)
        {
            var epoch = new Time(Data.StateVector.Epoch, TimeFrame.UTCFrame);
            observer = new CelestialBody(399, Frame.ICRF, epoch);
        }

        // Get the reference frame from metadata
        var frame = GetFrame();

        // Convert covariance from CCSDS units (km²) to framework units (m²)
        // Note: If covariance is in a different frame than the state vector, the caller
        // must handle frame transformation. Check CovarianceReferenceFrameConsistent property.
        Matrix? covariance = Data.Covariance?.ToMatrixWithUnitConversion();

        return Data.StateVector.ToStateVector(observer, frame, covariance);
    }

    /// <summary>
    /// Gets a value indicating whether the covariance reference frame is consistent with the state vector frame.
    /// </summary>
    /// <remarks>
    /// Returns true if no covariance is present, or if the covariance reference frame matches
    /// the metadata reference frame. Returns false if the frames differ, which may require
    /// frame transformation for proper uncertainty propagation.
    /// </remarks>
    public bool CovarianceReferenceFrameConsistent
    {
        get
        {
            if (!HasCovariance || string.IsNullOrEmpty(Data.Covariance.ReferenceFrame))
                return true;

            // Compare covariance frame with metadata frame (case-insensitive)
            return string.Equals(Data.Covariance.ReferenceFrame, Metadata.ReferenceFrame,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Converts this OPM to a framework Spacecraft.
    /// </summary>
    /// <param name="naifId">The NAIF identifier for the spacecraft (must be negative).</param>
    /// <param name="maximumOperatingMass">The maximum operating mass in kilograms.</param>
    /// <param name="clock">The spacecraft clock.</param>
    /// <param name="observer">The central body (observer). If null, Earth is used.</param>
    /// <returns>A new Spacecraft instance with the OPM data.</returns>
    /// <remarks>
    /// OPM provides: OBJECT_NAME (name), OBJECT_ID (cosparId), stateVector, and optionally spacecraftParameters.
    /// The caller must provide naifId, maximumOperatingMass, and clock as these are not part of the CCSDS OPM standard.
    /// If spacecraftParameters is not present in the OPM, default values are used for mass (1.0 kg),
    /// sectionalArea (1.0 m²), dragCoeff (0.3), and solarRadiationCoeff (1.0).
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when clock is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when naifId is non-negative or maximumOperatingMass is invalid.</exception>
    public Spacecraft ToSpacecraft(int naifId, double maximumOperatingMass, Clock clock, ILocalizable observer = null)
    {
        if (clock == null)
            throw new ArgumentNullException(nameof(clock));

        // Get state vector
        var stateVector = ToStateVector(observer);

        // Extract spacecraft parameters from OPM (with defaults if not present)
        double mass = Data.Mass ?? 1.0;
        double sectionalArea = Data.DragArea ?? 1.0;
        double dragCoeff = Data.DragCoefficient ?? 0.3;
        double solarRadCoeff = Data.SolarRadiationCoefficient ?? 1.0;

        // Ensure maximumOperatingMass is at least equal to mass
        if (maximumOperatingMass < mass)
            maximumOperatingMass = mass;

        return new Spacecraft(
            naifId,
            ObjectName,
            mass,
            maximumOperatingMass,
            clock,
            stateVector,
            sectionalArea,
            dragCoeff,
            ObjectId,  // COSPAR ID from OPM OBJECT_ID
            solarRadCoeff);
    }

    /// <summary>
    /// Gets the reference frame from metadata.
    /// </summary>
    /// <returns>The Frame object corresponding to the metadata reference frame.</returns>
    private Frame GetFrame()
    {
        if (Metadata.ReferenceFrameEnum.HasValue)
        {
            return Metadata.ReferenceFrameEnum.Value switch
            {
                Common.Enums.CcsdsReferenceFrame.ICRF => Frame.ICRF,
                Common.Enums.CcsdsReferenceFrame.EME2000 => Frame.ICRF,
                Common.Enums.CcsdsReferenceFrame.GCRF => Frame.ICRF,
                Common.Enums.CcsdsReferenceFrame.TEME => Frame.TEME,
                Common.Enums.CcsdsReferenceFrame.ITRF93 => new Frame("ITRF93"),
                _ => new Frame(Metadata.ReferenceFrame)
            };
        }

        return new Frame(Metadata.ReferenceFrame);
    }

    #endregion

    #region Factory Methods (Load)

    /// <summary>
    /// Loads an OPM from a file with optional validation.
    /// </summary>
    /// <param name="filePath">The path to the OPM XML file.</param>
    /// <param name="validateSchema">If true, validates the XML against the CCSDS OPM schema.</param>
    /// <param name="validateContent">If true, validates the content for physical constraints and consistency.</param>
    /// <returns>The loaded and validated OPM instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="OpmValidationException">Thrown when validation fails with errors.</exception>
    public static Opm LoadFromFile(string filePath, bool validateSchema = true, bool validateContent = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"OPM file not found: {filePath}", filePath);

        // Schema validation
        if (validateSchema)
        {
            var schemaResult = DefaultValidator.ValidateSchema(filePath);
            if (!schemaResult.IsValid)
            {
                throw new OpmValidationException("Schema validation failed.", schemaResult);
            }
        }

        // Parse the file
        var opm = DefaultReader.ReadFromFile(filePath);

        // Content validation
        if (validateContent)
        {
            var contentResult = DefaultValidator.Validate(opm);
            if (!contentResult.IsValid)
            {
                throw new OpmValidationException("Content validation failed.", contentResult);
            }
        }

        return opm;
    }

    /// <summary>
    /// Loads an OPM from an XML string with optional validation.
    /// </summary>
    /// <param name="xml">The OPM XML content.</param>
    /// <param name="validateSchema">If true, validates the XML against the CCSDS OPM schema.</param>
    /// <param name="validateContent">If true, validates the content for physical constraints and consistency.</param>
    /// <returns>The loaded and validated OPM instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when xml is null or empty.</exception>
    /// <exception cref="OpmValidationException">Thrown when validation fails with errors.</exception>
    public static Opm LoadFromString(string xml, bool validateSchema = true, bool validateContent = true)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentNullException(nameof(xml));

        // Schema validation
        if (validateSchema)
        {
            var schemaResult = DefaultValidator.ValidateSchemaFromXml(xml);
            if (!schemaResult.IsValid)
            {
                throw new OpmValidationException("Schema validation failed.", schemaResult);
            }
        }

        // Parse the string
        var opm = DefaultReader.ReadFromString(xml);

        // Content validation
        if (validateContent)
        {
            var contentResult = DefaultValidator.Validate(opm);
            if (!contentResult.IsValid)
            {
                throw new OpmValidationException("Content validation failed.", contentResult);
            }
        }

        return opm;
    }

    /// <summary>
    /// Loads an OPM from a stream with optional validation.
    /// </summary>
    /// <param name="stream">The stream containing OPM XML content.</param>
    /// <param name="validateSchema">If true, validates the XML against the CCSDS OPM schema.</param>
    /// <param name="validateContent">If true, validates the content for physical constraints and consistency.</param>
    /// <returns>The loaded and validated OPM instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    /// <exception cref="OpmValidationException">Thrown when validation fails with errors.</exception>
    public static Opm LoadFromStream(Stream stream, bool validateSchema = true, bool validateContent = true)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        using var reader = new StreamReader(stream, leaveOpen: true);
        var xml = reader.ReadToEnd();

        return LoadFromString(xml, validateSchema, validateContent);
    }

    /// <summary>
    /// Tries to load an OPM from a file, returning validation result instead of throwing.
    /// </summary>
    /// <param name="filePath">The path to the OPM XML file.</param>
    /// <param name="opm">The loaded OPM instance if successful, or null if validation failed.</param>
    /// <param name="validationResult">The validation result containing any errors or warnings.</param>
    /// <param name="validateSchema">If true, validates the XML against the CCSDS OPM schema.</param>
    /// <param name="validateContent">If true, validates the content for physical constraints and consistency.</param>
    /// <returns>True if the OPM was loaded successfully with no errors, false otherwise.</returns>
    public static bool TryLoadFromFile(
        string filePath,
        out Opm opm,
        out OpmValidationResult validationResult,
        bool validateSchema = true,
        bool validateContent = true)
    {
        opm = null;
        validationResult = new OpmValidationResult();

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
            opm = DefaultReader.ReadFromFile(filePath);
        }
        catch (Exception ex)
        {
            validationResult.AddError(
                "PARSE_ERROR",
                $"Failed to parse OPM: {ex.Message}",
                "File");
            return false;
        }

        // Content validation
        if (validateContent)
        {
            validationResult = DefaultValidator.Validate(opm);
            if (!validationResult.IsValid)
            {
                opm = null;
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Instance Methods (Save/Validate)

    /// <summary>
    /// Saves the OPM to a file with optional validation.
    /// </summary>
    /// <param name="filePath">The file path to save to.</param>
    /// <param name="validateBeforeSave">If true, validates the OPM before saving.</param>
    /// <param name="wrapInNdm">If true, wraps the OPM in an NDM container element.</param>
    /// <param name="indent">If true, indents the XML output for readability.</param>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="OpmValidationException">Thrown when validation fails and validateBeforeSave is true.</exception>
    public void SaveToFile(string filePath, bool validateBeforeSave = true, bool wrapInNdm = true, bool indent = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));

        if (validateBeforeSave)
        {
            var result = Validate();
            if (!result.IsValid)
            {
                throw new OpmValidationException("Validation failed before save.", result);
            }
        }

        var writer = new OpmWriter
        {
            WrapInNdmContainer = wrapInNdm,
            IndentOutput = indent
        };

        writer.WriteToFile(this, filePath);
    }

    /// <summary>
    /// Saves the OPM to a stream with optional validation.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="validateBeforeSave">If true, validates the OPM before saving.</param>
    /// <param name="wrapInNdm">If true, wraps the OPM in an NDM container element.</param>
    /// <param name="indent">If true, indents the XML output for readability.</param>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    /// <exception cref="OpmValidationException">Thrown when validation fails and validateBeforeSave is true.</exception>
    public void SaveToStream(Stream stream, bool validateBeforeSave = true, bool wrapInNdm = true, bool indent = true)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        if (validateBeforeSave)
        {
            var result = Validate();
            if (!result.IsValid)
            {
                throw new OpmValidationException("Validation failed before save.", result);
            }
        }

        var writer = new OpmWriter
        {
            WrapInNdmContainer = wrapInNdm,
            IndentOutput = indent
        };

        writer.WriteToStream(this, stream);
    }

    /// <summary>
    /// Converts the OPM to an XML string.
    /// </summary>
    /// <param name="wrapInNdm">If true, wraps the OPM in an NDM container element.</param>
    /// <param name="indent">If true, indents the XML output for readability.</param>
    /// <returns>The OPM as an XML string.</returns>
    public string ToXmlString(bool wrapInNdm = true, bool indent = true)
    {
        var writer = new OpmWriter
        {
            WrapInNdmContainer = wrapInNdm,
            IndentOutput = indent
        };

        return writer.WriteToString(this);
    }

    /// <summary>
    /// Validates this OPM instance.
    /// </summary>
    /// <returns>The validation result containing any errors or warnings.</returns>
    public OpmValidationResult Validate()
    {
        return DefaultValidator.Validate(this);
    }

    #endregion
}
