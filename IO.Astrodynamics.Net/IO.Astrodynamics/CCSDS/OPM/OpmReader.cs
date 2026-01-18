// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using IO.Astrodynamics.CCSDS.Common;

namespace IO.Astrodynamics.CCSDS.OPM;

/// <summary>
/// Reads OPM (Orbit Parameter Message) files in CCSDS NDM/XML format.
/// </summary>
/// <remarks>
/// Supports both standalone OPM documents and OPM messages wrapped in NDM containers.
/// Conforms to CCSDS 502.0-B-3 (ODM Blue Book) and CCSDS 505.0-B-3 (NDM/XML Blue Book).
/// </remarks>
public class OpmReader
{
    /// <summary>
    /// Reads an OPM from the specified file path.
    /// </summary>
    /// <param name="filePath">The path to the OPM file.</param>
    /// <returns>The parsed Opm object.</returns>
    /// <exception cref="ArgumentException">Thrown when the file path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="OpmParseException">Thrown when the file cannot be parsed.</exception>
    public Opm ReadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"OPM file not found: {filePath}", filePath);

        using var stream = File.OpenRead(filePath);
        return ReadFromStream(stream);
    }

    /// <summary>
    /// Reads an OPM from the specified stream.
    /// </summary>
    /// <param name="stream">The stream containing the OPM XML.</param>
    /// <returns>The parsed Opm object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    /// <exception cref="OpmParseException">Thrown when the stream cannot be parsed.</exception>
    public Opm ReadFromStream(Stream stream)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        try
        {
            var doc = XDocument.Load(stream);
            return ParseDocument(doc);
        }
        catch (XmlException ex)
        {
            throw new OpmParseException("Failed to parse OPM XML.", ex);
        }
    }

    /// <summary>
    /// Reads an OPM from the specified XML string.
    /// </summary>
    /// <param name="xml">The XML string containing the OPM.</param>
    /// <returns>The parsed Opm object.</returns>
    /// <exception cref="ArgumentException">Thrown when XML string is null or empty.</exception>
    /// <exception cref="OpmParseException">Thrown when the XML cannot be parsed.</exception>
    public Opm ReadFromString(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("XML string cannot be null or empty.", nameof(xml));

        try
        {
            var doc = XDocument.Parse(xml);
            return ParseDocument(doc);
        }
        catch (XmlException ex)
        {
            throw new OpmParseException("Failed to parse OPM XML.", ex);
        }
    }

    private Opm ParseDocument(XDocument doc)
    {
        var root = doc.Root;
        if (root == null)
            throw new OpmParseException("XML document has no root element.");

        // Handle both standalone OPM and NDM-wrapped OPM
        XElement opmElement = FindOpmElement(root);
        if (opmElement == null)
            throw new OpmParseException("Could not find OPM element in document.");

        return ParseOpmElement(opmElement);
    }

    private XElement FindOpmElement(XElement root)
    {
        var localName = root.Name.LocalName.ToUpperInvariant();

        // Direct OPM element
        if (localName == "OPM")
            return root;

        // NDM wrapper - look for opm child (case-insensitive)
        if (localName == "NDM")
        {
            return root.Elements()
                .FirstOrDefault(e => e.Name.LocalName.Equals("opm", StringComparison.OrdinalIgnoreCase));
        }

        // Check descendants if wrapped in other elements
        return root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName.Equals("opm", StringComparison.OrdinalIgnoreCase));
    }

    private Opm ParseOpmElement(XElement opmElement)
    {
        // Parse header
        var headerElement = GetRequiredElement(opmElement, "header");
        var header = ParseHeader(headerElement);

        // Parse body -> segment
        var bodyElement = GetRequiredElement(opmElement, "body");
        var segmentElement = GetRequiredElement(bodyElement, "segment");

        // Parse metadata
        var metadataElement = GetRequiredElement(segmentElement, "metadata");
        var metadata = ParseMetadata(metadataElement);

        // Parse data
        var dataElement = GetRequiredElement(segmentElement, "data");
        var data = ParseData(dataElement);

        return new Opm(header, metadata, data);
    }

    private CcsdsHeader ParseHeader(XElement headerElement)
    {
        var comments = GetComments(headerElement);
        var creationDateStr = GetRequiredElementValue(headerElement, "CREATION_DATE");
        var originator = GetRequiredElementValue(headerElement, "ORIGINATOR");

        var creationDate = ParseDateTime(creationDateStr, "CREATION_DATE");

        // Optional fields
        var classification = GetOptionalElementValue(headerElement, "CLASSIFICATION");
        var messageId = GetOptionalElementValue(headerElement, "MESSAGE_ID");

        return new CcsdsHeader(creationDate, originator, comments)
        {
            Classification = classification,
            MessageId = messageId
        };
    }

    private OpmMetadata ParseMetadata(XElement metadataElement)
    {
        var comments = GetComments(metadataElement);

        var objectName = GetRequiredElementValue(metadataElement, "OBJECT_NAME");
        var objectId = GetRequiredElementValue(metadataElement, "OBJECT_ID");
        var centerName = GetRequiredElementValue(metadataElement, "CENTER_NAME");
        var refFrame = GetRequiredElementValue(metadataElement, "REF_FRAME");
        var timeSystem = GetRequiredElementValue(metadataElement, "TIME_SYSTEM");

        // Optional reference frame epoch
        var refFrameEpochStr = GetOptionalElementValue(metadataElement, "REF_FRAME_EPOCH");
        DateTime? refFrameEpoch = null;
        if (!string.IsNullOrEmpty(refFrameEpochStr))
            refFrameEpoch = ParseDateTime(refFrameEpochStr, "REF_FRAME_EPOCH");

        return new OpmMetadata(
            objectName, objectId, centerName, refFrame, timeSystem,
            refFrameEpoch, comments);
    }

    private OpmData ParseData(XElement dataElement)
    {
        // Parse data-level comments
        var dataComments = GetComments(dataElement);

        // Parse required state vector
        var stateVectorElement = GetRequiredElement(dataElement, "stateVector");
        var stateVector = ParseStateVector(stateVectorElement);

        // Parse optional Keplerian elements
        OpmKeplerianElements keplerianElements = null;
        var keplerianElement = GetOptionalElement(dataElement, "keplerianElements");
        if (keplerianElement != null)
            keplerianElements = ParseKeplerianElements(keplerianElement);

        // Parse optional spacecraft parameters
        double? mass = null, solarRadArea = null, solarRadCoeff = null, dragArea = null, dragCoeff = null;
        IReadOnlyList<string> spacecraftComments = null;
        var spacecraftElement = GetOptionalElement(dataElement, "spacecraftParameters");
        if (spacecraftElement != null)
        {
            spacecraftComments = GetComments(spacecraftElement);
            var massStr = GetOptionalElementValue(spacecraftElement, "MASS");
            var solarRadAreaStr = GetOptionalElementValue(spacecraftElement, "SOLAR_RAD_AREA");
            var solarRadCoeffStr = GetOptionalElementValue(spacecraftElement, "SOLAR_RAD_COEFF");
            var dragAreaStr = GetOptionalElementValue(spacecraftElement, "DRAG_AREA");
            var dragCoeffStr = GetOptionalElementValue(spacecraftElement, "DRAG_COEFF");

            if (!string.IsNullOrEmpty(massStr)) mass = ParseDouble(massStr, "MASS");
            if (!string.IsNullOrEmpty(solarRadAreaStr)) solarRadArea = ParseDouble(solarRadAreaStr, "SOLAR_RAD_AREA");
            if (!string.IsNullOrEmpty(solarRadCoeffStr)) solarRadCoeff = ParseDouble(solarRadCoeffStr, "SOLAR_RAD_COEFF");
            if (!string.IsNullOrEmpty(dragAreaStr)) dragArea = ParseDouble(dragAreaStr, "DRAG_AREA");
            if (!string.IsNullOrEmpty(dragCoeffStr)) dragCoeff = ParseDouble(dragCoeffStr, "DRAG_COEFF");
        }

        // Parse optional covariance matrix
        CovarianceMatrix covariance = null;
        var covarianceElement = GetOptionalElement(dataElement, "covarianceMatrix");
        if (covarianceElement != null)
            covariance = ParseCovarianceMatrix(covarianceElement);

        // Parse maneuver parameters (0..*)
        var maneuvers = new List<OpmManeuverParameters>();
        foreach (var maneuverElement in dataElement.Elements()
            .Where(e => e.Name.LocalName.Equals("maneuverParameters", StringComparison.OrdinalIgnoreCase)))
        {
            maneuvers.Add(ParseManeuverParameters(maneuverElement));
        }

        // Parse optional user-defined parameters
        OpmUserDefinedParameters userDefinedParameters = null;
        var userDefinedElement = GetOptionalElement(dataElement, "userDefinedParameters");
        if (userDefinedElement != null)
            userDefinedParameters = ParseUserDefinedParameters(userDefinedElement);

        return new OpmData(
            stateVector,
            keplerianElements,
            mass,
            solarRadArea,
            solarRadCoeff,
            dragArea,
            dragCoeff,
            covariance,
            maneuvers,
            userDefinedParameters,
            spacecraftComments,
            dataComments);
    }

    private OpmStateVector ParseStateVector(XElement element)
    {
        var comments = GetComments(element);

        var epochStr = GetRequiredElementValue(element, "EPOCH");
        var epoch = ParseDateTime(epochStr, "EPOCH");

        var x = ParseDouble(GetRequiredElementValue(element, "X"), "X");
        var y = ParseDouble(GetRequiredElementValue(element, "Y"), "Y");
        var z = ParseDouble(GetRequiredElementValue(element, "Z"), "Z");
        var xDot = ParseDouble(GetRequiredElementValue(element, "X_DOT"), "X_DOT");
        var yDot = ParseDouble(GetRequiredElementValue(element, "Y_DOT"), "Y_DOT");
        var zDot = ParseDouble(GetRequiredElementValue(element, "Z_DOT"), "Z_DOT");

        return new OpmStateVector(epoch, x, y, z, xDot, yDot, zDot, comments);
    }

    private OpmKeplerianElements ParseKeplerianElements(XElement element)
    {
        var comments = GetComments(element);

        var sma = ParseDouble(GetRequiredElementValue(element, "SEMI_MAJOR_AXIS"), "SEMI_MAJOR_AXIS");
        var ecc = ParseDouble(GetRequiredElementValue(element, "ECCENTRICITY"), "ECCENTRICITY");
        var inc = ParseDouble(GetRequiredElementValue(element, "INCLINATION"), "INCLINATION");
        var raan = ParseDouble(GetRequiredElementValue(element, "RA_OF_ASC_NODE"), "RA_OF_ASC_NODE");
        var aop = ParseDouble(GetRequiredElementValue(element, "ARG_OF_PERICENTER"), "ARG_OF_PERICENTER");

        // Either TRUE_ANOMALY or MEAN_ANOMALY (choice)
        var trueAnomalyStr = GetOptionalElementValue(element, "TRUE_ANOMALY");
        var meanAnomalyStr = GetOptionalElementValue(element, "MEAN_ANOMALY");

        if (string.IsNullOrEmpty(trueAnomalyStr) && string.IsNullOrEmpty(meanAnomalyStr))
            throw new OpmParseException("Either TRUE_ANOMALY or MEAN_ANOMALY must be specified in keplerianElements.");

        if (!string.IsNullOrEmpty(trueAnomalyStr) && !string.IsNullOrEmpty(meanAnomalyStr))
            throw new OpmParseException("Only one of TRUE_ANOMALY or MEAN_ANOMALY should be specified in keplerianElements.");

        var gm = ParseDouble(GetRequiredElementValue(element, "GM"), "GM");

        if (!string.IsNullOrEmpty(trueAnomalyStr))
        {
            var trueAnomaly = ParseDouble(trueAnomalyStr, "TRUE_ANOMALY");
            return OpmKeplerianElements.CreateWithTrueAnomaly(sma, ecc, inc, raan, aop, trueAnomaly, gm, comments);
        }
        else
        {
            var meanAnomaly = ParseDouble(meanAnomalyStr, "MEAN_ANOMALY");
            return OpmKeplerianElements.CreateWithMeanAnomaly(sma, ecc, inc, raan, aop, meanAnomaly, gm, comments);
        }
    }

    private OpmManeuverParameters ParseManeuverParameters(XElement element)
    {
        var comments = GetComments(element);

        var epochStr = GetRequiredElementValue(element, "MAN_EPOCH_IGNITION");
        var epoch = ParseDateTime(epochStr, "MAN_EPOCH_IGNITION");

        var duration = ParseDouble(GetRequiredElementValue(element, "MAN_DURATION"), "MAN_DURATION");
        var deltaMass = ParseDouble(GetRequiredElementValue(element, "MAN_DELTA_MASS"), "MAN_DELTA_MASS");
        var refFrame = GetRequiredElementValue(element, "MAN_REF_FRAME");
        var dv1 = ParseDouble(GetRequiredElementValue(element, "MAN_DV_1"), "MAN_DV_1");
        var dv2 = ParseDouble(GetRequiredElementValue(element, "MAN_DV_2"), "MAN_DV_2");
        var dv3 = ParseDouble(GetRequiredElementValue(element, "MAN_DV_3"), "MAN_DV_3");

        return new OpmManeuverParameters(epoch, duration, deltaMass, refFrame, dv1, dv2, dv3, comments);
    }

    private CovarianceMatrix ParseCovarianceMatrix(XElement element)
    {
        var comments = GetComments(element);
        var refFrame = GetOptionalElementValue(element, "COV_REF_FRAME");

        var cxX = ParseDouble(GetRequiredElementValue(element, "CX_X"), "CX_X");
        var cyX = ParseDouble(GetRequiredElementValue(element, "CY_X"), "CY_X");
        var cyY = ParseDouble(GetRequiredElementValue(element, "CY_Y"), "CY_Y");
        var czX = ParseDouble(GetRequiredElementValue(element, "CZ_X"), "CZ_X");
        var czY = ParseDouble(GetRequiredElementValue(element, "CZ_Y"), "CZ_Y");
        var czZ = ParseDouble(GetRequiredElementValue(element, "CZ_Z"), "CZ_Z");
        var cxDotX = ParseDouble(GetRequiredElementValue(element, "CX_DOT_X"), "CX_DOT_X");
        var cxDotY = ParseDouble(GetRequiredElementValue(element, "CX_DOT_Y"), "CX_DOT_Y");
        var cxDotZ = ParseDouble(GetRequiredElementValue(element, "CX_DOT_Z"), "CX_DOT_Z");
        var cxDotXDot = ParseDouble(GetRequiredElementValue(element, "CX_DOT_X_DOT"), "CX_DOT_X_DOT");
        var cyDotX = ParseDouble(GetRequiredElementValue(element, "CY_DOT_X"), "CY_DOT_X");
        var cyDotY = ParseDouble(GetRequiredElementValue(element, "CY_DOT_Y"), "CY_DOT_Y");
        var cyDotZ = ParseDouble(GetRequiredElementValue(element, "CY_DOT_Z"), "CY_DOT_Z");
        var cyDotXDot = ParseDouble(GetRequiredElementValue(element, "CY_DOT_X_DOT"), "CY_DOT_X_DOT");
        var cyDotYDot = ParseDouble(GetRequiredElementValue(element, "CY_DOT_Y_DOT"), "CY_DOT_Y_DOT");
        var czDotX = ParseDouble(GetRequiredElementValue(element, "CZ_DOT_X"), "CZ_DOT_X");
        var czDotY = ParseDouble(GetRequiredElementValue(element, "CZ_DOT_Y"), "CZ_DOT_Y");
        var czDotZ = ParseDouble(GetRequiredElementValue(element, "CZ_DOT_Z"), "CZ_DOT_Z");
        var czDotXDot = ParseDouble(GetRequiredElementValue(element, "CZ_DOT_X_DOT"), "CZ_DOT_X_DOT");
        var czDotYDot = ParseDouble(GetRequiredElementValue(element, "CZ_DOT_Y_DOT"), "CZ_DOT_Y_DOT");
        var czDotZDot = ParseDouble(GetRequiredElementValue(element, "CZ_DOT_Z_DOT"), "CZ_DOT_Z_DOT");

        return new CovarianceMatrix(
            cxX, cyX, cyY, czX, czY, czZ,
            cxDotX, cxDotY, cxDotZ, cxDotXDot,
            cyDotX, cyDotY, cyDotZ, cyDotXDot, cyDotYDot,
            czDotX, czDotY, czDotZ, czDotXDot, czDotYDot, czDotZDot,
            refFrame, comments);
    }

    private OpmUserDefinedParameters ParseUserDefinedParameters(XElement element)
    {
        var comments = GetComments(element);
        var parameters = new Dictionary<string, string>();

        // Parse USER_DEFINED elements with "parameter" attribute
        foreach (var userDefElement in element.Elements()
            .Where(e => e.Name.LocalName.Equals("USER_DEFINED", StringComparison.OrdinalIgnoreCase)))
        {
            var parameterName = userDefElement.Attribute("parameter")?.Value;
            if (!string.IsNullOrEmpty(parameterName))
            {
                parameters[parameterName] = userDefElement.Value;
            }
        }

        return new OpmUserDefinedParameters(parameters, comments);
    }

    #region Helper Methods

    private XElement GetRequiredElement(XElement parent, string localName)
    {
        var element = GetOptionalElement(parent, localName);
        if (element == null)
            throw new OpmParseException($"Required element '{localName}' not found in '{parent.Name.LocalName}'.");
        return element;
    }

    private XElement GetOptionalElement(XElement parent, string localName)
    {
        return parent.Elements()
            .FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));
    }

    private string GetRequiredElementValue(XElement parent, string localName)
    {
        var element = GetOptionalElement(parent, localName);
        if (element == null)
            throw new OpmParseException($"Required element '{localName}' not found in '{parent.Name.LocalName}'.");
        return element.Value;
    }

    private string GetOptionalElementValue(XElement parent, string localName)
    {
        var element = GetOptionalElement(parent, localName);
        return element?.Value;
    }

    private IReadOnlyList<string> GetComments(XElement element)
    {
        return element.Elements()
            .Where(e => e.Name.LocalName.Equals("COMMENT", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Value)
            .ToList();
    }

    private DateTime ParseDateTime(string value, string fieldName)
    {
        // CCSDS datetime format: yyyy-MM-ddTHH:mm:ss.ffffff or yyyy-DDDTHH:mm:ss.ffffff
        // Also supports optional 'Z' suffix
        var formats = new[]
        {
            "yyyy-MM-ddTHH:mm:ss.ffffff",
            "yyyy-MM-ddTHH:mm:ss.fffff",
            "yyyy-MM-ddTHH:mm:ss.ffff",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss.ff",
            "yyyy-MM-ddTHH:mm:ss.f",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.ffffffZ",
            "yyyy-DDDTHH:mm:ss.ffffff",
            "yyyy-DDDTHH:mm:ss",
        };

        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result))
        {
            return result;
        }

        // Try standard parse as fallback
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
        {
            return result;
        }

        throw new OpmParseException($"Failed to parse datetime value '{value}' for field '{fieldName}'.");
    }

    private double ParseDouble(string value, string fieldName)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new OpmParseException($"Failed to parse numeric value '{value}' for field '{fieldName}'.");
    }

    #endregion
}
