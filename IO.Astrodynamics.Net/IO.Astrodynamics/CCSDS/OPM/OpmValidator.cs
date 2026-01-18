// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using IO.Astrodynamics.CCSDS.Common;

namespace IO.Astrodynamics.CCSDS.OPM;

/// <summary>
/// Validates OPM documents against CCSDS standards and physical constraints.
/// </summary>
public class OpmValidator
{
    // Error codes
    private const string RequiredField = "OPM001";
    private const string InvalidRange = "OPM002";
    private const string InvalidValue = "OPM003";
    private const string InconsistentData = "OPM004";
    private const string PhysicalConstraint = "OPM005";
    private const string SchemaValidation = "OPM006";

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
    /// Gets or sets whether to add warnings for recommended but optional fields.
    /// Default is true.
    /// </summary>
    public bool WarnOnMissingOptionalFields { get; set; } = true;

    /// <summary>
    /// Validates an OPM XML file against the CCSDS XSD schema.
    /// </summary>
    /// <param name="filePath">Path to the OPM XML file.</param>
    /// <returns>Validation result containing any schema errors.</returns>
    public OpmValidationResult ValidateSchema(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            return OpmValidationResult.Failure(SchemaValidation, $"File not found: {filePath}", "File");
        }

        using var stream = File.OpenRead(filePath);
        return ValidateSchema(stream);
    }

    /// <summary>
    /// Validates OPM XML content against the CCSDS XSD schema.
    /// </summary>
    /// <param name="xmlContent">The OPM XML content as a string.</param>
    /// <returns>Validation result containing any schema errors.</returns>
    public OpmValidationResult ValidateSchemaFromXml(string xmlContent)
    {
        ArgumentNullException.ThrowIfNull(xmlContent);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xmlContent));
        return ValidateSchema(stream);
    }

    /// <summary>
    /// Validates OPM XML content from a stream against the CCSDS XSD schema.
    /// </summary>
    /// <param name="xmlStream">Stream containing the OPM XML content.</param>
    /// <returns>Validation result containing any schema errors.</returns>
    public OpmValidationResult ValidateSchema(Stream xmlStream)
    {
        ArgumentNullException.ThrowIfNull(xmlStream);

        var result = new OpmValidationResult();

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
                    ? OpmValidationSeverity.Error
                    : OpmValidationSeverity.Warning;

                var path = e.Exception?.SourceUri ?? "XML";
                if (e.Exception?.LineNumber > 0)
                {
                    path = $"Line {e.Exception.LineNumber}, Position {e.Exception.LinePosition}";
                }

                result.AddIssue(new OpmValidationError(severity, SchemaValidation, e.Message, path));
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
    /// Validates an OPM file completely: first against XSD schema, then against physical constraints.
    /// </summary>
    /// <param name="filePath">Path to the OPM XML file.</param>
    /// <returns>Validation result containing all errors and warnings.</returns>
    public OpmValidationResult ValidateFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        // First validate against XSD schema
        var result = ValidateSchema(filePath);

        // If schema validation passed, also validate the parsed content
        if (result.IsValid)
        {
            try
            {
                var opm = new OpmReader().ReadFromFile(filePath);
                var contentResult = Validate(opm);

                // Merge the results
                foreach (var issue in contentResult.Issues)
                {
                    result.AddIssue(issue);
                }
            }
            catch (Exception ex)
            {
                result.AddError(SchemaValidation, $"Failed to parse OPM: {ex.Message}", "Parser");
            }
        }

        return result;
    }

    /// <summary>
    /// Gets or lazily initializes the XSD schema set for OPM validation.
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
            var schemaFiles = new[]
            {
                "ndmxml-4.0.0-common-4.0.xsd",
                "ndmxml-4.0.0-opm-3.0.xsd"
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

            // Add the root element declaration manually
            var rootElementSchema = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<xsd:schema xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
            xmlns:ndm=""urn:ccsds:schema:ndmxml""
            targetNamespace=""urn:ccsds:schema:ndmxml""
            elementFormDefault=""qualified"">
    <xsd:element name=""opm"" type=""ndm:opmType""/>
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
    /// Validates an OPM document.
    /// </summary>
    /// <param name="opm">The OPM document to validate.</param>
    /// <returns>Validation result containing any errors or warnings.</returns>
    public OpmValidationResult Validate(Opm opm)
    {
        ArgumentNullException.ThrowIfNull(opm);

        var result = new OpmValidationResult();

        ValidateHeader(opm.Header, result);
        ValidateMetadata(opm.Metadata, result);
        ValidateData(opm.Data, result);

        return result;
    }

    private void ValidateHeader(CcsdsHeader header, OpmValidationResult result)
    {
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

    private void ValidateMetadata(OpmMetadata metadata, OpmValidationResult result)
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
                $"Reference frame '{metadata.ReferenceFrame}' is not a standard CCSDS frame.",
                "Metadata.ReferenceFrame");
        }

        if (string.IsNullOrWhiteSpace(metadata.TimeSystem))
        {
            result.AddError(RequiredField, "TIME_SYSTEM is required.", "Metadata.TimeSystem");
        }
        else if (!metadata.TimeSystemEnum.HasValue)
        {
            result.AddWarning(InvalidValue,
                $"Time system '{metadata.TimeSystem}' is not a standard CCSDS time system.",
                "Metadata.TimeSystem");
        }
    }

    private void ValidateData(OpmData data, OpmValidationResult result)
    {
        // Validate state vector (required)
        ValidateStateVector(data.StateVector, result);

        // Validate Keplerian elements (if present)
        if (data.HasKeplerianElements)
        {
            ValidateKeplerianElements(data.KeplerianElements, result);
        }

        // Validate spacecraft parameters (if present)
        if (data.HasSpacecraftParameters)
        {
            ValidateSpacecraftParameters(data, result);
        }

        // Validate covariance matrix (if present)
        if (data.HasCovariance)
        {
            ValidateCovarianceMatrix(data.Covariance, result);
        }

        // Validate maneuvers (if present)
        for (int i = 0; i < data.Maneuvers.Count; i++)
        {
            ValidateManeuver(data.Maneuvers[i], i, result);
        }
    }

    private void ValidateStateVector(OpmStateVector stateVector, OpmValidationResult result)
    {
        if (stateVector == null)
        {
            result.AddError(RequiredField, "StateVector is required.", "Data.StateVector");
            return;
        }

        if (!ValidatePhysicalConstraints)
            return;

        // Basic sanity checks for position (typical LEO to GEO range)
        var positionMagnitude = System.Math.Sqrt(
            stateVector.X * stateVector.X +
            stateVector.Y * stateVector.Y +
            stateVector.Z * stateVector.Z);

        if (positionMagnitude < 100) // Less than 100 km
        {
            result.AddWarning(PhysicalConstraint,
                $"Position magnitude ({positionMagnitude:F1} km) is unusually small (inside Earth?).",
                "Data.StateVector.Position");
        }

        if (positionMagnitude > 1e8) // More than 100 million km
        {
            result.AddWarning(PhysicalConstraint,
                $"Position magnitude ({positionMagnitude:E2} km) is unusually large.",
                "Data.StateVector.Position");
        }

        // Basic sanity checks for velocity
        var velocityMagnitude = System.Math.Sqrt(
            stateVector.XDot * stateVector.XDot +
            stateVector.YDot * stateVector.YDot +
            stateVector.ZDot * stateVector.ZDot);

        if (velocityMagnitude < 0.1) // Less than 0.1 km/s
        {
            result.AddWarning(PhysicalConstraint,
                $"Velocity magnitude ({velocityMagnitude:F4} km/s) is unusually small.",
                "Data.StateVector.Velocity");
        }

        if (velocityMagnitude > 100) // More than 100 km/s
        {
            result.AddWarning(PhysicalConstraint,
                $"Velocity magnitude ({velocityMagnitude:F2} km/s) is unusually large (exceeds escape velocity from most bodies).",
                "Data.StateVector.Velocity");
        }
    }

    private void ValidateKeplerianElements(OpmKeplerianElements keplerian, OpmValidationResult result)
    {
        if (!ValidatePhysicalConstraints)
            return;

        // Eccentricity must be >= 0
        if (keplerian.Eccentricity < 0)
        {
            result.AddError(InvalidRange, "Eccentricity cannot be negative.", "Data.KeplerianElements.Eccentricity");
        }

        // Inclination must be 0-180 degrees
        if (keplerian.Inclination < 0 || keplerian.Inclination > 180)
        {
            result.AddError(InvalidRange,
                $"Inclination ({keplerian.Inclination}°) must be between 0 and 180 degrees.",
                "Data.KeplerianElements.Inclination");
        }

        // Semi-major axis must be positive for elliptical orbits
        if (keplerian.Eccentricity < 1 && keplerian.SemiMajorAxis <= 0)
        {
            result.AddError(InvalidRange,
                "Semi-major axis must be positive for elliptical orbits.",
                "Data.KeplerianElements.SemiMajorAxis");
        }

        // GM must be positive
        if (keplerian.GravitationalParameter <= 0)
        {
            result.AddError(InvalidRange,
                "Gravitational parameter (GM) must be positive.",
                "Data.KeplerianElements.GM");
        }
    }

    private void ValidateSpacecraftParameters(OpmData data, OpmValidationResult result)
    {
        if (!ValidatePhysicalConstraints)
            return;

        if (data.Mass.HasValue && data.Mass.Value <= 0)
        {
            result.AddError(InvalidRange, "Mass must be positive.", "Data.SpacecraftParameters.Mass");
        }

        if (data.DragArea.HasValue && data.DragArea.Value < 0)
        {
            result.AddError(InvalidRange, "Drag area cannot be negative.", "Data.SpacecraftParameters.DragArea");
        }

        if (data.DragCoefficient.HasValue && data.DragCoefficient.Value < 0)
        {
            result.AddError(InvalidRange, "Drag coefficient cannot be negative.", "Data.SpacecraftParameters.DragCoeff");
        }

        if (data.SolarRadiationPressureArea.HasValue && data.SolarRadiationPressureArea.Value < 0)
        {
            result.AddError(InvalidRange, "Solar radiation pressure area cannot be negative.", "Data.SpacecraftParameters.SolarRadArea");
        }

        if (data.SolarRadiationCoefficient.HasValue)
        {
            if (data.SolarRadiationCoefficient.Value < 0)
            {
                result.AddError(InvalidRange, "Solar radiation coefficient cannot be negative.", "Data.SpacecraftParameters.SolarRadCoeff");
            }
            else if (data.SolarRadiationCoefficient.Value > 2.5)
            {
                result.AddWarning(PhysicalConstraint,
                    $"Solar radiation coefficient ({data.SolarRadiationCoefficient.Value}) is unusually large (typical range: 1.0-2.0).",
                    "Data.SpacecraftParameters.SolarRadCoeff");
            }
        }
    }

    private void ValidateCovarianceMatrix(CovarianceMatrix covariance, OpmValidationResult result)
    {
        if (!ValidatePhysicalConstraints)
            return;

        // Diagonal elements (variances) must be non-negative
        if (covariance.CxX < 0)
            result.AddError(InvalidRange, "Variance CX_X cannot be negative.", "Data.Covariance.CX_X");
        if (covariance.CyY < 0)
            result.AddError(InvalidRange, "Variance CY_Y cannot be negative.", "Data.Covariance.CY_Y");
        if (covariance.CzZ < 0)
            result.AddError(InvalidRange, "Variance CZ_Z cannot be negative.", "Data.Covariance.CZ_Z");
        if (covariance.CxDotXDot < 0)
            result.AddError(InvalidRange, "Variance CX_DOT_X_DOT cannot be negative.", "Data.Covariance.CX_DOT_X_DOT");
        if (covariance.CyDotYDot < 0)
            result.AddError(InvalidRange, "Variance CY_DOT_Y_DOT cannot be negative.", "Data.Covariance.CY_DOT_Y_DOT");
        if (covariance.CzDotZDot < 0)
            result.AddError(InvalidRange, "Variance CZ_DOT_Z_DOT cannot be negative.", "Data.Covariance.CZ_DOT_Z_DOT");
    }

    private void ValidateManeuver(OpmManeuverParameters maneuver, int index, OpmValidationResult result)
    {
        var basePath = $"Data.Maneuvers[{index}]";

        if (string.IsNullOrWhiteSpace(maneuver.ManRefFrame))
        {
            result.AddError(RequiredField, "MAN_REF_FRAME is required.", $"{basePath}.ManRefFrame");
        }

        if (!ValidatePhysicalConstraints)
            return;

        if (maneuver.ManDuration < 0)
        {
            result.AddError(InvalidRange, "Maneuver duration cannot be negative.", $"{basePath}.ManDuration");
        }

        if (maneuver.ManDeltaMass > 0)
        {
            result.AddError(InvalidRange, "Maneuver delta mass must be <= 0 (mass is expelled).", $"{basePath}.ManDeltaMass");
        }

        // Warn on unusually large delta-V
        if (maneuver.DeltaVMagnitude > 10)
        {
            result.AddWarning(PhysicalConstraint,
                $"Delta-V magnitude ({maneuver.DeltaVMagnitude:F3} km/s) is unusually large.",
                $"{basePath}.DeltaV");
        }
    }
}
