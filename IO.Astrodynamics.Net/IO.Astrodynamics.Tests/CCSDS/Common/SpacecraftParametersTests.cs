// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Ccsds;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.Common
{
    public class SpacecraftParametersTests
    {
        [Fact]
        public void Constructor_WithNoParameters_CreatesEmptyInstance()
        {
            var parameters = new SpacecraftParameters();

            Assert.Null(parameters.Mass);
            Assert.Null(parameters.SolarRadArea);
            Assert.Null(parameters.SolarRadCoeff);
            Assert.Null(parameters.DragArea);
            Assert.Null(parameters.DragCoeff);
            Assert.Empty(parameters.Comments);
            Assert.False(parameters.HasAnyValue);
        }

        [Fact]
        public void Constructor_WithAllParameters_Succeeds()
        {
            var comments = new List<string> { "ISS parameters" };
            var parameters = new SpacecraftParameters(
                mass: 420000.0,
                solarRadArea: 2500.0,
                solarRadCoeff: 1.5,
                dragArea: 1500.0,
                dragCoeff: 2.2,
                comments: comments);

            Assert.Equal(420000.0, parameters.Mass);
            Assert.Equal(2500.0, parameters.SolarRadArea);
            Assert.Equal(1.5, parameters.SolarRadCoeff);
            Assert.Equal(1500.0, parameters.DragArea);
            Assert.Equal(2.2, parameters.DragCoeff);
            Assert.Single(parameters.Comments);
            Assert.True(parameters.HasAnyValue);
        }

        [Fact]
        public void Constructor_WithPartialParameters_Succeeds()
        {
            var parameters = new SpacecraftParameters(mass: 1000.0, dragCoeff: 2.2);

            Assert.Equal(1000.0, parameters.Mass);
            Assert.Null(parameters.SolarRadArea);
            Assert.Null(parameters.SolarRadCoeff);
            Assert.Null(parameters.DragArea);
            Assert.Equal(2.2, parameters.DragCoeff);
            Assert.True(parameters.HasAnyValue);
        }

        [Fact]
        public void Constructor_WithNegativeMass_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpacecraftParameters(mass: -100.0));
        }

        [Fact]
        public void Constructor_WithNegativeSolarRadArea_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpacecraftParameters(solarRadArea: -10.0));
        }

        [Fact]
        public void Constructor_WithNegativeSolarRadCoeff_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpacecraftParameters(solarRadCoeff: -0.5));
        }

        [Fact]
        public void Constructor_WithNegativeDragArea_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpacecraftParameters(dragArea: -10.0));
        }

        [Fact]
        public void Constructor_WithNegativeDragCoeff_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpacecraftParameters(dragCoeff: -2.2));
        }

        [Fact]
        public void Constructor_WithZeroValues_Succeeds()
        {
            var parameters = new SpacecraftParameters(
                mass: 0.0,
                solarRadArea: 0.0,
                solarRadCoeff: 0.0,
                dragArea: 0.0,
                dragCoeff: 0.0);

            Assert.Equal(0.0, parameters.Mass);
            Assert.Equal(0.0, parameters.DragCoeff);
            Assert.True(parameters.HasAnyValue);
        }

        [Fact]
        public void ToString_ReturnsFormattedString()
        {
            var parameters = new SpacecraftParameters(mass: 1000.0, dragArea: 10.0, dragCoeff: 2.2);

            var result = parameters.ToString();

            Assert.Contains("1000", result);
            Assert.Contains("10", result);
            Assert.Contains("2.2", result);
        }

        public static TheoryData<string, double> NonFiniteParameters()
        {
            var data = new TheoryData<string, double>();
            foreach (var parameter in new[] { "mass", "solarRadArea", "solarRadCoeff", "dragArea", "dragCoeff" })
            {
                data.Add(parameter, double.NaN);
                data.Add(parameter, double.PositiveInfinity);
                data.Add(parameter, double.NegativeInfinity);
            }

            return data;
        }

        private static SpacecraftParameters BuildWith(string parameter, double value) => new(
            mass: parameter == "mass" ? value : 420000.0,
            solarRadArea: parameter == "solarRadArea" ? value : 2500.0,
            solarRadCoeff: parameter == "solarRadCoeff" ? value : 1.2,
            dragArea: parameter == "dragArea" ? value : 2500.0,
            dragCoeff: parameter == "dragCoeff" ? value : 2.2);

        [Theory]
        [MemberData(nameof(NonFiniteParameters))]
        public void Constructor_NonFiniteParameter_ThrowsArgumentOutOfRangeException(string parameter, double value)
        {
            // Each is a finite value of 0 or more (nonNegativeDouble, massType and areaType of ndmxml-4.0.0-common-4.0.xsd);
            // NaN and +Infinity passed the check "< 0" before (#708)
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => BuildWith(parameter, value));
            Assert.Equal(parameter, ex.ParamName);
        }

        [Theory]
        [InlineData("mass")]
        [InlineData("solarRadArea")]
        [InlineData("solarRadCoeff")]
        [InlineData("dragArea")]
        [InlineData("dragCoeff")]
        public void Constructor_ZeroParameter_IsStoredAsGiven(string parameter)
        {
            var parameters = BuildWith(parameter, 0.0);
            double? stored = parameter switch
            {
                "mass" => parameters.Mass,
                "solarRadArea" => parameters.SolarRadArea,
                "solarRadCoeff" => parameters.SolarRadCoeff,
                "dragArea" => parameters.DragArea,
                _ => parameters.DragCoeff
            };
            Assert.Equal(0.0, stored);
        }
    }
}
