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
/// Supports both standalone OPM documents and OPM messages wrapped in NDM containers. A document holds one OPM message:
/// one that holds several (a catalog NDM) throws <see cref="OpmParseException"/>, as before 11.0.0 it returned the first
/// one (#679); messages of another type beside it (an OPM beside an OMM) are ignored. The message holds one header and one
/// body, the body one segment, the segment one metadata and one data section, and the data at most one of each block
/// (NDM/XML 4.0.0; CCSDS 502.0-B-3), the maneuvers excepted: a second one throws <see cref="OpmParseException"/>, where before 11.0.0 the
/// first was read and the others dropped, a whole second segment included. The reader is the guard without schema
/// validation; the schema of NDM/XML 4.0.0, which the validator checks in the document's form (third round of #668),
/// rejects these elements too, but not several messages of an NDM. A keyword whose value the data model refuses (a
/// negative ECCENTRICITY, an INCLINATION outside [0, 180], an empty OBJECT_NAME, ...) throws <see cref="OpmParseException"/> naming the
/// keyword, its value and the rule, the data model's exception as inner exception, where before 11.0.0 the
/// <see cref="ArgumentOutOfRangeException"/> or <see cref="ArgumentException"/> of its constructor crossed the reader; the
/// schema rejects some of these values first, not those its types allow (fourth round of #668). A numeric value that is
/// not finite (NaN, Infinity or -Infinity in any case, an overflow such as 1e400) throws <see cref="OpmParseException"/> naming the
/// keyword and its text, where before 11.0.0 it was read, every range check passing NaN; the schema validator of .NET does
/// not stop these values (#708).
/// Conforms to CCSDS 502.0-B-3 (ODM Blue Book) and CCSDS 505.0-B-3 (NDM/XML Blue Book).
/// </remarks>
internal class OpmReader
{
    /// <summary>
    /// Reads an OPM from the specified file path.
    /// </summary>
    /// <param name="filePath">The path to the OPM file.</param>
    /// <returns>The parsed Opm object.</returns>
    /// <exception cref="ArgumentException">Thrown when the file path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="OpmParseException">Thrown when the file cannot be parsed, holds several OPM messages, or an OPM
    /// with several segments, headers, bodies, metadata or data sections, or blocks of a data section, or a keyword whose
    /// value the data model refuses, naming the keyword and its value (fourth round of #668).</exception>
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
    /// <exception cref="OpmParseException">Thrown when the stream cannot be parsed, holds several OPM messages, or an OPM
    /// with several segments, headers, bodies, metadata or data sections, or blocks of a data section, or a keyword whose
    /// value the data model refuses, naming the keyword and its value (fourth round of #668).</exception>
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
    /// <exception cref="OpmParseException">Thrown when the XML cannot be parsed, holds several OPM messages, or an OPM
    /// with several segments, headers, bodies, metadata or data sections, or blocks of a data section, or a keyword whose
    /// value the data model refuses, naming the keyword and its value (fourth round of #668).</exception>
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

    // The one OPM of a document (all false) or every OPM of it (all true, LoadAll and ParseAll, R22 of #668): a document
    // that holds none throws, as Load does
    internal IReadOnlyList<Opm> Read(XDocument document, bool all)
    {
        if (!all)
            return [ParseDocument(document)];

        var root = document.Root ?? throw new OpmParseException("XML document has no root element.");
        var elements = CcsdsMessageLocator.FindAll(root, "opm");
        if (elements.Count == 0)
            throw new OpmParseException("Could not find OPM element in document.");

        return elements.Select(ParseOpmElement).ToArray();
    }

