// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
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
    }
}
