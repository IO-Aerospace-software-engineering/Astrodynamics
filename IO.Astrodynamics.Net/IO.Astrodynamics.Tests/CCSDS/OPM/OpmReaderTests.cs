// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OPM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OPM
{
    public class OpmReaderTests
    {
        private readonly OpmReader _reader = new();

        public OpmReaderTests()
        {
            TestKernels.EnsureSolarSystem();
        }

        [Fact]
        public void LoadFromString_PositionInMeters_ThrowsValidationException()
        {
            // An OPM states its position in km (positionUnits, ndmxml-4.0.0-common-4.0.xsd): X in m is rejected by the
            // schema under default validation (before: loaded, 6778.137 read as km)
            var xml = NdmDocuments.Mutated(OpmReaderTests.FullOpm().ToXmlString(), "opm", "X units=m");

            var ex = Assert.Throws<OpmValidationException>(() => Opm.LoadFromString(xml));
            Assert.Contains(ex.ValidationResult.Errors, e => e.Message.Contains("units", StringComparison.Ordinal));
            NdmDocuments.WithTempFile(xml, file => Assert.Throws<OpmValidationException>(() => Opm.LoadFromFile(file)));
            Assert.Throws<OpmValidationException>(() => Opm.LoadFromStream(new MemoryStream(Encoding.UTF8.GetBytes(xml))));
            Assert.Equal(6778.137, Opm.LoadFromString(xml, validateSchema: false).Data.StateVector.X);
        }

        [Fact]
        public void ReadFromString_ValidMinimalOpm_ReturnsOpm()
        {
            // Arrange
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<opm id=""CCSDS_OPM_VERS"" version=""3.0"">
    <header>
        <CREATION_DATE>2024-01-15T12:00:00.000000</CREATION_DATE>
        <ORIGINATOR>TestOrg</ORIGINATOR>
    </header>
    <body>
        <segment>
            <metadata>
                <OBJECT_NAME>TestSat</OBJECT_NAME>
                <OBJECT_ID>2024-001A</OBJECT_ID>
                <CENTER_NAME>EARTH</CENTER_NAME>
                <REF_FRAME>ICRF</REF_FRAME>
                <TIME_SYSTEM>UTC</TIME_SYSTEM>
            </metadata>
            <data>
                <stateVector>
                    <EPOCH>2024-01-15T12:00:00.000000</EPOCH>
                    <X>6800</X>
                    <Y>0</Y>
                    <Z>0</Z>
                    <X_DOT>0</X_DOT>
                    <Y_DOT>7.5</Y_DOT>
                    <Z_DOT>0</Z_DOT>
                </stateVector>
            </data>
        </segment>
    </body>
</opm>";

            // Act
            var opm = _reader.ReadFromString(xml);

            // Assert
            Assert.Equal("TestSat", opm.ObjectName);
            Assert.Equal("2024-001A", opm.ObjectId);
            Assert.Equal(6800, opm.Data.StateVector.X);
        }

        [Fact]
        public void ReadFromString_NdmWrapped_ReturnsOpm()
        {
            // Arrange
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<ndm>
    <opm id=""CCSDS_OPM_VERS"" version=""3.0"">
        <header>
            <CREATION_DATE>2024-01-15T12:00:00.000000</CREATION_DATE>
            <ORIGINATOR>TestOrg</ORIGINATOR>
        </header>
        <body>
            <segment>
                <metadata>
                    <OBJECT_NAME>TestSat</OBJECT_NAME>
                    <OBJECT_ID>2024-001A</OBJECT_ID>
                    <CENTER_NAME>EARTH</CENTER_NAME>
                    <REF_FRAME>ICRF</REF_FRAME>
                    <TIME_SYSTEM>UTC</TIME_SYSTEM>
                </metadata>
                <data>
                    <stateVector>
                        <EPOCH>2024-01-15T12:00:00.000000</EPOCH>
                        <X>6800</X>
                        <Y>0</Y>
                        <Z>0</Z>
                        <X_DOT>0</X_DOT>
                        <Y_DOT>7.5</Y_DOT>
                        <Z_DOT>0</Z_DOT>
                    </stateVector>
                </data>
            </segment>
        </body>
    </opm>
</ndm>";

            // Act
            var opm = _reader.ReadFromString(xml);

            // Assert
            Assert.Equal("TestSat", opm.ObjectName);
        }

        [Fact]
        public void ReadFromString_NullXml_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _reader.ReadFromString(null));
        }

        [Fact]
        public void ReadFromString_EmptyXml_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _reader.ReadFromString(""));
        }

        [Fact]
        public void ReadFromString_InvalidXml_ThrowsOpmParseException()
        {
            // Act & Assert
            Assert.Throws<OpmParseException>(() =>
                _reader.ReadFromString("not valid xml"));
        }

        [Fact]
        public void ReadFromFile_ValidFile_ReturnsOpm()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<opm id=""CCSDS_OPM_VERS"" version=""3.0"">
    <header>
        <CREATION_DATE>2024-01-15T12:00:00.000000</CREATION_DATE>
        <ORIGINATOR>TestOrg</ORIGINATOR>
    </header>
    <body>
        <segment>
            <metadata>
                <OBJECT_NAME>TestSat</OBJECT_NAME>
                <OBJECT_ID>2024-001A</OBJECT_ID>
                <CENTER_NAME>EARTH</CENTER_NAME>
                <REF_FRAME>ICRF</REF_FRAME>
                <TIME_SYSTEM>UTC</TIME_SYSTEM>
            </metadata>
            <data>
                <stateVector>
                    <EPOCH>2024-01-15T12:00:00.000000</EPOCH>
                    <X>6800</X>
                    <Y>0</Y>
                    <Z>0</Z>
                    <X_DOT>0</X_DOT>
                    <Y_DOT>7.5</Y_DOT>
                    <Z_DOT>0</Z_DOT>
                </stateVector>
            </data>
        </segment>
    </body>
