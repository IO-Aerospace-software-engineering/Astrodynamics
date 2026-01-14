// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
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
    }
}
