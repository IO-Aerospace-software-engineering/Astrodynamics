// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.CCSDS.OPM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OPM
{
    public class OpmManeuverParametersTests
    {
        private readonly DateTime _testEpoch = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        #region Constructor Tests

        [Fact]
        public void Constructor_ValidParameters_Succeeds()
        {
            var maneuver = new OpmManeuverParameters(
                manEpochIgnition: _testEpoch,
                manDuration: 120.0,
                manDeltaMass: -50.0,
                manRefFrame: "RSW",
                manDv1: 0.1,
                manDv2: 0.05,
                manDv3: 0.02);

            Assert.Equal(_testEpoch, maneuver.ManEpochIgnition);
            Assert.Equal(120.0, maneuver.ManDuration);
            Assert.Equal(-50.0, maneuver.ManDeltaMass);
            Assert.Equal("RSW", maneuver.ManRefFrame);
            Assert.Equal(0.1, maneuver.ManDv1);
            Assert.Equal(0.05, maneuver.ManDv2);
            Assert.Equal(0.02, maneuver.ManDv3);
        }

        [Fact]
        public void Constructor_ZeroDuration_Succeeds()
        {
            var maneuver = new OpmManeuverParameters(
                _testEpoch, 0.0, -50.0, "RSW", 0.1, 0.0, 0.0);

            Assert.Equal(0.0, maneuver.ManDuration);
        }

        [Fact]
        public void Constructor_ZeroDeltaMass_Succeeds()
        {
            var maneuver = new OpmManeuverParameters(
                _testEpoch, 120.0, 0.0, "RSW", 0.1, 0.0, 0.0);

            Assert.Equal(0.0, maneuver.ManDeltaMass);
        }

        [Fact]
        public void Constructor_NegativeDuration_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new OpmManeuverParameters(_testEpoch, -1.0, -50.0, "RSW", 0.1, 0.0, 0.0));
        }

        [Fact]
        public void Constructor_PositiveDeltaMass_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new OpmManeuverParameters(_testEpoch, 120.0, 50.0, "RSW", 0.1, 0.0, 0.0));
        }

        [Fact]
        public void Constructor_NullRefFrame_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new OpmManeuverParameters(_testEpoch, 120.0, -50.0, null, 0.1, 0.0, 0.0));
        }

        [Fact]
        public void Constructor_EmptyRefFrame_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new OpmManeuverParameters(_testEpoch, 120.0, -50.0, "", 0.1, 0.0, 0.0));
        }

        #endregion

        #region Reference Frame Tests

        [Fact]
        public void Constructor_RSWFrame_Succeeds()
        {
            var maneuver = new OpmManeuverParameters(
                _testEpoch, 120.0, -50.0, "RSW", 0.1, 0.0, 0.0);

            Assert.Equal("RSW", maneuver.ManRefFrame);
        }

        [Fact]
        public void Constructor_RTNFrame_Succeeds()
        {
            var maneuver = new OpmManeuverParameters(
                _testEpoch, 120.0, -50.0, "RTN", 0.1, 0.0, 0.0);

            Assert.Equal("RTN", maneuver.ManRefFrame);
        }

        [Fact]
        public void Constructor_TNWFrame_Succeeds()
        {
            var maneuver = new OpmManeuverParameters(
                _testEpoch, 120.0, -50.0, "TNW", 0.1, 0.0, 0.0);

            Assert.Equal("TNW", maneuver.ManRefFrame);
        }

        #endregion

        #region Delta-V Tests

        [Fact]
        public void Constructor_PositiveDeltaV_Succeeds()
        {
            var maneuver = new OpmManeuverParameters(
                _testEpoch, 120.0, -50.0, "RSW", 0.5, 0.3, 0.1);

            Assert.Equal(0.5, maneuver.ManDv1);
            Assert.Equal(0.3, maneuver.ManDv2);
            Assert.Equal(0.1, maneuver.ManDv3);
        }

        [Fact]
        public void Constructor_NegativeDeltaV_Succeeds()
        {
            var maneuver = new OpmManeuverParameters(
                _testEpoch, 120.0, -50.0, "RSW", -0.5, -0.3, -0.1);

            Assert.Equal(-0.5, maneuver.ManDv1);
            Assert.Equal(-0.3, maneuver.ManDv2);
            Assert.Equal(-0.1, maneuver.ManDv3);
        }

        [Fact]
        public void Constructor_ZeroDeltaV_Succeeds()
        {
            var maneuver = new OpmManeuverParameters(
                _testEpoch, 120.0, -50.0, "RSW", 0.0, 0.0, 0.0);

            Assert.Equal(0.0, maneuver.ManDv1);
            Assert.Equal(0.0, maneuver.ManDv2);
            Assert.Equal(0.0, maneuver.ManDv3);
        }

        #endregion

        #region Comments Tests

        [Fact]
        public void Constructor_WithComments_StoresComments()
        {
            var comments = new[] { "Orbit raise maneuver", "Phase 1" };
            var maneuver = new OpmManeuverParameters(
                _testEpoch, 120.0, -50.0, "RSW", 0.1, 0.0, 0.0, comments);

            Assert.Equal(2, maneuver.Comments.Count);
            Assert.Equal("Orbit raise maneuver", maneuver.Comments[0]);
            Assert.Equal("Phase 1", maneuver.Comments[1]);
        }

        [Fact]
        public void Constructor_NullComments_UsesEmptyArray()
        {
            var maneuver = new OpmManeuverParameters(
                _testEpoch, 120.0, -50.0, "RSW", 0.1, 0.0, 0.0, null);

            Assert.NotNull(maneuver.Comments);
            Assert.Empty(maneuver.Comments);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ContainsRelevantInfo()
        {
            var maneuver = new OpmManeuverParameters(
                _testEpoch, 120.0, -50.0, "RSW", 0.1, 0.05, 0.02);

            var result = maneuver.ToString();

            Assert.Contains("OpmManeuver", result);
            Assert.Contains("RSW", result);
        }

        #endregion
    }
}
