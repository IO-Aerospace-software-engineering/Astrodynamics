// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OPM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OPM
{
    public class OpmDataTests
    {
        private readonly DateTime _testEpoch = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        #region Constructor Tests

        [Fact]
        public void Constructor_MinimalWithStateVector_Succeeds()
        {
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);

            var data = new OpmData(stateVector);

            Assert.NotNull(data.StateVector);
            Assert.Equal(stateVector, data.StateVector);
            Assert.False(data.HasKeplerianElements);
            Assert.False(data.HasSpacecraftParameters);
            Assert.False(data.HasCovariance);
            Assert.False(data.HasManeuvers);
        }

        [Fact]
        public void Constructor_NullStateVector_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new OpmData(null));
        }

        [Fact]
        public void Constructor_WithKeplerianElements_SetsHasKeplerianElements()
        {
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);
            var keplerianElements = OpmKeplerianElements.CreateWithTrueAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418);

            var data = new OpmData(stateVector, keplerianElements);

            Assert.True(data.HasKeplerianElements);
            Assert.Equal(keplerianElements, data.KeplerianElements);
        }

        [Fact]
        public void Constructor_WithMass_SetsHasSpacecraftParameters()
        {
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);

            var data = new OpmData(stateVector, mass: 420000.0);

            Assert.True(data.HasSpacecraftParameters);
            Assert.Equal(420000.0, data.Mass.Value, 0);
        }

        [Fact]
        public void Constructor_WithDragCoefficient_SetsHasSpacecraftParameters()
        {
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);

            var data = new OpmData(stateVector, dragCoefficient: 2.2);

            Assert.True(data.HasSpacecraftParameters);
            Assert.Equal(2.2, data.DragCoefficient.Value, 1);
        }

        [Fact]
        public void Constructor_WithCovariance_SetsHasCovariance()
        {
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);
            var covariance = CreateDiagonalCovariance();

            var data = new OpmData(stateVector, covariance: covariance);

            Assert.True(data.HasCovariance);
            Assert.Equal(covariance, data.Covariance);
        }

        [Fact]
        public void Constructor_WithManeuvers_SetsHasManeuvers()
        {
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);
            var maneuver = new OpmManeuverParameters(
                _testEpoch.AddHours(1), 120.0, -50.0, "RSW", 0.1, 0.0, 0.0);

            var data = new OpmData(stateVector, maneuvers: new[] { maneuver });

            Assert.True(data.HasManeuvers);
            Assert.Single(data.Maneuvers);
        }

        [Fact]
        public void Constructor_WithMultipleManeuvers_StoresAll()
        {
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);
            var maneuver1 = new OpmManeuverParameters(
                _testEpoch.AddHours(1), 120.0, -50.0, "RSW", 0.1, 0.0, 0.0);
            var maneuver2 = new OpmManeuverParameters(
                _testEpoch.AddHours(2), 60.0, -25.0, "RSW", 0.05, 0.0, 0.0);

            var data = new OpmData(stateVector, maneuvers: new[] { maneuver1, maneuver2 });

            Assert.Equal(2, data.Maneuvers.Count);
        }

        #endregion

        #region Spacecraft Parameters Tests

        [Fact]
        public void SpacecraftParameters_AllSet_HasCorrectValues()
        {
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);

            var data = new OpmData(
                stateVector,
                mass: 420000.0,
                solarRadiationPressureArea: 100.0,
                solarRadiationCoefficient: 1.5,
                dragArea: 80.0,
                dragCoefficient: 2.2);

            Assert.True(data.HasSpacecraftParameters);
            Assert.Equal(420000.0, data.Mass.Value, 0);
            Assert.Equal(100.0, data.SolarRadiationPressureArea.Value, 0);
            Assert.Equal(1.5, data.SolarRadiationCoefficient.Value, 1);
            Assert.Equal(80.0, data.DragArea.Value, 0);
            Assert.Equal(2.2, data.DragCoefficient.Value, 1);
        }

        [Fact]
        public void SpacecraftParameters_NoneSet_HasSpacecraftParametersFalse()
        {
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);

            var data = new OpmData(stateVector);

            Assert.False(data.HasSpacecraftParameters);
            Assert.Null(data.Mass);
            Assert.Null(data.DragCoefficient);
        }

        #endregion

        #region Helper Methods

        private static CovarianceMatrix CreateDiagonalCovariance()
        {
            return new CovarianceMatrix(
                cxX: 1.0e-6,
                cyX: 0.0, cyY: 1.0e-6,
                czX: 0.0, czY: 0.0, czZ: 1.0e-6,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9);
        }

        #endregion
    }
}