    // The root OPM, the one OPM child of an NDM or the one OPM descendant of another root; several throw (#679)
    private static XElement FindOpmElement(XElement root)
    {
        return CcsdsMessageLocator.FindSingle(root, "opm", message => new OpmParseException(message));
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

        return Construct(() => new CcsdsHeader(creationDate, originator, comments)
            {
                Classification = classification,
                MessageId = messageId
            }, "header",
            new CcsdsKeyword("originator", "ORIGINATOR", originator));
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

        return Construct(() => new OpmMetadata(
                objectName, objectId, centerName, refFrame, timeSystem,
                refFrameEpoch, comments), "metadata",
            new CcsdsKeyword("objectName", "OBJECT_NAME", objectName),
            new CcsdsKeyword("objectId", "OBJECT_ID", objectId),
            new CcsdsKeyword("centerName", "CENTER_NAME", centerName),
            new CcsdsKeyword("referenceFrame", "REF_FRAME", refFrame),
            new CcsdsKeyword("timeSystem", "TIME_SYSTEM", timeSystem));
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
        var keplerianElement = GetOptionalBlock(dataElement, "keplerianElements");
        if (keplerianElement != null)
            keplerianElements = ParseKeplerianElements(keplerianElement);

        // Parse optional spacecraft parameters
        double? mass = null, solarRadArea = null, solarRadCoeff = null, dragArea = null, dragCoeff = null;
        string massStr = null, solarRadAreaStr = null, solarRadCoeffStr = null, dragAreaStr = null, dragCoeffStr = null;
        IReadOnlyList<string> spacecraftComments = null;
        var spacecraftElement = GetOptionalBlock(dataElement, "spacecraftParameters");
        if (spacecraftElement != null)
        {
            spacecraftComments = GetComments(spacecraftElement);
            massStr = GetOptionalElementValue(spacecraftElement, "MASS");
            solarRadAreaStr = GetOptionalElementValue(spacecraftElement, "SOLAR_RAD_AREA");
            solarRadCoeffStr = GetOptionalElementValue(spacecraftElement, "SOLAR_RAD_COEFF");
            dragAreaStr = GetOptionalElementValue(spacecraftElement, "DRAG_AREA");
            dragCoeffStr = GetOptionalElementValue(spacecraftElement, "DRAG_COEFF");

            if (!string.IsNullOrEmpty(massStr)) mass = ParseDouble(massStr, "MASS");
            if (!string.IsNullOrEmpty(solarRadAreaStr)) solarRadArea = ParseDouble(solarRadAreaStr, "SOLAR_RAD_AREA");
            if (!string.IsNullOrEmpty(solarRadCoeffStr)) solarRadCoeff = ParseDouble(solarRadCoeffStr, "SOLAR_RAD_COEFF");
            if (!string.IsNullOrEmpty(dragAreaStr)) dragArea = ParseDouble(dragAreaStr, "DRAG_AREA");
            if (!string.IsNullOrEmpty(dragCoeffStr)) dragCoeff = ParseDouble(dragCoeffStr, "DRAG_COEFF");
        }

        // Parse optional covariance matrix
        CovarianceMatrix covariance = null;
        var covarianceElement = GetOptionalBlock(dataElement, "covarianceMatrix");
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
        var userDefinedElement = GetOptionalBlock(dataElement, "userDefinedParameters");
        if (userDefinedElement != null)
            userDefinedParameters = ParseUserDefinedParameters(userDefinedElement);

        // The spacecraft parameters are checked by OpmData; a value it refuses throws OpmParseException naming its keyword
        // (fourth round of #668)
        return Construct(() => new OpmData(
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
                dataComments), "spacecraftParameters",
            new CcsdsKeyword("mass", "MASS", massStr),
            new CcsdsKeyword("solarRadiationPressureArea", "SOLAR_RAD_AREA", solarRadAreaStr),
            new CcsdsKeyword("solarRadiationCoefficient", "SOLAR_RAD_COEFF", solarRadCoeffStr),
            new CcsdsKeyword("dragArea", "DRAG_AREA", dragAreaStr),
            new CcsdsKeyword("dragCoefficient", "DRAG_COEFF", dragCoeffStr));
    }