</opm>";
                File.WriteAllText(tempFile, xml);

                // Act
                var opm = _reader.ReadFromFile(tempFile);

                // Assert
                Assert.Equal("TestSat", opm.ObjectName);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void ReadFromFile_NullFilePath_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _reader.ReadFromFile(null));
        }

        [Fact]
        public void ReadFromFile_NonExistentFile_ThrowsFileNotFoundException()
        {
            // Act & Assert
            Assert.Throws<FileNotFoundException>(() =>
                _reader.ReadFromFile("/nonexistent/file.opm"));
        }

        [Fact]
        public void ReadFromStream_ValidStream_ReturnsOpm()
        {
            // Arrange
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<opm id=""CCSDS_OPM_VERS"" version=""3.0"">
    <header>
        <CREATION_DATE>2024-01-15T12:00:00.000000</CREATION_DATE>
        <ORIGINATOR>TestOrg</ORIGINATOR>
    </header>
    <body>
        <segment>
            <metadata>
                <OBJECT_NAME>TestSat</OBJECT_NAME>
                <OBJECT_ID>2024-001A</OBJECT_ID>
                <CENTER_NAME>EARTH</CENTER_NAME>
                <REF_FRAME>ICRF</REF_FRAME>
                <TIME_SYSTEM>UTC</TIME_SYSTEM>
            </metadata>
            <data>
                <stateVector>
                    <EPOCH>2024-01-15T12:00:00.000000</EPOCH>
                    <X>6800</X>
                    <Y>0</Y>
                    <Z>0</Z>
                    <X_DOT>0</X_DOT>
                    <Y_DOT>7.5</Y_DOT>
                    <Z_DOT>0</Z_DOT>
                </stateVector>
            </data>
        </segment>
    </body>
</opm>";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            // Act
            var opm = _reader.ReadFromStream(stream);

            // Assert
            Assert.Equal("TestSat", opm.ObjectName);
        }

        [Fact]
        public void ReadFromStream_NullStream_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _reader.ReadFromStream(null));
        }

        [Fact]
        public void ReadFromString_WithComments_ParsesComments()
        {
            // Arrange
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<opm id=""CCSDS_OPM_VERS"" version=""3.0"">
    <header>
        <COMMENT>Header comment</COMMENT>
        <CREATION_DATE>2024-01-15T12:00:00.000000</CREATION_DATE>
        <ORIGINATOR>TestOrg</ORIGINATOR>
    </header>
    <body>
        <segment>
            <metadata>
                <COMMENT>Metadata comment</COMMENT>
                <OBJECT_NAME>TestSat</OBJECT_NAME>
                <OBJECT_ID>2024-001A</OBJECT_ID>
                <CENTER_NAME>EARTH</CENTER_NAME>
                <REF_FRAME>ICRF</REF_FRAME>
                <TIME_SYSTEM>UTC</TIME_SYSTEM>
            </metadata>
            <data>
                <stateVector>
                    <COMMENT>StateVector comment</COMMENT>
                    <EPOCH>2024-01-15T12:00:00.000000</EPOCH>
                    <X>6800</X>
                    <Y>0</Y>
                    <Z>0</Z>
                    <X_DOT>0</X_DOT>
                    <Y_DOT>7.5</Y_DOT>
                    <Z_DOT>0</Z_DOT>
                </stateVector>
            </data>
        </segment>
    </body>
</opm>";

            // Act
            var opm = _reader.ReadFromString(xml);

            // Assert
            Assert.Contains("Header comment", opm.Header.Comments);
            Assert.Contains("Metadata comment", opm.Metadata.Comments);
            Assert.Contains("StateVector comment", opm.Data.StateVector.Comments);
        }

        [Fact]
        public void ReadFromString_MissingRequiredElement_ThrowsOpmParseException()
        {
            // Arrange
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<opm id=""CCSDS_OPM_VERS"" version=""3.0"">
    <header>
        <CREATION_DATE>2024-01-15T12:00:00.000000</CREATION_DATE>
        <ORIGINATOR>TestOrg</ORIGINATOR>
    </header>
    <body>
        <segment>
            <metadata>
                <OBJECT_NAME>TestSat</OBJECT_NAME>
                <!-- OBJECT_ID is missing -->
                <CENTER_NAME>EARTH</CENTER_NAME>
                <REF_FRAME>ICRF</REF_FRAME>
                <TIME_SYSTEM>UTC</TIME_SYSTEM>
            </metadata>
            <data>
                <stateVector>
                    <EPOCH>2024-01-15T12:00:00.000000</EPOCH>
                    <X>6800</X>
                    <Y>0</Y>
                    <Z>0</Z>
                    <X_DOT>0</X_DOT>
                    <Y_DOT>7.5</Y_DOT>
                    <Z_DOT>0</Z_DOT>
                </stateVector>
            </data>
        </segment>
    </body>
</opm>";

            // Act & Assert
            Assert.Throws<OpmParseException>(() => _reader.ReadFromString(xml));
        }

        #region Several messages (#679)

        private static string TwoOpmNdm()
        {
            var epoch = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
            var first = Opm.CreateMinimal("FIRST", "2024-001A", "EARTH", "ICRF", "UTC", new OpmStateVector(epoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0));
            var second = Opm.CreateMinimal("SECOND", "2024-002A", "EARTH", "ICRF", "UTC", new OpmStateVector(epoch, 7000.0, 0.0, 0.0, 0.0, 7.4, 0.0));
            return NdmDocuments.TwoOpm(first, second);
        }

        [Theory]
        [InlineData("OpmReader.ReadFromString")]
        [InlineData("Opm.LoadFromString")]
        [InlineData("Opm.LoadFromString without schema validation")]
        [InlineData("Opm.LoadFromFile")]
        [InlineData("Opm.LoadFromStream")]
        [InlineData("OpmReader.ReadFromFile")]
        [InlineData("OpmReader.ReadFromStream")]
        public void Read_NdmWithTwoOpm_Throws(string path)
        {
            var xml = TwoOpmNdm();
            Assert.True(new OpmValidator().ValidateSchemaFromXml(xml).IsValid);

            Action read = path switch
            {
                "OpmReader.ReadFromString" => () => _reader.ReadFromString(xml),
                "Opm.LoadFromString" => () => Opm.LoadFromString(xml),
                "Opm.LoadFromFile" => () => NdmDocuments.WithTempFile(xml, file => Opm.LoadFromFile(file)),
                "Opm.LoadFromStream" => () => Opm.LoadFromStream(new MemoryStream(Encoding.UTF8.GetBytes(xml))),
                "OpmReader.ReadFromFile" => () => NdmDocuments.WithTempFile(xml, file => _reader.ReadFromFile(file)),
                "OpmReader.ReadFromStream" => () => _reader.ReadFromStream(new MemoryStream(Encoding.UTF8.GetBytes(xml))),
                _ => () => Opm.LoadFromString(xml, validateSchema: false)
            };

            var ex = Assert.Throws<OpmParseException>(read);
            Assert.Contains("2 OPM messages", ex.Message);
        }

        [Fact]
        public void TryLoadFromFile_NdmWithTwoOpm_ReturnsFalseWithParseError()
        {
            NdmDocuments.WithTempFile(TwoOpmNdm(), file =>
            {
                var loaded = Opm.TryLoadFromFile(file, out var opm, out var result);

                Assert.False(loaded);
                Assert.Null(opm);
                var error = Assert.Single(result.Errors);
                Assert.Equal("PARSE_ERROR", error.Code);
                Assert.Contains("2 OPM messages", error.Message);
            });
        }

        [Fact]
        public void ValidateFile_NdmWithTwoOpm_IsInvalid()
        {
            NdmDocuments.WithTempFile(TwoOpmNdm(), file =>
            {
                var result = new OpmValidator().ValidateFile(file);

                Assert.False(result.IsValid);
                var error = Assert.Single(result.Errors);
                Assert.Contains("2 OPM messages", error.Message);
            });
        }

        [Fact]
        public void Read_NonNdmRootWithTwoNestedOpm_Throws()
        {
            var opm = XDocument.Parse(TwoOpmNdm()).Root.Elements().First();
            var xml = new XElement("wrapper", new XElement("a", new XElement(opm)), new XElement("b", new XElement(opm))).ToString();

            var ex = Assert.Throws<OpmParseException>(() => _reader.ReadFromString(xml));
            Assert.Contains("2 OPM messages", ex.Message);
        }

        #endregion

        #region Several segments or structural elements

        // An OPM holds one header and one body, the body one segment, the segment one metadata and one data section, and
        // the data at most one of each block but the maneuvers (NDM/XML 4.0.0, ndmxml-4.0.0-opm-3.0.xsd: opmType, opmBody,
        // opmSegment, opmData; CCSDS 502.0-B-3)
        private static readonly DateTime OpmEpoch = new(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        private static Opm MinimalOpm() =>
            Opm.CreateMinimal("OPM SAT", "2024-001A", "EARTH", "ICRF", "UTC", new OpmStateVector(OpmEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0));

        internal static Opm FullOpm()
        {
            var stateVector = new OpmStateVector(OpmEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var keplerian = OpmKeplerianElements.CreateWithTrueAnomaly(6778.137, 0.001, 51.6, 120.0, 90.0, 45.0, 398600.4418);
            var covariance = new CovarianceMatrix(
                cxX: 1.0e-6,
                cyX: 0.0, cyY: 1.0e-6,
                czX: 0.0, czY: 0.0, czZ: 1.0e-6,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9);
            var maneuvers = new[]
            {
                new OpmManeuverParameters(OpmEpoch.AddHours(1.0), 10.0, -1.0, "RSW", 0.001, 0.0, 0.0),
                new OpmManeuverParameters(OpmEpoch.AddHours(2.0), 10.0, -1.0, "RSW", 0.002, 0.0, 0.0)
            };
            var data = new OpmData(stateVector, keplerian, mass: 100.0, dragArea: 1.0, dragCoefficient: 2.2,
                covariance: covariance, maneuvers: maneuvers,
                userDefinedParameters: new OpmUserDefinedParameters(new System.Collections.Generic.Dictionary<string, string> { ["COLOR"] = "BLUE" }));
            return new Opm(CcsdsHeader.Create("TEST"), new OpmMetadata("OPM SAT", "2024-001A", "EARTH", "ICRF", "UTC"), data);
        }

        [Fact]
        public void TwoSegmentOpm_SchemaBoundsTheSegmentToOne()
        {
            // The qualified copy is rejected by the embedded SANA NDM/XML 4.0.0 schema, its one-segment original accepted
            var validator = new OpmValidator();
            var original = validator.ValidateSchemaFromXml(NdmDocuments.QualifiedMessage(MinimalOpm().ToXmlString(), "opm"));
            Assert.True(original.IsValid, string.Join("; ", original.Errors.Select(e => e.Message)));
            var qualified = validator.ValidateSchemaFromXml(NdmDocuments.QualifiedMessage(NdmDocuments.TwoSegmentOpm(MinimalOpm()), "opm"));
            Assert.False(qualified.IsValid);
            Assert.Contains(qualified.Errors, e => e.Message.Contains("segment"));

            // The unqualified document, the writer's form, is rejected by SANA's unqualified set too (third round of #668;
            // before, it matched no declaration and passed with warnings, the reader rejecting it)
            var unqualified = validator.ValidateSchemaFromXml(NdmDocuments.TwoSegmentOpm(MinimalOpm()));
            Assert.False(unqualified.IsValid);
            Assert.Contains(unqualified.Errors, e => e.Message.Contains("segment"));
        }

        [Theory]
        [InlineData("OpmReader.ReadFromString")]
        [InlineData("Opm.LoadFromString")]
        [InlineData("Opm.LoadFromString without schema validation")]
        [InlineData("Opm.LoadFromFile")]
        [InlineData("Opm.LoadFromStream")]
        [InlineData("OpmReader.ReadFromFile")]
        [InlineData("OpmReader.ReadFromStream")]
        public void Read_OpmWithTwoSegments_Throws(string path)
        {
            var xml = NdmDocuments.TwoSegmentOpm(MinimalOpm());

            Action read = path switch
            {
                "OpmReader.ReadFromString" => () => _reader.ReadFromString(xml),
                "Opm.LoadFromString" => () => Opm.LoadFromString(xml),
                "Opm.LoadFromFile" => () => NdmDocuments.WithTempFile(xml, file => Opm.LoadFromFile(file)),
                "Opm.LoadFromStream" => () => Opm.LoadFromStream(new MemoryStream(Encoding.UTF8.GetBytes(xml))),
                "OpmReader.ReadFromFile" => () => NdmDocuments.WithTempFile(xml, file => _reader.ReadFromFile(file)),
                "OpmReader.ReadFromStream" => () => _reader.ReadFromStream(new MemoryStream(Encoding.UTF8.GetBytes(xml))),
                _ => () => Opm.LoadFromString(xml, validateSchema: false)
            };

            // Before: the first segment, OPM SAT, the second dropped. Under default validation the schema rejects it first
            // (third round of #668); without it, and through the reader, the reader does
            if (path is "Opm.LoadFromString" or "Opm.LoadFromFile" or "Opm.LoadFromStream")
            {
                var validation = Assert.Throws<OpmValidationException>(read);
                Assert.Contains(validation.ValidationResult.Errors, e => e.Message.Contains("segment"));
                return;
            }

            var ex = Assert.Throws<OpmParseException>(read);
            Assert.Contains("2 segment elements", ex.Message);
        }

        [Theory]
        [InlineData("header")]
        [InlineData("body")]
        [InlineData("body/segment/metadata")]
        [InlineData("body/segment/data")]
        [InlineData("body/segment/data/stateVector")]
        [InlineData("body/segment/data/keplerianElements")]
        [InlineData("body/segment/data/spacecraftParameters")]
        [InlineData("body/segment/data/covarianceMatrix")]
        [InlineData("body/segment/data/userDefinedParameters")]
        public void Read_OpmWithDuplicateStructuralElement_Throws(string path)
        {
            var xml = FullOpm().ToXmlString();
            Assert.Equal(2, _reader.ReadFromString(xml).Data.Maneuvers.Count);
            var duplicated = NdmDocuments.WithDuplicatedElement(xml, "opm", path);

            var ex = Assert.Throws<OpmParseException>(() => _reader.ReadFromString(duplicated));
            Assert.Contains($"2 {path.Split('/')[^1]} elements", ex.Message);
            var exLoad = Assert.Throws<OpmParseException>(() => Opm.LoadFromString(duplicated, validateSchema: false, validateContent: false));
            Assert.Contains($"2 {path.Split('/')[^1]} elements", exLoad.Message);
        }

        [Fact]
        public void TryLoadFromFile_OpmWithTwoSegments_ReturnsFalseWithSchemaOrParseError()
        {
            NdmDocuments.WithTempFile(NdmDocuments.TwoSegmentOpm(MinimalOpm()), file =>
            {
                // The schema rejects the second segment (third round of #668: a PARSE_ERROR of the reader before)
                var loaded = Opm.TryLoadFromFile(file, out var opm, out var result);

                Assert.False(loaded);
                Assert.Null(opm);
                Assert.Contains(result.Errors, e => e.Code == "OPM006" && e.Message.Contains("segment"));

                // Without schema validation, the reader rejects it
                loaded = Opm.TryLoadFromFile(file, out opm, out result, validateSchema: false);

                Assert.False(loaded);
                Assert.Null(opm);
                var error = Assert.Single(result.Errors);
                Assert.Equal("PARSE_ERROR", error.Code);
                Assert.Contains("2 segment elements", error.Message);
            });
        }

        [Fact]
        public void ValidateFile_OpmWithTwoSegments_IsInvalid()
        {
            NdmDocuments.WithTempFile(NdmDocuments.TwoSegmentOpm(MinimalOpm()), file =>
            {
                var result = new OpmValidator().ValidateFile(file);

                // The schema rejects the second segment (third round of #668: the reader's "2 segment elements" before,
                // reported under the path "Parser" as "Failed to parse OPM: ...", the schema then checking no unqualified file)
                Assert.False(result.IsValid);
                var error = Assert.Single(result.Errors);
                Assert.Equal("OPM006", error.Code);
                Assert.Contains("'segment'", error.Message);
                Assert.DoesNotContain("Failed to parse", error.Message);
                Assert.StartsWith("Line ", error.Path);
            });
        }

        #endregion

        #region Keywords out of their range

        /// <summary>satellite_complete.xml unqualified (its namespace removed), which the NDM/XML 4.0.0 schema accepts.</summary>
        internal static string KeywordBaseOpm()
        {
            return NdmDocuments.InForm(
                File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CCSDS", "OPM", "satellite_complete.xml")),
                "opm", "unqualified-ndm");
        }

        /// <summary>
        /// The keywords of the OPM whose value the data model refuses, with that value, a second keyword set with it
        /// ("KEYWORD=value", or null), and whether the NDM/XML 4.0.0 schema rejects the document (CCSDS 502.0-B-3; the facets
        /// of ndmxml-4.0.0-common-4.0.xsd and -opm-3.0.xsd). The schema-valid rows are those where the schema type is wider
        /// than the data model: distanceType is xsd:double (the sign of SEMI_MAJOR_AXIS against ECCENTRICITY), MAN_REF_FRAME,
        /// OBJECT_NAME and ORIGINATOR xsd:string. MASS 0, a massType value (nonNegativeDouble), is read since #708.
        /// </summary>
        public static TheoryData<string, string, string?, bool> OpmKeywordsOutOfRange() => new()
        {
            { "ECCENTRICITY", "-0.5", null, true }, // nonNegativeDouble
            { "SEMI_MAJOR_AXIS", "-6878.137", null, false }, // distanceType: xsd:double; e < 1 needs a > 0
            { "SEMI_MAJOR_AXIS", "0", null, false },
            { "SEMI_MAJOR_AXIS", "7000", "ECCENTRICITY=1.5", false }, // e > 1 needs a < 0
            { "RA_OF_ASC_NODE", "360", null, true }, // angleType: angleRange [-360, 360) (#708: read before)
            { "ARG_OF_PERICENTER", "-360.5", null, true },
            { "TRUE_ANOMALY", "1000", null, true },
            { "INCLINATION", "-1", null, true }, // inclinationRange [0, 180]
            { "INCLINATION", "181", null, true },
            { "GM", "-1", null, true }, // gmType: positiveDouble
            { "GM", "0", null, true },
            { "MASS", "-1", null, true }, // massType: nonNegativeDouble
            { "SOLAR_RAD_AREA", "-1", null, true }, // areaType: nonNegativeDouble
            { "SOLAR_RAD_COEFF", "-1", null, true }, // nonNegativeDouble
            { "DRAG_AREA", "-1", null, true },
            { "DRAG_COEFF", "-1", null, true },
            { "MAN_DURATION", "-1", null, true }, // durationType: nonNegativeDouble
            { "MAN_DELTA_MASS", "1", null, true }, // deltamassTypeZ: nonPositiveDouble
            { "MAN_REF_FRAME", "", null, false }, // xsd:string
            { "OBJECT_NAME", "", null, false },
            { "ORIGINATOR", "", null, false }
        };

        /// <summary>The base document with the keyword's value replaced, and the second keyword's when given.</summary>
        internal static string OpmWithKeyword(string keyword, string value, string? with = null)
        {
            var xml = NdmDocuments.WithElementValue(KeywordBaseOpm(), keyword, value);
            if (with != null)
            {
                var parts = with.Split('=');
                xml = NdmDocuments.WithElementValue(xml, parts[0], parts[1]);
            }

            return xml;
        }

        /// <summary>The parse exception names the keyword and its value (and the second keyword's), and holds the data model's exception.</summary>
        internal static void AssertNamesKeyword(Exception ex, string keyword, string value, string? with = null)
        {
            Assert.Contains(keyword, ex.Message);
            Assert.Contains($"'{value}'", ex.Message);
            if (with != null)
            {
                var parts = with.Split('=');
                Assert.Contains(parts[0], ex.Message);
                Assert.Contains($"'{parts[1]}'", ex.Message);
            }

            Assert.IsAssignableFrom<ArgumentException>(ex.InnerException);
        }

        [Fact]
        public void KeywordBaseDocument_IsSchemaValidAndReads()
        {
            var xml = KeywordBaseOpm();
            var schema = new OpmValidator().ValidateSchemaFromXml(xml);
            Assert.True(schema.IsValid, string.Join("; ", schema.Errors.Select(e => e.Message)));
            Assert.Equal(1500.0, Opm.LoadFromString(xml).Data.Mass);
        }

        [Fact]
        public void ReadFromString_MassZero_ReadsZero()
        {
            // massType of ndmxml-4.0.0-common-4.0.xsd extends nonNegativeDouble (minInclusive 0.0); the schema, the oracle,
            // accepts the document. Before #708: OpmParseException "Mass must be positive", the data model requiring a mass
            // above 0 where the OMM's SpacecraftParameters, for the same keyword of the same type, accepts 0
            var xml = OpmWithKeyword("MASS", "0");
            Assert.True(new OpmValidator().ValidateSchemaFromXml(xml).IsValid);

            Assert.Equal(0.0, _reader.ReadFromString(xml).Data.Mass);
            Assert.Equal(0.0, Opm.LoadFromString(xml).Data.Mass);
            Assert.Equal(0.0, Opm.LoadFromString(xml, validateSchema: true, validateContent: false).Data.Mass);
            NdmDocuments.WithTempFile(xml, file =>
            {
                Assert.True(Opm.TryLoadFromFile(file, out var opm, out var result));
                Assert.Equal(0.0, opm.Data.Mass);
                Assert.True(result.IsValid);
                Assert.True(new OpmValidator().ValidateFile(file).IsValid);
            });

            // A spacecraft of mass 0, which CelestialItem allows
            var spacecraft = Opm.LoadFromString(xml).ToSpacecraft(-1000, 1000.0, new IO.Astrodynamics.Body.Spacecraft.Clock("OnboardClock", 256),
                new IO.Astrodynamics.Body.CelestialBody(IO.Astrodynamics.SolarSystemObjects.PlanetsAndMoons.EARTH));
            Assert.Equal(0.0, spacecraft.Mass);
        }

        [Theory]
        [InlineData("360")]
        [InlineData("-720")]
        public void ReadFromString_MeanAnomalyOutOfSchemaRange_ThrowsOpmParseExceptionNamingKeyword(string value)
        {
            // The base document with its TRUE_ANOMALY renamed MEAN_ANOMALY (the choice of keplerianElementsType):
            // angleType, so the schema, the oracle, rejects 360 and -720, which the reader read before #708
            var xml = NdmDocuments.WithElementValue(KeywordBaseOpm(), "TRUE_ANOMALY", value, renameTo: "MEAN_ANOMALY");
            Assert.False(new OpmValidator().ValidateSchemaFromXml(xml).IsValid);
            Assert.True(new OpmValidator().ValidateSchemaFromXml(
                NdmDocuments.WithElementValue(KeywordBaseOpm(), "TRUE_ANOMALY", "-10", renameTo: "MEAN_ANOMALY")).IsValid);

            AssertNamesKeyword(Assert.Throws<OpmParseException>(() => _reader.ReadFromString(xml)), "MEAN_ANOMALY", value);
            AssertNamesKeyword(Assert.Throws<OpmParseException>(() => Opm.LoadFromString(xml, validateSchema: false)), "MEAN_ANOMALY", value);
            Assert.Equal(-10.0, Opm.LoadFromString(
                NdmDocuments.WithElementValue(KeywordBaseOpm(), "TRUE_ANOMALY", "-10", renameTo: "MEAN_ANOMALY")).Data.KeplerianElements.MeanAnomaly);
        }

        [Theory]
        [MemberData(nameof(OpmKeywordsOutOfRange))]
        public void ReadFromString_KeywordOutOfRange_ThrowsOpmParseExceptionNamingKeywordAndValue(
            string keyword, string value, string? with, bool schemaRejects)
        {
            var xml = OpmWithKeyword(keyword, value, with);

            // The schema is the oracle of the value's validity where it has a facet; where it is wider the document is
            // schema-valid, and only the data model refuses it
            Assert.Equal(schemaRejects, !new OpmValidator().ValidateSchemaFromXml(xml).IsValid);

            // Before: the ArgumentOutOfRangeException or ArgumentException of the data model's constructor
            AssertNamesKeyword(Assert.Throws<OpmParseException>(() => _reader.ReadFromString(xml)), keyword, value, with);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            AssertNamesKeyword(Assert.Throws<OpmParseException>(() => _reader.ReadFromStream(stream)), keyword, value, with);
            NdmDocuments.WithTempFile(xml, file =>
                AssertNamesKeyword(Assert.Throws<OpmParseException>(() => _reader.ReadFromFile(file)), keyword, value, with));
        }

        #endregion

        #region Non-finite values (#708)

        /// <summary>
        /// Keywords of the OPM with a text that double.TryParse (NumberStyles.Float, the invariant culture) reads as NaN or
        /// ±Infinity ("NaN", "Infinity" in any case, an overflow such as "1e400"). NaN and ±Infinity are not real numbers
        /// (IEEE 754). CCSDS 502.0-B-3 writes a KVN value in fixed-point or floating-point digits up to about 1.798E+308
        /// (7.5.5 to 7.5.7), which holds neither, but an ODM/XML value follows the xsd:double of XML Schema Part 2 (8.13.4),
        /// whose value space holds NaN and ±INF (CCSDS 502.0-B-3; confirmed by S. Guillet, 2026-11-17): the reader
        /// refuses them as a rule of the library, as #708 expects. The rows cover the state vector, the Keplerian
        /// elements, the spacecraft parameters, the covariance and the maneuvers, ranged or not by the data model.
        /// </summary>
        public static TheoryData<string, string> OpmNonFiniteValues() => new()
        {
            { "X", "NaN" },
            { "X", "Infinity" },
            { "X_DOT", "Infinity" },
            { "Z_DOT", "-Infinity" },
            { "SEMI_MAJOR_AXIS", "NaN" },
            { "SEMI_MAJOR_AXIS", "Infinity" },
            { "ECCENTRICITY", "NaN" },
            { "INCLINATION", "NaN" },
            { "TRUE_ANOMALY", "NaN" },
            { "GM", "NaN" },
            { "GM", "Infinity" },
            { "MASS", "NaN" },
            { "MASS", "infinity" },
            { "DRAG_COEFF", "1e400" },
            { "CX_X", "NaN" },
            { "CY_X", "NaN" },
            { "MAN_DURATION", "NaN" },
            { "MAN_DELTA_MASS", "NaN" },
            { "MAN_DV_1", "NaN" },
            { "MAN_DV_1", "1e400" }
        };

        /// <summary>The parse exception names the keyword and its text, and says that the value is not finite.</summary>
        internal static void AssertNamesNonFiniteKeyword(Exception ex, string keyword, string value)
        {
            Assert.Contains(keyword, ex.Message);
            Assert.Contains($"'{value}'", ex.Message);
            Assert.Contains("not a finite number", ex.Message);
        }

        [Theory]
        [MemberData(nameof(OpmNonFiniteValues))]
        public void ReadFromString_NonFiniteValue_ThrowsOpmParseExceptionNamingKeywordAndValue(string keyword, string value)
        {
            var xml = OpmWithKeyword(keyword, value);

            // Before: the document loaded, the keyword holding NaN or ±Infinity, which every range check passed
            AssertNamesNonFiniteKeyword(Assert.Throws<OpmParseException>(() => _reader.ReadFromString(xml)), keyword, value);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            AssertNamesNonFiniteKeyword(Assert.Throws<OpmParseException>(() => _reader.ReadFromStream(stream)), keyword, value);
            NdmDocuments.WithTempFile(xml, file =>
                AssertNamesNonFiniteKeyword(Assert.Throws<OpmParseException>(() => _reader.ReadFromFile(file)), keyword, value));
        }

        [Theory]
        [InlineData("X", "INF")] // the lexical INF of xsd:double, which double.TryParse rejects (as before)
        [InlineData("MASS", "-INF")]
        public void ReadFromString_XsdInfinityLiteral_StillThrowsOpmParseException(string keyword, string value)
        {
            var ex = Assert.Throws<OpmParseException>(() => _reader.ReadFromString(OpmWithKeyword(keyword, value)));
            Assert.Contains(keyword, ex.Message);
            Assert.Contains($"'{value}'", ex.Message);
        }

        [Theory]
        [InlineData("X", "-0")]
        [InlineData("X", "1.0e-300")]
        [InlineData("X", "4.9e-324")] // the least subnormal, finite
        [InlineData("X_DOT", "-1.7976931348623156e305")] // the largest magnitude finite in m/s (#729)
        public void ReadFromString_FiniteExtremeValue_ReadsTheValueOfTheText(string keyword, string value)
        {
            var opm = _reader.ReadFromString(OpmWithKeyword(keyword, value));
            double expected = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            double actual = keyword == "X" ? opm.Data.StateVector.X : opm.Data.StateVector.XDot;
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        }

        [Theory]
        [InlineData("X", "1e306")]
        [InlineData("Z", "-1.797693134862316e305")] // the next double after the largest magnitude finite in m
        [InlineData("X_DOT", "-1.7976931348623157e308")] // -double.MaxValue km/s
        [InlineData("Z_DOT", "1e306")]
        [InlineData("CX_X", "1e303")]
        [InlineData("CY_X", "-1.797693134862316e302")] // the next double after the largest magnitude finite in m²
        [InlineData("CZ_DOT_Z_DOT", "1.7976931348623157e308")]
        public void ReadFromString_FiniteValueOverflowingInSiUnits_ThrowsOpmParseException(string keyword, string value)
        {
            // #729: a finite km, km/s, km², km²/s or km²/s² text whose product by 1000 or 1e6 is beyond the range of a double
            // loaded, and Opm.ToStateVector gave ±Infinity in its state or covariance; refused as the CDM reader refuses it
            // (#708), naming the keyword and its text
            var xml = OpmWithKeyword(keyword, value);
            double factor = keyword.StartsWith("C", StringComparison.Ordinal) ? 1e6 : 1000.0;
            Assert.False(double.IsFinite(double.Parse(value, System.Globalization.CultureInfo.InvariantCulture) * factor));

            void AssertNamesKeyword(OpmParseException ex)
            {
                Assert.Contains(keyword, ex.Message);
                Assert.Contains($"'{value}'", ex.Message);
                Assert.Contains("SI units", ex.Message);
                Assert.IsType<ArgumentOutOfRangeException>(ex.InnerException);
            }

            AssertNamesKeyword(Assert.Throws<OpmParseException>(() => _reader.ReadFromString(xml)));
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            AssertNamesKeyword(Assert.Throws<OpmParseException>(() => _reader.ReadFromStream(stream)));
            NdmDocuments.WithTempFile(xml, file => AssertNamesKeyword(Assert.Throws<OpmParseException>(() => _reader.ReadFromFile(file))));
        }

        #endregion
    }
}
