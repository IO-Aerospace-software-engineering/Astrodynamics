// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using IO.Astrodynamics.CCSDS.Common;

namespace IO.Astrodynamics.CCSDS.OPM;

/// <summary>
/// Writes OPM (Orbit Parameter Message) files in CCSDS NDM/XML format.
/// </summary>
/// <remarks>
/// Generates XML conforming to CCSDS 502.0-B-3 (ODM Blue Book) and CCSDS 505.0-B-3 (NDM/XML Blue Book).
/// Supports both standalone OPM documents and OPM messages wrapped in NDM containers.
/// </remarks>
public class OpmWriter
{
    private const string CcsdsSchemaLocation = "https://sanaregistry.org/r/ndmxml_unqualified/ndmxml-4.0.0-master-4.0.xsd";
    private static readonly XNamespace XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>
    /// Gets or sets a value indicating whether to wrap the OPM in an NDM container element.
    /// </summary>
    /// <remarks>
    /// Default is true. When true, the output will be wrapped in an &lt;ndm&gt; element.
    /// </remarks>
    public bool WrapInNdmContainer { get; set; } = true;

    /// <summary>
    /// Gets or sets the XML indentation settings.
    /// </summary>
    public bool IndentOutput { get; set; } = true;

    /// <summary>
    /// Writes an OPM to the specified file path.
    /// </summary>
    /// <param name="opm">The OPM to write.</param>
    /// <param name="filePath">The file path to write to.</param>
    /// <exception cref="ArgumentNullException">Thrown when opm is null.</exception>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    public void WriteToFile(Opm opm, string filePath)
    {
        if (opm == null)
            throw new ArgumentNullException(nameof(opm));
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        var xml = WriteToString(opm);
        File.WriteAllText(filePath, xml, Encoding.UTF8);
    }

    /// <summary>
    /// Writes an OPM to the specified stream.
    /// </summary>
    /// <param name="opm">The OPM to write.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException">Thrown when opm or stream is null.</exception>
    public void WriteToStream(Opm opm, Stream stream)
    {
        if (opm == null)
            throw new ArgumentNullException(nameof(opm));
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        var doc = CreateDocument(opm);

        var settings = new XmlWriterSettings
        {
            Indent = IndentOutput,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        };

        using var writer = XmlWriter.Create(stream, settings);
        doc.Save(writer);
    }

