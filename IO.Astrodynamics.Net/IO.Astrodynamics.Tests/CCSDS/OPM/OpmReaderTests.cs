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

        private static Opm FullOpm()
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

            // The unqualified document, the writer's form, passes schema validation: the reader is the one that rejects it
            Assert.True(validator.ValidateSchemaFromXml(NdmDocuments.TwoSegmentOpm(MinimalOpm())).IsValid);
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

            // Before: the first segment, OPM SAT, the second dropped
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
        public void TryLoadFromFile_OpmWithTwoSegments_ReturnsFalseWithParseError()
        {
            NdmDocuments.WithTempFile(NdmDocuments.TwoSegmentOpm(MinimalOpm()), file =>
            {
                var loaded = Opm.TryLoadFromFile(file, out var opm, out var result);

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

                Assert.False(result.IsValid);
                var error = Assert.Single(result.Errors);
                Assert.Contains("2 segment elements", error.Message);
            });
        }

        #endregion
    }
}
