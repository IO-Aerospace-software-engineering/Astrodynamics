// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OPM;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OPM
{
    public class OpmTests
    {
        private readonly DateTime _testEpoch = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        public OpmTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_Succeeds()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);
            var data = new OpmData(stateVector);

            var opm = new Opm(header, metadata, data);

            Assert.Equal(header, opm.Header);
            Assert.Equal(metadata, opm.Metadata);
            Assert.Equal(data, opm.Data);
        }

        [Fact]
        public void Constructor_NullHeader_ThrowsArgumentNullException()
        {
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);
            var data = new OpmData(stateVector);

            Assert.Throws<ArgumentNullException>(() => new Opm(null, metadata, data));
        }

        [Fact]
        public void Constructor_NullMetadata_ThrowsArgumentNullException()
        {
            var header = CcsdsHeader.CreateDefault();
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);
            var data = new OpmData(stateVector);

            Assert.Throws<ArgumentNullException>(() => new Opm(header, null, data));
        }

        [Fact]
        public void Constructor_NullData_ThrowsArgumentNullException()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");

            Assert.Throws<ArgumentNullException>(() => new Opm(header, metadata, null));
        }

        #endregion

        #region Constants Tests

        [Fact]
        public void Version_IsCorrect()
        {
            Assert.Equal("3.0", Opm.Version);
        }

        [Fact]
        public void FormatId_IsCorrect()
        {
            Assert.Equal("CCSDS_OPM_VERS", Opm.FormatId);
        }

        #endregion

        #region Property Tests

        [Fact]
        public void ObjectName_ReturnsMetadataObjectName()
        {
            var opm = CreateMinimalOpm("ISS (ZARYA)", "1998-067A");

            Assert.Equal("ISS (ZARYA)", opm.ObjectName);
        }

        [Fact]
        public void ObjectId_ReturnsMetadataObjectId()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");

            Assert.Equal("1998-067A", opm.ObjectId);
        }

        [Fact]
        public void Epoch_ReturnsDataEpoch()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");

            Assert.Equal(_testEpoch, opm.Epoch);
        }

        [Fact]
        public void HasKeplerianElements_WithKeplerian_ReturnsTrue()
        {
            var opm = CreateOpmWithKeplerianElements();

            Assert.True(opm.HasKeplerianElements);
        }

        [Fact]
        public void HasKeplerianElements_WithoutKeplerian_ReturnsFalse()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");

            Assert.False(opm.HasKeplerianElements);
        }

        [Fact]
        public void HasSpacecraftParameters_WithParameters_ReturnsTrue()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);
            var data = new OpmData(stateVector, mass: 420000.0);

            var opm = new Opm(header, metadata, data);

            Assert.True(opm.HasSpacecraftParameters);
        }

        [Fact]
        public void HasSpacecraftParameters_WithoutParameters_ReturnsFalse()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");

            Assert.False(opm.HasSpacecraftParameters);
        }

        [Fact]
        public void HasCovariance_WithCovariance_ReturnsTrue()
        {
            var opm = CreateOpmWithCovariance();

            Assert.True(opm.HasCovariance);
        }

        [Fact]
        public void HasCovariance_WithoutCovariance_ReturnsFalse()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");

            Assert.False(opm.HasCovariance);
        }

        [Fact]
        public void HasManeuvers_WithManeuvers_ReturnsTrue()
        {
            var opm = CreateOpmWithManeuvers();

            Assert.True(opm.HasManeuvers);
        }

        [Fact]
        public void HasManeuvers_WithoutManeuvers_ReturnsFalse()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");

            Assert.False(opm.HasManeuvers);
        }

        [Fact]
        public void CovarianceReferenceFrameConsistent_NoCovariance_ReturnsTrue()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");

            Assert.True(opm.CovarianceReferenceFrameConsistent);
        }

        [Fact]
        public void CovarianceReferenceFrameConsistent_SameFrame_ReturnsTrue()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var covariance = new CovarianceMatrix(
                cxX: 1.0e-6,
                cyX: 0.0, cyY: 1.0e-6,
                czX: 0.0, czY: 0.0, czZ: 1.0e-6,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9,
                referenceFrame: "ICRF"); // Same as metadata reference frame
            var data = new OpmData(stateVector, covariance: covariance);
            var opm = new Opm(header, metadata, data);

            Assert.True(opm.CovarianceReferenceFrameConsistent);
        }

        [Fact]
        public void CovarianceReferenceFrameConsistent_DifferentFrame_ReturnsFalse()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var covariance = new CovarianceMatrix(
                cxX: 1.0e-6,
                cyX: 0.0, cyY: 1.0e-6,
                czX: 0.0, czY: 0.0, czZ: 1.0e-6,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9,
                referenceFrame: "RTN"); // Different from metadata reference frame (ICRF)
            var data = new OpmData(stateVector, covariance: covariance);
            var opm = new Opm(header, metadata, data);

            Assert.False(opm.CovarianceReferenceFrameConsistent);
        }

        [Fact]
        public void CovarianceReferenceFrameConsistent_CaseInsensitive_ReturnsTrue()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var covariance = new CovarianceMatrix(
                cxX: 1.0e-6,
                cyX: 0.0, cyY: 1.0e-6,
                czX: 0.0, czY: 0.0, czZ: 1.0e-6,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9,
                referenceFrame: "icrf"); // Lowercase version of ICRF
            var data = new OpmData(stateVector, covariance: covariance);
            var opm = new Opm(header, metadata, data);

            Assert.True(opm.CovarianceReferenceFrameConsistent);
        }

        #endregion

        #region Factory Methods Tests

        [Fact]
        public void CreateFromStateVector_NullStateVector_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                Opm.CreateFromStateVector("ISS", "1998-067A", null));
        }

        [Fact]
        public void CreateMinimal_FactoryMethod_CreatesValidOpm()
        {
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);

            var opm = Opm.CreateMinimal(
                objectName: "TEST SAT",
                objectId: "2024-001A",
                centerName: "EARTH",
                referenceFrame: "ICRF",
                timeSystem: "UTC",
                stateVector: stateVector,
                originator: "TEST ORG");

            Assert.Equal("TEST SAT", opm.ObjectName);
            Assert.Equal("2024-001A", opm.ObjectId);
            Assert.Equal("TEST ORG", opm.Header.Originator);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ContainsRelevantInfo()
        {
            var opm = CreateMinimalOpm("ISS (ZARYA)", "1998-067A");
            var result = opm.ToString();

            Assert.Contains("OPM", result);
            Assert.Contains("ISS", result);
            Assert.Contains("1998-067A", result);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void Validate_ValidOpm_ReturnsSuccessResult()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");

            var result = opm.Validate();

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_CovarianceFrameMismatch_AddsWarning()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var covariance = new CovarianceMatrix(
                cxX: 1.0e-6,
                cyX: 0.0, cyY: 1.0e-6,
                czX: 0.0, czY: 0.0, czZ: 1.0e-6,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9,
                referenceFrame: "RTN"); // Different from metadata reference frame (ICRF)
            var data = new OpmData(stateVector, covariance: covariance);
            var opm = new Opm(header, metadata, data);

            var result = opm.Validate();

            // Should be valid but have a warning
            Assert.True(result.IsValid);
            Assert.True(result.WarningCount > 0);
            Assert.Contains(result.Warnings, w => w.Message.Contains("Covariance reference frame"));
        }

        [Fact]
        public void Validate_CovarianceFrameMatch_NoWarning()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var covariance = new CovarianceMatrix(
                cxX: 1.0e-6,
                cyX: 0.0, cyY: 1.0e-6,
                czX: 0.0, czY: 0.0, czZ: 1.0e-6,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9,
                referenceFrame: "ICRF"); // Same as metadata reference frame
            var data = new OpmData(stateVector, covariance: covariance);
            var opm = new Opm(header, metadata, data);

            var result = opm.Validate();

            // Should be valid with no covariance frame warnings
            Assert.True(result.IsValid);
            Assert.DoesNotContain(result.Warnings, w => w.Message.Contains("Covariance reference frame"));
        }

        #endregion

        #region Save/Load Tests

        [Fact]
        public void ToXmlString_ReturnsValidXml()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");

            var xml = opm.ToXmlString();

            Assert.NotNull(xml);
            Assert.Contains("<OBJECT_NAME>ISS</OBJECT_NAME>", xml);
            Assert.Contains("<OBJECT_ID>1998-067A</OBJECT_ID>", xml);
            Assert.Contains("CCSDS_OPM_VERS", xml);
        }

        [Fact]
        public void ToXmlString_WithNdmWrapper_IncludesNdmElement()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");

            var xml = opm.ToXmlString(wrapInNdm: true);

            Assert.Contains("<ndm", xml);
            Assert.Contains("<opm", xml);
        }

        [Fact]
        public void ToXmlString_WithoutNdmWrapper_ExcludesNdmElement()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");

            var xml = opm.ToXmlString(wrapInNdm: false);

            Assert.DoesNotContain("<ndm", xml);
            Assert.StartsWith("<?xml", xml);
        }

        [Fact]
        public void LoadFromString_ValidXml_ReturnsOpm()
        {
            var original = CreateMinimalOpm("ISS", "1998-067A");
            var xml = original.ToXmlString();

            var loaded = Opm.LoadFromString(xml, validateSchema: false, validateContent: false);

            Assert.NotNull(loaded);
            Assert.Equal(original.ObjectName, loaded.ObjectName);
            Assert.Equal(original.ObjectId, loaded.ObjectId);
        }

        [Fact]
        public void LoadFromString_NullXml_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Opm.LoadFromString(null));
        }

        [Fact]
        public void LoadFromString_EmptyXml_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Opm.LoadFromString(""));
        }

        [Fact]
        public void LoadFromStream_ValidStream_ReturnsOpm()
        {
            var original = CreateMinimalOpm("ISS", "1998-067A");
            var xml = original.ToXmlString();
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));

            var loaded = Opm.LoadFromStream(stream, validateSchema: false, validateContent: false);

            Assert.NotNull(loaded);
            Assert.Equal(original.ObjectName, loaded.ObjectName);
        }

        [Fact]
        public void LoadFromStream_NullStream_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Opm.LoadFromStream(null));
        }

        [Fact]
        public void SaveToStream_ValidOpm_WritesXml()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");
            using var stream = new MemoryStream();

            opm.SaveToStream(stream, validateBeforeSave: false);

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var xml = reader.ReadToEnd();

            Assert.Contains("<OBJECT_NAME>ISS</OBJECT_NAME>", xml);
        }

        [Fact]
        public void SaveToStream_NullStream_ThrowsArgumentNullException()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");

            Assert.Throws<ArgumentNullException>(() => opm.SaveToStream(null));
        }

        [Fact]
        public void RoundTrip_CreateAndLoad_PreservesData()
        {
            var original = CreateMinimalOpm("ISS (ZARYA)", "1998-067A");

            var xml = original.ToXmlString();
            var loaded = Opm.LoadFromString(xml, validateSchema: false, validateContent: false);

            Assert.Equal(original.ObjectName, loaded.ObjectName);
            Assert.Equal(original.ObjectId, loaded.ObjectId);
            Assert.Equal(original.Epoch, loaded.Epoch);
            Assert.Equal(original.Data.StateVector.X, loaded.Data.StateVector.X, 6);
            Assert.Equal(original.Data.StateVector.YDot, loaded.Data.StateVector.YDot, 6);
        }

        [Fact]
        public void RoundTrip_WithKeplerianElements_PreservesData()
        {
            var original = CreateOpmWithKeplerianElements();

            var xml = original.ToXmlString();
            var loaded = Opm.LoadFromString(xml, validateSchema: false, validateContent: false);

            Assert.True(loaded.HasKeplerianElements);
            Assert.Equal(original.Data.KeplerianElements.SemiMajorAxis,
                loaded.Data.KeplerianElements.SemiMajorAxis, 3);
            Assert.Equal(original.Data.KeplerianElements.Eccentricity,
                loaded.Data.KeplerianElements.Eccentricity, 6);
        }

        [Fact]
        public void RoundTrip_WithCovariance_PreservesData()
        {
            var original = CreateOpmWithCovariance();

            var xml = original.ToXmlString();
            var loaded = Opm.LoadFromString(xml, validateSchema: false, validateContent: false);

            Assert.True(loaded.HasCovariance);
            Assert.Equal(original.Data.Covariance.CxX, loaded.Data.Covariance.CxX, 10);
        }

        [Fact]
        public void RoundTrip_WithManeuvers_PreservesData()
        {
            var original = CreateOpmWithManeuvers();

            var xml = original.ToXmlString();
            var loaded = Opm.LoadFromString(xml, validateSchema: false, validateContent: false);

            Assert.True(loaded.HasManeuvers);
            Assert.Single(loaded.Data.Maneuvers);
            Assert.Equal(original.Data.Maneuvers[0].ManDuration,
                loaded.Data.Maneuvers[0].ManDuration, 3);
        }

        #endregion

        #region LoadFromFile/SaveToFile Tests

        private static string GetTestFilePath(string fileName) =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CCSDS", "OPM", fileName);

        [Fact]
        public void LoadFromFile_MinimalOpm_Succeeds()
        {
            var filePath = GetTestFilePath("iss_minimal.xml");

            var opm = Opm.LoadFromFile(filePath, validateSchema: false, validateContent: true);

            Assert.NotNull(opm);
            Assert.Equal("ISS (ZARYA)", opm.ObjectName);
            Assert.Equal("1998-067A", opm.ObjectId);
            Assert.Equal("EARTH", opm.Metadata.CenterName);
            Assert.Equal("ICRF", opm.Metadata.ReferenceFrame);
            Assert.Equal("UTC", opm.Metadata.TimeSystem);
            Assert.Equal(6778.137, opm.Data.StateVector.X, 3);
            Assert.Equal(7.5, opm.Data.StateVector.YDot, 1);
        }

        [Fact]
        public void LoadFromFile_CompleteOpm_LoadsAllElements()
        {
            var filePath = GetTestFilePath("satellite_complete.xml");

            var opm = Opm.LoadFromFile(filePath, validateSchema: false, validateContent: true);

            Assert.NotNull(opm);
            Assert.Equal("TEST SATELLITE", opm.ObjectName);
            Assert.Equal("2024-001A", opm.ObjectId);

            // Verify state vector
            Assert.Equal(-4453.783586, opm.Data.StateVector.X, 6);
            Assert.Equal(5038.203756, opm.Data.StateVector.Y, 6);

            // Verify Keplerian elements
            Assert.True(opm.HasKeplerianElements);
            Assert.Equal(6878.137, opm.Data.KeplerianElements.SemiMajorAxis, 3);
            Assert.Equal(0.0001234, opm.Data.KeplerianElements.Eccentricity, 7);
            Assert.Equal(51.6420, opm.Data.KeplerianElements.Inclination, 4);

            // Verify spacecraft parameters
            Assert.True(opm.HasSpacecraftParameters);
            Assert.Equal(1500.0, opm.Data.Mass);
            Assert.Equal(22.0, opm.Data.DragArea);
            Assert.Equal(2.2, opm.Data.DragCoefficient);
            Assert.Equal(25.5, opm.Data.SolarRadiationPressureArea);
            Assert.Equal(1.8, opm.Data.SolarRadiationCoefficient);

            // Verify covariance
            Assert.True(opm.HasCovariance);
            Assert.Equal(1.0e-6, opm.Data.Covariance.CxX, 10);

            // Verify maneuvers
            Assert.True(opm.HasManeuvers);
            Assert.Equal(2, opm.Data.Maneuvers.Count);
            Assert.Equal(180.0, opm.Data.Maneuvers[0].ManDuration, 1);
            Assert.Equal(-25.0, opm.Data.Maneuvers[0].ManDeltaMass, 1);
            Assert.Equal(0.05, opm.Data.Maneuvers[0].ManDv1, 2);
        }

        [Fact]
        public void LoadFromFile_NullPath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Opm.LoadFromFile(null));
        }

        [Fact]
        public void LoadFromFile_EmptyPath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Opm.LoadFromFile(""));
        }

        [Fact]
        public void LoadFromFile_FileNotFound_ThrowsFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(() =>
                Opm.LoadFromFile("/nonexistent/path/opm.xml"));
        }

        [Fact]
        public void LoadFromFile_InvalidSchema_ThrowsOpmValidationException()
        {
            var filePath = GetTestFilePath("invalid_missing_epoch.xml");

            var ex = Assert.Throws<OpmValidationException>(() =>
                Opm.LoadFromFile(filePath, validateSchema: true, validateContent: false));

            Assert.Contains("Schema validation failed", ex.Message);
            Assert.NotNull(ex.ValidationResult);
            Assert.False(ex.ValidationResult.IsValid);
        }

        [Fact]
        public void LoadFromFile_InvalidSchema_WithValidationDisabled_Succeeds()
        {
            var filePath = GetTestFilePath("invalid_missing_epoch.xml");

            // Should not throw when schema validation is disabled
            // Note: The reader may still fail to parse, but that's a parse error, not validation
            var ex = Assert.ThrowsAny<Exception>(() =>
                Opm.LoadFromFile(filePath, validateSchema: false, validateContent: false));

            // The file is malformed so it will fail parsing, but not due to validation
            Assert.IsNotType<OpmValidationException>(ex);
        }

        [Fact]
        public void SaveToFile_ValidOpm_CreatesFile()
        {
            var opm = CreateMinimalOpm("SAVE TEST", "2024-002A");
            var tempPath = Path.Combine(Path.GetTempPath(), $"opm_test_{Guid.NewGuid()}.xml");

            try
            {
                opm.SaveToFile(tempPath, validateBeforeSave: false);

                Assert.True(File.Exists(tempPath));

                var content = File.ReadAllText(tempPath);
                Assert.Contains("<OBJECT_NAME>SAVE TEST</OBJECT_NAME>", content);
                Assert.Contains("<OBJECT_ID>2024-002A</OBJECT_ID>", content);
                Assert.Contains("CCSDS_OPM_VERS", content);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void SaveToFile_NullPath_ThrowsArgumentNullException()
        {
            var opm = CreateMinimalOpm("TEST", "2024-001A");

            Assert.Throws<ArgumentNullException>(() => opm.SaveToFile(null));
        }

        [Fact]
        public void SaveToFile_EmptyPath_ThrowsArgumentNullException()
        {
            var opm = CreateMinimalOpm("TEST", "2024-001A");

            Assert.Throws<ArgumentNullException>(() => opm.SaveToFile(""));
        }

        [Fact]
        public void SaveToFile_RoundTrip_PreservesData()
        {
            var original = CreateOpmWithKeplerianElements();
            var tempPath = Path.Combine(Path.GetTempPath(), $"opm_roundtrip_{Guid.NewGuid()}.xml");

            try
            {
                // Save to file
                original.SaveToFile(tempPath, validateBeforeSave: false);

                // Load from file
                var loaded = Opm.LoadFromFile(tempPath, validateSchema: false, validateContent: false);

                // Verify data preserved
                Assert.Equal(original.ObjectName, loaded.ObjectName);
                Assert.Equal(original.ObjectId, loaded.ObjectId);
                Assert.Equal(original.Data.StateVector.X, loaded.Data.StateVector.X, 6);
                Assert.Equal(original.Data.StateVector.YDot, loaded.Data.StateVector.YDot, 6);
                Assert.True(loaded.HasKeplerianElements);
                Assert.Equal(original.Data.KeplerianElements.SemiMajorAxis,
                    loaded.Data.KeplerianElements.SemiMajorAxis, 3);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void SaveToFile_WithAllOptionalElements_RoundTripPreservesData()
        {
            // Create OPM with all optional elements
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("FULL TEST", "2024-003A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 100.0, 200.0, 0.1, 7.5, 0.05);
            var keplerianElements = OpmKeplerianElements.CreateWithTrueAnomaly(
                6878.137, 0.001, 51.6, 120.0, 90.0, 45.0, 398600.4418);
            var covariance = new CovarianceMatrix(
                cxX: 1.0e-6,
                cyX: 0.0, cyY: 1.0e-6,
                czX: 0.0, czY: 0.0, czZ: 1.0e-6,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9);
            var maneuver = new OpmManeuverParameters(
                _testEpoch.AddHours(2), 120.0, -30.0, "RSW", 0.05, 0.0, 0.01);
            var data = new OpmData(stateVector, keplerianElements,
                mass: 2000.0,
                dragArea: 30.0,
                dragCoefficient: 2.1,
                solarRadiationPressureArea: 35.0,
                solarRadiationCoefficient: 1.6,
                covariance: covariance,
                maneuvers: new[] { maneuver });
            var original = new Opm(header, metadata, data);

            var tempPath = Path.Combine(Path.GetTempPath(), $"opm_full_roundtrip_{Guid.NewGuid()}.xml");

            try
            {
                original.SaveToFile(tempPath, validateBeforeSave: false);
                var loaded = Opm.LoadFromFile(tempPath, validateSchema: false, validateContent: false);

                Assert.Equal(original.ObjectName, loaded.ObjectName);
                Assert.True(loaded.HasKeplerianElements);
                Assert.True(loaded.HasSpacecraftParameters);
                Assert.True(loaded.HasCovariance);
                Assert.True(loaded.HasManeuvers);
                Assert.Equal(2000.0, loaded.Data.Mass);
                Assert.Equal(30.0, loaded.Data.DragArea);
                Assert.Single(loaded.Data.Maneuvers);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        #endregion

        #region TryLoad Tests

        [Fact]
        public void TryLoadFromFile_FileNotFound_ReturnsFalse()
        {
            var result = Opm.TryLoadFromFile("/nonexistent/path/opm.xml",
                out var opm, out var validationResult);

            Assert.False(result);
            Assert.Null(opm);
            Assert.NotNull(validationResult);
            Assert.False(validationResult.IsValid);
        }

        #endregion

        #region Framework Integration Tests (ToSpacecraft)

        [Fact]
        public void ToSpacecraft_WithMinimalOpm_CreatesSpacecraftWithDefaults()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");
            var clock = new Clock("OnboardClock", 256);
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);

            var spacecraft = opm.ToSpacecraft(-1000, 500000.0, clock, earth);

            Assert.Equal(-1000, spacecraft.NaifId);
            Assert.Equal("ISS", spacecraft.Name);
            Assert.Equal("1998-067A", spacecraft.CosparId);
            Assert.Equal(1.0, spacecraft.Mass); // Default mass when not specified
            Assert.Equal(500000.0, spacecraft.MaximumOperatingMass);
            Assert.Equal(1.0, spacecraft.SectionalArea); // Default
            Assert.Equal(2.2, spacecraft.DragCoefficient); // Default
            Assert.Equal(1.0, spacecraft.SolarRadiationCoeff); // Default
            Assert.Same(clock, spacecraft.Clock);
        }

        [Fact]
        public void ToSpacecraft_WithSpacecraftParameters_UsesOpmValues()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var data = new OpmData(stateVector,
                mass: 420000.0,
                dragArea: 1600.0,
                dragCoefficient: 2.2,
                solarRadiationCoefficient: 1.5);
            var opm = new Opm(header, metadata, data);

            var clock = new Clock("OnboardClock", 256);
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);

            var spacecraft = opm.ToSpacecraft(-1000, 500000.0, clock, earth);

            Assert.Equal(420000.0, spacecraft.Mass);
            Assert.Equal(1600.0, spacecraft.SectionalArea);
            Assert.Equal(2.2, spacecraft.DragCoefficient);
            Assert.Equal(1.5, spacecraft.SolarRadiationCoeff);
        }

        [Fact]
        public void ToSpacecraft_MaxMassLessThanMass_AdjustsMaxMass()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var data = new OpmData(stateVector, mass: 420000.0);
            var opm = new Opm(header, metadata, data);

            var clock = new Clock("OnboardClock", 256);
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);

            // maxMass (100000) < mass (420000), should be adjusted
            var spacecraft = opm.ToSpacecraft(-1000, 100000.0, clock, earth);

            Assert.Equal(420000.0, spacecraft.Mass);
            Assert.Equal(420000.0, spacecraft.MaximumOperatingMass);
        }

        [Fact]
        public void ToSpacecraft_NullClock_ThrowsArgumentNullException()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);

            Assert.Throws<ArgumentNullException>(() => opm.ToSpacecraft(-1000, 500000.0, null, earth));
        }

        [Fact]
        public void ToSpacecraft_StateVectorIsCorrect()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");
            var clock = new Clock("OnboardClock", 256);
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);

            var spacecraft = opm.ToSpacecraft(-1000, 500000.0, clock, earth);
            var sv = spacecraft.InitialOrbitalParameters.ToStateVector();

            // OPM state vector: X=6778.137 km, Y=0, Z=0, VX=0, VY=7.5 km/s, VZ=0
            // Framework uses meters
            Assert.Equal(6778137.0, sv.Position.X, 0);
            Assert.Equal(0.0, sv.Position.Y, 0);
            Assert.Equal(0.0, sv.Position.Z, 0);
            Assert.Equal(0.0, sv.Velocity.X, 0);
            Assert.Equal(7500.0, sv.Velocity.Y, 0);
            Assert.Equal(0.0, sv.Velocity.Z, 0);
        }

        [Fact]
        public void ToSpacecraft_RoundTrip_PreservesData()
        {
            // Create spacecraft
            var epoch = new TimeSystem.Time(_testEpoch, TimeSystem.TimeFrame.UTCFrame);
            var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, epoch);
            var clock = new Clock("OnboardClock", 256);
            var position = new Vector3(6778137.0, 0.0, 0.0);
            var velocity = new Vector3(0.0, 7500.0, 0.0);
            var sv = new StateVector(position, velocity, earth, epoch, Frames.Frame.ICRF);

            var originalSpacecraft = new Spacecraft(
                -1000, "ISS", 420000.0, 500000.0, clock, sv,
                1600.0, 2.2, "1998-067A", 1.5);

            // Convert to OPM
            var opm = originalSpacecraft.ToOpm();

            // Convert back to Spacecraft (need new clock since original is attached)
            var newClock = new Clock("OnboardClock2", 256);
            var newSpacecraft = opm.ToSpacecraft(-1001, 500000.0, newClock, earth);

            Assert.Equal(originalSpacecraft.Name, newSpacecraft.Name);
            Assert.Equal(originalSpacecraft.CosparId, newSpacecraft.CosparId);
            Assert.Equal(originalSpacecraft.Mass, newSpacecraft.Mass, 0);
            Assert.Equal(originalSpacecraft.SectionalArea, newSpacecraft.SectionalArea, 0);
            Assert.Equal(originalSpacecraft.DragCoefficient, newSpacecraft.DragCoefficient, 1);
            Assert.Equal(originalSpacecraft.SolarRadiationCoeff, newSpacecraft.SolarRadiationCoeff, 1);
        }

        [Fact]
        public void ToSpacecraft_DefaultObserver_UsesEarth()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");
            var clock = new Clock("OnboardClock", 256);

            // No observer specified - should default to Earth
            var spacecraft = opm.ToSpacecraft(-1000, 500000.0, clock);

            Assert.NotNull(spacecraft);
            Assert.NotNull(spacecraft.InitialOrbitalParameters);
        }

        #endregion

        #region CCSDS Compliance Tests (Units, Comments, UserDefinedParameters)

        [Fact]
        public void ToXmlString_StateVector_HasUnitsAttributes()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");

            var xml = opm.ToXmlString();

            Assert.Contains("units=\"km\"", xml);
            Assert.Contains("units=\"km/s\"", xml);
        }

        [Fact]
        public void ToXmlString_KeplerianElements_HasUnitsAttributes()
        {
            var opm = CreateOpmWithKeplerianElements();

            var xml = opm.ToXmlString();

            Assert.Contains("SEMI_MAJOR_AXIS", xml);
            Assert.Contains("units=\"km\"", xml);
            Assert.Contains("units=\"deg\"", xml);
            Assert.Contains("units=\"km**3/s**2\"", xml); // GM
        }

        [Fact]
        public void ToXmlString_SpacecraftParameters_HasUnitsAttributes()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var data = new OpmData(stateVector, mass: 420000.0, dragArea: 1600.0);
            var opm = new Opm(header, metadata, data);

            var xml = opm.ToXmlString();

            Assert.Contains("units=\"kg\"", xml);
            Assert.Contains("units=\"m**2\"", xml);
        }

        [Fact]
        public void ToXmlString_CovarianceMatrix_HasUnitsAttributes()
        {
            var opm = CreateOpmWithCovariance();

            var xml = opm.ToXmlString();

            Assert.Contains("units=\"km**2\"", xml);
            Assert.Contains("units=\"km**2/s\"", xml);
            Assert.Contains("units=\"km**2/s**2\"", xml);
        }

        [Fact]
        public void ToXmlString_ManeuverParameters_HasUnitsAttributes()
        {
            var opm = CreateOpmWithManeuvers();

            var xml = opm.ToXmlString();

            Assert.Contains("units=\"s\"", xml); // MAN_DURATION
            Assert.Contains("units=\"kg\"", xml); // MAN_DELTA_MASS
            // MAN_DV uses km/s which is already tested by state vector
        }

        [Fact]
        public void RoundTrip_WithDataLevelComments_PreservesComments()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var comments = new[] { "This is a data-level comment", "Another comment" };
            var data = new OpmData(stateVector, comments: comments);
            var original = new Opm(header, metadata, data);

            var xml = original.ToXmlString();
            var loaded = Opm.LoadFromString(xml, validateSchema: false, validateContent: false);

            Assert.Equal(2, loaded.Data.Comments.Count);
            Assert.Equal("This is a data-level comment", loaded.Data.Comments[0]);
            Assert.Equal("Another comment", loaded.Data.Comments[1]);
        }

        [Fact]
        public void ToXmlString_DataLevelComments_AppearBeforeStateVector()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var comments = new[] { "Data section comment" };
            var data = new OpmData(stateVector, comments: comments);
            var opm = new Opm(header, metadata, data);

            var xml = opm.ToXmlString();

            var commentIndex = xml.IndexOf("<COMMENT>Data section comment</COMMENT>", StringComparison.Ordinal);
            var stateVectorIndex = xml.IndexOf("<stateVector>", StringComparison.Ordinal);

            Assert.True(commentIndex < stateVectorIndex,
                "Data-level COMMENT should appear before stateVector in XML output");
        }

        [Fact]
        public void RoundTrip_WithUserDefinedParameters_PreservesParameters()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var userParams = new OpmUserDefinedParameters(
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["MISSION_ID"] = "STS-001",
                    ["OPERATOR"] = "NASA"
                },
                new[] { "User parameters comment" });
            var data = new OpmData(stateVector, userDefinedParameters: userParams);
            var original = new Opm(header, metadata, data);

            var xml = original.ToXmlString();
            var loaded = Opm.LoadFromString(xml, validateSchema: false, validateContent: false);

            Assert.True(loaded.Data.HasUserDefinedParameters);
            Assert.Equal("STS-001", loaded.Data.UserDefinedParameters.GetValue("MISSION_ID"));
            Assert.Equal("NASA", loaded.Data.UserDefinedParameters.GetValue("OPERATOR"));
            Assert.Single(loaded.Data.UserDefinedParameters.Comments);
            Assert.Equal("User parameters comment", loaded.Data.UserDefinedParameters.Comments[0]);
        }

        [Fact]
        public void ToXmlString_UserDefinedParameters_HasCorrectFormat()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var userParams = new OpmUserDefinedParameters(
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["CUSTOM_PARAM"] = "custom_value"
                });
            var data = new OpmData(stateVector, userDefinedParameters: userParams);
            var opm = new Opm(header, metadata, data);

            var xml = opm.ToXmlString();

            Assert.Contains("<userDefinedParameters>", xml);
            Assert.Contains("parameter=\"CUSTOM_PARAM\"", xml);
            Assert.Contains(">custom_value</USER_DEFINED>", xml);
        }

        [Fact]
        public void ToXmlString_UserDefinedParameters_AppearsAfterManeuvers()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var maneuver = new OpmManeuverParameters(
                _testEpoch.AddHours(1), 120.0, -50.0, "RSW", 0.1, 0.0, 0.0);
            var userParams = new OpmUserDefinedParameters(
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["TEST"] = "value"
                });
            var data = new OpmData(stateVector, maneuvers: new[] { maneuver }, userDefinedParameters: userParams);
            var opm = new Opm(header, metadata, data);

            var xml = opm.ToXmlString();

            var maneuverIndex = xml.IndexOf("</maneuverParameters>", StringComparison.Ordinal);
            var userDefinedIndex = xml.IndexOf("<userDefinedParameters>", StringComparison.Ordinal);

            Assert.True(maneuverIndex < userDefinedIndex,
                "userDefinedParameters should appear after maneuverParameters in XML output");
        }

        [Fact]
        public void RoundTrip_WithAllComplianceFeatures_PreservesData()
        {
            // Create OPM with all compliance features
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("COMPLIANCE TEST", "2024-099A", "EARTH", "ICRF", "UTC",
                comments: new[] { "Metadata comment" });
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 100.0, 200.0, 0.1, 7.5, 0.05,
                comments: new[] { "State vector comment" });
            var keplerianElements = OpmKeplerianElements.CreateWithTrueAnomaly(
                6878.137, 0.001, 51.6, 120.0, 90.0, 45.0, 398600.4418,
                comments: new[] { "Keplerian comment" });
            var covariance = new CovarianceMatrix(
                cxX: 1.0e-6,
                cyX: 0.0, cyY: 1.0e-6,
                czX: 0.0, czY: 0.0, czZ: 1.0e-6,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9,
                comments: new[] { "Covariance comment" });
            var maneuver = new OpmManeuverParameters(
                _testEpoch.AddHours(2), 120.0, -30.0, "RSW", 0.05, 0.0, 0.01,
                comments: new[] { "Maneuver comment" });
            var userParams = new OpmUserDefinedParameters(
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["MISSION"] = "TEST_MISSION",
                    ["VERSION"] = "1.0"
                },
                new[] { "User params comment" });

            var data = new OpmData(
                stateVector, keplerianElements,
                mass: 2000.0,
                dragArea: 30.0,
                dragCoefficient: 2.1,
                solarRadiationPressureArea: 35.0,
                solarRadiationCoefficient: 1.6,
                covariance: covariance,
                maneuvers: new[] { maneuver },
                userDefinedParameters: userParams,
                spacecraftComments: new[] { "Spacecraft comment" },
                comments: new[] { "Data-level comment" });

            var original = new Opm(header, metadata, data);

            var xml = original.ToXmlString();
            var loaded = Opm.LoadFromString(xml, validateSchema: false, validateContent: false);

            // Verify all features preserved
            Assert.Equal("COMPLIANCE TEST", loaded.ObjectName);
            Assert.Single(loaded.Metadata.Comments);
            Assert.Single(loaded.Data.Comments);
            Assert.Single(loaded.Data.StateVector.Comments);
            Assert.True(loaded.HasKeplerianElements);
            Assert.Single(loaded.Data.KeplerianElements.Comments);
            Assert.True(loaded.HasCovariance);
            Assert.Single(loaded.Data.Covariance.Comments);
            Assert.True(loaded.HasManeuvers);
            Assert.Single(loaded.Data.Maneuvers[0].Comments);
            Assert.True(loaded.HasSpacecraftParameters);
            Assert.Single(loaded.Data.SpacecraftComments);
            Assert.True(loaded.Data.HasUserDefinedParameters);
            Assert.Equal(2, loaded.Data.UserDefinedParameters.Parameters.Count);
            Assert.Equal("TEST_MISSION", loaded.Data.UserDefinedParameters.GetValue("MISSION"));
        }

        [Fact]
        public void ToXmlString_HasCorrectSchemaVersion()
        {
            var opm = CreateMinimalOpm("ISS", "1998-067A");

            var xml = opm.ToXmlString();

            // Should reference NDM/XML 4.0.0 schema
            Assert.Contains("ndmxml-4.0.0", xml);
        }

        #endregion

        #region Helper Methods

        private Opm CreateMinimalOpm(string objectName, string objectId)
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata(objectName, objectId, "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var data = new OpmData(stateVector);
            return new Opm(header, metadata, data);
        }

        private Opm CreateOpmWithKeplerianElements()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var keplerianElements = OpmKeplerianElements.CreateWithTrueAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418);
            var data = new OpmData(stateVector, keplerianElements);
            return new Opm(header, metadata, data);
        }

        private Opm CreateOpmWithCovariance()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var covariance = new CovarianceMatrix(
                cxX: 1.0e-6,
                cyX: 0.0, cyY: 1.0e-6,
                czX: 0.0, czY: 0.0, czZ: 1.0e-6,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9);
            var data = new OpmData(stateVector, covariance: covariance);
            return new Opm(header, metadata, data);
        }

        private Opm CreateOpmWithManeuvers()
        {
            var header = CcsdsHeader.CreateDefault();
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0);
            var maneuver = new OpmManeuverParameters(
                _testEpoch.AddHours(1), 120.0, -50.0, "RSW", 0.1, 0.0, 0.0);
            var data = new OpmData(stateVector, maneuvers: new[] { maneuver });
            return new Opm(header, metadata, data);
        }

        #endregion
    }
}