    private OpmStateVector ParseStateVector(XElement element)
    {
        var comments = GetComments(element);

        var epochStr = GetRequiredElementValue(element, "EPOCH");
        var epoch = ParseDateTime(epochStr, "EPOCH");

        var xStr = GetRequiredElementValue(element, "X");
        var yStr = GetRequiredElementValue(element, "Y");
        var zStr = GetRequiredElementValue(element, "Z");
        var xDotStr = GetRequiredElementValue(element, "X_DOT");
        var yDotStr = GetRequiredElementValue(element, "Y_DOT");
        var zDotStr = GetRequiredElementValue(element, "Z_DOT");
        var x = ParseDouble(xStr, "X");
        var y = ParseDouble(yStr, "Y");
        var z = ParseDouble(zStr, "Z");
        var xDot = ParseDouble(xDotStr, "X_DOT");
        var yDot = ParseDouble(yDotStr, "Y_DOT");
        var zDot = ParseDouble(zDotStr, "Z_DOT");

        // A component whose value in m or m/s is beyond the range of a double throws OpmParseException naming its keyword
        // (#729), as the CDM reader refuses it (#708)
        return Construct(() => new OpmStateVector(epoch, x, y, z, xDot, yDot, zDot, comments), "stateVector",
            new CcsdsKeyword("x", "X", xStr),
            new CcsdsKeyword("y", "Y", yStr),
            new CcsdsKeyword("z", "Z", zStr),
            new CcsdsKeyword("xDot", "X_DOT", xDotStr),
            new CcsdsKeyword("yDot", "Y_DOT", yDotStr),
            new CcsdsKeyword("zDot", "Z_DOT", zDotStr));
    }

    private OpmKeplerianElements ParseKeplerianElements(XElement element)
    {
        var comments = GetComments(element);

        var smaStr = GetRequiredElementValue(element, "SEMI_MAJOR_AXIS");
        var eccStr = GetRequiredElementValue(element, "ECCENTRICITY");
        var incStr = GetRequiredElementValue(element, "INCLINATION");
        var sma = ParseDouble(smaStr, "SEMI_MAJOR_AXIS");
        var ecc = ParseDouble(eccStr, "ECCENTRICITY");
        var inc = ParseDouble(incStr, "INCLINATION");
        var raanStr = GetRequiredElementValue(element, "RA_OF_ASC_NODE");
        var raan = ParseDouble(raanStr, "RA_OF_ASC_NODE");
        var aopStr = GetRequiredElementValue(element, "ARG_OF_PERICENTER");
        var aop = ParseDouble(aopStr, "ARG_OF_PERICENTER");

        // Either TRUE_ANOMALY or MEAN_ANOMALY (choice)
        var trueAnomalyStr = GetOptionalElementValue(element, "TRUE_ANOMALY");
        var meanAnomalyStr = GetOptionalElementValue(element, "MEAN_ANOMALY");

        if (string.IsNullOrEmpty(trueAnomalyStr) && string.IsNullOrEmpty(meanAnomalyStr))
            throw new OpmParseException("Either TRUE_ANOMALY or MEAN_ANOMALY must be specified in keplerianElements.");

        if (!string.IsNullOrEmpty(trueAnomalyStr) && !string.IsNullOrEmpty(meanAnomalyStr))
            throw new OpmParseException("Only one of TRUE_ANOMALY or MEAN_ANOMALY should be specified in keplerianElements.");

        var gmStr = GetRequiredElementValue(element, "GM");
        var gm = ParseDouble(gmStr, "GM");

        // A value the data model refuses throws OpmParseException naming its keyword (fourth round of #668); the sign of
        // SEMI_MAJOR_AXIS depends on ECCENTRICITY, so its message gives both
        var keywords = new[]
        {
            new CcsdsKeyword("semiMajorAxis", "SEMI_MAJOR_AXIS", smaStr, $"ECCENTRICITY '{eccStr}'"),
            new CcsdsKeyword("eccentricity", "ECCENTRICITY", eccStr),
            new CcsdsKeyword("inclination", "INCLINATION", incStr),
            new CcsdsKeyword("gm", "GM", gmStr),
            new CcsdsKeyword("raan", "RA_OF_ASC_NODE", raanStr),
            new CcsdsKeyword("aop", "ARG_OF_PERICENTER", aopStr),
            new CcsdsKeyword("trueAnomaly", "TRUE_ANOMALY", trueAnomalyStr),
            new CcsdsKeyword("meanAnomaly", "MEAN_ANOMALY", meanAnomalyStr)
        };

        if (!string.IsNullOrEmpty(trueAnomalyStr))
        {
            var trueAnomaly = ParseDouble(trueAnomalyStr, "TRUE_ANOMALY");
            return Construct(() => OpmKeplerianElements.CreateWithTrueAnomaly(sma, ecc, inc, raan, aop, trueAnomaly, gm, comments),
                "keplerianElements", keywords);
        }
        else
        {
            var meanAnomaly = ParseDouble(meanAnomalyStr, "MEAN_ANOMALY");
            return Construct(() => OpmKeplerianElements.CreateWithMeanAnomaly(sma, ecc, inc, raan, aop, meanAnomaly, gm, comments),
                "keplerianElements", keywords);
        }
    }

