// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.CCSDS.OMM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OMM
{
    public class MeanElementsTests
    {
        private readonly DateTime _testEpoch = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Constructor_WithSemiMajorAxis_Succeeds()
        {
            var elements = new MeanElements(
                _testEpoch,
                semiMajorAxis: 7000.0,
                eccentricity: 0.001,
                inclination: 51.6,
                rightAscensionOfAscendingNode: 120.0,
                argumentOfPericenter: 90.0,
                meanAnomaly: 45.0);

            Assert.Equal(_testEpoch, elements.Epoch);
            Assert.Equal(7000.0, elements.SemiMajorAxis);
            Assert.Null(elements.MeanMotion);
            Assert.Equal(0.001, elements.Eccentricity);
            Assert.Equal(51.6, elements.Inclination);
            Assert.False(elements.UsesMeanMotion);
        }

        [Fact]
        public void Constructor_WithMeanMotion_Succeeds()
        {
            var elements = new MeanElements(
                _testEpoch,
                meanMotion: 15.5,
                eccentricity: 0.0001,
                inclination: 51.6,
                rightAscensionOfAscendingNode: 120.0,
                argumentOfPericenter: 90.0,
                meanAnomaly: 45.0,
                useMeanMotion: true);

            Assert.Equal(_testEpoch, elements.Epoch);
            Assert.Null(elements.SemiMajorAxis);
            Assert.Equal(15.5, elements.MeanMotion);
            Assert.True(elements.UsesMeanMotion);
        }

        [Fact]
        public void CreateWithSemiMajorAxis_FactoryMethod_Succeeds()
        {
            var elements = MeanElements.CreateWithSemiMajorAxis(
                _testEpoch, 7000.0, 0.001, 51.6, 120.0, 90.0, 45.0);

            Assert.Equal(7000.0, elements.SemiMajorAxis);
            Assert.False(elements.UsesMeanMotion);
        }

        [Fact]
        public void CreateWithMeanMotion_FactoryMethod_Succeeds()
        {
            var elements = MeanElements.CreateWithMeanMotion(
                _testEpoch, 15.5, 0.001, 51.6, 120.0, 90.0, 45.0);

            Assert.Equal(15.5, elements.MeanMotion);
            Assert.True(elements.UsesMeanMotion);
        }

        [Fact]
        public void Constructor_WithGravitationalParameter_Succeeds()
        {
            var elements = new MeanElements(
                _testEpoch, 7000.0, 0.001, 51.6, 120.0, 90.0, 45.0,
                gravitationalParameter: 398600.4418);

            Assert.Equal(398600.4418, elements.GravitationalParameter);
        }

        [Fact]
        public void Constructor_WithComments_Succeeds()
        {
            var comments = new List<string> { "Test comment", "Another comment" };
            var elements = new MeanElements(
                _testEpoch, 7000.0, 0.001, 51.6, 120.0, 90.0, 45.0,
                comments: comments);

            Assert.Equal(2, elements.Comments.Count);
            Assert.Equal("Test comment", elements.Comments[0]);
        }

        [Fact]
        public void Constructor_NegativeSemiMajorAxis_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MeanElements(_testEpoch, -7000.0, 0.001, 51.6, 120.0, 90.0, 45.0));
        }

        [Fact]
        public void Constructor_ZeroSemiMajorAxis_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MeanElements(_testEpoch, 0.0, 0.001, 51.6, 120.0, 90.0, 45.0));
        }

        [Fact]
        public void Constructor_NegativeMeanMotion_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MeanElements(_testEpoch, -15.5, 0.001, 51.6, 120.0, 90.0, 45.0, true));
        }

        [Fact]
        public void Constructor_NegativeEccentricity_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MeanElements(_testEpoch, 7000.0, -0.1, 51.6, 120.0, 90.0, 45.0));
        }

        [Fact]
        public void Constructor_InclinationOutOfRange_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MeanElements(_testEpoch, 7000.0, 0.001, 200.0, 120.0, 90.0, 45.0));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MeanElements(_testEpoch, 7000.0, 0.001, -10.0, 120.0, 90.0, 45.0));
        }

        [Fact]
        public void Constructor_NegativeGravitationalParameter_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MeanElements(_testEpoch, 7000.0, 0.001, 51.6, 120.0, 90.0, 45.0,
                    gravitationalParameter: -1.0));
        }

        [Fact]
        public void Constructor_UseMeanMotionFalse_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new MeanElements(_testEpoch, 15.5, 0.001, 51.6, 120.0, 90.0, 45.0, false));
        }

        [Fact]
        public void Comments_DefaultsToEmpty()
        {
            var elements = new MeanElements(
                _testEpoch, 7000.0, 0.001, 51.6, 120.0, 90.0, 45.0);

            Assert.NotNull(elements.Comments);
            Assert.Empty(elements.Comments);
        }

        [Fact]
        public void ToString_ContainsRelevantInfo()
        {
            var elements = MeanElements.CreateWithSemiMajorAxis(
                _testEpoch, 7000.0, 0.0012345, 51.6, 120.0, 90.0, 45.0);

            var result = elements.ToString();

            Assert.Contains("MeanElements", result);
            Assert.Contains("a=", result);
            Assert.Contains("7000", result);
        }

        [Fact]
        public void ToString_WithMeanMotion_ContainsMeanMotionInfo()
        {
            var elements = MeanElements.CreateWithMeanMotion(
                _testEpoch, 15.5, 0.0012345, 51.6, 120.0, 90.0, 45.0);

            var result = elements.ToString();

            Assert.Contains("n=", result);
            Assert.Contains("rev/day", result);
        }

        [Theory]
        [InlineData(0.0, 51.6, 120.0, 90.0, 45.0)]  // Zero eccentricity (circular)
        [InlineData(0.9, 51.6, 120.0, 90.0, 45.0)]  // High eccentricity
        [InlineData(0.001, 0.0, 120.0, 90.0, 45.0)] // Zero inclination (equatorial)
        [InlineData(0.001, 180.0, 120.0, 90.0, 45.0)] // 180 degree inclination
        [InlineData(0.001, 90.0, 0.0, 0.0, 0.0)]    // Polar, zero angles
        public void Constructor_ValidEdgeCases_Succeeds(
            double eccentricity, double inclination, double raan, double aop, double ma)
        {
            var elements = new MeanElements(
                _testEpoch, 7000.0, eccentricity, inclination, raan, aop, ma);

            Assert.Equal(eccentricity, elements.Eccentricity);
            Assert.Equal(inclination, elements.Inclination);
        }
    }
}
