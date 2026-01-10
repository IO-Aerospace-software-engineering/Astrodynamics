// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.Common.Enums;

namespace IO.Astrodynamics.CCSDS.OMM;

/// <summary>
/// Validates OMM documents against CCSDS standards and physical constraints.
/// </summary>
public class OmmValidator
{
    // Error codes
    private const string RequiredField = "OMM001";
    private const string InvalidRange = "OMM002";
    private const string InvalidValue = "OMM003";
    private const string InconsistentData = "OMM004";
    private const string TleValidation = "OMM005";
    private const string PhysicalConstraint = "OMM006";
    private const string SchemaValidation = "OMM007";

    // Schema resource names
    private const string SchemaResourcePrefix = "IO.Astrodynamics.CCSDS.Schema.";
    private const string CcsdsNamespace = "urn:ccsds:schema:ndmxml";

    // Cached schema set
    private static XmlSchemaSet _schemaSet;
    private static readonly object _schemaLock = new();

    /// <summary>
    /// Gets or sets whether to validate physical constraints (e.g., orbital parameter ranges).
    /// Default is true.
    /// </summary>
    public bool ValidatePhysicalConstraints { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate TLE-specific requirements when TLE parameters are present.
    /// Default is true.
    /// </summary>
    public bool ValidateTleRequirements { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to add warnings for recommended but optional fields.
    /// Default is true.
    /// </summary>
    public bool WarnOnMissingOptionalFields { get; set; } = true;

    /// <summary>
    /// Validates an OMM XML file against the CCSDS XSD schema.
    /// </summary>
    /// <param name="filePath">Path to the OMM XML file.</param>
    /// <returns>Validation result containing any schema errors.</returns>
    public OmmValidationResult ValidateSchema(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            return OmmValidationResult.Failure(SchemaValidation, $"File not found: {filePath}", "File");
        }

        using var stream = File.OpenRead(filePath);
        return ValidateSchema(stream);
    }

    /// <summary>
    /// Validates OMM XML content against the CCSDS XSD schema.
    /// </summary>
    /// <param name="xmlContent">The OMM XML content as a string.</param>
    /// <returns>Validation result containing any schema errors.</returns>
    public OmmValidationResult ValidateSchemaFromXml(string xmlContent)
    {
        ArgumentNullException.ThrowIfNull(xmlContent);

        using var reader = new StringReader(xmlContent);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xmlContent));
        return ValidateSchema(stream);
    }

    /// <summary>
    /// Validates OMM XML content from a stream against the CCSDS XSD schema.
    /// </summary>
    /// <param name="xmlStream">Stream containing the OMM XML content.</param>
    /// <returns>Validation result containing any schema errors.</returns>
    public OmmValidationResult ValidateSchema(Stream xmlStream)
    {
        ArgumentNullException.ThrowIfNull(xmlStream);

        var result = new OmmValidationResult();
        var errors = new List<string>();

        try
        {
            var schemaSet = GetSchemaSet();
            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = schemaSet,
                ValidationFlags = XmlSchemaValidationFlags.ProcessIdentityConstraints |
                                  XmlSchemaValidationFlags.ReportValidationWarnings
            };

            settings.ValidationEventHandler += (sender, e) =>
            {
                var severity = e.Severity == XmlSeverityType.Error
                    ? OmmValidationSeverity.Error
                    : OmmValidationSeverity.Warning;

                var path = e.Exception?.SourceUri ?? "XML";
                if (e.Exception?.LineNumber > 0)
                {
                    path = $"Line {e.Exception.LineNumber}, Position {e.Exception.LinePosition}";
                }

                result.AddIssue(new OmmValidationError(severity, SchemaValidation, e.Message, path));
            };

            using var reader = XmlReader.Create(xmlStream, settings);
            while (reader.Read()) { }
        }
        catch (XmlSchemaException ex)
        {
            result.AddError(SchemaValidation, $"Schema error: {ex.Message}", $"Line {ex.LineNumber}");
        }
        catch (XmlException ex)
        {
            result.AddError(SchemaValidation, $"XML parsing error: {ex.Message}", $"Line {ex.LineNumber}");
        }
        catch (Exception ex)
        {
            result.AddError(SchemaValidation, $"Validation error: {ex.Message}", "Schema");
        }

        return result;
    }

    /// <summary>
    /// Validates an OMM file completely: first against XSD schema, then against physical constraints.
    /// </summary>
    /// <param name="filePath">Path to the OMM XML file.</param>
    /// <returns>Validation result containing all errors and warnings.</returns>
    public OmmValidationResult ValidateFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        // First validate against XSD schema
        var result = ValidateSchema(filePath);

        // If schema validation passed, also validate the parsed content
        if (result.IsValid)
        {
            try
            {
                var omm = new OmmReader().ReadFromFile(filePath);
                var contentResult = Validate(omm);

                // Merge the results
                foreach (var issue in contentResult.Issues)
                {
                    result.AddIssue(issue);
                }
            }
            catch (Exception ex)
            {
                result.AddError(SchemaValidation, $"Failed to parse OMM: {ex.Message}", "Parser");
            }
        }

        return result;
    }

    /// <summary>
    /// Gets or lazily initializes the XSD schema set for OMM validation.
    /// </summary>
    private static XmlSchemaSet GetSchemaSet()
    {
        if (_schemaSet != null)
            return _schemaSet;

        lock (_schemaLock)
        {
            if (_schemaSet != null)
                return _schemaSet;

            var schemaSet = new XmlSchemaSet();
            var assembly = Assembly.GetExecutingAssembly();

            // Load all schema files in the correct order
            // 1. Common schema (base types)
            // 2. OMM schema (OMM-specific types)
            // 3. Master schema (root element declarations)
            var schemaFiles = new[]
            {
                "ndmxml-4.0.0-common-4.0.xsd",
                "ndmxml-4.0.0-omm-3.0.xsd"
            };

            foreach (var schemaFile in schemaFiles)
            {
                var resourceName = SchemaResourcePrefix + schemaFile;
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    throw new InvalidOperationException($"Embedded resource not found: {resourceName}");

                using var reader = XmlReader.Create(stream);
                schemaSet.Add(CcsdsNamespace, reader);
            }

            // Add the root element declaration manually since it's in the master schema
            // which uses a different namespace approach
            var rootElementSchema = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<xsd:schema xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
            xmlns:ndm=""urn:ccsds:schema:ndmxml""
            targetNamespace=""urn:ccsds:schema:ndmxml""
            elementFormDefault=""qualified"">
    <xsd:element name=""omm"" type=""ndm:ommType""/>
</xsd:schema>";

            using (var reader = XmlReader.Create(new StringReader(rootElementSchema)))
            {
                schemaSet.Add(CcsdsNamespace, reader);
            }

            schemaSet.Compile();
            _schemaSet = schemaSet;
            return _schemaSet;
        }
    }

    /// <summary>
    /// Validates an OMM document.
    /// </summary>
    /// <param name="omm">The OMM document to validate.</param>
    /// <returns>Validation result containing any errors or warnings.</returns>
    public OmmValidationResult Validate(Omm omm)
    {
        ArgumentNullException.ThrowIfNull(omm);

        var result = new OmmValidationResult();

        ValidateHeader(omm.Header, result);
        ValidateMetadata(omm.Metadata, result);
        ValidateData(omm.Data, result);

        if (ValidateTleRequirements && omm.Data.HasTleParameters)
        {
            ValidateTleParameters(omm.Data.TleParameters, omm.Metadata, result);
        }

        return result;
    }

    private void ValidateHeader(CcsdsHeader header, OmmValidationResult result)
    {
        // Header is optional but if present, check recommended fields
        if (WarnOnMissingOptionalFields)
        {
            if (string.IsNullOrWhiteSpace(header.Originator))
            {
                result.AddWarning(RequiredField, "ORIGINATOR is recommended for traceability.", "Header.Originator");
            }

            if (header.CreationDate == default)
            {
                result.AddWarning(RequiredField, "CREATION_DATE is recommended for traceability.", "Header.CreationDate");
            }
        }
    }

    private void ValidateMetadata(OmmMetadata metadata, OmmValidationResult result)
    {
        // Required fields
        if (string.IsNullOrWhiteSpace(metadata.ObjectName))
        {
            result.AddError(RequiredField, "OBJECT_NAME is required.", "Metadata.ObjectName");
        }

        if (string.IsNullOrWhiteSpace(metadata.ObjectId))
        {
            result.AddError(RequiredField, "OBJECT_ID is required.", "Metadata.ObjectId");
        }

        if (string.IsNullOrWhiteSpace(metadata.CenterName))
        {
            result.AddError(RequiredField, "CENTER_NAME is required.", "Metadata.CenterName");
        }

        if (string.IsNullOrWhiteSpace(metadata.ReferenceFrame))
        {
            result.AddError(RequiredField, "REF_FRAME is required.", "Metadata.ReferenceFrame");
        }
        else if (!metadata.ReferenceFrameEnum.HasValue)
        {
            result.AddWarning(InvalidValue,
                $"Reference frame '{metadata.ReferenceFrame}' is not supported by the framework. " +
                "Supported frames: ICRF, EME2000, GCRF, TEME, ITRF93, ECLIPJ2000, ECLIPB1950, B1950, FK4.",
                "Metadata.ReferenceFrame");
        }

        if (string.IsNullOrWhiteSpace(metadata.TimeSystem))
        {
            result.AddError(RequiredField, "TIME_SYSTEM is required.", "Metadata.TimeSystem");
        }
        else if (!metadata.TimeSystemEnum.HasValue)
        {
            result.AddWarning(InvalidValue,
                $"Time system '{metadata.TimeSystem}' is not supported by the framework. " +
                "Supported time systems: UTC, TAI, TDB, TT, GPS.",
                "Metadata.TimeSystem");
        }

        // Validate reference frame and mean element theory consistency
        var theoryEnum = metadata.MeanElementTheoryEnum;
        if (theoryEnum == MeanElementTheory.SGP4 || theoryEnum == MeanElementTheory.SGP4XP)
        {
            var frameEnum = metadata.ReferenceFrameEnum;
            if (frameEnum.HasValue && frameEnum.Value != CcsdsReferenceFrame.TEME)
            {
                result.AddWarning(InconsistentData,
                    $"SGP4/SGP4-XP theory typically uses TEME reference frame, but {metadata.ReferenceFrame} was specified.",
                    "Metadata.ReferenceFrame");
            }

            // SGP4/SDP4 uses UTC time system
            var timeSystemEnum = metadata.TimeSystemEnum;
            if (timeSystemEnum.HasValue && timeSystemEnum.Value != CcsdsTimeSystem.UTC)
            {
                result.AddWarning(InconsistentData,
                    $"SGP4/SGP4-XP theory typically uses UTC time system, but {metadata.TimeSystem} was specified.",
                    "Metadata.TimeSystem");
            }
        }

        // Validate object ID format (should be international designator format for cataloged objects)
        if (!string.IsNullOrWhiteSpace(metadata.ObjectId) && WarnOnMissingOptionalFields)
        {
            if (!IsValidInternationalDesignator(metadata.ObjectId))
            {
                result.AddInfo(InvalidValue,
                    $"OBJECT_ID '{metadata.ObjectId}' does not match standard international designator format (YYYY-NNNP).",
                    "Metadata.ObjectId");
            }
        }
    }

    private void ValidateData(OmmData data, OmmValidationResult result)
    {
        ValidateMeanElements(data.MeanElements, result);

        if (data.HasSpacecraftParameters)
        {
            ValidateSpacecraftParameters(data.SpacecraftParameters, result);
        }

        if (data.HasCovarianceMatrix)
        {
            ValidateCovarianceMatrix(data.CovarianceMatrix, result);
        }
    }

    private void ValidateMeanElements(MeanElements elements, OmmValidationResult result)
    {
        // Required: Epoch
        if (elements.Epoch == default)
        {
            result.AddError(RequiredField, "EPOCH is required.", "Data.MeanElements.Epoch");
        }

        // Required: Either semi-major axis or mean motion
        if (!elements.UsesMeanMotion && elements.SemiMajorAxis == null)
        {
            result.AddError(RequiredField, "Either SEMI_MAJOR_AXIS or MEAN_MOTION is required.", "Data.MeanElements");
        }

        if (elements.UsesMeanMotion && elements.MeanMotion == null)
        {
            result.AddError(RequiredField, "MEAN_MOTION is marked as used but value is null.", "Data.MeanElements.MeanMotion");
        }

        // Validate physical constraints
        if (ValidatePhysicalConstraints)
        {
            ValidateMeanElementsPhysicalConstraints(elements, result);
        }
    }

    private void ValidateMeanElementsPhysicalConstraints(MeanElements elements, OmmValidationResult result)
    {
        // Eccentricity: must be >= 0
        if (elements.Eccentricity < 0)
        {
            result.AddError(InvalidRange, $"Eccentricity must be >= 0, got {elements.Eccentricity}.", "Data.MeanElements.Eccentricity");
        }
        else if (elements.Eccentricity >= 1)
        {
            // e >= 1 is hyperbolic/parabolic - warning but not error (could be valid for escape trajectories)
            result.AddWarning(PhysicalConstraint,
                $"Eccentricity {elements.Eccentricity} indicates hyperbolic/parabolic trajectory.",
                "Data.MeanElements.Eccentricity");
        }
        else if (elements.Eccentricity > 0.9)
        {
            // High eccentricity warning
            result.AddInfo(PhysicalConstraint,
                $"High eccentricity {elements.Eccentricity} may indicate atmospheric reentry or escape trajectory.",
                "Data.MeanElements.Eccentricity");
        }

        // Inclination: must be 0 to 180 degrees
        if (elements.Inclination < 0 || elements.Inclination > 180)
        {
            result.AddError(InvalidRange, $"Inclination must be 0-180 degrees, got {elements.Inclination}.", "Data.MeanElements.Inclination");
        }

        // RAAN: must be 0 to 360 degrees
        if (elements.RightAscensionOfAscendingNode < 0 || elements.RightAscensionOfAscendingNode >= 360)
        {
            result.AddError(InvalidRange,
                $"RA_OF_ASC_NODE must be 0-360 degrees, got {elements.RightAscensionOfAscendingNode}.",
                "Data.MeanElements.RightAscensionOfAscendingNode");
        }

        // Argument of pericenter: must be 0 to 360 degrees
        if (elements.ArgumentOfPericenter < 0 || elements.ArgumentOfPericenter >= 360)
        {
            result.AddError(InvalidRange,
                $"ARG_OF_PERICENTER must be 0-360 degrees, got {elements.ArgumentOfPericenter}.",
                "Data.MeanElements.ArgumentOfPericenter");
        }

        // Mean anomaly: must be 0 to 360 degrees
        if (elements.MeanAnomaly < 0 || elements.MeanAnomaly >= 360)
        {
            result.AddError(InvalidRange,
                $"MEAN_ANOMALY must be 0-360 degrees, got {elements.MeanAnomaly}.",
                "Data.MeanElements.MeanAnomaly");
        }

        // Semi-major axis: must be positive for bound orbits
        if (elements.SemiMajorAxis.HasValue)
        {
            if (elements.Eccentricity < 1 && elements.SemiMajorAxis.Value <= 0)
            {
                result.AddError(InvalidRange,
                    $"SEMI_MAJOR_AXIS must be positive for bound orbits, got {elements.SemiMajorAxis.Value}.",
                    "Data.MeanElements.SemiMajorAxis");
            }
        }

        // Mean motion: must be positive
        if (elements.MeanMotion.HasValue && elements.MeanMotion.Value <= 0)
        {
            result.AddError(InvalidRange,
                $"MEAN_MOTION must be positive, got {elements.MeanMotion.Value}.",
                "Data.MeanElements.MeanMotion");
        }

        // Gravitational parameter: must be positive if specified
        if (elements.GravitationalParameter.HasValue && elements.GravitationalParameter.Value <= 0)
        {
            result.AddError(InvalidRange,
                $"GM must be positive, got {elements.GravitationalParameter.Value}.",
                "Data.MeanElements.GravitationalParameter");
        }
    }

    private void ValidateSpacecraftParameters(SpacecraftParameters spacecraft, OmmValidationResult result)
    {
        // All spacecraft parameters should be positive if specified
        if (spacecraft.Mass.HasValue && spacecraft.Mass.Value <= 0)
        {
            result.AddError(InvalidRange, $"MASS must be positive, got {spacecraft.Mass.Value}.", "Data.SpacecraftParameters.Mass");
        }

        if (spacecraft.SolarRadArea.HasValue && spacecraft.SolarRadArea.Value < 0)
        {
            result.AddError(InvalidRange,
                $"SOLAR_RAD_AREA must be non-negative, got {spacecraft.SolarRadArea.Value}.",
                "Data.SpacecraftParameters.SolarRadArea");
        }

        if (spacecraft.SolarRadCoeff.HasValue && spacecraft.SolarRadCoeff.Value < 0)
        {
            result.AddError(InvalidRange,
                $"SOLAR_RAD_COEFF must be non-negative, got {spacecraft.SolarRadCoeff.Value}.",
                "Data.SpacecraftParameters.SolarRadCoeff");
        }

        if (spacecraft.DragArea.HasValue && spacecraft.DragArea.Value < 0)
        {
            result.AddError(InvalidRange,
                $"DRAG_AREA must be non-negative, got {spacecraft.DragArea.Value}.",
                "Data.SpacecraftParameters.DragArea");
        }

        if (spacecraft.DragCoeff.HasValue && spacecraft.DragCoeff.Value < 0)
        {
            result.AddError(InvalidRange,
                $"DRAG_COEFF must be non-negative, got {spacecraft.DragCoeff.Value}.",
                "Data.SpacecraftParameters.DragCoeff");
        }

        // Typical drag coefficient is 2.0-2.5, warn if outside reasonable range
        if (spacecraft.DragCoeff.HasValue && (spacecraft.DragCoeff.Value < 1.0 || spacecraft.DragCoeff.Value > 4.0))
        {
            result.AddInfo(PhysicalConstraint,
                $"DRAG_COEFF {spacecraft.DragCoeff.Value} is outside typical range (1.0-4.0).",
                "Data.SpacecraftParameters.DragCoeff");
        }

        // Typical solar radiation coefficient is 1.0-2.0
        if (spacecraft.SolarRadCoeff.HasValue && (spacecraft.SolarRadCoeff.Value < 0.5 || spacecraft.SolarRadCoeff.Value > 3.0))
        {
            result.AddInfo(PhysicalConstraint,
                $"SOLAR_RAD_COEFF {spacecraft.SolarRadCoeff.Value} is outside typical range (0.5-3.0).",
                "Data.SpacecraftParameters.SolarRadCoeff");
        }
    }

    private void ValidateCovarianceMatrix(CovarianceMatrix covariance, OmmValidationResult result)
    {
        // Diagonal elements (variances) must be non-negative
        if (covariance.CxX < 0)
        {
            result.AddError(InvalidRange, "Variance CX_X must be non-negative.", "Data.CovarianceMatrix.CxX");
        }
        if (covariance.CyY < 0)
        {
            result.AddError(InvalidRange, "Variance CY_Y must be non-negative.", "Data.CovarianceMatrix.CyY");
        }
        if (covariance.CzZ < 0)
        {
            result.AddError(InvalidRange, "Variance CZ_Z must be non-negative.", "Data.CovarianceMatrix.CzZ");
        }
        if (covariance.CxDotXDot < 0)
        {
            result.AddError(InvalidRange, "Variance CX_DOT_X_DOT must be non-negative.", "Data.CovarianceMatrix.CxDotXDot");
        }
        if (covariance.CyDotYDot < 0)
        {
            result.AddError(InvalidRange, "Variance CY_DOT_Y_DOT must be non-negative.", "Data.CovarianceMatrix.CyDotYDot");
        }
        if (covariance.CzDotZDot < 0)
        {
            result.AddError(InvalidRange, "Variance CZ_DOT_Z_DOT must be non-negative.", "Data.CovarianceMatrix.CzDotZDot");
        }

        // Reference frame should be specified
        if (string.IsNullOrWhiteSpace(covariance.ReferenceFrame) && WarnOnMissingOptionalFields)
        {
            result.AddWarning(RequiredField,
                "COV_REF_FRAME is recommended for covariance data.",
                "Data.CovarianceMatrix.ReferenceFrame");
        }
    }

    private void ValidateTleParameters(TleParameters tle, OmmMetadata metadata, OmmValidationResult result)
    {
        // NORAD catalog ID should be present and valid
        if (!tle.NoradCatalogId.HasValue || tle.NoradCatalogId.Value <= 0)
        {
            result.AddError(TleValidation, "NORAD_CAT_ID must be a positive integer.", "Data.TleParameters.NoradCatalogId");
        }

        // Either BSTAR or BTERM should be present for SGP4/SDP4
        if (!tle.UsesBStar && !tle.BTerm.HasValue)
        {
            result.AddWarning(TleValidation,
                "Neither BSTAR nor BTERM is specified. One is typically required for propagation.",
                "Data.TleParameters");
        }

        // BSTAR typically ranges from -1 to 1, but extreme values are possible
        if (tle.BStar.HasValue && System.Math.Abs(tle.BStar.Value) > 1)
        {
            result.AddInfo(TleValidation,
                $"BSTAR value {tle.BStar.Value} has unusual magnitude (typical range is -1 to 1).",
                "Data.TleParameters.BStar");
        }

        // Element set number should be non-negative
        if (tle.ElementSetNumber.HasValue && tle.ElementSetNumber.Value < 0)
        {
            result.AddError(TleValidation, "ELEMENT_SET_NO must be non-negative.", "Data.TleParameters.ElementSetNumber");
        }

        // Revolution number should be non-negative
        if (tle.RevolutionNumberAtEpoch.HasValue && tle.RevolutionNumberAtEpoch.Value < 0)
        {
            result.AddError(TleValidation, "REV_AT_EPOCH must be non-negative.", "Data.TleParameters.RevolutionNumberAtEpoch");
        }

        // Classification type validation
        var classification = tle.ClassificationType;
        if (!string.IsNullOrEmpty(classification) &&
            classification != "U" && classification != "C" && classification != "S")
        {
            result.AddWarning(TleValidation,
                $"Unknown classification type: {classification}. Expected U, C, or S.",
                "Data.TleParameters.ClassificationType");
        }

        // Mean element theory should be SGP4 or SGP4-XP when TLE parameters are present
        var theoryEnum = metadata.MeanElementTheoryEnum;
        if (theoryEnum.HasValue &&
            theoryEnum.Value != MeanElementTheory.SGP4 &&
            theoryEnum.Value != MeanElementTheory.SGP4XP)
        {
            result.AddWarning(InconsistentData,
                $"TLE parameters present but mean element theory is {metadata.MeanElementTheory}. Expected SGP4 or SGP4-XP.",
                "Metadata.MeanElementTheory");
        }
    }

    private static bool IsValidInternationalDesignator(string objectId)
    {
        // International designator format: YYYY-NNNP or YYYY-NNNPP
        // YYYY = 4-digit year
        // NNN = 3-digit launch number
        // P or PP = 1-3 letter piece designation
        if (string.IsNullOrWhiteSpace(objectId))
            return false;

        var parts = objectId.Split('-');
        if (parts.Length != 2)
            return false;

        // Check year part (4 digits)
        if (parts[0].Length != 4 || !int.TryParse(parts[0], out int year) || year < 1957 || year > 2100)
            return false;

        // Check launch number and piece (3 digits + 1-3 letters)
        if (parts[1].Length < 4 || parts[1].Length > 6)
            return false;

        // First 3 characters should be digits
        if (!int.TryParse(parts[1].Substring(0, 3), out _))
            return false;

        // Remaining characters should be letters
        string piece = parts[1].Substring(3);
        return piece.All(char.IsLetter);
    }
}
