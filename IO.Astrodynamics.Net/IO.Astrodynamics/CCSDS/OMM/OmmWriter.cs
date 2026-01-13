// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using IO.Astrodynamics.CCSDS.Common;

namespace IO.Astrodynamics.CCSDS.OMM;

/// <summary>
/// Writes OMM (Orbit Mean-elements Message) files in CCSDS NDM/XML format.
/// </summary>
/// <remarks>
/// Generates XML conforming to CCSDS 502.0-B-3 (ODM Blue Book) and CCSDS 505.0-B-3 (NDM/XML Blue Book).
/// Supports both standalone OMM documents and OMM messages wrapped in NDM containers.
/// </remarks>
public class OmmWriter
{
    private const string CcsdsSchemaLocation = "https://sanaregistry.org/r/ndmxml_unqualified/ndmxml-3.0.0-master-3.0.xsd";
    private static readonly XNamespace XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>
    /// Gets or sets a value indicating whether to wrap the OMM in an NDM container element.
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
    /// Writes an OMM to the specified file path.
    /// </summary>
    /// <param name="omm">The OMM to write.</param>
    /// <param name="filePath">The file path to write to.</param>
    /// <exception cref="ArgumentNullException">Thrown when omm is null.</exception>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    public void WriteToFile(Omm omm, string filePath)
    {
        if (omm == null)
            throw new ArgumentNullException(nameof(omm));
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        var xml = WriteToString(omm);
        File.WriteAllText(filePath, xml, Encoding.UTF8);
    }

