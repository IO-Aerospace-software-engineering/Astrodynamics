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

        #region Non-finite and boundary values (#708)

        public static TheoryData<string, double> NonFiniteRangedParameters()
        {
            var data = new TheoryData<string, double>();
            foreach (var parameter in new[] { "semiMajorAxis", "meanMotion", "eccentricity", "inclination", "raan", "argOfPericenter", "meanAnomaly", "gm" })
            {
                foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                {
                    data.Add(parameter, value);
                }
            }

            return data;
        }

        /// <summary>The ISS-like elements of the tests with one parameter replaced, through the factory of the semi-major
        /// axis (or of the mean motion for meanMotion), and through both for the parameters they share.</summary>
        private IEnumerable<Func<MeanElements>> BuildWith(string parameter, double value)
        {
            double V(string name, double nominal) => name == parameter ? value : nominal;
            if (parameter != "meanMotion")
            {
                yield return () => MeanElements.CreateWithSemiMajorAxis(_testEpoch, V("semiMajorAxis", 6778.0), V("eccentricity", 0.0005),
                    V("inclination", 51.6), V("raan", 120.0), V("argOfPericenter", 90.0), V("meanAnomaly", 45.0), V("gm", 398600.4418));
                yield return () => new MeanElements(_testEpoch, V("semiMajorAxis", 6778.0), V("eccentricity", 0.0005),
                    V("inclination", 51.6), V("raan", 120.0), V("argOfPericenter", 90.0), V("meanAnomaly", 45.0), V("gm", 398600.4418));
            }

            if (parameter != "semiMajorAxis")
            {
                yield return () => MeanElements.CreateWithMeanMotion(_testEpoch, V("meanMotion", 15.5), V("eccentricity", 0.0005),
                    V("inclination", 51.6), V("raan", 120.0), V("argOfPericenter", 90.0), V("meanAnomaly", 45.0), V("gm", 398600.4418));
                yield return () => new MeanElements(_testEpoch, V("meanMotion", 15.5), V("eccentricity", 0.0005),
                    V("inclination", 51.6), V("raan", 120.0), V("argOfPericenter", 90.0), V("meanAnomaly", 45.0), true, V("gm", 398600.4418));
            }
        }

        [Theory]
        [MemberData(nameof(NonFiniteRangedParameters))]
        public void Constructor_NonFiniteRangedParameter_ThrowsArgumentOutOfRangeException(string parameter, double value)
        {
            // NaN and ±Infinity are in none of the documented ranges (a > 0, n > 0, e >= 0, i in [0, 180], the angles in
            // [-360, 360), GM > 0; ndmxml-4.0.0-common-4.0.xsd): before, NaN (and +Infinity for the half-open ranges)
            // passed the checks, written as the complement of the range
            foreach (var build in BuildWith(parameter, value))
            {
                var ex = Assert.Throws<ArgumentOutOfRangeException>(() => build());
                Assert.Equal(parameter, ex.ParamName);
            }
        }

        [Theory]
        [InlineData("eccentricity", 0.0)]
        [InlineData("eccentricity", -0.0)]
        [InlineData("inclination", 0.0)]
        [InlineData("inclination", 180.0)]
        [InlineData("raan", -360.0)]
        [InlineData("argOfPericenter", -360.0)]
        [InlineData("meanAnomaly", 359.999)]
        [InlineData("semiMajorAxis", double.Epsilon)]
        [InlineData("gm", double.MaxValue)]
        public void Constructor_BoundaryValue_IsStoredAsGiven(string parameter, double value)
        {
            foreach (var build in BuildWith(parameter, value))
            {
                var elements = build();
                double stored = parameter switch
                {
                    "eccentricity" => elements.Eccentricity,
                    "inclination" => elements.Inclination,
                    "raan" => elements.RightAscensionOfAscendingNode,
                    "argOfPericenter" => elements.ArgumentOfPericenter,
                    "meanAnomaly" => elements.MeanAnomaly,
                    "semiMajorAxis" => elements.SemiMajorAxis!.Value,
                    _ => elements.GravitationalParameter!.Value
                };
                Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(stored));
            }
        }

        [Theory]
        [InlineData("raan")]
        [InlineData("argOfPericenter")]
        [InlineData("meanAnomaly")]
        public void Constructor_AngleAt360_MessageStatesHalfOpenRange(string parameter)
        {
            // The range is [-360, 360) (angleRange of ndmxml-4.0.0-common-4.0.xsd: minInclusive -360, maxExclusive 360);
            // before, the message said "between -360 and 360", which 360 is
            foreach (var build in BuildWith(parameter, 360.0))
            {
                var ex = Assert.Throws<ArgumentOutOfRangeException>(() => build());
                Assert.Equal(parameter, ex.ParamName);
                Assert.Contains("[-360, 360)", ex.Message);
            }
        }

        #endregion
    }
}
