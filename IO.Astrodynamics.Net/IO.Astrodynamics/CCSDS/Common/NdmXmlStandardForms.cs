// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace IO.Astrodynamics.CCSDS.Common;

/// <summary>
/// The schema sets of the two standard forms of an NDM/XML 4.0.0 document (CCSDS 505.0-B-3), whose root has no namespace:
/// the master of the embedded schemas (<c>ndmxml-4.0.0-master-4.0.xsd</c>), which declares the roots <c>ndm</c>,
/// <c>omm</c>, <c>opm</c>, <c>cdm</c>, ... without a namespace, with the message modules as embedded
/// (<c>elementFormDefault="qualified"</c>: the elements below the root in the namespace <c>urn:ccsds:schema:ndmxml</c>) or
/// with their <c>elementFormDefault</c> set to <c>unqualified</c> (every element without a namespace, the form of
/// Space-Track and of the library's OMM and OPM writers). The unqualified set is the one SANA publishes
/// (sanaregistry.org/files/ndmxml_unqualified/), which differs from the qualified one by that attribute alone.
/// </summary>
/// <remarks>
/// Before the third round of #668, the validators declared only a synthetic root in the namespace, which no unqualified root
/// matched: an unqualified document was reported valid with a warning per element, nothing below its root checked.
/// The compiled sets are built once and shared; validation only reads them.
/// </remarks>
internal static class NdmXmlStandardForms
{
    internal const string Namespace = "urn:ccsds:schema:ndmxml";
    private const string MasterSchema = "ndmxml-4.0.0-master-4.0.xsd";
    private const string BaseUri = "file:///ndmxml/";
    private const string Qualified = "elementFormDefault=\"qualified\"";
    private const string Unqualified = "elementFormDefault=\"unqualified\"";

    private static readonly Lazy<XmlSchemaSet> QualifiedSet = new(() => Build(false), LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<XmlSchemaSet> UnqualifiedSet = new(() => Build(true), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// The schema set of a document whose root has no namespace: the qualified set when an element of the document is in the
    /// NDM/XML namespace, the unqualified set otherwise.
    /// </summary>
    internal static XmlSchemaSet For(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        bool qualified = document.Descendants().Any(e => e.Name.NamespaceName == Namespace);
        return qualified ? QualifiedSet.Value : UnqualifiedSet.Value;
    }

    /// <summary>
    /// The message of the error for a root that is not one of <paramref name="acceptedRoots"/> without a namespace, or
    /// <c>null</c> for an accepted root. The caller handles a root in the NDM/XML namespace before.
    /// </summary>
    internal static string? RootError(XElement root, string messageName, params string[] acceptedRoots)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root.Name.Namespace == XNamespace.None && acceptedRoots.Contains(root.Name.LocalName))
        {
            return null;
        }

        return $"The root element '{root.Name.LocalName}'" +
               (root.Name.Namespace == XNamespace.None ? "" : $" in the namespace '{root.Name.NamespaceName}'") +
               $" is not a root of an NDM/XML {messageName} document: NDM/XML 4.0.0 (CCSDS 505.0-B-3) declares " +
               string.Join(" and ", acceptedRoots.Select(r => $"<{r}>")) + " without a namespace.";
    }

    private static XmlSchemaSet Build(bool unqualified)
    {
        var resolver = new Resolver(unqualified);
        var schemaSet = new XmlSchemaSet { XmlResolver = resolver };
        var settings = new XmlReaderSettings { XmlResolver = resolver, DtdProcessing = DtdProcessing.Prohibit };
        using (var stream = Open(MasterSchema, unqualified))
        using (var reader = XmlReader.Create(stream, settings, BaseUri + MasterSchema))
        {
            schemaSet.Add(null, reader);
        }

        schemaSet.Compile();
        return schemaSet;
    }

    /// <summary>
    /// The embedded schema, with its single <c>elementFormDefault="qualified"</c> made <c>unqualified</c> for the unqualified
    /// set.
    /// </summary>
    private static Stream Open(string fileName, bool unqualified)
    {
        if (!unqualified)
        {
            return CcsdsSchemaLoader.GetSchemaStream(fileName);
        }

        var text = CcsdsSchemaLoader.GetSchemaContent(fileName);
        int first = text.IndexOf(Qualified, StringComparison.Ordinal);
        if (first < 0 || text.IndexOf(Qualified, first + 1, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException(
                $"The embedded schema {fileName} does not hold exactly one {Qualified}: its unqualified form cannot be derived.");
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(text.Replace(Qualified, Unqualified, StringComparison.Ordinal)));
    }

    private sealed class Resolver : XmlResolver
    {
        private readonly bool _unqualified;

        internal Resolver(bool unqualified)
        {
            _unqualified = unqualified;
        }

        public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            var fileName = Path.GetFileName(absoluteUri.LocalPath);
            if (!fileName.StartsWith("ndmxml-", StringComparison.Ordinal) || !fileName.EndsWith(".xsd", StringComparison.Ordinal))
            {
                throw new XmlException($"Only the embedded NDM/XML schemas are resolved, not {absoluteUri}.");
            }

            return Open(fileName, _unqualified);
        }
    }
}
