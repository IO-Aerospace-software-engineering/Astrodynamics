// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace IO.Astrodynamics.CCSDS.CDM;

public sealed class Cdm
{
    private static readonly CdmReader DefaultReader = new();
    private static readonly CdmWriter DefaultWriter = new();
    private static readonly CdmValidator DefaultValidator = new();

    public const string Version = "1.0";
    public const string FormatId = "CCSDS_CDM_VERS";

    public Cdm(CdmHeader header, CdmRelativeMetadataData relativeMetadataData, IReadOnlyList<CdmSegment> segments)
    {
        Header = header ?? throw new ArgumentNullException(nameof(header));
        RelativeMetadataData = relativeMetadataData ?? throw new ArgumentNullException(nameof(relativeMetadataData));
        Segments = segments ?? throw new ArgumentNullException(nameof(segments));

        if (Segments.Count != 2)
        {
            throw new ArgumentException("CDM must contain exactly two segments.", nameof(segments));
        }
    }

    public CdmHeader Header { get; }

    public CdmRelativeMetadataData RelativeMetadataData { get; }

    public IReadOnlyList<CdmSegment> Segments { get; }

    public string WriteToString(bool wrapInNdmContainer = true)
    {
        return DefaultWriter.WriteToString(this, wrapInNdmContainer);
    }

    public void WriteToFile(string filePath, bool wrapInNdmContainer = true)
    {
        DefaultWriter.WriteToFile(this, filePath, wrapInNdmContainer);
    }

    public void WriteToStream(Stream stream, bool wrapInNdmContainer = true)
    {
        DefaultWriter.WriteToStream(this, stream, wrapInNdmContainer);
    }

    public CdmValidationResult Validate()
    {
        return DefaultValidator.Validate(this);
    }

    public static Cdm ReadFromFile(string filePath)
    {
        return DefaultReader.ReadFromFile(filePath);
    }

    public static Cdm ReadFromStream(Stream stream)
    {
        return DefaultReader.ReadFromStream(stream);
    }

    public static Cdm ReadFromString(string xml)
    {
        return DefaultReader.ReadFromString(xml);
    }

    public static CdmValidationResult ValidateSchema(string filePath)
    {
        return DefaultValidator.ValidateSchema(filePath);
    }

    public static CdmValidationResult ValidateSchemaFromXml(string xml)
    {
        return DefaultValidator.ValidateSchemaFromXml(xml);
    }
}
