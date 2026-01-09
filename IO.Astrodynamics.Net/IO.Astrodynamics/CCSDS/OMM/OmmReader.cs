// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using IO.Astrodynamics.CCSDS.Common;

namespace IO.Astrodynamics.CCSDS.OMM;

/// <summary>
/// Reads OMM (Orbit Mean-elements Message) files in CCSDS NDM/XML format.
/// </summary>
/// <remarks>
/// Supports both standalone OMM documents and OMM messages wrapped in NDM containers.
/// Conforms to CCSDS 502.0-B-3 (ODM Blue Book) and CCSDS 505.0-B-3 (NDM/XML Blue Book).
/// </remarks>
public class OmmReader
{
    private static readonly XNamespace CcsdsNamespace = "urn:ccsds:schema:ndmxml";

    /// <summary>
    /// Reads an OMM from the specified file path.
    /// </summary>
    /// <param name="filePath">The path to the OMM file.</param>
    /// <returns>The parsed OMM object.</returns>
    /// <exception cref="ArgumentException">Thrown when the file path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="OmmParseException">Thrown when the file cannot be parsed.</exception>
    public Omm ReadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"OMM file not found: {filePath}", filePath);

        using var stream = File.OpenRead(filePath);
        return ReadFromStream(stream);
    }

    /// <summary>
    /// Reads an OMM from the specified stream.
    /// </summary>
    /// <param name="stream">The stream containing the OMM XML.</param>
    /// <returns>The parsed OMM object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    /// <exception cref="OmmParseException">Thrown when the stream cannot be parsed.</exception>
    public Omm ReadFromStream(Stream stream)
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
            throw new OmmParseException("Failed to parse OMM XML.", ex);
        }
    }

    /// <summary>
    /// Reads an OMM from the specified XML string.
    /// </summary>
    /// <param name="xml">The XML string containing the OMM.</param>
    /// <returns>The parsed OMM object.</returns>
    /// <exception cref="ArgumentException">Thrown when XML string is null or empty.</exception>
    /// <exception cref="OmmParseException">Thrown when the XML cannot be parsed.</exception>
    public Omm ReadFromString(string xml)
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
            throw new OmmParseException("Failed to parse OMM XML.", ex);
        }
    }

    private Omm ParseDocument(XDocument doc)
    {
        var root = doc.Root;
        if (root == null)
            throw new OmmParseException("XML document has no root element.");

        // Handle both standalone OMM and NDM-wrapped OMM
        XElement ommElement = FindOmmElement(root);
        if (ommElement == null)
            throw new OmmParseException("Could not find OMM element in document.");

        return ParseOmmElement(ommElement);
    }

    private XElement FindOmmElement(XElement root)
    {
        var localName = root.Name.LocalName.ToUpperInvariant();

        // Direct OMM element
        if (localName == "OMM")
            return root;

        // NDM wrapper - look for omm child (case-insensitive)
        if (localName == "NDM")
        {
            return root.Elements()
                .FirstOrDefault(e => e.Name.LocalName.Equals("omm", StringComparison.OrdinalIgnoreCase));
        }

        // Check descendants if wrapped in other elements
        return root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName.Equals("omm", StringComparison.OrdinalIgnoreCase));
    }

    private Omm ParseOmmElement(XElement ommElement)
    {
        // Parse header
        var headerElement = GetRequiredElement(ommElement, "header");
        var header = ParseHeader(headerElement);

        // Parse body -> segment
        var bodyElement = GetRequiredElement(ommElement, "body");
        var segmentElement = GetRequiredElement(bodyElement, "segment");

        // Parse metadata
        var metadataElement = GetRequiredElement(segmentElement, "metadata");
        var metadata = ParseMetadata(metadataElement);

        // Parse data
        var dataElement = GetRequiredElement(segmentElement, "data");
        var data = ParseData(dataElement);

        return new Omm(header, metadata, data);
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

    private OmmMetadata ParseMetadata(XElement metadataElement)
    {
        var comments = GetComments(metadataElement);

        var objectName = GetRequiredElementValue(metadataElement, "OBJECT_NAME");
        var objectId = GetRequiredElementValue(metadataElement, "OBJECT_ID");
        var centerName = GetRequiredElementValue(metadataElement, "CENTER_NAME");
        var refFrame = GetRequiredElementValue(metadataElement, "REF_FRAME");
        var timeSystem = GetRequiredElementValue(metadataElement, "TIME_SYSTEM");
        var meanElementTheory = GetRequiredElementValue(metadataElement, "MEAN_ELEMENT_THEORY");

        // Optional reference frame epoch
        var refFrameEpochStr = GetOptionalElementValue(metadataElement, "REF_FRAME_EPOCH");
        DateTime? refFrameEpoch = null;
        if (!string.IsNullOrEmpty(refFrameEpochStr))
            refFrameEpoch = ParseDateTime(refFrameEpochStr, "REF_FRAME_EPOCH");

        return new OmmMetadata(
            objectName, objectId, centerName, refFrame, timeSystem, meanElementTheory,
            refFrameEpoch, comments);
    }

    private OmmData ParseData(XElement dataElement)
    {
        var comments = GetComments(dataElement);

        // Parse required mean elements
        var meanElementsElement = GetRequiredElement(dataElement, "meanElements");
        var meanElements = ParseMeanElements(meanElementsElement);

        // Parse optional spacecraft parameters
        SpacecraftParameters spacecraftParams = null;
        var spacecraftElement = GetOptionalElement(dataElement, "spacecraftParameters");
        if (spacecraftElement != null)
            spacecraftParams = ParseSpacecraftParameters(spacecraftElement);

        // Parse optional TLE parameters
        TleParameters tleParams = null;
        var tleElement = GetOptionalElement(dataElement, "tleParameters");
        if (tleElement != null)
            tleParams = ParseTleParameters(tleElement);

        // Parse optional covariance matrix
        CovarianceMatrix covariance = null;
        var covarianceElement = GetOptionalElement(dataElement, "covarianceMatrix");
        if (covarianceElement != null)
            covariance = ParseCovarianceMatrix(covarianceElement);

        // Parse optional user-defined parameters
        UserDefinedParameters userDefined = null;
        var userDefinedElement = GetOptionalElement(dataElement, "userDefinedParameters");
        if (userDefinedElement != null)
            userDefined = ParseUserDefinedParameters(userDefinedElement);

        return new OmmData(meanElements, spacecraftParams, tleParams, covariance, userDefined, comments);
    }

    private MeanElements ParseMeanElements(XElement element)
    {
        var comments = GetComments(element);

        var epochStr = GetRequiredElementValue(element, "EPOCH");
        var epoch = ParseDateTime(epochStr, "EPOCH");

        // Either SEMI_MAJOR_AXIS or MEAN_MOTION must be present
        var smaStr = GetOptionalElementValue(element, "SEMI_MAJOR_AXIS");
        var meanMotionStr = GetOptionalElementValue(element, "MEAN_MOTION");

        if (string.IsNullOrEmpty(smaStr) && string.IsNullOrEmpty(meanMotionStr))
            throw new OmmParseException("Either SEMI_MAJOR_AXIS or MEAN_MOTION must be specified in meanElements.");

        var eccentricity = ParseDouble(GetRequiredElementValue(element, "ECCENTRICITY"), "ECCENTRICITY");
        var inclination = ParseDouble(GetRequiredElementValue(element, "INCLINATION"), "INCLINATION");
        var raan = ParseDouble(GetRequiredElementValue(element, "RA_OF_ASC_NODE"), "RA_OF_ASC_NODE");
        var argOfPericenter = ParseDouble(GetRequiredElementValue(element, "ARG_OF_PERICENTER"), "ARG_OF_PERICENTER");
        var meanAnomaly = ParseDouble(GetRequiredElementValue(element, "MEAN_ANOMALY"), "MEAN_ANOMALY");

        // Optional GM
        var gmStr = GetOptionalElementValue(element, "GM");
        double? gm = null;
        if (!string.IsNullOrEmpty(gmStr))
            gm = ParseDouble(gmStr, "GM");

        if (!string.IsNullOrEmpty(smaStr))
        {
            var sma = ParseDouble(smaStr, "SEMI_MAJOR_AXIS");
            return MeanElements.CreateWithSemiMajorAxis(
                epoch, sma, eccentricity, inclination, raan, argOfPericenter, meanAnomaly, gm, comments);
        }
        else
        {
            var meanMotion = ParseDouble(meanMotionStr, "MEAN_MOTION");
            return MeanElements.CreateWithMeanMotion(
                epoch, meanMotion, eccentricity, inclination, raan, argOfPericenter, meanAnomaly, gm, comments);
        }
    }

    private SpacecraftParameters ParseSpacecraftParameters(XElement element)
    {
        var comments = GetComments(element);

        var massStr = GetOptionalElementValue(element, "MASS");
        var solarRadAreaStr = GetOptionalElementValue(element, "SOLAR_RAD_AREA");
        var solarRadCoeffStr = GetOptionalElementValue(element, "SOLAR_RAD_COEFF");
        var dragAreaStr = GetOptionalElementValue(element, "DRAG_AREA");
        var dragCoeffStr = GetOptionalElementValue(element, "DRAG_COEFF");

        double? mass = null, solarRadArea = null, solarRadCoeff = null, dragArea = null, dragCoeff = null;

        if (!string.IsNullOrEmpty(massStr)) mass = ParseDouble(massStr, "MASS");
        if (!string.IsNullOrEmpty(solarRadAreaStr)) solarRadArea = ParseDouble(solarRadAreaStr, "SOLAR_RAD_AREA");
        if (!string.IsNullOrEmpty(solarRadCoeffStr)) solarRadCoeff = ParseDouble(solarRadCoeffStr, "SOLAR_RAD_COEFF");
        if (!string.IsNullOrEmpty(dragAreaStr)) dragArea = ParseDouble(dragAreaStr, "DRAG_AREA");
        if (!string.IsNullOrEmpty(dragCoeffStr)) dragCoeff = ParseDouble(dragCoeffStr, "DRAG_COEFF");

        return new SpacecraftParameters(mass, solarRadArea, solarRadCoeff, dragArea, dragCoeff, comments);
    }

    private TleParameters ParseTleParameters(XElement element)
    {
        var comments = GetComments(element);

        // Optional metadata
        var ephemerisTypeStr = GetOptionalElementValue(element, "EPHEMERIS_TYPE");
        var classificationStr = GetOptionalElementValue(element, "CLASSIFICATION_TYPE");
        var noradCatIdStr = GetOptionalElementValue(element, "NORAD_CAT_ID");
        var elementSetNoStr = GetOptionalElementValue(element, "ELEMENT_SET_NO");
        var revAtEpochStr = GetOptionalElementValue(element, "REV_AT_EPOCH");

        int? ephemerisType = null, noradCatId = null, elementSetNo = null, revAtEpoch = null;

        if (!string.IsNullOrEmpty(ephemerisTypeStr)) ephemerisType = ParseInt(ephemerisTypeStr, "EPHEMERIS_TYPE");
        if (!string.IsNullOrEmpty(noradCatIdStr)) noradCatId = ParseInt(noradCatIdStr, "NORAD_CAT_ID");
        if (!string.IsNullOrEmpty(elementSetNoStr)) elementSetNo = ParseInt(elementSetNoStr, "ELEMENT_SET_NO");
        if (!string.IsNullOrEmpty(revAtEpochStr)) revAtEpoch = ParseInt(revAtEpochStr, "REV_AT_EPOCH");

        // BSTAR or BTERM (one required)
        var bstarStr = GetOptionalElementValue(element, "BSTAR");
        var btermStr = GetOptionalElementValue(element, "BTERM");

        // MEAN_MOTION_DOT (required)
        var meanMotionDotStr = GetRequiredElementValue(element, "MEAN_MOTION_DOT");
        var meanMotionDot = ParseDouble(meanMotionDotStr, "MEAN_MOTION_DOT");

        // MEAN_MOTION_DDOT or AGOM (one required)
        var meanMotionDDotStr = GetOptionalElementValue(element, "MEAN_MOTION_DDOT");
        var agomStr = GetOptionalElementValue(element, "AGOM");

        bool usesBStar = !string.IsNullOrEmpty(bstarStr);
        bool usesDDot = !string.IsNullOrEmpty(meanMotionDDotStr);

        if (usesBStar && usesDDot)
        {
            var bstar = ParseDouble(bstarStr, "BSTAR");
            var ddot = ParseDouble(meanMotionDDotStr, "MEAN_MOTION_DDOT");
            return TleParameters.CreateWithBStarAndDDot(
                bstar, meanMotionDot, ddot, ephemerisType, classificationStr, noradCatId, elementSetNo, revAtEpoch, comments);
        }
        else if (usesBStar && !usesDDot)
        {
            var bstar = ParseDouble(bstarStr, "BSTAR");
            var agom = ParseDouble(agomStr, "AGOM");
            return TleParameters.CreateWithBStarAndAGom(
                bstar, meanMotionDot, agom, ephemerisType, classificationStr, noradCatId, elementSetNo, revAtEpoch, comments);
        }
        else if (!usesBStar && usesDDot)
        {
            var bterm = ParseDouble(btermStr, "BTERM");
            var ddot = ParseDouble(meanMotionDDotStr, "MEAN_MOTION_DDOT");
            return TleParameters.CreateWithBTermAndDDot(
                bterm, meanMotionDot, ddot, ephemerisType, classificationStr, noradCatId, elementSetNo, revAtEpoch, comments);
        }
        else
        {
            var bterm = ParseDouble(btermStr, "BTERM");
            var agom = ParseDouble(agomStr, "AGOM");
            return TleParameters.CreateWithBTermAndAGom(
                bterm, meanMotionDot, agom, ephemerisType, classificationStr, noradCatId, elementSetNo, revAtEpoch, comments);
        }
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

    private UserDefinedParameters ParseUserDefinedParameters(XElement element)
    {
        var comments = GetComments(element);
        var parameters = new List<UserDefinedParameter>();

        // Find all USER_DEFINED elements
        foreach (var userDefElement in element.Elements()
            .Where(e => e.Name.LocalName.Equals("USER_DEFINED", StringComparison.OrdinalIgnoreCase)))
        {
            var paramName = userDefElement.Attribute("parameter")?.Value;
            var paramValue = userDefElement.Value;

            if (!string.IsNullOrEmpty(paramName) && paramValue != null)
            {
                parameters.Add(new UserDefinedParameter(paramName, paramValue));
            }
        }

        return new UserDefinedParameters(parameters, comments);
    }

    #region Helper Methods

    private XElement GetRequiredElement(XElement parent, string localName)
    {
        var element = GetOptionalElement(parent, localName);
        if (element == null)
            throw new OmmParseException($"Required element '{localName}' not found in '{parent.Name.LocalName}'.");
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
            throw new OmmParseException($"Required element '{localName}' not found in '{parent.Name.LocalName}'.");
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

        throw new OmmParseException($"Failed to parse datetime value '{value}' for field '{fieldName}'.");
    }

    private double ParseDouble(string value, string fieldName)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new OmmParseException($"Failed to parse numeric value '{value}' for field '{fieldName}'.");
    }

    private int ParseInt(string value, string fieldName)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new OmmParseException($"Failed to parse integer value '{value}' for field '{fieldName}'.");
    }

    #endregion
}

/// <summary>
/// Exception thrown when OMM parsing fails.
/// </summary>
public class OmmParseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmmParseException"/> class.
    /// </summary>
    public OmmParseException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmmParseException"/> class.
    /// </summary>
    public OmmParseException(string message, Exception innerException) : base(message, innerException) { }
}
