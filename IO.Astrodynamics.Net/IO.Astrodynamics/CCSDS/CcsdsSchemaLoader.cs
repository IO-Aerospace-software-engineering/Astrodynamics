// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;

namespace IO.Astrodynamics.CCSDS;

/// <summary>
/// Provides access to embedded CCSDS NDM/XML schemas for validation.
/// </summary>
/// <remarks>
/// Schemas are embedded as assembly resources and loaded on demand.
/// Supports CCSDS NDM/XML version 4.0.0.
/// </remarks>
public static class CcsdsSchemaLoader
{
    private const string SchemaResourcePrefix = "IO.Astrodynamics.CCSDS.Schema.";
    private const string CcsdsNamespace = "urn:ccsds:schema:ndmxml";

    private static readonly object LockObject = new object();
    private static XmlSchemaSet _ommSchemaSet;
    private static XmlSchemaSet _commonSchemaSet;

    /// <summary>
    /// Schema file names for different message types.
    /// </summary>
    public static class SchemaFiles
    {
        public const string Common = "ndmxml-4.0.0-common-4.0.xsd";
        public const string Omm = "ndmxml-4.0.0-omm-3.0.xsd";
        public const string Opm = "ndmxml-4.0.0-opm-3.0.xsd";
        public const string Oem = "ndmxml-4.0.0-oem-3.0.xsd";
        public const string Aem = "ndmxml-4.0.0-aem-2.0.xsd";
        public const string Apm = "ndmxml-4.0.0-apm-2.0.xsd";
        public const string Cdm = "ndmxml-4.0.0-cdm-1.0.xsd";
        public const string Tdm = "ndmxml-4.0.0-tdm-2.0.xsd";
        public const string Rdm = "ndmxml-4.0.0-rdm-1.0.xsd";
    }

    /// <summary>
    /// Gets the compiled XmlSchemaSet for OMM validation.
    /// </summary>
    /// <remarks>
    /// Includes both OMM-specific schema and common schema (via xs:include in OMM schema).
    /// Schema is loaded and compiled once, then cached.
    /// </remarks>
    /// <returns>A compiled XmlSchemaSet for OMM validation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when schemas cannot be loaded.</exception>
    public static XmlSchemaSet GetOmmSchemaSet()
    {
        if (_ommSchemaSet != null)
            return _ommSchemaSet;

        lock (LockObject)
        {
            if (_ommSchemaSet != null)
                return _ommSchemaSet;

            var schemaSet = new XmlSchemaSet();

            // Use custom resolver to handle schema includes (common schema is included via xs:include)
            schemaSet.XmlResolver = new EmbeddedResourceResolver();

            // Only add OMM schema - it will include common schema via xs:include
            AddSchemaFromResource(schemaSet, SchemaFiles.Omm);

            schemaSet.Compile();
            _ommSchemaSet = schemaSet;
        }

        return _ommSchemaSet;
    }

    /// <summary>
    /// Gets the compiled XmlSchemaSet containing only the common schema.
    /// </summary>
    /// <returns>A compiled XmlSchemaSet with common types.</returns>
    public static XmlSchemaSet GetCommonSchemaSet()
    {
        if (_commonSchemaSet != null)
            return _commonSchemaSet;

        lock (LockObject)
        {
            if (_commonSchemaSet != null)
                return _commonSchemaSet;

            var schemaSet = new XmlSchemaSet();
            schemaSet.XmlResolver = new EmbeddedResourceResolver();
            AddSchemaFromResource(schemaSet, SchemaFiles.Common);
            schemaSet.Compile();
            _commonSchemaSet = schemaSet;
        }

        return _commonSchemaSet;
    }

    /// <summary>
    /// Loads a schema file from embedded resources.
    /// </summary>
    /// <param name="schemaFileName">The schema file name (e.g., "ndmxml-4.0.0-omm-3.0.xsd").</param>
    /// <returns>A stream containing the schema content.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the schema resource is not found.</exception>
    public static Stream GetSchemaStream(string schemaFileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = SchemaResourcePrefix + schemaFileName;

        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            // Try alternative naming (dots replaced with underscores in resource names)
            var altResourceName = resourceName.Replace("-", "_");
            stream = assembly.GetManifestResourceStream(altResourceName);
        }

        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Could not find embedded schema resource: {resourceName}. " +
                $"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
        }

        return stream;
    }

    /// <summary>
    /// Reads a schema file content as string from embedded resources.
    /// </summary>
    /// <param name="schemaFileName">The schema file name.</param>
    /// <returns>The schema content as string.</returns>
    public static string GetSchemaContent(string schemaFileName)
    {
        using var stream = GetSchemaStream(schemaFileName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void AddSchemaFromResource(XmlSchemaSet schemaSet, string schemaFileName)
    {
        using var stream = GetSchemaStream(schemaFileName);
        using var reader = XmlReader.Create(stream);
        schemaSet.Add(CcsdsNamespace, reader);
    }

    /// <summary>
    /// Custom XML resolver that loads schema includes from embedded resources.
    /// </summary>
    private class EmbeddedResourceResolver : XmlUrlResolver
    {
        public override object GetEntity(Uri absoluteUri, string role, Type ofObjectToReturn)
        {
            // Extract file name from URI
            var fileName = Path.GetFileName(absoluteUri.LocalPath);

            // Check if this is one of our embedded schemas
            if (fileName.StartsWith("ndmxml-", StringComparison.OrdinalIgnoreCase) &&
                fileName.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return GetSchemaStream(fileName);
                }
                catch (InvalidOperationException)
                {
                    // Fall back to default resolver
                }
            }

            return base.GetEntity(absoluteUri, role, ofObjectToReturn);
        }
    }
}
