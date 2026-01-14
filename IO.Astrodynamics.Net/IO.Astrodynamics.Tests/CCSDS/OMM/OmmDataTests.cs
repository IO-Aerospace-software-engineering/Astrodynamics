// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OMM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OMM
{
    public class OmmDataTests
    {
        private readonly DateTime _testEpoch = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        private MeanElements CreateTestMeanElements()
        {
            return MeanElements.CreateWithMeanMotion(
                _testEpoch, 15.5, 0.0001, 51.6, 120.0, 90.0, 45.0);
        }

        private TleParameters CreateTestTleParameters()
        {
            return TleParameters.CreateWithBStarAndDDot(
                0.0001, 0.00001, 0.000001, noradCatalogId: 25544);
        }

        [Fact]
        public void Constructor_WithMeanElementsOnly_Succeeds()
        {
            var meanElements = CreateTestMeanElements();
            var data = new OmmData(meanElements);

            Assert.NotNull(data.MeanElements);
            Assert.Equal(meanElements, data.MeanElements);
            Assert.Null(data.SpacecraftParameters);
            Assert.Null(data.TleParameters);
            Assert.Null(data.CovarianceMatrix);
            Assert.Null(data.UserDefinedParameters);
        }

        [Fact]
        public void Constructor_NullMeanElements_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new OmmData(null));
        }

        [Fact]
        public void Constructor_WithAllParameters_Succeeds()
        {
            var meanElements = CreateTestMeanElements();
            var spacecraftParams = new SpacecraftParameters(mass: 420000.0);
            var tleParams = CreateTestTleParameters();
            var covariance = CreateDiagonalCovariance();
            var userDefined = new UserDefinedParameters(
                new List<UserDefinedParameter> { new("MISSION", "ISS") });
            var comments = new List<string> { "Test data" };

            var data = new OmmData(
                meanElements, spacecraftParams, tleParams, covariance, userDefined, comments);

            Assert.Equal(meanElements, data.MeanElements);
            Assert.Equal(spacecraftParams, data.SpacecraftParameters);
            Assert.Equal(tleParams, data.TleParameters);
            Assert.Equal(covariance, data.CovarianceMatrix);
            Assert.Equal(userDefined, data.UserDefinedParameters);
            Assert.Single(data.Comments);
        }

        [Fact]
        public void HasSpacecraftParameters_WithSpacecraftParams_ReturnsTrue()
        {
            var data = new OmmData(
                CreateTestMeanElements(),
                spacecraftParameters: new SpacecraftParameters(mass: 1000.0));

            Assert.True(data.HasSpacecraftParameters);
        }

        [Fact]
        public void HasSpacecraftParameters_WithoutSpacecraftParams_ReturnsFalse()
        {
            var data = new OmmData(CreateTestMeanElements());

            Assert.False(data.HasSpacecraftParameters);
        }

        [Fact]
        public void HasTleParameters_WithTleParams_ReturnsTrue()
        {
            var data = new OmmData(
                CreateTestMeanElements(),
                tleParameters: CreateTestTleParameters());

            Assert.True(data.HasTleParameters);
        }

        [Fact]
        public void HasTleParameters_WithoutTleParams_ReturnsFalse()
        {
            var data = new OmmData(CreateTestMeanElements());

            Assert.False(data.HasTleParameters);
        }

        [Fact]
        public void HasCovarianceMatrix_WithCovariance_ReturnsTrue()
        {
            var data = new OmmData(
                CreateTestMeanElements(),
                covarianceMatrix: CreateDiagonalCovariance());

            Assert.True(data.HasCovarianceMatrix);
        }

        [Fact]
        public void HasCovarianceMatrix_WithoutCovariance_ReturnsFalse()
        {
            var data = new OmmData(CreateTestMeanElements());

            Assert.False(data.HasCovarianceMatrix);
        }

        [Fact]
        public void HasUserDefinedParameters_WithParameters_ReturnsTrue()
        {
            var udp = new UserDefinedParameters(
                new List<UserDefinedParameter> { new("KEY", "VALUE") });
            var data = new OmmData(
                CreateTestMeanElements(),
                userDefinedParameters: udp);

            Assert.True(data.HasUserDefinedParameters);
        }

        [Fact]
        public void HasUserDefinedParameters_WithEmptyParameters_ReturnsFalse()
        {
            var udp = new UserDefinedParameters();
            var data = new OmmData(
                CreateTestMeanElements(),
                userDefinedParameters: udp);

            Assert.False(data.HasUserDefinedParameters);
        }

        [Fact]
        public void HasUserDefinedParameters_WithoutParameters_ReturnsFalse()
        {
            var data = new OmmData(CreateTestMeanElements());

            Assert.False(data.HasUserDefinedParameters);
        }

        [Fact]
        public void CreateMinimal_FactoryMethod_Succeeds()
        {
            var meanElements = CreateTestMeanElements();
            var data = OmmData.CreateMinimal(meanElements);

            Assert.Equal(meanElements, data.MeanElements);
            Assert.False(data.HasSpacecraftParameters);
            Assert.False(data.HasTleParameters);
            Assert.False(data.HasCovarianceMatrix);
            Assert.False(data.HasUserDefinedParameters);
        }

        [Fact]
        public void CreateForTle_FactoryMethod_Succeeds()
        {
            var meanElements = CreateTestMeanElements();
            var tleParams = CreateTestTleParameters();

            var data = OmmData.CreateForTle(meanElements, tleParams);

            Assert.Equal(meanElements, data.MeanElements);
            Assert.Equal(tleParams, data.TleParameters);
            Assert.True(data.HasTleParameters);
        }

        [Fact]
        public void CreateForTle_NullTleParameters_ThrowsArgumentNullException()
        {
            var meanElements = CreateTestMeanElements();

            Assert.Throws<ArgumentNullException>(() =>
                OmmData.CreateForTle(meanElements, null));
        }

        [Fact]
        public void Comments_DefaultsToEmpty()
        {
            var data = new OmmData(CreateTestMeanElements());

            Assert.NotNull(data.Comments);
            Assert.Empty(data.Comments);
        }

        [Fact]
        public void ToString_MinimalData_ContainsMeanElements()
        {
            var data = OmmData.CreateMinimal(CreateTestMeanElements());
            var result = data.ToString();

            Assert.Contains("MeanElements", result);
            Assert.DoesNotContain("TLE", result);
        }

        [Fact]
        public void ToString_WithTle_ContainsTle()
        {
            var data = OmmData.CreateForTle(CreateTestMeanElements(), CreateTestTleParameters());
            var result = data.ToString();

            Assert.Contains("MeanElements", result);
            Assert.Contains("TLE", result);
        }

        [Fact]
        public void ToString_WithAllOptional_ContainsAllMarkers()
        {
            var data = new OmmData(
                CreateTestMeanElements(),
                spacecraftParameters: new SpacecraftParameters(mass: 1000),
                tleParameters: CreateTestTleParameters(),
                covarianceMatrix: CreateDiagonalCovariance(),
                userDefinedParameters: new UserDefinedParameters(
                    new List<UserDefinedParameter> { new("K", "V") }));

            var result = data.ToString();

            Assert.Contains("MeanElements", result);
            Assert.Contains("SC", result);
            Assert.Contains("TLE", result);
            Assert.Contains("Cov", result);
            Assert.Contains("UDP", result);
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
