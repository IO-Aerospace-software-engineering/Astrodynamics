// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using IO.Astrodynamics.CCSDS.OPM;
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
        /// An unqualified &lt;ndm&gt; holding one OMM (iss.omm's) and one OPM.
        /// </summary>
        internal static string OneOmmOneOpm(Opm opm)
        {
            var omm = XDocument.Load(IssOmmPath).Root!.Elements().Single(e => e.Name.LocalName == "omm");
            var opmElement = XDocument.Parse(opm.ToXmlString(wrapInNdm: false)).Root!;
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
                StripXsi(XDocument.Parse(first.ToXmlString(wrapInNdm: false)).Root!),
                StripXsi(XDocument.Parse(second.ToXmlString(wrapInNdm: false)).Root!)).ToString();
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
