// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using IO.Astrodynamics.CCSDS.Common.Enums;

namespace IO.Astrodynamics.CCSDS.CDM;

public sealed class CdmWriter
{
    private static readonly XNamespace CcsdsNamespace = "urn:ccsds:schema:ndmxml";
    private static readonly XNamespace XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

    public bool IndentOutput { get; set; } = true;

    public void WriteToFile(Cdm cdm, string filePath, bool wrapInNdmContainer = true)
    {
        if (cdm == null)
        {
            throw new ArgumentNullException(nameof(cdm));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        File.WriteAllText(filePath, WriteToString(cdm, wrapInNdmContainer), Encoding.UTF8);
    }

    public void WriteToStream(Cdm cdm, Stream stream, bool wrapInNdmContainer = true)
    {
        if (cdm == null)
        {
            throw new ArgumentNullException(nameof(cdm));
        }

        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var document = CreateDocument(cdm, wrapInNdmContainer);
        var settings = new XmlWriterSettings
        {
            Indent = IndentOutput,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        };

        using var writer = XmlWriter.Create(stream, settings);
        document.Save(writer);
    }

    public string WriteToString(Cdm cdm, bool wrapInNdmContainer = true)
    {
        if (cdm == null)
        {
            throw new ArgumentNullException(nameof(cdm));
        }

        var document = CreateDocument(cdm, wrapInNdmContainer);
        var settings = new XmlWriterSettings
        {
            Indent = IndentOutput,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        };

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
        {
            document.Save(writer);
        }

        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private XDocument CreateDocument(Cdm cdm, bool wrapInNdmContainer)
    {
        var cdmElement = CreateCdmElement(cdm);
        XElement root = wrapInNdmContainer
            ? new XElement(CcsdsNamespace + "ndm",
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
                cdmElement)
            : cdmElement;

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
    }

    private XElement CreateCdmElement(Cdm cdm)
    {
        return new XElement(CcsdsNamespace + "cdm",
            new XAttribute("id", Cdm.FormatId),
            new XAttribute("version", Cdm.Version),
            CreateHeaderElement(cdm.Header),
            CreateBodyElement(cdm));
    }

    private XElement CreateHeaderElement(CdmHeader header)
    {
        var element = new XElement(CcsdsNamespace + "header");
        AddComments(element, header.Comments);
        element.Add(new XElement(CcsdsNamespace + "CREATION_DATE", FormatDateTime(header.CreationDateUtc)));
        element.Add(new XElement(CcsdsNamespace + "ORIGINATOR", header.Originator));

        if (!string.IsNullOrWhiteSpace(header.MessageFor))
        {
            element.Add(new XElement(CcsdsNamespace + "MESSAGE_FOR", header.MessageFor));
        }

        element.Add(new XElement(CcsdsNamespace + "MESSAGE_ID", header.MessageId));
        return element;
    }

    private XElement CreateBodyElement(Cdm cdm)
    {
        var body = new XElement(CcsdsNamespace + "body",
            CreateRelativeMetadataDataElement(cdm.RelativeMetadataData));

        foreach (var segment in cdm.Segments)
        {
            body.Add(CreateSegmentElement(segment));
        }

        return body;
    }

    private XElement CreateRelativeMetadataDataElement(CdmRelativeMetadataData relativeMetadataData)
    {
        var element = new XElement(CcsdsNamespace + "relativeMetadataData");
        AddComments(element, relativeMetadataData.Comments);
        element.Add(new XElement(CcsdsNamespace + "TCA", FormatDateTime(relativeMetadataData.TcaUtc)));
        element.Add(CreateLengthElement("MISS_DISTANCE", relativeMetadataData.MissDistanceMeters));

        if (relativeMetadataData.RelativeSpeedMetersPerSecond.HasValue)
        {
            element.Add(CreateDeltaVelocityElement("RELATIVE_SPEED", relativeMetadataData.RelativeSpeedMetersPerSecond.Value));
        }

        if (relativeMetadataData.RelativeStateVector != null)
        {
            element.Add(CreateRelativeStateVectorElement(relativeMetadataData.RelativeStateVector));
        }

        AddOptionalDateElement(element, "START_SCREEN_PERIOD", relativeMetadataData.StartScreenPeriodUtc);
        AddOptionalDateElement(element, "STOP_SCREEN_PERIOD", relativeMetadataData.StopScreenPeriodUtc);
        AddOptionalEnumElement(element, "SCREEN_VOLUME_FRAME", relativeMetadataData.ScreenVolumeFrame);
        AddOptionalEnumElement(element, "SCREEN_VOLUME_SHAPE", relativeMetadataData.ScreenVolumeShape);
        AddOptionalLengthElement(element, "SCREEN_VOLUME_X", relativeMetadataData.ScreenVolumeXMeters);
        AddOptionalLengthElement(element, "SCREEN_VOLUME_Y", relativeMetadataData.ScreenVolumeYMeters);
        AddOptionalLengthElement(element, "SCREEN_VOLUME_Z", relativeMetadataData.ScreenVolumeZMeters);
        AddOptionalDateElement(element, "SCREEN_ENTRY_TIME", relativeMetadataData.ScreenEntryTimeUtc);
        AddOptionalDateElement(element, "SCREEN_EXIT_TIME", relativeMetadataData.ScreenExitTimeUtc);

        if (relativeMetadataData.CollisionProbability.HasValue)
        {
            element.Add(new XElement(CcsdsNamespace + "COLLISION_PROBABILITY", FormatDouble(relativeMetadataData.CollisionProbability.Value)));
        }

        if (!string.IsNullOrWhiteSpace(relativeMetadataData.CollisionProbabilityMethod))
        {
            element.Add(new XElement(CcsdsNamespace + "COLLISION_PROBABILITY_METHOD", relativeMetadataData.CollisionProbabilityMethod));
        }

        return element;
    }

    private XElement CreateRelativeStateVectorElement(CdmRelativeStateVector stateVector)
    {
        return new XElement(CcsdsNamespace + "relativeStateVector",
            CreateLengthElement("RELATIVE_POSITION_R", stateVector.RelativePositionRtnMeters.X),
            CreateLengthElement("RELATIVE_POSITION_T", stateVector.RelativePositionRtnMeters.Y),
            CreateLengthElement("RELATIVE_POSITION_N", stateVector.RelativePositionRtnMeters.Z),
            CreateDeltaVelocityElement("RELATIVE_VELOCITY_R", stateVector.RelativeVelocityRtnMetersPerSecond.X),
            CreateDeltaVelocityElement("RELATIVE_VELOCITY_T", stateVector.RelativeVelocityRtnMetersPerSecond.Y),
            CreateDeltaVelocityElement("RELATIVE_VELOCITY_N", stateVector.RelativeVelocityRtnMetersPerSecond.Z));
    }

    private XElement CreateSegmentElement(CdmSegment segment)
    {
        if (segment.Metadata is null) throw new ArgumentNullException(nameof(segment), "Segment metadata is required.");
        if (segment.Data is null) throw new ArgumentNullException(nameof(segment), "Segment data is required.");
        return new XElement(CcsdsNamespace + "segment",
            CreateParticipantMetadataElement(segment.Metadata),
            CreateDataElement(segment.Data));
    }

    private XElement CreateParticipantMetadataElement(CdmParticipantMetadata metadata)
    {
        var element = new XElement(CcsdsNamespace + "metadata");
        AddComments(element, metadata.Comments);
        element.Add(new XElement(CcsdsNamespace + "OBJECT", metadata.Object.ToSchemaValue()));
        element.Add(new XElement(CcsdsNamespace + "OBJECT_DESIGNATOR", metadata.ObjectDesignator));
        element.Add(new XElement(CcsdsNamespace + "CATALOG_NAME", metadata.CatalogName));
        element.Add(new XElement(CcsdsNamespace + "OBJECT_NAME", metadata.ObjectName));
        element.Add(new XElement(CcsdsNamespace + "INTERNATIONAL_DESIGNATOR", metadata.InternationalDesignator));

        if (metadata.ObjectType.HasValue)
        {
            element.Add(new XElement(CcsdsNamespace + "OBJECT_TYPE", FormatObjectType(metadata.ObjectType.Value)));
        }

        AddOptionalStringElement(element, "OPERATOR_CONTACT_POSITION", metadata.OperatorContactPosition);
        AddOptionalStringElement(element, "OPERATOR_ORGANIZATION", metadata.OperatorOrganization);
        AddOptionalStringElement(element, "OPERATOR_PHONE", metadata.OperatorPhone);
        AddOptionalStringElement(element, "OPERATOR_EMAIL", metadata.OperatorEmail);
        element.Add(new XElement(CcsdsNamespace + "EPHEMERIS_NAME", metadata.EphemerisName));
        element.Add(new XElement(CcsdsNamespace + "COVARIANCE_METHOD", metadata.CovarianceMethod!.Value.ToSchemaValue()));
        element.Add(new XElement(CcsdsNamespace + "MANEUVERABLE", metadata.Maneuverable!.Value.ToSchemaValue()));
        AddOptionalStringElement(element, "ORBIT_CENTER", metadata.OrbitCenter);
        element.Add(new XElement(CcsdsNamespace + "REF_FRAME", metadata.ReferenceFrame!.Value.ToSchemaValue()));
        AddOptionalStringElement(element, "GRAVITY_MODEL", metadata.GravityModel);
        AddOptionalStringElement(element, "ATMOSPHERIC_MODEL", metadata.AtmosphericModel);
        AddOptionalStringElement(element, "N_BODY_PERTURBATIONS", metadata.NBodyPerturbations);
        AddOptionalYesNoElement(element, "SOLAR_RAD_PRESSURE", metadata.SolarRadPressure);
        AddOptionalYesNoElement(element, "EARTH_TIDES", metadata.EarthTides);
        AddOptionalYesNoElement(element, "INTRACK_THRUST", metadata.IntrackThrust);
        return element;
    }

    private XElement CreateDataElement(CdmData data)
    {
        var element = new XElement(CcsdsNamespace + "data");
        AddComments(element, data.Comments);

        if (data.OdParameters != null)
        {
            element.Add(CreateOdParametersElement(data.OdParameters));
        }

        if (data.AdditionalParameters != null)
        {
            element.Add(CreateAdditionalParametersElement(data.AdditionalParameters));
        }

        if (data.StateVector is null) throw new ArgumentNullException(nameof(data), "State vector is required.");
        if (data.CovarianceMatrix is null) throw new ArgumentNullException(nameof(data), "Covariance matrix is required.");
        element.Add(CreateStateVectorElement(data.StateVector));
        element.Add(CreateCovarianceMatrixElement(data.CovarianceMatrix));
        return element;
    }

    private XElement CreateOdParametersElement(CdmOdParameters odParameters)
    {
        var element = new XElement(CcsdsNamespace + "odParameters");
        AddComments(element, odParameters.Comments);
        AddOptionalDateElement(element, "TIME_LASTOB_START", odParameters.TimeLastObservationStartUtc);
        AddOptionalDateElement(element, "TIME_LASTOB_END", odParameters.TimeLastObservationEndUtc);
        AddOptionalDoubleElement(element, "RECOMMENDED_OD_SPAN", odParameters.RecommendedOdSpanDays);
        AddOptionalDoubleElement(element, "ACTUAL_OD_SPAN", odParameters.ActualOdSpanDays);
        AddOptionalIntElement(element, "OBS_AVAILABLE", odParameters.ObservationsAvailable);
        AddOptionalIntElement(element, "OBS_USED", odParameters.ObservationsUsed);
        AddOptionalIntElement(element, "TRACKS_AVAILABLE", odParameters.TracksAvailable);
        AddOptionalIntElement(element, "TRACKS_USED", odParameters.TracksUsed);
        AddOptionalDoubleElement(element, "RESIDUALS_ACCEPTED", odParameters.ResidualsAcceptedPercent);
        AddOptionalDoubleElement(element, "WEIGHTED_RMS", odParameters.WeightedRms);
        return element;
    }

    private XElement CreateAdditionalParametersElement(CdmAdditionalParameters additionalParameters)
    {
        var element = new XElement(CcsdsNamespace + "additionalParameters");
        AddComments(element, additionalParameters.Comments);
        AddOptionalAreaElement(element, "AREA_PC", additionalParameters.AreaPcSquareMeters);
        AddOptionalAreaElement(element, "AREA_DRG", additionalParameters.AreaDragSquareMeters);
        AddOptionalAreaElement(element, "AREA_SRP", additionalParameters.AreaSrpSquareMeters);
        AddOptionalMassElement(element, "MASS", additionalParameters.MassKilograms);
        AddOptionalDoubleElement(element, "CD_AREA_OVER_MASS", additionalParameters.CdAreaOverMass, "m**2/kg");
        AddOptionalDoubleElement(element, "CR_AREA_OVER_MASS", additionalParameters.CrAreaOverMass, "m**2/kg");
        AddOptionalDoubleElement(element, "THRUST_ACCELERATION", additionalParameters.ThrustAccelerationMetersPerSecondSquared, "m/s**2");
        AddOptionalDoubleElement(element, "SEDR", additionalParameters.SedrWattsPerKilogram, "W/kg");
        return element;
    }

    private XElement CreateStateVectorElement(CdmStateVector stateVector)
    {
        var element = new XElement(CcsdsNamespace + "stateVector");
        AddComments(element, stateVector.Comments);
        element.Add(CreatePositionElement("X", stateVector.PositionMeters.X));
        element.Add(CreatePositionElement("Y", stateVector.PositionMeters.Y));
        element.Add(CreatePositionElement("Z", stateVector.PositionMeters.Z));
        element.Add(CreateVelocityElement("X_DOT", stateVector.VelocityMetersPerSecond.X));
        element.Add(CreateVelocityElement("Y_DOT", stateVector.VelocityMetersPerSecond.Y));
        element.Add(CreateVelocityElement("Z_DOT", stateVector.VelocityMetersPerSecond.Z));
        return element;
    }

    private XElement CreateCovarianceMatrixElement(CdmCovarianceMatrix covarianceMatrix)
    {
        var element = new XElement(CcsdsNamespace + "covarianceMatrix");
        AddComments(element, covarianceMatrix.Comments);
        var matrix = covarianceMatrix.StateCovarianceRtn;

        element.Add(CreateMatrixElement("CR_R", matrix.Get(0, 0), "m**2"));
        element.Add(CreateMatrixElement("CT_R", matrix.Get(1, 0), "m**2"));
        element.Add(CreateMatrixElement("CT_T", matrix.Get(1, 1), "m**2"));
        element.Add(CreateMatrixElement("CN_R", matrix.Get(2, 0), "m**2"));
        element.Add(CreateMatrixElement("CN_T", matrix.Get(2, 1), "m**2"));
        element.Add(CreateMatrixElement("CN_N", matrix.Get(2, 2), "m**2"));
        element.Add(CreateMatrixElement("CRDOT_R", matrix.Get(3, 0), "m**2/s"));
        element.Add(CreateMatrixElement("CRDOT_T", matrix.Get(3, 1), "m**2/s"));
        element.Add(CreateMatrixElement("CRDOT_N", matrix.Get(3, 2), "m**2/s"));
        element.Add(CreateMatrixElement("CRDOT_RDOT", matrix.Get(3, 3), "m**2/s**2"));
        element.Add(CreateMatrixElement("CTDOT_R", matrix.Get(4, 0), "m**2/s"));
        element.Add(CreateMatrixElement("CTDOT_T", matrix.Get(4, 1), "m**2/s"));
        element.Add(CreateMatrixElement("CTDOT_N", matrix.Get(4, 2), "m**2/s"));
        element.Add(CreateMatrixElement("CTDOT_RDOT", matrix.Get(4, 3), "m**2/s**2"));
        element.Add(CreateMatrixElement("CTDOT_TDOT", matrix.Get(4, 4), "m**2/s**2"));
        element.Add(CreateMatrixElement("CNDOT_R", matrix.Get(5, 0), "m**2/s"));
        element.Add(CreateMatrixElement("CNDOT_T", matrix.Get(5, 1), "m**2/s"));
        element.Add(CreateMatrixElement("CNDOT_N", matrix.Get(5, 2), "m**2/s"));
        element.Add(CreateMatrixElement("CNDOT_RDOT", matrix.Get(5, 3), "m**2/s**2"));
        element.Add(CreateMatrixElement("CNDOT_TDOT", matrix.Get(5, 4), "m**2/s**2"));
        element.Add(CreateMatrixElement("CNDOT_NDOT", matrix.Get(5, 5), "m**2/s**2"));

        AddOptionalMatrixElement(element, "CDRG_R", covarianceMatrix.CdrgR, "m**3/kg");
        AddOptionalMatrixElement(element, "CDRG_T", covarianceMatrix.CdrgT, "m**3/kg");
        AddOptionalMatrixElement(element, "CDRG_N", covarianceMatrix.CdrgN, "m**3/kg");
        AddOptionalMatrixElement(element, "CDRG_RDOT", covarianceMatrix.CdrgRdot, "m**3/(kg*s)");
        AddOptionalMatrixElement(element, "CDRG_TDOT", covarianceMatrix.CdrgTdot, "m**3/(kg*s)");
        AddOptionalMatrixElement(element, "CDRG_NDOT", covarianceMatrix.CdrgNdot, "m**3/(kg*s)");
        AddOptionalMatrixElement(element, "CDRG_DRG", covarianceMatrix.CdrgDrg, "m**4/kg**2");
        AddOptionalMatrixElement(element, "CSRP_R", covarianceMatrix.CsrpR, "m**3/kg");
        AddOptionalMatrixElement(element, "CSRP_T", covarianceMatrix.CsrpT, "m**3/kg");
        AddOptionalMatrixElement(element, "CSRP_N", covarianceMatrix.CsrpN, "m**3/kg");
        AddOptionalMatrixElement(element, "CSRP_RDOT", covarianceMatrix.CsrpRdot, "m**3/(kg*s)");
        AddOptionalMatrixElement(element, "CSRP_TDOT", covarianceMatrix.CsrpTdot, "m**3/(kg*s)");
        AddOptionalMatrixElement(element, "CSRP_NDOT", covarianceMatrix.CsrpNdot, "m**3/(kg*s)");
        AddOptionalMatrixElement(element, "CSRP_DRG", covarianceMatrix.CsrpDrg, "m**4/kg**2");
        AddOptionalMatrixElement(element, "CSRP_SRP", covarianceMatrix.CsrpSrp, "m**4/kg**2");
        AddOptionalMatrixElement(element, "CTHR_R", covarianceMatrix.CthrR, "m**2/s**2");
        AddOptionalMatrixElement(element, "CTHR_T", covarianceMatrix.CthrT, "m**2/s**2");
        AddOptionalMatrixElement(element, "CTHR_N", covarianceMatrix.CthrN, "m**2/s**2");
        AddOptionalMatrixElement(element, "CTHR_RDOT", covarianceMatrix.CthrRdot, "m**2/s**3");
        AddOptionalMatrixElement(element, "CTHR_TDOT", covarianceMatrix.CthrTdot, "m**2/s**3");
        AddOptionalMatrixElement(element, "CTHR_NDOT", covarianceMatrix.CthrNdot, "m**2/s**3");
        AddOptionalMatrixElement(element, "CTHR_DRG", covarianceMatrix.CthrDrg, "m**3/(kg*s**2)");
        AddOptionalMatrixElement(element, "CTHR_SRP", covarianceMatrix.CthrSrp, "m**3/(kg*s**2)");
        AddOptionalMatrixElement(element, "CTHR_THR", covarianceMatrix.CthrThr, "m**2/s**4");
        return element;
    }

    private static void AddComments(XElement element, IReadOnlyList<string> comments)
    {
        foreach (var comment in comments)
        {
            element.Add(new XElement(CcsdsNamespace + "COMMENT", comment));
        }
    }

    private static void AddOptionalStringElement(XElement element, string localName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            element.Add(new XElement(CcsdsNamespace + localName, value));
        }
    }

    private static void AddOptionalDateElement(XElement element, string localName, DateTime? value)
    {
        if (value.HasValue)
        {
            element.Add(new XElement(CcsdsNamespace + localName, FormatDateTime(value.Value)));
        }
    }

    private static void AddOptionalIntElement(XElement element, string localName, int? value)
    {
        if (value.HasValue)
        {
            element.Add(new XElement(CcsdsNamespace + localName, value.Value.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static void AddOptionalLengthElement(XElement element, string localName, double? valueMeters)
    {
        if (valueMeters.HasValue)
        {
            element.Add(CreateLengthElement(localName, valueMeters.Value));
        }
    }

    private static void AddOptionalAreaElement(XElement element, string localName, double? value)
    {
        AddOptionalDoubleElement(element, localName, value, "m**2");
    }

    private static void AddOptionalMassElement(XElement element, string localName, double? value)
    {
        AddOptionalDoubleElement(element, localName, value, "kg");
    }

    private static void AddOptionalYesNoElement(XElement element, string localName, bool? value)
    {
        if (value.HasValue)
        {
            element.Add(new XElement(CcsdsNamespace + localName, CdmEnumExtensions.ToYesNoString(value.Value)));
        }
    }

    private static void AddOptionalEnumElement(XElement element, string localName, CdmScreenVolumeFrame? value)
    {
        if (value.HasValue)
        {
            element.Add(new XElement(CcsdsNamespace + localName, value.Value.ToSchemaValue()));
        }
    }

    private static void AddOptionalEnumElement(XElement element, string localName, CdmScreenVolumeShape? value)
    {
        if (value.HasValue)
        {
            element.Add(new XElement(CcsdsNamespace + localName, value.Value.ToSchemaValue()));
        }
    }

    private static void AddOptionalDoubleElement(XElement element, string localName, double? value, string? units = null)
    {
        if (!value.HasValue)
        {
            return;
        }

        var child = new XElement(CcsdsNamespace + localName, FormatDouble(value.Value));
        if (!string.IsNullOrWhiteSpace(units))
        {
            child.Add(new XAttribute("units", units));
        }

        element.Add(child);
    }

    private static XElement CreatePositionElement(string localName, double valueMeters)
    {
        return new XElement(CcsdsNamespace + localName,
            new XAttribute("units", "km"),
            FormatDouble(valueMeters / 1000.0));
    }

    private static XElement CreateVelocityElement(string localName, double valueMetersPerSecond)
    {
        return new XElement(CcsdsNamespace + localName,
            new XAttribute("units", "km/s"),
            FormatDouble(valueMetersPerSecond / 1000.0));
    }

    private static XElement CreateLengthElement(string localName, double valueMeters)
    {
        return new XElement(CcsdsNamespace + localName,
            new XAttribute("units", "m"),
            FormatDouble(valueMeters));
    }

    private static XElement CreateDeltaVelocityElement(string localName, double valueMetersPerSecond)
    {
        return new XElement(CcsdsNamespace + localName,
            new XAttribute("units", "m/s"),
            FormatDouble(valueMetersPerSecond));
    }

    private static XElement CreateMatrixElement(string localName, double value, string units)
    {
        return new XElement(CcsdsNamespace + localName,
            new XAttribute("units", units),
            FormatDouble(value));
    }

    private static void AddOptionalMatrixElement(XElement element, string localName, double? value, string units)
    {
        if (value.HasValue)
        {
            element.Add(CreateMatrixElement(localName, value.Value, units));
        }
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("G17", CultureInfo.InvariantCulture);
    }

    private static string FormatObjectType(ObjectType objectType)
    {
        return objectType switch
        {
            ObjectType.Payload => "PAYLOAD",
            ObjectType.RocketBody => "ROCKET BODY",
            ObjectType.Debris => "DEBRIS",
            ObjectType.Unknown => "UNKNOWN",
            ObjectType.Other => "OTHER",
            _ => throw new ArgumentOutOfRangeException(nameof(objectType), objectType, null)
        };
    }
}
