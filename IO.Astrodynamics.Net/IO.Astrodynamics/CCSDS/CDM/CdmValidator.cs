// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using IO.Astrodynamics.CCSDS.Common.Enums;

namespace IO.Astrodynamics.CCSDS.CDM;

public sealed class CdmValidator
{
    private const string SchemaValidationCode = "CDM001";
    private const string ContentValidationCode = "CDM002";
    private const string CcsdsNamespace = "urn:ccsds:schema:ndmxml";

    public CdmValidationResult ValidateSchema(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            return CdmValidationResult.Failure(SchemaValidationCode, $"File not found: {filePath}", "File");
        }

        using var stream = File.OpenRead(filePath);
        return ValidateSchema(stream);
    }

    public CdmValidationResult ValidateSchemaFromXml(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new ArgumentException("XML content cannot be null or empty.", nameof(xml));
        }

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        return ValidateSchema(stream);
    }

    public CdmValidationResult ValidateSchema(Stream xmlStream)
    {
        if (xmlStream == null)
        {
            throw new ArgumentNullException(nameof(xmlStream));
        }

        var result = new CdmValidationResult();

        try
        {
            using var buffer = new MemoryStream();
            xmlStream.CopyTo(buffer);
            buffer.Position = 0;
            var document = XDocument.Load(buffer);
            buffer.Position = 0;

            var schemaSet = CreateSchemaSet(document.Root?.Name.LocalName);
            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = schemaSet,
                ValidationFlags = XmlSchemaValidationFlags.ProcessIdentityConstraints |
                                  XmlSchemaValidationFlags.ReportValidationWarnings
            };

            settings.ValidationEventHandler += (_, args) =>
            {
                var severity = args.Severity == XmlSeverityType.Error
                    ? CdmValidationSeverity.Error
                    : CdmValidationSeverity.Warning;
                var path = args.Exception?.LineNumber > 0
                    ? $"Line {args.Exception.LineNumber}, Position {args.Exception.LinePosition}"
                    : "XML";
                result.AddIssue(new CdmValidationError(severity, SchemaValidationCode, args.Message, path));
            };

            using var reader = XmlReader.Create(buffer, settings);
            while (reader.Read())
            {
            }
        }
        catch (XmlException ex)
        {
            result.AddError(SchemaValidationCode, $"XML parsing error: {ex.Message}", $"Line {ex.LineNumber}");
        }
        catch (XmlSchemaException ex)
        {
            result.AddError(SchemaValidationCode, $"Schema error: {ex.Message}", $"Line {ex.LineNumber}");
        }
        catch (Exception ex)
        {
            result.AddError(SchemaValidationCode, $"Schema validation failed: {ex.Message}", "Schema");
        }

        return result;
    }

    public CdmValidationResult Validate(Cdm cdm)
    {
        if (cdm == null)
        {
            throw new ArgumentNullException(nameof(cdm));
        }

        var result = new CdmValidationResult();

        if (string.IsNullOrWhiteSpace(cdm.Header.Originator))
        {
            result.AddError(ContentValidationCode, "Header originator is required.", "Header.Originator");
        }

        if (string.IsNullOrWhiteSpace(cdm.Header.MessageId))
        {
            result.AddError(ContentValidationCode, "Header message ID is required.", "Header.MessageId");
        }

        if (cdm.RelativeMetadataData.TcaUtc == default)
        {
            result.AddError(ContentValidationCode, "TCA is required.", "RelativeMetadataData.TcaUtc");
        }

        if (cdm.RelativeMetadataData.MissDistanceMeters < 0.0)
        {
            result.AddError(ContentValidationCode, "Miss distance must be non-negative.", "RelativeMetadataData.MissDistanceMeters");
        }

        if (cdm.Segments.Count != 2)
        {
            result.AddError(ContentValidationCode, "CDM must contain exactly two segments.", "Segments");
            return result;
        }

        ValidateSegment(cdm.Segments[0], CdmObjectRole.Object1, result, "Segments[0]");
        ValidateSegment(cdm.Segments[1], CdmObjectRole.Object2, result, "Segments[1]");

        return result;
    }

    private static void ValidateSegment(CdmSegment segment, CdmObjectRole expectedRole, CdmValidationResult result, string path)
    {
        if (segment.Metadata == null)
        {
            result.AddError(ContentValidationCode, "Segment metadata is required.", $"{path}.Metadata");
            return;
        }

        if (segment.Data == null)
        {
            result.AddError(ContentValidationCode, "Segment data is required.", $"{path}.Data");
            return;
        }

        if (segment.Metadata.Object != expectedRole)
        {
            result.AddError(ContentValidationCode, $"Segment role must be {expectedRole.ToSchemaValue()}.", $"{path}.Metadata.Object");
        }

        RequireValue(segment.Metadata.ObjectDesignator, $"{path}.Metadata.ObjectDesignator", result);
        RequireValue(segment.Metadata.CatalogName, $"{path}.Metadata.CatalogName", result);
        RequireValue(segment.Metadata.ObjectName, $"{path}.Metadata.ObjectName", result);
        RequireValue(segment.Metadata.InternationalDesignator, $"{path}.Metadata.InternationalDesignator", result);
        RequireValue(segment.Metadata.EphemerisName, $"{path}.Metadata.EphemerisName", result);

        if (!segment.Metadata.CovarianceMethod.HasValue)
        {
            result.AddError(ContentValidationCode, "Covariance method is required.", $"{path}.Metadata.CovarianceMethod");
        }

        if (!segment.Metadata.Maneuverable.HasValue)
        {
            result.AddError(ContentValidationCode, "Maneuverable flag is required.", $"{path}.Metadata.Maneuverable");
        }

        if (!segment.Metadata.ReferenceFrame.HasValue)
        {
            result.AddError(ContentValidationCode, "Reference frame is required.", $"{path}.Metadata.ReferenceFrame");
        }

        if (segment.Data.StateVector == null)
        {
            result.AddError(ContentValidationCode, "State vector is required.", $"{path}.Data.StateVector");
        }

        if (segment.Data.CovarianceMatrix == null)
        {
            result.AddError(ContentValidationCode, "Covariance matrix is required.", $"{path}.Data.CovarianceMatrix");
        }
        else if (segment.Data.CovarianceMatrix.StateCovarianceRtn.Rows != 6 || segment.Data.CovarianceMatrix.StateCovarianceRtn.Columns != 6)
        {
            result.AddError(ContentValidationCode, "Covariance matrix must be 6x6.", $"{path}.Data.CovarianceMatrix.StateCovarianceRtn");
        }
    }

    private static void RequireValue(string? value, string path, CdmValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result.AddError(ContentValidationCode, "Value is required.", path);
        }
    }

    private static void AddSyntheticRootSchema(XmlSchemaSet schemaSet, string elementName, string typeName)
    {
        var rootSchema = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<xsd:schema xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
            xmlns:ndm=""urn:ccsds:schema:ndmxml""
            targetNamespace=""urn:ccsds:schema:ndmxml""
            elementFormDefault=""qualified"">
    <xsd:element name=""{elementName}"" type=""ndm:{typeName}""/>
</xsd:schema>";

        using var stringReader = new StringReader(rootSchema);
        using var rootReader = XmlReader.Create(stringReader);
        schemaSet.Add(CcsdsNamespace, rootReader);
    }

    private static XmlSchemaSet CreateSchemaSet(string? rootLocalName)
    {
        var schemaSet = new XmlSchemaSet
        {
            XmlResolver = new EmbeddedSchemaResolver()
        };

        if (string.Equals(rootLocalName, "ndm", StringComparison.OrdinalIgnoreCase))
        {
            using var ndmStream = CcsdsSchemaLoader.GetSchemaStream("ndmxml-4.0.0-ndm-4.0.xsd");
            using var ndmReader = XmlReader.Create(ndmStream);
            schemaSet.Add(CcsdsNamespace, ndmReader);
            AddSyntheticRootSchema(schemaSet, "ndm", "ndmType");
        }
        else
        {
            using var cdmStream = CcsdsSchemaLoader.GetSchemaStream(CcsdsSchemaLoader.SchemaFiles.Cdm);
            using var cdmReader = XmlReader.Create(cdmStream);
            schemaSet.Add(CcsdsNamespace, cdmReader);
            AddSyntheticRootSchema(schemaSet, "cdm", "cdmType");
        }

        schemaSet.Compile();
        return schemaSet;
    }

    private sealed class EmbeddedSchemaResolver : XmlUrlResolver
    {
        public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            var fileName = Path.GetFileName(absoluteUri.LocalPath);
            if (!string.IsNullOrWhiteSpace(fileName) &&
                fileName.StartsWith("ndmxml-", StringComparison.OrdinalIgnoreCase) &&
                fileName.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase))
            {
                return CcsdsSchemaLoader.GetSchemaStream(fileName);
            }

            return base.GetEntity(absoluteUri, role, ofObjectToReturn ?? typeof(Stream))
                   ?? throw new InvalidOperationException($"Could not resolve schema resource '{absoluteUri}'.");
        }
    }
}
