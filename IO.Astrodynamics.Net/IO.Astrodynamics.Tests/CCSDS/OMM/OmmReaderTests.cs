// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OMM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OMM
{
    public class OmmReaderTests
    {
        private const string TestOmmFilePath = "CCSDS/H2ADEB.omm";

        private readonly OmmReader _reader = new OmmReader();

        [Fact]
        public void ReadFromFile_H2ADEB_ParsesCorrectly()
        {
            var omm = _reader.ReadFromFile(TestOmmFilePath);

            Assert.NotNull(omm);
            Assert.NotNull(omm.Header);
            Assert.NotNull(omm.Metadata);
            Assert.NotNull(omm.Data);
        }

        [Fact]
        public void ReadFromFile_H2ADEB_ParsesHeaderCorrectly()
        {
            var omm = _reader.ReadFromFile(TestOmmFilePath);

            Assert.Equal("18 SPCS", omm.Header.Originator);
            Assert.Equal(new DateTime(2026, 1, 8, 1, 16, 23, DateTimeKind.Utc), omm.Header.CreationDate);
            Assert.Single(omm.Header.Comments);
            Assert.Equal("GENERATED VIA SPACE-TRACK.ORG API", omm.Header.Comments[0]);
        }

        [Fact]
        public void ReadFromFile_H2ADEB_ParsesMetadataCorrectly()
        {
            var omm = _reader.ReadFromFile(TestOmmFilePath);

            Assert.Equal("H-2A DEB", omm.Metadata.ObjectName);
            Assert.Equal("2017-082D", omm.Metadata.ObjectId);
            Assert.Equal("EARTH", omm.Metadata.CenterName);
            Assert.Equal("TEME", omm.Metadata.ReferenceFrame);
            Assert.Equal("UTC", omm.Metadata.TimeSystem);
            Assert.Equal("SGP4", omm.Metadata.MeanElementTheory);
        }

        [Fact]
        public void ReadFromFile_H2ADEB_ParsesMeanElementsCorrectly()
        {
            var omm = _reader.ReadFromFile(TestOmmFilePath);
            var meanElements = omm.Data.MeanElements;

            Assert.True(meanElements.UsesMeanMotion);
            Assert.Equal(16.44496501, meanElements.MeanMotion.Value, 8);
            Assert.Equal(0.00037100, meanElements.Eccentricity, 8);
            Assert.Equal(97.9775, meanElements.Inclination, 4);
            Assert.Equal(179.9880, meanElements.RightAscensionOfAscendingNode, 4);
            Assert.Equal(357.7541, meanElements.ArgumentOfPericenter, 4);
            Assert.Equal(2.3742, meanElements.MeanAnomaly, 4);

            // Check epoch
            var expectedEpoch = new DateTime(2026, 1, 8, 0, 23, 28, 41, DateTimeKind.Utc).AddTicks(7920);
            Assert.Equal(expectedEpoch.Year, meanElements.Epoch.Year);
            Assert.Equal(expectedEpoch.Month, meanElements.Epoch.Month);
            Assert.Equal(expectedEpoch.Day, meanElements.Epoch.Day);
            Assert.Equal(expectedEpoch.Hour, meanElements.Epoch.Hour);
            Assert.Equal(expectedEpoch.Minute, meanElements.Epoch.Minute);
            Assert.Equal(expectedEpoch.Second, meanElements.Epoch.Second);
        }

        [Fact]
        public void ReadFromFile_H2ADEB_ParsesTleParametersCorrectly()
        {
            var omm = _reader.ReadFromFile(TestOmmFilePath);
            var tleParams = omm.Data.TleParameters;

            Assert.NotNull(tleParams);
            Assert.True(tleParams.UsesBStar);
            Assert.True(tleParams.UsesMeanMotionDDot);

            Assert.Equal(0, tleParams.EphemerisType);
            Assert.Equal("U", tleParams.ClassificationType);
            Assert.Equal(43068, tleParams.NoradCatalogId);
            Assert.Equal(999, tleParams.ElementSetNumber);
            Assert.Equal(43886, tleParams.RevolutionNumberAtEpoch);
            Assert.Equal(0.00044681, tleParams.BStar.Value, 10);
            Assert.Equal(0.13073258, tleParams.MeanMotionDot, 8);
            Assert.Equal(0.0000026736, tleParams.MeanMotionDDot.Value, 10);
        }

        [Fact]
        public void ReadFromFile_H2ADEB_ParsesUserDefinedParametersCorrectly()
        {
            var omm = _reader.ReadFromFile(TestOmmFilePath);
            var userDefined = omm.Data.UserDefinedParameters;

            Assert.NotNull(userDefined);
            Assert.True(userDefined.HasParameters);

            Assert.Equal("6532.005", userDefined.GetValue("SEMIMAJOR_AXIS"));
            Assert.Equal("87.565", userDefined.GetValue("PERIOD"));
            Assert.Equal("156.294", userDefined.GetValue("APOAPSIS"));
            Assert.Equal("151.447", userDefined.GetValue("PERIAPSIS"));
            Assert.Equal("DEBRIS", userDefined.GetValue("OBJECT_TYPE"));
            Assert.Equal("JPN", userDefined.GetValue("COUNTRY_CODE"));
            Assert.Equal("2017-12-23", userDefined.GetValue("LAUNCH_DATE"));
        }

        [Fact]
        public void ReadFromFile_H2ADEB_NoSpacecraftParameters()
        {
            var omm = _reader.ReadFromFile(TestOmmFilePath);

            Assert.False(omm.Data.HasSpacecraftParameters);
            Assert.Null(omm.Data.SpacecraftParameters);
        }

        [Fact]
        public void ReadFromFile_H2ADEB_NoCovarianceMatrix()
        {
            var omm = _reader.ReadFromFile(TestOmmFilePath);

            Assert.False(omm.Data.HasCovarianceMatrix);
            Assert.Null(omm.Data.CovarianceMatrix);
        }

        [Fact]
        public void ReadFromFile_H2ADEB_IsTleCompatible()
        {
            var omm = _reader.ReadFromFile(TestOmmFilePath);

            Assert.True(omm.IsTleCompatible);
        }

        [Fact]
        public void ReadFromFile_NullPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _reader.ReadFromFile(null));
        }

        [Fact]
        public void ReadFromFile_EmptyPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _reader.ReadFromFile(""));
        }

        [Fact]
        public void ReadFromFile_NonExistentFile_ThrowsFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(() => _reader.ReadFromFile("nonexistent.omm"));
        }

        [Fact]
        public void ReadFromStream_NullStream_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _reader.ReadFromStream(null));
        }

        [Fact]
        public void ReadFromString_NullString_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _reader.ReadFromString(null));
        }

        [Fact]
        public void ReadFromString_EmptyString_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _reader.ReadFromString(""));
        }

        [Fact]
        public void ReadFromString_InvalidXml_ThrowsOmmParseException()
        {
            Assert.Throws<OmmParseException>(() => _reader.ReadFromString("not valid xml"));
        }

        [Fact]
        public void ReadFromString_MissingOmmElement_ThrowsOmmParseException()
        {
            var xml = "<root><child>value</child></root>";
            Assert.Throws<OmmParseException>(() => _reader.ReadFromString(xml));
        }

        [Fact]
        public void ReadFromString_MinimalValidOmm_ParsesCorrectly()
        {
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<omm id=""CCSDS_OMM_VERS"" version=""3.0"">
  <header>
    <CREATION_DATE>2024-01-15T12:00:00</CREATION_DATE>
    <ORIGINATOR>TEST</ORIGINATOR>
  </header>
  <body>
    <segment>
      <metadata>
        <OBJECT_NAME>TEST SAT</OBJECT_NAME>
        <OBJECT_ID>2024-001A</OBJECT_ID>
        <CENTER_NAME>EARTH</CENTER_NAME>
        <REF_FRAME>TEME</REF_FRAME>
        <TIME_SYSTEM>UTC</TIME_SYSTEM>
        <MEAN_ELEMENT_THEORY>SGP4</MEAN_ELEMENT_THEORY>
      </metadata>
      <data>
        <meanElements>
          <EPOCH>2024-01-15T12:00:00</EPOCH>
          <MEAN_MOTION>15.5</MEAN_MOTION>
          <ECCENTRICITY>0.001</ECCENTRICITY>
          <INCLINATION>51.6</INCLINATION>
          <RA_OF_ASC_NODE>120.0</RA_OF_ASC_NODE>
          <ARG_OF_PERICENTER>90.0</ARG_OF_PERICENTER>
          <MEAN_ANOMALY>45.0</MEAN_ANOMALY>
        </meanElements>
      </data>
    </segment>
  </body>
</omm>";

            var omm = _reader.ReadFromString(xml);

            Assert.Equal("TEST SAT", omm.ObjectName);
            Assert.Equal("2024-001A", omm.ObjectId);
            Assert.Equal("TEST", omm.Header.Originator);
            Assert.Equal(15.5, omm.Data.MeanElements.MeanMotion);
            Assert.False(omm.IsTleCompatible);
        }

        [Fact]
        public void ReadFromString_WithSemiMajorAxis_ParsesCorrectly()
        {
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<omm id=""CCSDS_OMM_VERS"" version=""3.0"">
  <header>
    <CREATION_DATE>2024-01-15T12:00:00</CREATION_DATE>
    <ORIGINATOR>TEST</ORIGINATOR>
  </header>
  <body>
    <segment>
      <metadata>
        <OBJECT_NAME>TEST SAT</OBJECT_NAME>
        <OBJECT_ID>2024-001A</OBJECT_ID>
        <CENTER_NAME>EARTH</CENTER_NAME>
        <REF_FRAME>ICRF</REF_FRAME>
        <TIME_SYSTEM>TDB</TIME_SYSTEM>
        <MEAN_ELEMENT_THEORY>DSST</MEAN_ELEMENT_THEORY>
      </metadata>
      <data>
        <meanElements>
          <EPOCH>2024-01-15T12:00:00</EPOCH>
          <SEMI_MAJOR_AXIS>7000.0</SEMI_MAJOR_AXIS>
          <ECCENTRICITY>0.001</ECCENTRICITY>
          <INCLINATION>51.6</INCLINATION>
          <RA_OF_ASC_NODE>120.0</RA_OF_ASC_NODE>
          <ARG_OF_PERICENTER>90.0</ARG_OF_PERICENTER>
          <MEAN_ANOMALY>45.0</MEAN_ANOMALY>
          <GM>398600.4418</GM>
        </meanElements>
      </data>
    </segment>
  </body>
</omm>";

            var omm = _reader.ReadFromString(xml);

            Assert.False(omm.Data.MeanElements.UsesMeanMotion);
            Assert.Equal(7000.0, omm.Data.MeanElements.SemiMajorAxis);
            Assert.Equal(398600.4418, omm.Data.MeanElements.GravitationalParameter);
        }

        [Fact]
        public void ReadFromString_WithSpacecraftParameters_ParsesCorrectly()
        {
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<omm id=""CCSDS_OMM_VERS"" version=""3.0"">
  <header>
    <CREATION_DATE>2024-01-15T12:00:00</CREATION_DATE>
    <ORIGINATOR>TEST</ORIGINATOR>
  </header>
  <body>
    <segment>
      <metadata>
        <OBJECT_NAME>TEST SAT</OBJECT_NAME>
        <OBJECT_ID>2024-001A</OBJECT_ID>
        <CENTER_NAME>EARTH</CENTER_NAME>
        <REF_FRAME>TEME</REF_FRAME>
        <TIME_SYSTEM>UTC</TIME_SYSTEM>
        <MEAN_ELEMENT_THEORY>SGP4</MEAN_ELEMENT_THEORY>
      </metadata>
      <data>
        <meanElements>
          <EPOCH>2024-01-15T12:00:00</EPOCH>
          <MEAN_MOTION>15.5</MEAN_MOTION>
          <ECCENTRICITY>0.001</ECCENTRICITY>
          <INCLINATION>51.6</INCLINATION>
          <RA_OF_ASC_NODE>120.0</RA_OF_ASC_NODE>
          <ARG_OF_PERICENTER>90.0</ARG_OF_PERICENTER>
          <MEAN_ANOMALY>45.0</MEAN_ANOMALY>
        </meanElements>
        <spacecraftParameters>
          <MASS>1500.0</MASS>
          <SOLAR_RAD_AREA>25.0</SOLAR_RAD_AREA>
          <SOLAR_RAD_COEFF>1.2</SOLAR_RAD_COEFF>
          <DRAG_AREA>20.0</DRAG_AREA>
          <DRAG_COEFF>2.2</DRAG_COEFF>
        </spacecraftParameters>
      </data>
    </segment>
  </body>
</omm>";

            var omm = _reader.ReadFromString(xml);

            Assert.True(omm.Data.HasSpacecraftParameters);
            Assert.Equal(1500.0, omm.Data.SpacecraftParameters.Mass);
            Assert.Equal(25.0, omm.Data.SpacecraftParameters.SolarRadArea);
            Assert.Equal(1.2, omm.Data.SpacecraftParameters.SolarRadCoeff);
            Assert.Equal(20.0, omm.Data.SpacecraftParameters.DragArea);
            Assert.Equal(2.2, omm.Data.SpacecraftParameters.DragCoeff);
        }

        [Fact]
        public void ReadFromString_WithComments_ParsesCorrectly()
        {
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<omm id=""CCSDS_OMM_VERS"" version=""3.0"">
  <header>
    <COMMENT>Header comment 1</COMMENT>
    <COMMENT>Header comment 2</COMMENT>
    <CREATION_DATE>2024-01-15T12:00:00</CREATION_DATE>
    <ORIGINATOR>TEST</ORIGINATOR>
  </header>
  <body>
    <segment>
      <metadata>
        <COMMENT>Metadata comment</COMMENT>
        <OBJECT_NAME>TEST SAT</OBJECT_NAME>
        <OBJECT_ID>2024-001A</OBJECT_ID>
        <CENTER_NAME>EARTH</CENTER_NAME>
        <REF_FRAME>TEME</REF_FRAME>
        <TIME_SYSTEM>UTC</TIME_SYSTEM>
        <MEAN_ELEMENT_THEORY>SGP4</MEAN_ELEMENT_THEORY>
      </metadata>
      <data>
        <COMMENT>Data comment</COMMENT>
        <meanElements>
          <COMMENT>Mean elements comment</COMMENT>
          <EPOCH>2024-01-15T12:00:00</EPOCH>
          <MEAN_MOTION>15.5</MEAN_MOTION>
          <ECCENTRICITY>0.001</ECCENTRICITY>
          <INCLINATION>51.6</INCLINATION>
          <RA_OF_ASC_NODE>120.0</RA_OF_ASC_NODE>
          <ARG_OF_PERICENTER>90.0</ARG_OF_PERICENTER>
          <MEAN_ANOMALY>45.0</MEAN_ANOMALY>
        </meanElements>
      </data>
    </segment>
  </body>
</omm>";

            var omm = _reader.ReadFromString(xml);

            Assert.Equal(2, omm.Header.Comments.Count);
            Assert.Equal("Header comment 1", omm.Header.Comments[0]);
            Assert.Single(omm.Metadata.Comments);
            Assert.Equal("Metadata comment", omm.Metadata.Comments[0]);
            Assert.Single(omm.Data.Comments);
            Assert.Single(omm.Data.MeanElements.Comments);
        }

        [Fact]
        public void ReadFromStream_ValidStream_ParsesCorrectly()
        {
            using var stream = File.OpenRead(TestOmmFilePath);
            var omm = _reader.ReadFromStream(stream);

            Assert.NotNull(omm);
            Assert.Equal("H-2A DEB", omm.ObjectName);
        }

        #region Several messages (#679)

        [Theory]
        [InlineData("OmmReader.ReadFromString")]
        [InlineData("Omm.LoadFromString")]
        [InlineData("Omm.LoadFromFile")]
        [InlineData("Omm.LoadFromStream")]
        [InlineData("OmmReader.ReadFromFile")]
        [InlineData("OmmReader.ReadFromStream")]
        public void Read_NdmWithTwoOmm_Throws(string path)
        {
            var xml = NdmDocuments.TwoOmm();
            // The schema accepts the document, so the throw under default validation is the reader's
            Assert.True(new OmmValidator().ValidateSchemaFromXml(xml).IsValid);

            Action read = path switch
            {
                "OmmReader.ReadFromString" => () => _reader.ReadFromString(xml),
                "Omm.LoadFromString" => () => Omm.LoadFromString(xml),
                "Omm.LoadFromStream" => () => Omm.LoadFromStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml))),
                "OmmReader.ReadFromFile" => () => NdmDocuments.WithTempFile(xml, file => _reader.ReadFromFile(file)),
                "OmmReader.ReadFromStream" => () => _reader.ReadFromStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml))),
                _ => () => NdmDocuments.WithTempFile(xml, file => Omm.LoadFromFile(file))
            };

            var ex = Assert.Throws<OmmParseException>(read);
            Assert.Contains("2 OMM messages", ex.Message);
        }

        [Fact]
        public void Read_NonNdmRootWithTwoNestedOmm_Throws()
        {
            var xml = NdmDocuments.TwoNestedOmm();

            var ex = Assert.Throws<OmmParseException>(() => _reader.ReadFromString(xml));
            Assert.Contains("2 OMM messages", ex.Message);
        }

        // An NDM holding a second OMM below one of its children (a wrapper, an inner NDM) is not schema-valid, but holds
        // two messages all the same
        [Theory]
        [InlineData("wrapper")]
        [InlineData("ndm")]
        public void Read_NdmWithOneOmmAndOneNestedOmm_Throws(string inner)
        {
            var xml = NdmDocuments.OneOmmAndOneNestedOmm(inner);

            var ex = Assert.Throws<OmmParseException>(() => _reader.ReadFromString(xml));
            Assert.Contains("2 OMM messages", ex.Message);
            var exLoad = Assert.Throws<OmmParseException>(() => Omm.LoadFromString(xml, validateSchema: false));
            Assert.Contains("2 OMM messages", exLoad.Message);
        }

        // An NDM whose only OMM is below another element holds no OMM child: no OMM, as before 11.0.0
        [Fact]
        public void Read_NdmWithOnlyANestedOmm_FindsNoOmm()
        {
            var omm = XDocument.Load(NdmDocuments.IssOmmPath).Root!.Elements().Single(e => e.Name.LocalName == "omm");
            var xml = new XElement("ndm", new XElement("wrapper", new XElement(omm))).ToString();

            var ex = Assert.Throws<OmmParseException>(() => _reader.ReadFromString(xml));
            Assert.Equal("Could not find OMM element in document.", ex.Message);
        }

        [Fact]
        public void Read_MixedNdmOneOmmOneOpm_ReadsEach()
        {
            var opm = IO.Astrodynamics.CCSDS.OPM.Opm.CreateMinimal("OPM SAT", "2024-001A", "EARTH", "ICRF", "UTC",
                new IO.Astrodynamics.CCSDS.OPM.OpmStateVector(new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                    6778.137, 0.0, 0.0, 0.0, 7.5, 0.0));
            var xml = NdmDocuments.OneOmmOneOpm(opm);

            Assert.Equal("ISS (ZARYA)", _reader.ReadFromString(xml).ObjectName);
            Assert.Equal("OPM SAT", new IO.Astrodynamics.CCSDS.OPM.OpmReader().ReadFromString(xml).ObjectName);
        }

        #endregion

        #region Several segments or structural elements

        // An OMM holds one header and one body, the body one segment, the segment one metadata and one data section, and
        // the data at most one of each block (NDM/XML 4.0.0, ndmxml-4.0.0-omm-3.0.xsd: ommType, ommBody, ommSegment,
        // ommData; CCSDS 502.0-B-3)
        internal static Omm FullOmm()
        {
            var meanElements = MeanElements.CreateWithMeanMotion(
                new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), 15.5, 0.001, 51.6, 10.0, 20.0, 30.0);
            var tleParameters = TleParameters.CreateWithBStarAndDDot(1e-4, 0.0, 0.0, noradCatalogId: 12345,
                elementSetNumber: 1, revolutionNumberAtEpoch: 10);
            var covariance = new CovarianceMatrix(
                cxX: 1.0e-6,
                cyX: 0.0, cyY: 1.0e-6,
                czX: 0.0, czY: 0.0, czZ: 1.0e-6,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9);
            return new Omm(CcsdsHeader.Create("TEST"),
                new OmmMetadata("SAT", "2020-001A", "EARTH", "TEME", "UTC", "SGP4"),
                new OmmData(meanElements, new SpacecraftParameters(mass: 100.0, dragArea: 1.0, dragCoeff: 2.2), tleParameters,
                    covariance, new UserDefinedParameters(new[] { new UserDefinedParameter("COLOR", "BLUE") })));
        }

        [Fact]
        public void TwoSegmentOmm_SchemaBoundsTheSegmentToOne()
        {
            // The qualified copy of the two-segment document is rejected by the embedded SANA NDM/XML 4.0.0 schema, its
            // one-segment original accepted: the schema bounds the segment to one
            var validator = new OmmValidator();
            var original = validator.ValidateSchemaFromXml(NdmDocuments.QualifiedMessage(File.ReadAllText(NdmDocuments.IssOmmPath), "omm"));
            Assert.True(original.IsValid, string.Join("; ", original.Errors.Select(e => e.Message)));
            var qualified = validator.ValidateSchemaFromXml(NdmDocuments.QualifiedMessage(NdmDocuments.TwoSegmentOmm(), "omm"));
            Assert.False(qualified.IsValid);
            Assert.Contains(qualified.Errors, e => e.Message.Contains("segment"));

            // The unqualified document, Space-Track's form and the writer's, is rejected by SANA's unqualified set too (third
            // round of #668; before, it matched no declaration and passed with warnings, the reader rejecting it)
            var unqualified = validator.ValidateSchemaFromXml(NdmDocuments.TwoSegmentOmm());
            Assert.False(unqualified.IsValid);
            Assert.Contains(unqualified.Errors, e => e.Message.Contains("segment"));
        }

        [Theory]
        [InlineData("OmmReader.ReadFromString")]
        [InlineData("Omm.LoadFromString")]
        [InlineData("Omm.LoadFromString without schema validation")]
        [InlineData("Omm.LoadFromFile")]
        [InlineData("Omm.LoadFromStream")]
        [InlineData("OmmReader.ReadFromFile")]
        [InlineData("OmmReader.ReadFromStream")]
        public void Read_OmmWithTwoSegments_Throws(string path)
        {
            var xml = NdmDocuments.TwoSegmentOmm();

            Action read = path switch
            {
                "OmmReader.ReadFromString" => () => _reader.ReadFromString(xml),
                "Omm.LoadFromString" => () => Omm.LoadFromString(xml),
                "Omm.LoadFromString without schema validation" => () => Omm.LoadFromString(xml, validateSchema: false),
                "Omm.LoadFromStream" => () => Omm.LoadFromStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml))),
                "OmmReader.ReadFromFile" => () => NdmDocuments.WithTempFile(xml, file => _reader.ReadFromFile(file)),
                "OmmReader.ReadFromStream" => () => _reader.ReadFromStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml))),
                _ => () => NdmDocuments.WithTempFile(xml, file => Omm.LoadFromFile(file))
            };

            // Before: the first segment, ISS (ZARYA), the second dropped. Under default validation the schema rejects it first
            // (third round of #668); without it, and through the reader, the reader does
            if (path.StartsWith("Omm.LoadFrom", StringComparison.Ordinal) && !path.EndsWith("without schema validation", StringComparison.Ordinal))
            {
                var validation = Assert.Throws<OmmValidationException>(read);
                Assert.Contains(validation.ValidationResult.Errors, e => e.Message.Contains("segment"));
                return;
            }

            var ex = Assert.Throws<OmmParseException>(read);
            Assert.Contains("2 segment elements", ex.Message);
        }

        [Theory]
        [InlineData("header")]
        [InlineData("body")]
        [InlineData("body/segment/metadata")]
        [InlineData("body/segment/data")]
        [InlineData("body/segment/data/meanElements")]
        [InlineData("body/segment/data/spacecraftParameters")]
        [InlineData("body/segment/data/tleParameters")]
        [InlineData("body/segment/data/covarianceMatrix")]
        [InlineData("body/segment/data/userDefinedParameters")]
        public void Read_OmmWithDuplicateStructuralElement_Throws(string path)
        {
            var xml = FullOmm().ToXml();
            Assert.Equal("SAT", _reader.ReadFromString(xml).ObjectName);
            var duplicated = NdmDocuments.WithDuplicatedElement(xml, "omm", path);

            var ex = Assert.Throws<OmmParseException>(() => _reader.ReadFromString(duplicated));
            Assert.Contains($"2 {path.Split('/')[^1]} elements", ex.Message);
            var exLoad = Assert.Throws<OmmParseException>(() => Omm.LoadFromString(duplicated, validateSchema: false, validateContent: false));
            Assert.Contains($"2 {path.Split('/')[^1]} elements", exLoad.Message);
        }

        #endregion

        #region Keywords out of their range

        /// <summary>
        /// iss.omm (as Space-Track writes it) unqualified, its xsi attributes removed, with a GM after MEAN_ANOMALY and a
        /// spacecraftParameters block after meanElements, so that every keyword of the data model's range checks is present.
        /// The NDM/XML 4.0.0 schema accepts it (<see cref="KeywordBaseDocument_IsSchemaValidAndReads"/>).
        /// </summary>
        internal static string KeywordBaseOmm()
        {
            var document = XDocument.Parse(NdmDocuments.InForm(File.ReadAllText(NdmDocuments.IssOmmPath), "omm", "unqualified-ndm"));
            var meanElements = document.Descendants().Single(e => e.Name.LocalName == "meanElements");
            meanElements.Elements().Single(e => e.Name.LocalName == "MEAN_ANOMALY")
                .AddAfterSelf(new XElement("GM", new XAttribute("units", "km**3/s**2"), "398600.4418"));
            meanElements.AddAfterSelf(new XElement("spacecraftParameters",
                new XElement("MASS", new XAttribute("units", "kg"), "420000.0"),
                new XElement("SOLAR_RAD_AREA", new XAttribute("units", "m**2"), "2500.0"),
                new XElement("SOLAR_RAD_COEFF", "1.2"),
                new XElement("DRAG_AREA", new XAttribute("units", "m**2"), "2500.0"),
                new XElement("DRAG_COEFF", "2.2")));
            return document.ToString();
        }

        /// <summary>
        /// The keywords of the OMM whose value the data model refuses, with that value and whether the NDM/XML 4.0.0 schema
        /// (the oracle that the value is invalid where it has a facet) rejects it: CCSDS 502.0-B-3 and the facets of
        /// ndmxml-4.0.0-common-4.0.xsd and -omm-3.0.xsd. The schema-valid rows are those where the schema type is wider than
        /// the data model: revType and distanceType are xsd:double, the metadata and ORIGINATOR xsd:string, the
        /// USER_DEFINED parameter attribute xsd:string. "SEMI_MAJOR_AXIS" replaces MEAN_MOTION (the choice of meanElements).
        /// </summary>
        public static TheoryData<string, string, bool> OmmKeywordsOutOfRange() => new()
        {
            { "ECCENTRICITY", "-0.5", true }, // nonNegativeDouble
            { "INCLINATION", "-1", true }, // inclinationRange [0, 180]
            { "INCLINATION", "190", true },
            { "RA_OF_ASC_NODE", "360", true }, // angleRange [-360, 360)
            { "RA_OF_ASC_NODE", "-361", true },
            { "ARG_OF_PERICENTER", "360", true },
            { "ARG_OF_PERICENTER", "-361", true },
            { "MEAN_ANOMALY", "360", true },
            { "MEAN_ANOMALY", "-361", true },
            { "MEAN_MOTION", "0", false }, // revType: xsd:double
            { "MEAN_MOTION", "-1", false },
            { "SEMI_MAJOR_AXIS", "-7000", false }, // distanceType: xsd:double
            { "GM", "0", true }, // gmType: positiveDouble
            { "ELEMENT_SET_NO", "10000", true }, // elementSetNoType [0, 9999]
            { "REV_AT_EPOCH", "-1", true }, // xsd:nonNegativeInteger
            { "MASS", "-1", true }, // massType: nonNegativeDouble
            { "SOLAR_RAD_AREA", "-1", true }, // areaType: nonNegativeDouble
            { "SOLAR_RAD_COEFF", "-1", true }, // nonNegativeDouble
            { "DRAG_AREA", "-1", true },
            { "DRAG_COEFF", "-1", true },
            { "OBJECT_NAME", "", false }, // xsd:string
            { "OBJECT_ID", "", false },
            { "CENTER_NAME", "", false },
            { "REF_FRAME", "", false },
            { "TIME_SYSTEM", "", false },
            { "MEAN_ELEMENT_THEORY", "", false },
            { "ORIGINATOR", "", false },
            { "USER_DEFINED", " ", false } // the parameter attribute: xsd:string
        };

        /// <summary>The base document with the keyword's value replaced (for USER_DEFINED, the first parameter name).</summary>
        internal static string OmmWithKeyword(string keyword, string value)
        {
            var xml = KeywordBaseOmm();
            if (keyword == "USER_DEFINED")
            {
                var document = XDocument.Parse(xml);
                document.Descendants().First(e => e.Name.LocalName == "USER_DEFINED").SetAttributeValue("parameter", value);
                return document.ToString();
            }

            return keyword == "SEMI_MAJOR_AXIS"
                ? NdmDocuments.WithElementValue(xml, "MEAN_MOTION", value, renameTo: "SEMI_MAJOR_AXIS")
                : NdmDocuments.WithElementValue(xml, keyword, value);
        }

        /// <summary>The parse exception names the keyword and its value, and holds the data model's exception.</summary>
        internal static void AssertNamesKeyword(Exception ex, string keyword, string value)
        {
            Assert.Contains(keyword, ex.Message);
            Assert.Contains($"'{value}'", ex.Message);
            Assert.IsAssignableFrom<ArgumentException>(ex.InnerException);
        }

        [Fact]
        public void KeywordBaseDocument_IsSchemaValidAndReads()
        {
            var xml = KeywordBaseOmm();
            var schema = new OmmValidator().ValidateSchemaFromXml(xml);
            Assert.True(schema.IsValid, string.Join("; ", schema.Errors.Select(e => e.Message)));

            var omm = Omm.LoadFromString(xml);
            Assert.Equal(398600.4418, omm.Data.MeanElements.GravitationalParameter);
            Assert.Equal(420000.0, omm.Data.SpacecraftParameters.Mass);
        }

        [Theory]
        [MemberData(nameof(OmmKeywordsOutOfRange))]
        public void ReadFromString_KeywordOutOfRange_ThrowsOmmParseExceptionNamingKeywordAndValue(string keyword, string value, bool schemaRejects)
        {
            var xml = OmmWithKeyword(keyword, value);

            // The schema is the oracle of the value's validity where it has a facet; where it is wider (schemaRejects false)
            // the document is schema-valid, and only the data model refuses it
            Assert.Equal(schemaRejects, !new OmmValidator().ValidateSchemaFromXml(xml).IsValid);

            // Before: the ArgumentOutOfRangeException or ArgumentException of the data model's constructor, naming its
            // parameter (eccentricity, objectName, ...), not the reader's documented exception
            var ex = Assert.Throws<OmmParseException>(() => _reader.ReadFromString(xml));
            AssertNamesKeyword(ex, keyword, value);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
            AssertNamesKeyword(Assert.Throws<OmmParseException>(() => _reader.ReadFromStream(stream)), keyword, value);
            NdmDocuments.WithTempFile(xml, file =>
                AssertNamesKeyword(Assert.Throws<OmmParseException>(() => _reader.ReadFromFile(file)), keyword, value));
        }

        [Theory]
        [InlineData("ECCENTRICITY", "0")]
        [InlineData("INCLINATION", "0")]
        [InlineData("INCLINATION", "180")]
        [InlineData("RA_OF_ASC_NODE", "-360")]
        [InlineData("RA_OF_ASC_NODE", "359.999")]
        [InlineData("MASS", "0")]
        public void ReadFromString_KeywordAtRangeBoundary_Parses(string keyword, string value)
        {
            // The bounds of the schema's facets (nonNegativeDouble, inclinationRange, angleRange, massType) are accepted by
            // the reader, with the schema, and read exactly (the value of the text, a decimal that round-trips). The content
            // validation, a separate layer, is not run: it refuses RA_OF_ASC_NODE -360 and MASS 0, which the schema accepts
            var xml = OmmWithKeyword(keyword, value);
            Assert.True(new OmmValidator().ValidateSchemaFromXml(xml).IsValid);

            var omm = Omm.LoadFromString(xml, validateSchema: true, validateContent: false);
            Assert.Equal(omm.ToXml(), _reader.ReadFromString(xml).ToXml());

            double expected = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            double actual = keyword switch
            {
                "ECCENTRICITY" => omm.Data.MeanElements.Eccentricity,
                "INCLINATION" => omm.Data.MeanElements.Inclination,
                "RA_OF_ASC_NODE" => omm.Data.MeanElements.RightAscensionOfAscendingNode,
                _ => omm.Data.SpacecraftParameters.Mass!.Value
            };
            Assert.Equal(expected, actual);
        }

        #endregion

        #region Non-finite values (#708)

        /// <summary>
        /// Keywords of the OMM with a text that double.TryParse (NumberStyles.Float, the invariant culture) reads as NaN or
        /// ±Infinity: "NaN" and "Infinity" in any case, and an overflow ("1e400"), which .NET Core 3.0 and later round to
        /// ±Infinity. NaN and ±Infinity are not real numbers (IEEE 754), and the values of an OMM are real numbers in their
        /// units (CCSDS 502.0-B-3; to be verified by S. Guillet): the reader refuses them, as #708 expects. The rows cover
        /// the mean elements (ranged or not by the data model), GM, the spacecraft parameters and the TLE parameters, which
        /// the data model does not range-check.
        /// </summary>
        public static TheoryData<string, string> OmmNonFiniteValues() => new()
        {
            { "ECCENTRICITY", "NaN" },
            { "ECCENTRICITY", "Infinity" },
            { "MEAN_MOTION", "NaN" },
            { "MEAN_MOTION", "nan" },
            { "MEAN_MOTION", "Infinity" },
            { "MEAN_MOTION", "-Infinity" },
            { "MEAN_MOTION", "1e400" },
            { "INCLINATION", "NaN" },
            { "INCLINATION", "Infinity" },
            { "RA_OF_ASC_NODE", "NaN" },
            { "ARG_OF_PERICENTER", "NaN" },
            { "MEAN_ANOMALY", "NaN" },
            { "GM", "NaN" },
            { "MASS", "NaN" },
            { "DRAG_COEFF", "1e400" },
            { "BSTAR", "NaN" },
            { "BSTAR", "-1e400" },
            { "MEAN_MOTION_DOT", "Infinity" },
            { "MEAN_MOTION_DDOT", "NaN" }
        };

        /// <summary>The parse exception names the keyword and its text, and says that the value is not finite.</summary>
        internal static void AssertNamesNonFiniteKeyword(Exception ex, string keyword, string value)
        {
            Assert.Contains(keyword, ex.Message);
            Assert.Contains($"'{value}'", ex.Message);
            Assert.Contains("not a finite number", ex.Message);
        }

        [Theory]
        [MemberData(nameof(OmmNonFiniteValues))]
        public void ReadFromString_NonFiniteValue_ThrowsOmmParseExceptionNamingKeywordAndValue(string keyword, string value)
        {
            var xml = OmmWithKeyword(keyword, value);

            // Before: the document loaded, the keyword holding NaN or ±Infinity, which every range check passed
            AssertNamesNonFiniteKeyword(Assert.Throws<OmmParseException>(() => _reader.ReadFromString(xml)), keyword, value);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
            AssertNamesNonFiniteKeyword(Assert.Throws<OmmParseException>(() => _reader.ReadFromStream(stream)), keyword, value);
            NdmDocuments.WithTempFile(xml, file =>
                AssertNamesNonFiniteKeyword(Assert.Throws<OmmParseException>(() => _reader.ReadFromFile(file)), keyword, value));
        }

        [Theory]
        [InlineData("ECCENTRICITY", "INF")] // the lexical INF of xsd:double, which double.TryParse rejects (as before)
        [InlineData("MEAN_MOTION", "-INF")]
        public void ReadFromString_XsdInfinityLiteral_StillThrowsOmmParseException(string keyword, string value)
        {
            var ex = Assert.Throws<OmmParseException>(() => _reader.ReadFromString(OmmWithKeyword(keyword, value)));
            Assert.Contains(keyword, ex.Message);
            Assert.Contains($"'{value}'", ex.Message);
        }

        [Theory]
        [InlineData("ECCENTRICITY", "0")]
        [InlineData("ECCENTRICITY", "-0")]
        [InlineData("ECCENTRICITY", "1.0e-300")]
        [InlineData("ECCENTRICITY", "4.9e-324")] // the least subnormal, finite
        [InlineData("BSTAR", "1.7976931348623157e308")] // double.MaxValue, finite
        public void ReadFromString_FiniteExtremeValue_ReadsTheValueOfTheText(string keyword, string value)
        {
            var omm = _reader.ReadFromString(OmmWithKeyword(keyword, value));
            double expected = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            double actual = keyword == "BSTAR" ? omm.Data.TleParameters.BStar!.Value : omm.Data.MeanElements.Eccentricity;
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        }

        #endregion
    }
}
