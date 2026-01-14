// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OMM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OMM
{
    public class OmmWriterTests
    {
        private readonly DateTime _testEpoch = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        private readonly OmmWriter _writer = new OmmWriter();
        private readonly OmmReader _reader = new OmmReader();

        [Fact]
        public void WriteToString_MinimalOmm_GeneratesValidXml()
        {
            var omm = CreateMinimalOmm();

            var xml = _writer.WriteToString(omm);

            Assert.NotNull(xml);
            Assert.Contains("<omm", xml);
            Assert.Contains("CCSDS_OMM_VERS", xml);
            Assert.Contains("version=\"3.0\"", xml);
            Assert.Contains("<header>", xml);
            Assert.Contains("<body>", xml);
            Assert.Contains("<segment>", xml);
            Assert.Contains("<metadata>", xml);
            Assert.Contains("<data>", xml);
            Assert.Contains("<meanElements>", xml);
        }

        [Fact]
        public void WriteToString_WithNdmWrapper_GeneratesNdmRoot()
        {
            var omm = CreateMinimalOmm();
            _writer.WrapInNdmContainer = true;

            var xml = _writer.WriteToString(omm);

            Assert.Contains("<ndm", xml);
            Assert.Contains("</ndm>", xml);
        }

        [Fact]
        public void WriteToString_WithoutNdmWrapper_GeneratesOmmRoot()
        {
            var omm = CreateMinimalOmm();
            _writer.WrapInNdmContainer = false;

            var xml = _writer.WriteToString(omm);

            Assert.DoesNotContain("<ndm", xml);
            Assert.StartsWith("<?xml", xml);
        }

        [Fact]
        public void WriteToString_NullOmm_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _writer.WriteToString(null));
        }

        [Fact]
        public void WriteToFile_NullOmm_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _writer.WriteToFile(null, "test.omm"));
        }

        [Fact]
        public void WriteToFile_NullPath_ThrowsArgumentException()
        {
            var omm = CreateMinimalOmm();
            Assert.Throws<ArgumentException>(() => _writer.WriteToFile(omm, null));
        }

        [Fact]
        public void WriteToFile_EmptyPath_ThrowsArgumentException()
        {
            var omm = CreateMinimalOmm();
            Assert.Throws<ArgumentException>(() => _writer.WriteToFile(omm, ""));
        }

        [Fact]
        public void WriteToStream_NullOmm_ThrowsArgumentNullException()
        {
            using var stream = new MemoryStream();
            Assert.Throws<ArgumentNullException>(() => _writer.WriteToStream(null, stream));
        }

        [Fact]
        public void WriteToStream_NullStream_ThrowsArgumentNullException()
        {
            var omm = CreateMinimalOmm();
            Assert.Throws<ArgumentNullException>(() => _writer.WriteToStream(omm, null));
        }

        [Fact]
        public void RoundTrip_MinimalOmm_PreservesData()
        {
            var original = CreateMinimalOmm();

            var xml = _writer.WriteToString(original);
            var parsed = _reader.ReadFromString(xml);

            Assert.Equal(original.ObjectName, parsed.ObjectName);
            Assert.Equal(original.ObjectId, parsed.ObjectId);
            Assert.Equal(original.Metadata.CenterName, parsed.Metadata.CenterName);
            Assert.Equal(original.Metadata.ReferenceFrame, parsed.Metadata.ReferenceFrame);
            Assert.Equal(original.Metadata.TimeSystem, parsed.Metadata.TimeSystem);
            Assert.Equal(original.Metadata.MeanElementTheory, parsed.Metadata.MeanElementTheory);
        }

        [Fact]
        public void RoundTrip_MeanElements_PreservesData()
        {
            var original = CreateMinimalOmm();

            var xml = _writer.WriteToString(original);
            var parsed = _reader.ReadFromString(xml);

            Assert.Equal(original.Data.MeanElements.Epoch, parsed.Data.MeanElements.Epoch);
            Assert.Equal(original.Data.MeanElements.MeanMotion.Value, parsed.Data.MeanElements.MeanMotion.Value, 10);
            Assert.Equal(original.Data.MeanElements.Eccentricity, parsed.Data.MeanElements.Eccentricity, 10);
            Assert.Equal(original.Data.MeanElements.Inclination, parsed.Data.MeanElements.Inclination, 10);
            Assert.Equal(original.Data.MeanElements.RightAscensionOfAscendingNode,
                parsed.Data.MeanElements.RightAscensionOfAscendingNode, 10);
            Assert.Equal(original.Data.MeanElements.ArgumentOfPericenter,
                parsed.Data.MeanElements.ArgumentOfPericenter, 10);
            Assert.Equal(original.Data.MeanElements.MeanAnomaly, parsed.Data.MeanElements.MeanAnomaly, 10);
        }

        [Fact]
        public void RoundTrip_WithSemiMajorAxis_PreservesData()
        {
            var meanElements = MeanElements.CreateWithSemiMajorAxis(
                _testEpoch, 7000.0, 0.001, 51.6, 120.0, 90.0, 45.0, 398600.4418);
            var omm = Omm.CreateMinimal("TEST", "2024-001A", "EARTH", "ICRF", "TDB", "DSST", meanElements);

            var xml = _writer.WriteToString(omm);
            var parsed = _reader.ReadFromString(xml);

            Assert.False(parsed.Data.MeanElements.UsesMeanMotion);
            Assert.Equal(7000.0, parsed.Data.MeanElements.SemiMajorAxis.Value, 6);
            Assert.Equal(398600.4418, parsed.Data.MeanElements.GravitationalParameter.Value, 4);
        }

        [Fact]
        public void RoundTrip_TleParameters_PreservesData()
        {
            var original = Omm.CreateForTle(
                "ISS", "1998-067A", _testEpoch,
                15.49309423, 0.0000493, 51.6423, 353.0312, 320.8755, 39.2360,
                0.0001027, 0.00016717, 0.0,
                noradCatalogId: 25544, elementSetNumber: 999, revolutionNumber: 25703);

            var xml = _writer.WriteToString(original);
            var parsed = _reader.ReadFromString(xml);

            Assert.True(parsed.IsTleCompatible);
            Assert.Equal(original.Data.TleParameters.BStar.Value, parsed.Data.TleParameters.BStar.Value, 10);
            Assert.Equal(original.Data.TleParameters.MeanMotionDot, parsed.Data.TleParameters.MeanMotionDot, 10);
            Assert.Equal(original.Data.TleParameters.MeanMotionDDot.Value, parsed.Data.TleParameters.MeanMotionDDot.Value, 10);
            Assert.Equal(original.Data.TleParameters.NoradCatalogId, parsed.Data.TleParameters.NoradCatalogId);
            Assert.Equal(original.Data.TleParameters.ElementSetNumber, parsed.Data.TleParameters.ElementSetNumber);
            Assert.Equal(original.Data.TleParameters.RevolutionNumberAtEpoch,
                parsed.Data.TleParameters.RevolutionNumberAtEpoch);
        }

        [Fact]
        public void RoundTrip_SpacecraftParameters_PreservesData()
        {
            var meanElements = MeanElements.CreateWithMeanMotion(
                _testEpoch, 15.5, 0.001, 51.6, 120.0, 90.0, 45.0);
            var spacecraftParams = new SpacecraftParameters(1500.0, 25.0, 1.2, 20.0, 2.2);
            var header = CcsdsHeader.CreateDefault();
            var metadata = OmmMetadata.CreateForSgp4("TEST", "2024-001A");
            var data = new OmmData(meanElements, spacecraftParams);
            var original = new Omm(header, metadata, data);

            var xml = _writer.WriteToString(original);
            var parsed = _reader.ReadFromString(xml);

            Assert.True(parsed.Data.HasSpacecraftParameters);
            Assert.Equal(1500.0, parsed.Data.SpacecraftParameters.Mass.Value, 6);
            Assert.Equal(25.0, parsed.Data.SpacecraftParameters.SolarRadArea.Value, 6);
            Assert.Equal(1.2, parsed.Data.SpacecraftParameters.SolarRadCoeff.Value, 6);
            Assert.Equal(20.0, parsed.Data.SpacecraftParameters.DragArea.Value, 6);
            Assert.Equal(2.2, parsed.Data.SpacecraftParameters.DragCoeff.Value, 6);
        }

        [Fact]
        public void RoundTrip_UserDefinedParameters_PreservesData()
        {
            var meanElements = MeanElements.CreateWithMeanMotion(
                _testEpoch, 15.5, 0.001, 51.6, 120.0, 90.0, 45.0);
            var userDefined = new UserDefinedParameters(new List<UserDefinedParameter>
            {
                new("MISSION", "ISS"),
                new("COUNTRY", "USA"),
                new("LAUNCH_DATE", "1998-11-20")
            });
            var header = CcsdsHeader.CreateDefault();
            var metadata = OmmMetadata.CreateForSgp4("TEST", "2024-001A");
            var data = new OmmData(meanElements, userDefinedParameters: userDefined);
            var original = new Omm(header, metadata, data);

            var xml = _writer.WriteToString(original);
            var parsed = _reader.ReadFromString(xml);

            Assert.True(parsed.Data.HasUserDefinedParameters);
            Assert.Equal("ISS", parsed.Data.UserDefinedParameters.GetValue("MISSION"));
            Assert.Equal("USA", parsed.Data.UserDefinedParameters.GetValue("COUNTRY"));
            Assert.Equal("1998-11-20", parsed.Data.UserDefinedParameters.GetValue("LAUNCH_DATE"));
        }

        [Fact]
        public void RoundTrip_Comments_PreservesData()
        {
            var headerComments = new List<string> { "Header comment 1", "Header comment 2" };
            var metadataComments = new List<string> { "Metadata comment" };
            var dataComments = new List<string> { "Data comment" };
            var meanElementsComments = new List<string> { "Mean elements comment" };

            var header = new CcsdsHeader(DateTime.UtcNow, "TEST", headerComments);
            var metadata = new OmmMetadata("TEST SAT", "2024-001A", "EARTH", "TEME", "UTC", "SGP4",
                comments: metadataComments);
            var meanElements = MeanElements.CreateWithMeanMotion(
                _testEpoch, 15.5, 0.001, 51.6, 120.0, 90.0, 45.0, comments: meanElementsComments);
            var data = new OmmData(meanElements, comments: dataComments);
            var original = new Omm(header, metadata, data);

            var xml = _writer.WriteToString(original);
            var parsed = _reader.ReadFromString(xml);

            Assert.Equal(2, parsed.Header.Comments.Count);
            Assert.Equal("Header comment 1", parsed.Header.Comments[0]);
            Assert.Single(parsed.Metadata.Comments);
            Assert.Single(parsed.Data.Comments);
            Assert.Single(parsed.Data.MeanElements.Comments);
        }

        [Fact]
        public void RoundTrip_H2ADEB_PreservesData()
        {
            // Read the actual H2ADEB file
            var original = _reader.ReadFromFile("CCSDS/H2ADEB.omm");

            // Write it back to string
            var xml = _writer.WriteToString(original);

            // Read the written XML
            var parsed = _reader.ReadFromString(xml);

            // ===== HEADER =====
            Assert.Equal(original.Header.Originator, parsed.Header.Originator);
            Assert.Equal(original.Header.CreationDate, parsed.Header.CreationDate);
            Assert.Equal(original.Header.Comments.Count, parsed.Header.Comments.Count);
            Assert.Equal(original.Header.Comments[0], parsed.Header.Comments[0]);

            // ===== METADATA =====
            Assert.Equal(original.ObjectName, parsed.ObjectName);
            Assert.Equal(original.ObjectId, parsed.ObjectId);
            Assert.Equal(original.Metadata.CenterName, parsed.Metadata.CenterName);
            Assert.Equal(original.Metadata.ReferenceFrame, parsed.Metadata.ReferenceFrame);
            Assert.Equal(original.Metadata.TimeSystem, parsed.Metadata.TimeSystem);
            Assert.Equal(original.Metadata.MeanElementTheory, parsed.Metadata.MeanElementTheory);

            // ===== MEAN ELEMENTS =====
            var origMean = original.Data.MeanElements;
            var parsedMean = parsed.Data.MeanElements;

            Assert.Equal(origMean.Epoch, parsedMean.Epoch);
            Assert.Equal(origMean.UsesMeanMotion, parsedMean.UsesMeanMotion);
            Assert.Equal(origMean.MeanMotion.Value, parsedMean.MeanMotion.Value);
            Assert.Equal(origMean.Eccentricity, parsedMean.Eccentricity);
            Assert.Equal(origMean.Inclination, parsedMean.Inclination);
            Assert.Equal(origMean.RightAscensionOfAscendingNode, parsedMean.RightAscensionOfAscendingNode);
            Assert.Equal(origMean.ArgumentOfPericenter, parsedMean.ArgumentOfPericenter);
            Assert.Equal(origMean.MeanAnomaly, parsedMean.MeanAnomaly);

            // ===== TLE PARAMETERS =====
            Assert.True(parsed.IsTleCompatible);
            Assert.True(parsed.Data.HasTleParameters);

            var origTle = original.Data.TleParameters;
            var parsedTle = parsed.Data.TleParameters;

            Assert.Equal(origTle.EphemerisType, parsedTle.EphemerisType);
            Assert.Equal(origTle.ClassificationType, parsedTle.ClassificationType);
            Assert.Equal(origTle.NoradCatalogId, parsedTle.NoradCatalogId);
            Assert.Equal(origTle.ElementSetNumber, parsedTle.ElementSetNumber);
            Assert.Equal(origTle.RevolutionNumberAtEpoch, parsedTle.RevolutionNumberAtEpoch);
            Assert.Equal(origTle.UsesBStar, parsedTle.UsesBStar);
            Assert.Equal(origTle.BStar.Value, parsedTle.BStar.Value);
            Assert.Equal(origTle.MeanMotionDot, parsedTle.MeanMotionDot);
            Assert.Equal(origTle.UsesMeanMotionDDot, parsedTle.UsesMeanMotionDDot);
            Assert.Equal(origTle.MeanMotionDDot.Value, parsedTle.MeanMotionDDot.Value);

            // ===== SPACECRAFT PARAMETERS (not present in H2ADEB) =====
            Assert.False(parsed.Data.HasSpacecraftParameters);

            // ===== COVARIANCE MATRIX (not present in H2ADEB) =====
            Assert.False(parsed.Data.HasCovarianceMatrix);

            // ===== USER-DEFINED PARAMETERS =====
            Assert.True(parsed.Data.HasUserDefinedParameters);

            var origUser = original.Data.UserDefinedParameters;
            var parsedUser = parsed.Data.UserDefinedParameters;

            Assert.Equal(origUser.Parameters.Count, parsedUser.Parameters.Count);
            Assert.Equal(origUser.GetValue("SEMIMAJOR_AXIS"), parsedUser.GetValue("SEMIMAJOR_AXIS"));
            Assert.Equal(origUser.GetValue("PERIOD"), parsedUser.GetValue("PERIOD"));
            Assert.Equal(origUser.GetValue("APOAPSIS"), parsedUser.GetValue("APOAPSIS"));
            Assert.Equal(origUser.GetValue("PERIAPSIS"), parsedUser.GetValue("PERIAPSIS"));
            Assert.Equal(origUser.GetValue("OBJECT_TYPE"), parsedUser.GetValue("OBJECT_TYPE"));
            Assert.Equal(origUser.GetValue("RCS_SIZE"), parsedUser.GetValue("RCS_SIZE"));
            Assert.Equal(origUser.GetValue("COUNTRY_CODE"), parsedUser.GetValue("COUNTRY_CODE"));
            Assert.Equal(origUser.GetValue("LAUNCH_DATE"), parsedUser.GetValue("LAUNCH_DATE"));
            Assert.Equal(origUser.GetValue("SITE"), parsedUser.GetValue("SITE"));
            Assert.Equal(origUser.GetValue("DECAY_DATE"), parsedUser.GetValue("DECAY_DATE"));
            Assert.Equal(origUser.GetValue("FILE"), parsedUser.GetValue("FILE"));
            Assert.Equal(origUser.GetValue("GP_ID"), parsedUser.GetValue("GP_ID"));
        }

        [Fact]
        public void WriteToFile_CreatesFile()
        {
            var omm = CreateMinimalOmm();
            var tempFile = Path.GetTempFileName() + ".omm";

            try
            {
                _writer.WriteToFile(omm, tempFile);

                Assert.True(File.Exists(tempFile));

                var content = File.ReadAllText(tempFile);
                Assert.Contains("<omm", content);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void WriteToStream_WritesToStream()
        {
            var omm = CreateMinimalOmm();

            using var stream = new MemoryStream();
            _writer.WriteToStream(omm, stream);

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();

            Assert.Contains("<omm", content);
        }

        [Fact]
        public void IndentOutput_True_FormatsWithIndentation()
        {
            var omm = CreateMinimalOmm();
            _writer.IndentOutput = true;

            var xml = _writer.WriteToString(omm);

            Assert.Contains("\n", xml);
            Assert.Contains("  <", xml); // Indented elements
        }

        [Fact]
        public void IndentOutput_False_NoIndentation()
        {
            var omm = CreateMinimalOmm();
            _writer.IndentOutput = false;

            var xml = _writer.WriteToString(omm);

            // Should have no indentation (no leading spaces before elements)
            // Note: may still have newlines depending on XML writer settings
            Assert.DoesNotContain("  <header>", xml);
        }

        private Omm CreateMinimalOmm()
        {
            return Omm.CreateForTle(
                "TEST SAT", "2024-001A", _testEpoch,
                15.5, 0.001, 51.6, 120.0, 90.0, 45.0,
                0.0001, 0.00001, 0.0,
                originator: "TEST");
        }
    }
}