    private OpmManeuverParameters ParseManeuverParameters(XElement element)
    {
        var comments = GetComments(element);

        var epochStr = GetRequiredElementValue(element, "MAN_EPOCH_IGNITION");
        var epoch = ParseDateTime(epochStr, "MAN_EPOCH_IGNITION");

        var durationStr = GetRequiredElementValue(element, "MAN_DURATION");
        var deltaMassStr = GetRequiredElementValue(element, "MAN_DELTA_MASS");
        var duration = ParseDouble(durationStr, "MAN_DURATION");
        var deltaMass = ParseDouble(deltaMassStr, "MAN_DELTA_MASS");
        var refFrame = GetRequiredElementValue(element, "MAN_REF_FRAME");
        var dv1 = ParseDouble(GetRequiredElementValue(element, "MAN_DV_1"), "MAN_DV_1");
        var dv2 = ParseDouble(GetRequiredElementValue(element, "MAN_DV_2"), "MAN_DV_2");
        var dv3 = ParseDouble(GetRequiredElementValue(element, "MAN_DV_3"), "MAN_DV_3");

        return Construct(() => new OpmManeuverParameters(epoch, duration, deltaMass, refFrame, dv1, dv2, dv3, comments),
            "maneuverParameters",
            new CcsdsKeyword("manDuration", "MAN_DURATION", durationStr),
            new CcsdsKeyword("manDeltaMass", "MAN_DELTA_MASS", deltaMassStr),
            new CcsdsKeyword("manRefFrame", "MAN_REF_FRAME", refFrame));
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

    // Builds a data-model object; a value it refuses throws OpmParseException naming its keyword and its text, not the
    // ArgumentException of the constructor (fourth round of #668)
    private static T Construct<T>(Func<T> construct, string block, params CcsdsKeyword[] keywords)
    {
        return CcsdsValues.Construct(construct, block, (message, inner) => new OpmParseException(message, inner), keywords);
    }

    // A header, body, segment, metadata, data section or required data block: one, which the schema bounds to one; none
    // or several throw, a second one no longer dropped silently
    private static XElement GetRequiredElement(XElement parent, string localName)
    {
        var element = GetOptionalBlock(parent, localName);
        if (element == null)
            throw new OpmParseException($"Required element '{localName}' not found in '{parent.Name.LocalName}'.");
        return element;
    }

    // An optional data block, which the schema bounds to one: null for none, several throw
    private static XElement GetOptionalBlock(XElement parent, string localName)
    {
        return CcsdsElements.AtMostOne(parent, localName, "OPM", message => new OpmParseException(message));
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

    private static DateTime ParseDateTime(string value, string fieldName)
    {
        // A CCSDS date, calendar or day of year (#490)
        if (CcsdsEpochParser.TryParse(value, out var result))
        {
            return result;
        }

        throw new OpmParseException($"Failed to parse datetime value '{value}' for field '{fieldName}'.");
    }

    // A value is a real number (#708): the text double.TryParse reads as NaN or ±Infinity
    // ("NaN", "Infinity" in any case, an overflow such as "1e400") is refused, as the lexical INF of xsd:double it rejects
    private double ParseDouble(string value, string fieldName)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            throw new OpmParseException($"Failed to parse numeric value '{value}' for field '{fieldName}'.");

        if (!double.IsFinite(result))
            throw new OpmParseException($"Failed to parse numeric value '{value}' for field '{fieldName}': not a finite number.");

        return result;
    }

    #endregion
}