    /// <summary>
    /// Writes an OMM to the specified stream.
    /// </summary>
    /// <param name="omm">The OMM to write.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException">Thrown when omm or stream is null.</exception>
    public void WriteToStream(Omm omm, Stream stream)
    {
        if (omm == null)
            throw new ArgumentNullException(nameof(omm));
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        var doc = CreateDocument(omm);

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
    /// Writes an OMM to an XML string.
    /// </summary>
    /// <param name="omm">The OMM to write.</param>
    /// <returns>The XML string representation of the OMM.</returns>
    /// <exception cref="ArgumentNullException">Thrown when omm is null.</exception>
    public string WriteToString(Omm omm)
    {
        if (omm == null)
            throw new ArgumentNullException(nameof(omm));

        var doc = CreateDocument(omm);

        var settings = new XmlWriterSettings
        {
            Indent = IndentOutput,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        };

        // Use MemoryStream + StreamReader to ensure UTF-8 encoding in XML declaration
        // (StringWriter always uses UTF-16 regardless of XmlWriterSettings.Encoding)
        using var memoryStream = new MemoryStream();
        using (var writer = XmlWriter.Create(memoryStream, settings))
        {
            doc.Save(writer);
        }

        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private XDocument CreateDocument(Omm omm)
    {
        var ommElement = CreateOmmElement(omm);

        XElement root;
        if (WrapInNdmContainer)
        {
            root = new XElement("ndm",
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
                new XAttribute(XsiNamespace + "noNamespaceSchemaLocation", CcsdsSchemaLocation),
                ommElement);
        }
        else
        {
            ommElement.Add(new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace));
            ommElement.Add(new XAttribute(XsiNamespace + "noNamespaceSchemaLocation", CcsdsSchemaLocation));
            root = ommElement;
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
    }

    private XElement CreateOmmElement(Omm omm)
    {
        var ommElement = new XElement("omm",
            new XAttribute("id", Omm.FormatId),
            new XAttribute("version", Omm.Version));

        ommElement.Add(CreateHeaderElement(omm.Header));
        ommElement.Add(CreateBodyElement(omm));

        return ommElement;
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

    private XElement CreateBodyElement(Omm omm)
    {
        var bodyElement = new XElement("body");
        var segmentElement = new XElement("segment");

        segmentElement.Add(CreateMetadataElement(omm.Metadata));
        segmentElement.Add(CreateDataElement(omm.Data));

        bodyElement.Add(segmentElement);
        return bodyElement;
    }

    private XElement CreateMetadataElement(OmmMetadata metadata)
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
        metadataElement.Add(new XElement("MEAN_ELEMENT_THEORY", metadata.MeanElementTheory));

        return metadataElement;
    }

    private XElement CreateDataElement(OmmData data)
    {
        var dataElement = new XElement("data");

        foreach (var comment in data.Comments)
        {
            dataElement.Add(new XElement("COMMENT", comment));
        }

        dataElement.Add(CreateMeanElementsElement(data.MeanElements));

        if (data.HasSpacecraftParameters)
        {
            dataElement.Add(CreateSpacecraftParametersElement(data.SpacecraftParameters));
        }

        if (data.HasTleParameters)
        {
            dataElement.Add(CreateTleParametersElement(data.TleParameters));
        }

        if (data.HasCovarianceMatrix)
        {
            dataElement.Add(CreateCovarianceMatrixElement(data.CovarianceMatrix));
        }

        if (data.HasUserDefinedParameters)
        {
            dataElement.Add(CreateUserDefinedParametersElement(data.UserDefinedParameters));
        }

        return dataElement;
    }

    private XElement CreateMeanElementsElement(MeanElements meanElements)
    {
        var element = new XElement("meanElements");

        foreach (var comment in meanElements.Comments)
        {
            element.Add(new XElement("COMMENT", comment));
        }

        element.Add(new XElement("EPOCH", FormatDateTime(meanElements.Epoch)));

        if (meanElements.UsesMeanMotion)
        {
            element.Add(new XElement("MEAN_MOTION", FormatDouble(meanElements.MeanMotion.Value)));
        }
        else
        {
            element.Add(new XElement("SEMI_MAJOR_AXIS", FormatDouble(meanElements.SemiMajorAxis.Value)));
        }

        element.Add(new XElement("ECCENTRICITY", FormatDouble(meanElements.Eccentricity)));
        element.Add(new XElement("INCLINATION", FormatDouble(meanElements.Inclination)));
        element.Add(new XElement("RA_OF_ASC_NODE", FormatDouble(meanElements.RightAscensionOfAscendingNode)));
        element.Add(new XElement("ARG_OF_PERICENTER", FormatDouble(meanElements.ArgumentOfPericenter)));
        element.Add(new XElement("MEAN_ANOMALY", FormatDouble(meanElements.MeanAnomaly)));

        if (meanElements.GravitationalParameter.HasValue)
        {
            element.Add(new XElement("GM", FormatDouble(meanElements.GravitationalParameter.Value)));
        }

        return element;
    }

    private XElement CreateSpacecraftParametersElement(SpacecraftParameters spacecraftParams)
    {
        var element = new XElement("spacecraftParameters");

        foreach (var comment in spacecraftParams.Comments)
        {
            element.Add(new XElement("COMMENT", comment));
        }

        if (spacecraftParams.Mass.HasValue)
        {
            element.Add(new XElement("MASS", FormatDouble(spacecraftParams.Mass.Value)));
        }

        if (spacecraftParams.SolarRadArea.HasValue)
        {
            element.Add(new XElement("SOLAR_RAD_AREA", FormatDouble(spacecraftParams.SolarRadArea.Value)));
        }

        if (spacecraftParams.SolarRadCoeff.HasValue)
        {
            element.Add(new XElement("SOLAR_RAD_COEFF", FormatDouble(spacecraftParams.SolarRadCoeff.Value)));
        }

        if (spacecraftParams.DragArea.HasValue)
        {
            element.Add(new XElement("DRAG_AREA", FormatDouble(spacecraftParams.DragArea.Value)));
        }

        if (spacecraftParams.DragCoeff.HasValue)
        {
            element.Add(new XElement("DRAG_COEFF", FormatDouble(spacecraftParams.DragCoeff.Value)));
        }

        return element;
    }

    private XElement CreateTleParametersElement(TleParameters tleParams)
    {
        var element = new XElement("tleParameters");

        foreach (var comment in tleParams.Comments)
        {
            element.Add(new XElement("COMMENT", comment));
        }

        if (tleParams.EphemerisType.HasValue)
        {
            element.Add(new XElement("EPHEMERIS_TYPE", tleParams.EphemerisType.Value));
        }

        if (!string.IsNullOrEmpty(tleParams.ClassificationType))
        {
            element.Add(new XElement("CLASSIFICATION_TYPE", tleParams.ClassificationType));
        }

        if (tleParams.NoradCatalogId.HasValue)
        {
            element.Add(new XElement("NORAD_CAT_ID", tleParams.NoradCatalogId.Value));
        }

        if (tleParams.ElementSetNumber.HasValue)
        {
            element.Add(new XElement("ELEMENT_SET_NO", tleParams.ElementSetNumber.Value));
        }

        if (tleParams.RevolutionNumberAtEpoch.HasValue)
        {
            element.Add(new XElement("REV_AT_EPOCH", tleParams.RevolutionNumberAtEpoch.Value));
        }

        // BSTAR or BTERM
        if (tleParams.UsesBStar)
        {
            element.Add(new XElement("BSTAR", FormatDouble(tleParams.BStar.Value)));
        }
        else
        {
            element.Add(new XElement("BTERM", FormatDouble(tleParams.BTerm.Value)));
        }

        element.Add(new XElement("MEAN_MOTION_DOT", FormatDouble(tleParams.MeanMotionDot)));

        // MEAN_MOTION_DDOT or AGOM
        if (tleParams.UsesMeanMotionDDot)
        {
            element.Add(new XElement("MEAN_MOTION_DDOT", FormatDouble(tleParams.MeanMotionDDot.Value)));
        }
        else
        {
            element.Add(new XElement("AGOM", FormatDouble(tleParams.AGom.Value)));
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

        element.Add(new XElement("CX_X", FormatDouble(covariance.CxX)));
        element.Add(new XElement("CY_X", FormatDouble(covariance.CyX)));
        element.Add(new XElement("CY_Y", FormatDouble(covariance.CyY)));
        element.Add(new XElement("CZ_X", FormatDouble(covariance.CzX)));
        element.Add(new XElement("CZ_Y", FormatDouble(covariance.CzY)));
        element.Add(new XElement("CZ_Z", FormatDouble(covariance.CzZ)));
        element.Add(new XElement("CX_DOT_X", FormatDouble(covariance.CxDotX)));
        element.Add(new XElement("CX_DOT_Y", FormatDouble(covariance.CxDotY)));
        element.Add(new XElement("CX_DOT_Z", FormatDouble(covariance.CxDotZ)));
        element.Add(new XElement("CX_DOT_X_DOT", FormatDouble(covariance.CxDotXDot)));
        element.Add(new XElement("CY_DOT_X", FormatDouble(covariance.CyDotX)));
        element.Add(new XElement("CY_DOT_Y", FormatDouble(covariance.CyDotY)));
        element.Add(new XElement("CY_DOT_Z", FormatDouble(covariance.CyDotZ)));
        element.Add(new XElement("CY_DOT_X_DOT", FormatDouble(covariance.CyDotXDot)));
        element.Add(new XElement("CY_DOT_Y_DOT", FormatDouble(covariance.CyDotYDot)));
        element.Add(new XElement("CZ_DOT_X", FormatDouble(covariance.CzDotX)));
        element.Add(new XElement("CZ_DOT_Y", FormatDouble(covariance.CzDotY)));
        element.Add(new XElement("CZ_DOT_Z", FormatDouble(covariance.CzDotZ)));
        element.Add(new XElement("CZ_DOT_X_DOT", FormatDouble(covariance.CzDotXDot)));
        element.Add(new XElement("CZ_DOT_Y_DOT", FormatDouble(covariance.CzDotYDot)));
        element.Add(new XElement("CZ_DOT_Z_DOT", FormatDouble(covariance.CzDotZDot)));

        return element;
    }

    private XElement CreateUserDefinedParametersElement(UserDefinedParameters userDefined)
    {
        var element = new XElement("userDefinedParameters");

        foreach (var comment in userDefined.Comments)
        {
            element.Add(new XElement("COMMENT", comment));
        }

        foreach (var param in userDefined.Parameters)
        {
            element.Add(new XElement("USER_DEFINED",
                new XAttribute("parameter", param.Name),
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

    #endregion
}
