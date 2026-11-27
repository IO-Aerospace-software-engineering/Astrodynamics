// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Ccsds;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OMM
{
    public class TleParametersTests
    {
        [Fact]
        public void Constructor_WithBStarAndDDot_Succeeds()
        {
            var tleParams = new TleParameters(
                bStar: 0.0001,
                meanMotionDot: 0.00001,
                meanMotionDDot: 0.000001);

            Assert.Equal(0.0001, tleParams.BStar);
            Assert.Null(tleParams.BTerm);
            Assert.Equal(0.00001, tleParams.MeanMotionDot);
            Assert.Equal(0.000001, tleParams.MeanMotionDDot);
            Assert.Null(tleParams.AGom);
            Assert.True(tleParams.UsesBStar);
            Assert.True(tleParams.UsesMeanMotionDDot);
        }

        [Fact]
        public void CreateWithBStarAndDDot_FactoryMethod_Succeeds()
        {
            var tleParams = TleParameters.CreateWithBStarAndDDot(
                bStar: 0.0001,
                meanMotionDot: 0.00001,
                meanMotionDDot: 0.000001,
                noradCatalogId: 25544);

            Assert.Equal(0.0001, tleParams.BStar);
            Assert.Equal(25544, tleParams.NoradCatalogId);
            Assert.True(tleParams.UsesBStar);
            Assert.True(tleParams.UsesMeanMotionDDot);
        }

        [Fact]
        public void CreateWithBStarAndAGom_FactoryMethod_Succeeds()
        {
            var tleParams = TleParameters.CreateWithBStarAndAGom(
                bStar: 0.0001,
                meanMotionDot: 0.00001,
                agom: 0.02);

            Assert.Equal(0.0001, tleParams.BStar);
            Assert.Equal(0.02, tleParams.AGom);
            Assert.Null(tleParams.MeanMotionDDot);
            Assert.True(tleParams.UsesBStar);
            Assert.False(tleParams.UsesMeanMotionDDot);
        }

        [Fact]
        public void CreateWithBTermAndDDot_FactoryMethod_Succeeds()
        {
            var tleParams = TleParameters.CreateWithBTermAndDDot(
                bterm: 0.05,
                meanMotionDot: 0.00001,
                meanMotionDDot: 0.000001);

            Assert.Null(tleParams.BStar);
            Assert.Equal(0.05, tleParams.BTerm);
            Assert.False(tleParams.UsesBStar);
            Assert.True(tleParams.UsesMeanMotionDDot);
        }

        [Fact]
        public void CreateWithBTermAndAGom_FactoryMethod_Succeeds()
        {
            var tleParams = TleParameters.CreateWithBTermAndAGom(
                bterm: 0.05,
                meanMotionDot: 0.00001,
                agom: 0.02);

            Assert.Null(tleParams.BStar);
            Assert.Equal(0.05, tleParams.BTerm);
            Assert.Equal(0.02, tleParams.AGom);
            Assert.Null(tleParams.MeanMotionDDot);
            Assert.False(tleParams.UsesBStar);
            Assert.False(tleParams.UsesMeanMotionDDot);
        }

        [Fact]
        public void Constructor_WithAllOptionalParameters_Succeeds()
        {
            var comments = new List<string> { "Test TLE" };
            var tleParams = new TleParameters(
                bStar: 0.0001,
                meanMotionDot: 0.00001,
                meanMotionDDot: 0.000001,
                ephemerisType: 0,
                classificationType: "U",
                noradCatalogId: 25544,
                elementSetNumber: 999,
                revolutionNumberAtEpoch: 12345,
                comments: comments);

            Assert.Equal(0, tleParams.EphemerisType);
            Assert.Equal("U", tleParams.ClassificationType);
            Assert.Equal(25544, tleParams.NoradCatalogId);
            Assert.Equal(999, tleParams.ElementSetNumber);
            Assert.Equal(12345, tleParams.RevolutionNumberAtEpoch);
            Assert.Single(tleParams.Comments);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(999)]
        [InlineData(9999)]
        public void Constructor_ValidElementSetNumber_Succeeds(int elementSetNumber)
        {
            var tleParams = TleParameters.CreateWithBStarAndDDot(
                0.0001, 0.00001, 0.000001,
                elementSetNumber: elementSetNumber);

            Assert.Equal(elementSetNumber, tleParams.ElementSetNumber);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(10000)]
        public void Constructor_InvalidElementSetNumber_ThrowsArgumentOutOfRangeException(int elementSetNumber)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TleParameters.CreateWithBStarAndDDot(
                    0.0001, 0.00001, 0.000001,
                    elementSetNumber: elementSetNumber));
        }

        [Fact]
        public void Constructor_NegativeRevolutionNumber_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TleParameters.CreateWithBStarAndDDot(
                    0.0001, 0.00001, 0.000001,
                    revolutionNumberAtEpoch: -1));
        }

        [Theory]
        [InlineData("Constructor")]
        [InlineData("CreateWithBStarAndDDot")]
        [InlineData("CreateWithBStarAndAGom")]
        [InlineData("CreateWithBTermAndDDot")]
        [InlineData("CreateWithBTermAndAGom")]
        public void Entry_NegativeRevolutionNumber_NamesRevolutionNumberAtEpoch(string entry)
        {
            // #734: the exception named revNumber, the private validator's parameter, which no public entry has; every entry
            // takes the revolution number as revolutionNumberAtEpoch
            Func<TleParameters> create = entry switch
            {
                "Constructor" => () => new TleParameters(1e-4, 1e-5, 0.0, revolutionNumberAtEpoch: -1),
                "CreateWithBStarAndDDot" => () => TleParameters.CreateWithBStarAndDDot(1e-4, 1e-5, 0.0, revolutionNumberAtEpoch: -1),
                "CreateWithBStarAndAGom" => () => TleParameters.CreateWithBStarAndAGom(1e-4, 1e-5, 0.01, revolutionNumberAtEpoch: -1),
                "CreateWithBTermAndDDot" => () => TleParameters.CreateWithBTermAndDDot(0.02, 1e-5, 0.0, revolutionNumberAtEpoch: -1),
                _ => () => TleParameters.CreateWithBTermAndAGom(0.02, 1e-5, 0.01, revolutionNumberAtEpoch: -1)
            };
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => create());
            Assert.Equal("revolutionNumberAtEpoch", ex.ParamName);

            // The element set number keeps its name
            ex = Assert.Throws<ArgumentOutOfRangeException>(() => new TleParameters(1e-4, 1e-5, 0.0, elementSetNumber: -1));
            Assert.Equal("elementSetNumber", ex.ParamName);
        }

        [Fact]
        public void Constructor_ZeroRevolutionNumber_Succeeds()
        {
            var tleParams = TleParameters.CreateWithBStarAndDDot(
                0.0001, 0.00001, 0.000001,
                revolutionNumberAtEpoch: 0);

            Assert.Equal(0, tleParams.RevolutionNumberAtEpoch);
        }

        [Fact]
        public void Comments_DefaultsToEmpty()
        {
            var tleParams = new TleParameters(0.0001, 0.00001, 0.000001);

            Assert.NotNull(tleParams.Comments);
            Assert.Empty(tleParams.Comments);
        }

        [Fact]
        public void ToString_WithBStar_ContainsBStarInfo()
        {
            var tleParams = TleParameters.CreateWithBStarAndDDot(
                0.0001, 0.00001, 0.000001, noradCatalogId: 25544);

            var result = tleParams.ToString();

            Assert.Contains("BSTAR", result);
            Assert.Contains("NORAD=25544", result);
        }

        [Fact]
        public void ToString_WithBTerm_ContainsBTermInfo()
        {
            var tleParams = TleParameters.CreateWithBTermAndDDot(
                0.05, 0.00001, 0.000001);

            var result = tleParams.ToString();

            Assert.Contains("BTerm", result);
        }

        [Fact]
        public void ToString_WithAGom_ContainsAGomInfo()
        {
            var tleParams = TleParameters.CreateWithBStarAndAGom(
                0.0001, 0.00001, 0.02);

            var result = tleParams.ToString();

            Assert.Contains("AGOM", result);
        }

        [Theory]
        [InlineData(-0.1)]  // Negative BSTAR
        [InlineData(0.0)]   // Zero BSTAR
        [InlineData(0.1)]   // Positive BSTAR
        public void Constructor_VariousBStarValues_Succeeds(double bstar)
        {
            // BSTAR can be any value (including negative for some edge cases)
            var tleParams = new TleParameters(bstar, 0.0, 0.0);
            Assert.Equal(bstar, tleParams.BStar);
        }

        public static TheoryData<string, string, double> NonFiniteParameters()
        {
            var data = new TheoryData<string, string, double>();
            var entries = new (string Entry, string[] Parameters)[]
            {
                ("Constructor", new[] { "bStar", "meanMotionDot", "meanMotionDDot" }),
                ("CreateWithBStarAndDDot", new[] { "bStar", "meanMotionDot", "meanMotionDDot" }),
                ("CreateWithBStarAndAGom", new[] { "bStar", "meanMotionDot", "agom" }),
                ("CreateWithBTermAndDDot", new[] { "bterm", "meanMotionDot", "meanMotionDDot" }),
                ("CreateWithBTermAndAGom", new[] { "bterm", "meanMotionDot", "agom" })
            };
            foreach (var (entry, parameters) in entries)
            foreach (var parameter in parameters)
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                data.Add(entry, parameter, value);
            return data;
        }

        [Theory]
        [MemberData(nameof(NonFiniteParameters))]
        public void Entry_NonFiniteParameter_ThrowsArgumentOutOfRangeException(string entry, string parameter, double value)
        {
            // BSTAR, BTERM, MEAN_MOTION_DOT, MEAN_MOTION_DDOT and AGOM are xsd:double values the library reads as real
            // numbers (#708, #717); before, NaN and ±Infinity built parameters that Validate() passed (an infinite BSTAR with
            // an information message only) and SaveToStream wrote as NaN, Infinity or -Infinity, which the reader refused
            double V(string name, double given) => name == parameter ? value : given;
            Func<TleParameters> create = entry switch
            {
                "Constructor" => () => new TleParameters(V("bStar", 1e-4), V("meanMotionDot", 1e-5), V("meanMotionDDot", 0.0)),
                "CreateWithBStarAndDDot" => () => TleParameters.CreateWithBStarAndDDot(V("bStar", 1e-4), V("meanMotionDot", 1e-5), V("meanMotionDDot", 0.0)),
                "CreateWithBStarAndAGom" => () => TleParameters.CreateWithBStarAndAGom(V("bStar", 1e-4), V("meanMotionDot", 1e-5), V("agom", 0.01)),
                "CreateWithBTermAndDDot" => () => TleParameters.CreateWithBTermAndDDot(V("bterm", 0.02), V("meanMotionDot", 1e-5), V("meanMotionDDot", 0.0)),
                _ => () => TleParameters.CreateWithBTermAndAGom(V("bterm", 0.02), V("meanMotionDot", 1e-5), V("agom", 0.01))
            };
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => create());
            Assert.Equal(parameter, ex.ParamName);
        }

        [Fact]
        public void Constructor_LargestFiniteValues_AreStoredAsGiven()
        {
            var tleParams = new TleParameters(double.MaxValue, -double.MaxValue, double.Epsilon);
            Assert.Equal(double.MaxValue, tleParams.BStar);
            Assert.Equal(-double.MaxValue, tleParams.MeanMotionDot);
            Assert.Equal(double.Epsilon, tleParams.MeanMotionDDot);
        }
    }
}