    /// <summary>
    /// Writes an OPM to an XML string.
    /// </summary>
    /// <param name="opm">The OPM to write.</param>
    /// <returns>The XML string representation of the OPM.</returns>
    /// <exception cref="ArgumentNullException">Thrown when opm is null.</exception>
    public string WriteToString(Opm opm)
    {
        if (opm == null)
            throw new ArgumentNullException(nameof(opm));

        var doc = CreateDocument(opm);

        var settings = new XmlWriterSettings
        {
            Indent = IndentOutput,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        };

        // Use MemoryStream + StreamReader to ensure UTF-8 encoding in XML declaration
        using var memoryStream = new MemoryStream();
        using (var writer = XmlWriter.Create(memoryStream, settings))
        {
            doc.Save(writer);
        }

        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private XDocument CreateDocument(Opm opm)
    {
        var opmElement = CreateOpmElement(opm);

        XElement root;
        if (WrapInNdmContainer)
        {
            root = new XElement("ndm",
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
                new XAttribute(XsiNamespace + "noNamespaceSchemaLocation", CcsdsSchemaLocation),
                opmElement);
        }
        else
        {
            opmElement.Add(new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace));
            opmElement.Add(new XAttribute(XsiNamespace + "noNamespaceSchemaLocation", CcsdsSchemaLocation));
            root = opmElement;
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
    }

    private XElement CreateOpmElement(Opm opm)
    {
        var opmElement = new XElement("opm",
            new XAttribute("id", Opm.FormatId),
            new XAttribute("version", Opm.Version));

        opmElement.Add(CreateHeaderElement(opm.Header));
        opmElement.Add(CreateBodyElement(opm));

        return opmElement;
    }

    private XElement CreateHeaderElement(CcsdsHeader header)
    {
        var headerElement = new XElement("header");

        foreach (var comment in header.Comments)
        {
            headerElement.Add(new XElement("COMMENT", comment));
        }

        if (!string.IsNullOrEmpty(header.Classification))
        {
            headerElement.Add(new XElement("CLASSIFICATION", header.Classification));
        }

        headerElement.Add(new XElement("CREATION_DATE", FormatDateTime(header.CreationDate)));
        headerElement.Add(new XElement("ORIGINATOR", header.Originator));

        if (!string.IsNullOrEmpty(header.MessageId))
        {
            headerElement.Add(new XElement("MESSAGE_ID", header.MessageId));
        }

        return headerElement;
    }

    private XElement CreateBodyElement(Opm opm)
    {
        var bodyElement = new XElement("body");
        var segmentElement = new XElement("segment");

        segmentElement.Add(CreateMetadataElement(opm.Metadata));
        segmentElement.Add(CreateDataElement(opm.Data));

        bodyElement.Add(segmentElement);
        return bodyElement;
    }

    private XElement CreateMetadataElement(OpmMetadata metadata)
    {
        var metadataElement = new XElement("metadata");

        foreach (var comment in metadata.Comments)
        {
            metadataElement.Add(new XElement("COMMENT", comment));
        }

        metadataElement.Add(new XElement("OBJECT_NAME", metadata.ObjectName));
        metadataElement.Add(new XElement("OBJECT_ID", metadata.ObjectId));
        metadataElement.Add(new XElement("CENTER_NAME", metadata.CenterName));
        metadataElement.Add(new XElement("REF_FRAME", metadata.ReferenceFrame));

        if (metadata.ReferenceFrameEpoch.HasValue)
        {
            metadataElement.Add(new XElement("REF_FRAME_EPOCH", FormatDateTime(metadata.ReferenceFrameEpoch.Value)));
        }

        metadataElement.Add(new XElement("TIME_SYSTEM", metadata.TimeSystem));

        return metadataElement;
    }

    private XElement CreateDataElement(OpmData data)
    {
        var dataElement = new XElement("data");

        // Data-level comments (before stateVector per schema)
        foreach (var comment in data.Comments)
        {
            dataElement.Add(new XElement("COMMENT", comment));
        }

        // State vector (required)
        dataElement.Add(CreateStateVectorElement(data.StateVector));

        // Keplerian elements (optional)
        if (data.HasKeplerianElements)
        {
            dataElement.Add(CreateKeplerianElementsElement(data.KeplerianElements));
        }

        // Spacecraft parameters (optional)
        if (data.HasSpacecraftParameters)
        {
            dataElement.Add(CreateSpacecraftParametersElement(data));
        }

        // Covariance matrix (optional)
        if (data.HasCovariance)
        {
            dataElement.Add(CreateCovarianceMatrixElement(data.Covariance));
        }

        // Maneuver parameters (0..*)
        foreach (var maneuver in data.Maneuvers)
        {
            dataElement.Add(CreateManeuverParametersElement(maneuver));
        }

        // User-defined parameters (optional)
        if (data.HasUserDefinedParameters)
        {
            dataElement.Add(CreateUserDefinedParametersElement(data.UserDefinedParameters));
        }

        return dataElement;
    }

    private XElement CreateStateVectorElement(OpmStateVector stateVector)
    {
        var element = new XElement("stateVector");

        foreach (var comment in stateVector.Comments)
        {
            element.Add(new XElement("COMMENT", comment));
        }

        element.Add(new XElement("EPOCH", FormatDateTime(stateVector.Epoch)));
        element.Add(CreateElementWithUnits("X", stateVector.X, "km"));
        element.Add(CreateElementWithUnits("Y", stateVector.Y, "km"));
        element.Add(CreateElementWithUnits("Z", stateVector.Z, "km"));
        element.Add(CreateElementWithUnits("X_DOT", stateVector.XDot, "km/s"));
        element.Add(CreateElementWithUnits("Y_DOT", stateVector.YDot, "km/s"));
        element.Add(CreateElementWithUnits("Z_DOT", stateVector.ZDot, "km/s"));

        return element;
    }

    private XElement CreateKeplerianElementsElement(OpmKeplerianElements keplerianElements)
    {
        var element = new XElement("keplerianElements");

        foreach (var comment in keplerianElements.Comments)
        {
            element.Add(new XElement("COMMENT", comment));
        }

        element.Add(CreateElementWithUnits("SEMI_MAJOR_AXIS", keplerianElements.SemiMajorAxis, "km"));
        element.Add(new XElement("ECCENTRICITY", FormatDouble(keplerianElements.Eccentricity)));
        element.Add(CreateElementWithUnits("INCLINATION", keplerianElements.Inclination, "deg"));
        element.Add(CreateElementWithUnits("RA_OF_ASC_NODE", keplerianElements.RightAscensionOfAscendingNode, "deg"));
        element.Add(CreateElementWithUnits("ARG_OF_PERICENTER", keplerianElements.ArgumentOfPericenter, "deg"));

        // TRUE_ANOMALY or MEAN_ANOMALY (choice)
        if (keplerianElements.UsesTrueAnomaly)
        {
            element.Add(CreateElementWithUnits("TRUE_ANOMALY", keplerianElements.TrueAnomaly.Value, "deg"));
        }
        else
        {
            element.Add(CreateElementWithUnits("MEAN_ANOMALY", keplerianElements.MeanAnomaly.Value, "deg"));
        }

        element.Add(CreateElementWithUnits("GM", keplerianElements.GravitationalParameter, "km**3/s**2"));

        return element;
    }

    private XElement CreateSpacecraftParametersElement(OpmData data)
    {
        var element = new XElement("spacecraftParameters");

        foreach (var comment in data.SpacecraftComments)
        {
            element.Add(new XElement("COMMENT", comment));
        }

        if (data.Mass.HasValue)
        {
            element.Add(CreateElementWithUnits("MASS", data.Mass.Value, "kg"));
        }

        if (data.SolarRadiationPressureArea.HasValue)
        {
            element.Add(CreateElementWithUnits("SOLAR_RAD_AREA", data.SolarRadiationPressureArea.Value, "m**2"));
        }

        if (data.SolarRadiationCoefficient.HasValue)
        {
            element.Add(new XElement("SOLAR_RAD_COEFF", FormatDouble(data.SolarRadiationCoefficient.Value)));
        }

        if (data.DragArea.HasValue)
        {
            element.Add(CreateElementWithUnits("DRAG_AREA", data.DragArea.Value, "m**2"));
        }

        if (data.DragCoefficient.HasValue)
        {
            element.Add(new XElement("DRAG_COEFF", FormatDouble(data.DragCoefficient.Value)));
        }

        return element;
    }

    private XElement CreateCovarianceMatrixElement(CovarianceMatrix covariance)
    {
        var element = new XElement("covarianceMatrix");

        foreach (var comment in covariance.Comments)
        {
            element.Add(new XElement("COMMENT", comment));
        }

        if (!string.IsNullOrEmpty(covariance.ReferenceFrame))
        {
            element.Add(new XElement("COV_REF_FRAME", covariance.ReferenceFrame));
        }

        // Position covariance (km²)
        element.Add(CreateElementWithUnits("CX_X", covariance.CxX, "km**2"));
        element.Add(CreateElementWithUnits("CY_X", covariance.CyX, "km**2"));
        element.Add(CreateElementWithUnits("CY_Y", covariance.CyY, "km**2"));
        element.Add(CreateElementWithUnits("CZ_X", covariance.CzX, "km**2"));
        element.Add(CreateElementWithUnits("CZ_Y", covariance.CzY, "km**2"));
        element.Add(CreateElementWithUnits("CZ_Z", covariance.CzZ, "km**2"));

        // Position-velocity cross-covariance (km²/s)
        element.Add(CreateElementWithUnits("CX_DOT_X", covariance.CxDotX, "km**2/s"));
        element.Add(CreateElementWithUnits("CX_DOT_Y", covariance.CxDotY, "km**2/s"));
        element.Add(CreateElementWithUnits("CX_DOT_Z", covariance.CxDotZ, "km**2/s"));

        // Velocity covariance (km²/s²)
        element.Add(CreateElementWithUnits("CX_DOT_X_DOT", covariance.CxDotXDot, "km**2/s**2"));

        element.Add(CreateElementWithUnits("CY_DOT_X", covariance.CyDotX, "km**2/s"));
        element.Add(CreateElementWithUnits("CY_DOT_Y", covariance.CyDotY, "km**2/s"));
        element.Add(CreateElementWithUnits("CY_DOT_Z", covariance.CyDotZ, "km**2/s"));
        element.Add(CreateElementWithUnits("CY_DOT_X_DOT", covariance.CyDotXDot, "km**2/s**2"));
        element.Add(CreateElementWithUnits("CY_DOT_Y_DOT", covariance.CyDotYDot, "km**2/s**2"));

        element.Add(CreateElementWithUnits("CZ_DOT_X", covariance.CzDotX, "km**2/s"));
        element.Add(CreateElementWithUnits("CZ_DOT_Y", covariance.CzDotY, "km**2/s"));
        element.Add(CreateElementWithUnits("CZ_DOT_Z", covariance.CzDotZ, "km**2/s"));
        element.Add(CreateElementWithUnits("CZ_DOT_X_DOT", covariance.CzDotXDot, "km**2/s**2"));
        element.Add(CreateElementWithUnits("CZ_DOT_Y_DOT", covariance.CzDotYDot, "km**2/s**2"));
        element.Add(CreateElementWithUnits("CZ_DOT_Z_DOT", covariance.CzDotZDot, "km**2/s**2"));

        return element;
    }

    private XElement CreateManeuverParametersElement(OpmManeuverParameters maneuver)
    {
        var element = new XElement("maneuverParameters");

        foreach (var comment in maneuver.Comments)
        {
            element.Add(new XElement("COMMENT", comment));
        }

        element.Add(new XElement("MAN_EPOCH_IGNITION", FormatDateTime(maneuver.ManEpochIgnition)));
        element.Add(CreateElementWithUnits("MAN_DURATION", maneuver.ManDuration, "s"));
        element.Add(CreateElementWithUnits("MAN_DELTA_MASS", maneuver.ManDeltaMass, "kg"));
        element.Add(new XElement("MAN_REF_FRAME", maneuver.ManRefFrame));
        element.Add(CreateElementWithUnits("MAN_DV_1", maneuver.ManDv1, "km/s"));
        element.Add(CreateElementWithUnits("MAN_DV_2", maneuver.ManDv2, "km/s"));
        element.Add(CreateElementWithUnits("MAN_DV_3", maneuver.ManDv3, "km/s"));

        return element;
    }

    private XElement CreateUserDefinedParametersElement(OpmUserDefinedParameters userDefined)
    {
        var element = new XElement("userDefinedParameters");

        foreach (var comment in userDefined.Comments)
        {
            element.Add(new XElement("COMMENT", comment));
        }

        foreach (var param in userDefined.Parameters)
        {
            element.Add(new XElement("USER_DEFINED",
                new XAttribute("parameter", param.Key),
                param.Value));
        }

        return element;
    }

    #region Formatting Helpers

    private static string FormatDateTime(DateTime dateTime)
    {
        // CCSDS datetime format: yyyy-MM-ddTHH:mm:ss.ffffff
        return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffff", CultureInfo.InvariantCulture);
    }

    private static string FormatDouble(double value)
    {
        // Format with enough precision to preserve value, removing trailing zeros
        var formatted = value.ToString("G17", CultureInfo.InvariantCulture);

        // If the number is in scientific notation, keep it
        if (formatted.Contains('E') || formatted.Contains('e'))
            return formatted;

        // For regular decimal numbers, ensure reasonable precision
        return value.ToString("0.##############", CultureInfo.InvariantCulture);
    }

    private static XElement CreateElementWithUnits(string name, double value, string units)
    {
        return new XElement(name,
            new XAttribute("units", units),
            FormatDouble(value));
    }

    #endregion
}
