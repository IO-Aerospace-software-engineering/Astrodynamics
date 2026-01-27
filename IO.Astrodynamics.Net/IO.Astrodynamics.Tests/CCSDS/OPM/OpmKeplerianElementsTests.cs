// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.CCSDS.OPM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OPM
{
    public class OpmKeplerianElementsTests
    {
        #region CreateWithTrueAnomaly Tests

        [Fact]
        public void CreateWithTrueAnomaly_ValidParameters_Succeeds()
        {
            var kep = OpmKeplerianElements.CreateWithTrueAnomaly(
                semiMajorAxis: 6778.137,
                eccentricity: 0.0001,
                inclination: 51.6,
                raan: 120.0,
                aop: 90.0,
                trueAnomaly: 45.0,
                gm: 398600.4418);

            Assert.Equal(6778.137, kep.SemiMajorAxis, 3);
            Assert.Equal(0.0001, kep.Eccentricity, 6);
            Assert.Equal(51.6, kep.Inclination, 4);
            Assert.Equal(120.0, kep.RightAscensionOfAscendingNode, 4);
            Assert.Equal(90.0, kep.ArgumentOfPericenter, 4);
            Assert.Equal(45.0, kep.TrueAnomaly.Value, 4);
            Assert.Null(kep.MeanAnomaly);
            Assert.Equal(398600.4418, kep.GravitationalParameter, 4);
        }

        [Fact]
        public void CreateWithTrueAnomaly_UsesTrueAnomaly_ReturnsTrue()
        {
            var kep = OpmKeplerianElements.CreateWithTrueAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418);

            Assert.True(kep.UsesTrueAnomaly);
            Assert.False(kep.UsesMeanAnomaly);
        }

        #endregion

        #region CreateWithMeanAnomaly Tests

        [Fact]
        public void CreateWithMeanAnomaly_ValidParameters_Succeeds()
        {
            var kep = OpmKeplerianElements.CreateWithMeanAnomaly(
                semiMajorAxis: 6778.137,
                eccentricity: 0.0001,
                inclination: 51.6,
                raan: 120.0,
                aop: 90.0,
                meanAnomaly: 45.0,
                gm: 398600.4418);

            Assert.Equal(6778.137, kep.SemiMajorAxis, 3);
            Assert.Equal(45.0, kep.MeanAnomaly.Value, 4);
            Assert.Null(kep.TrueAnomaly);
        }

        [Fact]
        public void CreateWithMeanAnomaly_UsesMeanAnomaly_ReturnsTrue()
        {
            var kep = OpmKeplerianElements.CreateWithMeanAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418);

            Assert.True(kep.UsesMeanAnomaly);
            Assert.False(kep.UsesTrueAnomaly);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void CreateWithTrueAnomaly_NegativeSemiMajorAxis_EllipticalOrbit_ThrowsArgumentOutOfRangeException()
        {
            // For elliptical orbits (e < 1), semi-major axis must be positive
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(-6778.137, 0.5, 51.6, 120.0, 90.0, 45.0, 398600.4418));
        }

        [Fact]
        public void CreateWithTrueAnomaly_ZeroSemiMajorAxis_EllipticalOrbit_ThrowsArgumentOutOfRangeException()
        {
            // For elliptical orbits (e < 1), semi-major axis must be positive
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(0.0, 0.5, 51.6, 120.0, 90.0, 45.0, 398600.4418));
        }

        [Fact]
        public void CreateWithTrueAnomaly_HyperbolicOrbit_NegativeSemiMajorAxis_Succeeds()
        {
            // For hyperbolic orbits (e > 1), semi-major axis must be negative
            var kep = OpmKeplerianElements.CreateWithTrueAnomaly(
                semiMajorAxis: -25000.0,  // Negative for hyperbolic
                eccentricity: 1.5,         // e > 1 for hyperbolic
                inclination: 30.0,
                raan: 45.0,
                aop: 60.0,
                trueAnomaly: 90.0,
                gm: 398600.4418);

            Assert.Equal(-25000.0, kep.SemiMajorAxis, 3);
            Assert.Equal(1.5, kep.Eccentricity, 6);
        }

        [Fact]
        public void CreateWithTrueAnomaly_HyperbolicOrbit_PositiveSemiMajorAxis_ThrowsArgumentOutOfRangeException()
        {
            // For hyperbolic orbits (e > 1), semi-major axis must be negative
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(25000.0, 1.5, 51.6, 120.0, 90.0, 45.0, 398600.4418));
        }

        [Fact]
        public void CreateWithTrueAnomaly_HyperbolicOrbit_ZeroSemiMajorAxis_ThrowsArgumentOutOfRangeException()
        {
            // For hyperbolic orbits (e > 1), semi-major axis must be negative (not zero)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(0.0, 1.5, 51.6, 120.0, 90.0, 45.0, 398600.4418));
        }

        [Fact]
        public void CreateWithMeanAnomaly_HyperbolicOrbit_NegativeSemiMajorAxis_Succeeds()
        {
            // For hyperbolic orbits (e > 1), semi-major axis must be negative
            var kep = OpmKeplerianElements.CreateWithMeanAnomaly(
                semiMajorAxis: -50000.0,
                eccentricity: 2.0,
                inclination: 45.0,
                raan: 90.0,
                aop: 180.0,
                meanAnomaly: 30.0,
                gm: 398600.4418);

            Assert.Equal(-50000.0, kep.SemiMajorAxis, 3);
            Assert.Equal(2.0, kep.Eccentricity, 6);
            Assert.True(kep.UsesMeanAnomaly);
        }

        [Fact]
        public void CreateWithTrueAnomaly_NegativeEccentricity_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(6778.137, -0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418));
        }

        [Fact]
        public void CreateWithTrueAnomaly_InclinationNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(6778.137, 0.0001, -1.0, 120.0, 90.0, 45.0, 398600.4418));
        }

        [Fact]
        public void CreateWithTrueAnomaly_InclinationOver180_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(6778.137, 0.0001, 181.0, 120.0, 90.0, 45.0, 398600.4418));
        }

        [Fact]
        public void CreateWithTrueAnomaly_NegativeGM_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, -398600.4418));
        }

        [Fact]
        public void CreateWithTrueAnomaly_ZeroGM_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 0.0));
        }

        #endregion

        #region AnomalyDegrees Tests

        [Fact]
        public void AnomalyDegrees_WithTrueAnomaly_ReturnsTrueAnomaly()
        {
            var kep = OpmKeplerianElements.CreateWithTrueAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418);

            Assert.Equal(45.0, kep.AnomalyDegrees, 4);
        }

        [Fact]
        public void AnomalyDegrees_WithMeanAnomaly_ReturnsMeanAnomaly()
        {
            var kep = OpmKeplerianElements.CreateWithMeanAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 60.0, 398600.4418);

            Assert.Equal(60.0, kep.AnomalyDegrees, 4);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ContainsRelevantInfo()
        {
            var kep = OpmKeplerianElements.CreateWithTrueAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418);

            var result = kep.ToString();

            Assert.Contains("OpmKeplerianElements", result);
            Assert.Contains("6778", result);
            Assert.Contains("TA", result);
        }

        [Fact]
        public void ToString_WithMeanAnomaly_ContainsMA()
        {
            var kep = OpmKeplerianElements.CreateWithMeanAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418);

            var result = kep.ToString();

            Assert.Contains("MA", result);
        }

        #endregion

        #region Comments Tests

        [Fact]
        public void CreateWithTrueAnomaly_WithComments_StoresComments()
        {
            var comments = new[] { "Comment 1", "Comment 2" };
            var kep = OpmKeplerianElements.CreateWithTrueAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418, comments);

            Assert.Equal(2, kep.Comments.Count);
            Assert.Equal("Comment 1", kep.Comments[0]);
        }

        [Fact]
        public void CreateWithTrueAnomaly_NullComments_UsesEmptyArray()
        {
            var kep = OpmKeplerianElements.CreateWithTrueAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418, null);

            Assert.NotNull(kep.Comments);
            Assert.Empty(kep.Comments);
        }

        #endregion
    }
}
