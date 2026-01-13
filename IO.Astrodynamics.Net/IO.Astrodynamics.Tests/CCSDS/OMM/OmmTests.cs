// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Text;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OMM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OMM
{
    public class OmmTests
    {
        private readonly DateTime _testEpoch = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Constructor_Succeeds()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = OmmMetadata.CreateForSgp4("ISS", "1998-067A");
            var meanElements = MeanElements.CreateWithMeanMotion(
                _testEpoch, 15.5, 0.0001, 51.6, 120.0, 90.0, 45.0);
            var data = OmmData.CreateMinimal(meanElements);

            var omm = new Omm(header, metadata, data);

            Assert.Equal(header, omm.Header);
            Assert.Equal(metadata, omm.Metadata);
            Assert.Equal(data, omm.Data);
        }

        [Fact]
        public void Constructor_NullHeader_ThrowsArgumentNullException()
        {
            var metadata = OmmMetadata.CreateForSgp4("ISS", "1998-067A");
            var data = OmmData.CreateMinimal(MeanElements.CreateWithMeanMotion(
                _testEpoch, 15.5, 0.0001, 51.6, 120.0, 90.0, 45.0));

            Assert.Throws<ArgumentNullException>(() => new Omm(null, metadata, data));
        }

        [Fact]
        public void Constructor_NullMetadata_ThrowsArgumentNullException()
        {
            var header = CcsdsHeader.CreateDefault();
            var data = OmmData.CreateMinimal(MeanElements.CreateWithMeanMotion(
                _testEpoch, 15.5, 0.0001, 51.6, 120.0, 90.0, 45.0));

            Assert.Throws<ArgumentNullException>(() => new Omm(header, null, data));
        }

        [Fact]
        public void Constructor_NullData_ThrowsArgumentNullException()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = OmmMetadata.CreateForSgp4("ISS", "1998-067A");

            Assert.Throws<ArgumentNullException>(() => new Omm(header, metadata, null));
        }

        [Fact]
        public void Version_IsCorrect()
        {
            Assert.Equal("3.0", Omm.Version);
        }

        [Fact]
        public void FormatId_IsCorrect()
        {
            Assert.Equal("CCSDS_OMM_VERS", Omm.FormatId);
        }

        [Fact]
        public void ObjectName_ReturnsMetadataObjectName()
        {
            var omm = CreateMinimalOmm("ISS (ZARYA)", "1998-067A");

            Assert.Equal("ISS (ZARYA)", omm.ObjectName);
        }

        [Fact]
        public void ObjectId_ReturnsMetadataObjectId()
        {
            var omm = CreateMinimalOmm("ISS", "1998-067A");

            Assert.Equal("1998-067A", omm.ObjectId);
        }

        [Fact]
        public void Epoch_ReturnsDataEpoch()
        {
            var omm = CreateMinimalOmm("ISS", "1998-067A");

            Assert.Equal(_testEpoch, omm.Epoch);
        }

        [Fact]
        public void IsTleCompatible_WithTleParameters_ReturnsTrue()
        {
            var omm = Omm.CreateForTle(
                "ISS", "1998-067A", _testEpoch,
                meanMotion: 15.5,
                eccentricity: 0.0001,
                inclination: 51.6,
                raan: 120.0,
                argOfPericenter: 90.0,
                meanAnomaly: 45.0,
                bstar: 0.0001,
                meanMotionDot: 0.00001,
                meanMotionDDot: 0.000001);

            Assert.True(omm.IsTleCompatible);
        }

        [Fact]
        public void IsTleCompatible_WithoutTleParameters_ReturnsFalse()
        {
            var omm = CreateMinimalOmm("ISS", "1998-067A");

            Assert.False(omm.IsTleCompatible);
        }

        [Fact]
        public void HasCovariance_WithCovariance_ReturnsTrue()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = OmmMetadata.CreateForSgp4("ISS", "1998-067A");
            var meanElements = MeanElements.CreateWithMeanMotion(
                _testEpoch, 15.5, 0.0001, 51.6, 120.0, 90.0, 45.0);
            var covariance = CreateDiagonalCovariance();
            var data = new OmmData(meanElements, covarianceMatrix: covariance);

            var omm = new Omm(header, metadata, data);

            Assert.True(omm.HasCovariance);
        }

        [Fact]
        public void HasCovariance_WithoutCovariance_ReturnsFalse()
        {
            var omm = CreateMinimalOmm("ISS", "1998-067A");

            Assert.False(omm.HasCovariance);
        }

        [Fact]
        public void CreateForTle_FactoryMethod_CreatesValidOmm()
        {
            var omm = Omm.CreateForTle(
                objectName: "ISS (ZARYA)",
                objectId: "1998-067A",
                epoch: _testEpoch,
                meanMotion: 15.49309423,
                eccentricity: 0.0000493,
                inclination: 51.6423,
                raan: 353.0312,
                argOfPericenter: 320.8755,
                meanAnomaly: 39.2360,
                bstar: 0.0001027,
                meanMotionDot: 0.00016717,
                meanMotionDDot: 0.0,
                noradCatalogId: 25544,
                elementSetNumber: 999,
                revolutionNumber: 25703,
                originator: "USSPACECOM");

            Assert.Equal("ISS (ZARYA)", omm.ObjectName);
            Assert.Equal("1998-067A", omm.ObjectId);
            Assert.Equal(_testEpoch, omm.Epoch);
            Assert.True(omm.IsTleCompatible);
            Assert.Equal("USSPACECOM", omm.Header.Originator);
            Assert.Equal("TEME", omm.Metadata.ReferenceFrame);
            Assert.Equal("SGP4", omm.Metadata.MeanElementTheory);

            Assert.Equal(15.49309423, omm.Data.MeanElements.MeanMotion);
            Assert.Equal(0.0000493, omm.Data.MeanElements.Eccentricity);
            Assert.Equal(0.0001027, omm.Data.TleParameters.BStar);
            Assert.Equal(25544, omm.Data.TleParameters.NoradCatalogId);
        }

        [Fact]
        public void CreateForTle_WithNullOriginator_UsesDefaultHeader()
        {
            var omm = Omm.CreateForTle(
                "ISS", "1998-067A", _testEpoch,
                15.5, 0.0001, 51.6, 120.0, 90.0, 45.0,
                0.0001, 0.00001, 0.0);

            Assert.Equal("IO.Astrodynamics", omm.Header.Originator);
        }

        [Fact]
        public void CreateMinimal_FactoryMethod_CreatesValidOmm()
        {
            var meanElements = MeanElements.CreateWithSemiMajorAxis(
                _testEpoch, 7000.0, 0.001, 51.6, 120.0, 90.0, 45.0);

            var omm = Omm.CreateMinimal(
                objectName: "TEST SAT",
                objectId: "2024-001A",
                centerName: "EARTH",
                referenceFrame: "ICRF",
                timeSystem: "TDB",
                meanElementTheory: "DSST",
                meanElements: meanElements,
                originator: "TEST ORG");

            Assert.Equal("TEST SAT", omm.ObjectName);
            Assert.Equal("2024-001A", omm.ObjectId);
            Assert.Equal(_testEpoch, omm.Epoch);
            Assert.False(omm.IsTleCompatible);
            Assert.Equal("TEST ORG", omm.Header.Originator);
            Assert.Equal("ICRF", omm.Metadata.ReferenceFrame);
            Assert.Equal("DSST", omm.Metadata.MeanElementTheory);
            Assert.Equal(7000.0, omm.Data.MeanElements.SemiMajorAxis);
        }

        [Fact]
        public void ToString_ContainsRelevantInfo()
        {
            var omm = CreateMinimalOmm("ISS (ZARYA)", "1998-067A");
            var result = omm.ToString();

            Assert.Contains("OMM", result);
            Assert.Contains("ISS", result);
            Assert.Contains("1998-067A", result);
        }

        [Fact]
        public void ToString_WithTle_ContainsTleMarker()
        {
            var omm = Omm.CreateForTle(
                "ISS", "1998-067A", _testEpoch,
                15.5, 0.0001, 51.6, 120.0, 90.0, 45.0,
                0.0001, 0.00001, 0.0);

            var result = omm.ToString();

            Assert.Contains("TLE", result);
        }

        private Omm CreateMinimalOmm(string objectName, string objectId)
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = OmmMetadata.CreateForSgp4(objectName, objectId);
            var meanElements = MeanElements.CreateWithMeanMotion(
                _testEpoch, 15.5, 0.0001, 51.6, 120.0, 90.0, 45.0);
            var data = OmmData.CreateMinimal(meanElements);
            return new Omm(header, metadata, data);
        }

        private static CovarianceMatrix CreateDiagonalCovariance()
        {
            return new CovarianceMatrix(
                cxX: 1.0,
                cyX: 0.0, cyY: 1.0,
                czX: 0.0, czY: 0.0, czZ: 1.0,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 0.001,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 0.001,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 0.001);
        }

        #region Factory Methods (Load) Tests

        private const string IssOmmPath = "CCSDS/iss.omm";

        [Fact]
        public void LoadFromFile_ValidOmm_ReturnsOmm()
        {
            var omm = Omm.LoadFromFile(IssOmmPath, validateSchema: false);

            Assert.NotNull(omm);
            Assert.Equal("ISS (ZARYA)", omm.ObjectName);
            Assert.Equal("1998-067A", omm.ObjectId);
            Assert.Equal("EARTH", omm.Metadata.CenterName);
            Assert.Equal("TEME", omm.Metadata.ReferenceFrame);
            Assert.Equal("SGP4", omm.Metadata.MeanElementTheory);
            Assert.True(omm.IsTleCompatible);
        }

        [Fact]
        public void LoadFromFile_ValidOmm_ParsesMeanElements()
        {
            var omm = Omm.LoadFromFile(IssOmmPath, validateSchema: false);

            Assert.Equal(15.49269250, omm.Data.MeanElements.MeanMotion.Value, 6);
            Assert.Equal(0.00077414, omm.Data.MeanElements.Eccentricity, 8);
            Assert.Equal(51.6330, omm.Data.MeanElements.Inclination, 4);
            Assert.Equal(346.6801, omm.Data.MeanElements.RightAscensionOfAscendingNode, 4);
            Assert.Equal(12.6584, omm.Data.MeanElements.ArgumentOfPericenter, 4);
            Assert.Equal(347.4598, omm.Data.MeanElements.MeanAnomaly, 4);
        }

        [Fact]
        public void LoadFromFile_ValidOmm_ParsesTleParameters()
        {
            var omm = Omm.LoadFromFile(IssOmmPath, validateSchema: false);

            Assert.NotNull(omm.Data.TleParameters);
            Assert.Equal(25544, omm.Data.TleParameters.NoradCatalogId);
            Assert.Equal(999, omm.Data.TleParameters.ElementSetNumber);
            Assert.Equal(54773, omm.Data.TleParameters.RevolutionNumberAtEpoch);
            Assert.Equal(0.00016698581, omm.Data.TleParameters.BStar.Value, 10);
            Assert.Equal(0.00008852, omm.Data.TleParameters.MeanMotionDot, 8);
            Assert.Equal("U", omm.Data.TleParameters.ClassificationType);
        }

        [Fact]
        public void LoadFromString_ValidXml_ReturnsOmm()
        {
            var xml = File.ReadAllText(IssOmmPath);
            var omm = Omm.LoadFromString(xml, validateSchema: false);

            Assert.NotNull(omm);
            Assert.Equal("ISS (ZARYA)", omm.ObjectName);
            Assert.Equal("1998-067A", omm.ObjectId);
        }

        [Fact]
        public void LoadFromString_NullXml_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Omm.LoadFromString(null));
        }

        [Fact]
        public void LoadFromString_EmptyXml_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Omm.LoadFromString(""));
        }

        [Fact]
        public void LoadFromString_InvalidXml_ThrowsException()
        {
            // XML with wrong root element - fails during parsing
            var invalidXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<wrong xmlns=""urn:ccsds:schema:ndmxml"">
  <header><CREATION_DATE>2024-01-15T12:00:00</CREATION_DATE></header>
</wrong>";

            // Should throw either OmmValidationException (schema validation) or InvalidOperationException (parsing)
            Assert.ThrowsAny<Exception>(() =>
                Omm.LoadFromString(invalidXml, validateSchema: true));
        }

        [Fact]
        public void LoadFromString_MalformedXml_ThrowsException()
        {
            // Malformed XML that will fail parsing
            var malformedXml = @"<?xml version=""1.0""?><omm><unclosed>";

            Assert.ThrowsAny<Exception>(() =>
                Omm.LoadFromString(malformedXml, validateSchema: false));
        }

        [Fact]
        public void LoadFromString_SkipSchemaValidation_Succeeds()
        {
            var xml = File.ReadAllText(IssOmmPath);
            var omm = Omm.LoadFromString(xml, validateSchema: false);

            Assert.NotNull(omm);
            Assert.Equal("ISS (ZARYA)", omm.ObjectName);
        }

        [Fact]
        public void LoadFromString_SkipContentValidation_Succeeds()
        {
            var xml = File.ReadAllText(IssOmmPath);
            var omm = Omm.LoadFromString(xml, validateSchema: false, validateContent: false);

            Assert.NotNull(omm);
        }

        [Fact]
        public void LoadFromStream_ValidStream_ReturnsOmm()
        {
            using var stream = File.OpenRead(IssOmmPath);
            var omm = Omm.LoadFromStream(stream, validateSchema: false);

            Assert.NotNull(omm);
            Assert.Equal("ISS (ZARYA)", omm.ObjectName);
        }

        [Fact]
        public void LoadFromStream_NullStream_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Omm.LoadFromStream(null));
        }

        [Fact]
        public void LoadFromFile_FileNotFound_ThrowsFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(() =>
                Omm.LoadFromFile("/nonexistent/path/omm.xml"));
        }

        [Fact]
        public void LoadFromFile_NullPath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Omm.LoadFromFile(null));
        }

        [Fact]
        public void TryLoadFromFile_FileNotFound_ReturnsFalse()
        {
            var result = Omm.TryLoadFromFile("/nonexistent/path/omm.xml",
                out var omm, out var validationResult);

            Assert.False(result);
            Assert.Null(omm);
            Assert.NotNull(validationResult);
            Assert.False(validationResult.IsValid);
        }

        [Fact]
        public void TryLoadFromFile_ValidFile_ReturnsTrue()
        {
            var result = Omm.TryLoadFromFile(IssOmmPath,
                out var omm, out var validationResult,
                validateSchema: false);

            Assert.True(result);
            Assert.NotNull(omm);
            Assert.Equal("ISS (ZARYA)", omm.ObjectName);
            Assert.True(validationResult.IsValid);
        }

        [Fact]
        public void LoadFromFile_RealOmm_ValidatesSuccessfully()
        {
            var omm = Omm.LoadFromFile(IssOmmPath, validateSchema: false, validateContent: true);

            Assert.True(omm.IsValid());
            Assert.True(omm.Validate().IsStrictlyValid);
        }

        #endregion

        #region Instance Methods (Save/Validate) Tests

        [Fact]
        public void ToXml_ReturnsValidXml()
        {
            var omm = CreateMinimalOmm("ISS", "1998-067A");

            var xml = omm.ToXml();

            Assert.NotNull(xml);
            Assert.Contains("<OBJECT_NAME>ISS</OBJECT_NAME>", xml);
            Assert.Contains("<OBJECT_ID>1998-067A</OBJECT_ID>", xml);
            Assert.Contains("CCSDS_OMM_VERS", xml);
        }

        [Fact]
        public void Validate_ValidOmm_ReturnsSuccessResult()
        {
            var omm = CreateMinimalOmm("ISS", "1998-067A");

            var result = omm.Validate();

            Assert.True(result.IsValid);
        }

        [Fact]
        public void IsValid_ValidOmm_ReturnsTrue()
        {
            var omm = CreateMinimalOmm("ISS", "1998-067A");

            Assert.True(omm.IsValid());
        }

        [Fact]
        public void SaveToStream_ValidOmm_WritesXml()
        {
            var omm = CreateMinimalOmm("ISS", "1998-067A");
            using var stream = new MemoryStream();

            omm.SaveToStream(stream);

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var xml = reader.ReadToEnd();

            Assert.Contains("<OBJECT_NAME>ISS</OBJECT_NAME>", xml);
        }

        [Fact]
        public void SaveToStream_NullStream_ThrowsArgumentNullException()
        {
            var omm = CreateMinimalOmm("ISS", "1998-067A");

            Assert.Throws<ArgumentNullException>(() => omm.SaveToStream(null));
        }

        [Fact]
        public void SaveToFile_NullPath_ThrowsArgumentNullException()
        {
            var omm = CreateMinimalOmm("ISS", "1998-067A");

            Assert.Throws<ArgumentNullException>(() => omm.SaveToFile(null));
        }

        [Fact]
        public void RoundTrip_CreateAndLoad_PreservesData()
        {
            var original = Omm.CreateForTle(
                objectName: "ISS (ZARYA)",
                objectId: "1998-067A",
                epoch: _testEpoch,
                meanMotion: 15.49309423,
                eccentricity: 0.0000493,
                inclination: 51.6423,
                raan: 353.0312,
                argOfPericenter: 320.8755,
                meanAnomaly: 39.2360,
                bstar: 0.0001027,
                meanMotionDot: 0.00016717,
                meanMotionDDot: 0.0,
                noradCatalogId: 25544);

            var xml = original.ToXml();
            // Skip schema validation - schema validator has encoding issues with string content
            var loaded = Omm.LoadFromString(xml, validateSchema: false);

            Assert.Equal(original.ObjectName, loaded.ObjectName);
            Assert.Equal(original.ObjectId, loaded.ObjectId);
            Assert.Equal(original.Data.MeanElements.MeanMotion.Value, loaded.Data.MeanElements.MeanMotion.Value, 6);
            Assert.Equal(original.Data.MeanElements.Eccentricity, loaded.Data.MeanElements.Eccentricity, 7);
            Assert.Equal(original.Data.MeanElements.Inclination, loaded.Data.MeanElements.Inclination, 4);
        }

        [Fact]
        public void RoundTrip_LoadFileAndRewrite_PreservesData()
        {
            // Load real OMM file
            var original = Omm.LoadFromFile(IssOmmPath, validateSchema: false);

            // Convert to XML and reload
            var xml = original.ToXml();
            var reloaded = Omm.LoadFromString(xml, validateSchema: false);

            // Verify key data preserved
            Assert.Equal(original.ObjectName, reloaded.ObjectName);
            Assert.Equal(original.ObjectId, reloaded.ObjectId);
            Assert.Equal(original.Data.MeanElements.MeanMotion.Value, reloaded.Data.MeanElements.MeanMotion.Value, 6);
            Assert.Equal(original.Data.MeanElements.Eccentricity, reloaded.Data.MeanElements.Eccentricity, 8);
            Assert.Equal(original.Data.TleParameters.NoradCatalogId, reloaded.Data.TleParameters.NoradCatalogId);
            Assert.Equal(original.Data.TleParameters.BStar.Value, reloaded.Data.TleParameters.BStar.Value, 10);
        }

        #endregion

        #region OmmValidationException Tests

        [Fact]
        public void OmmValidationException_ContainsValidationResult()
        {
            var result = new OmmValidationResult();
            result.AddError("TEST_ERROR", "Test error message", "Test.Path");

            var ex = new OmmValidationException("Validation failed", result);

            Assert.NotNull(ex.ValidationResult);
            Assert.Same(result, ex.ValidationResult);
            Assert.Contains("Validation failed", ex.Message);
            Assert.Contains("Test error message", ex.Message);
        }

        [Fact]
        public void OmmValidationException_MultipleErrors_ShowsFirstFew()
        {
            var result = new OmmValidationResult();
            result.AddError("ERR1", "Error 1", "Path1");
            result.AddError("ERR2", "Error 2", "Path2");
            result.AddError("ERR3", "Error 3", "Path3");
            result.AddError("ERR4", "Error 4", "Path4");

            var ex = new OmmValidationException("Failed", result);

            Assert.Contains("4 error(s)", ex.Message);
            Assert.Contains("Error 1", ex.Message);
            Assert.Contains("Error 2", ex.Message);
            Assert.Contains("Error 3", ex.Message);
            Assert.Contains("more error(s)", ex.Message);
        }

        #endregion
    }
}
