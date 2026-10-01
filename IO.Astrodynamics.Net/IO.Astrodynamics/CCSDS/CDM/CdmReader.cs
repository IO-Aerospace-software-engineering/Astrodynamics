// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using IO.Astrodynamics.CCSDS.Common.Enums;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.CCSDS.CDM;

public sealed class CdmReader
{
    public Cdm ReadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"CDM file not found: {filePath}", filePath);
        }

        using var stream = File.OpenRead(filePath);
        return ReadFromStream(stream);
    }

    public Cdm ReadFromStream(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        try
        {
            var document = XDocument.Load(stream);
            return ParseDocument(document);
        }
        catch (XmlException ex)
        {
            throw new CdmParseException("Failed to parse CDM XML.", ex);
        }
    }

    public Cdm ReadFromString(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new ArgumentException("XML string cannot be null or empty.", nameof(xml));
        }

        try
        {
            var document = XDocument.Parse(xml);
            return ParseDocument(document);
        }
        catch (XmlException ex)
        {
            throw new CdmParseException("Failed to parse CDM XML.", ex);
        }
    }

    private Cdm ParseDocument(XDocument document)
    {
        var root = document.Root ?? throw new CdmParseException("XML document has no root element.");
        var cdmElement = FindCdmElement(root) ?? throw new CdmParseException("Could not find CDM element in document.");

        var header = ParseHeader(GetRequiredElement(cdmElement, "header"));
        var body = GetRequiredElement(cdmElement, "body");
        var relativeMetadataData = ParseRelativeMetadataData(GetRequiredElement(body, "relativeMetadataData"));
        var segments = body.Elements()
            .Where(element => element.Name.LocalName.Equals("segment", StringComparison.OrdinalIgnoreCase))
            .Select(ParseSegment)
            .ToArray();

        return new Cdm(header, relativeMetadataData, segments);
    }

    private XElement? FindCdmElement(XElement root)
    {
        var localName = root.Name.LocalName.ToUpperInvariant();
        if (localName == "CDM")
        {
            return root;
        }

        if (localName == "NDM")
        {
            return root.Elements()
                .FirstOrDefault(element => element.Name.LocalName.Equals("cdm", StringComparison.OrdinalIgnoreCase));
        }

        return root.Descendants()
            .FirstOrDefault(element => element.Name.LocalName.Equals("cdm", StringComparison.OrdinalIgnoreCase));
    }

    private CdmHeader ParseHeader(XElement element)
    {
        return new CdmHeader
        {
            Comments = GetComments(element),
            CreationDateUtc = ParseDateTime(GetRequiredElementValue(element, "CREATION_DATE"), "CREATION_DATE"),
            Originator = GetRequiredElementValue(element, "ORIGINATOR"),
            MessageFor = GetOptionalElementValue(element, "MESSAGE_FOR"),
            MessageId = GetRequiredElementValue(element, "MESSAGE_ID")
        };
    }

    private CdmRelativeMetadataData ParseRelativeMetadataData(XElement element)
    {
        var relativeStateVectorElement = GetOptionalElement(element, "relativeStateVector");

        return new CdmRelativeMetadataData
        {
            Comments = GetComments(element),
            TcaUtc = ParseDateTime(GetRequiredElementValue(element, "TCA"), "TCA"),
            MissDistanceMeters = ParseLengthToMeters(GetRequiredElement(element, "MISS_DISTANCE")),
            RelativeSpeedMetersPerSecond = ParseOptionalDeltaVelocity(element, "RELATIVE_SPEED"),
            RelativeStateVector = relativeStateVectorElement != null ? ParseRelativeStateVector(relativeStateVectorElement) : null,
            StartScreenPeriodUtc = ParseOptionalDateTime(element, "START_SCREEN_PERIOD"),
            StopScreenPeriodUtc = ParseOptionalDateTime(element, "STOP_SCREEN_PERIOD"),
            ScreenVolumeFrame = ParseOptionalEnum(element, "SCREEN_VOLUME_FRAME", CdmEnumExtensions.ParseScreenVolumeFrame),
            ScreenVolumeShape = ParseOptionalEnum(element, "SCREEN_VOLUME_SHAPE", CdmEnumExtensions.ParseScreenVolumeShape),
            ScreenVolumeXMeters = ParseOptionalLength(element, "SCREEN_VOLUME_X"),
            ScreenVolumeYMeters = ParseOptionalLength(element, "SCREEN_VOLUME_Y"),
            ScreenVolumeZMeters = ParseOptionalLength(element, "SCREEN_VOLUME_Z"),
            ScreenEntryTimeUtc = ParseOptionalDateTime(element, "SCREEN_ENTRY_TIME"),
            ScreenExitTimeUtc = ParseOptionalDateTime(element, "SCREEN_EXIT_TIME"),
            CollisionProbability = ParseOptionalDouble(element, "COLLISION_PROBABILITY"),
            CollisionProbabilityMethod = GetOptionalElementValue(element, "COLLISION_PROBABILITY_METHOD")
        };
    }

    private CdmRelativeStateVector ParseRelativeStateVector(XElement element)
    {
        return new CdmRelativeStateVector
        {
            RelativePositionRtnMeters = new Vector3(
                ParseLengthToMeters(GetRequiredElement(element, "RELATIVE_POSITION_R")),
                ParseLengthToMeters(GetRequiredElement(element, "RELATIVE_POSITION_T")),
                ParseLengthToMeters(GetRequiredElement(element, "RELATIVE_POSITION_N"))),
            RelativeVelocityRtnMetersPerSecond = new Vector3(
                ParseDeltaVelocityToMetersPerSecond(GetRequiredElement(element, "RELATIVE_VELOCITY_R")),
                ParseDeltaVelocityToMetersPerSecond(GetRequiredElement(element, "RELATIVE_VELOCITY_T")),
                ParseDeltaVelocityToMetersPerSecond(GetRequiredElement(element, "RELATIVE_VELOCITY_N")))
        };
    }

    private CdmSegment ParseSegment(XElement element)
    {
        return new CdmSegment
        {
            Metadata = ParseParticipantMetadata(GetRequiredElement(element, "metadata")),
            Data = ParseData(GetRequiredElement(element, "data"))
        };
    }

    private CdmParticipantMetadata ParseParticipantMetadata(XElement element)
    {
        return new CdmParticipantMetadata
        {
            Comments = GetComments(element),
            Object = CdmEnumExtensions.ParseObjectRole(GetRequiredElementValue(element, "OBJECT")),
            ObjectDesignator = GetRequiredElementValue(element, "OBJECT_DESIGNATOR"),
            CatalogName = GetRequiredElementValue(element, "CATALOG_NAME"),
            ObjectName = GetRequiredElementValue(element, "OBJECT_NAME"),
            InternationalDesignator = GetRequiredElementValue(element, "INTERNATIONAL_DESIGNATOR"),
            ObjectType = ParseOptionalObjectType(element, "OBJECT_TYPE"),
            OperatorContactPosition = GetOptionalElementValue(element, "OPERATOR_CONTACT_POSITION"),
            OperatorOrganization = GetOptionalElementValue(element, "OPERATOR_ORGANIZATION"),
            OperatorPhone = GetOptionalElementValue(element, "OPERATOR_PHONE"),
            OperatorEmail = GetOptionalElementValue(element, "OPERATOR_EMAIL"),
            EphemerisName = GetRequiredElementValue(element, "EPHEMERIS_NAME"),
            CovarianceMethod = ParseRequiredEnum(element, "COVARIANCE_METHOD", CdmEnumExtensions.ParseCovarianceMethod),
            Maneuverable = ParseRequiredEnum(element, "MANEUVERABLE", CdmEnumExtensions.ParseManeuverable),
            OrbitCenter = GetOptionalElementValue(element, "ORBIT_CENTER"),
            ReferenceFrame = ParseRequiredEnum(element, "REF_FRAME", CdmEnumExtensions.ParseReferenceFrame),
            GravityModel = GetOptionalElementValue(element, "GRAVITY_MODEL"),
            AtmosphericModel = GetOptionalElementValue(element, "ATMOSPHERIC_MODEL"),
            NBodyPerturbations = GetOptionalElementValue(element, "N_BODY_PERTURBATIONS"),
            SolarRadPressure = ParseOptionalYesNo(element, "SOLAR_RAD_PRESSURE"),
            EarthTides = ParseOptionalYesNo(element, "EARTH_TIDES"),
            IntrackThrust = ParseOptionalYesNo(element, "INTRACK_THRUST")
        };
    }

    private CdmData ParseData(XElement element)
    {
        var odParametersElement = GetOptionalElement(element, "odParameters");
        var additionalParametersElement = GetOptionalElement(element, "additionalParameters");

        return new CdmData
        {
            Comments = GetComments(element),
            OdParameters = odParametersElement != null ? ParseOdParameters(odParametersElement) : null,
            AdditionalParameters = additionalParametersElement != null ? ParseAdditionalParameters(additionalParametersElement) : null,
            StateVector = ParseStateVector(GetRequiredElement(element, "stateVector")),
            CovarianceMatrix = ParseCovarianceMatrix(GetRequiredElement(element, "covarianceMatrix"))
        };
    }

    private CdmOdParameters ParseOdParameters(XElement element)
    {
        return new CdmOdParameters
        {
            Comments = GetComments(element),
            TimeLastObservationStartUtc = ParseOptionalDateTime(element, "TIME_LASTOB_START"),
            TimeLastObservationEndUtc = ParseOptionalDateTime(element, "TIME_LASTOB_END"),
            RecommendedOdSpanDays = ParseOptionalDouble(element, "RECOMMENDED_OD_SPAN"),
            ActualOdSpanDays = ParseOptionalDouble(element, "ACTUAL_OD_SPAN"),
            ObservationsAvailable = ParseOptionalInt(element, "OBS_AVAILABLE"),
            ObservationsUsed = ParseOptionalInt(element, "OBS_USED"),
            TracksAvailable = ParseOptionalInt(element, "TRACKS_AVAILABLE"),
            TracksUsed = ParseOptionalInt(element, "TRACKS_USED"),
            ResidualsAcceptedPercent = ParseOptionalDouble(element, "RESIDUALS_ACCEPTED"),
            WeightedRms = ParseOptionalDouble(element, "WEIGHTED_RMS")
        };
    }

    private CdmAdditionalParameters ParseAdditionalParameters(XElement element)
    {
        return new CdmAdditionalParameters
        {
            Comments = GetComments(element),
            AreaPcSquareMeters = ParseOptionalDouble(element, "AREA_PC"),
            AreaDragSquareMeters = ParseOptionalDouble(element, "AREA_DRG"),
            AreaSrpSquareMeters = ParseOptionalDouble(element, "AREA_SRP"),
            MassKilograms = ParseOptionalDouble(element, "MASS"),
            CdAreaOverMass = ParseOptionalDouble(element, "CD_AREA_OVER_MASS"),
            CrAreaOverMass = ParseOptionalDouble(element, "CR_AREA_OVER_MASS"),
            ThrustAccelerationMetersPerSecondSquared = ParseOptionalDouble(element, "THRUST_ACCELERATION"),
            SedrWattsPerKilogram = ParseOptionalDouble(element, "SEDR")
        };
    }

    private CdmStateVector ParseStateVector(XElement element)
    {
        return new CdmStateVector
        {
            Comments = GetComments(element),
            PositionMeters = new Vector3(
                ParsePositionToMeters(GetRequiredElement(element, "X")),
                ParsePositionToMeters(GetRequiredElement(element, "Y")),
                ParsePositionToMeters(GetRequiredElement(element, "Z"))),
            VelocityMetersPerSecond = new Vector3(
                ParseVelocityToMetersPerSecond(GetRequiredElement(element, "X_DOT")),
                ParseVelocityToMetersPerSecond(GetRequiredElement(element, "Y_DOT")),
                ParseVelocityToMetersPerSecond(GetRequiredElement(element, "Z_DOT")))
        };
    }

    private CdmCovarianceMatrix ParseCovarianceMatrix(XElement element)
    {
        var matrix = new Matrix(6, 6);
        SetSymmetric(matrix, 0, 0, ParseRequiredDouble(element, "CR_R"));
        SetSymmetric(matrix, 1, 0, ParseRequiredDouble(element, "CT_R"));
        SetSymmetric(matrix, 1, 1, ParseRequiredDouble(element, "CT_T"));
        SetSymmetric(matrix, 2, 0, ParseRequiredDouble(element, "CN_R"));
        SetSymmetric(matrix, 2, 1, ParseRequiredDouble(element, "CN_T"));
        SetSymmetric(matrix, 2, 2, ParseRequiredDouble(element, "CN_N"));
        SetSymmetric(matrix, 3, 0, ParseRequiredDouble(element, "CRDOT_R"));
        SetSymmetric(matrix, 3, 1, ParseRequiredDouble(element, "CRDOT_T"));
        SetSymmetric(matrix, 3, 2, ParseRequiredDouble(element, "CRDOT_N"));
        SetSymmetric(matrix, 3, 3, ParseRequiredDouble(element, "CRDOT_RDOT"));
        SetSymmetric(matrix, 4, 0, ParseRequiredDouble(element, "CTDOT_R"));
        SetSymmetric(matrix, 4, 1, ParseRequiredDouble(element, "CTDOT_T"));
        SetSymmetric(matrix, 4, 2, ParseRequiredDouble(element, "CTDOT_N"));
        SetSymmetric(matrix, 4, 3, ParseRequiredDouble(element, "CTDOT_RDOT"));
        SetSymmetric(matrix, 4, 4, ParseRequiredDouble(element, "CTDOT_TDOT"));
        SetSymmetric(matrix, 5, 0, ParseRequiredDouble(element, "CNDOT_R"));
        SetSymmetric(matrix, 5, 1, ParseRequiredDouble(element, "CNDOT_T"));
        SetSymmetric(matrix, 5, 2, ParseRequiredDouble(element, "CNDOT_N"));
        SetSymmetric(matrix, 5, 3, ParseRequiredDouble(element, "CNDOT_RDOT"));
        SetSymmetric(matrix, 5, 4, ParseRequiredDouble(element, "CNDOT_TDOT"));
        SetSymmetric(matrix, 5, 5, ParseRequiredDouble(element, "CNDOT_NDOT"));

        return new CdmCovarianceMatrix
        {
            Comments = GetComments(element),
            StateCovarianceRtn = matrix,
            CdrgR = ParseOptionalDouble(element, "CDRG_R"),
            CdrgT = ParseOptionalDouble(element, "CDRG_T"),
            CdrgN = ParseOptionalDouble(element, "CDRG_N"),
            CdrgRdot = ParseOptionalDouble(element, "CDRG_RDOT"),
            CdrgTdot = ParseOptionalDouble(element, "CDRG_TDOT"),
            CdrgNdot = ParseOptionalDouble(element, "CDRG_NDOT"),
            CdrgDrg = ParseOptionalDouble(element, "CDRG_DRG"),
            CsrpR = ParseOptionalDouble(element, "CSRP_R"),
            CsrpT = ParseOptionalDouble(element, "CSRP_T"),
            CsrpN = ParseOptionalDouble(element, "CSRP_N"),
            CsrpRdot = ParseOptionalDouble(element, "CSRP_RDOT"),
            CsrpTdot = ParseOptionalDouble(element, "CSRP_TDOT"),
            CsrpNdot = ParseOptionalDouble(element, "CSRP_NDOT"),
            CsrpDrg = ParseOptionalDouble(element, "CSRP_DRG"),
            CsrpSrp = ParseOptionalDouble(element, "CSRP_SRP"),
            CthrR = ParseOptionalDouble(element, "CTHR_R"),
            CthrT = ParseOptionalDouble(element, "CTHR_T"),
            CthrN = ParseOptionalDouble(element, "CTHR_N"),
            CthrRdot = ParseOptionalDouble(element, "CTHR_RDOT"),
            CthrTdot = ParseOptionalDouble(element, "CTHR_TDOT"),
            CthrNdot = ParseOptionalDouble(element, "CTHR_NDOT"),
            CthrDrg = ParseOptionalDouble(element, "CTHR_DRG"),
            CthrSrp = ParseOptionalDouble(element, "CTHR_SRP"),
            CthrThr = ParseOptionalDouble(element, "CTHR_THR")
        };
    }

    private static void SetSymmetric(Matrix matrix, int row, int column, double value)
    {
        matrix.Set(row, column, value);
        matrix.Set(column, row, value);
    }

    private static IReadOnlyList<string> GetComments(XElement element)
    {
        return element.Elements()
            .Where(child => child.Name.LocalName.Equals("COMMENT", StringComparison.OrdinalIgnoreCase))
            .Select(child => child.Value)
            .ToArray();
    }

    private static XElement GetRequiredElement(XElement parent, string localName)
    {
        return GetOptionalElement(parent, localName) ??
               throw new CdmParseException($"Required element '{localName}' was not found.");
    }

    private static XElement? GetOptionalElement(XElement parent, string localName)
    {
        return parent.Elements()
            .FirstOrDefault(element => element.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetRequiredElementValue(XElement parent, string localName)
    {
        return GetRequiredElement(parent, localName).Value;
    }

    private static string? GetOptionalElementValue(XElement parent, string localName)
    {
        return GetOptionalElement(parent, localName)?.Value;
    }

    private static T ParseRequiredEnum<T>(XElement parent, string localName, Func<string, T> parser)
    {
        return parser(GetRequiredElementValue(parent, localName));
    }

    private static T? ParseOptionalEnum<T>(XElement parent, string localName, Func<string, T> parser)
        where T : struct
    {
        var value = GetOptionalElementValue(parent, localName);
        return value == null ? null : parser(value);
    }

    private static readonly string[] DateTimeFormats =
    [
        "yyyy-MM-ddTHH:mm:ss.fffffffZ",
        "yyyy-MM-ddTHH:mm:ss.ffffffZ",
        "yyyy-MM-ddTHH:mm:ss.fffffZ",
        "yyyy-MM-ddTHH:mm:ss.ffffZ",
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyy-MM-ddTHH:mm:ss.ffZ",
        "yyyy-MM-ddTHH:mm:ss.fZ",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss.fffffff",
        "yyyy-MM-ddTHH:mm:ss.ffffff",
        "yyyy-MM-ddTHH:mm:ss.fffff",
        "yyyy-MM-ddTHH:mm:ss.ffff",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss.ff",
        "yyyy-MM-ddTHH:mm:ss.f",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-DDDTHH:mm:ss.fffffffZ",
        "yyyy-DDDTHH:mm:ss.ffffffZ",
        "yyyy-DDDTHH:mm:ssZ",
        "yyyy-DDDTHH:mm:ss.fffffff",
        "yyyy-DDDTHH:mm:ss.ffffff",
        "yyyy-DDDTHH:mm:ss"
    ];

    private static DateTime ParseDateTime(string value, string fieldName)
    {

        if (DateTime.TryParseExact(value, DateTimeFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result))
        {
            return result;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
        {
            return result;
        }

        throw new CdmParseException($"Invalid datetime value for {fieldName}: '{value}'.");
    }

    private static DateTime? ParseOptionalDateTime(XElement parent, string localName)
    {
        var value = GetOptionalElementValue(parent, localName);
        return value == null ? null : ParseDateTime(value, localName);
    }

    private static double ParseRequiredDouble(XElement parent, string localName)
    {
        return ParseDouble(GetRequiredElementValue(parent, localName), localName);
    }

    private static double? ParseOptionalDouble(XElement parent, string localName)
    {
        var value = GetOptionalElementValue(parent, localName);
        return value == null ? null : ParseDouble(value, localName);
    }

    private static int? ParseOptionalInt(XElement parent, string localName)
    {
        var value = GetOptionalElementValue(parent, localName);
        if (value == null)
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        throw new CdmParseException($"Invalid integer value for {localName}: '{value}'.");
    }

    private static double ParseDouble(string value, string fieldName)
    {
        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        throw new CdmParseException($"Invalid numeric value for {fieldName}: '{value}'.");
    }

    private static double ParsePositionToMeters(XElement element)
    {
        return ParseLengthValue(element, "km") * 1000.0;
    }

    private static double ParseVelocityToMetersPerSecond(XElement element)
    {
        var units = element.Attribute("units")?.Value ?? "km/s";
        var value = ParseDouble(element.Value, element.Name.LocalName);
        return units switch
        {
            "km/s" => value * 1000.0,
            "m/s" => value,
            _ => throw new CdmParseException($"Unsupported velocity units '{units}'.")
        };
    }

    private static double ParseLengthToMeters(XElement element)
    {
        return ParseLengthValue(element, "km") * 1000.0;
    }

    private static double? ParseOptionalLength(XElement parent, string localName)
    {
        var element = GetOptionalElement(parent, localName);
        return element == null ? null : ParseLengthToMeters(element);
    }

    private static double ParseLengthValue(XElement element, string defaultUnits)
    {
        var units = element.Attribute("units")?.Value ?? defaultUnits;
        var value = ParseDouble(element.Value, element.Name.LocalName);
        return units switch
        {
            "km" => value,
            "m" => value / 1000.0,
            _ => throw new CdmParseException($"Unsupported length units '{units}'.")
        };
    }

    private static double ParseDeltaVelocityToMetersPerSecond(XElement element)
    {
        var units = element.Attribute("units")?.Value ?? "m/s";
        var value = ParseDouble(element.Value, element.Name.LocalName);
        return units switch
        {
            "m/s" => value,
            "km/s" => value * 1000.0,
            _ => throw new CdmParseException($"Unsupported delta-velocity units '{units}'.")
        };
    }

    private static double? ParseOptionalDeltaVelocity(XElement parent, string localName)
    {
        var element = GetOptionalElement(parent, localName);
        return element == null ? null : ParseDeltaVelocityToMetersPerSecond(element);
    }

    private static bool? ParseOptionalYesNo(XElement parent, string localName)
    {
        var value = GetOptionalElementValue(parent, localName);
        return value == null ? null : CdmEnumExtensions.ParseYesNo(value);
    }

    private static ObjectType? ParseOptionalObjectType(XElement parent, string localName)
    {
        var value = GetOptionalElementValue(parent, localName);
        if (value == null)
        {
            return null;
        }

        return value.ToUpperInvariant() switch
        {
            "PAYLOAD" => ObjectType.Payload,
            "ROCKET BODY" => ObjectType.RocketBody,
            "DEBRIS" => ObjectType.Debris,
            "UNKNOWN" => ObjectType.Unknown,
            "OTHER" => ObjectType.Other,
            _ => throw new CdmParseException($"Unsupported object type '{value}'.")
        };
    }
}
