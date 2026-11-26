// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using IO.Astrodynamics.Ccsds;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS
{
    /// <summary>
    /// NDM documents holding several messages, built with System.Xml.Linq, which also counts their messages independently
    /// of the readers (#679).
    /// </summary>
    internal static class NdmDocuments
    {
        internal const string IssOmmPath = "CCSDS/iss.omm";

        /// <summary>
        /// An unqualified &lt;ndm&gt; holding iss.omm's &lt;omm&gt; twice, the second renamed.
        /// </summary>
        internal static string TwoOmm()
        {
            var document = XDocument.Load(IssOmmPath);
            var first = document.Root!.Elements().Single(e => e.Name.LocalName == "omm");
            var second = new XElement(first);
            second.Descendants().Single(e => e.Name.LocalName == "OBJECT_NAME").Value = "ISS COPY";
            first.AddAfterSelf(second);
            var xml = document.ToString();
            AssertMessageCount(xml, "ndm", "omm", 2);
            return xml;
        }

        /// <summary>
        /// A &lt;wrapper&gt; root, not an NDM, holding iss.omm's &lt;omm&gt; twice, each in its own nested element.
        /// </summary>
        internal static string TwoNestedOmm()
        {
            var omm = XDocument.Load(IssOmmPath).Root!.Elements().Single(e => e.Name.LocalName == "omm");
            var xml = new XElement("wrapper", new XElement("a", new XElement(omm)), new XElement("b", new XElement(omm))).ToString();
            Assert.Equal(2, XDocument.Parse(xml).Root!.Descendants().Count(e => e.Name.LocalName == "omm"));
            return xml;
        }

        /// <summary>
        /// An unqualified &lt;ndm&gt; holding iss.omm's &lt;omm&gt; as a child, and again inside an &lt;inner&gt; child
        /// (a wrapper or another NDM).
        /// </summary>
        internal static string OneOmmAndOneNestedOmm(string inner)
        {
            var omm = XDocument.Load(IssOmmPath).Root!.Elements().Single(e => e.Name.LocalName == "omm");
            var xml = new XElement("ndm", new XElement(omm), new XElement(inner, new XElement(omm))).ToString();
            AssertMessageCount(xml, "ndm", "omm", 1);
            Assert.Equal(2, XDocument.Parse(xml).Root!.Descendants().Count(e => e.Name.LocalName == "omm"));
            return xml;
        }

        /// <summary>
        /// An unqualified &lt;ndm&gt; holding one OMM (iss.omm's) and one OPM.
        /// </summary>
        internal static string OneOmmOneOpm(Opm opm)
        {
            var omm = XDocument.Load(IssOmmPath).Root!.Elements().Single(e => e.Name.LocalName == "omm");
            var opmElement = XDocument.Parse(opm.ToXml(new CcsdsWriteOptions { WrapInNdmContainer = false })).Root!;
            var xml = new XElement("ndm", new XElement(omm), StripXsi(opmElement)).ToString();
            AssertMessageCount(xml, "ndm", "omm", 1);
            AssertMessageCount(xml, "ndm", "opm", 1);
            return xml;
        }

        /// <summary>
        /// An unqualified &lt;ndm&gt; holding two OPMs, as <see cref="Opm.ToXmlString"/> writes them.
        /// </summary>
        internal static string TwoOpm(Opm first, Opm second)
        {
            var xml = new XElement("ndm",
                StripXsi(XDocument.Parse(first.ToXml(new CcsdsWriteOptions { WrapInNdmContainer = false })).Root!),
                StripXsi(XDocument.Parse(second.ToXml(new CcsdsWriteOptions { WrapInNdmContainer = false })).Root!)).ToString();
            AssertMessageCount(xml, "ndm", "opm", 2);
            return xml;
        }

        /// <summary>
        /// An unqualified &lt;ndm&gt; holding two copies of a standalone unqualified &lt;cdm&gt;, the second with another MESSAGE_ID.
        /// </summary>
        internal static string TwoCdm(string unqualifiedCdmXml)
        {
            var first = XDocument.Parse(unqualifiedCdmXml).Root!;
            Assert.Equal("cdm", first.Name.LocalName);
            var second = new XElement(first);
            second.Descendants().Single(e => e.Name.LocalName == "MESSAGE_ID").Value = "SECOND";
            var xml = new XElement("ndm", new XElement(first), second).ToString();
            AssertMessageCount(xml, "ndm", "cdm", 2);
            return xml;
        }

        /// <summary>The namespace of the qualified NDM/XML 4.0.0 schemas (CCSDS 505.0-B-3).</summary>
        internal const string NdmNamespace = "urn:ccsds:schema:ndmxml";

        /// <summary>
        /// iss.omm (an unqualified NDM of one OMM, as Space-Track writes it) with its &lt;segment&gt; twice, the copy's
        /// OBJECT_NAME renamed.
        /// </summary>
        internal static string TwoSegmentOmm()
        {
            return WithDuplicatedElement(System.IO.File.ReadAllText(IssOmmPath), "omm", "body/segment");
        }

        /// <summary>
        /// An OPM as <see cref="Opm.ToXmlString"/> writes it (unqualified) with its &lt;segment&gt; twice, the copy's
        /// OBJECT_NAME renamed.
        /// </summary>
        internal static string TwoSegmentOpm(Opm opm)
        {
            return WithDuplicatedElement(opm.ToXml(), "opm", "body/segment");
        }

        /// <summary>
        /// A copy of the document with the element at <paramref name="path"/> (local names from the message element,
        /// '/'-separated) repeated right after itself, the copy's OBJECT_NAME, if any, renamed "SECOND SEGMENT". The count of
        /// that element in its parent, 2, is checked with System.Xml.Linq, independently of the readers.
        /// </summary>
        internal static string WithDuplicatedElement(string xml, string messageName, string path)
        {
            var document = XDocument.Parse(xml);
            var message = document.Root!.Name.LocalName == messageName
                ? document.Root
                : document.Root.Elements().Single(e => e.Name.LocalName == messageName);
            var element = path.Split('/').Aggregate(message, (parent, name) => parent.Elements().Single(e => e.Name.LocalName == name));
            var copy = new XElement(element);
            foreach (var objectName in copy.DescendantsAndSelf().Where(e => e.Name.LocalName == "OBJECT_NAME"))
            {
                objectName.Value = "SECOND SEGMENT";
            }

            element.AddAfterSelf(copy);
            var result = document.ToString();
            var reparsed = XDocument.Parse(result);
            var reparsedMessage = reparsed.Root!.Name.LocalName == messageName
                ? reparsed.Root
                : reparsed.Root.Elements().Single(e => e.Name.LocalName == messageName);
            var names = path.Split('/');
            var parentOfDuplicate = names.Take(names.Length - 1)
                .Aggregate(reparsedMessage, (parent, name) => parent.Elements().Single(e => e.Name.LocalName == name));
            Assert.Equal(2, parentOfDuplicate.Elements().Count(e => e.Name.LocalName == names[^1]));
            return result;
        }

        /// <summary>
        /// The message element of the document, standalone (out of its &lt;ndm&gt;, which the validators' schema set does
        /// not declare), with every element in the namespace of the qualified NDM/XML schemas and the namespace declarations
        /// and xsi attributes removed, so that the embedded SANA schemas check its structure.
        /// </summary>
        internal static string QualifiedMessage(string xml, string messageName)
        {
            var root = XDocument.Parse(xml).Root!;
            var message = new XElement(root.Name.LocalName == messageName
                ? root
                : root.Elements().Single(e => e.Name.LocalName == messageName));
            XNamespace ndm = NdmNamespace;
            foreach (var element in message.DescendantsAndSelf())
            {
                element.Attributes().Where(a => a.IsNamespaceDeclaration || a.Name.Namespace == XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance")).Remove();
                element.Name = ndm + element.Name.LocalName;
            }

            return message.ToString();
        }

        /// <summary>
        /// The forms of an NDM/XML document that the SANA NDM/XML 4.0.0 master declares (CCSDS 505.0-B-3): the message of
        /// <paramref name="xml"/> under an unqualified &lt;ndm&gt; ("unqualified-ndm") or as the root ("unqualified-message"),
        /// every element without a namespace, as Space-Track and the writers write it; and the same with every element below
        /// the root in the NDM namespace ("qualified-ndm", "qualified-message"), the form of the qualified schema set. Namespace
        /// declarations and xsi attributes are removed.
        /// </summary>
        internal static string InForm(string xml, string messageName, string form)
        {
            var root = XDocument.Parse(xml).Root!;
            var source = root.Name.LocalName == messageName
                ? root
                : root.Elements().Single(e => e.Name.LocalName == messageName);
            var message = new XElement(source);
            foreach (var element in message.DescendantsAndSelf())
            {
                element.Attributes().Where(a => a.IsNamespaceDeclaration || a.Name.Namespace == XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance")).Remove();
                element.Name = element.Name.LocalName;
            }

            bool qualified = form.StartsWith("qualified", StringComparison.Ordinal);
            if (qualified)
            {
                XNamespace ndm = NdmNamespace;
                foreach (var element in form == "qualified-ndm" ? message.DescendantsAndSelf() : message.Descendants())
                {
                    element.Name = ndm + element.Name.LocalName;
                }
            }

            var result = form switch
            {
                "unqualified-ndm" or "qualified-ndm" => new XElement("ndm", message),
                "unqualified-message" or "qualified-message" => message,
                _ => throw new ArgumentException($"Unknown form {form}", nameof(form))
            };
            return result.ToString();
        }

        /// <summary>
        /// A copy of the document with one mutation that the NDM/XML 4.0.0 schema rejects, applied by local name in the
        /// message <paramref name="messageName"/>, the new elements in the namespace of their parent: "ECCENTRICITY=-0.5"
        /// (nonNegativeDouble), "remove OBJECT_NAME" or "remove EPOCH" (required), "EPOCH=2026-DDDT05:00:55" (epochType
        /// pattern), "INCLINATION units=rad" (angle units deg only), "X units=m" (length units km only),
        /// "COLLISION_PROBABILITY=1.5" (probabilityType, at most 1), "FOO" (an undeclared element after OBJECT_NAME), "segment twice" (one segment), "root foo" (the root renamed, no global declaration).
        /// </summary>
        internal static string Mutated(string xml, string messageName, string mutation)
        {
            var document = XDocument.Parse(xml);
            var message = document.Root!.Name.LocalName == messageName
                ? document.Root
                : document.Root.Elements().Single(e => e.Name.LocalName == messageName);
            XElement First(string name) => message.Descendants().First(e => e.Name.LocalName == name);
            switch (mutation)
            {
                case "ECCENTRICITY=-0.5":
                    First("ECCENTRICITY").Value = "-0.5";
                    break;
                case "remove OBJECT_NAME":
                    First("OBJECT_NAME").Remove();
                    break;
                case "remove EPOCH":
                    First("EPOCH").Remove();
                    break;
                case "EPOCH=2026-DDDT05:00:55":
                    First("EPOCH").Value = "2026-DDDT05:00:55";
                    break;
                case "INCLINATION units=rad":
                    First("INCLINATION").SetAttributeValue("units", "rad");
                    break;
                case "COLLISION_PROBABILITY=1.5":
                    First("COLLISION_PROBABILITY").Value = "1.5";
                    break;
                case "X units=m":
                    First("X").SetAttributeValue("units", "m");
                    break;
                case "FOO":
                    var objectName = First("OBJECT_NAME");
                    objectName.AddAfterSelf(new XElement(objectName.Name.Namespace + "FOO", "1"));
                    break;
                case "segment twice":
                    var segment = First("segment");
                    segment.AddAfterSelf(new XElement(segment));
                    break;
                case "root foo":
                    document.Root.Name = document.Root.Name.Namespace + "foo";
                    break;
                default:
                    throw new ArgumentException($"Unknown mutation {mutation}", nameof(mutation));
            }

            return document.ToString();
        }

        /// <summary>
        /// A copy of the document with the text of its first element named <paramref name="localName"/> replaced by
        /// <paramref name="value"/> (an empty string leaves the element empty), the element renamed
        /// <paramref name="renameTo"/> when given; the new value is checked with System.Xml.Linq, independently of the
        /// readers.
        /// </summary>
        internal static string WithElementValue(string xml, string localName, string value, string? renameTo = null)
        {
            var document = XDocument.Parse(xml);
            var element = document.Descendants().First(e => e.Name.LocalName == localName);
            element.Value = value;
            if (renameTo != null)
            {
                element.Name = element.Name.Namespace + renameTo;
            }

            var result = document.ToString();
            Assert.Equal(value, XDocument.Parse(result).Descendants().First(e => e.Name.LocalName == (renameTo ?? localName)).Value);
            return result;
        }

        /// <summary>
        /// Writes the XML to a temporary file, runs the action on its path, and deletes the file.
        /// </summary>
        internal static void WithTempFile(string xml, Action<string> action)
        {
            var path = Path.Combine(Path.GetTempPath(), $"ndm_{Guid.NewGuid():N}.xml");
            File.WriteAllText(path, xml);
            try
            {
                action(path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static XElement StripXsi(XElement element)
        {
            var copy = new XElement(element);
            copy.Attributes().Where(a => a.IsNamespaceDeclaration || a.Name.Namespace == XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance")).Remove();
            return copy;
        }

        private static void AssertMessageCount(string xml, string root, string message, int count)
        {
            var document = XDocument.Parse(xml);
            Assert.Equal(root, document.Root!.Name.LocalName);
            Assert.Equal(count, document.Root.Elements().Count(e => e.Name.LocalName == message));
        }
    }
}
