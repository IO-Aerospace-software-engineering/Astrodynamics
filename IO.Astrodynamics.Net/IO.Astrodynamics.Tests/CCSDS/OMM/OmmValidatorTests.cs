// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.CCSDS;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OMM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OMM;

public class OmmValidatorTests
{
    private readonly OmmValidator _validator;
    private readonly OmmReader _reader;

    public OmmValidatorTests()
    {
        _validator = new OmmValidator();
        _reader = new OmmReader();
    }

    #region Valid OMM Tests

    [Fact]
    public void Validate_ValidH2ADEB_ReturnsValid()
    {
        var omm = _reader.ReadFromFile("CCSDS/H2ADEB.omm");

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public void Validate_ValidMinimalOMM_ReturnsValid()
    {
        var omm = CreateValidMinimalOmm();

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public void Validate_ValidTleOMM_ReturnsValid()
    {
        var omm = CreateValidTleOmm();

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
    }

    #endregion

    #region Physical Constraints Tests

    [Fact]
    public void Validate_HighEccentricity_ReturnsInfo()
    {
        // High eccentricity (0.95) is valid but worth noting
        var omm = CreateOmmWithMeanElements(eccentricity: 0.95);

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.Contains(result.Infos, i => i.Path == "Data.MeanElements.Eccentricity");
    }

    [Fact]
    public void Validate_PhysicalConstraintsDisabled_SkipsInfoMessages()
    {
        var validator = new OmmValidator { ValidatePhysicalConstraints = false };
        var omm = CreateOmmWithMeanElements(eccentricity: 0.95);

        var result = validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Infos, i => i.Path == "Data.MeanElements.Eccentricity");
    }

    #endregion

    #region Spacecraft Parameters Tests

    [Fact]
    public void Validate_UnusualDragCoefficient_ReturnsInfo()
    {
        var omm = CreateOmmWithSpacecraftParameters(dragCoeff: 5.0);

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.Contains(result.Infos, i => i.Path == "Data.SpacecraftParameters.DragCoeff");
    }

    [Fact]
    public void Validate_UnusualSolarRadCoeff_ReturnsInfo()
    {
        var omm = CreateOmmWithSpacecraftParameters(solarRadCoeff: 0.3);

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.Contains(result.Infos, i => i.Path == "Data.SpacecraftParameters.SolarRadCoeff");
    }

    [Fact]
    public void Validate_NormalSpacecraftParameters_NoInfo()
    {
        var omm = CreateOmmWithSpacecraftParameters(dragCoeff: 2.2, solarRadCoeff: 1.5);

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Infos, i => i.Path.Contains("SpacecraftParameters"));
    }

    #endregion

    #region Covariance Matrix Tests

