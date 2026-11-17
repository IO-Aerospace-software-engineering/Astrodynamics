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
/// Supports both standalone OMM documents and OMM messages wrapped in NDM containers. A document holds one OMM message:
/// one that holds several (a catalog NDM) throws <see cref="OmmParseException"/>, as before 11.0.0 it returned the first
/// one (#679); messages of another type beside it (an OPM beside an OMM) are ignored. The message holds one header and one
/// body, the body one segment, the segment one metadata and one data section, and the data at most one of each block
/// (NDM/XML 4.0.0; CCSDS 502.0-B-3): a second one throws <see cref="OmmParseException"/>, where before 11.0.0 the
/// first was read and the others dropped, a whole second segment included. The reader is the guard without schema
/// validation; the schema of NDM/XML 4.0.0, which the validator checks in the document's form (third round of #668),
/// rejects these elements too, but not several messages of an NDM. A keyword whose value the data model refuses (a
/// negative ECCENTRICITY, an INCLINATION outside [0, 180], an empty OBJECT_NAME, ...) throws <see cref="OmmParseException"/> naming the
/// keyword, its value and the rule, the data model's exception as inner exception, where before 11.0.0 the
/// <see cref="ArgumentOutOfRangeException"/> or <see cref="ArgumentException"/> of its constructor crossed the reader; the
/// schema rejects some of these values first, not those its types allow (fourth round of #668). A numeric value that is
/// not finite (NaN, Infinity or -Infinity in any case, an overflow such as 1e400) throws <see cref="OmmParseException"/> naming the
/// keyword and its text, where before 11.0.0 it was read, every range check passing NaN; the schema validator of .NET does
/// not stop these values (#708).
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
    /// <exception cref="OmmParseException">Thrown when the file cannot be parsed, holds several OMM messages, or an OMM
    /// with several segments, headers, bodies, metadata or data sections, or blocks of a data section, or a keyword whose
    /// value the data model refuses, naming the keyword and its value (fourth round of #668).</exception>
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
    /// <exception cref="OmmParseException">Thrown when the stream cannot be parsed, holds several OMM messages, or an OMM
    /// with several segments, headers, bodies, metadata or data sections, or blocks of a data section, or a keyword whose
    /// value the data model refuses, naming the keyword and its value (fourth round of #668).</exception>
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
    /// <exception cref="OmmParseException">Thrown when the XML cannot be parsed, holds several OMM messages, or an OMM
    /// with several segments, headers, bodies, metadata or data sections, or blocks of a data section, or a keyword whose
    /// value the data model refuses, naming the keyword and its value (fourth round of #668).</exception>
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

    // The root OMM, the one OMM child of an NDM or the one OMM descendant of another root; several throw (#679)
    private static XElement FindOmmElement(XElement root)
    {
        return CcsdsMessageLocator.FindSingle(root, "omm", message => new OmmParseException(message));
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

        return Construct(() => new CcsdsHeader(creationDate, originator, comments)
            {
                Classification = classification,
                MessageId = messageId
            }, "header",
            new CcsdsKeyword("originator", "ORIGINATOR", originator));
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

        return Construct(() => new OmmMetadata(
                objectName, objectId, centerName, refFrame, timeSystem, meanElementTheory,
                refFrameEpoch, comments), "metadata",
            new CcsdsKeyword("objectName", "OBJECT_NAME", objectName),
            new CcsdsKeyword("objectId", "OBJECT_ID", objectId),
            new CcsdsKeyword("centerName", "CENTER_NAME", centerName),
            new CcsdsKeyword("referenceFrame", "REF_FRAME", refFrame),
            new CcsdsKeyword("timeSystem", "TIME_SYSTEM", timeSystem),
            new CcsdsKeyword("meanElementTheory", "MEAN_ELEMENT_THEORY", meanElementTheory));
    }

    private OmmData ParseData(XElement dataElement)
    {
        var comments = GetComments(dataElement);

        // Parse required mean elements
        var meanElementsElement = GetRequiredElement(dataElement, "meanElements");
        var meanElements = ParseMeanElements(meanElementsElement);

        // Parse optional spacecraft parameters
        SpacecraftParameters spacecraftParams = null;
        var spacecraftElement = GetOptionalBlock(dataElement, "spacecraftParameters");
        if (spacecraftElement != null)
            spacecraftParams = ParseSpacecraftParameters(spacecraftElement);

        // Parse optional TLE parameters
        TleParameters tleParams = null;
        var tleElement = GetOptionalBlock(dataElement, "tleParameters");
        if (tleElement != null)
            tleParams = ParseTleParameters(tleElement);

        // Parse optional covariance matrix
        CovarianceMatrix covariance = null;
        var covarianceElement = GetOptionalBlock(dataElement, "covarianceMatrix");
        if (covarianceElement != null)
            covariance = ParseCovarianceMatrix(covarianceElement);

        // Parse optional user-defined parameters
        UserDefinedParameters userDefined = null;
        var userDefinedElement = GetOptionalBlock(dataElement, "userDefinedParameters");
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

        var eccentricityStr = GetRequiredElementValue(element, "ECCENTRICITY");
        var inclinationStr = GetRequiredElementValue(element, "INCLINATION");
        var raanStr = GetRequiredElementValue(element, "RA_OF_ASC_NODE");
        var argOfPericenterStr = GetRequiredElementValue(element, "ARG_OF_PERICENTER");
        var meanAnomalyStr = GetRequiredElementValue(element, "MEAN_ANOMALY");
        var eccentricity = ParseDouble(eccentricityStr, "ECCENTRICITY");
        var inclination = ParseDouble(inclinationStr, "INCLINATION");
        var raan = ParseDouble(raanStr, "RA_OF_ASC_NODE");
        var argOfPericenter = ParseDouble(argOfPericenterStr, "ARG_OF_PERICENTER");
        var meanAnomaly = ParseDouble(meanAnomalyStr, "MEAN_ANOMALY");

        // Optional GM
        var gmStr = GetOptionalElementValue(element, "GM");
        double? gm = null;
        if (!string.IsNullOrEmpty(gmStr))
            gm = ParseDouble(gmStr, "GM");

        // A value the data model refuses throws OmmParseException naming its keyword (fourth round of #668)
        var keywords = new[]
        {
            new CcsdsKeyword("semiMajorAxis", "SEMI_MAJOR_AXIS", smaStr),
            new CcsdsKeyword("meanMotion", "MEAN_MOTION", meanMotionStr),
            new CcsdsKeyword("eccentricity", "ECCENTRICITY", eccentricityStr),
            new CcsdsKeyword("inclination", "INCLINATION", inclinationStr),
            new CcsdsKeyword("raan", "RA_OF_ASC_NODE", raanStr),
            new CcsdsKeyword("argOfPericenter", "ARG_OF_PERICENTER", argOfPericenterStr),
            new CcsdsKeyword("meanAnomaly", "MEAN_ANOMALY", meanAnomalyStr),
            new CcsdsKeyword("gm", "GM", gmStr)
        };

        if (!string.IsNullOrEmpty(smaStr))
        {
            var sma = ParseDouble(smaStr, "SEMI_MAJOR_AXIS");
            return Construct(() => MeanElements.CreateWithSemiMajorAxis(
                epoch, sma, eccentricity, inclination, raan, argOfPericenter, meanAnomaly, gm, comments), "meanElements", keywords);
        }
        else
        {
            var meanMotion = ParseDouble(meanMotionStr, "MEAN_MOTION");
            return Construct(() => MeanElements.CreateWithMeanMotion(
                epoch, meanMotion, eccentricity, inclination, raan, argOfPericenter, meanAnomaly, gm, comments), "meanElements", keywords);
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

        return Construct(() => new SpacecraftParameters(mass, solarRadArea, solarRadCoeff, dragArea, dragCoeff, comments),
            "spacecraftParameters",
            new CcsdsKeyword("mass", "MASS", massStr),
            new CcsdsKeyword("solarRadArea", "SOLAR_RAD_AREA", solarRadAreaStr),
            new CcsdsKeyword("solarRadCoeff", "SOLAR_RAD_COEFF", solarRadCoeffStr),
            new CcsdsKeyword("dragArea", "DRAG_AREA", dragAreaStr),
            new CcsdsKeyword("dragCoeff", "DRAG_COEFF", dragCoeffStr));
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

        // A value the data model refuses throws OmmParseException naming its keyword (fourth round of #668)
        TleParameters Build(Func<TleParameters> create) => Construct(create, "tleParameters",
            new CcsdsKeyword("elementSetNumber", "ELEMENT_SET_NO", elementSetNoStr),
            new CcsdsKeyword("revolutionNumberAtEpoch", "REV_AT_EPOCH", revAtEpochStr));

        if (usesBStar && usesDDot)
        {
            var bstar = ParseDouble(bstarStr, "BSTAR");
            var ddot = ParseDouble(meanMotionDDotStr, "MEAN_MOTION_DDOT");
            return Build(() => TleParameters.CreateWithBStarAndDDot(
                bstar, meanMotionDot, ddot, ephemerisType, classificationStr, noradCatId, elementSetNo, revAtEpoch, comments));
        }
        else if (usesBStar && !usesDDot)
        {
            var bstar = ParseDouble(bstarStr, "BSTAR");
            var agom = ParseDouble(agomStr, "AGOM");
            return Build(() => TleParameters.CreateWithBStarAndAGom(
                bstar, meanMotionDot, agom, ephemerisType, classificationStr, noradCatId, elementSetNo, revAtEpoch, comments));
        }
        else if (!usesBStar && usesDDot)
        {
            var bterm = ParseDouble(btermStr, "BTERM");
            var ddot = ParseDouble(meanMotionDDotStr, "MEAN_MOTION_DDOT");
            return Build(() => TleParameters.CreateWithBTermAndDDot(
                bterm, meanMotionDot, ddot, ephemerisType, classificationStr, noradCatId, elementSetNo, revAtEpoch, comments));
        }
        else
        {
            var bterm = ParseDouble(btermStr, "BTERM");
            var agom = ParseDouble(agomStr, "AGOM");
            return Build(() => TleParameters.CreateWithBTermAndAGom(
                bterm, meanMotionDot, agom, ephemerisType, classificationStr, noradCatId, elementSetNo, revAtEpoch, comments));
        }
    }

    private CovarianceMatrix ParseCovarianceMatrix(XElement element)
    {
        var comments = GetComments(element);
        var refFrame = GetOptionalElementValue(element, "COV_REF_FRAME");

        var keywords = CovarianceMatrix.TermKeywords;
        var texts = new string[keywords.Length];
        var terms = new double[keywords.Length];
        for (int i = 0; i < keywords.Length; i++)
        {
            texts[i] = GetRequiredElementValue(element, keywords[i].Keyword);
            terms[i] = ParseDouble(texts[i], keywords[i].Keyword);
        }

        // A term whose value in m², m²/s or m²/s² is beyond the range of a double throws the parse exception naming its
        // keyword (#729)
        return Construct(() => CovarianceMatrix.FromTerms(terms, refFrame, comments), "covarianceMatrix",
            keywords.Select((keyword, i) => new CcsdsKeyword(keyword.Parameter, keyword.Keyword, texts[i])).ToArray());
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
                parameters.Add(Construct(() => new UserDefinedParameter(paramName, paramValue), "userDefinedParameters",
                    new CcsdsKeyword("name", "USER_DEFINED parameter", paramName)));
            }
        }

        return new UserDefinedParameters(parameters, comments);
    }

    #region Helper Methods

    // Builds a data-model object; a value it refuses throws OmmParseException naming its keyword and its text, not the
    // ArgumentException of the constructor (fourth round of #668)
    private static T Construct<T>(Func<T> construct, string block, params CcsdsKeyword[] keywords)
    {
        return CcsdsValues.Construct(construct, block, (message, inner) => new OmmParseException(message, inner), keywords);
    }

    // A header, body, segment, metadata, data section or required data block: one, which the schema bounds to one; none
    // or several throw, a second one no longer dropped silently
    private static XElement GetRequiredElement(XElement parent, string localName)
    {
        var element = GetOptionalBlock(parent, localName);
        if (element == null)
            throw new OmmParseException($"Required element '{localName}' not found in '{parent.Name.LocalName}'.");
        return element;
    }

    // An optional data block, which the schema bounds to one: null for none, several throw
    private static XElement GetOptionalBlock(XElement parent, string localName)
    {
        return CcsdsElements.AtMostOne(parent, localName, "OMM", message => new OmmParseException(message));
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

    private static DateTime ParseDateTime(string value, string fieldName)
    {
        // A CCSDS date, calendar or day of year (#490)
        if (CcsdsEpochParser.TryParse(value, out var result))
        {
            return result;
        }

        throw new OmmParseException($"Failed to parse datetime value '{value}' for field '{fieldName}'.");
    }

    // A value is a real number (#708): the text double.TryParse reads as NaN or ±Infinity
    // ("NaN", "Infinity" in any case, an overflow such as "1e400") is refused, as the lexical INF of xsd:double it rejects
    private double ParseDouble(string value, string fieldName)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            throw new OmmParseException($"Failed to parse numeric value '{value}' for field '{fieldName}'.");

        if (!double.IsFinite(result))
            throw new OmmParseException($"Failed to parse numeric value '{value}' for field '{fieldName}': not a finite number.");

        return result;
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