    [Fact]
    public void Validate_MissingCovarianceRefFrame_ReturnsWarning()
    {
        var omm = CreateOmmWithCovariance(referenceFrame: null);

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Path == "Data.CovarianceMatrix.ReferenceFrame");
    }

    [Fact]
    public void Validate_CovarianceWithRefFrame_NoWarning()
    {
        var omm = CreateOmmWithCovariance(referenceFrame: "RTN");

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Warnings, w => w.Path == "Data.CovarianceMatrix.ReferenceFrame");
    }

    #endregion

    #region TLE Parameters Tests

    [Fact]
    public void Validate_UnusualBStar_ReturnsInfo()
    {
        var omm = CreateOmmWithTleParameters(bstar: 5.0);

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.Contains(result.Infos, i => i.Path == "Data.TleParameters.BStar");
    }

    [Fact]
    public void Validate_NormalBStar_NoInfo()
    {
        var omm = CreateOmmWithTleParameters(bstar: 0.0001);

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Infos, i => i.Path == "Data.TleParameters.BStar");
    }

    [Fact]
    public void Validate_TleRequirementsDisabled_SkipsTleValidation()
    {
        var validator = new OmmValidator { ValidateTleRequirements = false };
        var omm = CreateOmmWithTleParameters(bstar: 10.0); // unusual value

        var result = validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Infos, i => i.Path.Contains("TleParameters"));
    }

    [Fact]
    public void Validate_NonTemeFrameWithSgp4_ReturnsWarning()
    {
        var metadata = new OmmMetadata(
            objectName: "TEST SAT",
            objectId: "2020-001A",
            centerName: "EARTH",
            referenceFrame: "ICRF",
            timeSystem: "UTC",
            meanElementTheory: "SGP4");

        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch: DateTime.UtcNow,
            meanMotion: 15.5,
            eccentricity: 0.001,
            inclination: 51.6,
            raan: 100.0,
            argOfPericenter: 200.0,
            meanAnomaly: 50.0);

        var omm = new Omm(CcsdsHeader.CreateDefault(), metadata, new OmmData(meanElements));

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Path == "Metadata.ReferenceFrame");
    }

    [Fact]
    public void Validate_TemeFrameWithSgp4_NoWarning()
    {
        var metadata = new OmmMetadata(
            objectName: "TEST SAT",
            objectId: "2020-001A",
            centerName: "EARTH",
            referenceFrame: "TEME",
            timeSystem: "UTC",
            meanElementTheory: "SGP4");

        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch: DateTime.UtcNow,
            meanMotion: 15.5,
            eccentricity: 0.001,
            inclination: 51.6,
            raan: 100.0,
            argOfPericenter: 200.0,
            meanAnomaly: 50.0);

        var omm = new Omm(CcsdsHeader.CreateDefault(), metadata, new OmmData(meanElements));

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Warnings, w => w.Path == "Metadata.ReferenceFrame");
    }

    [Fact]
    public void Validate_NonUtcTimeSystemWithSgp4_ReturnsWarning()
    {
        var metadata = new OmmMetadata(
            objectName: "TEST SAT",
            objectId: "2020-001A",
            centerName: "EARTH",
            referenceFrame: "TEME",
            timeSystem: "TAI",  // Should be UTC for SGP4
            meanElementTheory: "SGP4");

        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch: DateTime.UtcNow,
            meanMotion: 15.5,
            eccentricity: 0.001,
            inclination: 51.6,
            raan: 100.0,
            argOfPericenter: 200.0,
            meanAnomaly: 50.0);

        var omm = new Omm(CcsdsHeader.CreateDefault(), metadata, new OmmData(meanElements));

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Path == "Metadata.TimeSystem");
    }

    [Fact]
    public void Validate_UtcTimeSystemWithSgp4_NoWarning()
    {
        var metadata = new OmmMetadata(
            objectName: "TEST SAT",
            objectId: "2020-001A",
            centerName: "EARTH",
            referenceFrame: "TEME",
            timeSystem: "UTC",
            meanElementTheory: "SGP4");

        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch: DateTime.UtcNow,
            meanMotion: 15.5,
            eccentricity: 0.001,
            inclination: 51.6,
            raan: 100.0,
            argOfPericenter: 200.0,
            meanAnomaly: 50.0);

        var omm = new Omm(CcsdsHeader.CreateDefault(), metadata, new OmmData(meanElements));

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Warnings, w => w.Path == "Metadata.TimeSystem");
    }

    [Fact]
    public void Validate_UnsupportedReferenceFrame_ReturnsWarning()
    {
        var metadata = new OmmMetadata(
            objectName: "TEST SAT",
            objectId: "2020-001A",
            centerName: "EARTH",
            referenceFrame: "RTN",  // Not supported by framework
            timeSystem: "UTC",
            meanElementTheory: "SGP4");

        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch: DateTime.UtcNow,
            meanMotion: 15.5,
            eccentricity: 0.001,
            inclination: 51.6,
            raan: 100.0,
            argOfPericenter: 200.0,
            meanAnomaly: 50.0);

        var omm = new Omm(CcsdsHeader.CreateDefault(), metadata, new OmmData(meanElements));

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);  // Warning, not error
        Assert.Contains(result.Warnings, w =>
            w.Path == "Metadata.ReferenceFrame" &&
            w.Message.Contains("not supported"));
    }

    [Fact]
    public void Validate_UnsupportedTimeSystem_ReturnsWarning()
    {
        var metadata = new OmmMetadata(
            objectName: "TEST SAT",
            objectId: "2020-001A",
            centerName: "EARTH",
            referenceFrame: "TEME",
            timeSystem: "UT1",  // Not supported by framework
            meanElementTheory: "SGP4");

        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch: DateTime.UtcNow,
            meanMotion: 15.5,
            eccentricity: 0.001,
            inclination: 51.6,
            raan: 100.0,
            argOfPericenter: 200.0,
            meanAnomaly: 50.0);

        var omm = new Omm(CcsdsHeader.CreateDefault(), metadata, new OmmData(meanElements));

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);  // Warning, not error
        Assert.Contains(result.Warnings, w =>
            w.Path == "Metadata.TimeSystem" &&
            w.Message.Contains("not supported"));
    }

    [Fact]
    public void Validate_UnsupportedMeanElementTheory_ReturnsWarning()
    {
        var metadata = new OmmMetadata(
            objectName: "TEST SAT",
            objectId: "2020-001A",
            centerName: "EARTH",
            referenceFrame: "TEME",
            timeSystem: "UTC",
            meanElementTheory: "DSST");  // Not supported by framework

        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch: DateTime.UtcNow,
            meanMotion: 15.5,
            eccentricity: 0.001,
            inclination: 51.6,
            raan: 100.0,
            argOfPericenter: 200.0,
            meanAnomaly: 50.0);

        var omm = new Omm(CcsdsHeader.CreateDefault(), metadata, new OmmData(meanElements));

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);  // Warning, not error
        Assert.Contains(result.Warnings, w =>
            w.Path == "Metadata.MeanElementTheory" &&
            w.Message.Contains("not supported"));
    }

    #endregion

    #region Header Validation Tests

    [Fact]
    public void Validate_ValidHeader_NoWarnings()
    {
        var header = new CcsdsHeader(DateTime.UtcNow, "TEST");
        var metadata = OmmMetadata.CreateForSgp4("TEST SAT", "2020-001A");
        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch: DateTime.UtcNow,
            meanMotion: 15.5,
            eccentricity: 0.001,
            inclination: 51.6,
            raan: 100.0,
            argOfPericenter: 200.0,
            meanAnomaly: 50.0);

        var omm = new Omm(header, metadata, new OmmData(meanElements));

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Warnings, w => w.Path == "Header.Originator");
        Assert.DoesNotContain(result.Warnings, w => w.Path == "Header.CreationDate");
    }

    [Fact]
    public void Validate_WarnOnMissingOptionalFieldsDisabled_SkipsWarnings()
    {
        var validator = new OmmValidator { WarnOnMissingOptionalFields = false };
        var omm = CreateOmmWithCovariance(referenceFrame: null); // Would normally warn

        var result = validator.Validate(omm);

        Assert.True(result.IsStrictlyValid);
        Assert.Empty(result.Warnings);
    }

    #endregion

    #region International Designator Tests

    [Fact]
    public void Validate_ValidInternationalDesignator_NoInfo()
    {
        var omm = CreateValidMinimalOmm();

        var result = _validator.Validate(omm);

        Assert.DoesNotContain(result.Infos, i => i.Path == "Metadata.ObjectId");
    }

    [Fact]
    public void Validate_InvalidInternationalDesignator_ReturnsInfo()
    {
        var metadata = OmmMetadata.CreateForSgp4("TEST SAT", "INVALID-ID");
        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch: DateTime.UtcNow,
            meanMotion: 15.5,
            eccentricity: 0.001,
            inclination: 51.6,
            raan: 100.0,
            argOfPericenter: 200.0,
            meanAnomaly: 50.0);

        var omm = new Omm(new CcsdsHeader(DateTime.UtcNow, "TEST"),
            metadata, new OmmData(meanElements));

        var result = _validator.Validate(omm);

        Assert.True(result.IsValid);
        Assert.Contains(result.Infos, i => i.Path == "Metadata.ObjectId");
    }

    #endregion

    #region XSD Schema Validation Tests

    [Fact]
    public void ValidateSchema_ValidH2ADEB_ReturnsValid()
    {
        var result = _validator.ValidateSchema("CCSDS/H2ADEB.omm");

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public void ValidateFile_ValidH2ADEB_ReturnsValid()
    {
        var result = _validator.ValidateFile("CCSDS/H2ADEB.omm");

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void ValidateSchema_NonExistentFile_ReturnsError()
    {
        var result = _validator.ValidateSchema("nonexistent.omm");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("not found"));
    }

    [Fact]
    public void ValidateSchemaFromXml_InvalidXml_ReturnsError()
    {
        var invalidXml = "<invalid>not valid xml</invalid";

        var result = _validator.ValidateSchemaFromXml(invalidXml);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateSchemaFromXml_ValidOmmStructure_Validates()
    {
        // Read H2ADEB.omm as a string and validate
        var xmlContent = System.IO.File.ReadAllText("CCSDS/H2ADEB.omm");

        var result = _validator.ValidateSchemaFromXml(xmlContent);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void ValidateSchemaFromXml_MissingRequiredElement_ReturnsError()
    {
        // OMM without required OBJECT_NAME element
        var invalidOmm = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<omm xmlns=""urn:ccsds:schema:ndmxml"" id=""CCSDS_OMM_VERS"" version=""3.0"">
    <header>
        <CREATION_DATE>2024-01-01T00:00:00Z</CREATION_DATE>
        <ORIGINATOR>TEST</ORIGINATOR>
    </header>
    <body>
        <segment>
            <metadata>
                <OBJECT_ID>2020-001A</OBJECT_ID>
                <CENTER_NAME>EARTH</CENTER_NAME>
                <REF_FRAME>TEME</REF_FRAME>
                <TIME_SYSTEM>UTC</TIME_SYSTEM>
                <MEAN_ELEMENT_THEORY>SGP4</MEAN_ELEMENT_THEORY>
            </metadata>
            <data>
                <meanElements>
                    <EPOCH>2024-01-01T00:00:00Z</EPOCH>
                    <MEAN_MOTION units=""rev/day"">15.5</MEAN_MOTION>
                    <ECCENTRICITY>0.001</ECCENTRICITY>
                    <INCLINATION units=""deg"">51.6</INCLINATION>
                    <RA_OF_ASC_NODE units=""deg"">100.0</RA_OF_ASC_NODE>
                    <ARG_OF_PERICENTER units=""deg"">200.0</ARG_OF_PERICENTER>
                    <MEAN_ANOMALY units=""deg"">50.0</MEAN_ANOMALY>
                </meanElements>
            </data>
        </segment>
    </body>
</omm>";

        var result = _validator.ValidateSchemaFromXml(invalidOmm);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("OBJECT_NAME"));
    }

    #endregion

    #region Result Class Tests

    [Fact]
    public void OmmValidationResult_Success_ReturnsEmptyResult()
    {
        var result = OmmValidationResult.Success();

        Assert.True(result.IsValid);
        Assert.True(result.IsStrictlyValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void OmmValidationResult_Failure_ReturnsResultWithError()
    {
        var result = OmmValidationResult.Failure("CODE", "Message", "Path");

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("CODE", result.Errors.First().Code);
    }

    [Fact]
    public void OmmValidationError_ToString_FormatsCorrectly()
    {
        var error = OmmValidationError.Error("OMM001", "Test message", "Test.Path");

        var str = error.ToString();

        Assert.Contains("Error", str);
        Assert.Contains("OMM001", str);
        Assert.Contains("Test message", str);
        Assert.Contains("Test.Path", str);
    }

    [Fact]
    public void OmmValidationResult_ToString_ReportsStatus()
    {
        var result = new OmmValidationResult();
        Assert.Contains("passed", result.ToString());

        result.AddWarning("W1", "Warning", "Path");
        Assert.Contains("warning", result.ToString());

        result.AddError("E1", "Error", "Path");
        Assert.Contains("failed", result.ToString());
    }

    [Fact]
    public void OmmValidationResult_IsStrictlyValid_FalseWithWarnings()
    {
        var result = new OmmValidationResult();
        Assert.True(result.IsStrictlyValid);

        result.AddWarning("W1", "Warning", "Path");
        Assert.True(result.IsValid);
        Assert.False(result.IsStrictlyValid);
    }

    [Fact]
    public void OmmValidationError_FactoryMethods_CreateCorrectSeverity()
    {
        var error = OmmValidationError.Error("E1", "Error", "Path");
        Assert.Equal(OmmValidationSeverity.Error, error.Severity);

        var warning = OmmValidationError.Warning("W1", "Warning", "Path");
        Assert.Equal(OmmValidationSeverity.Warning, warning.Severity);

        var info = OmmValidationError.Info("I1", "Info", "Path");
        Assert.Equal(OmmValidationSeverity.Info, info.Severity);
    }

    #endregion

    #region Helper Methods

    private static Omm CreateValidMinimalOmm()
    {
        var header = new CcsdsHeader(DateTime.UtcNow, "TEST");
        var metadata = OmmMetadata.CreateForSgp4("TEST SAT", "2020-001A");
        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch: DateTime.UtcNow,
            meanMotion: 15.5,
            eccentricity: 0.001,
            inclination: 51.6,
            raan: 100.0,
            argOfPericenter: 200.0,
            meanAnomaly: 50.0);

        return new Omm(header, metadata, new OmmData(meanElements));
    }

    private static Omm CreateValidTleOmm()
    {
        var header = new CcsdsHeader(DateTime.UtcNow, "18 SPCS");
        var metadata = OmmMetadata.CreateForSgp4("ISS (ZARYA)", "1998-067A");
        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch: DateTime.UtcNow,
            meanMotion: 15.49,
            eccentricity: 0.0007,
            inclination: 51.6,
            raan: 100.0,
            argOfPericenter: 200.0,
            meanAnomaly: 50.0);

        var tleParams = TleParameters.CreateWithBStarAndDDot(
            bstar: 0.0001,
            meanMotionDot: 0.00001,
            meanMotionDDot: 0.0,
            ephemerisType: 0,
            classificationType: "U",
            noradCatalogId: 25544,
            elementSetNumber: 999,
            revolutionNumberAtEpoch: 50000);

        return new Omm(header, metadata, new OmmData(meanElements, tleParameters: tleParams));
    }

    private static Omm CreateOmmWithMeanElements(double eccentricity = 0.001)
    {
        var header = new CcsdsHeader(DateTime.UtcNow, "TEST");
        var metadata = OmmMetadata.CreateForSgp4("TEST SAT", "2020-001A");
        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch: DateTime.UtcNow,
            meanMotion: 15.5,
            eccentricity: eccentricity,
            inclination: 51.6,
            raan: 100.0,
            argOfPericenter: 200.0,
            meanAnomaly: 50.0);

        return new Omm(header, metadata, new OmmData(meanElements));
    }

    private static Omm CreateOmmWithSpacecraftParameters(
        double? mass = 1000,
        double? solarRadArea = 10,
        double? solarRadCoeff = 1.5,
        double? dragArea = 10,
        double? dragCoeff = 2.2)
    {
        var header = new CcsdsHeader(DateTime.UtcNow, "TEST");
        var metadata = OmmMetadata.CreateForSgp4("TEST SAT", "2020-001A");
        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch: DateTime.UtcNow,
            meanMotion: 15.5,
            eccentricity: 0.001,
            inclination: 51.6,
            raan: 100.0,
            argOfPericenter: 200.0,
            meanAnomaly: 50.0);

        var spacecraft = new SpacecraftParameters(mass, solarRadArea, solarRadCoeff, dragArea, dragCoeff);

        return new Omm(header, metadata, new OmmData(meanElements, spacecraftParameters: spacecraft));
    }

    private static Omm CreateOmmWithCovariance(string referenceFrame = null, double cxX = 1.0)
    {
        var header = new CcsdsHeader(DateTime.UtcNow, "TEST");
        var metadata = OmmMetadata.CreateForSgp4("TEST SAT", "2020-001A");
        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch: DateTime.UtcNow,
            meanMotion: 15.5,
            eccentricity: 0.001,
            inclination: 51.6,
            raan: 100.0,
            argOfPericenter: 200.0,
            meanAnomaly: 50.0);

        var covariance = new CovarianceMatrix(
            referenceFrame: referenceFrame,
            cxX: cxX, cyX: 0, cyY: 1.0,
            czX: 0, czY: 0, czZ: 1.0,
            cxDotX: 0, cxDotY: 0, cxDotZ: 0, cxDotXDot: 0.001,
            cyDotX: 0, cyDotY: 0, cyDotZ: 0, cyDotXDot: 0, cyDotYDot: 0.001,
            czDotX: 0, czDotY: 0, czDotZ: 0, czDotXDot: 0, czDotYDot: 0, czDotZDot: 0.001);

        return new Omm(header, metadata, new OmmData(meanElements, covarianceMatrix: covariance));
    }

    private static Omm CreateOmmWithTleParameters(double bstar = 0.0001)
    {
        var header = new CcsdsHeader(DateTime.UtcNow, "TEST");
        var metadata = OmmMetadata.CreateForSgp4("TEST SAT", "2020-001A");
        var meanElements = MeanElements.CreateWithMeanMotion(
            epoch: DateTime.UtcNow,
            meanMotion: 15.5,
            eccentricity: 0.001,
            inclination: 51.6,
            raan: 100.0,
            argOfPericenter: 200.0,
            meanAnomaly: 50.0);

        var tleParams = TleParameters.CreateWithBStarAndDDot(
            bstar: bstar,
            meanMotionDot: 0.00001,
            meanMotionDDot: 0.0,
            ephemerisType: 0,
            classificationType: "U",
            noradCatalogId: 25544,
            elementSetNumber: 999,
            revolutionNumberAtEpoch: 50000);

        return new Omm(header, metadata, new OmmData(meanElements, tleParameters: tleParams));
    }

    #endregion
}
